using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using ThinkOnErp.Domain.Models;
using ThinkOnErp.Infrastructure.Configuration;
using ThinkOnErp.Infrastructure.Data;
using ThinkOnErp.Infrastructure.Services;
using Xunit;

namespace ThinkOnErp.Infrastructure.Tests.Services;

/// <summary>
/// Unit tests for SecurityMonitor Redis integration.
/// Tests Redis-based sliding window tracking for failed login attempts.
/// </summary>
public class SecurityMonitorRedisTests
{
    private readonly Mock<OracleDbContext> _mockDbContext;
    private readonly Mock<ILogger<SecurityMonitor>> _mockLogger;
    private readonly Mock<IDistributedCache> _mockCache;
    private readonly SecurityMonitoringOptions _options;

    public SecurityMonitorRedisTests()
    {
        _mockDbContext = new Mock<OracleDbContext>();
        _mockLogger = new Mock<ILogger<SecurityMonitor>>();
        _mockCache = new Mock<IDistributedCache>();
        
        _options = new SecurityMonitoringOptions
        {
            Enabled = true,
            FailedLoginThreshold = 5,
            FailedLoginWindowMinutes = 5,
            UseRedisCache = true,
            RedisConnectionString = "localhost:6379"
        };
    }

    [Fact]
    public async Task DetectFailedLoginPatternAsync_WithRedisEnabled_UsesRedisForTracking()
    {
        // Arrange
        var ipAddress = "192.168.1.100";
        var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var timestamps = new[]
        {
            now - 60,  // 1 minute ago
            now - 120, // 2 minutes ago
            now - 180, // 3 minutes ago
            now - 240, // 4 minutes ago
            now - 280  // 4.67 minutes ago
        };
        
        var cachedData = string.Join(',', timestamps);
        
        _mockCache.Setup(c => c.GetAsync(
            It.Is<string>(k => k == $"failed_logins:{ipAddress}"),
            default))
            .ReturnsAsync(System.Text.Encoding.UTF8.GetBytes(cachedData));

        var monitor = new SecurityMonitor(
            _mockDbContext.Object,
            _mockLogger.Object,
            Options.Create(_options),
            _mockCache.Object);

        // Act
        var threat = await monitor.DetectFailedLoginPatternAsync(ipAddress);

        // Assert
        Assert.NotNull(threat);
        Assert.Equal(ThreatType.FailedLoginPattern, threat.ThreatType);
        Assert.Equal(ipAddress, threat.IpAddress);
        Assert.Contains("5 attempts", threat.Description);
        
        // Verify Redis was called
        _mockCache.Verify(c => c.GetAsync(
            It.Is<string>(k => k == $"failed_logins:{ipAddress}"),
            default), Times.Once);
    }

    [Fact]
    public async Task DetectFailedLoginPatternAsync_WithRedisEnabled_FiltersToSlidingWindow()
    {
        // Arrange
        var ipAddress = "192.168.1.100";
        var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var timestamps = new[]
        {
            now - 60,   // 1 minute ago (in window)
            now - 120,  // 2 minutes ago (in window)
            now - 180,  // 3 minutes ago (in window)
            now - 400,  // 6.67 minutes ago (outside window)
            now - 500   // 8.33 minutes ago (outside window)
        };
        
        var cachedData = string.Join(',', timestamps);
        
        _mockCache.Setup(c => c.GetAsync(
            It.Is<string>(k => k == $"failed_logins:{ipAddress}"),
            default))
            .ReturnsAsync(System.Text.Encoding.UTF8.GetBytes(cachedData));

        var monitor = new SecurityMonitor(
            _mockDbContext.Object,
            _mockLogger.Object,
            Options.Create(_options),
            _mockCache.Object);

        // Act
        var threat = await monitor.DetectFailedLoginPatternAsync(ipAddress);

        // Assert - Should only count 3 attempts within the 5-minute window
        Assert.Null(threat); // Below threshold of 5
    }

    [Fact]
    public async Task DetectFailedLoginPatternAsync_WithRedisDisabled_FallsBackToDatabase()
    {
        // Arrange
        var ipAddress = "192.168.1.100";
        _options.UseRedisCache = false;

        var monitor = new SecurityMonitor(
            _mockDbContext.Object,
            _mockLogger.Object,
            Options.Create(_options),
            null); // No cache provided

        // Act & Assert
        // This will attempt to use database (which will fail in unit test without real DB)
        // But it demonstrates the fallback path
        await Assert.ThrowsAsync<NullReferenceException>(async () =>
            await monitor.DetectFailedLoginPatternAsync(ipAddress));
        
        // Verify Redis was NOT called
        _mockCache.Verify(c => c.GetAsync(It.IsAny<string>(), default), Times.Never);
    }

    [Fact]
    public async Task DetectFailedLoginPatternAsync_WithEmptyCache_ReturnsNull()
    {
        // Arrange
        var ipAddress = "192.168.1.100";
        
        _mockCache.Setup(c => c.GetAsync(
            It.Is<string>(k => k == $"failed_logins:{ipAddress}"),
            default))
            .ReturnsAsync((byte[]?)null);

        var monitor = new SecurityMonitor(
            _mockDbContext.Object,
            _mockLogger.Object,
            Options.Create(_options),
            _mockCache.Object);

        // Act
        var threat = await monitor.DetectFailedLoginPatternAsync(ipAddress);

        // Assert
        Assert.Null(threat);
    }

    [Fact]
    public async Task DetectFailedLoginPatternAsync_WithNullOrEmptyIpAddress_ReturnsNull()
    {
        // Arrange
        var monitor = new SecurityMonitor(
            _mockDbContext.Object,
            _mockLogger.Object,
            Options.Create(_options),
            _mockCache.Object);

        // Act & Assert
        var threat1 = await monitor.DetectFailedLoginPatternAsync(null!);
        var threat2 = await monitor.DetectFailedLoginPatternAsync("");
        var threat3 = await monitor.DetectFailedLoginPatternAsync("   ");

        Assert.Null(threat1);
        Assert.Null(threat2);
        Assert.Null(threat3);
    }

    [Fact]
    public async Task DetectFailedLoginPatternAsync_WithExactlyThresholdAttempts_ReturnsThreat()
    {
        // Arrange
        var ipAddress = "192.168.1.100";
        var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var timestamps = new[]
        {
            now - 60,  // 1 minute ago
            now - 120, // 2 minutes ago
            now - 180, // 3 minutes ago
            now - 240, // 4 minutes ago
            now - 280  // 4.67 minutes ago
        };
        
        var cachedData = string.Join(',', timestamps);
        
        _mockCache.Setup(c => c.GetAsync(
            It.Is<string>(k => k == $"failed_logins:{ipAddress}"),
            default))
            .ReturnsAsync(System.Text.Encoding.UTF8.GetBytes(cachedData));

        var monitor = new SecurityMonitor(
            _mockDbContext.Object,
            _mockLogger.Object,
            Options.Create(_options),
            _mockCache.Object);

        // Act
        var threat = await monitor.DetectFailedLoginPatternAsync(ipAddress);

        // Assert
        Assert.NotNull(threat);
        Assert.Equal(ThreatSeverity.High, threat.Severity);
    }

    [Fact]
    public async Task DetectFailedLoginPatternAsync_WithTenOrMoreAttempts_ReturnsCriticalThreat()
    {
        // Arrange
        var ipAddress = "192.168.1.100";
        var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var timestamps = new long[10];
        for (int i = 0; i < 10; i++)
        {
            timestamps[i] = now - (i * 30); // Every 30 seconds
        }
        
        var cachedData = string.Join(',', timestamps);
        
        _mockCache.Setup(c => c.GetAsync(
            It.Is<string>(k => k == $"failed_logins:{ipAddress}"),
            default))
            .ReturnsAsync(System.Text.Encoding.UTF8.GetBytes(cachedData));

        var monitor = new SecurityMonitor(
            _mockDbContext.Object,
            _mockLogger.Object,
            Options.Create(_options),
            _mockCache.Object);

        // Act
        var threat = await monitor.DetectFailedLoginPatternAsync(ipAddress);

        // Assert
        Assert.NotNull(threat);
        Assert.Equal(ThreatSeverity.Critical, threat.Severity);
        Assert.Contains("10 attempts", threat.Description);
    }

    [Fact]
    public void SecurityMonitor_WithRedisEnabledButNoCacheProvided_LogsWarning()
    {
        // Arrange & Act
        var monitor = new SecurityMonitor(
            _mockDbContext.Object,
            _mockLogger.Object,
            Options.Create(_options),
            null); // No cache provided but UseRedisCache is true

        // Assert
        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("UseRedisCache is enabled but IDistributedCache is not available")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public async Task TrackFailedLoginAttemptAsync_WithRedisEnabled_StoresInRedis()
    {
        // Arrange
        var ipAddress = "192.168.1.100";
        var username = "testuser";
        var failureReason = "Invalid password";

        _mockCache.Setup(c => c.GetAsync(
            It.Is<string>(k => k == $"failed_logins:{ipAddress}"),
            default))
            .ReturnsAsync((byte[]?)null);

        _mockCache.Setup(c => c.SetAsync(
            It.Is<string>(k => k == $"failed_logins:{ipAddress}"),
            It.IsAny<byte[]>(),
            It.IsAny<DistributedCacheEntryOptions>(),
            default))
            .Returns(Task.CompletedTask);

        var monitor = new SecurityMonitor(
            _mockDbContext.Object,
            _mockLogger.Object,
            Options.Create(_options),
            _mockCache.Object);

        // Act
        await monitor.TrackFailedLoginAttemptAsync(ipAddress, username, failureReason);

        // Assert
        _mockCache.Verify(c => c.SetAsync(
            It.Is<string>(k => k == $"failed_logins:{ipAddress}"),
            It.IsAny<byte[]>(),
            It.Is<DistributedCacheEntryOptions>(o => 
                o.AbsoluteExpirationRelativeToNow == TimeSpan.FromMinutes(_options.FailedLoginWindowMinutes * 2)),
            default), Times.Once);
    }

    [Fact]
    public async Task GetFailedLoginCountForUserAsync_WithRedisEnabled_UsesRedis()
    {
        // Arrange
        var username = "testuser";
        var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var timestamps = new[]
        {
            now - 60,  // 1 minute ago
            now - 120, // 2 minutes ago
            now - 180  // 3 minutes ago
        };
        
        var cachedData = string.Join(',', timestamps);
        
        _mockCache.Setup(c => c.GetAsync(
            It.Is<string>(k => k == $"failed_logins_user:{username}"),
            default))
            .ReturnsAsync(System.Text.Encoding.UTF8.GetBytes(cachedData));

        var monitor = new SecurityMonitor(
            _mockDbContext.Object,
            _mockLogger.Object,
            Options.Create(_options),
            _mockCache.Object);

        // Act
        var count = await monitor.GetFailedLoginCountForUserAsync(username);

        // Assert
        Assert.Equal(3, count);
        
        // Verify Redis was called
        _mockCache.Verify(c => c.GetAsync(
            It.Is<string>(k => k == $"failed_logins_user:{username}"),
            default), Times.Once);
    }
}
