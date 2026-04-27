using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using ThinkOnErp.Domain.Interfaces;
using ThinkOnErp.Domain.Models;
using ThinkOnErp.Infrastructure.Configuration;
using ThinkOnErp.Infrastructure.Services;
using Xunit;

namespace ThinkOnErp.Infrastructure.Tests.Services;

/// <summary>
/// Unit tests for ArchivalBackgroundService.
/// Tests background service lifecycle, cron scheduling, configuration, and archival execution.
/// </summary>
public class ArchivalBackgroundServiceTests
{
    private readonly Mock<ILogger<ArchivalBackgroundService>> _mockLogger;
    private readonly ArchivalOptions _defaultOptions;

    public ArchivalBackgroundServiceTests()
    {
        _mockLogger = new Mock<ILogger<ArchivalBackgroundService>>();
        _defaultOptions = new ArchivalOptions
        {
            Enabled = true,
            Schedule = "0 2 * * *", // Daily at 2 AM
            BatchSize = 10000,
            CompressionAlgorithm = "GZip",
            StorageProvider = "Database",
            VerifyIntegrity = true,
            TimeoutMinutes = 60,
            RunOnStartup = false,
            TimeZone = "UTC"
        };
    }

    #region Constructor Tests

    [Fact]
    public void Constructor_WithNullServiceProvider_ThrowsArgumentNullException()
    {
        // Arrange
        var options = Options.Create(_defaultOptions);

        // Act & Assert
        Assert.Throws<ArgumentNullException>(() =>
            new ArchivalBackgroundService(null!, _mockLogger.Object, options));
    }

    [Fact]
    public void Constructor_WithNullLogger_ThrowsArgumentNullException()
    {
        // Arrange
        var serviceProvider = new Mock<IServiceProvider>().Object;
        var options = Options.Create(_defaultOptions);

        // Act & Assert
        Assert.Throws<ArgumentNullException>(() =>
            new ArchivalBackgroundService(serviceProvider, null!, options));
    }

    [Fact]
    public void Constructor_WithNullOptions_ThrowsArgumentNullException()
    {
        // Arrange
        var serviceProvider = new Mock<IServiceProvider>().Object;

        // Act & Assert
        Assert.Throws<ArgumentNullException>(() =>
            new ArchivalBackgroundService(serviceProvider, _mockLogger.Object, null!));
    }

    [Fact]
    public void Constructor_WithValidParameters_InitializesSuccessfully()
    {
        // Arrange
        var serviceProvider = new Mock<IServiceProvider>().Object;
        var options = Options.Create(_defaultOptions);

        // Act
        var service = new ArchivalBackgroundService(serviceProvider, _mockLogger.Object, options);

        // Assert
        Assert.NotNull(service);
    }

    [Fact]
    public void Constructor_WithInvalidCronExpression_UsesDefaultSchedule()
    {
        // Arrange
        var invalidOptions = new ArchivalOptions
        {
            Enabled = true,
            Schedule = "invalid cron expression",
            TimeZone = "UTC"
        };
        var serviceProvider = new Mock<IServiceProvider>().Object;
        var options = Options.Create(invalidOptions);

        // Act
        var service = new ArchivalBackgroundService(serviceProvider, _mockLogger.Object, options);

        // Assert
        Assert.NotNull(service);
        // Verify that an error was logged about invalid cron expression
        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Error,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Invalid cron expression")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public void Constructor_WithInvalidTimeZone_UsesUtc()
    {
        // Arrange
        var invalidOptions = new ArchivalOptions
        {
            Enabled = true,
            Schedule = "0 2 * * *",
            TimeZone = "Invalid/TimeZone"
        };
        var serviceProvider = new Mock<IServiceProvider>().Object;
        var options = Options.Create(invalidOptions);

        // Act
        var service = new ArchivalBackgroundService(serviceProvider, _mockLogger.Object, options);

        // Assert
        Assert.NotNull(service);
        // Verify that a warning was logged about invalid time zone
        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Invalid time zone")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    #endregion

    #region Service Lifecycle Tests

    [Fact]
    public async Task StartAsync_WithValidConfiguration_StartsSuccessfully()
    {
        // Arrange
        var mockArchivalService = new Mock<IArchivalService>();
        var serviceProvider = CreateServiceProvider(mockArchivalService.Object);
        var options = Options.Create(_defaultOptions);
        var service = new ArchivalBackgroundService(serviceProvider, _mockLogger.Object, options);

        // Act
        await service.StartAsync(CancellationToken.None);

        // Assert - Service should start without throwing
        Assert.True(true);
    }

    [Fact]
    public async Task StopAsync_AfterStart_StopsGracefully()
    {
        // Arrange
        var mockArchivalService = new Mock<IArchivalService>();
        var serviceProvider = CreateServiceProvider(mockArchivalService.Object);
        var options = Options.Create(_defaultOptions);
        var service = new ArchivalBackgroundService(serviceProvider, _mockLogger.Object, options);

        await service.StartAsync(CancellationToken.None);

        // Act
        await service.StopAsync(CancellationToken.None);

        // Assert - Service should stop without throwing
        Assert.True(true);
        
        // Verify graceful shutdown was logged
        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("stopping gracefully")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public async Task ExecuteAsync_WhenDisabled_DoesNotExecuteArchival()
    {
        // Arrange
        var disabledOptions = new ArchivalOptions
        {
            Enabled = false,
            Schedule = "0 2 * * *",
            TimeZone = "UTC"
        };
        var mockArchivalService = new Mock<IArchivalService>();
        var serviceProvider = CreateServiceProvider(mockArchivalService.Object);
        var options = Options.Create(disabledOptions);
        var service = new ArchivalBackgroundService(serviceProvider, _mockLogger.Object, options);

        var cts = new CancellationTokenSource();

        // Act
        var startTask = service.StartAsync(cts.Token);
        await Task.Delay(100); // Give it time to check configuration
        cts.Cancel();

        // Assert
        await startTask;
        
        // Verify archival service was never called
        mockArchivalService.Verify(
            x => x.ArchiveExpiredDataAsync(It.IsAny<CancellationToken>()),
            Times.Never);
        
        // Verify disabled message was logged
        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("disabled")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    #endregion

    #region RunOnStartup Tests

    [Fact]
    public async Task ExecuteAsync_WithRunOnStartup_ExecutesImmediately()
    {
        // Arrange
        var runOnStartupOptions = new ArchivalOptions
        {
            Enabled = true,
            Schedule = "0 2 * * *",
            RunOnStartup = true,
            TimeZone = "UTC"
        };
        
        var mockArchivalService = new Mock<IArchivalService>();
        mockArchivalService
            .Setup(x => x.ArchiveExpiredDataAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<ArchivalResult>
            {
                new ArchivalResult
                {
                    ArchiveId = 1,
                    RecordsArchived = 100,
                    IsSuccess = true,
                    ArchivalStartTime = DateTime.UtcNow,
                    ArchivalEndTime = DateTime.UtcNow.AddSeconds(5)
                }
            });

        var serviceProvider = CreateServiceProvider(mockArchivalService.Object);
        var options = Options.Create(runOnStartupOptions);
        var service = new ArchivalBackgroundService(serviceProvider, _mockLogger.Object, options);

        var cts = new CancellationTokenSource();

        // Act
        var startTask = service.StartAsync(cts.Token);
        await Task.Delay(500); // Give it time to execute
        cts.Cancel();

        // Assert
        await startTask;
        
        // Verify archival service was called at least once
        mockArchivalService.Verify(
            x => x.ArchiveExpiredDataAsync(It.IsAny<CancellationToken>()),
            Times.AtLeastOnce);
    }

    #endregion

    #region Archival Execution Tests

    [Fact]
    public async Task ExecuteArchival_WithSuccessfulResults_LogsSuccess()
    {
        // Arrange
        var runOnStartupOptions = new ArchivalOptions
        {
            Enabled = true,
            Schedule = "0 2 * * *",
            RunOnStartup = true,
            TimeZone = "UTC"
        };
        
        var mockArchivalService = new Mock<IArchivalService>();
        mockArchivalService
            .Setup(x => x.ArchiveExpiredDataAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<ArchivalResult>
            {
                new ArchivalResult
                {
                    ArchiveId = 1,
                    RecordsArchived = 100,
                    IsSuccess = true,
                    UncompressedSize = 1000000,
                    CompressedSize = 300000,
                    ArchivalStartTime = DateTime.UtcNow,
                    ArchivalEndTime = DateTime.UtcNow.AddSeconds(5)
                },
                new ArchivalResult
                {
                    ArchiveId = 2,
                    RecordsArchived = 50,
                    IsSuccess = true,
                    UncompressedSize = 500000,
                    CompressedSize = 150000,
                    ArchivalStartTime = DateTime.UtcNow,
                    ArchivalEndTime = DateTime.UtcNow.AddSeconds(3)
                }
            });

        var serviceProvider = CreateServiceProvider(mockArchivalService.Object);
        var options = Options.Create(runOnStartupOptions);
        var service = new ArchivalBackgroundService(serviceProvider, _mockLogger.Object, options);

        var cts = new CancellationTokenSource();

        // Act
        var startTask = service.StartAsync(cts.Token);
        await Task.Delay(500); // Give it time to execute
        cts.Cancel();

        // Assert
        await startTask;
        
        // Verify success was logged
        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("completed successfully")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.AtLeastOnce);
        
        // Verify compression statistics were logged
        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Compression statistics")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.AtLeastOnce);
    }

    [Fact]
    public async Task ExecuteArchival_WithFailedResults_LogsWarning()
    {
        // Arrange
        var runOnStartupOptions = new ArchivalOptions
        {
            Enabled = true,
            Schedule = "0 2 * * *",
            RunOnStartup = true,
            TimeZone = "UTC"
        };
        
        var mockArchivalService = new Mock<IArchivalService>();
        mockArchivalService
            .Setup(x => x.ArchiveExpiredDataAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<ArchivalResult>
            {
                new ArchivalResult
                {
                    ArchiveId = 1,
                    RecordsArchived = 100,
                    IsSuccess = true,
                    ArchivalStartTime = DateTime.UtcNow,
                    ArchivalEndTime = DateTime.UtcNow.AddSeconds(5)
                },
                new ArchivalResult
                {
                    ArchiveId = 2,
                    RecordsArchived = 0,
                    IsSuccess = false,
                    ErrorMessage = "Database connection failed",
                    ArchivalStartTime = DateTime.UtcNow,
                    ArchivalEndTime = DateTime.UtcNow.AddSeconds(1)
                }
            });

        var serviceProvider = CreateServiceProvider(mockArchivalService.Object);
        var options = Options.Create(runOnStartupOptions);
        var service = new ArchivalBackgroundService(serviceProvider, _mockLogger.Object, options);

        var cts = new CancellationTokenSource();

        // Act
        var startTask = service.StartAsync(cts.Token);
        await Task.Delay(500); // Give it time to execute
        cts.Cancel();

        // Assert
        await startTask;
        
        // Verify warning was logged
        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("completed with") && v.ToString()!.Contains("failures")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.AtLeastOnce);
        
        // Verify error details were logged
        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Error,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Database connection failed")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.AtLeastOnce);
    }

    [Fact]
    public async Task ExecuteArchival_WithException_LogsError()
    {
        // Arrange
        var runOnStartupOptions = new ArchivalOptions
        {
            Enabled = true,
            Schedule = "0 2 * * *",
            RunOnStartup = true,
            TimeZone = "UTC"
        };
        
        var mockArchivalService = new Mock<IArchivalService>();
        mockArchivalService
            .Setup(x => x.ArchiveExpiredDataAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Archival service error"));

        var serviceProvider = CreateServiceProvider(mockArchivalService.Object);
        var options = Options.Create(runOnStartupOptions);
        var service = new ArchivalBackgroundService(serviceProvider, _mockLogger.Object, options);

        var cts = new CancellationTokenSource();

        // Act
        var startTask = service.StartAsync(cts.Token);
        await Task.Delay(500); // Give it time to execute
        cts.Cancel();

        // Assert
        await startTask;
        
        // Verify error was logged
        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Error,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("failed")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.AtLeastOnce);
    }

    [Fact]
    public async Task ExecuteArchival_WithTimeout_LogsTimeoutError()
    {
        // Arrange
        var shortTimeoutOptions = new ArchivalOptions
        {
            Enabled = true,
            Schedule = "0 2 * * *",
            RunOnStartup = true,
            TimeoutMinutes = 0, // Immediate timeout
            TimeZone = "UTC"
        };
        
        var mockArchivalService = new Mock<IArchivalService>();
        mockArchivalService
            .Setup(x => x.ArchiveExpiredDataAsync(It.IsAny<CancellationToken>()))
            .Returns(async (CancellationToken ct) =>
            {
                await Task.Delay(10000, ct); // Long delay to trigger timeout
                return new List<ArchivalResult>();
            });

        var serviceProvider = CreateServiceProvider(mockArchivalService.Object);
        var options = Options.Create(shortTimeoutOptions);
        var service = new ArchivalBackgroundService(serviceProvider, _mockLogger.Object, options);

        var cts = new CancellationTokenSource();

        // Act
        var startTask = service.StartAsync(cts.Token);
        await Task.Delay(500); // Give it time to execute and timeout
        cts.Cancel();

        // Assert
        await startTask;
        
        // Verify timeout was logged
        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Error,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("timed out")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.AtLeastOnce);
    }

    #endregion

    #region Cron Schedule Tests

    [Theory]
    [InlineData("0 2 * * *")]    // Daily at 2 AM
    [InlineData("0 */6 * * *")]  // Every 6 hours
    [InlineData("0 2 * * 0")]    // Weekly on Sunday at 2 AM
    [InlineData("0 2 1 * *")]    // Monthly on the 1st at 2 AM
    public void Constructor_WithValidCronExpressions_InitializesSuccessfully(string cronExpression)
    {
        // Arrange
        var options = new ArchivalOptions
        {
            Enabled = true,
            Schedule = cronExpression,
            TimeZone = "UTC"
        };
        var serviceProvider = new Mock<IServiceProvider>().Object;
        var optionsWrapper = Options.Create(options);

        // Act
        var service = new ArchivalBackgroundService(serviceProvider, _mockLogger.Object, optionsWrapper);

        // Assert
        Assert.NotNull(service);
    }

    #endregion

    #region Helper Methods

    private IServiceProvider CreateServiceProvider(IArchivalService archivalService)
    {
        var services = new ServiceCollection();
        services.AddScoped<IArchivalService>(sp => archivalService);
        services.AddLogging();
        return services.BuildServiceProvider();
    }

    #endregion
}
