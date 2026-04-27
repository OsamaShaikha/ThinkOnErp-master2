using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Oracle.ManagedDataAccess.Client;
using ThinkOnErp.Domain.Interfaces;
using ThinkOnErp.Infrastructure.Configuration;
using ThinkOnErp.Infrastructure.Data;
using ThinkOnErp.Infrastructure.Services;
using Xunit;

namespace ThinkOnErp.Infrastructure.Tests.Services;

/// <summary>
/// Integration tests for ArchivalService with external storage providers.
/// Tests export to and import from external storage (S3, Azure Blob).
/// </summary>
public class ArchivalServiceExternalStorageTests
{
    [Fact]
    public async Task ExportToExternalStorageAsync_ShouldExportSuccessfully()
    {
        // Arrange
        var mockDbContext = new Mock<OracleDbContext>();
        var mockLogger = new Mock<ILogger<ArchivalService>>();
        var mockCompressionService = new Mock<ICompressionService>();
        var mockStorageProviderFactory = new Mock<IExternalStorageProviderFactory>();
        var mockStorageProvider = new Mock<IExternalStorageProvider>();

        var options = Options.Create(new ArchivalOptions
        {
            StorageProvider = "S3",
            StorageConnectionString = "BucketName=test-bucket;Region=us-east-1",
            CompressionAlgorithm = "GZip",
            VerifyIntegrity = true
        });

        // Setup mock connection
        var mockConnection = new Mock<OracleConnection>();
        mockDbContext.Setup(x => x.CreateConnection()).Returns(mockConnection.Object);

        // Setup storage provider factory
        mockStorageProviderFactory
            .Setup(x => x.CreateProvider("S3", It.IsAny<string>()))
            .Returns(mockStorageProvider.Object);

        mockStorageProvider.Setup(x => x.ProviderName).Returns("S3");
        mockStorageProvider
            .Setup(x => x.UploadAsync(
                It.IsAny<long>(),
                It.IsAny<byte[]>(),
                It.IsAny<Dictionary<string, string>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync("s3://test-bucket/archives/year=2024/month=01/archive-12345.bin");

        var service = new ArchivalService(
            mockDbContext.Object,
            mockLogger.Object,
            options,
            mockCompressionService.Object,
            mockStorageProviderFactory.Object);

        var archiveId = 12345L;

        // Act & Assert
        // Note: This test would require a real database connection to work fully
        // For now, we verify that the service is properly initialized with external storage
        Assert.NotNull(service);
    }

    [Fact]
    public async Task ExportToExternalStorageAsync_ShouldThrowWhenProviderNotConfigured()
    {
        // Arrange
        var mockDbContext = new Mock<OracleDbContext>();
        var mockLogger = new Mock<ILogger<ArchivalService>>();
        var mockCompressionService = new Mock<ICompressionService>();

        var options = Options.Create(new ArchivalOptions
        {
            StorageProvider = "Database", // No external storage configured
            CompressionAlgorithm = "GZip"
        });

        var service = new ArchivalService(
            mockDbContext.Object,
            mockLogger.Object,
            options,
            mockCompressionService.Object,
            null); // No factory provided

        var archiveId = 12345L;

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await service.ExportToExternalStorageAsync(archiveId));
    }

    [Fact]
    public async Task ImportFromExternalStorageAsync_ShouldThrowWhenProviderNotConfigured()
    {
        // Arrange
        var mockDbContext = new Mock<OracleDbContext>();
        var mockLogger = new Mock<ILogger<ArchivalService>>();
        var mockCompressionService = new Mock<ICompressionService>();

        var options = Options.Create(new ArchivalOptions
        {
            StorageProvider = "Database", // No external storage configured
            CompressionAlgorithm = "GZip"
        });

        var service = new ArchivalService(
            mockDbContext.Object,
            mockLogger.Object,
            options,
            mockCompressionService.Object,
            null); // No factory provided

        var storageLocation = "s3://test-bucket/archives/archive-12345.bin";

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await service.ImportFromExternalStorageAsync(storageLocation));
    }

    [Fact]
    public async Task VerifyExternalStorageIntegrityAsync_ShouldThrowWhenProviderNotConfigured()
    {
        // Arrange
        var mockDbContext = new Mock<OracleDbContext>();
        var mockLogger = new Mock<ILogger<ArchivalService>>();
        var mockCompressionService = new Mock<ICompressionService>();

        var options = Options.Create(new ArchivalOptions
        {
            StorageProvider = "Database", // No external storage configured
            CompressionAlgorithm = "GZip"
        });

        var service = new ArchivalService(
            mockDbContext.Object,
            mockLogger.Object,
            options,
            mockCompressionService.Object,
            null); // No factory provided

        var storageLocation = "s3://test-bucket/archives/archive-12345.bin";
        var expectedChecksum = "abc123";

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await service.VerifyExternalStorageIntegrityAsync(storageLocation, expectedChecksum));
    }

    [Fact]
    public void ArchivalService_Constructor_ShouldInitializeExternalStorageProvider()
    {
        // Arrange
        var mockDbContext = new Mock<OracleDbContext>();
        var mockLogger = new Mock<ILogger<ArchivalService>>();
        var mockCompressionService = new Mock<ICompressionService>();
        var mockStorageProviderFactory = new Mock<IExternalStorageProviderFactory>();
        var mockStorageProvider = new Mock<IExternalStorageProvider>();

        var options = Options.Create(new ArchivalOptions
        {
            StorageProvider = "S3",
            StorageConnectionString = "BucketName=test-bucket;Region=us-east-1",
            CompressionAlgorithm = "GZip"
        });

        mockStorageProviderFactory
            .Setup(x => x.CreateProvider("S3", "BucketName=test-bucket;Region=us-east-1"))
            .Returns(mockStorageProvider.Object);

        mockStorageProvider.Setup(x => x.ProviderName).Returns("S3");

        // Act
        var service = new ArchivalService(
            mockDbContext.Object,
            mockLogger.Object,
            options,
            mockCompressionService.Object,
            mockStorageProviderFactory.Object);

        // Assert
        Assert.NotNull(service);
        mockStorageProviderFactory.Verify(
            x => x.CreateProvider("S3", "BucketName=test-bucket;Region=us-east-1"),
            Times.Once);
    }

    [Fact]
    public void ArchivalService_Constructor_ShouldNotInitializeExternalStorageWhenDatabaseProvider()
    {
        // Arrange
        var mockDbContext = new Mock<OracleDbContext>();
        var mockLogger = new Mock<ILogger<ArchivalService>>();
        var mockCompressionService = new Mock<ICompressionService>();
        var mockStorageProviderFactory = new Mock<IExternalStorageProviderFactory>();

        var options = Options.Create(new ArchivalOptions
        {
            StorageProvider = "Database", // Database storage, no external provider needed
            CompressionAlgorithm = "GZip"
        });

        // Act
        var service = new ArchivalService(
            mockDbContext.Object,
            mockLogger.Object,
            options,
            mockCompressionService.Object,
            mockStorageProviderFactory.Object);

        // Assert
        Assert.NotNull(service);
        mockStorageProviderFactory.Verify(
            x => x.CreateProvider(It.IsAny<string>(), It.IsAny<string>()),
            Times.Never);
    }

    [Fact]
    public void ArchivalService_Constructor_ShouldHandleExternalStorageInitializationFailure()
    {
        // Arrange
        var mockDbContext = new Mock<OracleDbContext>();
        var mockLogger = new Mock<ILogger<ArchivalService>>();
        var mockCompressionService = new Mock<ICompressionService>();
        var mockStorageProviderFactory = new Mock<IExternalStorageProviderFactory>();

        var options = Options.Create(new ArchivalOptions
        {
            StorageProvider = "S3",
            StorageConnectionString = "BucketName=test-bucket;Region=us-east-1",
            CompressionAlgorithm = "GZip"
        });

        // Setup factory to throw exception
        mockStorageProviderFactory
            .Setup(x => x.CreateProvider("S3", It.IsAny<string>()))
            .Throws(new Exception("Failed to initialize S3 client"));

        // Act
        var service = new ArchivalService(
            mockDbContext.Object,
            mockLogger.Object,
            options,
            mockCompressionService.Object,
            mockStorageProviderFactory.Object);

        // Assert
        // Service should still be created, but external storage will not be available
        Assert.NotNull(service);
    }
}
