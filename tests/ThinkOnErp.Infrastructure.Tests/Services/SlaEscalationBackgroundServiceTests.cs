using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Moq;
using ThinkOnErp.Domain.Interfaces;
using ThinkOnErp.Infrastructure.Services;
using Xunit;

namespace ThinkOnErp.Infrastructure.Tests.Services;

/// <summary>
/// Unit tests for SlaEscalationBackgroundService.
/// Tests background service lifecycle, configuration, and escalation processing.
/// </summary>
public class SlaEscalationBackgroundServiceTests
{
    private readonly Mock<ILogger<SlaEscalationBackgroundService>> _mockLogger;
    private readonly Mock<IConfiguration> _mockConfiguration;

    public SlaEscalationBackgroundServiceTests()
    {
        _mockLogger = new Mock<ILogger<SlaEscalationBackgroundService>>();
        _mockConfiguration = new Mock<IConfiguration>();
    }

    [Fact]
    public void Constructor_WithNullServiceProvider_ThrowsArgumentNullException()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() =>
            new SlaEscalationBackgroundService(null!, _mockLogger.Object, _mockConfiguration.Object));
    }

    [Fact]
    public void Constructor_WithNullLogger_ThrowsArgumentNullException()
    {
        // Arrange
        var serviceProvider = new Mock<IServiceProvider>().Object;

        // Act & Assert
        Assert.Throws<ArgumentNullException>(() =>
            new SlaEscalationBackgroundService(serviceProvider, null!, _mockConfiguration.Object));
    }

    [Fact]
    public void Constructor_WithNullConfiguration_ThrowsArgumentNullException()
    {
        // Arrange
        var serviceProvider = new Mock<IServiceProvider>().Object;

        // Act & Assert
        Assert.Throws<ArgumentNullException>(() =>
            new SlaEscalationBackgroundService(serviceProvider, _mockLogger.Object, null!));
    }

    [Fact]
    public async Task StartAsync_WithValidConfiguration_StartsSuccessfully()
    {
        // Arrange
        SetupConfiguration(enabled: true, intervalMinutes: 30);
        var mockEscalationService = new Mock<ISlaEscalationService>();
        var serviceProvider = CreateServiceProvider(mockEscalationService.Object);
        var service = new SlaEscalationBackgroundService(serviceProvider, _mockLogger.Object, _mockConfiguration.Object);

        // Act
        await service.StartAsync(CancellationToken.None);

        // Assert - Service should start without throwing
        Assert.True(true);
    }

    [Fact]
    public async Task StopAsync_AfterStart_StopsSuccessfully()
    {
        // Arrange
        SetupConfiguration(enabled: true, intervalMinutes: 30);
        var mockEscalationService = new Mock<ISlaEscalationService>();
        var serviceProvider = CreateServiceProvider(mockEscalationService.Object);
        var service = new SlaEscalationBackgroundService(serviceProvider, _mockLogger.Object, _mockConfiguration.Object);

        await service.StartAsync(CancellationToken.None);

        // Act
        await service.StopAsync(CancellationToken.None);

        // Assert - Service should stop without throwing
        Assert.True(true);
    }

    [Fact]
    public async Task ExecuteAsync_WhenDisabled_DoesNotThrow()
    {
        // Arrange
        SetupConfiguration(enabled: false, intervalMinutes: 1);
        var mockEscalationService = new Mock<ISlaEscalationService>();
        var serviceProvider = CreateServiceProvider(mockEscalationService.Object);
        var service = new SlaEscalationBackgroundService(serviceProvider, _mockLogger.Object, _mockConfiguration.Object);

        var cts = new CancellationTokenSource();

        // Act
        var startTask = service.StartAsync(cts.Token);
        await Task.Delay(50); // Give it a moment to check configuration
        cts.Cancel();

        // Assert - Should not throw
        await startTask;
        Assert.True(true);
    }

    #region Helper Methods

    private void SetupConfiguration(bool enabled, int intervalMinutes)
    {
        _mockConfiguration
            .Setup(c => c["SlaEscalation:BackgroundService:Enabled"])
            .Returns(enabled.ToString());

        _mockConfiguration
            .Setup(c => c["SlaEscalation:BackgroundService:IntervalMinutes"])
            .Returns(intervalMinutes.ToString());
    }

    private IServiceProvider CreateServiceProvider(ISlaEscalationService escalationService)
    {
        var services = new ServiceCollection();
        services.AddScoped<ISlaEscalationService>(sp => escalationService);
        services.AddLogging();
        return services.BuildServiceProvider();
    }

    #endregion
}
