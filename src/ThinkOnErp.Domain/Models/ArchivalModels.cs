namespace ThinkOnErp.Domain.Models;

/// <summary>
/// Result of an archival operation containing statistics and status information
/// </summary>
public class ArchivalResult
{
    /// <summary>
    /// Unique identifier for this archival operation
    /// </summary>
    public long ArchiveId { get; set; }
    
    /// <summary>
    /// Number of records successfully archived
    /// </summary>
    public int RecordsArchived { get; set; }
    
    /// <summary>
    /// Start date of the archived data range
    /// </summary>
    public DateTime StartDate { get; set; }
    
    /// <summary>
    /// End date of the archived data range
    /// </summary>
    public DateTime EndDate { get; set; }
    
    /// <summary>
    /// Size of archived data before compression (in bytes)
    /// </summary>
    public long UncompressedSize { get; set; }
    
    /// <summary>
    /// Size of archived data after compression (in bytes)
    /// </summary>
    public long CompressedSize { get; set; }
    
    /// <summary>
    /// Compression ratio (compressed size / uncompressed size)
    /// </summary>
    public double CompressionRatio => UncompressedSize > 0 
        ? (double)CompressedSize / UncompressedSize 
        : 0;
    
    /// <summary>
    /// SHA-256 checksum for integrity verification
    /// </summary>
    public string Checksum { get; set; } = null!;
    
    /// <summary>
    /// When the archival operation started
    /// </summary>
    public DateTime ArchivalStartTime { get; set; }
    
    /// <summary>
    /// When the archival operation completed
    /// </summary>
    public DateTime ArchivalEndTime { get; set; }
    
    /// <summary>
    /// Duration of the archival operation
    /// </summary>
    public TimeSpan Duration => ArchivalEndTime - ArchivalStartTime;
    
    /// <summary>
    /// Indicates if the archival operation was successful
    /// </summary>
    public bool IsSuccess { get; set; }
    
    /// <summary>
    /// Error message if the archival operation failed
    /// </summary>
    public string? ErrorMessage { get; set; }
    
    /// <summary>
    /// Storage location of the archived data (file path, S3 URL, Azure Blob URL, etc.)
    /// </summary>
    public string? StorageLocation { get; set; }
    
    /// <summary>
    /// Additional metadata about the archival operation
    /// </summary>
    public Dictionary<string, object> Metadata { get; set; } = new();
}

/// <summary>
/// Defines retention policies for different types of audit events
/// Specifies how long data should be retained before archival or deletion
/// </summary>
public class RetentionPolicy
{
    /// <summary>
    /// Unique identifier for the retention policy
    /// </summary>
    public long PolicyId { get; set; }
    
    /// <summary>
    /// Event type this policy applies to (e.g., "Authentication", "DataChange", "Financial", "GDPR")
    /// </summary>
    public string EventType { get; set; } = null!;
    
    /// <summary>
    /// Number of days to retain data in the active database before archival
    /// </summary>
    public int RetentionDays { get; set; }
    
    /// <summary>
    /// Number of days to retain archived data before permanent deletion
    /// Set to -1 for indefinite retention
    /// </summary>
    public int ArchiveRetentionDays { get; set; }
    
    /// <summary>
    /// Indicates if this policy is currently active
    /// </summary>
    public bool IsActive { get; set; }
    
    /// <summary>
    /// Description of the policy and its compliance requirements
    /// </summary>
    public string? Description { get; set; }
    
    /// <summary>
    /// Regulatory requirement this policy satisfies (e.g., "GDPR", "SOX", "ISO 27001")
    /// </summary>
    public string? ComplianceRequirement { get; set; }
    
    /// <summary>
    /// When this policy was created
    /// </summary>
    public DateTime CreatedDate { get; set; }
    
    /// <summary>
    /// When this policy was last modified
    /// </summary>
    public DateTime? ModifiedDate { get; set; }
    
    /// <summary>
    /// User who created this policy
    /// </summary>
    public long CreatedBy { get; set; }
    
    /// <summary>
    /// User who last modified this policy
    /// </summary>
    public long? ModifiedBy { get; set; }
    
    /// <summary>
    /// Additional configuration options for this policy
    /// </summary>
    public Dictionary<string, object> Configuration { get; set; } = new();
}

/// <summary>
/// Configuration options for the archival service
/// </summary>
public class ArchivalConfiguration
{
    /// <summary>
    /// Indicates if archival is enabled
    /// </summary>
    public bool Enabled { get; set; } = true;
    
    /// <summary>
    /// Schedule for automatic archival (cron expression)
    /// Default: "0 2 * * *" (daily at 2 AM)
    /// </summary>
    public string Schedule { get; set; } = "0 2 * * *";
    
    /// <summary>
    /// Maximum number of records to archive in a single batch
    /// </summary>
    public int BatchSize { get; set; } = 10000;
    
    /// <summary>
    /// Compression algorithm to use (GZip, Brotli, None)
    /// </summary>
    public string CompressionAlgorithm { get; set; } = "GZip";
    
    /// <summary>
    /// Storage provider for archived data (Database, FileSystem, S3, AzureBlob)
    /// </summary>
    public string StorageProvider { get; set; } = "Database";
    
    /// <summary>
    /// Connection string or configuration for the storage provider
    /// </summary>
    public string? StorageConnectionString { get; set; }
    
    /// <summary>
    /// Indicates if integrity verification should be performed after archival
    /// </summary>
    public bool VerifyIntegrity { get; set; } = true;
    
    /// <summary>
    /// Timeout for archival operations (in minutes)
    /// </summary>
    public int TimeoutMinutes { get; set; } = 60;
    
    /// <summary>
    /// Indicates if archived data should be encrypted
    /// </summary>
    public bool EncryptArchivedData { get; set; } = false;
    
    /// <summary>
    /// Encryption key identifier for archived data
    /// </summary>
    public string? EncryptionKeyId { get; set; }
}

/// <summary>
/// Statistics about archived data
/// </summary>
public class ArchivalStatistics
{
    /// <summary>
    /// Total number of archived records
    /// </summary>
    public long TotalArchivedRecords { get; set; }
    
    /// <summary>
    /// Total size of archived data (compressed)
    /// </summary>
    public long TotalArchivedSize { get; set; }
    
    /// <summary>
    /// Number of archival operations performed
    /// </summary>
    public int ArchivalOperationCount { get; set; }
    
    /// <summary>
    /// Date of the oldest archived record
    /// </summary>
    public DateTime? OldestArchivedDate { get; set; }
    
    /// <summary>
    /// Date of the most recent archival operation
    /// </summary>
    public DateTime? LastArchivalDate { get; set; }
    
    /// <summary>
    /// Average compression ratio across all archives
    /// </summary>
    public double AverageCompressionRatio { get; set; }
    
    /// <summary>
    /// Statistics by event type
    /// </summary>
    public Dictionary<string, EventTypeStatistics> StatisticsByEventType { get; set; } = new();
}

/// <summary>
/// Statistics for a specific event type
/// </summary>
public class EventTypeStatistics
{
    /// <summary>
    /// Event type name
    /// </summary>
    public string EventType { get; set; } = null!;
    
    /// <summary>
    /// Number of archived records for this event type
    /// </summary>
    public long ArchivedRecordCount { get; set; }
    
    /// <summary>
    /// Total size of archived data for this event type
    /// </summary>
    public long TotalSize { get; set; }
    
    /// <summary>
    /// Date of the oldest archived record for this event type
    /// </summary>
    public DateTime? OldestDate { get; set; }
    
    /// <summary>
    /// Date of the most recent archived record for this event type
    /// </summary>
    public DateTime? NewestDate { get; set; }
}
