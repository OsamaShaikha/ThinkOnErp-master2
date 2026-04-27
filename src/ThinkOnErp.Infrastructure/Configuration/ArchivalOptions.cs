using System.ComponentModel.DataAnnotations;

namespace ThinkOnErp.Infrastructure.Configuration;

/// <summary>
/// Configuration options for the archival background service.
/// Controls scheduling, batch processing, compression, and storage settings for audit data archival.
/// </summary>
public class ArchivalOptions
{
    /// <summary>
    /// Configuration section name in appsettings.json
    /// </summary>
    public const string SectionName = "Archival";

    /// <summary>
    /// Indicates if the archival background service is enabled.
    /// When disabled, no automatic archival will occur.
    /// Default: true
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// Cron expression for archival schedule.
    /// Default: "0 2 * * *" (daily at 2 AM UTC)
    /// 
    /// Common cron expressions:
    /// - "0 2 * * *" - Daily at 2 AM
    /// - "0 2 * * 0" - Weekly on Sunday at 2 AM
    /// - "0 2 1 * *" - Monthly on the 1st at 2 AM
    /// - "0 */6 * * *" - Every 6 hours
    /// </summary>
    [Required(ErrorMessage = "Schedule is required")]
    [RegularExpression(@"^(\*|([0-9]|1[0-9]|2[0-9]|3[0-9]|4[0-9]|5[0-9])|\*\/([0-9]|1[0-9]|2[0-9]|3[0-9]|4[0-9]|5[0-9])) (\*|([0-9]|1[0-9]|2[0-3])|\*\/([0-9]|1[0-9]|2[0-3])) (\*|([1-9]|1[0-9]|2[0-9]|3[0-1])|\*\/([1-9]|1[0-9]|2[0-9]|3[0-1])) (\*|([1-9]|1[0-2])|\*\/([1-9]|1[0-2])) (\*|([0-6])|\*\/([0-6]))$", 
        ErrorMessage = "Schedule must be a valid cron expression")]
    public string Schedule { get; set; } = "0 2 * * *";

    /// <summary>
    /// Maximum number of records to archive in a single batch.
    /// Larger batches improve performance but may cause longer transactions.
    /// Recommended: 1000 for production to avoid long-running transactions.
    /// Default: 1000
    /// </summary>
    [Range(100, 10000, ErrorMessage = "BatchSize must be between 100 and 10000")]
    public int BatchSize { get; set; } = 1000;

    /// <summary>
    /// Maximum transaction timeout in seconds for each batch operation.
    /// Transactions exceeding this timeout will be rolled back.
    /// Default: 30 seconds
    /// </summary>
    [Range(10, 300, ErrorMessage = "TransactionTimeoutSeconds must be between 10 and 300 seconds")]
    public int TransactionTimeoutSeconds { get; set; } = 30;

    /// <summary>
    /// Compression algorithm to use for archived data.
    /// Supported values: "GZip", "None"
    /// Default: "GZip"
    /// </summary>
    [Required(ErrorMessage = "CompressionAlgorithm is required")]
    [RegularExpression("^(GZip|None)$", ErrorMessage = "CompressionAlgorithm must be 'GZip' or 'None'")]
    public string CompressionAlgorithm { get; set; } = "GZip";

    /// <summary>
    /// Storage provider for archived data.
    /// Supported values: "Database", "FileSystem", "S3", "AzureBlob"
    /// Default: "Database"
    /// </summary>
    [Required(ErrorMessage = "StorageProvider is required")]
    [RegularExpression("^(Database|FileSystem|S3|AzureBlob)$", ErrorMessage = "StorageProvider must be 'Database', 'FileSystem', 'S3', or 'AzureBlob'")]
    public string StorageProvider { get; set; } = "Database";

    /// <summary>
    /// Connection string or configuration for the storage provider.
    /// Required for FileSystem, S3, and AzureBlob providers.
    /// </summary>
    public string? StorageConnectionString { get; set; }

    /// <summary>
    /// Indicates if integrity verification should be performed after archival.
    /// Calculates and verifies SHA-256 checksums for archived data.
    /// Default: true
    /// </summary>
    public bool VerifyIntegrity { get; set; } = true;

    /// <summary>
    /// Timeout for archival operations (in minutes).
    /// Operations exceeding this timeout will be cancelled.
    /// Default: 60 minutes
    /// </summary>
    [Range(5, 1440, ErrorMessage = "TimeoutMinutes must be between 5 and 1440 minutes (24 hours)")]
    public int TimeoutMinutes { get; set; } = 60;

    /// <summary>
    /// Indicates if archived data should be encrypted.
    /// Requires EncryptionKeyId to be configured.
    /// Default: false
    /// </summary>
    public bool EncryptArchivedData { get; set; } = false;

    /// <summary>
    /// Encryption key identifier for archived data.
    /// Required when EncryptArchivedData is true.
    /// </summary>
    public string? EncryptionKeyId { get; set; }

    /// <summary>
    /// Indicates if the service should run immediately on startup (for testing).
    /// When true, archival runs immediately after service starts.
    /// Default: false
    /// </summary>
    public bool RunOnStartup { get; set; } = false;

    /// <summary>
    /// Time zone for cron schedule evaluation.
    /// Default: "UTC"
    /// </summary>
    [Required(ErrorMessage = "TimeZone is required")]
    [MinLength(1, ErrorMessage = "TimeZone cannot be empty")]
    public string TimeZone { get; set; } = "UTC";
}
