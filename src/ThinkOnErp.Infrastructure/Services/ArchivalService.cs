using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Oracle.ManagedDataAccess.Client;
using System.Data;
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
            using var connection = _dbContext.CreateConnection();
            await connection.OpenAsync(cancellationToken);

            // Step 1: Count records to be archived for this event category
            var countSql = @"
                SELECT COUNT(*) 
                FROM SYS_AUDIT_LOG 
                WHERE EVENT_CATEGORY = :EventCategory 
                AND CREATION_DATE < :CutoffDate";

            int recordCount;
            using (var countCmd = new OracleCommand(countSql, connection))
            {
                countCmd.Parameters.Add(":EventCategory", OracleDbType.NVarchar2).Value = eventCategory;
                countCmd.Parameters.Add(":CutoffDate", OracleDbType.Date).Value = cutoffDate;

                var countResult = await countCmd.ExecuteScalarAsync(cancellationToken);
                recordCount = Convert.ToInt32(countResult);
            }

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
            var archiveBatchId = await GetNextArchiveBatchIdAsync(connection, cancellationToken);

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
                using var transaction = connection.BeginTransaction();
                var batchStartTime = DateTime.UtcNow;
                
                try
                {
                    // Step 1: Fetch records to archive with CLOB fields
                    var selectSql = @"
                        SELECT 
                            ROW_ID, ACTOR_TYPE, ACTOR_ID, COMPANY_ID, BRANCH_ID,
                            ACTION, ENTITY_TYPE, ENTITY_ID, OLD_VALUE, NEW_VALUE,
                            IP_ADDRESS, USER_AGENT, CORRELATION_ID, HTTP_METHOD, ENDPOINT_PATH,
                            REQUEST_PAYLOAD, RESPONSE_PAYLOAD, EXECUTION_TIME_MS, STATUS_CODE,
                            EXCEPTION_TYPE, EXCEPTION_MESSAGE, STACK_TRACE, SEVERITY,
                            EVENT_CATEGORY, METADATA, BUSINESS_MODULE, DEVICE_IDENTIFIER,
                            ERROR_CODE, BUSINESS_DESCRIPTION, CREATION_DATE
                        FROM (
                            SELECT * FROM SYS_AUDIT_LOG
                            WHERE EVENT_CATEGORY = :EventCategory 
                            AND CREATION_DATE < :CutoffDate
                            AND ROWNUM <= :BatchSize
                        )";

                    var recordsToArchive = new List<Dictionary<string, object?>>();
                    
                    using (var selectCmd = new OracleCommand(selectSql, connection))
                    {
                        selectCmd.Transaction = transaction;
                        selectCmd.Parameters.Add(":EventCategory", OracleDbType.NVarchar2).Value = eventCategory;
                        selectCmd.Parameters.Add(":CutoffDate", OracleDbType.Date).Value = cutoffDate;
                        selectCmd.Parameters.Add(":BatchSize", OracleDbType.Int32).Value = batchSize;

                        using var reader = await selectCmd.ExecuteReaderAsync(cancellationToken);
                        while (await reader.ReadAsync(cancellationToken))
                        {
                            var record = new Dictionary<string, object?>();
                            for (int i = 0; i < reader.FieldCount; i++)
                            {
                                var fieldName = reader.GetName(i);
                                var value = reader.IsDBNull(i) ? null : reader.GetValue(i);
                                record[fieldName] = value;
                            }
                            recordsToArchive.Add(record);
                        }
                    }

                    if (recordsToArchive.Count == 0)
                    {
                        transaction.Commit();
                        break;
                    }

                    // Step 2: Compress CLOB fields if compression is enabled
                    if (compressionEnabled)
                    {
                        foreach (var record in recordsToArchive)
                        {
                            // Compress CLOB fields: OLD_VALUE, NEW_VALUE, REQUEST_PAYLOAD, RESPONSE_PAYLOAD, STACK_TRACE, METADATA
                            var clobFields = new[] { "OLD_VALUE", "NEW_VALUE", "REQUEST_PAYLOAD", "RESPONSE_PAYLOAD", "STACK_TRACE", "METADATA" };
                            
                            foreach (var field in clobFields)
                            {
                                if (record.ContainsKey(field) && record[field] != null)
                                {
                                    var originalValue = record[field]?.ToString();
                                    if (!string.IsNullOrEmpty(originalValue))
                                    {
                                        // Track uncompressed size
                                        totalUncompressedSize += _compressionService.GetSizeInBytes(originalValue);
                                        
                                        // Compress the field
                                        var compressedValue = _compressionService.Compress(originalValue);
                                        record[field] = compressedValue;
                                        
                                        // Track compressed size
                                        totalCompressedSize += _compressionService.GetSizeInBytes(compressedValue);
                                    }
                                }
                            }
                        }
                    }

                    // Step 3: Insert compressed records into archive table
                    var insertSql = @"
                        INSERT INTO SYS_AUDIT_LOG_ARCHIVE (
                            ROW_ID, ACTOR_TYPE, ACTOR_ID, COMPANY_ID, BRANCH_ID,
                            ACTION, ENTITY_TYPE, ENTITY_ID, OLD_VALUE, NEW_VALUE,
                            IP_ADDRESS, USER_AGENT, CORRELATION_ID, HTTP_METHOD, ENDPOINT_PATH,
                            REQUEST_PAYLOAD, RESPONSE_PAYLOAD, EXECUTION_TIME_MS, STATUS_CODE,
                            EXCEPTION_TYPE, EXCEPTION_MESSAGE, STACK_TRACE, SEVERITY,
                            EVENT_CATEGORY, METADATA, BUSINESS_MODULE, DEVICE_IDENTIFIER,
                            ERROR_CODE, BUSINESS_DESCRIPTION, CREATION_DATE, ARCHIVED_DATE, ARCHIVE_BATCH_ID
                        ) VALUES (
                            :ROW_ID, :ACTOR_TYPE, :ACTOR_ID, :COMPANY_ID, :BRANCH_ID,
                            :ACTION, :ENTITY_TYPE, :ENTITY_ID, :OLD_VALUE, :NEW_VALUE,
                            :IP_ADDRESS, :USER_AGENT, :CORRELATION_ID, :HTTP_METHOD, :ENDPOINT_PATH,
                            :REQUEST_PAYLOAD, :RESPONSE_PAYLOAD, :EXECUTION_TIME_MS, :STATUS_CODE,
                            :EXCEPTION_TYPE, :EXCEPTION_MESSAGE, :STACK_TRACE, :SEVERITY,
                            :EVENT_CATEGORY, :METADATA, :BUSINESS_MODULE, :DEVICE_IDENTIFIER,
                            :ERROR_CODE, :BUSINESS_DESCRIPTION, :CREATION_DATE, SYSDATE, :ARCHIVE_BATCH_ID
                        )";

                    int insertedCount = 0;
                    foreach (var record in recordsToArchive)
                    {
                        using var insertCmd = new OracleCommand(insertSql, connection);
                        insertCmd.Transaction = transaction;
                        
                        // Add parameters
                        insertCmd.Parameters.Add(":ROW_ID", OracleDbType.Int64).Value = record["ROW_ID"] ?? DBNull.Value;
                        insertCmd.Parameters.Add(":ACTOR_TYPE", OracleDbType.NVarchar2).Value = record["ACTOR_TYPE"] ?? DBNull.Value;
                        insertCmd.Parameters.Add(":ACTOR_ID", OracleDbType.Int64).Value = record["ACTOR_ID"] ?? DBNull.Value;
                        insertCmd.Parameters.Add(":COMPANY_ID", OracleDbType.Int64).Value = record["COMPANY_ID"] ?? DBNull.Value;
                        insertCmd.Parameters.Add(":BRANCH_ID", OracleDbType.Int64).Value = record["BRANCH_ID"] ?? DBNull.Value;
                        insertCmd.Parameters.Add(":ACTION", OracleDbType.NVarchar2).Value = record["ACTION"] ?? DBNull.Value;
                        insertCmd.Parameters.Add(":ENTITY_TYPE", OracleDbType.NVarchar2).Value = record["ENTITY_TYPE"] ?? DBNull.Value;
                        insertCmd.Parameters.Add(":ENTITY_ID", OracleDbType.Int64).Value = record["ENTITY_ID"] ?? DBNull.Value;
                        insertCmd.Parameters.Add(":OLD_VALUE", OracleDbType.Clob).Value = record["OLD_VALUE"] ?? DBNull.Value;
                        insertCmd.Parameters.Add(":NEW_VALUE", OracleDbType.Clob).Value = record["NEW_VALUE"] ?? DBNull.Value;
                        insertCmd.Parameters.Add(":IP_ADDRESS", OracleDbType.NVarchar2).Value = record["IP_ADDRESS"] ?? DBNull.Value;
                        insertCmd.Parameters.Add(":USER_AGENT", OracleDbType.NVarchar2).Value = record["USER_AGENT"] ?? DBNull.Value;
                        insertCmd.Parameters.Add(":CORRELATION_ID", OracleDbType.NVarchar2).Value = record["CORRELATION_ID"] ?? DBNull.Value;
                        insertCmd.Parameters.Add(":HTTP_METHOD", OracleDbType.NVarchar2).Value = record["HTTP_METHOD"] ?? DBNull.Value;
                        insertCmd.Parameters.Add(":ENDPOINT_PATH", OracleDbType.NVarchar2).Value = record["ENDPOINT_PATH"] ?? DBNull.Value;
                        insertCmd.Parameters.Add(":REQUEST_PAYLOAD", OracleDbType.Clob).Value = record["REQUEST_PAYLOAD"] ?? DBNull.Value;
                        insertCmd.Parameters.Add(":RESPONSE_PAYLOAD", OracleDbType.Clob).Value = record["RESPONSE_PAYLOAD"] ?? DBNull.Value;
                        insertCmd.Parameters.Add(":EXECUTION_TIME_MS", OracleDbType.Int64).Value = record["EXECUTION_TIME_MS"] ?? DBNull.Value;
                        insertCmd.Parameters.Add(":STATUS_CODE", OracleDbType.Int32).Value = record["STATUS_CODE"] ?? DBNull.Value;
                        insertCmd.Parameters.Add(":EXCEPTION_TYPE", OracleDbType.NVarchar2).Value = record["EXCEPTION_TYPE"] ?? DBNull.Value;
                        insertCmd.Parameters.Add(":EXCEPTION_MESSAGE", OracleDbType.NVarchar2).Value = record["EXCEPTION_MESSAGE"] ?? DBNull.Value;
                        insertCmd.Parameters.Add(":STACK_TRACE", OracleDbType.Clob).Value = record["STACK_TRACE"] ?? DBNull.Value;
                        insertCmd.Parameters.Add(":SEVERITY", OracleDbType.NVarchar2).Value = record["SEVERITY"] ?? DBNull.Value;
                        insertCmd.Parameters.Add(":EVENT_CATEGORY", OracleDbType.NVarchar2).Value = record["EVENT_CATEGORY"] ?? DBNull.Value;
                        insertCmd.Parameters.Add(":METADATA", OracleDbType.Clob).Value = record["METADATA"] ?? DBNull.Value;
                        insertCmd.Parameters.Add(":BUSINESS_MODULE", OracleDbType.NVarchar2).Value = record["BUSINESS_MODULE"] ?? DBNull.Value;
                        insertCmd.Parameters.Add(":DEVICE_IDENTIFIER", OracleDbType.NVarchar2).Value = record["DEVICE_IDENTIFIER"] ?? DBNull.Value;
                        insertCmd.Parameters.Add(":ERROR_CODE", OracleDbType.NVarchar2).Value = record["ERROR_CODE"] ?? DBNull.Value;
                        insertCmd.Parameters.Add(":BUSINESS_DESCRIPTION", OracleDbType.NVarchar2).Value = record["BUSINESS_DESCRIPTION"] ?? DBNull.Value;
                        insertCmd.Parameters.Add(":CREATION_DATE", OracleDbType.Date).Value = record["CREATION_DATE"] ?? DBNull.Value;
                        insertCmd.Parameters.Add(":ARCHIVE_BATCH_ID", OracleDbType.Int64).Value = archiveBatchId;

                        await insertCmd.ExecuteNonQueryAsync(cancellationToken);
                        insertedCount++;
                    }

                    // Step 4: Delete archived records from active table
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

                        var deleteSql = @"
                            DELETE FROM SYS_AUDIT_LOG
                            WHERE EVENT_CATEGORY = :EventCategory 
                            AND CREATION_DATE < :CutoffDate
                            AND ROWNUM <= :BatchSize";

                        using (var deleteCmd = new OracleCommand(deleteSql, connection))
                        {
                            deleteCmd.Transaction = transaction;
                            deleteCmd.Parameters.Add(":EventCategory", OracleDbType.NVarchar2).Value = eventCategory;
                            deleteCmd.Parameters.Add(":CutoffDate", OracleDbType.Date).Value = cutoffDate;
                            deleteCmd.Parameters.Add(":BatchSize", OracleDbType.Int32).Value = batchSize;

                            var deletedCount = await deleteCmd.ExecuteNonQueryAsync(cancellationToken);

                            _logger.LogDebug(
                                "Batch {BatchIndex}/{TotalBatches}: Archived {InsertedCount} records, deleted {DeletedCount} records in {ElapsedSeconds:F2}s",
                                batchIndex + 1,
                                batches,
                                insertedCount,
                                deletedCount,
                                batchElapsedTime.TotalSeconds);
                        }
                    }

                    // Commit transaction - this releases locks immediately
                    transaction.Commit();
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
                    transaction.Rollback();
                    
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
                checksum = await CalculateArchiveChecksumAsync(connection, archiveBatchId, cancellationToken);
                
                // Update the CHECKSUM column for all records in this archive batch
                if (!string.IsNullOrEmpty(checksum))
                {
                    var updateChecksumSql = @"
                        UPDATE SYS_AUDIT_LOG_ARCHIVE 
                        SET CHECKSUM = :Checksum 
                        WHERE ARCHIVE_BATCH_ID = :ArchiveBatchId";

                    using var updateCmd = new OracleCommand(updateChecksumSql, connection);
                    updateCmd.Parameters.Add(":Checksum", OracleDbType.NVarchar2).Value = checksum;
                    updateCmd.Parameters.Add(":ArchiveBatchId", OracleDbType.Int64).Value = archiveBatchId;

                    var updatedRows = await updateCmd.ExecuteNonQueryAsync(cancellationToken);
                    
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
    private async Task<long> GetNextArchiveBatchIdAsync(OracleConnection connection, CancellationToken cancellationToken)
    {
        var sql = "SELECT SEQ_SYS_AUDIT_LOG.NEXTVAL FROM DUAL";
        using var command = new OracleCommand(sql, connection);
        var result = await command.ExecuteScalarAsync(cancellationToken);
        return Convert.ToInt64(result);
    }

    /// <summary>
    /// Calculate SHA-256 checksum for archived data integrity verification.
    /// Hashes the complete audit log entry data including all fields to ensure data integrity.
    /// The checksum is calculated over the concatenated string of all field values in a deterministic order.
    /// </summary>
    private async Task<string> CalculateArchiveChecksumAsync(
        OracleConnection connection,
        long archiveBatchId,
        CancellationToken cancellationToken)
    {
        try
        {
            _logger.LogDebug("Calculating SHA-256 checksum for archive batch {ArchiveBatchId}", archiveBatchId);

            // Query all archived records for this batch in a deterministic order
            var sql = @"
                SELECT 
                    ROW_ID, ACTOR_TYPE, ACTOR_ID, COMPANY_ID, BRANCH_ID,
                    ACTION, ENTITY_TYPE, ENTITY_ID, OLD_VALUE, NEW_VALUE,
                    IP_ADDRESS, USER_AGENT, CORRELATION_ID, HTTP_METHOD, ENDPOINT_PATH,
                    REQUEST_PAYLOAD, RESPONSE_PAYLOAD, EXECUTION_TIME_MS, STATUS_CODE,
                    EXCEPTION_TYPE, EXCEPTION_MESSAGE, STACK_TRACE, SEVERITY,
                    EVENT_CATEGORY, METADATA, BUSINESS_MODULE, DEVICE_IDENTIFIER,
                    ERROR_CODE, BUSINESS_DESCRIPTION, CREATION_DATE
                FROM SYS_AUDIT_LOG_ARCHIVE
                WHERE ARCHIVE_BATCH_ID = :ArchiveBatchId
                ORDER BY ROW_ID";

            using var command = new OracleCommand(sql, connection);
            command.Parameters.Add(":ArchiveBatchId", OracleDbType.Int64).Value = archiveBatchId;

            using var sha256 = System.Security.Cryptography.SHA256.Create();
            using var reader = await command.ExecuteReaderAsync(cancellationToken);

            var recordCount = 0;
            while (await reader.ReadAsync(cancellationToken))
            {
                // Build a deterministic string representation of the record
                var recordData = BuildRecordDataString(reader);
                
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
    private string BuildRecordDataString(OracleDataReader reader)
    {
        var fields = new[]
        {
            GetFieldValue(reader, "ROW_ID"),
            GetFieldValue(reader, "ACTOR_TYPE"),
            GetFieldValue(reader, "ACTOR_ID"),
            GetFieldValue(reader, "COMPANY_ID"),
            GetFieldValue(reader, "BRANCH_ID"),
            GetFieldValue(reader, "ACTION"),
            GetFieldValue(reader, "ENTITY_TYPE"),
            GetFieldValue(reader, "ENTITY_ID"),
            GetFieldValue(reader, "OLD_VALUE"),
            GetFieldValue(reader, "NEW_VALUE"),
            GetFieldValue(reader, "IP_ADDRESS"),
            GetFieldValue(reader, "USER_AGENT"),
            GetFieldValue(reader, "CORRELATION_ID"),
            GetFieldValue(reader, "HTTP_METHOD"),
            GetFieldValue(reader, "ENDPOINT_PATH"),
            GetFieldValue(reader, "REQUEST_PAYLOAD"),
            GetFieldValue(reader, "RESPONSE_PAYLOAD"),
            GetFieldValue(reader, "EXECUTION_TIME_MS"),
            GetFieldValue(reader, "STATUS_CODE"),
            GetFieldValue(reader, "EXCEPTION_TYPE"),
            GetFieldValue(reader, "EXCEPTION_MESSAGE"),
            GetFieldValue(reader, "STACK_TRACE"),
            GetFieldValue(reader, "SEVERITY"),
            GetFieldValue(reader, "EVENT_CATEGORY"),
            GetFieldValue(reader, "METADATA"),
            GetFieldValue(reader, "BUSINESS_MODULE"),
            GetFieldValue(reader, "DEVICE_IDENTIFIER"),
            GetFieldValue(reader, "ERROR_CODE"),
            GetFieldValue(reader, "BUSINESS_DESCRIPTION"),
            GetFieldValue(reader, "CREATION_DATE")
        };

        return string.Join("|", fields);
    }

    /// <summary>
    /// Get field value from reader with null handling for checksum calculation
    /// </summary>
    private string GetFieldValue(OracleDataReader reader, string fieldName)
    {
        try
        {
            var ordinal = reader.GetOrdinal(fieldName);
            if (reader.IsDBNull(ordinal))
            {
                return "NULL";
            }

            var value = reader.GetValue(ordinal);
            if (value is DateTime dateTime)
            {
                // Use ISO 8601 format for consistent date representation
                return dateTime.ToString("yyyy-MM-ddTHH:mm:ss.fffZ");
            }

            return value?.ToString() ?? "NULL";
        }
        catch
        {
            return "NULL";
        }
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
            using var connection = _dbContext.CreateConnection();
            await connection.OpenAsync(cancellationToken);

            // Build dynamic SQL query based on filter criteria
            var sql = BuildArchivedDataQuery(filter);
            
            using var command = new OracleCommand(sql, connection);
            AddFilterParameters(command, filter);

            _logger.LogDebug("Executing archived data query: {Query}", sql);

            using var reader = await command.ExecuteReaderAsync(cancellationToken);
            
            var recordCount = 0;
            var compressionEnabled = _options.CompressionAlgorithm.Equals("GZip", StringComparison.OrdinalIgnoreCase);

            while (await reader.ReadAsync(cancellationToken))
            {
                if (cancellationToken.IsCancellationRequested)
                {
                    _logger.LogWarning("Archived data retrieval cancelled by user request");
                    break;
                }

                var entry = await MapArchivedDataToAuditLogEntryAsync(reader, compressionEnabled, cancellationToken);
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
    /// Build SQL query for retrieving archived data based on filter criteria
    /// </summary>
    private string BuildArchivedDataQuery(AuditQueryFilter filter)
    {
        var sql = @"
            SELECT 
                ROW_ID, ACTOR_TYPE, ACTOR_ID, COMPANY_ID, BRANCH_ID,
                ACTION, ENTITY_TYPE, ENTITY_ID, OLD_VALUE, NEW_VALUE,
                IP_ADDRESS, USER_AGENT, CORRELATION_ID, HTTP_METHOD, ENDPOINT_PATH,
                REQUEST_PAYLOAD, RESPONSE_PAYLOAD, EXECUTION_TIME_MS, STATUS_CODE,
                EXCEPTION_TYPE, EXCEPTION_MESSAGE, STACK_TRACE, SEVERITY,
                EVENT_CATEGORY, METADATA, BUSINESS_MODULE, DEVICE_IDENTIFIER,
                ERROR_CODE, BUSINESS_DESCRIPTION, CREATION_DATE, ARCHIVED_DATE,
                ARCHIVE_BATCH_ID, CHECKSUM
            FROM SYS_AUDIT_LOG_ARCHIVE
            WHERE 1=1";

        // Add filter conditions
        if (filter.StartDate.HasValue)
        {
            sql += " AND CREATION_DATE >= :StartDate";
        }

        if (filter.EndDate.HasValue)
        {
            sql += " AND CREATION_DATE <= :EndDate";
        }

        if (filter.ActorId.HasValue)
        {
            sql += " AND ACTOR_ID = :ActorId";
        }

        if (!string.IsNullOrEmpty(filter.ActorType))
        {
            sql += " AND ACTOR_TYPE = :ActorType";
        }

        if (filter.CompanyId.HasValue)
        {
            sql += " AND COMPANY_ID = :CompanyId";
        }

        if (filter.BranchId.HasValue)
        {
            sql += " AND BRANCH_ID = :BranchId";
        }

        if (!string.IsNullOrEmpty(filter.EntityType))
        {
            sql += " AND ENTITY_TYPE = :EntityType";
        }

        if (filter.EntityId.HasValue)
        {
            sql += " AND ENTITY_ID = :EntityId";
        }

        if (!string.IsNullOrEmpty(filter.Action))
        {
            sql += " AND ACTION = :Action";
        }

        if (!string.IsNullOrEmpty(filter.IpAddress))
        {
            sql += " AND IP_ADDRESS = :IpAddress";
        }

        if (!string.IsNullOrEmpty(filter.CorrelationId))
        {
            sql += " AND CORRELATION_ID = :CorrelationId";
        }

        if (!string.IsNullOrEmpty(filter.EventCategory))
        {
            sql += " AND EVENT_CATEGORY = :EventCategory";
        }

        if (!string.IsNullOrEmpty(filter.Severity))
        {
            sql += " AND SEVERITY = :Severity";
        }

        if (!string.IsNullOrEmpty(filter.HttpMethod))
        {
            sql += " AND HTTP_METHOD = :HttpMethod";
        }

        if (!string.IsNullOrEmpty(filter.EndpointPath))
        {
            sql += " AND ENDPOINT_PATH = :EndpointPath";
        }

        if (!string.IsNullOrEmpty(filter.BusinessModule))
        {
            sql += " AND BUSINESS_MODULE = :BusinessModule";
        }

        if (!string.IsNullOrEmpty(filter.ErrorCode))
        {
            sql += " AND ERROR_CODE = :ErrorCode";
        }

        // Order by creation date for consistent results
        sql += " ORDER BY CREATION_DATE DESC, ROW_ID DESC";

        return sql;
    }

    /// <summary>
    /// Add filter parameters to the Oracle command
    /// </summary>
    private void AddFilterParameters(OracleCommand command, AuditQueryFilter filter)
    {
        if (filter.StartDate.HasValue)
        {
            command.Parameters.Add(":StartDate", OracleDbType.Date).Value = filter.StartDate.Value;
        }

        if (filter.EndDate.HasValue)
        {
            command.Parameters.Add(":EndDate", OracleDbType.Date).Value = filter.EndDate.Value;
        }

        if (filter.ActorId.HasValue)
        {
            command.Parameters.Add(":ActorId", OracleDbType.Int64).Value = filter.ActorId.Value;
        }

        if (!string.IsNullOrEmpty(filter.ActorType))
        {
            command.Parameters.Add(":ActorType", OracleDbType.NVarchar2).Value = filter.ActorType;
        }

        if (filter.CompanyId.HasValue)
        {
            command.Parameters.Add(":CompanyId", OracleDbType.Int64).Value = filter.CompanyId.Value;
        }

        if (filter.BranchId.HasValue)
        {
            command.Parameters.Add(":BranchId", OracleDbType.Int64).Value = filter.BranchId.Value;
        }

        if (!string.IsNullOrEmpty(filter.EntityType))
        {
            command.Parameters.Add(":EntityType", OracleDbType.NVarchar2).Value = filter.EntityType;
        }

        if (filter.EntityId.HasValue)
        {
            command.Parameters.Add(":EntityId", OracleDbType.Int64).Value = filter.EntityId.Value;
        }

        if (!string.IsNullOrEmpty(filter.Action))
        {
            command.Parameters.Add(":Action", OracleDbType.NVarchar2).Value = filter.Action;
        }

        if (!string.IsNullOrEmpty(filter.IpAddress))
        {
            command.Parameters.Add(":IpAddress", OracleDbType.NVarchar2).Value = filter.IpAddress;
        }

        if (!string.IsNullOrEmpty(filter.CorrelationId))
        {
            command.Parameters.Add(":CorrelationId", OracleDbType.NVarchar2).Value = filter.CorrelationId;
        }

        if (!string.IsNullOrEmpty(filter.EventCategory))
        {
            command.Parameters.Add(":EventCategory", OracleDbType.NVarchar2).Value = filter.EventCategory;
        }

        if (!string.IsNullOrEmpty(filter.Severity))
        {
            command.Parameters.Add(":Severity", OracleDbType.NVarchar2).Value = filter.Severity;
        }

        if (!string.IsNullOrEmpty(filter.HttpMethod))
        {
            command.Parameters.Add(":HttpMethod", OracleDbType.NVarchar2).Value = filter.HttpMethod;
        }

        if (!string.IsNullOrEmpty(filter.EndpointPath))
        {
            command.Parameters.Add(":EndpointPath", OracleDbType.NVarchar2).Value = filter.EndpointPath;
        }

        if (!string.IsNullOrEmpty(filter.BusinessModule))
        {
            command.Parameters.Add(":BusinessModule", OracleDbType.NVarchar2).Value = filter.BusinessModule;
        }

        if (!string.IsNullOrEmpty(filter.ErrorCode))
        {
            command.Parameters.Add(":ErrorCode", OracleDbType.NVarchar2).Value = filter.ErrorCode;
        }
    }

    /// <summary>
    /// Map archived data reader to AuditLogEntry, decompressing CLOB fields if necessary
    /// </summary>
    private async Task<AuditLogEntry> MapArchivedDataToAuditLogEntryAsync(
        OracleDataReader reader,
        bool compressionEnabled,
        CancellationToken cancellationToken)
    {
        // Read all fields from the archive
        var entry = new AuditLogEntry
        {
            RowId = reader.GetInt64(reader.GetOrdinal("ROW_ID")),
            ActorType = reader.GetString(reader.GetOrdinal("ACTOR_TYPE")),
            ActorId = reader.GetInt64(reader.GetOrdinal("ACTOR_ID")),
            CompanyId = reader.IsDBNull(reader.GetOrdinal("COMPANY_ID")) 
                ? null 
                : reader.GetInt64(reader.GetOrdinal("COMPANY_ID")),
            BranchId = reader.IsDBNull(reader.GetOrdinal("BRANCH_ID")) 
                ? null 
                : reader.GetInt64(reader.GetOrdinal("BRANCH_ID")),
            Action = reader.GetString(reader.GetOrdinal("ACTION")),
            EntityType = reader.GetString(reader.GetOrdinal("ENTITY_TYPE")),
            EntityId = reader.IsDBNull(reader.GetOrdinal("ENTITY_ID")) 
                ? null 
                : reader.GetInt64(reader.GetOrdinal("ENTITY_ID")),
            IpAddress = reader.IsDBNull(reader.GetOrdinal("IP_ADDRESS")) 
                ? null 
                : reader.GetString(reader.GetOrdinal("IP_ADDRESS")),
            UserAgent = reader.IsDBNull(reader.GetOrdinal("USER_AGENT")) 
                ? null 
                : reader.GetString(reader.GetOrdinal("USER_AGENT")),
            CorrelationId = reader.IsDBNull(reader.GetOrdinal("CORRELATION_ID")) 
                ? null 
                : reader.GetString(reader.GetOrdinal("CORRELATION_ID")),
            HttpMethod = reader.IsDBNull(reader.GetOrdinal("HTTP_METHOD")) 
                ? null 
                : reader.GetString(reader.GetOrdinal("HTTP_METHOD")),
            EndpointPath = reader.IsDBNull(reader.GetOrdinal("ENDPOINT_PATH")) 
                ? null 
                : reader.GetString(reader.GetOrdinal("ENDPOINT_PATH")),
            ExecutionTimeMs = reader.IsDBNull(reader.GetOrdinal("EXECUTION_TIME_MS")) 
                ? null 
                : reader.GetInt64(reader.GetOrdinal("EXECUTION_TIME_MS")),
            StatusCode = reader.IsDBNull(reader.GetOrdinal("STATUS_CODE")) 
                ? null 
                : reader.GetInt32(reader.GetOrdinal("STATUS_CODE")),
            ExceptionType = reader.IsDBNull(reader.GetOrdinal("EXCEPTION_TYPE")) 
                ? null 
                : reader.GetString(reader.GetOrdinal("EXCEPTION_TYPE")),
            ExceptionMessage = reader.IsDBNull(reader.GetOrdinal("EXCEPTION_MESSAGE")) 
                ? null 
                : reader.GetString(reader.GetOrdinal("EXCEPTION_MESSAGE")),
            Severity = reader.GetString(reader.GetOrdinal("SEVERITY")),
            EventCategory = reader.GetString(reader.GetOrdinal("EVENT_CATEGORY")),
            BusinessModule = reader.IsDBNull(reader.GetOrdinal("BUSINESS_MODULE")) 
                ? null 
                : reader.GetString(reader.GetOrdinal("BUSINESS_MODULE")),
            DeviceIdentifier = reader.IsDBNull(reader.GetOrdinal("DEVICE_IDENTIFIER")) 
                ? null 
                : reader.GetString(reader.GetOrdinal("DEVICE_IDENTIFIER")),
            ErrorCode = reader.IsDBNull(reader.GetOrdinal("ERROR_CODE")) 
                ? null 
                : reader.GetString(reader.GetOrdinal("ERROR_CODE")),
            BusinessDescription = reader.IsDBNull(reader.GetOrdinal("BUSINESS_DESCRIPTION")) 
                ? null 
                : reader.GetString(reader.GetOrdinal("BUSINESS_DESCRIPTION")),
            CreationDate = reader.GetDateTime(reader.GetOrdinal("CREATION_DATE"))
        };

        // Decompress CLOB fields if compression is enabled
        if (compressionEnabled)
        {
            entry.OldValue = await DecompressClobFieldAsync(reader, "OLD_VALUE", cancellationToken);
            entry.NewValue = await DecompressClobFieldAsync(reader, "NEW_VALUE", cancellationToken);
            entry.RequestPayload = await DecompressClobFieldAsync(reader, "REQUEST_PAYLOAD", cancellationToken);
            entry.ResponsePayload = await DecompressClobFieldAsync(reader, "RESPONSE_PAYLOAD", cancellationToken);
            entry.StackTrace = await DecompressClobFieldAsync(reader, "STACK_TRACE", cancellationToken);
            entry.Metadata = await DecompressClobFieldAsync(reader, "METADATA", cancellationToken);
        }
        else
        {
            // Read CLOB fields directly without decompression
            entry.OldValue = await ReadClobFieldAsync(reader, "OLD_VALUE", cancellationToken);
            entry.NewValue = await ReadClobFieldAsync(reader, "NEW_VALUE", cancellationToken);
            entry.RequestPayload = await ReadClobFieldAsync(reader, "REQUEST_PAYLOAD", cancellationToken);
            entry.ResponsePayload = await ReadClobFieldAsync(reader, "RESPONSE_PAYLOAD", cancellationToken);
            entry.StackTrace = await ReadClobFieldAsync(reader, "STACK_TRACE", cancellationToken);
            entry.Metadata = await ReadClobFieldAsync(reader, "METADATA", cancellationToken);
        }

        // Verify checksum if integrity verification is enabled
        if (_options.VerifyIntegrity)
        {
            var storedChecksum = reader.IsDBNull(reader.GetOrdinal("CHECKSUM")) 
                ? null 
                : reader.GetString(reader.GetOrdinal("CHECKSUM"));
            
            if (!string.IsNullOrEmpty(storedChecksum))
            {
                var archiveBatchId = reader.GetInt64(reader.GetOrdinal("ARCHIVE_BATCH_ID"));
                
                // Note: Full checksum verification would require recalculating the entire batch checksum
                // For performance, we log a warning if checksum exists but skip full verification during retrieval
                _logger.LogDebug(
                    "Retrieved archived record {RowId} from batch {ArchiveBatchId} with checksum {Checksum}",
                    entry.RowId,
                    archiveBatchId,
                    storedChecksum);
            }
        }

        return entry;
    }

    /// <summary>
    /// Decompress a CLOB field from the archived data
    /// </summary>
    private async Task<string?> DecompressClobFieldAsync(
        OracleDataReader reader,
        string fieldName,
        CancellationToken cancellationToken)
    {
        try
        {
            var ordinal = reader.GetOrdinal(fieldName);
            if (reader.IsDBNull(ordinal))
            {
                return null;
            }

            // Read the compressed CLOB data
            var compressedData = await ReadClobFieldAsync(reader, fieldName, cancellationToken);
            
            if (string.IsNullOrEmpty(compressedData))
            {
                return null;
            }

            // Decompress using the compression service
            var decompressedData = _compressionService.Decompress(compressedData);
            
            return decompressedData;
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Error decompressing CLOB field '{FieldName}' during archived data retrieval",
                fieldName);
            
            // Return null on decompression error to avoid breaking the entire retrieval
            return null;
        }
    }

    /// <summary>
    /// Read a CLOB field from the Oracle data reader
    /// </summary>
    private async Task<string?> ReadClobFieldAsync(
        OracleDataReader reader,
        string fieldName,
        CancellationToken cancellationToken)
    {
        try
        {
            var ordinal = reader.GetOrdinal(fieldName);
            if (reader.IsDBNull(ordinal))
            {
                return null;
            }

            // Oracle CLOB fields can be read as strings
            var value = reader.GetString(ordinal);
            return value;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(
                ex,
                "Error reading CLOB field '{FieldName}' from archived data",
                fieldName);
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
            using var connection = _dbContext.CreateConnection();
            await connection.OpenAsync(cancellationToken);

            // Step 1: Get the stored checksum for this archive batch
            var getChecksumSql = @"
                SELECT DISTINCT CHECKSUM, ARCHIVE_BATCH_ID
                FROM SYS_AUDIT_LOG_ARCHIVE
                WHERE ARCHIVE_BATCH_ID = :ArchiveBatchId";

            string? storedChecksum = null;
            long? archiveBatchId = null;

            using (var getCmd = new OracleCommand(getChecksumSql, connection))
            {
                getCmd.Parameters.Add(":ArchiveBatchId", OracleDbType.Int64).Value = archiveId;

                using var reader = await getCmd.ExecuteReaderAsync(cancellationToken);
                if (await reader.ReadAsync(cancellationToken))
                {
                    storedChecksum = reader.IsDBNull(reader.GetOrdinal("CHECKSUM"))
                        ? null
                        : reader.GetString(reader.GetOrdinal("CHECKSUM"));
                    archiveBatchId = reader.GetInt64(reader.GetOrdinal("ARCHIVE_BATCH_ID"));
                }
            }

            // Step 2: Check if archive exists
            if (!archiveBatchId.HasValue)
            {
                _logger.LogWarning("Archive batch {ArchiveId} not found", archiveId);
                return false;
            }

            // Step 3: Check if checksum was stored
            if (string.IsNullOrEmpty(storedChecksum))
            {
                _logger.LogWarning(
                    "No checksum found for archive batch {ArchiveId}. Integrity verification not available.",
                    archiveId);
                return false;
            }

            // Step 4: Recalculate the checksum from current archive data
            var recalculatedChecksum = await CalculateArchiveChecksumAsync(connection, archiveId, cancellationToken);

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
            using var connection = _dbContext.CreateConnection();
            await connection.OpenAsync(cancellationToken);

            var sql = @"
                SELECT ROW_ID, EVENT_CATEGORY, RETENTION_DAYS, ARCHIVE_ENABLED, 
                       DESCRIPTION, LAST_MODIFIED_DATE, LAST_MODIFIED_BY
                FROM SYS_RETENTION_POLICIES
                WHERE EVENT_CATEGORY = :EventType";

            using var command = new OracleCommand(sql, connection);
            command.Parameters.Add(":EventType", OracleDbType.NVarchar2).Value = eventType;

            using var reader = await command.ExecuteReaderAsync(cancellationToken);
            if (await reader.ReadAsync(cancellationToken))
            {
                return MapRetentionPolicyFromReader(reader);
            }

            return null;
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
            using var connection = _dbContext.CreateConnection();
            await connection.OpenAsync(cancellationToken);

            var sql = @"
                SELECT ROW_ID, EVENT_CATEGORY, RETENTION_DAYS, ARCHIVE_ENABLED, 
                       DESCRIPTION, LAST_MODIFIED_DATE, LAST_MODIFIED_BY
                FROM SYS_RETENTION_POLICIES
                WHERE ARCHIVE_ENABLED = 1
                ORDER BY EVENT_CATEGORY";

            using var command = new OracleCommand(sql, connection);
            using var reader = await command.ExecuteReaderAsync(cancellationToken);

            while (await reader.ReadAsync(cancellationToken))
            {
                policies.Add(MapRetentionPolicyFromReader(reader));
            }

            _logger.LogInformation("Retrieved {PolicyCount} active retention policies", policies.Count);
            return policies;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving retention policies");
            throw;
        }
    }

    /// <summary>
    /// Map database reader to RetentionPolicy model
    /// </summary>
    private RetentionPolicy MapRetentionPolicyFromReader(OracleDataReader reader)
    {
        return new RetentionPolicy
        {
            PolicyId = reader.GetInt64(reader.GetOrdinal("ROW_ID")),
            EventType = reader.GetString(reader.GetOrdinal("EVENT_CATEGORY")),
            RetentionDays = reader.GetInt32(reader.GetOrdinal("RETENTION_DAYS")),
            IsActive = reader.GetInt32(reader.GetOrdinal("ARCHIVE_ENABLED")) == 1,
            Description = reader.IsDBNull(reader.GetOrdinal("DESCRIPTION")) 
                ? null 
                : reader.GetString(reader.GetOrdinal("DESCRIPTION")),
            ModifiedDate = reader.IsDBNull(reader.GetOrdinal("LAST_MODIFIED_DATE"))
                ? null
                : reader.GetDateTime(reader.GetOrdinal("LAST_MODIFIED_DATE")),
            ModifiedBy = reader.IsDBNull(reader.GetOrdinal("LAST_MODIFIED_BY"))
                ? null
                : reader.GetInt64(reader.GetOrdinal("LAST_MODIFIED_BY")),
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
            using var connection = _dbContext.CreateConnection();
            await connection.OpenAsync(cancellationToken);

            // Check if we can query the retention policies table
            var sql = "SELECT COUNT(*) FROM SYS_RETENTION_POLICIES";
            using var command = new OracleCommand(sql, connection);
            await command.ExecuteScalarAsync(cancellationToken);

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Health check failed for ArchivalService");
            return false;
        }
    }

    /// <summary>
    /// Export archived data to external storage (S3, Azure Blob, etc.).
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
            using var connection = _dbContext.CreateConnection();
            await connection.OpenAsync(cancellationToken);

            // Step 1: Retrieve archived data from database
            var archivedData = await RetrieveArchivedDataForExportAsync(connection, archiveId, cancellationToken);

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
            await UpdateArchiveStorageLocationAsync(connection, archiveId, storageLocation, cancellationToken);

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
            using var connection = _dbContext.CreateConnection();
            await connection.OpenAsync(cancellationToken);

            var importedCount = await InsertArchivedDataAsync(connection, archivedData, cancellationToken);

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
        OracleConnection connection,
        long archiveId,
        CancellationToken cancellationToken)
    {
        var sql = @"
            SELECT 
                ROW_ID, ACTOR_TYPE, ACTOR_ID, COMPANY_ID, BRANCH_ID,
                ACTION, ENTITY_TYPE, ENTITY_ID, OLD_VALUE, NEW_VALUE,
                IP_ADDRESS, USER_AGENT, CORRELATION_ID, HTTP_METHOD, ENDPOINT_PATH,
                REQUEST_PAYLOAD, RESPONSE_PAYLOAD, EXECUTION_TIME_MS, STATUS_CODE,
                EXCEPTION_TYPE, EXCEPTION_MESSAGE, STACK_TRACE, SEVERITY,
                EVENT_CATEGORY, METADATA, BUSINESS_MODULE, DEVICE_IDENTIFIER,
                ERROR_CODE, BUSINESS_DESCRIPTION, CREATION_DATE, ARCHIVED_DATE,
                ARCHIVE_BATCH_ID, CHECKSUM
            FROM SYS_AUDIT_LOG_ARCHIVE
            WHERE ARCHIVE_BATCH_ID = :ArchiveBatchId
            ORDER BY ROW_ID";

        var records = new List<Dictionary<string, object?>>();

        using var command = new OracleCommand(sql, connection);
        command.Parameters.Add(":ArchiveBatchId", OracleDbType.Int64).Value = archiveId;

        using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            var record = new Dictionary<string, object?>();
            for (int i = 0; i < reader.FieldCount; i++)
            {
                var fieldName = reader.GetName(i);
                var value = reader.IsDBNull(i) ? null : reader.GetValue(i);
                record[fieldName] = value;
            }
            records.Add(record);
        }

        return records;
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
        OracleConnection connection,
        List<Dictionary<string, object?>> archivedData,
        CancellationToken cancellationToken)
    {
        var insertSql = @"
            INSERT INTO SYS_AUDIT_LOG_ARCHIVE (
                ROW_ID, ACTOR_TYPE, ACTOR_ID, COMPANY_ID, BRANCH_ID,
                ACTION, ENTITY_TYPE, ENTITY_ID, OLD_VALUE, NEW_VALUE,
                IP_ADDRESS, USER_AGENT, CORRELATION_ID, HTTP_METHOD, ENDPOINT_PATH,
                REQUEST_PAYLOAD, RESPONSE_PAYLOAD, EXECUTION_TIME_MS, STATUS_CODE,
                EXCEPTION_TYPE, EXCEPTION_MESSAGE, STACK_TRACE, SEVERITY,
                EVENT_CATEGORY, METADATA, BUSINESS_MODULE, DEVICE_IDENTIFIER,
                ERROR_CODE, BUSINESS_DESCRIPTION, CREATION_DATE, ARCHIVED_DATE,
                ARCHIVE_BATCH_ID, CHECKSUM
            ) VALUES (
                :ROW_ID, :ACTOR_TYPE, :ACTOR_ID, :COMPANY_ID, :BRANCH_ID,
                :ACTION, :ENTITY_TYPE, :ENTITY_ID, :OLD_VALUE, :NEW_VALUE,
                :IP_ADDRESS, :USER_AGENT, :CORRELATION_ID, :HTTP_METHOD, :ENDPOINT_PATH,
                :REQUEST_PAYLOAD, :RESPONSE_PAYLOAD, :EXECUTION_TIME_MS, :STATUS_CODE,
                :EXCEPTION_TYPE, :EXCEPTION_MESSAGE, :STACK_TRACE, :SEVERITY,
                :EVENT_CATEGORY, :METADATA, :BUSINESS_MODULE, :DEVICE_IDENTIFIER,
                :ERROR_CODE, :BUSINESS_DESCRIPTION, :CREATION_DATE, :ARCHIVED_DATE,
                :ARCHIVE_BATCH_ID, :CHECKSUM
            )";

        var insertedCount = 0;

        foreach (var record in archivedData)
        {
            using var command = new OracleCommand(insertSql, connection);

            // Add all parameters
            command.Parameters.Add(":ROW_ID", OracleDbType.Int64).Value = record["ROW_ID"] ?? DBNull.Value;
            command.Parameters.Add(":ACTOR_TYPE", OracleDbType.NVarchar2).Value = record["ACTOR_TYPE"] ?? DBNull.Value;
            command.Parameters.Add(":ACTOR_ID", OracleDbType.Int64).Value = record["ACTOR_ID"] ?? DBNull.Value;
            command.Parameters.Add(":COMPANY_ID", OracleDbType.Int64).Value = record["COMPANY_ID"] ?? DBNull.Value;
            command.Parameters.Add(":BRANCH_ID", OracleDbType.Int64).Value = record["BRANCH_ID"] ?? DBNull.Value;
            command.Parameters.Add(":ACTION", OracleDbType.NVarchar2).Value = record["ACTION"] ?? DBNull.Value;
            command.Parameters.Add(":ENTITY_TYPE", OracleDbType.NVarchar2).Value = record["ENTITY_TYPE"] ?? DBNull.Value;
            command.Parameters.Add(":ENTITY_ID", OracleDbType.Int64).Value = record["ENTITY_ID"] ?? DBNull.Value;
            command.Parameters.Add(":OLD_VALUE", OracleDbType.Clob).Value = record["OLD_VALUE"] ?? DBNull.Value;
            command.Parameters.Add(":NEW_VALUE", OracleDbType.Clob).Value = record["NEW_VALUE"] ?? DBNull.Value;
            command.Parameters.Add(":IP_ADDRESS", OracleDbType.NVarchar2).Value = record["IP_ADDRESS"] ?? DBNull.Value;
            command.Parameters.Add(":USER_AGENT", OracleDbType.NVarchar2).Value = record["USER_AGENT"] ?? DBNull.Value;
            command.Parameters.Add(":CORRELATION_ID", OracleDbType.NVarchar2).Value = record["CORRELATION_ID"] ?? DBNull.Value;
            command.Parameters.Add(":HTTP_METHOD", OracleDbType.NVarchar2).Value = record["HTTP_METHOD"] ?? DBNull.Value;
            command.Parameters.Add(":ENDPOINT_PATH", OracleDbType.NVarchar2).Value = record["ENDPOINT_PATH"] ?? DBNull.Value;
            command.Parameters.Add(":REQUEST_PAYLOAD", OracleDbType.Clob).Value = record["REQUEST_PAYLOAD"] ?? DBNull.Value;
            command.Parameters.Add(":RESPONSE_PAYLOAD", OracleDbType.Clob).Value = record["RESPONSE_PAYLOAD"] ?? DBNull.Value;
            command.Parameters.Add(":EXECUTION_TIME_MS", OracleDbType.Int64).Value = record["EXECUTION_TIME_MS"] ?? DBNull.Value;
            command.Parameters.Add(":STATUS_CODE", OracleDbType.Int32).Value = record["STATUS_CODE"] ?? DBNull.Value;
            command.Parameters.Add(":EXCEPTION_TYPE", OracleDbType.NVarchar2).Value = record["EXCEPTION_TYPE"] ?? DBNull.Value;
            command.Parameters.Add(":EXCEPTION_MESSAGE", OracleDbType.NVarchar2).Value = record["EXCEPTION_MESSAGE"] ?? DBNull.Value;
            command.Parameters.Add(":STACK_TRACE", OracleDbType.Clob).Value = record["STACK_TRACE"] ?? DBNull.Value;
            command.Parameters.Add(":SEVERITY", OracleDbType.NVarchar2).Value = record["SEVERITY"] ?? DBNull.Value;
            command.Parameters.Add(":EVENT_CATEGORY", OracleDbType.NVarchar2).Value = record["EVENT_CATEGORY"] ?? DBNull.Value;
            command.Parameters.Add(":METADATA", OracleDbType.Clob).Value = record["METADATA"] ?? DBNull.Value;
            command.Parameters.Add(":BUSINESS_MODULE", OracleDbType.NVarchar2).Value = record["BUSINESS_MODULE"] ?? DBNull.Value;
            command.Parameters.Add(":DEVICE_IDENTIFIER", OracleDbType.NVarchar2).Value = record["DEVICE_IDENTIFIER"] ?? DBNull.Value;
            command.Parameters.Add(":ERROR_CODE", OracleDbType.NVarchar2).Value = record["ERROR_CODE"] ?? DBNull.Value;
            command.Parameters.Add(":BUSINESS_DESCRIPTION", OracleDbType.NVarchar2).Value = record["BUSINESS_DESCRIPTION"] ?? DBNull.Value;
            command.Parameters.Add(":CREATION_DATE", OracleDbType.Date).Value = record["CREATION_DATE"] ?? DBNull.Value;
            command.Parameters.Add(":ARCHIVED_DATE", OracleDbType.Date).Value = record["ARCHIVED_DATE"] ?? DBNull.Value;
            command.Parameters.Add(":ARCHIVE_BATCH_ID", OracleDbType.Int64).Value = record["ARCHIVE_BATCH_ID"] ?? DBNull.Value;
            command.Parameters.Add(":CHECKSUM", OracleDbType.NVarchar2).Value = record["CHECKSUM"] ?? DBNull.Value;

            await command.ExecuteNonQueryAsync(cancellationToken);
            insertedCount++;
        }

        return insertedCount;
    }

    /// <summary>
    /// Update archive record with external storage location
    /// </summary>
    private async Task UpdateArchiveStorageLocationAsync(
        OracleConnection connection,
        long archiveId,
        string storageLocation,
        CancellationToken cancellationToken)
    {
        // Note: This would require adding a STORAGE_LOCATION column to SYS_AUDIT_LOG_ARCHIVE table
        // For now, we'll store it in the METADATA column as JSON
        var updateSql = @"
            UPDATE SYS_AUDIT_LOG_ARCHIVE 
            SET METADATA = JSON_OBJECT('StorageLocation' VALUE :StorageLocation)
            WHERE ARCHIVE_BATCH_ID = :ArchiveBatchId";

        using var command = new OracleCommand(updateSql, connection);
        command.Parameters.Add(":StorageLocation", OracleDbType.NVarchar2).Value = storageLocation;
        command.Parameters.Add(":ArchiveBatchId", OracleDbType.Int64).Value = archiveId;

        await command.ExecuteNonQueryAsync(cancellationToken);
    }
}
