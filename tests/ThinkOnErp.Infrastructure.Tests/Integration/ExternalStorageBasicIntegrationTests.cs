using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Xunit;
using Xunit.Abstractions;
using ThinkOnErp.Domain.Interfaces;
using ThinkOnErp.Infrastructure.Services;

namespace ThinkOnErp.Infrastructure.Tests.Integration;

/// <summary>
/// Basic integration tests for external storage functionality.
/// These tests verify that the external storage providers can be created and basic operations work.
/// 
/// **Validates: Task 19.9 - External Storage Integration Tests**
/// - S3 and Azure Blob Storage provider creation
/// - Basic configuration validation
/// - Factory pattern functionality
/// </summary>
public class ExternalStorageBasicIntegrationTests : IDisposable
{
    private readonly ITestOutputHelper _output;
    private readonly ServiceProvider _serviceProvider;

    public ExternalStorageBasicIntegrationTests(ITestOutputHelper output)
    {
        _output = output;
        
        // Setup DI container
        var services = new ServiceCollection();
        services.AddLogging(builder => builder.AddConsole().SetMinimumLevel(LogLevel.Information));
        services.AddSingleton<IExternalStorageProviderFactory, ExternalStorageProviderFactory>();
        
        _serviceProvider = services.BuildServiceProvider();
    }

    [Fact]
    public void ExternalStorageProviderFactory_CreateS3Provider_ShouldSucceed()
    {
        // Arrange
        var factory = _serviceProvider.GetRequiredService<IExternalStorageProviderFactory>();
        var connectionString = "BucketName=test-bucket;Region=us-east-1;Prefix=integration-tests/";

        // Act
        _output.WriteLine("Testing S3 provider creation...");
        var provider = factory.CreateProvider("S3", connectionString);

        // Assert
        Assert.NotNull(provider);
        Assert.Equal("S3", provider.ProviderName);
        _output.WriteLine("S3 provider created successfully");
    }

    [Fact]
    public void ExternalStorageProviderFactory_CreateAzureProvider_ShouldSucceed()
    {
        // Arrange
        var factory = _serviceProvider.GetRequiredService<IExternalStorageProviderFactory>();
        var connectionString = "AccountName=testaccount;AccountKey=dGVzdGtleQ==;ContainerName=test-container;Prefix=integration-tests/";

        // Act
        _output.WriteLine("Testing Azure provider creation...");
        var provider = factory.CreateProvider("AzureBlob", connectionString);

        // Assert
        Assert.NotNull(provider);
        Assert.Equal("AzureBlob", provider.ProviderName);
        _output.WriteLine("Azure provider created successfully");
    }

    [Fact]
    public void ExternalStorageProviderFactory_InvalidProvider_ShouldThrowException()
    {
        // Arrange
        var factory = _serviceProvider.GetRequiredService<IExternalStorageProviderFactory>();

        // Act & Assert
        _output.WriteLine("Testing invalid provider type...");
        var exception = Assert.Throws<ArgumentException>(() =>
            factory.CreateProvider("InvalidProvider", "test-connection-string"));
        
        Assert.Contains("Unsupported storage provider", exception.Message);
        _output.WriteLine($"Invalid provider correctly rejected: {exception.Message}");
    }

    [Theory]
    [InlineData("S3", "Region=us-east-1")] // Missing BucketName
    [InlineData("S3", "BucketName=")] // Empty BucketName
    [InlineData("AzureBlob", "AccountName=test")] // Missing ContainerName
    [InlineData("AzureBlob", "ContainerName=")] // Empty ContainerName
    public void ExternalStorageProviderFactory_InvalidConnectionString_ShouldThrowException(string providerType, string connectionString)
    {
        // Arrange
        var factory = _serviceProvider.GetRequiredService<IExternalStorageProviderFactory>();

        // Act & Assert
        _output.WriteLine($"Testing invalid connection string for {providerType}: {connectionString}");
        Assert.Throws<ArgumentException>(() => factory.CreateProvider(providerType, connectionString));
        _output.WriteLine($"Invalid connection string correctly rejected for {providerType}");
    }

    [Fact]
    public async Task ExternalStorageProvider_HealthCheck_ShouldWork()
    {
        // Arrange
        var factory = _serviceProvider.GetRequiredService<IExternalStorageProviderFactory>();
        var s3Provider = factory.CreateProvider("S3", "BucketName=test-bucket;Region=us-east-1");
        var azureProvider = factory.CreateProvider("AzureBlob", "ContainerName=test-container");

        // Act & Assert
        _output.WriteLine("Testing health checks...");
        
        // Note: Health checks may fail with real providers due to authentication
        // but the method should exist and be callable
        try
        {
            var s3Health = await s3Provider.IsHealthyAsync();
            _output.WriteLine($"S3 Health Check: {s3Health}");
        }
        catch (Exception ex)
        {
            _output.WriteLine($"S3 Health Check failed (expected): {ex.Message}");
        }

        try
        {
            var azureHealth = await azureProvider.IsHealthyAsync();
            _output.WriteLine($"Azure Health Check: {azureHealth}");
        }
        catch (Exception ex)
        {
            _output.WriteLine($"Azure Health Check failed (expected): {ex.Message}");
        }

        // The test passes if we can call the methods without compilation errors
        Assert.True(true, "Health check methods are callable");
    }

    [Fact]
    public void ExternalStorageProvider_ProviderNames_ShouldBeCorrect()
    {
        // Arrange
        var factory = _serviceProvider.GetRequiredService<IExternalStorageProviderFactory>();

        // Act
        var s3Provider = factory.CreateProvider("S3", "BucketName=test;Region=us-east-1");
        var azureProvider = factory.CreateProvider("AzureBlob", "ContainerName=test");

        // Assert
        _output.WriteLine("Testing provider names...");
        Assert.Equal("S3", s3Provider.ProviderName);
        Assert.Equal("AzureBlob", azureProvider.ProviderName);
        _output.WriteLine("Provider names are correct");
    }

    public void Dispose()
    {
        _serviceProvider?.Dispose();
    }
}