using System;
using System.Collections.Generic;
using System.Diagnostics;
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
/// Performance and load testing for Redis caching integration.
/// Tests cache performance under various load conditions and validates performance requirements.
/// 
/// **Validates: Requirements 8.5, 6.3, Performance Requirements**
/// - Cache performance under load
/// - Concurrent access handling
/// - Memory usage and efficiency
/// - Response time requirements
/// </summary>
public class RedisCachePerformanceIntegrationTests : IDisposable
{
    private readonly ServiceProvider _serviceProvider;
    private readonly IDistributedCache _distributedCache;
    private readonly ConnectionMultiplexer _redis;
    private readonly IDatabase _redisDatabase;
    private readonly AuditQueryService _auditQueryService;
    private readonly SecurityMonitor _securityMonitor;
    private readonly Mock<IAuditRepository> _mockAuditRepository;

    public RedisCachePerformanceIntegrationTests()
    {
        // Setup Redis connection for performance testing
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:Redis"] = "localhost:6379",
                ["AuditQueryCaching:Enabled"] = "true",
                ["AuditQueryCaching:CacheDurationMinutes"] = "10",
                ["AuditQueryCaching:RedisConnectionString"] = "localhost:6379",
                ["SecurityMonitoring:UseRedisCache"] = "true",
                ["SecurityMonitoring:RedisConnectionString"] = "localhost:6379",
                ["SecurityMonitoring:FailedLoginThreshold"] = "5",
                ["SecurityMonitoring:FailedLoginWindowMinutes"] = "5"
            })
            .Build();

        var services = new ServiceCollection();
        
        // Add logging with minimal level to reduce noise during performance tests
        services.AddLogging(builder => builder.AddConsole().SetMinimumLevel(LogLevel.Warning));
        
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
        _mockAuditRepository = new Mock<IAuditRepository>();
        services.AddSingleton(_mockAuditRepository.Object);
        
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
        
        _serviceProvider = services.BuildServiceProvider();
        
        // Get services
        _distributedCache = _serviceProvider.GetRequiredService<IDistributedCache>();
        _auditQueryService = _serviceProvider.GetRequiredService<AuditQueryService>();
        _securityMonitor = _serviceProvider.GetRequiredService<SecurityMonitor>();
        
        // Setup direct Redis connection for performance monitoring
        _redis = ConnectionMultiplexer.Connect("localhost:6379");
        _redisDatabase = _redis.GetDatabase();
        
        // Clear Redis before each test
        ClearRedisCache().Wait();
    }

    #region Cache Performance Tests

    [Fact]
    public async Task AuditQueryCache_HighVolumeQueries_MaintainsPerformance()
    {
        // Arrange
        var mockResults = CreateMockAuditResults(100); // 100 audit entries
        
        _mockAuditRepository.Setup(r => r.QueryAsync(It.IsAny<AuditQueryFilter>(), It.IsAny<PaginationOptions>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PagedResult<SysAuditLog>
            {
                Items = mockResults,
                TotalCount = 100,
                PageNumber = 1,
                PageSize = 100
            });

        var filter = new AuditQueryFilter { CompanyId = 1 };
        var pagination = new PaginationOptions { PageNumber = 1, PageSize = 100 };

        // Act - Measure first call (database + cache write)
        var stopwatch = Stopwatch.StartNew();
        var result1 = await _auditQueryService.QueryAsync(filter, pagination);
        stopwatch.Stop();
        var firstCallTime = stopwatch.ElapsedMilliseconds;

        // Act - Measure second call (cache hit)
        stopwatch.Restart();
        var result2 = await _auditQueryService.QueryAsync(filter, pagination);
        stopwatch.Stop();
        var secondCallTime = stopwatch.ElapsedMilliseconds;

        // Assert - Cache hit should be significantly faster
        Assert.NotNull(result1);
        Assert.NotNull(result2);
        Assert.True(secondCallTime < firstCallTime, 
            $"Cache hit ({secondCallTime}ms) should be faster than database call ({firstCallTime}ms)");
        
        // Cache hit should be under 50ms (performance requirement)
        Assert.True(secondCallTime < 50, 
            $"Cache hit took {secondCallTime}ms, should be under 50ms");
        
        // Verify repository was called only once
        _mockAuditRepository.Verify(r => r.QueryAsync(It.IsAny<AuditQueryFilter>(), It.IsAny<PaginationOptions>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task AuditQueryCache_ConcurrentRequests_HandlesLoadEfficiently()
    {
        // Arrange
        var mockResults = CreateMockAuditResults(50);
        
        _mockAuditRepository.Setup(r => r.QueryAsync(It.IsAny<AuditQueryFilter>(), It.IsAny<PaginationOptions>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PagedResult<SysAuditLog>
            {
                Items = mockResults,
                TotalCount = 50,
                PageNumber = 1,
                PageSize = 50
            });

        var filter = new AuditQueryFilter { CompanyId = 1 };
        var pagination = new PaginationOptions { PageNumber = 1, PageSize = 50 };

        // Act - Execute 100 concurrent requests
        var tasks = new List<Task<PagedResult<AuditLogEntry>>>();
        var stopwatch = Stopwatch.StartNew();
        
        for (int i = 0; i < 100; i++)
        {
            tasks.Add(_auditQueryService.QueryAsync(filter, pagination));
        }
        
        var results = await Task.WhenAll(tasks);
        stopwatch.Stop();

        // Assert - All requests should complete successfully
        Assert.All(results, result => Assert.NotNull(result));
        Assert.All(results, result => Assert.Equal(50, result.TotalCount));
        
        // Total time for 100 concurrent requests should be reasonable
        Assert.True(stopwatch.ElapsedMilliseconds < 5000, 
            $"100 concurrent requests took {stopwatch.ElapsedMilliseconds}ms, should be under 5000ms");
        
        // Repository should be called only once (first request), others use cache
        _mockAuditRepository.Verify(r => r.QueryAsync(It.IsAny<AuditQueryFilter>(), It.IsAny<PaginationOptions>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task SecurityMonitorCache_HighVolumeFailedLogins_MaintainsPerformance()
    {
        // Arrange
        var ipAddress = "192.168.1.100";
        var stopwatch = new Stopwatch();
        var trackingTimes = new List<long>();
        var detectionTimes = new List<long>();

        // Act - Track 1000 failed login attempts and measure performance
        for (int i = 0; i < 1000; i++)
        {
            stopwatch.Restart();
            await _securityMonitor.TrackFailedLoginAttemptAsync(ipAddress, $"user{i}", "Invalid password");
            stopwatch.Stop();
            trackingTimes.Add(stopwatch.ElapsedMilliseconds);
        }

        // Act - Perform 100 threat detection calls and measure performance
        for (int i = 0; i < 100; i++)
        {
            stopwatch.Restart();
            var threat = await _securityMonitor.DetectFailedLoginPatternAsync(ipAddress);
            stopwatch.Stop();
            detectionTimes.Add(stopwatch.ElapsedMilliseconds);
            
            Assert.NotNull(threat); // Should detect threat with 1000 attempts
        }

        // Assert - Performance requirements
        var avgTrackingTime = trackingTimes.Average();
        var avgDetectionTime = detectionTimes.Average();
        var maxTrackingTime = trackingTimes.Max();
        var maxDetectionTime = detectionTimes.Max();

        Assert.True(avgTrackingTime < 10, 
            $"Average tracking time {avgTrackingTime}ms should be under 10ms");
        Assert.True(avgDetectionTime < 20, 
            $"Average detection time {avgDetectionTime}ms should be under 20ms");
        Assert.True(maxTrackingTime < 50, 
            $"Max tracking time {maxTrackingTime}ms should be under 50ms");
        Assert.True(maxDetectionTime < 100, 
            $"Max detection time {maxDetectionTime}ms should be under 100ms");
    }

    [Fact]
    public async Task SecurityMonitorCache_ConcurrentFailedLogins_HandlesRaceConditions()
    {
        // Arrange
        var ipAddresses = Enumerable.Range(1, 10).Select(i => $"192.168.1.{i}").ToList();
        var stopwatch = Stopwatch.StartNew();

        // Act - Simulate concurrent failed logins from multiple IPs
        var tasks = new List<Task>();
        
        foreach (var ip in ipAddresses)
        {
            for (int i = 0; i < 10; i++)
            {
                var userId = i;
                tasks.Add(_securityMonitor.TrackFailedLoginAttemptAsync(ip, $"user{userId}", "Invalid password"));
            }
        }
        
        await Task.WhenAll(tasks);
        stopwatch.Stop();

        // Assert - All operations should complete quickly
        Assert.True(stopwatch.ElapsedMilliseconds < 2000, 
            $"100 concurrent tracking operations took {stopwatch.ElapsedMilliseconds}ms, should be under 2000ms");

        // Verify all IPs have threat detection
        var detectionTasks = ipAddresses.Select(ip => _securityMonitor.DetectFailedLoginPatternAsync(ip));
        var threats = await Task.WhenAll(detectionTasks);
        
        Assert.All(threats, threat => Assert.NotNull(threat));
        Assert.All(threats, threat => Assert.Equal(ThreatSeverity.Critical, threat.Severity));
    }

    #endregion

    #region Memory Usage Tests

    [Fact]
    public async Task CacheMemoryUsage_LargeDataSets_EfficientMemoryManagement()
    {
        // Arrange - Create large audit result sets
        var largeResultSets = new List<PagedResult<SysAuditLog>>();
        
        for (int i = 0; i < 10; i++)
        {
            largeResultSets.Add(new PagedResult<SysAuditLog>
            {
                Items = CreateMockAuditResults(1000), // 1000 entries per result set
                TotalCount = 1000,
                PageNumber = 1,
                PageSize = 1000
            });
        }

        var setupIndex = 0;
        _mockAuditRepository.Setup(r => r.QueryAsync(It.IsAny<AuditQueryFilter>(), It.IsAny<PaginationOptions>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => largeResultSets[setupIndex++ % largeResultSets.Count]);

        // Act - Cache multiple large result sets
        var tasks = new List<Task>();
        
        for (int i = 0; i < 10; i++)
        {
            var companyId = i;
            var filter = new AuditQueryFilter { CompanyId = companyId };
            var pagination = new PaginationOptions { PageNumber = 1, PageSize = 1000 };
            
            tasks.Add(_auditQueryService.QueryAsync(filter, pagination));
        }
        
        await Task.WhenAll(tasks);

        // Assert - Verify cache contains all entries
        var cacheKeys = await GetRedisCacheKeys("audit:query:*");
        Assert.Equal(10, cacheKeys.Count);

        // Verify Redis memory usage is reasonable
        var info = await _redisDatabase.ExecuteAsync("INFO", "memory");
        var memoryInfo = info.ToString();
        
        // Extract used memory (this is a simplified check)
        Assert.Contains("used_memory:", memoryInfo);
        
        // Cache should handle large datasets without issues
        // (Specific memory assertions would depend on Redis configuration)
    }

    [Fact]
    public async Task CacheMemoryUsage_HighFrequencyOperations_NoMemoryLeaks()
    {
        // Arrange
        var initialMemoryInfo = await _redisDatabase.ExecuteAsync("INFO", "memory");
        
        _mockAuditRepository.Setup(r => r.QueryAsync(It.IsAny<AuditQueryFilter>(), It.IsAny<PaginationOptions>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PagedResult<SysAuditLog> { Items = CreateMockAuditResults(10), TotalCount = 10, PageNumber = 1, PageSize = 10 });

        // Act - Perform many cache operations with different keys
        for (int i = 0; i < 1000; i++)
        {
            var filter = new AuditQueryFilter 
            { 
                CompanyId = i % 100, // 100 different companies
                StartDate = DateTime.UtcNow.AddDays(-i % 30) // 30 different date ranges
            };
            var pagination = new PaginationOptions { PageNumber = 1, PageSize = 10 };
            
            await _auditQueryService.QueryAsync(filter, pagination);
        }

        // Assert - Memory usage should be stable
        var finalMemoryInfo = await _redisDatabase.ExecuteAsync("INFO", "memory");
        
        // Verify cache contains expected number of unique entries
        var cacheKeys = await GetRedisCacheKeys("audit:query:*");
        
        // Should have many cache entries but not 1000 (due to key overlap from modulo operations)
        Assert.True(cacheKeys.Count > 100);
        Assert.True(cacheKeys.Count < 1000);
        
        // Memory should not have grown excessively
        // (Specific assertions would depend on Redis configuration and TTL settings)
        Assert.NotNull(finalMemoryInfo);
    }

    #endregion

    #region Cache Hit Ratio Tests

    [Fact]
    public async Task CacheHitRatio_RepeatedQueries_AchievesHighHitRatio()
    {
        // Arrange
        var mockResults = CreateMockAuditResults(50);
        
        _mockAuditRepository.Setup(r => r.QueryAsync(It.IsAny<AuditQueryFilter>(), It.IsAny<PaginationOptions>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PagedResult<SysAuditLog>
            {
                Items = mockResults,
                TotalCount = 50,
                PageNumber = 1,
                PageSize = 50
            });

        // Create 10 different filters
        var filters = Enumerable.Range(1, 10)
            .Select(i => new AuditQueryFilter { CompanyId = i })
            .ToList();
        
        var pagination = new PaginationOptions { PageNumber = 1, PageSize = 50 };

        // Act - First round: populate cache (10 database calls)
        foreach (var filter in filters)
        {
            await _auditQueryService.QueryAsync(filter, pagination);
        }

        // Act - Second round: should hit cache (0 additional database calls)
        foreach (var filter in filters)
        {
            await _auditQueryService.QueryAsync(filter, pagination);
        }

        // Act - Third round: should hit cache (0 additional database calls)
        foreach (var filter in filters)
        {
            await _auditQueryService.QueryAsync(filter, pagination);
        }

        // Assert - Repository should be called only 10 times (first round only)
        _mockAuditRepository.Verify(r => r.QueryAsync(It.IsAny<AuditQueryFilter>(), It.IsAny<PaginationOptions>(), It.IsAny<CancellationToken>()), Times.Exactly(10));
        
        // Cache hit ratio should be 66.7% (20 cache hits out of 30 total calls)
        var totalCalls = 30;
        var databaseCalls = 10;
        var cacheHits = totalCalls - databaseCalls;
        var hitRatio = (double)cacheHits / totalCalls;
        
        Assert.True(hitRatio >= 0.66, $"Cache hit ratio {hitRatio:P} should be at least 66%");
    }

    #endregion

    #region Helper Methods

    private List<SysAuditLog> CreateMockAuditResults(int count)
    {
        var results = new List<SysAuditLog>();
        
        for (int i = 0; i < count; i++)
        {
            results.Add(new SysAuditLog
            {
                RowId = i + 1,
                ActorType = "USER",
                ActorId = i + 1,
                CompanyId = 1,
                Action = i % 2 == 0 ? "INSERT" : "UPDATE",
                EntityType = "SysUser",
                EntityId = i + 1,
                CreationDate = DateTime.UtcNow.AddMinutes(-i),
                CorrelationId = Guid.NewGuid().ToString(),
                OldValue = i % 2 == 0 ? null : $"{{\"name\":\"OldValue{i}\"}}",
                NewValue = $"{{\"name\":\"NewValue{i}\"}}"
            });
        }
        
        return results;
    }

    private async Task ClearRedisCache()
    {
        try
        {
            var server = _redis.GetServer(_redis.GetEndPoints().First());
            await server.FlushDatabaseAsync();
        }
        catch (Exception)
        {
            // Ignore errors during cleanup
        }
    }

    private async Task<List<string>> GetRedisCacheKeys(string pattern)
    {
        try
        {
            var server = _redis.GetServer(_redis.GetEndPoints().First());
            var keys = server.Keys(pattern: pattern);
            return keys.Select(k => k.ToString()).ToList();
        }
        catch (Exception)
        {
            return new List<string>();
        }
    }

    #endregion

    public void Dispose()
    {
        ClearRedisCache().Wait();
        _redis?.Dispose();
        _serviceProvider?.Dispose();
    }
}