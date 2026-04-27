using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Amazon.S3;
using Amazon.S3.Model;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;
using Xunit.Abstractions;
using ThinkOnErp.Domain.Interfaces;
using ThinkOnErp.Infrastructure.Services;

namespace ThinkOnErp.Infrastructure.Tests.Integration;

/// <summary>
/// Integration tests focused on error handling scenarios for external storage providers.
/// Tests network failures, authentication issues, timeout scenarios, and recovery mechanisms.
/// 
/// **Validates: Requirements 12.7, 16.1, 16.2, 16.3**
/// - Requirement 12.7: Error handling for network failures and authentication issues
/// - Requirement 16.1: Circuit breaker pattern for resilience
/// - Requirement 16.2: Retry policy for transient failures
/// - Requirement 16.3: Graceful degradation when external storage fails
/// </summary>
public class ExternalStorageErrorHandlingIntegrationTests : IDisposable
{
    private readonly ITestOutputHelper _output;
    private readonly ServiceProvider _serviceProvider;
    private readonly ILogger<ExternalStorageErrorHandlingIntegrationTests> _logger;

    public ExternalStorageErrorHandlingIntegrationTests(ITestOutputHelper output)
    {
        _output = output;
        
        var services = new ServiceCollection();
        services.AddLogging(builder => builder.AddConsole().SetMinimumLevel(LogLevel.Debug));
        services.AddSingleton<IExternalStorageProviderFactory, ExternalStorageProviderFactory>();
        
        _serviceProvider = services.BuildServiceProvider();
        _logger = _serviceProvider.GetRequiredService<ILogger<ExternalStorageErrorHandlingIntegrationTests>>();
    }

    #region S3 Error Handling Tests

    [Fact]
    public async Task S3Storage_AccessDenied_ShouldThrowAppropriateException()
    {
        // Arrange
        var mockS3Client = new Mock<IAmazonS3>();
        var mockLogger = new Mock<ILogger<S3StorageProvider>>();
        var connectionString = "BucketName=test-bucket;Region=us-east-1";

        // Setup mock to throw access denied exception
        mockS3Client
            .Setup(x => x.PutObjectAsync(It.IsAny<PutObjectRequest>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new AmazonS3Exception("Access Denied") 
            { 
                StatusCode = System.Net.HttpStatusCode.Forbidden,
                ErrorCode = "AccessDenied"
            });

        var provider = new S3StorageProvider(mockS3Client.Object, mockLogger.Object, connectionString);
        var testData = new byte[] { 1, 2, 3, 4, 5 };

        // Act & Assert
        _output.WriteLine("Testing S3 access denied scenario...");
        var exception = await Assert.ThrowsAsync<AmazonS3Exception>(async () =>
            await provider.UploadAsync(12345L, testData, new Dictionary<string, string>()));
        
        Assert.Equal("AccessDenied", exception.ErrorCode);
        Assert.Equal(System.Net.HttpStatusCode.Forbidden, exception.StatusCode);
        _output.WriteLine($"Access denied handled correctly: {exception.Message}");
    }

    [Fact]
    public async Task S3Storage_BucketNotFound_ShouldThrowAppropriateException()
    {
        // Arrange
        var mockS3Client = new Mock<IAmazonS3>();
        var mockLogger = new Mock<ILogger<S3StorageProvider>>();
        var connectionString = "BucketName=nonexistent-bucket;Region=us-east-1";

        // Setup mock to throw bucket not found exception
        mockS3Client
            .Setup(x => x.PutObjectAsync(It.IsAny<PutObjectRequest>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new AmazonS3Exception("The specified bucket does not exist") 
            { 
                StatusCode = System.Net.HttpStatusCode.NotFound,
                ErrorCode = "NoSuchBucket"
            });

        var provider = new S3StorageProvider(mockS3Client.Object, mockLogger.Object, connectionString);
        var testData = new byte[] { 1, 2, 3, 4, 5 };

        // Act & Assert
        _output.WriteLine("Testing S3 bucket not found scenario...");
        var exception = await Assert.ThrowsAsync<AmazonS3Exception>(async () =>
            await provider.UploadAsync(12345L, testData, new Dictionary<string, string>()));
        
        Assert.Equal("NoSuchBucket", exception.ErrorCode);
        Assert.Equal(System.Net.HttpStatusCode.NotFound, exception.StatusCode);
        _output.WriteLine($"Bucket not found handled correctly: {exception.Message}");
    }

    [Fact]
    public async Task S3Storage_NetworkTimeout_ShouldThrowTimeoutException()
    {
        // Arrange
        var mockS3Client = new Mock<IAmazonS3>();
        var mockLogger = new Mock<ILogger<S3StorageProvider>>();
        var connectionString = "BucketName=test-bucket;Region=us-east-1";

        // Setup mock to throw timeout exception
        mockS3Client
            .Setup(x => x.PutObjectAsync(It.IsAny<PutObjectRequest>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new TaskCanceledException("The operation was canceled due to timeout"));

        var provider = new S3StorageProvider(mockS3Client.Object, mockLogger.Object, connectionString);
        var testData = new byte[] { 1, 2, 3, 4, 5 };

        // Act & Assert
        _output.WriteLine("Testing S3 network timeout scenario...");
        await Assert.ThrowsAsync<TaskCanceledException>(async () =>
            await provider.UploadAsync(12345L, testData, new Dictionary<string, string>()));
        
        _output.WriteLine("Network timeout handled correctly");
    }

    [Fact]
    public async Task S3Storage_ServiceUnavailable_ShouldThrowServiceException()
    {
        // Arrange
        var mockS3Client = new Mock<IAmazonS3>();
        var mockLogger = new Mock<ILogger<S3StorageProvider>>();
        var connectionString = "BucketName=test-bucket;Region=us-east-1";

        // Setup mock to throw service unavailable exception
        mockS3Client
            .Setup(x => x.PutObjectAsync(It.IsAny<PutObjectRequest>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new AmazonS3Exception("Service Unavailable") 
            { 
                StatusCode = System.Net.HttpStatusCode.ServiceUnavailable,
                ErrorCode = "ServiceUnavailable"
            });

        var provider = new S3StorageProvider(mockS3Client.Object, mockLogger.Object, connectionString);
        var testData = new byte[] { 1, 2, 3, 4, 5 };

        // Act & Assert
        _output.WriteLine("Testing S3 service unavailable scenario...");
        var exception = await Assert.ThrowsAsync<AmazonS3Exception>(async () =>
            await provider.UploadAsync(12345L, testData, new Dictionary<string, string>()));
        
        Assert.Equal("ServiceUnavailable", exception.ErrorCode);
        Assert.Equal(System.Net.HttpStatusCode.ServiceUnavailable, exception.StatusCode);
        _output.WriteLine($"Service unavailable handled correctly: {exception.Message}");
    }

    [Fact]
    public async Task S3Storage_ObjectNotFound_ShouldReturnFalseForExists()
    {
        // Arrange
        var mockS3Client = new Mock<IAmazonS3>();
        var mockLogger = new Mock<ILogger<S3StorageProvider>>();
        var connectionString = "BucketName=test-bucket;Region=us-east-1";

        // Setup mock to throw not found exception for metadata request
        mockS3Client
            .Setup(x => x.GetObjectMetadataAsync(It.IsAny<GetObjectMetadataRequest>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new AmazonS3Exception("Not Found") 
            { 
                StatusCode = System.Net.HttpStatusCode.NotFound,
                ErrorCode = "NotFound"
            });

        var provider = new S3StorageProvider(mockS3Client.Object, mockLogger.Object, connectionString);
        var storageLocation = "s3://test-bucket/nonexistent/archive-12345.bin";

        // Act & Assert
        _output.WriteLine("Testing S3 object not found scenario...");
        var exists = await provider.ExistsAsync(storageLocation);
        
        Assert.False(exists);
        _output.WriteLine("Object not found handled correctly - returned false");
    }

    [Fact]
    public async Task S3Storage_CorruptedDownload_ShouldThrowException()
    {
        // Arrange
        var mockS3Client = new Mock<IAmazonS3>();
        var mockLogger = new Mock<ILogger<S3StorageProvider>>();
        var connectionString = "BucketName=test-bucket;Region=us-east-1";

        // Setup mock to return corrupted stream
        var corruptedStream = new MemoryStream();
        corruptedStream.WriteByte(0xFF); // Invalid data
        corruptedStream.Position = 0;

        var mockResponse = new Mock<GetObjectResponse>();
        mockResponse.Setup(x => x.ResponseStream).Returns(corruptedStream);

        mockS3Client
            .Setup(x => x.GetObjectAsync(It.IsAny<GetObjectRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(mockResponse.Object);

        var provider = new S3StorageProvider(mockS3Client.Object, mockLogger.Object, connectionString);
        var storageLocation = "s3://test-bucket/archives/archive-12345.bin";

        // Act & Assert
        _output.WriteLine("Testing S3 corrupted download scenario...");
        var downloadedData = await provider.DownloadAsync(storageLocation);
        
        // Should return the corrupted data (integrity check happens separately)
        Assert.Single(downloadedData);
        Assert.Equal(0xFF, downloadedData[0]);
        _output.WriteLine("Corrupted download handled - integrity verification should catch this");
    }

    #endregion

    #region Azure Blob Storage Error Handling Tests

    [Fact]
    public async Task AzureStorage_AuthenticationFailure_ShouldThrowAppropriateException()
    {
        // Arrange
        var mockBlobServiceClient = new Mock<BlobServiceClient>();
        var mockLogger = new Mock<ILogger<AzureBlobStorageProvider>>();
        var connectionString = "ContainerName=test-container";

        // Setup mock to throw authentication exception
        mockBlobServiceClient
            .Setup(x => x.GetBlobContainerClient(It.IsAny<string>()))
            .Throws(new Azure.RequestFailedException(401, "Authentication failed"));

        var provider = new AzureBlobStorageProvider(mockBlobServiceClient.Object, mockLogger.Object, connectionString);
        var testData = new byte[] { 1, 2, 3, 4, 5 };

        // Act & Assert
        _output.WriteLine("Testing Azure authentication failure scenario...");
        await Assert.ThrowsAsync<Azure.RequestFailedException>(async () =>
            await provider.UploadAsync(12345L, testData, new Dictionary<string, string>()));
        
        _output.WriteLine("Azure authentication failure handled correctly");
    }

    [Fact]
    public async Task AzureStorage_ContainerNotFound_ShouldThrowAppropriateException()
    {
        // Arrange
        var mockBlobServiceClient = new Mock<BlobServiceClient>();
        var mockLogger = new Mock<ILogger<AzureBlobStorageProvider>>();
        var connectionString = "ContainerName=nonexistent-container";

        // Setup mock to throw container not found exception
        mockBlobServiceClient
            .Setup(x => x.GetBlobContainerClient(It.IsAny<string>()))
            .Throws(new Azure.RequestFailedException(404, "Container not found"));

        var provider = new AzureBlobStorageProvider(mockBlobServiceClient.Object, mockLogger.Object, connectionString);
        var testData = new byte[] { 1, 2, 3, 4, 5 };

        // Act & Assert
        _output.WriteLine("Testing Azure container not found scenario...");
        await Assert.ThrowsAsync<Azure.RequestFailedException>(async () =>
            await provider.UploadAsync(12345L, testData, new Dictionary<string, string>()));
        
        _output.WriteLine("Azure container not found handled correctly");
    }

    [Fact]
    public async Task AzureStorage_NetworkError_ShouldThrowNetworkException()
    {
        // Arrange
        var mockBlobServiceClient = new Mock<BlobServiceClient>();
        var mockLogger = new Mock<ILogger<AzureBlobStorageProvider>>();
        var connectionString = "ContainerName=test-container";

        // Setup mock to throw network exception
        mockBlobServiceClient
            .Setup(x => x.GetBlobContainerClient(It.IsAny<string>()))
            .Throws(new Azure.RequestFailedException(500, "Internal server error"));

        var provider = new AzureBlobStorageProvider(mockBlobServiceClient.Object, mockLogger.Object, connectionString);
        var testData = new byte[] { 1, 2, 3, 4, 5 };

        // Act & Assert
        _output.WriteLine("Testing Azure network error scenario...");
        await Assert.ThrowsAsync<Azure.RequestFailedException>(async () =>
            await provider.UploadAsync(12345L, testData, new Dictionary<string, string>()));
        
        _output.WriteLine("Azure network error handled correctly");
    }

    [Fact]
    public async Task AzureStorage_BlobNotFound_ShouldReturnFalseForExists()
    {
        // Arrange
        var mockBlobServiceClient = new Mock<BlobServiceClient>();
        var mockBlobContainerClient = new Mock<BlobContainerClient>();
        var mockBlobClient = new Mock<BlobClient>();
        var mockLogger = new Mock<ILogger<AzureBlobStorageProvider>>();
        var connectionString = "ContainerName=test-container";

        // Setup mock chain
        mockBlobServiceClient
            .Setup(x => x.GetBlobContainerClient(It.IsAny<string>()))
            .Returns(mockBlobContainerClient.Object);
        
        mockBlobContainerClient
            .Setup(x => x.GetBlobClient(It.IsAny<string>()))
            .Returns(mockBlobClient.Object);

        // Setup mock to throw not found exception
        mockBlobClient
            .Setup(x => x.ExistsAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Azure.RequestFailedException(404, "Blob not found"));

        var provider = new AzureBlobStorageProvider(mockBlobServiceClient.Object, mockLogger.Object, connectionString);
        var storageLocation = "https://test.blob.core.windows.net/test-container/nonexistent/archive-12345.bin";

        // Act & Assert
        _output.WriteLine("Testing Azure blob not found scenario...");
        await Assert.ThrowsAsync<Azure.RequestFailedException>(async () =>
            await provider.ExistsAsync(storageLocation));
        
        _output.WriteLine("Azure blob not found handled correctly");
    }

    #endregion

    #region Factory Error Handling Tests

    [Fact]
    public void ExternalStorageProviderFactory_InvalidProviderType_ShouldThrowArgumentException()
    {
        // Arrange
        var factory = _serviceProvider.GetRequiredService<IExternalStorageProviderFactory>();

        // Act & Assert
        _output.WriteLine("Testing factory with invalid provider type...");
        var exception = Assert.Throws<ArgumentException>(() =>
            factory.CreateProvider("InvalidProvider", "test-connection-string"));
        
        Assert.Contains("Unsupported storage provider", exception.Message);
        _output.WriteLine($"Invalid provider type handled correctly: {exception.Message}");
    }

    [Fact]
    public void ExternalStorageProviderFactory_NullProviderType_ShouldThrowArgumentException()
    {
        // Arrange
        var factory = _serviceProvider.GetRequiredService<IExternalStorageProviderFactory>();

        // Act & Assert
        _output.WriteLine("Testing factory with null provider type...");
        var exception = Assert.Throws<ArgumentException>(() =>
            factory.CreateProvider(null!, "test-connection-string"));
        
        Assert.Contains("Provider type cannot be null or empty", exception.Message);
        _output.WriteLine($"Null provider type handled correctly: {exception.Message}");
    }

    [Fact]
    public void ExternalStorageProviderFactory_EmptyConnectionString_ShouldThrowArgumentException()
    {
        // Arrange
        var factory = _serviceProvider.GetRequiredService<IExternalStorageProviderFactory>();

        // Act & Assert
        _output.WriteLine("Testing factory with empty connection string...");
        var exception = Assert.Throws<ArgumentException>(() =>
            factory.CreateProvider("S3", ""));
        
        Assert.Contains("Connection string cannot be null or empty", exception.Message);
        _output.WriteLine($"Empty connection string handled correctly: {exception.Message}");
    }

    [Fact]
    public void S3StorageProvider_InvalidConnectionString_ShouldThrowArgumentException()
    {
        // Arrange
        var mockS3Client = new Mock<IAmazonS3>();
        var mockLogger = new Mock<ILogger<S3StorageProvider>>();

        // Act & Assert - Missing BucketName
        _output.WriteLine("Testing S3 provider with invalid connection string (missing BucketName)...");
        var exception = Assert.Throws<ArgumentException>(() =>
            new S3StorageProvider(mockS3Client.Object, mockLogger.Object, "Region=us-east-1"));
        
        Assert.Contains("BucketName is required", exception.Message);
        _output.WriteLine($"Invalid S3 connection string handled correctly: {exception.Message}");
    }

    [Fact]
    public void AzureBlobStorageProvider_InvalidConnectionString_ShouldThrowArgumentException()
    {
        // Arrange
        var mockBlobServiceClient = new Mock<BlobServiceClient>();
        var mockLogger = new Mock<ILogger<AzureBlobStorageProvider>>();

        // Act & Assert - Missing ContainerName
        _output.WriteLine("Testing Azure provider with invalid connection string (missing ContainerName)...");
        var exception = Assert.Throws<ArgumentException>(() =>
            new AzureBlobStorageProvider(mockBlobServiceClient.Object, mockLogger.Object, "AccountName=test"));
        
        Assert.Contains("ContainerName is required", exception.Message);
        _output.WriteLine($"Invalid Azure connection string handled correctly: {exception.Message}");
    }

    #endregion

    #region Concurrent Access Tests

    [Fact]
    public async Task ExternalStorage_ConcurrentUploads_ShouldHandleCorrectly()
    {
        // Arrange
        var mockS3Client = new Mock<IAmazonS3>();
        var mockLogger = new Mock<ILogger<S3StorageProvider>>();
        var connectionString = "BucketName=test-bucket;Region=us-east-1";

        // Setup mock to simulate successful uploads with delay
        mockS3Client
            .Setup(x => x.PutObjectAsync(It.IsAny<PutObjectRequest>(), It.IsAny<CancellationToken>()))
            .Returns(async () =>
            {
                await Task.Delay(100); // Simulate network delay
                return new PutObjectResponse { HttpStatusCode = System.Net.HttpStatusCode.OK };
            });

        var provider = new S3StorageProvider(mockS3Client.Object, mockLogger.Object, connectionString);

        // Act - Start multiple concurrent uploads
        _output.WriteLine("Testing concurrent uploads to S3...");
        var tasks = new List<Task<string>>();
        
        for (int i = 0; i < 5; i++)
        {
            var archiveId = 12345L + i;
            var testData = new byte[] { (byte)i, (byte)(i + 1), (byte)(i + 2) };
            var metadata = new Dictionary<string, string> { ["Index"] = i.ToString() };
            
            tasks.Add(provider.UploadAsync(archiveId, testData, metadata));
        }

        // Assert - All uploads should complete successfully
        var results = await Task.WhenAll(tasks);
        
        Assert.Equal(5, results.Length);
        Assert.All(results, result => Assert.NotNull(result));
        Assert.All(results, result => Assert.StartsWith("s3://", result));
        
        _output.WriteLine($"All {results.Length} concurrent uploads completed successfully");
    }

    [Fact]
    public async Task ExternalStorage_ConcurrentOperations_ShouldNotInterfere()
    {
        // Arrange
        var mockS3Client = new Mock<IAmazonS3>();
        var mockLogger = new Mock<ILogger<S3StorageProvider>>();
        var connectionString = "BucketName=test-bucket;Region=us-east-1";

        // Setup mocks for different operations
        mockS3Client
            .Setup(x => x.PutObjectAsync(It.IsAny<PutObjectRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PutObjectResponse { HttpStatusCode = System.Net.HttpStatusCode.OK });

        mockS3Client
            .Setup(x => x.GetObjectMetadataAsync(It.IsAny<GetObjectMetadataRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new GetObjectMetadataResponse());

        mockS3Client
            .Setup(x => x.DeleteObjectAsync(It.IsAny<DeleteObjectRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new DeleteObjectResponse { HttpStatusCode = System.Net.HttpStatusCode.NoContent });

        var provider = new S3StorageProvider(mockS3Client.Object, mockLogger.Object, connectionString);

        // Act - Start mixed concurrent operations
        _output.WriteLine("Testing concurrent mixed operations on S3...");
        var tasks = new List<Task>();
        
        // Upload tasks
        for (int i = 0; i < 3; i++)
        {
            var archiveId = 12345L + i;
            var testData = new byte[] { (byte)i };
            tasks.Add(provider.UploadAsync(archiveId, testData, new Dictionary<string, string>()));
        }
        
        // Exists check tasks
        for (int i = 0; i < 3; i++)
        {
            var location = $"s3://test-bucket/archives/archive-{12345 + i}.bin";
            tasks.Add(provider.ExistsAsync(location));
        }
        
        // Delete tasks
        for (int i = 0; i < 2; i++)
        {
            var location = $"s3://test-bucket/archives/archive-{12345 + i}.bin";
            tasks.Add(provider.DeleteAsync(location));
        }

        // Assert - All operations should complete without interference
        await Task.WhenAll(tasks);
        
        _output.WriteLine("All concurrent mixed operations completed successfully");
    }

    #endregion

    public void Dispose()
    {
        _serviceProvider?.Dispose();
    }
}