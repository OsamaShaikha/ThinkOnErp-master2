using Amazon.S3;
using Amazon.S3.Model;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Microsoft.Extensions.Logging;
using Moq;
using ThinkOnErp.Infrastructure.Services;
using Xunit;

namespace ThinkOnErp.Infrastructure.Tests.Services;

/// <summary>
/// Unit tests for external storage providers (S3 and Azure Blob Storage).
/// Tests upload, download, delete, exists, metadata, and integrity verification operations.
/// </summary>
public class ExternalStorageProviderTests
{
    #region S3StorageProvider Tests

    [Fact]
    public async Task S3StorageProvider_UploadAsync_ShouldUploadDataSuccessfully()
    {
        // Arrange
        var mockS3Client = new Mock<IAmazonS3>();
        var mockLogger = new Mock<ILogger<S3StorageProvider>>();
        var connectionString = "BucketName=test-bucket;Region=us-east-1;Prefix=archives/";

        mockS3Client
            .Setup(x => x.PutObjectAsync(It.IsAny<PutObjectRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PutObjectResponse { HttpStatusCode = System.Net.HttpStatusCode.OK });

        var provider = new S3StorageProvider(mockS3Client.Object, mockLogger.Object, connectionString);

        var archiveId = 12345L;
        var data = new byte[] { 1, 2, 3, 4, 5 };
        var metadata = new Dictionary<string, string>
        {
            ["EventType"] = "Authentication",
            ["RecordCount"] = "100"
        };

        // Act
        var storageLocation = await provider.UploadAsync(archiveId, data, metadata);

        // Assert
        Assert.NotNull(storageLocation);
        Assert.StartsWith("s3://test-bucket/archives/", storageLocation);
        mockS3Client.Verify(
            x => x.PutObjectAsync(It.IsAny<PutObjectRequest>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task S3StorageProvider_DownloadAsync_ShouldDownloadDataSuccessfully()
    {
        // Arrange
        var mockS3Client = new Mock<IAmazonS3>();
        var mockLogger = new Mock<ILogger<S3StorageProvider>>();
        var connectionString = "BucketName=test-bucket;Region=us-east-1";

        var expectedData = new byte[] { 1, 2, 3, 4, 5 };
        var responseStream = new MemoryStream(expectedData);

        var mockResponse = new Mock<GetObjectResponse>();
        mockResponse.Setup(x => x.ResponseStream).Returns(responseStream);

        mockS3Client
            .Setup(x => x.GetObjectAsync(It.IsAny<GetObjectRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(mockResponse.Object);

        var provider = new S3StorageProvider(mockS3Client.Object, mockLogger.Object, connectionString);

        var storageLocation = "s3://test-bucket/archives/year=2024/month=01/archive-12345.bin";

        // Act
        var downloadedData = await provider.DownloadAsync(storageLocation);

        // Assert
        Assert.Equal(expectedData, downloadedData);
        mockS3Client.Verify(
            x => x.GetObjectAsync(It.IsAny<GetObjectRequest>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task S3StorageProvider_DeleteAsync_ShouldDeleteDataSuccessfully()
    {
        // Arrange
        var mockS3Client = new Mock<IAmazonS3>();
        var mockLogger = new Mock<ILogger<S3StorageProvider>>();
        var connectionString = "BucketName=test-bucket;Region=us-east-1";

        mockS3Client
            .Setup(x => x.DeleteObjectAsync(It.IsAny<DeleteObjectRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new DeleteObjectResponse { HttpStatusCode = System.Net.HttpStatusCode.NoContent });

        var provider = new S3StorageProvider(mockS3Client.Object, mockLogger.Object, connectionString);

        var storageLocation = "s3://test-bucket/archives/year=2024/month=01/archive-12345.bin";

        // Act
        var result = await provider.DeleteAsync(storageLocation);

        // Assert
        Assert.True(result);
        mockS3Client.Verify(
            x => x.DeleteObjectAsync(It.IsAny<DeleteObjectRequest>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task S3StorageProvider_ExistsAsync_ShouldReturnTrueWhenObjectExists()
    {
        // Arrange
        var mockS3Client = new Mock<IAmazonS3>();
        var mockLogger = new Mock<ILogger<S3StorageProvider>>();
        var connectionString = "BucketName=test-bucket;Region=us-east-1";

        mockS3Client
            .Setup(x => x.GetObjectMetadataAsync(It.IsAny<GetObjectMetadataRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new GetObjectMetadataResponse());

        var provider = new S3StorageProvider(mockS3Client.Object, mockLogger.Object, connectionString);

        var storageLocation = "s3://test-bucket/archives/year=2024/month=01/archive-12345.bin";

        // Act
        var exists = await provider.ExistsAsync(storageLocation);

        // Assert
        Assert.True(exists);
    }

    [Fact]
    public async Task S3StorageProvider_ExistsAsync_ShouldReturnFalseWhenObjectDoesNotExist()
    {
        // Arrange
        var mockS3Client = new Mock<IAmazonS3>();
        var mockLogger = new Mock<ILogger<S3StorageProvider>>();
        var connectionString = "BucketName=test-bucket;Region=us-east-1";

        mockS3Client
            .Setup(x => x.GetObjectMetadataAsync(It.IsAny<GetObjectMetadataRequest>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new AmazonS3Exception("Not Found") { StatusCode = System.Net.HttpStatusCode.NotFound });

        var provider = new S3StorageProvider(mockS3Client.Object, mockLogger.Object, connectionString);

        var storageLocation = "s3://test-bucket/archives/year=2024/month=01/archive-12345.bin";

        // Act
        var exists = await provider.ExistsAsync(storageLocation);

        // Assert
        Assert.False(exists);
    }

    [Fact]
    public async Task S3StorageProvider_GetMetadataAsync_ShouldReturnMetadata()
    {
        // Arrange
        var mockS3Client = new Mock<IAmazonS3>();
        var mockLogger = new Mock<ILogger<S3StorageProvider>>();
        var connectionString = "BucketName=test-bucket;Region=us-east-1";

        var mockResponse = new GetObjectMetadataResponse();
        mockResponse.Metadata.Add("x-amz-meta-checksum", "abc123");
        mockResponse.Metadata.Add("x-amz-meta-recordcount", "100");

        mockS3Client
            .Setup(x => x.GetObjectMetadataAsync(It.IsAny<GetObjectMetadataRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(mockResponse);

        var provider = new S3StorageProvider(mockS3Client.Object, mockLogger.Object, connectionString);

        var storageLocation = "s3://test-bucket/archives/year=2024/month=01/archive-12345.bin";

        // Act
        var metadata = await provider.GetMetadataAsync(storageLocation);

        // Assert
        Assert.NotNull(metadata);
        Assert.Contains("checksum", metadata.Keys);
        Assert.Contains("recordcount", metadata.Keys);
    }

    [Fact]
    public async Task S3StorageProvider_VerifyIntegrityAsync_ShouldReturnTrueWhenChecksumsMatch()
    {
        // Arrange
        var mockS3Client = new Mock<IAmazonS3>();
        var mockLogger = new Mock<ILogger<S3StorageProvider>>();
        var connectionString = "BucketName=test-bucket;Region=us-east-1";

        var expectedChecksum = "abc123def456";
        var mockResponse = new GetObjectMetadataResponse();
        mockResponse.Metadata.Add("x-amz-meta-checksum", expectedChecksum);

        mockS3Client
            .Setup(x => x.GetObjectMetadataAsync(It.IsAny<GetObjectMetadataRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(mockResponse);

        var provider = new S3StorageProvider(mockS3Client.Object, mockLogger.Object, connectionString);

        var storageLocation = "s3://test-bucket/archives/year=2024/month=01/archive-12345.bin";

        // Act
        var isValid = await provider.VerifyIntegrityAsync(storageLocation, expectedChecksum);

        // Assert
        Assert.True(isValid);
    }

    [Fact]
    public async Task S3StorageProvider_VerifyIntegrityAsync_ShouldReturnFalseWhenChecksumsMismatch()
    {
        // Arrange
        var mockS3Client = new Mock<IAmazonS3>();
        var mockLogger = new Mock<ILogger<S3StorageProvider>>();
        var connectionString = "BucketName=test-bucket;Region=us-east-1";

        var storedChecksum = "abc123def456";
        var expectedChecksum = "different123";
        var mockResponse = new GetObjectMetadataResponse();
        mockResponse.Metadata.Add("x-amz-meta-checksum", storedChecksum);

        mockS3Client
            .Setup(x => x.GetObjectMetadataAsync(It.IsAny<GetObjectMetadataRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(mockResponse);

        var provider = new S3StorageProvider(mockS3Client.Object, mockLogger.Object, connectionString);

        var storageLocation = "s3://test-bucket/archives/year=2024/month=01/archive-12345.bin";

        // Act
        var isValid = await provider.VerifyIntegrityAsync(storageLocation, expectedChecksum);

        // Assert
        Assert.False(isValid);
    }

    [Fact]
    public async Task S3StorageProvider_IsHealthyAsync_ShouldReturnTrueWhenBucketAccessible()
    {
        // Arrange
        var mockS3Client = new Mock<IAmazonS3>();
        var mockLogger = new Mock<ILogger<S3StorageProvider>>();
        var connectionString = "BucketName=test-bucket;Region=us-east-1";

        mockS3Client
            .Setup(x => x.ListObjectsV2Async(It.IsAny<ListObjectsV2Request>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ListObjectsV2Response());

        var provider = new S3StorageProvider(mockS3Client.Object, mockLogger.Object, connectionString);

        // Act
        var isHealthy = await provider.IsHealthyAsync();

        // Assert
        Assert.True(isHealthy);
    }

    [Fact]
    public void S3StorageProvider_Constructor_ShouldThrowWhenConnectionStringMissingBucketName()
    {
        // Arrange
        var mockS3Client = new Mock<IAmazonS3>();
        var mockLogger = new Mock<ILogger<S3StorageProvider>>();
        var connectionString = "Region=us-east-1"; // Missing BucketName

        // Act & Assert
        Assert.Throws<ArgumentException>(() =>
            new S3StorageProvider(mockS3Client.Object, mockLogger.Object, connectionString));
    }

    #endregion

    #region AzureBlobStorageProvider Tests

    [Fact]
    public void AzureBlobStorageProvider_Constructor_ShouldThrowWhenConnectionStringMissingContainerName()
    {
        // Arrange
        var mockBlobServiceClient = new Mock<BlobServiceClient>();
        var mockLogger = new Mock<ILogger<AzureBlobStorageProvider>>();
        var connectionString = "AccountName=test;AccountKey=key"; // Missing ContainerName

        // Act & Assert
        Assert.Throws<ArgumentException>(() =>
            new AzureBlobStorageProvider(mockBlobServiceClient.Object, mockLogger.Object, connectionString));
    }

    [Fact]
    public void AzureBlobStorageProvider_ProviderName_ShouldReturnAzureBlob()
    {
        // Arrange
        var mockBlobServiceClient = new Mock<BlobServiceClient>();
        var mockLogger = new Mock<ILogger<AzureBlobStorageProvider>>();
        var connectionString = "ContainerName=test-container";

        var provider = new AzureBlobStorageProvider(mockBlobServiceClient.Object, mockLogger.Object, connectionString);

        // Act
        var providerName = provider.ProviderName;

        // Assert
        Assert.Equal("AzureBlob", providerName);
    }

    #endregion

    #region ExternalStorageProviderFactory Tests

    [Fact]
    public void ExternalStorageProviderFactory_CreateProvider_ShouldCreateS3Provider()
    {
        // Arrange
        var mockLoggerFactory = new Mock<ILoggerFactory>();
        mockLoggerFactory
            .Setup(x => x.CreateLogger(It.IsAny<string>()))
            .Returns(new Mock<ILogger>().Object);

        var factory = new ExternalStorageProviderFactory(mockLoggerFactory.Object);

        var providerType = "S3";
        var connectionString = "BucketName=test-bucket;Region=us-east-1";

        // Act
        var provider = factory.CreateProvider(providerType, connectionString);

        // Assert
        Assert.NotNull(provider);
        Assert.Equal("S3", provider.ProviderName);
    }

    [Fact]
    public void ExternalStorageProviderFactory_CreateProvider_ShouldCreateAzureBlobProvider()
    {
        // Arrange
        var mockLoggerFactory = new Mock<ILoggerFactory>();
        mockLoggerFactory
            .Setup(x => x.CreateLogger(It.IsAny<string>()))
            .Returns(new Mock<ILogger>().Object);

        var factory = new ExternalStorageProviderFactory(mockLoggerFactory.Object);

        var providerType = "AzureBlob";
        var connectionString = "AccountName=test;AccountKey=testkey;ContainerName=test-container";

        // Act
        var provider = factory.CreateProvider(providerType, connectionString);

        // Assert
        Assert.NotNull(provider);
        Assert.Equal("AzureBlob", provider.ProviderName);
    }

    [Fact]
    public void ExternalStorageProviderFactory_CreateProvider_ShouldThrowForUnsupportedProvider()
    {
        // Arrange
        var mockLoggerFactory = new Mock<ILoggerFactory>();
        var factory = new ExternalStorageProviderFactory(mockLoggerFactory.Object);

        var providerType = "UnsupportedProvider";
        var connectionString = "test";

        // Act & Assert
        Assert.Throws<ArgumentException>(() =>
            factory.CreateProvider(providerType, connectionString));
    }

    [Fact]
    public void ExternalStorageProviderFactory_CreateProvider_ShouldThrowWhenProviderTypeIsNull()
    {
        // Arrange
        var mockLoggerFactory = new Mock<ILoggerFactory>();
        var factory = new ExternalStorageProviderFactory(mockLoggerFactory.Object);

        // Act & Assert
        Assert.Throws<ArgumentException>(() =>
            factory.CreateProvider(null!, "connectionString"));
    }

    [Fact]
    public void ExternalStorageProviderFactory_CreateProvider_ShouldThrowWhenConnectionStringIsNull()
    {
        // Arrange
        var mockLoggerFactory = new Mock<ILoggerFactory>();
        var factory = new ExternalStorageProviderFactory(mockLoggerFactory.Object);

        // Act & Assert
        Assert.Throws<ArgumentException>(() =>
            factory.CreateProvider("S3", null!));
    }

    #endregion
}
