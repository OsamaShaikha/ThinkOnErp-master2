using System.Security.Cryptography;
using System.Text;
using Amazon.S3;
using Amazon.S3.Model;
using Microsoft.Extensions.Logging;
using ThinkOnErp.Domain.Interfaces;
using ThinkOnErp.Infrastructure.Configuration;

namespace ThinkOnErp.Infrastructure.Services;

/// <summary>
/// AWS S3 storage provider for cold storage of archived audit data.
/// Supports uploading, downloading, and managing archived data in S3 buckets.
/// Implements data integrity verification using checksums and metadata storage.
/// </summary>
public class S3StorageProvider : IExternalStorageProvider
{
    private readonly IAmazonS3 _s3Client;
    private readonly ILogger<S3StorageProvider> _logger;
    private readonly string _bucketName;
    private readonly string _prefix;

    public string ProviderName => "S3";

    /// <summary>
    /// Initialize S3 storage provider with connection string format:
    /// "BucketName=my-bucket;Region=us-east-1;Prefix=audit-archives/"
    /// Or with AWS credentials:
    /// "BucketName=my-bucket;Region=us-east-1;AccessKey=xxx;SecretKey=yyy;Prefix=audit-archives/"
    /// </summary>
    public S3StorageProvider(
        IAmazonS3 s3Client,
        ILogger<S3StorageProvider> logger,
        string connectionString)
    {
        _s3Client = s3Client ?? throw new ArgumentNullException(nameof(s3Client));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new ArgumentException("Connection string cannot be null or empty", nameof(connectionString));
        }

        // Parse connection string
        var config = ParseConnectionString(connectionString);
        _bucketName = config["BucketName"];
        _prefix = config.ContainsKey("Prefix") ? config["Prefix"] : "audit-archives/";

        _logger.LogInformation(
            "Initialized S3 storage provider for bucket '{BucketName}' with prefix '{Prefix}'",
            _bucketName,
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

        var key = GenerateS3Key(archiveId);

        try
        {
            _logger.LogInformation(
                "Uploading archive {ArchiveId} to S3 bucket '{BucketName}' with key '{Key}' ({SizeMB:N2} MB)",
                archiveId,
                _bucketName,
                key,
                data.Length / (1024.0 * 1024.0));

            // Calculate checksum for integrity verification
            var checksum = CalculateSha256Checksum(data);
            metadata["Checksum"] = checksum;
            metadata["UploadDate"] = DateTime.UtcNow.ToString("O");
            metadata["ArchiveId"] = archiveId.ToString();

            var request = new PutObjectRequest
            {
                BucketName = _bucketName,
                Key = key,
                InputStream = new MemoryStream(data),
                ContentType = "application/octet-stream",
                ServerSideEncryptionMethod = ServerSideEncryptionMethod.AES256,
                StorageClass = S3StorageClass.StandardInfrequentAccess // Use IA for cost savings
            };

            // Add metadata to S3 object
            foreach (var kvp in metadata)
            {
                request.Metadata.Add($"x-amz-meta-{kvp.Key.ToLowerInvariant()}", kvp.Value);
            }

            var response = await _s3Client.PutObjectAsync(request, cancellationToken);

            if (response.HttpStatusCode != System.Net.HttpStatusCode.OK)
            {
                throw new Exception($"S3 upload failed with status code: {response.HttpStatusCode}");
            }

            var storageLocation = $"s3://{_bucketName}/{key}";

            _logger.LogInformation(
                "Successfully uploaded archive {ArchiveId} to S3: {StorageLocation}",
                archiveId,
                storageLocation);

            return storageLocation;
        }
        catch (AmazonS3Exception ex)
        {
            _logger.LogError(
                ex,
                "S3 error uploading archive {ArchiveId}: {ErrorCode} - {ErrorMessage}",
                archiveId,
                ex.ErrorCode,
                ex.Message);
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error uploading archive {ArchiveId} to S3", archiveId);
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

        var key = ExtractS3KeyFromLocation(storageLocation);

        try
        {
            _logger.LogInformation(
                "Downloading archive from S3 bucket '{BucketName}' with key '{Key}'",
                _bucketName,
                key);

            var request = new GetObjectRequest
            {
                BucketName = _bucketName,
                Key = key
            };

            using var response = await _s3Client.GetObjectAsync(request, cancellationToken);
            using var memoryStream = new MemoryStream();
            await response.ResponseStream.CopyToAsync(memoryStream, cancellationToken);

            var data = memoryStream.ToArray();

            _logger.LogInformation(
                "Successfully downloaded archive from S3: {StorageLocation} ({SizeMB:N2} MB)",
                storageLocation,
                data.Length / (1024.0 * 1024.0));

            return data;
        }
        catch (AmazonS3Exception ex)
        {
            _logger.LogError(
                ex,
                "S3 error downloading archive from {StorageLocation}: {ErrorCode} - {ErrorMessage}",
                storageLocation,
                ex.ErrorCode,
                ex.Message);
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error downloading archive from S3: {StorageLocation}", storageLocation);
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

        var key = ExtractS3KeyFromLocation(storageLocation);

        try
        {
            _logger.LogInformation(
                "Deleting archive from S3 bucket '{BucketName}' with key '{Key}'",
                _bucketName,
                key);

            var request = new DeleteObjectRequest
            {
                BucketName = _bucketName,
                Key = key
            };

            var response = await _s3Client.DeleteObjectAsync(request, cancellationToken);

            var success = response.HttpStatusCode == System.Net.HttpStatusCode.NoContent ||
                          response.HttpStatusCode == System.Net.HttpStatusCode.OK;

            if (success)
            {
                _logger.LogInformation("Successfully deleted archive from S3: {StorageLocation}", storageLocation);
            }
            else
            {
                _logger.LogWarning(
                    "S3 delete returned unexpected status code: {StatusCode} for {StorageLocation}",
                    response.HttpStatusCode,
                    storageLocation);
            }

            return success;
        }
        catch (AmazonS3Exception ex)
        {
            _logger.LogError(
                ex,
                "S3 error deleting archive from {StorageLocation}: {ErrorCode} - {ErrorMessage}",
                storageLocation,
                ex.ErrorCode,
                ex.Message);
            return false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting archive from S3: {StorageLocation}", storageLocation);
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

        var key = ExtractS3KeyFromLocation(storageLocation);

        try
        {
            var request = new GetObjectMetadataRequest
            {
                BucketName = _bucketName,
                Key = key
            };

            await _s3Client.GetObjectMetadataAsync(request, cancellationToken);
            return true;
        }
        catch (AmazonS3Exception ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            return false;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error checking if archive exists in S3: {StorageLocation}", storageLocation);
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

        var key = ExtractS3KeyFromLocation(storageLocation);

        try
        {
            var request = new GetObjectMetadataRequest
            {
                BucketName = _bucketName,
                Key = key
            };

            var response = await _s3Client.GetObjectMetadataAsync(request, cancellationToken);

            var metadata = new Dictionary<string, string>();

            // Extract custom metadata
            foreach (var key2 in response.Metadata.Keys)
            {
                var metadataKey = key2.Replace("x-amz-meta-", "", StringComparison.OrdinalIgnoreCase);
                metadata[metadataKey] = response.Metadata[key2];
            }

            // Add standard metadata
            metadata["ContentLength"] = response.ContentLength.ToString();
            metadata["LastModified"] = response.LastModified.ToString("O");
            metadata["ETag"] = response.ETag;

            return metadata;
        }
        catch (AmazonS3Exception ex)
        {
            _logger.LogError(
                ex,
                "S3 error getting metadata for {StorageLocation}: {ErrorCode} - {ErrorMessage}",
                storageLocation,
                ex.ErrorCode,
                ex.Message);
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting metadata from S3: {StorageLocation}", storageLocation);
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

            if (!metadata.TryGetValue("checksum", out var storedChecksum))
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
            // Try to list objects in the bucket to verify access
            var request = new ListObjectsV2Request
            {
                BucketName = _bucketName,
                MaxKeys = 1,
                Prefix = _prefix
            };

            await _s3Client.ListObjectsV2Async(request, cancellationToken);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "S3 health check failed for bucket '{BucketName}'", _bucketName);
            return false;
        }
    }

    /// <summary>
    /// Generate S3 key for an archive based on archive ID and date
    /// Format: {prefix}year=YYYY/month=MM/archive-{archiveId}.bin
    /// </summary>
    private string GenerateS3Key(long archiveId)
    {
        var now = DateTime.UtcNow;
        return $"{_prefix}year={now:yyyy}/month={now:MM}/archive-{archiveId}.bin";
    }

    /// <summary>
    /// Extract S3 key from storage location URL
    /// </summary>
    private string ExtractS3KeyFromLocation(string storageLocation)
    {
        if (storageLocation.StartsWith("s3://", StringComparison.OrdinalIgnoreCase))
        {
            var uri = new Uri(storageLocation);
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

        if (!config.ContainsKey("BucketName"))
        {
            throw new ArgumentException("Connection string must contain 'BucketName'", nameof(connectionString));
        }

        return config;
    }
}
