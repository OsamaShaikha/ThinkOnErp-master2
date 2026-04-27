using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ThinkOnErp.Domain.Interfaces;
using ThinkOnErp.Infrastructure.Configuration;

namespace ThinkOnErp.Infrastructure.Services;

/// <summary>
/// Service for generating and verifying cryptographic hashes for audit log integrity.
/// Uses HMAC-SHA256 or HMAC-SHA512 for tamper detection through hash comparison.
/// Provides comprehensive audit trail integrity verification capabilities.
/// </summary>
public class AuditLogIntegrityService : IAuditLogIntegrityService
{
    private readonly byte[] _signingKey;
    private readonly ILogger<AuditLogIntegrityService> _logger;
    private readonly AuditIntegrityOptions _options;
    private readonly IAuditRepository _auditRepository;
    private readonly IAlertManager? _alertManager;
    private readonly SemaphoreSlim _verificationLock;

    /// <summary>
    /// Initializes a new instance of the AuditLogIntegrityService.
    /// </summary>
    /// <param name="options">Integrity verification configuration options</param>
    /// <param name="logger">Logger instance</param>
    /// <param name="auditRepository">Repository for accessing audit log data</param>
    /// <param name="alertManager">Optional alert manager for tampering notifications</param>
    /// <exception cref="InvalidOperationException">Thrown when signing key is not configured or invalid</exception>
    public AuditLogIntegrityService(
        IOptions<AuditIntegrityOptions> options,
        ILogger<AuditLogIntegrityService> logger,
        IAuditRepository auditRepository,
        IAlertManager? alertManager = null)
    {
        _options = options?.Value ?? throw new ArgumentNullException(nameof(options));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _auditRepository = auditRepository ?? throw new ArgumentNullException(nameof(auditRepository));
        _alertManager = alertManager;

        if (string.IsNullOrWhiteSpace(_options.SigningKey))
        {
            throw new InvalidOperationException(
                "Audit integrity signing key not configured. Please set AuditIntegrity:SigningKey in configuration.");
        }

        try
        {
            _signingKey = Convert.FromBase64String(_options.SigningKey);

            // Validate key length (must be at least 32 bytes for security)
            if (_signingKey.Length < 32)
            {
                throw new InvalidOperationException(
                    $"Signing key must be at least 32 bytes (256 bits). Current key is {_signingKey.Length} bytes. " +
                    "Generate a valid key using: Convert.ToBase64String(RandomNumberGenerator.GetBytes(32))");
            }
        }
        catch (FormatException ex)
        {
            throw new InvalidOperationException("Signing key is not a valid Base64 string.", ex);
        }

        // Initialize semaphore for thread-safe batch operations (allow up to 5 concurrent verifications)
        _verificationLock = new SemaphoreSlim(5, 5);

        _logger.LogInformation(
            "AuditLogIntegrityService initialized with {Algorithm} algorithm",
            _options.HashAlgorithm);
    }

    /// <summary>
    /// Generates a cryptographic hash for an audit log entry using HMAC-SHA256 or HMAC-SHA512.
    /// The hash is computed over critical fields to detect any tampering.
    /// </summary>
    public string GenerateIntegrityHash(
        long rowId,
        long actorId,
        string action,
        string entityType,
        long? entityId,
        DateTime creationDate,
        string? oldValue = null,
        string? newValue = null)
    {
        if (string.IsNullOrWhiteSpace(action))
            throw new ArgumentException("Action cannot be null or empty", nameof(action));

        if (string.IsNullOrWhiteSpace(entityType))
            throw new ArgumentException("EntityType cannot be null or empty", nameof(entityType));

        if (!_options.Enabled)
        {
            _logger.LogWarning("Integrity verification is disabled. Returning empty hash.");
            return string.Empty;
        }

        try
        {
            // Create canonical representation of audit entry
            // Format: rowId|actorId|action|entityType|entityId|creationDate|oldValue|newValue
            var canonical = $"{rowId}|{actorId}|{action}|{entityType}|{entityId?.ToString() ?? "NULL"}|" +
                           $"{creationDate:O}|{oldValue ?? "NULL"}|{newValue ?? "NULL"}";

            byte[] hash;
            using (var hmac = CreateHmacAlgorithm())
            {
                hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(canonical));
            }

            var hashString = Convert.ToBase64String(hash);

            if (_options.LogIntegrityOperations)
            {
                _logger.LogDebug(
                    "Generated integrity hash for audit log {RowId}. Canonical length: {Length}, Hash: {Hash}",
                    rowId, canonical.Length, hashString);
            }

            return hashString;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to generate integrity hash for audit log {RowId}", rowId);
            throw new InvalidOperationException(
                $"Integrity hash generation failed for audit log {rowId}. See inner exception for details.", ex);
        }
    }

    /// <summary>
    /// Verifies the integrity of an audit log entry by comparing the stored hash
    /// with a newly computed hash. Returns true if hashes match (no tampering detected).
    /// </summary>
    public bool VerifyIntegrityHash(
        long rowId,
        long actorId,
        string action,
        string entityType,
        long? entityId,
        DateTime creationDate,
        string? oldValue,
        string? newValue,
        string storedHash)
    {
        if (string.IsNullOrWhiteSpace(storedHash))
        {
            _logger.LogWarning("Stored hash is empty for audit log {RowId}. Cannot verify integrity.", rowId);
            return false;
        }

        if (!_options.Enabled)
        {
            _logger.LogWarning("Integrity verification is disabled. Skipping verification.");
            return true; // Consider valid when verification is disabled
        }

        try
        {
            var computedHash = GenerateIntegrityHash(
                rowId, actorId, action, entityType, entityId, creationDate, oldValue, newValue);

            var isValid = storedHash == computedHash;

            if (_options.LogIntegrityOperations)
            {
                _logger.LogDebug(
                    "Integrity verification for audit log {RowId}: {Result}. Stored: {StoredHash}, Computed: {ComputedHash}",
                    rowId, isValid ? "VALID" : "TAMPERED", storedHash, computedHash);
            }

            if (!isValid)
            {
                _logger.LogWarning(
                    "TAMPERING DETECTED: Audit log {RowId} integrity verification failed. " +
                    "Stored hash does not match computed hash.",
                    rowId);
            }

            return isValid;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to verify integrity hash for audit log {RowId}", rowId);
            return false; // Consider invalid on verification error
        }
    }

    /// <summary>
    /// Asynchronously verifies the integrity of an audit log entry from the database.
    /// Retrieves the entry and compares its stored hash with a computed hash.
    /// </summary>
    public async Task<bool> VerifyAuditLogIntegrityAsync(
        long auditLogId,
        CancellationToken cancellationToken = default)
    {
        if (!_options.Enabled)
        {
            _logger.LogWarning("Integrity verification is disabled. Skipping verification.");
            return true;
        }

        try
        {
            // Retrieve audit log entry from database
            var auditLog = await _auditRepository.GetByIdAsync(auditLogId, cancellationToken);

            if (auditLog == null)
            {
                _logger.LogWarning("Audit log {AuditLogId} not found. Cannot verify integrity.", auditLogId);
                return false;
            }

            // Extract stored hash from metadata (assuming it's stored in Metadata field as JSON)
            var storedHash = ExtractStoredHash(auditLog.Metadata);

            if (string.IsNullOrWhiteSpace(storedHash))
            {
                _logger.LogWarning(
                    "No integrity hash found for audit log {AuditLogId}. Entry may predate integrity verification.",
                    auditLogId);
                return false;
            }

            // Verify integrity
            var isValid = VerifyIntegrityHash(
                auditLog.RowId,
                auditLog.ActorId,
                auditLog.Action,
                auditLog.EntityType,
                auditLog.EntityId,
                auditLog.CreationDate,
                auditLog.OldValue,
                auditLog.NewValue,
                storedHash);

            // Trigger alert if tampering detected
            if (!isValid && _options.AlertOnTampering && _alertManager != null)
            {
                await TriggerTamperingAlertAsync(auditLogId, cancellationToken);
            }

            return isValid;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to verify audit log integrity for {AuditLogId}", auditLogId);
            return false;
        }
    }

    /// <summary>
    /// Batch verifies the integrity of multiple audit log entries.
    /// Useful for periodic integrity checks across the audit log.
    /// </summary>
    public async Task<Dictionary<long, bool>> VerifyBatchIntegrityAsync(
        IEnumerable<long> auditLogIds,
        CancellationToken cancellationToken = default)
    {
        if (auditLogIds == null)
            throw new ArgumentNullException(nameof(auditLogIds));

        var results = new Dictionary<long, bool>();
        var idList = auditLogIds.ToList();

        if (idList.Count == 0)
            return results;

        if (!_options.Enabled)
        {
            _logger.LogWarning("Integrity verification is disabled. Returning all entries as valid.");
            return idList.ToDictionary(id => id, _ => true);
        }

        _logger.LogInformation("Starting batch integrity verification for {Count} audit log entries", idList.Count);

        // Acquire semaphore for thread-safe operation
        await _verificationLock.WaitAsync(cancellationToken);

        try
        {
            // Process in batches to avoid overwhelming the database
            var batches = idList.Chunk(_options.BatchSize);

            foreach (var batch in batches)
            {
                var tasks = batch.Select(async id =>
                {
                    var isValid = await VerifyAuditLogIntegrityAsync(id, cancellationToken);
                    return new KeyValuePair<long, bool>(id, isValid);
                });

                var batchResults = await Task.WhenAll(tasks);

                foreach (var result in batchResults)
                {
                    results[result.Key] = result.Value;
                }

                // Small delay between batches to avoid overwhelming the system
                if (cancellationToken.IsCancellationRequested)
                    break;

                await Task.Delay(100, cancellationToken);
            }

            var tamperedCount = results.Count(r => !r.Value);
            if (tamperedCount > 0)
            {
                _logger.LogWarning(
                    "Batch integrity verification completed. {TamperedCount} of {TotalCount} entries show tampering.",
                    tamperedCount, results.Count);
            }
            else
            {
                _logger.LogInformation(
                    "Batch integrity verification completed. All {TotalCount} entries are valid.",
                    results.Count);
            }

            return results;
        }
        finally
        {
            _verificationLock.Release();
        }
    }

    /// <summary>
    /// Detects tampering by scanning audit logs within a date range.
    /// Returns a list of audit log IDs where tampering was detected.
    /// </summary>
    public async Task<List<long>> DetectTamperingAsync(
        DateTime startDate,
        DateTime endDate,
        CancellationToken cancellationToken = default)
    {
        if (startDate > endDate)
            throw new ArgumentException("Start date must be before or equal to end date");

        if (!_options.Enabled)
        {
            _logger.LogWarning("Integrity verification is disabled. Returning empty tampering list.");
            return new List<long>();
        }

        _logger.LogInformation(
            "Starting tampering detection scan from {StartDate} to {EndDate}",
            startDate, endDate);

        try
        {
            // Get all audit log IDs in the date range
            var auditLogIds = await _auditRepository.GetAuditLogIdsByDateRangeAsync(
                startDate, endDate, cancellationToken);

            if (!auditLogIds.Any())
            {
                _logger.LogInformation("No audit logs found in the specified date range.");
                return new List<long>();
            }

            _logger.LogInformation(
                "Found {Count} audit log entries to verify in date range",
                auditLogIds.Count());

            // Verify integrity for all entries
            var verificationResults = await VerifyBatchIntegrityAsync(auditLogIds, cancellationToken);

            // Filter to only tampered entries
            var tamperedIds = verificationResults
                .Where(r => !r.Value)
                .Select(r => r.Key)
                .ToList();

            if (tamperedIds.Any())
            {
                _logger.LogWarning(
                    "TAMPERING DETECTED: {TamperedCount} audit log entries show signs of tampering in date range {StartDate} to {EndDate}",
                    tamperedIds.Count, startDate, endDate);

                // Trigger alert for detected tampering
                if (_options.AlertOnTampering && _alertManager != null)
                {
                    await TriggerBatchTamperingAlertAsync(tamperedIds, startDate, endDate, cancellationToken);
                }
            }
            else
            {
                _logger.LogInformation(
                    "Tampering detection scan completed. No tampering detected in {Count} entries.",
                    verificationResults.Count);
            }

            return tamperedIds;
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Failed to complete tampering detection scan for date range {StartDate} to {EndDate}",
                startDate, endDate);
            throw;
        }
    }

    /// <summary>
    /// Creates the appropriate HMAC algorithm based on configuration.
    /// </summary>
    private HMAC CreateHmacAlgorithm()
    {
        return _options.HashAlgorithm switch
        {
            "HMACSHA512" => new HMACSHA512(_signingKey),
            "HMACSHA256" => new HMACSHA256(_signingKey),
            _ => new HMACSHA256(_signingKey) // Default to SHA256
        };
    }

    /// <summary>
    /// Extracts the stored integrity hash from the audit log metadata JSON.
    /// </summary>
    private string? ExtractStoredHash(string? metadata)
    {
        if (string.IsNullOrWhiteSpace(metadata))
            return null;

        try
        {
            // Parse metadata JSON and extract integrity_hash field
            var json = System.Text.Json.JsonDocument.Parse(metadata);
            if (json.RootElement.TryGetProperty("integrity_hash", out var hashElement))
            {
                return hashElement.GetString();
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to extract integrity hash from metadata");
        }

        return null;
    }

    /// <summary>
    /// Triggers an alert when tampering is detected on a single audit log entry.
    /// </summary>
    private async Task TriggerTamperingAlertAsync(long auditLogId, CancellationToken cancellationToken)
    {
        if (_alertManager == null)
            return;

        try
        {
            var alert = new Domain.Models.Alert
            {
                AlertType = "AuditLogTampering",
                Severity = "Critical",
                Title = "Audit Log Tampering Detected",
                Description = $"Integrity verification failed for audit log entry {auditLogId}. " +
                         "The entry may have been tampered with or corrupted.",
                Metadata = System.Text.Json.JsonSerializer.Serialize(new
                {
                    AuditLogId = auditLogId,
                    DetectionTime = DateTime.UtcNow,
                    Source = "AuditLogIntegrityService"
                })
            };

            await _alertManager.TriggerAlertAsync(alert);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to trigger tampering alert for audit log {AuditLogId}", auditLogId);
        }
    }

    /// <summary>
    /// Triggers an alert when tampering is detected on multiple audit log entries.
    /// </summary>
    private async Task TriggerBatchTamperingAlertAsync(
        List<long> tamperedIds,
        DateTime startDate,
        DateTime endDate,
        CancellationToken cancellationToken)
    {
        if (_alertManager == null || !tamperedIds.Any())
            return;

        try
        {
            var alert = new Domain.Models.Alert
            {
                AlertType = "AuditLogTamperingBatch",
                Severity = "Critical",
                Title = "Multiple Audit Log Tampering Detected",
                Description = $"Integrity verification failed for {tamperedIds.Count} audit log entries " +
                         $"in date range {startDate:yyyy-MM-dd} to {endDate:yyyy-MM-dd}. " +
                         "Multiple entries may have been tampered with or corrupted.",
                Metadata = System.Text.Json.JsonSerializer.Serialize(new
                {
                    TamperedCount = tamperedIds.Count,
                    TamperedIds = tamperedIds.Take(10).ToList(), // Include first 10 IDs
                    StartDate = startDate,
                    EndDate = endDate,
                    DetectionTime = DateTime.UtcNow,
                    Source = "AuditLogIntegrityService"
                })
            };

            await _alertManager.TriggerAlertAsync(alert);
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Failed to trigger batch tampering alert for {Count} audit logs",
                tamperedIds.Count);
        }
    }
}
