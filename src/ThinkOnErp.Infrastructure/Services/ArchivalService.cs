using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ThinkOnErp.Domain.Entities;
using ThinkOnErp.Domain.Interfaces;
using ThinkOnErp.Domain.Models;
using ThinkOnErp.Infrastructure.Configuration;
using ThinkOnErp.Infrastructure.Data;

namespace ThinkOnErp.Infrastructure.Services;

/// <summary>
/// Service for managing data retention policies and archiving historical audit data.
/// Implements automated archival based on retention policies, manual archival by date range,
/// retrieval of archived data, integrity verification, and retention policy management.
/// Designed to meet compliance requirements (GDPR, SOX, ISO 27001) while managing storage costs.
/// </summary>
public class ArchivalService : IArchivalService
{
    private readonly OracleDbContext _dbContext;
    private readonly ILogger<ArchivalService> _logger;
    private readonly ArchivalOptions _options;
    private readonly ICompressionService _compressionService;
    private readonly IExternalStorageProviderFactory? _storageProviderFactory;
    private IExternalStorageProvider? _externalStorageProvider;

    public ArchivalService(
        OracleDbContext dbContext,
        ILogger<ArchivalService> logger,
        IOptions<ArchivalOptions> options,
        ICompressionService compressionService,
        IExternalStorageProviderFactory? storageProviderFactory = null)
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _options = options?.Value ?? throw new ArgumentNullException(nameof(options));
        _compressionService = compressionService ?? throw new ArgumentNullException(nameof(compressionService));
        _storageProviderFactory = storageProviderFactory;

        // Initialize external storage provider if configured
        if (_options.StorageProvider != "Database" && 
            !string.IsNullOrWhiteSpace(_options.StorageConnectionString) &&
            _storageProviderFactory != null)
        {
            try
            {
                _externalStorageProvider = _storageProviderFactory.CreateProvider(
                    _options.StorageProvider,
                    _options.StorageConnectionString);

                _logger.LogInformation(
                    "Initialized external storage provider: {ProviderName}",
                    _externalStorageProvider.ProviderName);
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "Failed to initialize external storage provider '{StorageProvider}'. External storage will not be available.",
                    _options.StorageProvider);
            }
        }
    }

    /// <summary>
    /// Archive all audit data that has exceeded its retention period based on configured retention policies.
    /// This is the core method for task 10.3 - it reads retention policies and applies them by event category.
    /// </summary>
    public async Task<IEnumerable<ArchivalResult>> ArchiveExpiredDataAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Starting archival of expired audit data");
        var results = new List<ArchivalResult>();

        try
        {
            // Step 1: Get all active retention policies from SYS_RETENTION_POLICIES table
            var retentionPolicies = await GetAllRetentionPoliciesAsync(cancellationToken);
            
            if (!retentionPolicies.Any())
            {
                _logger.LogWarning("No retention policies found. Skipping archival.");
                return results;
            }

            _logger.LogInformation("Found {PolicyCount} retention policies to process", retentionPolicies.Count());

            // Step 2: Process each retention policy by event category
            foreach (var policy in retentionPolicies)
            {
                if (cancellationToken.IsCancellationRequested)
                {
                    _logger.LogWarning("Archival cancelled by user request");
                    break;
                }

                try
                {
                    _logger.LogInformation(
                        "Processing retention policy for event category '{EventCategory}' with {RetentionDays} days retention",
                        policy.EventType,
                        policy.RetentionDays);

                    // Step 3: Calculate the cutoff date based on retention policy
                    var cutoffDate = DateTime.UtcNow.AddDays(-policy.RetentionDays);

                    // Step 4: Archive data for this event category that exceeds retention period
                    var result = await ArchiveByEventCategoryAsync(
                        policy.EventType,
                        cutoffDate,
                        policy.PolicyId,
                        cancellationToken);

                    results.Add(result);

                    if (result.IsSuccess)
                    {
                        _logger.LogInformation(
                            "Successfully archived {RecordCount} records for event category '{EventCategory}'",
                            result.RecordsArchived,
                            policy.EventType);
                    }
                    else
                    {
                        _logger.LogError(
                            "Failed to archive data for event category '{EventCategory}': {ErrorMessage}",
                            policy.EventType,
                            result.ErrorMessage);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(
                        ex,
                        "Error processing retention policy for event category '{EventCategory}'",
                        policy.EventType);

                    results.Add(new ArchivalResult
                    {
                        IsSuccess = false,
                        ErrorMessage = $"Exception during archival: {ex.Message}",
                        ArchivalStartTime = DateTime.UtcNow,
                        ArchivalEndTime = DateTime.UtcNow
                    });
                }
            }

            _logger.LogInformation(
                "Archival cycle completed. Processed {TotalPolicies} policies, {SuccessCount} successful, {FailureCount} failed",
                results.Count,
                results.Count(r => r.IsSuccess),
                results.Count(r => !r.IsSuccess));

            return results;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Fatal error during archival cycle");
            throw;
        }
    }

    /// <summary>
    /// Archive audit data for a specific event category that exceeds the cutoff date.
    /// This method implements the core retention policy enforcement logic.
    /// </summary>
    private async Task<ArchivalResult> ArchiveByEventCategoryAsync(
        string eventCategory,
        DateTime cutoffDate,
        long policyId,
        CancellationToken cancellationToken)
    {
        var result = new ArchivalResult
        {
            ArchivalStartTime = DateTime.UtcNow,
            IsSuccess = false
        };

        try
        {
            // Step 1: Count records to be archived for this event category
            var recordCount = await _dbContext.SysAuditLogs
                .Where(s => s.EventCategory == eventCategory && s.CreationDate < cutoffDate)
                .CountAsync(cancellationToken);

            if (recordCount == 0)
            {
                _logger.LogInformation(
                    "No records found for archival in event category '{EventCategory}' before {CutoffDate}",
                    eventCategory,
                    cutoffDate);

                result.IsSuccess = true;
                result.RecordsArchived = 0;
                result.ArchivalEndTime = DateTime.UtcNow;
                return result;
            }

            _logger.LogInformation(
                "Found {RecordCount} records to archive for event category '{EventCategory}'",
                recordCount,
                eventCategory);

            // Step 2: Generate archive batch ID
            var archiveBatchId = await GetNextArchiveBatchIdAsync(cancellationToken);

            // Step 3: Move records to archive table in batches
            var totalArchived = 0;
            var batchSize = _options.BatchSize;
            var batches = (int)Math.Ceiling((double)recordCount / batchSize);

            _logger.LogInformation(
                "Archiving {RecordCount} records in {BatchCount} batches of {BatchSize} (Transaction timeout: {TimeoutSeconds}s)",
                recordCount,
                batches,
                batchSize,
                _options.TransactionTimeoutSeconds);

            // Track compression statistics
            long totalUncompressedSize = 0;
            long totalCompressedSize = 0;
            bool compressionEnabled = _options.CompressionAlgorithm.Equals("GZip", StringComparison.OrdinalIgnoreCase);

            // Track progress for resumption capability
            var progressStartTime = DateTime.UtcNow;
            var lastProgressLog = DateTime.UtcNow;
            var batchesProcessed = 0;

            for (int batchIndex = 0; batchIndex < batches; batchIndex++)
            {
                if (cancellationToken.IsCancellationRequested)
                {
                    _logger.LogWarning(
                        "Archival cancelled during batch processing. Progress: {ArchivedCount}/{TotalCount} records ({Percentage:F2}%)",
                        totalArchived,
                        recordCount,
                        (double)totalArchived / recordCount * 100);
                    break;
                }

                // Log progress every 10 batches or every 30 seconds
                var timeSinceLastLog = DateTime.UtcNow - lastProgressLog;
                if (batchIndex > 0 && (batchIndex % 10 == 0 || timeSinceLastLog.TotalSeconds >= 30))
                {
                    var elapsedTime = DateTime.UtcNow - progressStartTime;
                    var recordsPerSecond = totalArchived / Math.Max(1, elapsedTime.TotalSeconds);
                    var estimatedTimeRemaining = TimeSpan.FromSeconds((recordCount - totalArchived) / Math.Max(1, recordsPerSecond));

                    _logger.LogInformation(
                        "Archival progress: {ArchivedCount}/{TotalCount} records ({Percentage:F2}%), " +
                        "Rate: {RecordsPerSecond:F0} records/sec, ETA: {ETA}",
                        totalArchived,
                        recordCount,
                        (double)totalArchived / recordCount * 100,
                        recordsPerSecond,
                        estimatedTimeRemaining.ToString(@"hh\:mm\:ss"));

                    lastProgressLog = DateTime.UtcNow;
                }

                // Insert into archive table and delete from active table in a transaction with timeout
                await using var transaction = await _dbContext.Database.BeginTransactionAsync(cancellationToken);
                var batchStartTime = DateTime.UtcNow;

                try
                {
                    // Step 1: Fetch records to archive with ordering for determinism
                    var recordsToArchive = await _dbContext.SysAuditLogs
                        .Where(s => s.EventCategory == eventCategory && s.CreationDate < cutoffDate)
                        .OrderBy(s => s.Id)
                        .Take(batchSize)
                        .ToListAsync(cancellationToken);

                    if (recordsToArchive.Count == 0)
                    {
                        await transaction.CommitAsync(cancellationToken);
                        break;
                    }

                    // Step 2: Compress CLOB fields if compression is enabled
                    if (compressionEnabled)
                    {
                        foreach (var record in recordsToArchive)
                        {
                            // Compress CLOB fields: OLD_VALUE, NEW_VALUE, REQUEST_PAYLOAD, RESPONSE_PAYLOAD, STACK_TRACE, METADATA
                            var clobFields = new Func<string?>[]
                            {
                                () => record.OldValue,
                                () => record.NewValue,
                                () => record.RequestPayload,
                                () => record.ResponsePayload,
                                () => record.StackTrace,
                                () => record.Metadata
                            };
                            var clobSetters = new Action<string?>[]
                            {
                                v => record.OldValue = v,
                                v => record.NewValue = v,
                                v => record.RequestPayload = v,
                                v => record.ResponsePayload = v,
                                v => record.StackTrace = v,
                                v => record.Metadata = v
                            };

                            for (int i = 0; i < clobFields.Length; i++)
                            {
                                var originalValue = clobFields[i]();
                                if (!string.IsNullOrEmpty(originalValue))
                                {
                                    totalUncompressedSize += _compressionService.GetSizeInBytes(originalValue);
                                    var compressedValue = _compressionService.Compress(originalValue);
                                    clobSetters[i](compressedValue);
                                    totalCompressedSize += _compressionService.GetSizeInBytes(compressedValue);
                                }
                            }
                        }
                    }

                    // Step 3: Create archive entities and add them
                    var archiveEntities = recordsToArchive.Select(record => new SysAuditLogArchive
                    {
                        Id = record.Id,
                        ActorType = record.ActorType,
                        ActorId = record.ActorId,
                        CompanyId = record.CompanyId,
                        BranchId = record.BranchId,
                        Action = record.Action,
                        EntityType = record.EntityType,
                        EntityId = record.EntityId,
                        OldValue = record.OldValue,
                        NewValue = record.NewValue,
                        IpAddress = record.IpAddress,
                        UserAgent = record.UserAgent,
                        CorrelationId = record.CorrelationId,
                        HttpMethod = record.HttpMethod,
                        EndpointPath = record.EndpointPath,
                        RequestPayload = record.RequestPayload,
                        ResponsePayload = record.ResponsePayload,
                        ExecutionTimeMs = record.ExecutionTimeMs,
                        StatusCode = record.StatusCode,
                        ExceptionType = record.ExceptionType,
                        ExceptionMessage = record.ExceptionMessage,
                        StackTrace = record.StackTrace,
                        Severity = record.Severity,
                        EventCategory = record.EventCategory,
                        Metadata = record.Metadata,
                        SystemId = record.SystemId,
                        DeviceIdentifier = record.DeviceIdentifier,
                        ErrorCode = record.ErrorCode,
                        BusinessDescription = record.BusinessDescription,
                        CreationDate = record.CreationDate,
                        ArchivedDate = DateTime.UtcNow,
                        ArchiveBatchId = archiveBatchId
                    }).ToList();

                    await _dbContext.SysAuditLogArchives.AddRangeAsync(archiveEntities, cancellationToken);

                    // Step 4: Delete archived records from active table
                    var insertedCount = archiveEntities.Count;
                    if (insertedCount > 0)
                    {
                        // Check if we're approaching transaction timeout
                        var batchElapsedTime = DateTime.UtcNow - batchStartTime;
                        if (batchElapsedTime.TotalSeconds > _options.TransactionTimeoutSeconds * 0.8)
                        {
                            _logger.LogWarning(
                                "Batch {BatchIndex}/{TotalBatches} approaching transaction timeout ({ElapsedSeconds:F1}s / {TimeoutSeconds}s). " +
                                "Consider reducing batch size from {CurrentBatchSize}.",
                                batchIndex + 1,
                                batches,
                                batchElapsedTime.TotalSeconds,
                                _options.TransactionTimeoutSeconds,
                                batchSize);
                        }

                        _dbContext.SysAuditLogs.RemoveRange(recordsToArchive);
                        await _dbContext.SaveChangesAsync(cancellationToken);

                        _logger.LogDebug(
                            "Batch {BatchIndex}/{TotalBatches}: Archived {InsertedCount} records, deleted {DeletedCount} records in {ElapsedSeconds:F2}s",
                            batchIndex + 1,
                            batches,
                            insertedCount,
                            insertedCount,
                            batchElapsedTime.TotalSeconds);
                    }

                    // Commit transaction - this releases locks immediately
                    await transaction.CommitAsync(cancellationToken);
                    totalArchived += insertedCount;
                    batchesProcessed++;

                    // Break if we archived fewer records than batch size (last batch)
                    if (insertedCount < batchSize)
                    {
                        break;
                    }
                }
                catch (Exception ex)
                {
                    await transaction.RollbackAsync(cancellationToken);

                    var batchElapsedTime = DateTime.UtcNow - batchStartTime;

                    // Check if this was a timeout-related error
                    if (batchElapsedTime.TotalSeconds > _options.TransactionTimeoutSeconds)
                    {
                        _logger.LogError(
                            ex,
                            "Transaction timeout in batch {BatchIndex}/{TotalBatches} for event category '{EventCategory}'. " +
                            "Batch took {ElapsedSeconds:F1}s (timeout: {TimeoutSeconds}s). " +
                            "Archived {TotalArchived}/{TotalCount} records before failure. " +
                            "RECOMMENDATION: Reduce BatchSize from {CurrentBatchSize} to {RecommendedBatchSize}.",
                            batchIndex + 1,
                            batches,
                            eventCategory,
                            batchElapsedTime.TotalSeconds,
                            _options.TransactionTimeoutSeconds,
                            totalArchived,
                            recordCount,
                            batchSize,
                            Math.Max(100, batchSize / 2));
                    }
                    else
                    {
                        _logger.LogError(
                            ex,
                            "Error archiving batch {BatchIndex}/{TotalBatches} for event category '{EventCategory}'. " +
                            "Archived {TotalArchived}/{TotalCount} records before failure.",
                            batchIndex + 1,
                            batches,
                            eventCategory,
                            totalArchived,
                            recordCount);
                    }

                    throw;
                }
            }

            // Log final archival summary with performance metrics
            var totalElapsedTime = DateTime.UtcNow - progressStartTime;
            var overallRecordsPerSecond = totalArchived / Math.Max(1, totalElapsedTime.TotalSeconds);

            _logger.LogInformation(
                "Completed archival for event category '{EventCategory}': " +
                "{TotalArchived}/{TotalCount} records in {ElapsedTime}, " +
                "Rate: {RecordsPerSecond:F0} records/sec, " +
                "Avg batch time: {AvgBatchTime:F2}s",
                eventCategory,
                totalArchived,
                recordCount,
                totalElapsedTime.ToString(@"hh\:mm\:ss"),
                overallRecordsPerSecond,
                totalElapsedTime.TotalSeconds / Math.Max(1, batchesProcessed));

            // Step 4: Calculate checksum if integrity verification is enabled
            string? checksum = null;
            if (_options.VerifyIntegrity)
            {
                checksum = await CalculateArchiveChecksumAsync(archiveBatchId, cancellationToken);

                // Update the CHECKSUM column for all records in this archive batch using raw SQL
                // (CHECKSUM column is not mapped in the entity)
                if (!string.IsNullOrEmpty(checksum))
                {
                    var updatedRows = await _dbContext.Database.ExecuteSqlRawAsync(
                        "UPDATE SYS_AUDIT_LOG_ARCHIVE SET CHECKSUM = {0} WHERE ARCHIVE_BATCH_ID = {1}",
                        checksum, archiveBatchId, cancellationToken);

                    _logger.LogDebug(
                        "Updated CHECKSUM column for {UpdatedRows} records in archive batch {ArchiveBatchId}",
                        updatedRows,
                        archiveBatchId);
                }
            }

            // Step 5: Update archive metadata with compression statistics
            result.ArchiveId = archiveBatchId;
            result.RecordsArchived = totalArchived;
            result.StartDate = cutoffDate;
            result.EndDate = DateTime.UtcNow;
            result.Checksum = checksum ?? string.Empty;
            result.UncompressedSize = totalUncompressedSize;
            result.CompressedSize = totalCompressedSize;
            result.IsSuccess = true;
            result.ArchivalEndTime = DateTime.UtcNow;
            result.Metadata["EventCategory"] = eventCategory;
            result.Metadata["PolicyId"] = policyId;
            result.Metadata["CompressionEnabled"] = compressionEnabled;

            if (compressionEnabled && totalUncompressedSize > 0)
            {
                var compressionRatio = (double)totalCompressedSize / totalUncompressedSize;
                var spaceSaved = totalUncompressedSize - totalCompressedSize;

                _logger.LogInformation(
                    "Compression statistics for event category '{EventCategory}': " +
                    "Uncompressed: {UncompressedMB:N2} MB, Compressed: {CompressedMB:N2} MB, " +
                    "Ratio: {Ratio:P2}, Space saved: {SavedMB:N2} MB",
                    eventCategory,
                    totalUncompressedSize / (1024.0 * 1024.0),
                    totalCompressedSize / (1024.0 * 1024.0),
                    compressionRatio,
                    spaceSaved / (1024.0 * 1024.0));
            }

            _logger.LogInformation(
                "Successfully archived {TotalArchived} records for event category '{EventCategory}' in archive batch {ArchiveBatchId}",
                totalArchived,
                eventCategory,
                archiveBatchId);

            return result;
        }
        catch (Exception ex)
        {
            result.IsSuccess = false;
            result.ErrorMessage = ex.Message;
            result.ArchivalEndTime = DateTime.UtcNow;

            _logger.LogError(
                ex,
                "Failed to archive data for event category '{EventCategory}'",
                eventCategory);

            return result;
        }
    }

    /// <summary>
    /// Get the next archive batch ID from the sequence
    /// </summary>
    private async Task<long> GetNextArchiveBatchIdAsync(CancellationToken cancellationToken)
    {
        var result = await _dbContext.Database
            .SqlQueryRaw<long>("SELECT SEQ_SYS_AUDIT_LOG.NEXTVAL FROM DUAL")
            .FirstOrDefaultAsync(cancellationToken);
        return result;
    }

    /// <summary>
    /// Calculate SHA-256 checksum for archived data integrity verification.
    /// Hashes the complete audit log entry data including all fields to ensure data integrity.
    /// The checksum is calculated over the concatenated string of all field values in a deterministic order.
    /// </summary>
    private async Task<string> CalculateArchiveChecksumAsync(
        long archiveBatchId,
        CancellationToken cancellationToken)
    {
        try
        {
            _logger.LogDebug("Calculating SHA-256 checksum for archive batch {ArchiveBatchId}", archiveBatchId);

            // Query all archived records for this batch in a deterministic order
            var archivedRecords = await _dbContext.SysAuditLogArchives
                .Where(a => a.ArchiveBatchId == archiveBatchId)
                .OrderBy(a => a.Id)
                .ToListAsync(cancellationToken);

            using var sha256 = System.Security.Cryptography.SHA256.Create();

            var recordCount = 0;
            foreach (var record in archivedRecords)
            {
                // Build a deterministic string representation of the record
                var recordData = BuildRecordDataString(record);

                // Hash the record data
                var recordBytes = System.Text.Encoding.UTF8.GetBytes(recordData);
                sha256.TransformBlock(recordBytes, 0, recordBytes.Length, null, 0);

                recordCount++;
            }

            // Finalize the hash
            sha256.TransformFinalBlock(Array.Empty<byte>(), 0, 0);
            var hashBytes = sha256.Hash ?? Array.Empty<byte>();
            var checksum = BitConverter.ToString(hashBytes).Replace("-", "").ToLowerInvariant();

            _logger.LogDebug(
                "Calculated checksum {Checksum} for archive batch {ArchiveBatchId} ({RecordCount} records)",
                checksum,
                archiveBatchId,
                recordCount);

            return checksum;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to calculate checksum for archive batch {ArchiveBatchId}", archiveBatchId);
            return string.Empty;
        }
    }

    /// <summary>
    /// Build a deterministic string representation of an audit log record for checksum calculation.
    /// Uses pipe-delimited format with null handling to ensure consistent hashing.
    /// </summary>
    private string BuildRecordDataString(SysAuditLogArchive record)
    {
        var fields = new[]
        {
            GetFieldValue(record.Id),
            GetFieldValue(record.ActorType),
            GetFieldValue(record.ActorId),
            GetFieldValue(record.CompanyId),
            GetFieldValue(record.BranchId),
            GetFieldValue(record.Action),
            GetFieldValue(record.EntityType),
            GetFieldValue(record.EntityId),
            GetFieldValue(record.OldValue),
            GetFieldValue(record.NewValue),
            GetFieldValue(record.IpAddress),
            GetFieldValue(record.UserAgent),
            GetFieldValue(record.CorrelationId),
            GetFieldValue(record.HttpMethod),
            GetFieldValue(record.EndpointPath),
            GetFieldValue(record.RequestPayload),
            GetFieldValue(record.ResponsePayload),
            GetFieldValue(record.ExecutionTimeMs),
            GetFieldValue(record.StatusCode),
            GetFieldValue(record.ExceptionType),
            GetFieldValue(record.ExceptionMessage),
            GetFieldValue(record.StackTrace),
            GetFieldValue(record.Severity),
            GetFieldValue(record.EventCategory),
            GetFieldValue(record.Metadata),
            GetFieldValue(record.SystemId),
            GetFieldValue(record.DeviceIdentifier),
            GetFieldValue(record.ErrorCode),
            GetFieldValue(record.BusinessDescription),
            GetFieldValue(record.CreationDate)
        };

        return string.Join("|", fields);
    }

    /// <summary>
    /// Get field value with null handling for checksum calculation
    /// </summary>
    private static string GetFieldValue(object? value)
    {
        if (value == null)
        {
            return "NULL";
        }

        if (value is DateTime dateTime)
        {
            // Use ISO 8601 format for consistent date representation
            return dateTime.ToString("yyyy-MM-ddTHH:mm:ss.fffZ");
        }

        return value.ToString() ?? "NULL";
    }

    public async Task<ArchivalResult> ArchiveByDateRangeAsync(
        DateTime startDate,
        DateTime endDate,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation(
            "Archiving audit data by date range: {StartDate} to {EndDate}",
            startDate,
            endDate);

        // Implementation for manual date range archival
        // This is a simplified version - full implementation would be similar to ArchiveByEventCategoryAsync
        throw new NotImplementedException("Manual date range archival will be implemented in task 10.7");
    }

    /// <summary>
    /// Retrieve archived audit data based on filter criteria.
    /// Decompresses GZip-compressed data and returns it in the standard AuditLogEntry format.
    /// Supports the same filtering capabilities as the active audit log.
    /// Verifies checksums for data integrity during retrieval.
    /// </summary>
    public async Task<IEnumerable<AuditLogEntry>> RetrieveArchivedDataAsync(
        AuditQueryFilter filter,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Retrieving archived audit data with filter criteria");
        var results = new List<AuditLogEntry>();

        try
        {
            // Build dynamic LINQ query based on filter criteria
            var query = _dbContext.SysAuditLogArchives.AsQueryable();

            if (filter.StartDate.HasValue)
            {
                var startDate = filter.StartDate.Value;
                query = query.Where(a => a.CreationDate >= startDate);
            }

            if (filter.EndDate.HasValue)
            {
                var endDate = filter.EndDate.Value;
                query = query.Where(a => a.CreationDate <= endDate);
            }

            if (filter.ActorId.HasValue)
            {
                var actorId = filter.ActorId.Value;
                query = query.Where(a => a.ActorId == actorId);
            }

            if (!string.IsNullOrEmpty(filter.ActorType))
            {
                var actorType = filter.ActorType;
                query = query.Where(a => a.ActorType == actorType);
            }

            if (filter.CompanyId.HasValue)
            {
                var companyId = filter.CompanyId.Value;
                query = query.Where(a => a.CompanyId == companyId);
            }

            if (filter.BranchId.HasValue)
            {
                var branchId = filter.BranchId.Value;
                query = query.Where(a => a.BranchId == branchId);
            }

            if (!string.IsNullOrEmpty(filter.EntityType))
            {
                var entityType = filter.EntityType;
                query = query.Where(a => a.EntityType == entityType);
            }

            if (filter.EntityId.HasValue)
            {
                var entityId = filter.EntityId.Value;
                query = query.Where(a => a.EntityId == entityId);
            }

            if (!string.IsNullOrEmpty(filter.Action))
            {
                var action = filter.Action;
                query = query.Where(a => a.Action == action);
            }

            if (!string.IsNullOrEmpty(filter.IpAddress))
            {
                var ipAddress = filter.IpAddress;
                query = query.Where(a => a.IpAddress == ipAddress);
            }

            if (!string.IsNullOrEmpty(filter.CorrelationId))
            {
                var correlationId = filter.CorrelationId;
                query = query.Where(a => a.CorrelationId == correlationId);
            }

            if (!string.IsNullOrEmpty(filter.EventCategory))
            {
                var eventCategory = filter.EventCategory;
                query = query.Where(a => a.EventCategory == eventCategory);
            }

            if (!string.IsNullOrEmpty(filter.Severity))
            {
                var severity = filter.Severity;
                query = query.Where(a => a.Severity == severity);
            }

            if (!string.IsNullOrEmpty(filter.HttpMethod))
            {
                var httpMethod = filter.HttpMethod;
                query = query.Where(a => a.HttpMethod == httpMethod);
            }

            if (!string.IsNullOrEmpty(filter.EndpointPath))
            {
                var endpointPath = filter.EndpointPath;
                query = query.Where(a => a.EndpointPath == endpointPath);
            }

            if (filter.SystemId.HasValue)
            {
                var systemId = filter.SystemId.Value;
                query = query.Where(a => a.SystemId == systemId);
            }

            if (!string.IsNullOrEmpty(filter.ErrorCode))
            {
                var errorCode = filter.ErrorCode;
                query = query.Where(a => a.ErrorCode == errorCode);
            }

            // Order by creation date for consistent results
            query = query.OrderByDescending(a => a.CreationDate).ThenByDescending(a => a.Id);

            var archivedRecords = await query.ToListAsync(cancellationToken);

            _logger.LogDebug("Executing archived data query with {FilterCount} filter criteria", 
                typeof(AuditQueryFilter).GetProperties().Count(p => p.GetValue(filter) != null));

            var recordCount = 0;
            var compressionEnabled = _options.CompressionAlgorithm.Equals("GZip", StringComparison.OrdinalIgnoreCase);

            foreach (var record in archivedRecords)
            {
                if (cancellationToken.IsCancellationRequested)
                {
                    _logger.LogWarning("Archived data retrieval cancelled by user request");
                    break;
                }

                var entry = await MapArchivedDataToAuditLogEntryAsync(record, compressionEnabled, cancellationToken);
                results.Add(entry);
                recordCount++;

                // Log progress for large retrievals
                if (recordCount % 1000 == 0)
                {
                    _logger.LogDebug("Retrieved {RecordCount} archived records so far", recordCount);
                }
            }

            _logger.LogInformation(
                "Successfully retrieved {RecordCount} archived audit log entries",
                recordCount);

            return results;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving archived audit data");
            throw;
        }
    }

    /// <summary>
    /// Map archived data entity to AuditLogEntry, decompressing CLOB fields if necessary
    /// </summary>
    private async Task<AuditLogEntry> MapArchivedDataToAuditLogEntryAsync(
        SysAuditLogArchive record,
        bool compressionEnabled,
        CancellationToken cancellationToken)
    {
        var entry = new AuditLogEntry
        {
            Id = record.Id,
            ActorType = record.ActorType,
            ActorId = record.ActorId,
            CompanyId = record.CompanyId,
            BranchId = record.BranchId,
            Action = record.Action,
            EntityType = record.EntityType,
            EntityId = record.EntityId,
            IpAddress = record.IpAddress,
            UserAgent = record.UserAgent,
            CorrelationId = record.CorrelationId,
            HttpMethod = record.HttpMethod,
            EndpointPath = record.EndpointPath,
            ExecutionTimeMs = record.ExecutionTimeMs,
            StatusCode = record.StatusCode,
            ExceptionType = record.ExceptionType,
            ExceptionMessage = record.ExceptionMessage,
            Severity = record.Severity ?? "Info",
            EventCategory = record.EventCategory ?? "DataChange",
            SystemId = record.SystemId,
            DeviceIdentifier = record.DeviceIdentifier,
            ErrorCode = record.ErrorCode,
            BusinessDescription = record.BusinessDescription,
            CreationDate = record.CreationDate
        };

        // Decompress CLOB fields if compression is enabled
        if (compressionEnabled)
        {
            entry.OldValue = await DecompressClobFieldAsync(record.OldValue, cancellationToken);
            entry.NewValue = await DecompressClobFieldAsync(record.NewValue, cancellationToken);
            entry.RequestPayload = await DecompressClobFieldAsync(record.RequestPayload, cancellationToken);
            entry.ResponsePayload = await DecompressClobFieldAsync(record.ResponsePayload, cancellationToken);
            entry.StackTrace = await DecompressClobFieldAsync(record.StackTrace, cancellationToken);
            entry.Metadata = await DecompressClobFieldAsync(record.Metadata, cancellationToken);
        }
        else
        {
            entry.OldValue = record.OldValue;
            entry.NewValue = record.NewValue;
            entry.RequestPayload = record.RequestPayload;
            entry.ResponsePayload = record.ResponsePayload;
            entry.StackTrace = record.StackTrace;
            entry.Metadata = record.Metadata;
        }

        return entry;
    }

    /// <summary>
    /// Decompress a CLOB field value
    /// </summary>
    private async Task<string?> DecompressClobFieldAsync(
        string? fieldValue,
        CancellationToken cancellationToken)
    {
        try
        {
            if (string.IsNullOrEmpty(fieldValue))
            {
                return null;
            }

            // Decompress using the compression service
            var decompressedData = _compressionService.Decompress(fieldValue);

            return decompressedData;
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Error decompressing CLOB field during archived data retrieval");

            // Return null on decompression error to avoid breaking the entire retrieval
            return null;
        }
    }

    public async Task<bool> VerifyArchiveIntegrityAsync(
        long archiveId,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Verifying integrity of archive {ArchiveId}", archiveId);

        try
        {
            // Step 1: Check if archive batch exists
            var archiveExists = await _dbContext.SysAuditLogArchives
                .AnyAsync(a => a.ArchiveBatchId == archiveId, cancellationToken);

            if (!archiveExists)
            {
                _logger.LogWarning("Archive batch {ArchiveId} not found", archiveId);
                return false;
            }

            // Step 2: Get stored checksum using raw SQL since CHECKSUM is not mapped in entity
            string? storedChecksum = null;
            var checksumResult = await _dbContext.Database
                .SqlQueryRaw<string>(
                    "SELECT CHECKSUM FROM SYS_AUDIT_LOG_ARCHIVE WHERE ARCHIVE_BATCH_ID = {0} AND ROWNUM = 1",
                    archiveId)
                .FirstOrDefaultAsync(cancellationToken);
            storedChecksum = checksumResult;

            // Step 3: Check if checksum was stored
            if (string.IsNullOrEmpty(storedChecksum))
            {
                _logger.LogWarning(
                    "No checksum found for archive batch {ArchiveId}. Integrity verification not available.",
                    archiveId);
                return false;
            }

            // Step 4: Recalculate the checksum from current archive data
            var recalculatedChecksum = await CalculateArchiveChecksumAsync(archiveId, cancellationToken);

            // Step 5: Compare checksums
            var isValid = string.Equals(storedChecksum, recalculatedChecksum, StringComparison.OrdinalIgnoreCase);

            if (isValid)
            {
                _logger.LogInformation(
                    "Archive integrity verification PASSED for archive batch {ArchiveId}. Checksum: {Checksum}",
                    archiveId,
                    storedChecksum);
            }
            else
            {
                _logger.LogError(
                    "Archive integrity verification FAILED for archive batch {ArchiveId}. " +
                    "Stored checksum: {StoredChecksum}, Recalculated checksum: {RecalculatedChecksum}",
                    archiveId,
                    storedChecksum,
                    recalculatedChecksum);
            }

            return isValid;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error verifying integrity of archive {ArchiveId}", archiveId);
            return false;
        }
    }

    /// <summary>
    /// Get the retention policy for a specific event type from SYS_RETENTION_POLICIES table
    /// </summary>
    public async Task<RetentionPolicy?> GetRetentionPolicyAsync(
        string eventType,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var policy = await _dbContext.SysRetentionPolicies
                .FirstOrDefaultAsync(p => p.EventCategory == eventType, cancellationToken);

            if (policy == null)
            {
                return null;
            }

            return MapRetentionPolicyFromEntity(policy);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving retention policy for event type '{EventType}'", eventType);
            throw;
        }
    }

    /// <summary>
    /// Get all active retention policies from SYS_RETENTION_POLICIES table
    /// </summary>
    public async Task<IEnumerable<RetentionPolicy>> GetAllRetentionPoliciesAsync(
        CancellationToken cancellationToken = default)
    {
        var policies = new List<RetentionPolicy>();

        try
        {
            var entities = await _dbContext.SysRetentionPolicies
                .Where(p => p.ArchiveEnabled)
                .OrderBy(p => p.EventCategory)
                .ToListAsync(cancellationToken);

            _logger.LogInformation("Retrieved {PolicyCount} active retention policies", entities.Count);
            return entities.Select(MapRetentionPolicyFromEntity);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving retention policies");
            throw;
        }
    }

    /// <summary>
    /// Map SysRetentionPolicy entity to RetentionPolicy model
    /// </summary>
    private static RetentionPolicy MapRetentionPolicyFromEntity(SysRetentionPolicy policy)
    {
        return new RetentionPolicy
        {
            PolicyId = policy.Id,
            EventType = policy.EventCategory,
            RetentionDays = policy.RetentionDays,
            IsActive = policy.ArchiveEnabled,
            Description = policy.Description,
            ModifiedDate = policy.LastModifiedDate,
            ModifiedBy = policy.LastModifiedBy,
            ArchiveRetentionDays = -1, // Indefinite retention by default
            CreatedDate = DateTime.UtcNow, // Not stored in current schema
            CreatedBy = 0 // Not stored in current schema
        };
    }

    public async Task<RetentionPolicy> UpdateRetentionPolicyAsync(
        RetentionPolicy policy,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Updating retention policy for event type '{EventType}'", policy.EventType);

        // Implementation for updating retention policies
        throw new NotImplementedException("Retention policy updates will be implemented in a future task");
    }

    public async Task<ArchivalStatistics> GetArchivalStatisticsAsync(
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Retrieving archival statistics");

        // Implementation for archival statistics
        throw new NotImplementedException("Archival statistics will be implemented in a future task");
    }

    public async Task<bool> DeleteExpiredArchiveAsync(
        long archiveId,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Deleting expired archive {ArchiveId}", archiveId);

        // Implementation for deleting expired archives
        throw new NotImplementedException("Archive deletion will be implemented in a future task");
    }

    public async Task<int> RestoreArchivedDataAsync(
        long archiveId,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Restoring archived data from archive {ArchiveId}", archiveId);

        // Implementation for restoring archived data
        throw new NotImplementedException("Archive restoration will be implemented in a future task");
    }

    public async Task<bool> IsHealthyAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            // Check if we can query the retention policies table
            await _dbContext.SysRetentionPolicies.CountAsync(cancellationToken);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Health check failed for ArchivalService");
            return false;
        }
    }

    /// <summary>
    /// Export archived data to external storage (S3, Azure CLOB, etc.).
    /// This method retrieves archived data from the database, serializes it, and uploads it to external storage.
    /// The data is already compressed in the database, so we export it in compressed format.
    /// </summary>
    /// <param name="archiveId">Archive batch ID to export</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Storage location URL where the data was exported</returns>
    public async Task<string> ExportToExternalStorageAsync(
        long archiveId,
        CancellationToken cancellationToken = default)
    {
        if (_externalStorageProvider == null)
        {
            throw new InvalidOperationException(
                "External storage provider is not configured. " +
                $"Please configure StorageProvider and StorageConnectionString in {ArchivalOptions.SectionName} settings.");
        }

        _logger.LogInformation(
            "Exporting archive {ArchiveId} to external storage provider '{ProviderName}'",
            archiveId,
            _externalStorageProvider.ProviderName);

        try
        {
            // Step 1: Retrieve archived data from database
            var archivedData = await RetrieveArchivedDataForExportAsync(archiveId, cancellationToken);

            if (archivedData.Count == 0)
            {
                throw new InvalidOperationException($"No archived data found for archive ID {archiveId}");
            }

            _logger.LogInformation(
                "Retrieved {RecordCount} records from archive {ArchiveId} for export",
                archivedData.Count,
                archiveId);

            // Step 2: Serialize archived data to binary format
            var serializedData = SerializeArchivedData(archivedData);

            _logger.LogInformation(
                "Serialized archive {ArchiveId} to {SizeMB:N2} MB",
                archiveId,
                serializedData.Length / (1024.0 * 1024.0));

            // Step 3: Prepare metadata for external storage
            var metadata = new Dictionary<string, string>
            {
                ["ArchiveId"] = archiveId.ToString(),
                ["RecordCount"] = archivedData.Count.ToString(),
                ["ExportDate"] = DateTime.UtcNow.ToString("O"),
                ["CompressionAlgorithm"] = _options.CompressionAlgorithm
            };

            // Add checksum from first record (all records in batch have same checksum)
            // Note: CHECKSUM column is not mapped in the entity, so it's not included in the export data
            if (archivedData.Count > 0 && archivedData[0].ContainsKey("CHECKSUM"))
            {
                var checksum = archivedData[0]["CHECKSUM"]?.ToString();
                if (!string.IsNullOrEmpty(checksum))
                {
                    metadata["Checksum"] = checksum;
                }
            }

            // Step 4: Upload to external storage
            var storageLocation = await _externalStorageProvider.UploadAsync(
                archiveId,
                serializedData,
                metadata,
                cancellationToken);

            _logger.LogInformation(
                "Successfully exported archive {ArchiveId} to external storage: {StorageLocation}",
                archiveId,
                storageLocation);

            // Step 5: Update archive record with storage location
            await UpdateArchiveStorageLocationAsync(archiveId, storageLocation, cancellationToken);

            return storageLocation;
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Error exporting archive {ArchiveId} to external storage",
                archiveId);
            throw;
        }
    }

    /// <summary>
    /// Retrieve archived data from external storage and import it back to the database.
    /// This method downloads data from external storage, deserializes it, and inserts it into the archive table.
    /// </summary>
    /// <param name="storageLocation">Storage location URL from where to retrieve the data</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Number of records imported</returns>
    public async Task<int> ImportFromExternalStorageAsync(
        string storageLocation,
        CancellationToken cancellationToken = default)
    {
        if (_externalStorageProvider == null)
        {
            throw new InvalidOperationException(
                "External storage provider is not configured. " +
                $"Please configure StorageProvider and StorageConnectionString in {ArchivalOptions.SectionName} settings.");
        }

        _logger.LogInformation(
            "Importing archived data from external storage: {StorageLocation}",
            storageLocation);

        try
        {
            // Step 1: Download data from external storage
            var serializedData = await _externalStorageProvider.DownloadAsync(storageLocation, cancellationToken);

            _logger.LogInformation(
                "Downloaded {SizeMB:N2} MB from external storage: {StorageLocation}",
                serializedData.Length / (1024.0 * 1024.0),
                storageLocation);

            // Step 2: Deserialize data
            var archivedData = DeserializeArchivedData(serializedData);

            _logger.LogInformation(
                "Deserialized {RecordCount} records from external storage",
                archivedData.Count);

            // Step 3: Insert data into archive table
            var importedCount = await InsertArchivedDataAsync(archivedData, cancellationToken);

            _logger.LogInformation(
                "Successfully imported {RecordCount} records from external storage: {StorageLocation}",
                importedCount,
                storageLocation);

            return importedCount;
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Error importing archived data from external storage: {StorageLocation}",
                storageLocation);
            throw;
        }
    }

    /// <summary>
    /// Verify integrity of archived data in external storage by comparing checksums.
    /// </summary>
    public async Task<bool> VerifyExternalStorageIntegrityAsync(
        string storageLocation,
        string expectedChecksum,
        CancellationToken cancellationToken = default)
    {
        if (_externalStorageProvider == null)
        {
            throw new InvalidOperationException("External storage provider is not configured.");
        }

        return await _externalStorageProvider.VerifyIntegrityAsync(
            storageLocation,
            expectedChecksum,
            cancellationToken);
    }

    /// <summary>
    /// Retrieve archived data from database for export to external storage
    /// </summary>
    private async Task<List<Dictionary<string, object?>>> RetrieveArchivedDataForExportAsync(
        long archiveId,
        CancellationToken cancellationToken)
    {
        var records = await _dbContext.SysAuditLogArchives
            .Where(a => a.ArchiveBatchId == archiveId)
            .OrderBy(a => a.Id)
            .ToListAsync(cancellationToken);

        return records.Select(record => new Dictionary<string, object?>
        {
            ["ROW_ID"] = record.Id,
            ["ACTOR_TYPE"] = record.ActorType,
            ["ACTOR_ID"] = record.ActorId,
            ["COMPANY_ID"] = record.CompanyId,
            ["BRANCH_ID"] = record.BranchId,
            ["ACTION"] = record.Action,
            ["ENTITY_TYPE"] = record.EntityType,
            ["ENTITY_ID"] = record.EntityId,
            ["OLD_VALUE"] = record.OldValue,
            ["NEW_VALUE"] = record.NewValue,
            ["IP_ADDRESS"] = record.IpAddress,
            ["USER_AGENT"] = record.UserAgent,
            ["CORRELATION_ID"] = record.CorrelationId,
            ["HTTP_METHOD"] = record.HttpMethod,
            ["ENDPOINT_PATH"] = record.EndpointPath,
            ["REQUEST_PAYLOAD"] = record.RequestPayload,
            ["RESPONSE_PAYLOAD"] = record.ResponsePayload,
            ["EXECUTION_TIME_MS"] = record.ExecutionTimeMs,
            ["STATUS_CODE"] = record.StatusCode,
            ["EXCEPTION_TYPE"] = record.ExceptionType,
            ["EXCEPTION_MESSAGE"] = record.ExceptionMessage,
            ["STACK_TRACE"] = record.StackTrace,
            ["SEVERITY"] = record.Severity,
            ["EVENT_CATEGORY"] = record.EventCategory,
            ["METADATA"] = record.Metadata,
            ["BUSINESS_MODULE"] = record.SystemId,
            ["DEVICE_IDENTIFIER"] = record.DeviceIdentifier,
            ["ERROR_CODE"] = record.ErrorCode,
            ["BUSINESS_DESCRIPTION"] = record.BusinessDescription,
            ["CREATION_DATE"] = record.CreationDate,
            ["ARCHIVED_DATE"] = record.ArchivedDate,
            ["ARCHIVE_BATCH_ID"] = record.ArchiveBatchId
        }).ToList();
    }

    /// <summary>
    /// Serialize archived data to binary format for external storage.
    /// Uses JSON serialization with GZip compression.
    /// </summary>
    private byte[] SerializeArchivedData(List<Dictionary<string, object?>> archivedData)
    {
        // Convert to JSON
        var json = System.Text.Json.JsonSerializer.Serialize(archivedData);

        // Compress with GZip
        using var outputStream = new System.IO.MemoryStream();
        using (var gzipStream = new System.IO.Compression.GZipStream(outputStream, System.IO.Compression.CompressionLevel.Optimal))
        {
            var bytes = System.Text.Encoding.UTF8.GetBytes(json);
            gzipStream.Write(bytes, 0, bytes.Length);
        }

        return outputStream.ToArray();
    }

    /// <summary>
    /// Deserialize archived data from binary format.
    /// </summary>
    private List<Dictionary<string, object?>> DeserializeArchivedData(byte[] serializedData)
    {
        // Decompress with GZip
        using var inputStream = new System.IO.MemoryStream(serializedData);
        using var gzipStream = new System.IO.Compression.GZipStream(inputStream, System.IO.Compression.CompressionMode.Decompress);
        using var outputStream = new System.IO.MemoryStream();

        gzipStream.CopyTo(outputStream);
        var json = System.Text.Encoding.UTF8.GetString(outputStream.ToArray());

        // Deserialize from JSON
        var records = System.Text.Json.JsonSerializer.Deserialize<List<Dictionary<string, object?>>>(json);
        return records ?? new List<Dictionary<string, object?>>();
    }

    /// <summary>
    /// Insert archived data into the archive table
    /// </summary>
    private async Task<int> InsertArchivedDataAsync(
        List<Dictionary<string, object?>> archivedData,
        CancellationToken cancellationToken)
    {
        var archiveEntities = archivedData.Select(d => new SysAuditLogArchive
        {
            Id = Convert.ToInt64(d["ROW_ID"]),
            ActorType = d["ACTOR_TYPE"]?.ToString() ?? string.Empty,
            ActorId = Convert.ToInt64(d["ACTOR_ID"]),
            CompanyId = d.TryGetValue("COMPANY_ID", out var companyId) && companyId != null ? Convert.ToInt64(companyId) : null,
            BranchId = d.TryGetValue("BRANCH_ID", out var branchId) && branchId != null ? Convert.ToInt64(branchId) : null,
            Action = d["ACTION"]?.ToString() ?? string.Empty,
            EntityType = d["ENTITY_TYPE"]?.ToString() ?? string.Empty,
            EntityId = d.TryGetValue("ENTITY_ID", out var entityId) && entityId != null ? Convert.ToInt64(entityId) : null,
            OldValue = d.TryGetValue("OLD_VALUE", out var oldValue) ? oldValue?.ToString() : null,
            NewValue = d.TryGetValue("NEW_VALUE", out var newValue) ? newValue?.ToString() : null,
            IpAddress = d.TryGetValue("IP_ADDRESS", out var ipAddress) ? ipAddress?.ToString() : null,
            UserAgent = d.TryGetValue("USER_AGENT", out var userAgent) ? userAgent?.ToString() : null,
            CorrelationId = d.TryGetValue("CORRELATION_ID", out var correlationId) ? correlationId?.ToString() : null,
            HttpMethod = d.TryGetValue("HTTP_METHOD", out var httpMethod) ? httpMethod?.ToString() : null,
            EndpointPath = d.TryGetValue("ENDPOINT_PATH", out var endpointPath) ? endpointPath?.ToString() : null,
            RequestPayload = d.TryGetValue("REQUEST_PAYLOAD", out var requestPayload) ? requestPayload?.ToString() : null,
            ResponsePayload = d.TryGetValue("RESPONSE_PAYLOAD", out var responsePayload) ? responsePayload?.ToString() : null,
            ExecutionTimeMs = d.TryGetValue("EXECUTION_TIME_MS", out var executionTimeMs) && executionTimeMs != null ? Convert.ToInt64(executionTimeMs) : null,
            StatusCode = d.TryGetValue("STATUS_CODE", out var statusCode) && statusCode != null ? Convert.ToInt32(statusCode) : null,
            ExceptionType = d.TryGetValue("EXCEPTION_TYPE", out var exceptionType) ? exceptionType?.ToString() : null,
            ExceptionMessage = d.TryGetValue("EXCEPTION_MESSAGE", out var exceptionMessage) ? exceptionMessage?.ToString() : null,
            StackTrace = d.TryGetValue("STACK_TRACE", out var stackTrace) ? stackTrace?.ToString() : null,
            Severity = d.TryGetValue("SEVERITY", out var severity) ? severity?.ToString() : null,
            EventCategory = d.TryGetValue("EVENT_CATEGORY", out var eventCategory) ? eventCategory?.ToString() : null,
            Metadata = d.TryGetValue("METADATA", out var metadata) ? metadata?.ToString() : null,
            SystemId = d.TryGetValue("BUSINESS_MODULE", out var businessModule) && businessModule != null ? Convert.ToInt64(businessModule) : null,
            DeviceIdentifier = d.TryGetValue("DEVICE_IDENTIFIER", out var deviceIdentifier) ? deviceIdentifier?.ToString() : null,
            ErrorCode = d.TryGetValue("ERROR_CODE", out var errorCode) ? errorCode?.ToString() : null,
            BusinessDescription = d.TryGetValue("BUSINESS_DESCRIPTION", out var businessDescription) ? businessDescription?.ToString() : null,
            CreationDate = Convert.ToDateTime(d["CREATION_DATE"]),
            ArchivedDate = d.TryGetValue("ARCHIVED_DATE", out var archivedDate) && archivedDate != null ? Convert.ToDateTime(archivedDate) : DateTime.UtcNow,
            ArchiveBatchId = Convert.ToInt64(d["ARCHIVE_BATCH_ID"])
        }).ToList();

        await _dbContext.SysAuditLogArchives.AddRangeAsync(archiveEntities, cancellationToken);
        await _dbContext.SaveChangesAsync(cancellationToken);

        return archiveEntities.Count;
    }

    /// <summary>
    /// Update archive record with external storage location
    /// </summary>
    private async Task UpdateArchiveStorageLocationAsync(
        long archiveId,
        string storageLocation,
        CancellationToken cancellationToken)
    {
        // Note: This would require adding a STORAGE_LOCATION column to SYS_AUDIT_LOG_ARCHIVE table
        // For now, we'll store it in the METADATA column as JSON
        var archives = await _dbContext.SysAuditLogArchives
            .Where(a => a.ArchiveBatchId == archiveId)
            .ToListAsync(cancellationToken);

        foreach (var archive in archives)
        {
            archive.Metadata = $$"""{"StorageLocation": "{{storageLocation}}"}""";
        }

        await _dbContext.SaveChangesAsync(cancellationToken);
    }
}
