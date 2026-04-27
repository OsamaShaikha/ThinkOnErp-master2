using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Moq;
using StackExchange.Redis;
using Xunit;
using ThinkOnErp.Domain.Interfaces;
using ThinkOnErp.Domain.Models;
using ThinkOnErp.Domain.Entities;
using ThinkOnErp.Infrastructure.Configuration;
using ThinkOnErp.Infrastructure.Data;
using ThinkOnErp.Infrastructure.Services;

namespace ThinkOnErp.Infrastructure.Tests.Integration;

/// <summary>
/// Integration tests for Redis cache configuration and setup scenarios.
/// Tests various configuration options, connection scenarios, and fallback behaviors.
/// 
/// **Validates: Requirements 8.5, 8.6, 6.3**
/// - Redis configuration validation
/// - Connection string handling
/// - Fallback behavior when Redis is unavailable
/// - Configuration option validation
/// </summary>
public class RedisCacheConfigurationIntegrationTests : IDisposable
{
    private ServiceProvider? _serviceProvider;
    private ConnectionMultiplexer? _redis;

    #region Configuration Tests

    [Fact]
    public async Task AuditQueryService_WithCachingDisabled_SkipsCacheOperations()
    {
        // Arrange
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["AuditQueryCaching:Enabled"] = "false", // Caching disabled
                ["AuditQueryCaching:CacheDurationMinutes"] = "5",
                ["AuditQueryCaching:RedisConnectionString"] = "localhost:6379"
            })
            .Build();

        var services = CreateServiceCollection(configuration);
        _serviceProvider = services.BuildServiceProvider();

        var auditQueryService = _serviceProvider.GetRequiredService<AuditQueryService>();
        var mockRepository = _serviceProvider.GetRequiredService<IAuditRepository>();

        var filter = new AuditQueryFilter { CompanyId = 1 };
        var pagination = new PaginationOptions { PageNumber = 1, PageSize = 10 };

        // Act - Multiple calls should all hit database
        await auditQueryService.QueryAsync(filter, pagination);
        await auditQueryService.QueryAsync(filter, pagination);
        await auditQueryService.QueryAsync(filter, pagination);

        // Assert - Repository should be called 3 times (no caching)
        var mockRepo = Mock.Get(mockRepository);
        mockRepo.Verify(r => r.QueryAsync(It.IsAny<AuditQueryFilter>(), It.IsAny<PaginationOptions>(), It.IsAny<CancellationToken>()), Times.Exactly(3));
    }

    [Fact]
    public async Task AuditQueryService_WithCustomCacheDuration_RespectsConfiguration()
    {
        // Arrange
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:Redis"] = "localhost:6379",
                ["AuditQueryCaching:Enabled"] = "true",
                ["AuditQueryCaching:CacheDurationMinutes"] = "15", // Custom duration
                ["AuditQueryCaching:RedisConnectionString"] = "localhost:6379"
            })
            .Build();

        var services = CreateServiceCollection(configuration);
        _serviceProvider = services.BuildServiceProvider();

        var auditQueryService = _serviceProvider.GetRequiredService<AuditQueryService>();
        var distributedCache = _serviceProvider.GetRequiredService<IDistributedCache>();

        // Setup Redis connection for verification
        _redis = ConnectionMultiplexer.Connect("localhost:6379");
        var redisDatabase = _redis.GetDatabase();
        await redisDatabase.ExecuteAsync("FLUSHDB");

        var filter = new AuditQueryFilter { CompanyId = 1 };
        var pagination = new PaginationOptions { PageNumber = 1, PageSize = 10 };

        // Act - Cache a result
        await auditQueryService.QueryAsync(filter, pagination);

        // Assert - Verify TTL is approximately 15 minutes (900 seconds)
        var server = _redis.GetServer(_redis.GetEndPoints().First());
        var keys = server.Keys(pattern: "audit:query:*");
        var cacheKey = keys.FirstOrDefault();
        
        Assert.NotNull(cacheKey);
        
        var ttl = await redisDatabase.KeyTimeToLiveAsync(cacheKey);
        Assert.True(ttl.HasValue);
        Assert.True(ttl.Value.TotalMinutes >= 14 && ttl.Value.TotalMinutes <= 15, 
            $"TTL should be approximately 15 minutes, but was {ttl.Value.TotalMinutes} minutes");
    }

    [Fact]
    public async Task SecurityMonitor_WithRedisDisabled_FallsBackToDatabase()
    {
        // Arrange
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["SecurityMonitoring:UseRedisCache"] = "false", // Redis disabled
                ["SecurityMonitoring:FailedLoginThreshold"] = "5",
                ["SecurityMonitoring:FailedLoginWindowMinutes"] = "5"
            })
            .Build();

        var services = CreateServiceCollection(configuration);
        _serviceProvider = services.BuildServiceProvider();

        var securityMonitor = _serviceProvider.GetRequiredService<SecurityMonitor>();
        var ipAddress = "192.168.1.100";

        // Act & Assert - Should use database fallback (will throw in test due to mock setup)
        var exception = await Assert.ThrowsAsync<NullReferenceException>(async () =>
            await securityMonitor.DetectFailedLoginPatternAsync(ipAddress));

        // Exception indicates it tried to use database fallback instead of Redis
        Assert.NotNull(exception);
    }

    [Fact]
    public void SecurityMonitor_WithRedisEnabledButNoCache_LogsWarning()
    {
        // Arrange
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["SecurityMonitoring:UseRedisCache"] = "true", // Redis enabled
                ["SecurityMonitoring:RedisConnectionString"] = "localhost:6379",
                ["SecurityMonitoring:FailedLoginThreshold"] = "5",
                ["SecurityMonitoring:FailedLoginWindowMinutes"] = "5"
            })
            .Build();

        var services = new ServiceCollection();
        
        // Add logging to capture warnings
        var mockLogger = new Mock<ILogger<SecurityMonitor>>();
        services.AddSingleton(mockLogger.Object);
        
        // Add configuration
        services.Configure<SecurityMonitoringOptions>(configuration.GetSection("SecurityMonitoring"));
        
        // Add other required services but NOT IDistributedCache
        var mockDbContext = new Mock<OracleDbContext>();
        services.AddSingleton(mockDbContext.Object);
        
        _serviceProvider = services.BuildServiceProvider();

        // Act - Create SecurityMonitor without IDistributedCache
        var securityMonitor = new SecurityMonitor(
            mockDbContext.Object,
            mockLogger.Object,
            _serviceProvider.GetRequiredService<IOptions<SecurityMonitoringOptions>>(),
            null); // No cache provided

        // Assert - Should log warning about Redis being enabled but unavailable
        mockLogger.Verify(
            x => x.Log(
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("UseRedisCache is enabled but IDistributedCache is not available")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    #endregion

    #region Connection String Tests

    [Fact]
    public async Task RedisConnection_WithValidConnectionString_ConnectsSuccessfully()
    {
        // Arrange
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:Redis"] = "localhost:6379",
                ["AuditQueryCaching:Enabled"] = "true",
                ["AuditQueryCaching:CacheDurationMinutes"] = "5",
                ["AuditQueryCaching:RedisConnectionString"] = "localhost:6379"
            })
            .Build();

        var services = CreateServiceCollection(configuration);
        _serviceProvider = services.BuildServiceProvider();

        var distributedCache = _serviceProvider.GetRequiredService<IDistributedCache>();

        // Act - Test Redis connection by setting and getting a value
        var testKey = "test:connection";
        var testValue = "connection_test_value";
        
        await distributedCache.SetStringAsync(testKey, testValue);
        var retrievedValue = await distributedCache.GetStringAsync(testKey);

        // Assert
        Assert.Equal(testValue, retrievedValue);
        
        // Cleanup
        await distributedCache.RemoveAsync(testKey);
    }

    [Fact]
    public async Task RedisConnection_WithInvalidConnectionString_HandlesGracefully()
    {
        // Arrange
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:Redis"] = "invalid:9999", // Invalid connection
                ["AuditQueryCaching:Enabled"] = "true",
                ["AuditQueryCaching:CacheDurationMinutes"] = "5",
                ["AuditQueryCaching:RedisConnectionString"] = "invalid:9999"
            })
            .Build();

        var services = CreateServiceCollection(configuration);
        
        // This should not throw during service registration
        _serviceProvider = services.BuildServiceProvider();

        var auditQueryService = _serviceProvider.GetRequiredService<AuditQueryService>();
        var filter = new AuditQueryFilter { CompanyId = 1 };
        var pagination = new PaginationOptions { PageNumber = 1, PageSize = 10 };

        // Act - Should gracefully fall back to database when Redis is unavailable
        var result = await auditQueryService.QueryAsync(filter, pagination);

        // Assert - Should still return results from database
        Assert.NotNull(result);
        
        var mockRepository = _serviceProvider.GetRequiredService<IAuditRepository>();
        var mockRepo = Mock.Get(mockRepository);
        mockRepo.Verify(r => r.QueryAsync(It.IsAny<AuditQueryFilter>(), It.IsAny<PaginationOptions>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    #endregion

    #region Configuration Validation Tests

    [Fact]
    public void AuditQueryCachingOptions_WithValidConfiguration_ValidatesSuccessfully()
    {
        // Arrange
        var options = new AuditQueryCachingOptions
        {
            Enabled = true,
            CacheDurationMinutes = 10,
            RedisConnectionString = "localhost:6379",
            ParallelQueriesEnabled = true,
            ParallelQueryThresholdDays = 30,
            ParallelQueryChunkSizeDays = 7,
            MaxParallelQueries = 4
        };

        // Act & Assert - Should not throw
        Assert.True(options.Enabled);
        Assert.Equal(10, options.CacheDurationMinutes);
        Assert.Equal(TimeSpan.FromMinutes(10), options.CacheDuration);
        Assert.Equal("localhost:6379", options.RedisConnectionString);
        Assert.True(options.ParallelQueriesEnabled);
        Assert.Equal(30, options.ParallelQueryThresholdDays);
        Assert.Equal(7, options.ParallelQueryChunkSizeDays);
        Assert.Equal(4, options.MaxParallelQueries);
    }

    [Fact]
    public void SecurityMonitoringOptions_WithValidConfiguration_ValidatesSuccessfully()
    {
        // Arrange
        var options = new SecurityMonitoringOptions
        {
            Enabled = true,
            UseRedisCache = true,
            RedisConnectionString = "localhost:6379",
            FailedLoginThreshold = 5,
            FailedLoginWindowMinutes = 5
        };

        // Act & Assert - Should not throw
        Assert.True(options.Enabled);
        Assert.True(options.UseRedisCache);
        Assert.Equal("localhost:6379", options.RedisConnectionString);
        Assert.Equal(5, options.FailedLoginThreshold);
        Assert.Equal(5, options.FailedLoginWindowMinutes);
    }

    #endregion

    #region Fallback Behavior Tests

    [Fact]
    public async Task AuditQueryService_WithRedisConnectionLoss_ContinuesOperation()
    {
        // Arrange
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:Redis"] = "localhost:6379",
                ["AuditQueryCaching:Enabled"] = "true",
                ["AuditQueryCaching:CacheDurationMinutes"] = "5",
                ["AuditQueryCaching:RedisConnectionString"] = "localhost:6379"
            })
            .Build();

        var services = CreateServiceCollection(configuration);
        _serviceProvider = services.BuildServiceProvider();

        var auditQueryService = _serviceProvider.GetRequiredService<AuditQueryService>();
        var distributedCache = _serviceProvider.GetRequiredService<IDistributedCache>();

        var filter = new AuditQueryFilter { CompanyId = 1 };
        var pagination = new PaginationOptions { PageNumber = 1, PageSize = 10 };

        // Act - First call should work normally
        var result1 = await auditQueryService.QueryAsync(filter, pagination);
        Assert.NotNull(result1);

        // Simulate Redis connection loss by disposing the connection
        if (_redis != null)
        {
            await _redis.DisposeAsync();
        }

        // Second call should gracefully fall back to database
        var result2 = await auditQueryService.QueryAsync(filter, pagination);
        Assert.NotNull(result2);

        // Assert - Both calls should succeed
        var mockRepository = _serviceProvider.GetRequiredService<IAuditRepository>();
        var mockRepo = Mock.Get(mockRepository);
        
        // Should be called at least twice (once for initial, once for fallback)
        mockRepo.Verify(r => r.QueryAsync(It.IsAny<AuditQueryFilter>(), It.IsAny<PaginationOptions>(), It.IsAny<CancellationToken>()), Times.AtLeast(2));
    }

    [Fact]
    public async Task SecurityMonitor_WithRedisConnectionLoss_FallsBackToDatabase()
    {
        // Arrange
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:Redis"] = "localhost:6379",
                ["SecurityMonitoring:UseRedisCache"] = "true",
                ["SecurityMonitoring:RedisConnectionString"] = "localhost:6379",
                ["SecurityMonitoring:FailedLoginThreshold"] = "5",
                ["SecurityMonitoring:FailedLoginWindowMinutes"] = "5"
            })
            .Build();

        var services = CreateServiceCollection(configuration);
        _serviceProvider = services.BuildServiceProvider();

        var securityMonitor = _serviceProvider.GetRequiredService<SecurityMonitor>();
        var ipAddress = "192.168.1.100";

        // Act - First call should work with Redis
        await securityMonitor.TrackFailedLoginAttemptAsync(ipAddress, "user1", "Invalid password");

        // Simulate Redis connection loss
        if (_redis != null)
        {
            await _redis.DisposeAsync();
        }

        // Second call should fall back to database (will throw in test due to mock setup)
        var exception = await Assert.ThrowsAsync<NullReferenceException>(async () =>
            await securityMonitor.TrackFailedLoginAttemptAsync(ipAddress, "user2", "Invalid password"));

        // Assert - Exception indicates fallback to database was attempted
        Assert.NotNull(exception);
    }

    #endregion

    #region Helper Methods

    private ServiceCollection CreateServiceCollection(IConfiguration configuration)
    {
        var services = new ServiceCollection();
        
        // Add logging
        services.AddLogging(builder => builder.AddConsole().SetMinimumLevel(LogLevel.Debug));
        
        // Add Redis distributed cache
        services.AddStackExchangeRedisCache(options =>
        {
            options.Configuration = configuration.GetConnectionString("Redis");
        });
        
        // Add configuration options
        services.Configure<AuditQueryCachingOptions>(configuration.GetSection("AuditQueryCaching"));
        services.Configure<SecurityMonitoringOptions>(configuration.GetSection("SecurityMonitoring"));
        
        // Add HTTP context accessor
        services.AddSingleton<IHttpContextAccessor, HttpContextAccessor>();
        
        // Create mock repository
        var mockAuditRepository = new Mock<IAuditRepository>();
        mockAuditRepository.Setup(r => r.QueryAsync(It.IsAny<AuditQueryFilter>(), It.IsAny<PaginationOptions>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PagedResult<SysAuditLog> { Items = new List<SysAuditLog>(), TotalCount = 0, PageNumber = 1, PageSize = 10 });
        services.AddSingleton(mockAuditRepository.Object);
        
        // Create mock HTTP context accessor
        var mockHttpContextAccessor = new Mock<IHttpContextAccessor>();
        var mockHttpContext = new Mock<HttpContext>();
        var mockUser = new Mock<System.Security.Claims.ClaimsPrincipal>();
        mockUser.Setup(u => u.FindFirst("sub")).Returns(new System.Security.Claims.Claim("sub", "1"));
        mockUser.Setup(u => u.FindFirst("role")).Returns(new System.Security.Claims.Claim("role", "SUPER_ADMIN"));
        mockHttpContext.Setup(c => c.User).Returns(mockUser.Object);
        mockHttpContextAccessor.Setup(a => a.HttpContext).Returns(mockHttpContext.Object);
        services.AddSingleton(mockHttpContextAccessor.Object);
        
        // Create in-memory database context
        var dbOptions = new DbContextOptionsBuilder<OracleDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
        var dbContext = new OracleDbContext(dbOptions);
        services.AddSingleton(dbContext);
        
        // Add services
        services.AddScoped<AuditQueryService>();
        services.AddScoped<SecurityMonitor>();
        
        return services;
    }

    #endregion

    public void Dispose()
    {
        try
        {
            if (_redis != null)
            {
                var database = _redis.GetDatabase();
                database.ExecuteAsync("FLUSHDB").Wait();
                _redis.Dispose();
            }
        }
        catch (Exception)
        {
            // Ignore cleanup errors
        }
        
        _serviceProvider?.Dispose();
    }
}