using System.Security.Cryptography;
using Azure;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Microsoft.Extensions.Logging;
using ThinkOnErp.Domain.Interfaces;

namespace ThinkOnErp.Infrastructure.Services;

/// <summary>
/// Azure Blob Storage provider for cold storage of archived audit data.
/// Supports uploading, downloading, and managing archived data in Azure Blob containers.
/// Implements data integrity verification using checksums and metadata storage.
/// Uses Cool tier for cost-effective long-term storage.
/// </summary>
public class AzureBlobStorageProvider : IExternalStorageProvider
{
    private readonly BlobServiceClient _blobServiceClient;
    private readonly ILogger<AzureBlobStorageProvider> _logger;
    private readonly string _containerName;
    private readonly string _prefix;

    public string ProviderName => "AzureBlob";

    /// <summary>
    /// Initialize Azure Blob storage provider with connection string format:
    /// "ConnectionString=DefaultEndpointsProtocol=https;AccountName=xxx;AccountKey=yyy;EndpointSuffix=core.windows.net;ContainerName=audit-archives;Prefix=archives/"
    /// Or simplified:
    /// "AccountName=myaccount;AccountKey=mykey;ContainerName=audit-archives;Prefix=archives/"
    /// </summary>
    public AzureBlobStorageProvider(
        BlobServiceClient blobServiceClient,
        ILogger<AzureBlobStorageProvider> logger,
        string connectionString)
    {
        _blobServiceClient = blobServiceClient ?? throw new ArgumentNullException(nameof(blobServiceClient));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new ArgumentException("Connection string cannot be null or empty", nameof(connectionString));
        }

        // Parse connection string
        var config = ParseConnectionString(connectionString);
        _containerName = config["ContainerName"];
        _prefix = config.ContainsKey("Prefix") ? config["Prefix"] : "audit-archives/";

        _logger.LogInformation(
            "Initialized Azure Blob storage provider for container '{ContainerName}' with prefix '{Prefix}'",
            _containerName,
            _prefix);
    }

    public async Task<string> UploadAsync(
        long archiveId,
        byte[] data,
        Dictionary<string, string> metadata,
        CancellationToken cancellationToken = default)
    {
        if (data == null || data.Length == 0)
        {
            throw new ArgumentException("Data cannot be null or empty", nameof(data));
        }

        var blobName = GenerateBlobName(archiveId);

        try
        {
            _logger.LogInformation(
                "Uploading archive {ArchiveId} to Azure Blob container '{ContainerName}' with name '{BlobName}' ({SizeMB:N2} MB)",
                archiveId,
                _containerName,
                blobName,
                data.Length / (1024.0 * 1024.0));

            // Calculate checksum for integrity verification
            var checksum = CalculateSha256Checksum(data);
            metadata["Checksum"] = checksum;
            metadata["UploadDate"] = DateTime.UtcNow.ToString("O");
            metadata["ArchiveId"] = archiveId.ToString();

            var containerClient = _blobServiceClient.GetBlobContainerClient(_containerName);
            
            // Ensure container exists
            await containerClient.CreateIfNotExistsAsync(cancellationToken: cancellationToken);

            var blobClient = containerClient.GetBlobClient(blobName);

            // Upload with Cool tier for cost savings
            var uploadOptions = new BlobUploadOptions
            {
                AccessTier = AccessTier.Cool,
                Metadata = metadata,
                HttpHeaders = new BlobHttpHeaders
                {
                    ContentType = "application/octet-stream"
                }
            };

            using var stream = new MemoryStream(data);
            var response = await blobClient.UploadAsync(stream, uploadOptions, cancellationToken);

            var storageLocation = blobClient.Uri.ToString();

            _logger.LogInformation(
                "Successfully uploaded archive {ArchiveId} to Azure Blob: {StorageLocation}",
                archiveId,
                storageLocation);

            return storageLocation;
        }
        catch (RequestFailedException ex)
        {
            _logger.LogError(
                ex,
                "Azure Blob error uploading archive {ArchiveId}: {ErrorCode} - {ErrorMessage}",
                archiveId,
                ex.ErrorCode,
                ex.Message);
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error uploading archive {ArchiveId} to Azure Blob", archiveId);
            throw;
        }
    }

    public async Task<byte[]> DownloadAsync(
        string storageLocation,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(storageLocation))
        {
            throw new ArgumentException("Storage location cannot be null or empty", nameof(storageLocation));
        }

        var blobName = ExtractBlobNameFromLocation(storageLocation);

        try
        {
            _logger.LogInformation(
                "Downloading archive from Azure Blob container '{ContainerName}' with name '{BlobName}'",
                _containerName,
                blobName);

            var containerClient = _blobServiceClient.GetBlobContainerClient(_containerName);
            var blobClient = containerClient.GetBlobClient(blobName);

            using var memoryStream = new MemoryStream();
            await blobClient.DownloadToAsync(memoryStream, cancellationToken);

            var data = memoryStream.ToArray();

            _logger.LogInformation(
                "Successfully downloaded archive from Azure Blob: {StorageLocation} ({SizeMB:N2} MB)",
                storageLocation,
                data.Length / (1024.0 * 1024.0));

            return data;
        }
        catch (RequestFailedException ex)
        {
            _logger.LogError(
                ex,
                "Azure Blob error downloading archive from {StorageLocation}: {ErrorCode} - {ErrorMessage}",
                storageLocation,
                ex.ErrorCode,
                ex.Message);
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error downloading archive from Azure Blob: {StorageLocation}", storageLocation);
            throw;
        }
    }

    public async Task<bool> DeleteAsync(
        string storageLocation,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(storageLocation))
        {
            throw new ArgumentException("Storage location cannot be null or empty", nameof(storageLocation));
        }

        var blobName = ExtractBlobNameFromLocation(storageLocation);

        try
        {
            _logger.LogInformation(
                "Deleting archive from Azure Blob container '{ContainerName}' with name '{BlobName}'",
                _containerName,
                blobName);

            var containerClient = _blobServiceClient.GetBlobContainerClient(_containerName);
            var blobClient = containerClient.GetBlobClient(blobName);

            var response = await blobClient.DeleteIfExistsAsync(cancellationToken: cancellationToken);

            if (response.Value)
            {
                _logger.LogInformation("Successfully deleted archive from Azure Blob: {StorageLocation}", storageLocation);
            }
            else
            {
                _logger.LogWarning("Archive not found in Azure Blob: {StorageLocation}", storageLocation);
            }

            return response.Value;
        }
        catch (RequestFailedException ex)
        {
            _logger.LogError(
                ex,
                "Azure Blob error deleting archive from {StorageLocation}: {ErrorCode} - {ErrorMessage}",
                storageLocation,
                ex.ErrorCode,
                ex.Message);
            return false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting archive from Azure Blob: {StorageLocation}", storageLocation);
            return false;
        }
    }

    public async Task<bool> ExistsAsync(
        string storageLocation,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(storageLocation))
        {
            return false;
        }

        var blobName = ExtractBlobNameFromLocation(storageLocation);

        try
        {
            var containerClient = _blobServiceClient.GetBlobContainerClient(_containerName);
            var blobClient = containerClient.GetBlobClient(blobName);

            var response = await blobClient.ExistsAsync(cancellationToken);
            return response.Value;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error checking if archive exists in Azure Blob: {StorageLocation}", storageLocation);
            return false;
        }
    }

    public async Task<Dictionary<string, string>> GetMetadataAsync(
        string storageLocation,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(storageLocation))
        {
            throw new ArgumentException("Storage location cannot be null or empty", nameof(storageLocation));
        }

        var blobName = ExtractBlobNameFromLocation(storageLocation);

        try
        {
            var containerClient = _blobServiceClient.GetBlobContainerClient(_containerName);
            var blobClient = containerClient.GetBlobClient(blobName);

            var properties = await blobClient.GetPropertiesAsync(cancellationToken: cancellationToken);

            var metadata = new Dictionary<string, string>(properties.Value.Metadata);

            // Add standard properties
            metadata["ContentLength"] = properties.Value.ContentLength.ToString();
            metadata["LastModified"] = properties.Value.LastModified.ToString("O");
            metadata["ETag"] = properties.Value.ETag.ToString();
            metadata["ContentType"] = properties.Value.ContentType;

            return metadata;
        }
        catch (RequestFailedException ex)
        {
            _logger.LogError(
                ex,
                "Azure Blob error getting metadata for {StorageLocation}: {ErrorCode} - {ErrorMessage}",
                storageLocation,
                ex.ErrorCode,
                ex.Message);
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting metadata from Azure Blob: {StorageLocation}", storageLocation);
            throw;
        }
    }

    public async Task<bool> VerifyIntegrityAsync(
        string storageLocation,
        string expectedChecksum,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(storageLocation))
        {
            throw new ArgumentException("Storage location cannot be null or empty", nameof(storageLocation));
        }

        if (string.IsNullOrWhiteSpace(expectedChecksum))
        {
            throw new ArgumentException("Expected checksum cannot be null or empty", nameof(expectedChecksum));
        }

        try
        {
            // Get metadata to retrieve stored checksum
            var metadata = await GetMetadataAsync(storageLocation, cancellationToken);

            if (!metadata.TryGetValue("Checksum", out var storedChecksum))
            {
                _logger.LogWarning(
                    "No checksum found in metadata for {StorageLocation}. Cannot verify integrity.",
                    storageLocation);
                return false;
            }

            var isValid = string.Equals(storedChecksum, expectedChecksum, StringComparison.OrdinalIgnoreCase);

            if (isValid)
            {
                _logger.LogInformation(
                    "Integrity verification PASSED for {StorageLocation}. Checksum: {Checksum}",
                    storageLocation,
                    expectedChecksum);
            }
            else
            {
                _logger.LogError(
                    "Integrity verification FAILED for {StorageLocation}. Expected: {ExpectedChecksum}, Stored: {StoredChecksum}",
                    storageLocation,
                    expectedChecksum,
                    storedChecksum);
            }

            return isValid;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error verifying integrity for {StorageLocation}", storageLocation);
            return false;
        }
    }

    public async Task<bool> IsHealthyAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            // Try to get container properties to verify access
            var containerClient = _blobServiceClient.GetBlobContainerClient(_containerName);
            await containerClient.GetPropertiesAsync(cancellationToken: cancellationToken);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Azure Blob health check failed for container '{ContainerName}'", _containerName);
            return false;
        }
    }

    /// <summary>
    /// Generate blob name for an archive based on archive ID and date
    /// Format: {prefix}year=YYYY/month=MM/archive-{archiveId}.bin
    /// </summary>
    private string GenerateBlobName(long archiveId)
    {
        var now = DateTime.UtcNow;
        return $"{_prefix}year={now:yyyy}/month={now:MM}/archive-{archiveId}.bin";
    }

    /// <summary>
    /// Extract blob name from storage location URL
    /// </summary>
    private string ExtractBlobNameFromLocation(string storageLocation)
    {
        if (Uri.TryCreate(storageLocation, UriKind.Absolute, out var uri))
        {
            // Extract path from URL (remove leading slash)
            return uri.AbsolutePath.TrimStart('/');
        }

        return storageLocation;
    }

    /// <summary>
    /// Calculate SHA-256 checksum for data integrity verification
    /// </summary>
    private string CalculateSha256Checksum(byte[] data)
    {
        using var sha256 = SHA256.Create();
        var hashBytes = sha256.ComputeHash(data);
        return BitConverter.ToString(hashBytes).Replace("-", "").ToLowerInvariant();
    }

    /// <summary>
    /// Parse connection string into configuration dictionary
    /// </summary>
    private Dictionary<string, string> ParseConnectionString(string connectionString)
    {
        var config = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        var parts = connectionString.Split(';', StringSplitOptions.RemoveEmptyEntries);
        foreach (var part in parts)
        {
            var keyValue = part.Split('=', 2);
            if (keyValue.Length == 2)
            {
                config[keyValue[0].Trim()] = keyValue[1].Trim();
            }
        }

        if (!config.ContainsKey("ContainerName"))
        {
            throw new ArgumentException("Connection string must contain 'ContainerName'", nameof(connectionString));
        }

        return config;
    }
}
