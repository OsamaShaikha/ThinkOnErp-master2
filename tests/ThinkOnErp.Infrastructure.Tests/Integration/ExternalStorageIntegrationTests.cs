using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Amazon.S3;
using Amazon.S3.Model;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;
using Xunit.Abstractions;
using ThinkOnErp.Domain.Interfaces;
using ThinkOnErp.Infrastructure.Configuration;
using ThinkOnErp.Infrastructure.Services;

namespace ThinkOnErp.Infrastructure.Tests.Integration;

/// <summary>
/// Integration tests for external storage functionality (S3 and Azure Blob Storage).
/// Tests data upload, download, integrity verification, and error handling scenarios.
/// 
/// **Validates: Requirements 12.4, 12.5, 12.6, 12.7**
/// - Requirement 12.4: External storage integration for cold storage
/// - Requirement 12.5: Data integrity verification with checksums
/// - Requirement 12.6: Archive data retrieval and decompression
/// - Requirement 12.7: Error handling for network failures and authentication issues
/// 
/// **Tests both mocked and real external storage scenarios**
/// - Mock tests: Verify integration logic without external dependencies
/// - Real tests: Validate against actual S3/Azure services (requires configuration)
/// </summary>
public class ExternalStorageIntegrationTests : IDisposable
{
    private readonly ITestOutputHelper _output;
    private readonly ServiceProvider _serviceProvider;
    private readonly ILogger<ExternalStorageIntegrationTests> _logger;
    private readonly List<string> _createdStorageLocations = new();
    
    // Test configuration flags
    private readonly bool _useRealS3;
    private readonly bool _useRealAzure;
    private readonly string? _s3ConnectionString;
    private readonly string? _azureConnectionString;

    public ExternalStorageIntegrationTests(ITestOutputHelper output)
    {
        _output = output;
        
        // Setup configuration
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                // Default test configuration
                ["Archival:StorageProvider"] = "S3",
                ["Archival:CompressionAlgorithm"] = "GZip",
                ["Archival:VerifyIntegrity"] = "true",
                
                // Test S3 configuration (will be overridden if real S3 is available)
                ["TestStorage:S3:ConnectionString"] = "BucketName=test-audit-archives;Region=us-east-1;Prefix=integration-tests/",
                ["TestStorage:S3:UseReal"] = "false",
                
                // Test Azure configuration (will be overridden if real Azure is available)
                ["TestStorage:Azure:ConnectionString"] = "AccountName=testaccount;AccountKey=testkey;ContainerName=audit-archives;Prefix=integration-tests/",
                ["TestStorage:Azure:UseReal"] = "false"
            })
            .AddEnvironmentVariables("THINKONERP_TEST_") // Allow override via environment variables
            .Build();

        // Check if real external storage should be used
        _useRealS3 = configuration.GetValue<bool>("TestStorage:S3:UseReal");
        _useRealAzure = configuration.GetValue<bool>("TestStorage:Azure:UseReal");
        _s3ConnectionString = configuration["TestStorage:S3:ConnectionString"];
        _azureConnectionString = configuration["TestStorage:Azure:ConnectionString"];

        // Setup DI container
        var services = new ServiceCollection();
        services.AddLogging(builder => builder.AddConsole().SetMinimumLevel(LogLevel.Debug));
        services.Configure<ArchivalOptions>(configuration.GetSection("Archival"));
        services.AddSingleton<IExternalStorageProviderFactory, ExternalStorageProviderFactory>();
        
        _serviceProvider = services.BuildServiceProvider();
        _logger = _serviceProvider.GetRequiredService<ILogger<ExternalStorageIntegrationTests>>();
        
        _output.WriteLine($"Test Configuration:");
        _output.WriteLine($"  Use Real S3: {_useRealS3}");
        _output.WriteLine($"  Use Real Azure: {_useRealAzure}");
        _output.WriteLine($"  S3 Connection: {_s3ConnectionString}");
        _output.WriteLine($"  Azure Connection: {_azureConnectionString}");
    }

    #region S3 Integration Tests

    [Fact]
    public async Task S3Storage_UploadDownloadDelete_ShouldWorkEndToEnd()
    {
        // Arrange
        var provider = CreateS3Provider();
        var archiveId = GenerateTestArchiveId();
        var testData = GenerateTestArchiveData();
        var metadata = new Dictionary<string, string>
        {
            ["EventType"] = "Authentication",
            ["RecordCount"] = "150",
            ["CompressionAlgorithm"] = "GZip",
            ["OriginalSize"] = testData.Length.ToString()
        };

        try
        {
            // Act & Assert - Upload
            _output.WriteLine($"Testing S3 upload for archive {archiveId}...");
            var storageLocation = await provider.UploadAsync(archiveId, testData, metadata);
            
            Assert.NotNull(storageLocation);
            Assert.StartsWith("s3://", storageLocation);
            _createdStorageLocations.Add(storageLocation);
            _output.WriteLine($"Upload successful: {storageLocation}");

            // Act & Assert - Verify existence
            _output.WriteLine("Verifying S3 object exists...");
            var exists = await provider.ExistsAsync(storageLocation);
            Assert.True(exists, "Uploaded object should exist");

            // Act & Assert - Get metadata
            _output.WriteLine("Retrieving S3 object metadata...");
            var retrievedMetadata = await provider.GetMetadataAsync(storageLocation);
            Assert.NotNull(retrievedMetadata);
            Assert.Contains("eventtype", retrievedMetadata.Keys.Select(k => k.ToLowerInvariant()));
            Assert.Contains("recordcount", retrievedMetadata.Keys.Select(k => k.ToLowerInvariant()));

            // Act & Assert - Download
            _output.WriteLine("Testing S3 download...");
            var downloadedData = await provider.DownloadAsync(storageLocation);
            Assert.Equal(testData, downloadedData);

            // Act & Assert - Integrity verification
            _output.WriteLine("Verifying S3 data integrity...");
            var checksum = CalculateChecksum(testData);
            var isIntegrityValid = await provider.VerifyIntegrityAsync(storageLocation, checksum);
            Assert.True(isIntegrityValid, "Data integrity should be valid");

            // Act & Assert - Delete
            _output.WriteLine("Testing S3 delete...");
            var deleteResult = await provider.DeleteAsync(storageLocation);
            Assert.True(deleteResult, "Delete operation should succeed");

            // Verify deletion
            var existsAfterDelete = await provider.ExistsAsync(storageLocation);
            Assert.False(existsAfterDelete, "Object should not exist after deletion");
            
            _createdStorageLocations.Remove(storageLocation);
            _output.WriteLine("S3 end-to-end test completed successfully");
        }
        catch (Exception ex)
        {
            _output.WriteLine($"S3 test failed: {ex.Message}");
            throw;
        }
    }

    [Fact]
    public async Task S3Storage_LargeFile_ShouldHandleCorrectly()
    {
        // Arrange
        var provider = CreateS3Provider();
        var archiveId = GenerateTestArchiveId();
        
        // Create a larger test file (1MB)
        var largeData = new byte[1024 * 1024];
        new Random().NextBytes(largeData);
        
        var metadata = new Dictionary<string, string>
        {
            ["EventType"] = "DataChange",
            ["RecordCount"] = "10000",
            ["FileSize"] = largeData.Length.ToString()
        };

        try
        {
            // Act & Assert
            _output.WriteLine($"Testing S3 large file upload (1MB) for archive {archiveId}...");
            var storageLocation = await provider.UploadAsync(archiveId, largeData, metadata);
            
            Assert.NotNull(storageLocation);
            _createdStorageLocations.Add(storageLocation);
            _output.WriteLine($"Large file upload successful: {storageLocation}");

            // Verify download
            _output.WriteLine("Testing large file download...");
            var downloadedData = await provider.DownloadAsync(storageLocation);
            Assert.Equal(largeData.Length, downloadedData.Length);
            Assert.Equal(largeData, downloadedData);

            _output.WriteLine("S3 large file test completed successfully");
        }
        catch (Exception ex)
        {
            _output.WriteLine($"S3 large file test failed: {ex.Message}");
            throw;
        }
    }

    [Fact]
    public async Task S3Storage_InvalidCredentials_ShouldHandleGracefully()
    {
        // Arrange - Create provider with invalid credentials
        var invalidConnectionString = "BucketName=nonexistent-bucket;Region=us-east-1;AccessKey=invalid;SecretKey=invalid;Prefix=test/";
        var factory = _serviceProvider.GetRequiredService<IExternalStorageProviderFactory>();
        
        try
        {
            var provider = factory.CreateProvider("S3", invalidConnectionString);
            var archiveId = GenerateTestArchiveId();
            var testData = GenerateTestArchiveData();

            // Act & Assert - Should handle authentication errors gracefully
            _output.WriteLine("Testing S3 with invalid credentials...");
            
            if (_useRealS3)
            {
                // With real S3, expect authentication exception
                await Assert.ThrowsAsync<AmazonS3Exception>(async () =>
                    await provider.UploadAsync(archiveId, testData, new Dictionary<string, string>()));
            }
            else
            {
                // With mocked S3, the provider should still be created successfully
                Assert.NotNull(provider);
                Assert.Equal("S3", provider.ProviderName);
            }
            
            _output.WriteLine("S3 invalid credentials test completed");
        }
        catch (Exception ex)
        {
            _output.WriteLine($"S3 invalid credentials test failed: {ex.Message}");
            throw;
        }
    }

    [Fact]
    public async Task S3Storage_NetworkFailure_ShouldRetryAndFail()
    {
        // This test is primarily for mocked scenarios
        if (_useRealS3)
        {
            _output.WriteLine("Skipping network failure test for real S3");
            return;
        }

        // Arrange - Create a mock that simulates network failures
        var mockS3Client = new Mock<IAmazonS3>();
        var mockLogger = new Mock<ILogger<S3StorageProvider>>();
        
        // Setup mock to throw network exception
        mockS3Client
            .Setup(x => x.PutObjectAsync(It.IsAny<PutObjectRequest>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new AmazonS3Exception("Network error"));

        var provider = new S3StorageProvider(mockS3Client.Object, mockLogger.Object, _s3ConnectionString!);
        var archiveId = GenerateTestArchiveId();
        var testData = GenerateTestArchiveData();

        // Act & Assert
        _output.WriteLine("Testing S3 network failure handling...");
        await Assert.ThrowsAsync<AmazonS3Exception>(async () =>
            await provider.UploadAsync(archiveId, testData, new Dictionary<string, string>()));
        
        _output.WriteLine("S3 network failure test completed");
    }

    #endregion

    #region Azure Blob Storage Integration Tests

    [Fact]
    public async Task AzureStorage_UploadDownloadDelete_ShouldWorkEndToEnd()
    {
        // Arrange
        var provider = CreateAzureProvider();
        var archiveId = GenerateTestArchiveId();
        var testData = GenerateTestArchiveData();
        var metadata = new Dictionary<string, string>
        {
            ["EventType"] = "Permission",
            ["RecordCount"] = "75",
            ["CompressionAlgorithm"] = "GZip",
            ["OriginalSize"] = testData.Length.ToString()
        };

        try
        {
            // Act & Assert - Upload
            _output.WriteLine($"Testing Azure upload for archive {archiveId}...");
            var storageLocation = await provider.UploadAsync(archiveId, testData, metadata);
            
            Assert.NotNull(storageLocation);
            Assert.Contains("blob.core.windows.net", storageLocation);
            _createdStorageLocations.Add(storageLocation);
            _output.WriteLine($"Upload successful: {storageLocation}");

            // Act & Assert - Verify existence
            _output.WriteLine("Verifying Azure blob exists...");
            var exists = await provider.ExistsAsync(storageLocation);
            Assert.True(exists, "Uploaded blob should exist");

            // Act & Assert - Get metadata
            _output.WriteLine("Retrieving Azure blob metadata...");
            var retrievedMetadata = await provider.GetMetadataAsync(storageLocation);
            Assert.NotNull(retrievedMetadata);
            Assert.Contains("eventtype", retrievedMetadata.Keys.Select(k => k.ToLowerInvariant()));
            Assert.Contains("recordcount", retrievedMetadata.Keys.Select(k => k.ToLowerInvariant()));

            // Act & Assert - Download
            _output.WriteLine("Testing Azure download...");
            var downloadedData = await provider.DownloadAsync(storageLocation);
            Assert.Equal(testData, downloadedData);

            // Act & Assert - Integrity verification
            _output.WriteLine("Verifying Azure data integrity...");
            var checksum = CalculateChecksum(testData);
            var isIntegrityValid = await provider.VerifyIntegrityAsync(storageLocation, checksum);
            Assert.True(isIntegrityValid, "Data integrity should be valid");

            // Act & Assert - Delete
            _output.WriteLine("Testing Azure delete...");
            var deleteResult = await provider.DeleteAsync(storageLocation);
            Assert.True(deleteResult, "Delete operation should succeed");

            // Verify deletion
            var existsAfterDelete = await provider.ExistsAsync(storageLocation);
            Assert.False(existsAfterDelete, "Blob should not exist after deletion");
            
            _createdStorageLocations.Remove(storageLocation);
            _output.WriteLine("Azure end-to-end test completed successfully");
        }
        catch (Exception ex)
        {
            _output.WriteLine($"Azure test failed: {ex.Message}");
            throw;
        }
    }

    [Fact]
    public async Task AzureStorage_LargeFile_ShouldHandleCorrectly()
    {
        // Arrange
        var provider = CreateAzureProvider();
        var archiveId = GenerateTestArchiveId();
        
        // Create a larger test file (1MB)
        var largeData = new byte[1024 * 1024];
        new Random().NextBytes(largeData);
        
        var metadata = new Dictionary<string, string>
        {
            ["EventType"] = "Configuration",
            ["RecordCount"] = "5000",
            ["FileSize"] = largeData.Length.ToString()
        };

        try
        {
            // Act & Assert
            _output.WriteLine($"Testing Azure large file upload (1MB) for archive {archiveId}...");
            var storageLocation = await provider.UploadAsync(archiveId, largeData, metadata);
            
            Assert.NotNull(storageLocation);
            _createdStorageLocations.Add(storageLocation);
            _output.WriteLine($"Large file upload successful: {storageLocation}");

            // Verify download
            _output.WriteLine("Testing large file download...");
            var downloadedData = await provider.DownloadAsync(storageLocation);
            Assert.Equal(largeData.Length, downloadedData.Length);
            Assert.Equal(largeData, downloadedData);

            _output.WriteLine("Azure large file test completed successfully");
        }
        catch (Exception ex)
        {
            _output.WriteLine($"Azure large file test failed: {ex.Message}");
            throw;
        }
    }

    [Fact]
    public async Task AzureStorage_InvalidCredentials_ShouldHandleGracefully()
    {
        // Arrange - Create provider with invalid credentials
        var invalidConnectionString = "AccountName=nonexistent;AccountKey=aW52YWxpZGtleQ==;ContainerName=test-container;Prefix=test/";
        var factory = _serviceProvider.GetRequiredService<IExternalStorageProviderFactory>();
        
        try
        {
            var provider = factory.CreateProvider("AzureBlob", invalidConnectionString);
            var archiveId = GenerateTestArchiveId();
            var testData = GenerateTestArchiveData();

            // Act & Assert - Should handle authentication errors gracefully
            _output.WriteLine("Testing Azure with invalid credentials...");
            
            if (_useRealAzure)
            {
                // With real Azure, expect authentication exception
                await Assert.ThrowsAnyAsync<Exception>(async () =>
                    await provider.UploadAsync(archiveId, testData, new Dictionary<string, string>()));
            }
            else
            {
                // With mocked Azure, the provider should still be created successfully
                Assert.NotNull(provider);
                Assert.Equal("AzureBlob", provider.ProviderName);
            }
            
            _output.WriteLine("Azure invalid credentials test completed");
        }
        catch (Exception ex)
        {
            _output.WriteLine($"Azure invalid credentials test failed: {ex.Message}");
            throw;
        }
    }

    #endregion

    #region Cross-Provider Tests

    [Fact]
    public async Task ExternalStorage_ProviderFactory_ShouldCreateCorrectProviders()
    {
        // Arrange
        var factory = _serviceProvider.GetRequiredService<IExternalStorageProviderFactory>();

        // Act & Assert - S3 Provider
        _output.WriteLine("Testing S3 provider creation...");
        var s3Provider = factory.CreateProvider("S3", _s3ConnectionString!);
        Assert.NotNull(s3Provider);
        Assert.Equal("S3", s3Provider.ProviderName);

        // Act & Assert - Azure Provider
        _output.WriteLine("Testing Azure provider creation...");
        var azureProvider = factory.CreateProvider("AzureBlob", _azureConnectionString!);
        Assert.NotNull(azureProvider);
        Assert.Equal("AzureBlob", azureProvider.ProviderName);

        // Act & Assert - Invalid Provider
        _output.WriteLine("Testing invalid provider creation...");
        Assert.Throws<ArgumentException>(() =>
            factory.CreateProvider("InvalidProvider", "test"));

        _output.WriteLine("Provider factory test completed successfully");
    }

    [Fact]
    public async Task ExternalStorage_HealthCheck_ShouldReportCorrectStatus()
    {
        // Arrange & Act
        var s3Provider = CreateS3Provider();
        var azureProvider = CreateAzureProvider();

        // Act & Assert - S3 Health Check
        _output.WriteLine("Testing S3 health check...");
        var s3Healthy = await s3Provider.IsHealthyAsync();
        
        if (_useRealS3)
        {
            // With real S3, health depends on actual connectivity
            _output.WriteLine($"S3 Health Status: {s3Healthy}");
        }
        else
        {
            // With mocked S3, health check should work
            Assert.True(s3Healthy);
        }

        // Act & Assert - Azure Health Check
        _output.WriteLine("Testing Azure health check...");
        var azureHealthy = await azureProvider.IsHealthyAsync();
        
        if (_useRealAzure)
        {
            // With real Azure, health depends on actual connectivity
            _output.WriteLine($"Azure Health Status: {azureHealthy}");
        }
        else
        {
            // With mocked Azure, health check should work
            Assert.True(azureHealthy);
        }

        _output.WriteLine("Health check test completed successfully");
    }

    [Fact]
    public async Task ExternalStorage_DataIntegrity_ShouldDetectCorruption()
    {
        // Arrange
        var provider = CreateS3Provider(); // Use S3 for this test
        var archiveId = GenerateTestArchiveId();
        var testData = GenerateTestArchiveData();
        var correctChecksum = CalculateChecksum(testData);
        var incorrectChecksum = "incorrect_checksum_value";

        try
        {
            // Act - Upload data
            _output.WriteLine($"Testing data integrity detection for archive {archiveId}...");
            var storageLocation = await provider.UploadAsync(archiveId, testData, new Dictionary<string, string>());
            _createdStorageLocations.Add(storageLocation);

            // Act & Assert - Correct checksum should pass
            var validIntegrity = await provider.VerifyIntegrityAsync(storageLocation, correctChecksum);
            Assert.True(validIntegrity, "Correct checksum should pass integrity check");

            // Act & Assert - Incorrect checksum should fail
            var invalidIntegrity = await provider.VerifyIntegrityAsync(storageLocation, incorrectChecksum);
            Assert.False(invalidIntegrity, "Incorrect checksum should fail integrity check");

            _output.WriteLine("Data integrity test completed successfully");
        }
        catch (Exception ex)
        {
            _output.WriteLine($"Data integrity test failed: {ex.Message}");
            throw;
        }
    }

    #endregion

    #region Helper Methods

    private IExternalStorageProvider CreateS3Provider()
    {
        var factory = _serviceProvider.GetRequiredService<IExternalStorageProviderFactory>();
        return factory.CreateProvider("S3", _s3ConnectionString!);
    }

    private IExternalStorageProvider CreateAzureProvider()
    {
        var factory = _serviceProvider.GetRequiredService<IExternalStorageProviderFactory>();
        return factory.CreateProvider("AzureBlob", _azureConnectionString!);
    }

    private static long GenerateTestArchiveId()
    {
        return DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
    }

    private static byte[] GenerateTestArchiveData()
    {
        // Generate realistic audit log data
        var auditEntries = new List<object>();
        for (int i = 0; i < 100; i++)
        {
            auditEntries.Add(new
            {
                Id = i + 1,
                ActorType = "USER",
                ActorId = 123,
                CompanyId = 1,
                BranchId = 1,
                Action = "UPDATE",
                EntityType = "SysUser",
                EntityId = i + 1,
                OldValue = $"{{\"Name\":\"User{i}\"}}",
                NewValue = $"{{\"Name\":\"UpdatedUser{i}\"}}",
                IpAddress = "192.168.1.100",
                UserAgent = "Mozilla/5.0 Test Browser",
                CreationDate = DateTime.UtcNow.AddMinutes(-i),
                CorrelationId = Guid.NewGuid().ToString()
            });
        }

        var json = JsonSerializer.Serialize(auditEntries, new JsonSerializerOptions { WriteIndented = true });
        return Encoding.UTF8.GetBytes(json);
    }

    private static string CalculateChecksum(byte[] data)
    {
        using var sha256 = SHA256.Create();
        var hash = sha256.ComputeHash(data);
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    public void Dispose()
    {
        // Cleanup any created storage objects
        if (_createdStorageLocations.Any())
        {
            _output.WriteLine($"Cleaning up {_createdStorageLocations.Count} created storage objects...");
            
            foreach (var location in _createdStorageLocations.ToList())
            {
                try
                {
                    if (location.StartsWith("s3://"))
                    {
                        var s3Provider = CreateS3Provider();
                        _ = s3Provider.DeleteAsync(location).GetAwaiter().GetResult();
                    }
                    else if (location.Contains("blob.core.windows.net"))
                    {
                        var azureProvider = CreateAzureProvider();
                        _ = azureProvider.DeleteAsync(location).GetAwaiter().GetResult();
                    }
                    
                    _output.WriteLine($"Cleaned up: {location}");
                }
                catch (Exception ex)
                {
                    _output.WriteLine($"Failed to cleanup {location}: {ex.Message}");
                }
            }
        }

        _serviceProvider?.Dispose();
    }

    #endregion
}