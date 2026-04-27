namespace ThinkOnErp.Domain.Interfaces;

/// <summary>
/// Interface for external storage providers that support cold storage of archived audit data.
/// Implementations include AWS S3, Azure Blob Storage, and file system storage.
/// Used by ArchivalService to export archived data to external storage for long-term retention.
/// </summary>
public interface IExternalStorageProvider
{
    /// <summary>
    /// Upload archived data to external storage.
    /// Data should be compressed before upload to reduce storage costs and transfer time.
    /// </summary>
    /// <param name="archiveId">Unique identifier for the archive batch</param>
    /// <param name="data">Compressed archive data to upload</param>
    /// <param name="metadata">Additional metadata about the archive (event type, date range, checksum, etc.)</param>
    /// <param name="cancellationToken">Cancellation token for async operation</param>
    /// <returns>Storage location URL or path where the data was uploaded</returns>
    Task<string> UploadAsync(
        long archiveId, 
        byte[] data, 
        Dictionary<string, string> metadata, 
        CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Download archived data from external storage.
    /// Returns compressed data that should be decompressed by the caller.
    /// </summary>
    /// <param name="storageLocation">Storage location URL or path returned from UploadAsync</param>
    /// <param name="cancellationToken">Cancellation token for async operation</param>
    /// <returns>Compressed archive data</returns>
    Task<byte[]> DownloadAsync(
        string storageLocation, 
        CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Delete archived data from external storage.
    /// Used when archived data has exceeded its retention period and should be permanently removed.
    /// </summary>
    /// <param name="storageLocation">Storage location URL or path to delete</param>
    /// <param name="cancellationToken">Cancellation token for async operation</param>
    /// <returns>True if deletion was successful, false otherwise</returns>
    Task<bool> DeleteAsync(
        string storageLocation, 
        CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Check if archived data exists at the specified storage location.
    /// </summary>
    /// <param name="storageLocation">Storage location URL or path to check</param>
    /// <param name="cancellationToken">Cancellation token for async operation</param>
    /// <returns>True if the data exists, false otherwise</returns>
    Task<bool> ExistsAsync(
        string storageLocation, 
        CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Get metadata about archived data stored at the specified location.
    /// </summary>
    /// <param name="storageLocation">Storage location URL or path</param>
    /// <param name="cancellationToken">Cancellation token for async operation</param>
    /// <returns>Metadata dictionary containing information about the archived data</returns>
    Task<Dictionary<string, string>> GetMetadataAsync(
        string storageLocation, 
        CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Verify the integrity of archived data by comparing checksums.
    /// </summary>
    /// <param name="storageLocation">Storage location URL or path</param>
    /// <param name="expectedChecksum">Expected SHA-256 checksum of the data</param>
    /// <param name="cancellationToken">Cancellation token for async operation</param>
    /// <returns>True if the checksum matches, false if there is a mismatch indicating corruption</returns>
    Task<bool> VerifyIntegrityAsync(
        string storageLocation, 
        string expectedChecksum, 
        CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Get the storage provider name (e.g., "S3", "AzureBlob", "FileSystem")
    /// </summary>
    string ProviderName { get; }
    
    /// <summary>
    /// Check if the storage provider is properly configured and accessible.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token for async operation</param>
    /// <returns>True if the provider is healthy and accessible, false otherwise</returns>
    Task<bool> IsHealthyAsync(CancellationToken cancellationToken = default);
}
