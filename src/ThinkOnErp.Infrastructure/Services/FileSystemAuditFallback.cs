using Microsoft.Extensions.Logging;
using System.Text.Json;
using ThinkOnErp.Domain.Entities.Audit;

namespace ThinkOnErp.Infrastructure.Services;

/// <summary>
/// Fallback storage for audit events when database is unavailable.
/// Stores events in structured JSON format with file rotation and replay capability.
/// 
/// Implements Task 16.3: FileSystemAuditFallback for database outages
/// Implements Task 16.4: Fallback event replay mechanism
/// </summary>
public class FileSystemAuditFallback
{
    private readonly string _fallbackPath;
    private readonly ILogger<FileSystemAuditFallback> _logger;
    private readonly FileSystemAuditFallbackOptions _options;
    private readonly SemaphoreSlim _writeLock = new(1, 1);
    private readonly SemaphoreSlim _replayLock = new(1, 1);

    // Metrics
    private long _totalEventsWritten = 0;
    private long _totalEventsReplayed = 0;
    private long _failedWrites = 0;
    private long _failedReplays = 0;

    public FileSystemAuditFallback(
        ILogger<FileSystemAuditFallback> logger,
        FileSystemAuditFallbackOptions? options = null)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _options = options ?? new FileSystemAuditFallbackOptions();
        _fallbackPath = _options.FallbackPath;

        // Ensure fallback directory exists
        EnsureDirectoryExists();
    }

    /// <summary>
    /// Write an audit event to file system as fallback storage.
    /// Events are stored in structured JSON format for later replay.
    /// </summary>
    public async Task WriteAsync(AuditEvent auditEvent, CancellationToken cancellationToken = default)
    {
        if (auditEvent == null)
        {
            _logger.LogWarning("Attempted to write null audit event to fallback storage");
            return;
        }

        await _writeLock.WaitAsync(cancellationToken);
        try
        {
            // Check if rotation is needed before writing
            await RotateFilesIfNeededAsync(cancellationToken);

            // Generate unique filename with timestamp and GUID
            var timestamp = DateTime.UtcNow;
            var fileName = $"audit_fallback_{timestamp:yyyyMMdd_HHmmss}_{Guid.NewGuid():N}.json";
            var filePath = Path.Combine(_fallbackPath, fileName);

            // Create fallback entry with metadata
            var fallbackEntry = new FallbackAuditEntry
            {
                EventType = auditEvent.GetType().Name,
                Event = auditEvent,
                WrittenAt = timestamp,
                ReplayAttempts = 0
            };

            // Serialize to JSON with indentation for readability
            var jsonOptions = new JsonSerializerOptions
            {
                WriteIndented = true,
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            };

            var json = JsonSerializer.Serialize(fallbackEntry, jsonOptions);
            await File.WriteAllTextAsync(filePath, json, cancellationToken);

            Interlocked.Increment(ref _totalEventsWritten);

            _logger.LogInformation(
                "Wrote audit event to fallback storage: {FilePath}, EventType: {EventType}, CorrelationId: {CorrelationId}",
                filePath, auditEvent.GetType().Name, auditEvent.CorrelationId);
        }
        catch (Exception ex)
        {
            Interlocked.Increment(ref _failedWrites);
            _logger.LogError(ex, 
                "Failed to write audit event to fallback storage. EventType: {EventType}, CorrelationId: {CorrelationId}",
                auditEvent.GetType().Name, auditEvent.CorrelationId);
            
            // Last resort: try to write to Windows Event Log or system log
            TryWriteToSystemLog(auditEvent, ex);
        }
        finally
        {
            _writeLock.Release();
        }
    }

    /// <summary>
    /// Write multiple audit events to file system in batch.
    /// </summary>
    public async Task WriteBatchAsync(IEnumerable<AuditEvent> auditEvents, CancellationToken cancellationToken = default)
    {
        if (auditEvents == null || !auditEvents.Any())
        {
            _logger.LogWarning("Attempted to write null or empty batch to fallback storage");
            return;
        }

        await _writeLock.WaitAsync(cancellationToken);
        try
        {
            // Check if rotation is needed before writing
            await RotateFilesIfNeededAsync(cancellationToken);

            // Generate unique filename for batch
            var timestamp = DateTime.UtcNow;
            var fileName = $"audit_fallback_batch_{timestamp:yyyyMMdd_HHmmss}_{Guid.NewGuid():N}.json";
            var filePath = Path.Combine(_fallbackPath, fileName);

            // Create fallback batch entry
            var fallbackBatch = new FallbackAuditBatch
            {
                Events = auditEvents.Select(e => new FallbackAuditEntry
                {
                    EventType = e.GetType().Name,
                    Event = e,
                    WrittenAt = timestamp,
                    ReplayAttempts = 0
                }).ToList(),
                WrittenAt = timestamp,
                EventCount = auditEvents.Count()
            };

            // Serialize to JSON
            var jsonOptions = new JsonSerializerOptions
            {
                WriteIndented = true,
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            };

            var json = JsonSerializer.Serialize(fallbackBatch, jsonOptions);
            await File.WriteAllTextAsync(filePath, json, cancellationToken);

            Interlocked.Add(ref _totalEventsWritten, auditEvents.Count());

            _logger.LogInformation(
                "Wrote {Count} audit events to fallback storage batch: {FilePath}",
                auditEvents.Count(), filePath);
        }
        catch (Exception ex)
        {
            Interlocked.Increment(ref _failedWrites);
            _logger.LogError(ex, "Failed to write audit event batch to fallback storage");
        }
        finally
        {
            _writeLock.Release();
        }
    }

    /// <summary>
    /// Replay fallback events to the database when it becomes available.
    /// Returns the number of successfully replayed events.
    /// </summary>
    public async Task<int> ReplayFallbackEventsAsync(
        Func<AuditEvent, Task> replayAction,
        CancellationToken cancellationToken = default)
    {
        if (replayAction == null)
            throw new ArgumentNullException(nameof(replayAction));

        // Prevent concurrent replay operations
        if (!await _replayLock.WaitAsync(0, cancellationToken))
        {
            _logger.LogWarning("Replay operation already in progress, skipping");
            return 0;
        }

        try
        {
            var files = Directory.GetFiles(_fallbackPath, "audit_fallback_*.json")
                .OrderBy(f => f) // Process oldest files first
                .ToList();

            if (!files.Any())
            {
                _logger.LogDebug("No fallback files found for replay");
                return 0;
            }

            _logger.LogInformation("Starting replay of {Count} fallback files", files.Count);

            int successCount = 0;
            int failureCount = 0;

            foreach (var file in files)
            {
                if (cancellationToken.IsCancellationRequested)
                {
                    _logger.LogWarning("Replay operation cancelled");
                    break;
                }

                try
                {
                    var result = await ReplayFileAsync(file, replayAction, cancellationToken);
                    if (result)
                    {
                        successCount++;
                        Interlocked.Increment(ref _totalEventsReplayed);
                    }
                    else
                    {
                        failureCount++;
                        Interlocked.Increment(ref _failedReplays);
                    }
                }
                catch (Exception ex)
                {
                    failureCount++;
                    Interlocked.Increment(ref _failedReplays);
                    _logger.LogError(ex, "Failed to replay fallback file: {File}", file);
                }
            }

            _logger.LogInformation(
                "Replay completed: {Success} successful, {Failed} failed",
                successCount, failureCount);

            return successCount;
        }
        finally
        {
            _replayLock.Release();
        }
    }

    /// <summary>
    /// Replay a single fallback file.
    /// Returns true if successful, false otherwise.
    /// </summary>
    private async Task<bool> ReplayFileAsync(
        string filePath,
        Func<AuditEvent, Task> replayAction,
        CancellationToken cancellationToken)
    {
        try
        {
            var json = await File.ReadAllTextAsync(filePath, cancellationToken);
            
            // Try to deserialize as batch first
            try
            {
                var batch = JsonSerializer.Deserialize<FallbackAuditBatch>(json);
                if (batch != null && batch.Events != null)
                {
                    foreach (var entry in batch.Events)
                    {
                        if (entry.Event != null)
                        {
                            await replayAction(entry.Event);
                        }
                    }

                    // Delete file after successful replay
                    File.Delete(filePath);
                    _logger.LogInformation("Successfully replayed and deleted batch file: {File}", filePath);
                    return true;
                }
            }
            catch
            {
                // Not a batch, try single entry
            }

            // Try to deserialize as single entry
            var fallbackEntry = JsonSerializer.Deserialize<FallbackAuditEntry>(json);
            if (fallbackEntry?.Event != null)
            {
                await replayAction(fallbackEntry.Event);

                // Delete file after successful replay
                File.Delete(filePath);
                _logger.LogInformation("Successfully replayed and deleted file: {File}", filePath);
                return true;
            }

            _logger.LogWarning("Failed to deserialize fallback file: {File}", filePath);
            
            // Move corrupted file to error directory
            await MoveToErrorDirectoryAsync(filePath, cancellationToken);
            return false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to replay fallback file: {File}", filePath);

            // Check if we should retry or move to error directory
            var entry = await TryReadEntryAsync(filePath, cancellationToken);
            if (entry != null)
            {
                entry.ReplayAttempts++;
                if (entry.ReplayAttempts >= _options.MaxReplayAttempts)
                {
                    _logger.LogWarning(
                        "Max replay attempts ({Max}) reached for file: {File}, moving to error directory",
                        _options.MaxReplayAttempts, filePath);
                    await MoveToErrorDirectoryAsync(filePath, cancellationToken);
                }
                else
                {
                    // Update the file with incremented attempt count
                    await UpdateEntryAsync(filePath, entry, cancellationToken);
                }
            }

            return false;
        }
    }

    /// <summary>
    /// Rotate files if the total size exceeds the configured limit.
    /// </summary>
    private async Task RotateFilesIfNeededAsync(CancellationToken cancellationToken)
    {
        try
        {
            var directory = new DirectoryInfo(_fallbackPath);
            var files = directory.GetFiles("audit_fallback_*.json");

            // Calculate total size
            long totalSize = files.Sum(f => f.Length);

            if (totalSize > _options.MaxTotalSizeBytes)
            {
                _logger.LogWarning(
                    "Fallback storage size ({Size} bytes) exceeds limit ({Limit} bytes), rotating files",
                    totalSize, _options.MaxTotalSizeBytes);

                // Delete oldest files until we're under the limit
                var filesToDelete = files
                    .OrderBy(f => f.CreationTimeUtc)
                    .ToList();

                foreach (var file in filesToDelete)
                {
                    if (totalSize <= _options.MaxTotalSizeBytes * 0.8) // Keep 20% buffer
                        break;

                    try
                    {
                        // Move to archive directory instead of deleting
                        await MoveToArchiveDirectoryAsync(file.FullName, cancellationToken);
                        totalSize -= file.Length;
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Failed to rotate file: {File}", file.FullName);
                    }
                }

                _logger.LogInformation("File rotation completed, new size: {Size} bytes", totalSize);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to check/rotate fallback files");
        }
    }

    /// <summary>
    /// Move a file to the archive directory.
    /// </summary>
    private async Task MoveToArchiveDirectoryAsync(string filePath, CancellationToken cancellationToken)
    {
        var archivePath = Path.Combine(_fallbackPath, "archive");
        Directory.CreateDirectory(archivePath);

        var fileName = Path.GetFileName(filePath);
        var archiveFilePath = Path.Combine(archivePath, fileName);

        // If file already exists in archive, append timestamp
        if (File.Exists(archiveFilePath))
        {
            var timestamp = DateTime.UtcNow.ToString("yyyyMMdd_HHmmss");
            archiveFilePath = Path.Combine(archivePath, $"{timestamp}_{fileName}");
        }

        File.Move(filePath, archiveFilePath);
        _logger.LogInformation("Moved file to archive: {File}", archiveFilePath);
    }

    /// <summary>
    /// Move a corrupted file to the error directory.
    /// </summary>
    private async Task MoveToErrorDirectoryAsync(string filePath, CancellationToken cancellationToken)
    {
        var errorPath = Path.Combine(_fallbackPath, "error");
        Directory.CreateDirectory(errorPath);

        var fileName = Path.GetFileName(filePath);
        var errorFilePath = Path.Combine(errorPath, fileName);

        // If file already exists in error directory, append timestamp
        if (File.Exists(errorFilePath))
        {
            var timestamp = DateTime.UtcNow.ToString("yyyyMMdd_HHmmss");
            errorFilePath = Path.Combine(errorPath, $"{timestamp}_{fileName}");
        }

        File.Move(filePath, errorFilePath);
        _logger.LogWarning("Moved corrupted file to error directory: {File}", errorFilePath);
    }

    /// <summary>
    /// Try to read a fallback entry from file.
    /// </summary>
    private async Task<FallbackAuditEntry?> TryReadEntryAsync(string filePath, CancellationToken cancellationToken)
    {
        try
        {
            var json = await File.ReadAllTextAsync(filePath, cancellationToken);
            return JsonSerializer.Deserialize<FallbackAuditEntry>(json);
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Update a fallback entry in file.
    /// </summary>
    private async Task UpdateEntryAsync(string filePath, FallbackAuditEntry entry, CancellationToken cancellationToken)
    {
        try
        {
            var jsonOptions = new JsonSerializerOptions
            {
                WriteIndented = true,
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            };

            var json = JsonSerializer.Serialize(entry, jsonOptions);
            await File.WriteAllTextAsync(filePath, json, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to update fallback entry: {File}", filePath);
        }
    }

    /// <summary>
    /// Ensure the fallback directory exists.
    /// </summary>
    private void EnsureDirectoryExists()
    {
        try
        {
            if (!Directory.Exists(_fallbackPath))
            {
                Directory.CreateDirectory(_fallbackPath);
                _logger.LogInformation("Created fallback directory: {Path}", _fallbackPath);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create fallback directory: {Path}", _fallbackPath);
            throw;
        }
    }

    /// <summary>
    /// Last resort: write to system event log.
    /// </summary>
    private void TryWriteToSystemLog(AuditEvent auditEvent, Exception originalException)
    {
        try
        {
            var message = $"CRITICAL: Failed to write audit event to fallback storage. " +
                         $"EventType: {auditEvent.GetType().Name}, " +
                         $"CorrelationId: {auditEvent.CorrelationId}, " +
                         $"Error: {originalException.Message}";

            // On Windows, write to Event Log
            if (OperatingSystem.IsWindows())
            {
                System.Diagnostics.EventLog.WriteEntry("ThinkOnErp", message, 
                    System.Diagnostics.EventLogEntryType.Error);
            }
            else
            {
                // On Linux, write to syslog (via console which systemd captures)
                Console.Error.WriteLine($"[CRITICAL] {message}");
            }
        }
        catch
        {
            // If even this fails, there's nothing more we can do
        }
    }

    /// <summary>
    /// Get metrics for monitoring.
    /// </summary>
    public FileSystemAuditFallbackMetrics GetMetrics()
    {
        var directory = new DirectoryInfo(_fallbackPath);
        var files = directory.Exists ? directory.GetFiles("audit_fallback_*.json") : Array.Empty<FileInfo>();
        var totalSize = files.Sum(f => f.Length);

        return new FileSystemAuditFallbackMetrics
        {
            TotalEventsWritten = _totalEventsWritten,
            TotalEventsReplayed = _totalEventsReplayed,
            FailedWrites = _failedWrites,
            FailedReplays = _failedReplays,
            PendingFiles = files.Length,
            TotalSizeBytes = totalSize,
            FallbackPath = _fallbackPath
        };
    }

    /// <summary>
    /// Get count of pending fallback files.
    /// </summary>
    public int GetPendingFileCount()
    {
        try
        {
            return Directory.GetFiles(_fallbackPath, "audit_fallback_*.json").Length;
        }
        catch
        {
            return 0;
        }
    }

    /// <summary>
    /// Clear all fallback files (use with caution).
    /// </summary>
    public async Task ClearAllAsync(CancellationToken cancellationToken = default)
    {
        await _writeLock.WaitAsync(cancellationToken);
        try
        {
            var files = Directory.GetFiles(_fallbackPath, "audit_fallback_*.json");
            foreach (var file in files)
            {
                File.Delete(file);
            }

            _logger.LogWarning("Cleared {Count} fallback files", files.Length);
        }
        finally
        {
            _writeLock.Release();
        }
    }
}

/// <summary>
/// Configuration options for FileSystemAuditFallback.
/// </summary>
public class FileSystemAuditFallbackOptions
{
    /// <summary>
    /// Path to store fallback audit files.
    /// Default: "logs/audit-fallback"
    /// </summary>
    public string FallbackPath { get; set; } = "logs/audit-fallback";

    /// <summary>
    /// Maximum total size of fallback files in bytes before rotation.
    /// Default: 100 MB
    /// </summary>
    public long MaxTotalSizeBytes { get; set; } = 100 * 1024 * 1024; // 100 MB

    /// <summary>
    /// Maximum number of replay attempts before moving to error directory.
    /// Default: 3
    /// </summary>
    public int MaxReplayAttempts { get; set; } = 3;
}

/// <summary>
/// Fallback audit entry with metadata.
/// </summary>
public class FallbackAuditEntry
{
    public string EventType { get; set; } = null!;
    public AuditEvent Event { get; set; } = null!;
    public DateTime WrittenAt { get; set; }
    public int ReplayAttempts { get; set; }
}

/// <summary>
/// Fallback audit batch entry.
/// </summary>
public class FallbackAuditBatch
{
    public List<FallbackAuditEntry> Events { get; set; } = new();
    public DateTime WrittenAt { get; set; }
    public int EventCount { get; set; }
}

/// <summary>
/// Metrics for FileSystemAuditFallback monitoring.
/// </summary>
public class FileSystemAuditFallbackMetrics
{
    public long TotalEventsWritten { get; set; }
    public long TotalEventsReplayed { get; set; }
    public long FailedWrites { get; set; }
    public long FailedReplays { get; set; }
    public int PendingFiles { get; set; }
    public long TotalSizeBytes { get; set; }
    public string FallbackPath { get; set; } = null!;
}
