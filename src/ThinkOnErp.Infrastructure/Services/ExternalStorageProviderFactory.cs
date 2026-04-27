using Amazon;
using Amazon.S3;
using Azure.Storage.Blobs;
using Microsoft.Extensions.Logging;
using ThinkOnErp.Domain.Interfaces;
using ThinkOnErp.Infrastructure.Configuration;

namespace ThinkOnErp.Infrastructure.Services;

/// <summary>
/// Factory for creating external storage provider instances based on configuration.
/// Supports AWS S3, Azure Blob Storage, and file system storage providers.
/// </summary>
public interface IExternalStorageProviderFactory
{
    /// <summary>
    /// Create an external storage provider based on the configured storage provider type.
    /// </summary>
    /// <param name="providerType">Storage provider type (S3, AzureBlob, FileSystem)</param>
    /// <param name="connectionString">Connection string or configuration for the provider</param>
    /// <returns>Configured external storage provider instance</returns>
    IExternalStorageProvider CreateProvider(string providerType, string connectionString);
}

/// <summary>
/// Implementation of external storage provider factory
/// </summary>
public class ExternalStorageProviderFactory : IExternalStorageProviderFactory
{
    private readonly ILoggerFactory _loggerFactory;

    public ExternalStorageProviderFactory(ILoggerFactory loggerFactory)
    {
        _loggerFactory = loggerFactory ?? throw new ArgumentNullException(nameof(loggerFactory));
    }

    public IExternalStorageProvider CreateProvider(string providerType, string connectionString)
    {
        if (string.IsNullOrWhiteSpace(providerType))
        {
            throw new ArgumentException("Provider type cannot be null or empty", nameof(providerType));
        }

        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new ArgumentException("Connection string cannot be null or empty", nameof(connectionString));
        }

        return providerType.ToUpperInvariant() switch
        {
            "S3" => CreateS3Provider(connectionString),
            "AZUREBLOB" => CreateAzureBlobProvider(connectionString),
            "FILESYSTEM" => throw new NotImplementedException("FileSystem provider not yet implemented"),
            _ => throw new ArgumentException($"Unsupported storage provider type: {providerType}", nameof(providerType))
        };
    }

    private IExternalStorageProvider CreateS3Provider(string connectionString)
    {
        var config = ParseConnectionString(connectionString);

        // Parse AWS configuration
        var bucketName = config["BucketName"];
        var regionString = config.ContainsKey("Region") ? config["Region"] : "us-east-1";
        var region = RegionEndpoint.GetBySystemName(regionString);

        // Create S3 client
        IAmazonS3 s3Client;

        if (config.ContainsKey("AccessKey") && config.ContainsKey("SecretKey"))
        {
            // Use explicit credentials
            var accessKey = config["AccessKey"];
            var secretKey = config["SecretKey"];
            s3Client = new AmazonS3Client(accessKey, secretKey, region);
        }
        else
        {
            // Use default credentials (IAM role, environment variables, etc.)
            s3Client = new AmazonS3Client(region);
        }

        var logger = _loggerFactory.CreateLogger<S3StorageProvider>();
        return new S3StorageProvider(s3Client, logger, connectionString);
    }

    private IExternalStorageProvider CreateAzureBlobProvider(string connectionString)
    {
        var config = ParseConnectionString(connectionString);

        // Create Blob Service Client
        BlobServiceClient blobServiceClient;

        if (config.ContainsKey("ConnectionString"))
        {
            // Use full Azure Storage connection string
            blobServiceClient = new BlobServiceClient(config["ConnectionString"]);
        }
        else if (config.ContainsKey("AccountName") && config.ContainsKey("AccountKey"))
        {
            // Build connection string from account name and key
            var accountName = config["AccountName"];
            var accountKey = config["AccountKey"];
            var azureConnectionString = $"DefaultEndpointsProtocol=https;AccountName={accountName};AccountKey={accountKey};EndpointSuffix=core.windows.net";
            blobServiceClient = new BlobServiceClient(azureConnectionString);
        }
        else
        {
            throw new ArgumentException("Azure Blob connection string must contain either 'ConnectionString' or 'AccountName' and 'AccountKey'");
        }

        var logger = _loggerFactory.CreateLogger<AzureBlobStorageProvider>();
        return new AzureBlobStorageProvider(blobServiceClient, logger, connectionString);
    }

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

        return config;
    }
}
