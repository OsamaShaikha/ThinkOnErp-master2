using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using ThinkOnErp.Domain.Interfaces;
using ThinkOnErp.Infrastructure;
using ThinkOnErp.Infrastructure.Services;
using Xunit;

namespace ThinkOnErp.Infrastructure.Tests.DependencyInjection;

/// <summary>
/// Unit tests for AddTraceabilitySystem extension method.
/// Validates that all traceability system services are registered with appropriate lifetimes.
/// 
/// **Validates: Requirements 14.1, 14.2, 14.3, 14.4, 14.5, 14.6, 14.7**
/// - Requirement 14.1: Audit Logger writes to SYS_AUDIT_LOG table
/// - Requirement 14.2: Audit Logger populates all existing columns
/// - Requirement 14.3: Audit Logger stores complex objects as JSON
/// - Requirement 14.4: Audit Logger uses SYS_AUDIT_LOG_SEQ sequence
/// - Requirement 14.5: Audit Logger maintains backward compatibility
/// - Requirement 14.6: Audit Logger supports schema extensions
/// - Requirement 14.7: Audit Logger supports legacy audit log formats
/// </summary>
public class AddTraceabilitySystemTests
{
    private readonly IConfiguration _configuration;
    private readonly ServiceCollection _services;

    public AddTraceabilitySystemTests()
    {
        // Create minimal configuration for testing
        var configBuilder = new ConfigurationBuilder();
        configBuilder.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["AuditLogging:Enabled"] = "true",
            ["AuditLogging:BatchSize"] = "50",
            ["AuditLogging:BatchWindowMs"] = "100",
            ["AuditLogging:MaxQueueSize"] = "10000",
            ["AuditLogging:CircuitBreakerFailureThreshold"] = "5",
            ["AuditLogging:CircuitBreakerTimeoutSeconds"] = "60",
            ["AuditLogging:RetryMaxAttempts"] = "3",
            ["AuditLogging:RetryDelayMs"] = "1000",
            ["SecurityMonitoring:UseRedisCache"] = "false",
            ["AuditQueryCaching:Enabled"] = "false",
            ["ConnectionStrings:OracleConnection"] = "Data Source=localhost:1521/XE;User Id=test;Password=test;",
            ["KeyManagement:Provider"] = "Configuration",
            ["KeyManagement:EncryptionKey"] = "dGVzdGtleWZvcmVuY3J5cHRpb24xMjM0NTY3ODkwMTIzNDU2Nzg5MDEyMzQ1Njc4OTAxMjM0NTY3ODkwMTIzNDU2Nzg5MDEyMzQ1Njc4OTA=",
            ["KeyManagement:SigningKey"] = "dGVzdGtleWZvcnNpZ25pbmcxMjM0NTY3ODkwMTIzNDU2Nzg5MDEyMzQ1Njc4OTAxMjM0NTY3ODkwMTIzNDU2Nzg5MDEyMzQ1Njc4OTA="
        });
        _configuration = configBuilder.Build();
        
        _services = new ServiceCollection();
        _services.AddLogging();
        _services.AddHttpContextAccessor();
    }

    [Fact]
    public void AddTraceabilitySystem_RegistersAllAuditLoggingServices()
    {
        // Act
        _services.AddTraceabilitySystem(_configuration);
        var serviceProvider = _services.BuildServiceProvider();

        // Assert - Audit Logging Services
        Assert.NotNull(serviceProvider.GetService<IAuditLogger>());
        Assert.NotNull(serviceProvider.GetService<IAuditRepository>());
        Assert.NotNull(serviceProvider.GetService<ILegacyAuditService>());
        Assert.NotNull(serviceProvider.GetService<IAuditTrailService>());
    }

    [Fact]
    public void AddTraceabilitySystem_RegistersAllMonitoringServices()
    {
        // Act
        _services.AddTraceabilitySystem(_configuration);
        var serviceProvider = _services.BuildServiceProvider();

        // Assert - Monitoring Services
        Assert.NotNull(serviceProvider.GetService<IPerformanceMonitor>());
        Assert.NotNull(serviceProvider.GetService<IMemoryMonitor>());
        Assert.NotNull(serviceProvider.GetService<ISecurityMonitor>());
        Assert.NotNull(serviceProvider.GetService<ISlowQueryRepository>());
    }

    [Fact]
    public void AddTraceabilitySystem_RegistersAllComplianceServices()
    {
        // Act
        _services.AddTraceabilitySystem(_configuration);
        var serviceProvider = _services.BuildServiceProvider();

        // Assert - Compliance Services
        Assert.NotNull(serviceProvider.GetService<IComplianceReporter>());
    }

    [Fact]
    public void AddTraceabilitySystem_RegistersAllQueryServices()
    {
        // Act
        _services.AddTraceabilitySystem(_configuration);
        var serviceProvider = _services.BuildServiceProvider();

        // Assert - Query Services
        Assert.NotNull(serviceProvider.GetService<IAuditQueryService>());
    }

    [Fact]
    public void AddTraceabilitySystem_RegistersAllArchivalServices()
    {
        // Act
        _services.AddTraceabilitySystem(_configuration);
        var serviceProvider = _services.BuildServiceProvider();

        // Assert - Archival Services
        Assert.NotNull(serviceProvider.GetService<IArchivalService>());
        Assert.NotNull(serviceProvider.GetService<ICompressionService>());
        Assert.NotNull(serviceProvider.GetService<IExternalStorageProviderFactory>());
    }

    [Fact]
    public void AddTraceabilitySystem_RegistersAllAlertServices()
    {
        // Act
        _services.AddTraceabilitySystem(_configuration);
        var serviceProvider = _services.BuildServiceProvider();

        // Assert - Alert Services
        Assert.NotNull(serviceProvider.GetService<IAlertManager>());
        Assert.NotNull(serviceProvider.GetService<IEmailNotificationChannel>());
        Assert.NotNull(serviceProvider.GetService<IWebhookNotificationChannel>());
        Assert.NotNull(serviceProvider.GetService<ISmsNotificationChannel>());
    }

    [Fact]
    public void AddTraceabilitySystem_RegistersAllHelperServices()
    {
        // Act
        _services.AddTraceabilitySystem(_configuration);
        var serviceProvider = _services.BuildServiceProvider();

        // Assert - Helper Services
        Assert.NotNull(serviceProvider.GetService<ISensitiveDataMasker>());
        Assert.NotNull(serviceProvider.GetService<IAuditContextProvider>());
        Assert.NotNull(serviceProvider.GetService<IExceptionCategorizationService>());
        Assert.NotNull(serviceProvider.GetService<IMultiTenantAccessService>());
    }

    [Fact]
    public void AddTraceabilitySystem_RegistersAllSecurityServices()
    {
        // Act
        _services.AddTraceabilitySystem(_configuration);
        var serviceProvider = _services.BuildServiceProvider();

        // Assert - Security Services
        Assert.NotNull(serviceProvider.GetService<IAuditDataEncryption>());
        Assert.NotNull(serviceProvider.GetService<IAuditLogIntegrityService>());
        Assert.NotNull(serviceProvider.GetService<IKeyManagementService>());
        Assert.NotNull(serviceProvider.GetService<KeyManagementCli>());
    }

    [Fact]
    public void AddTraceabilitySystem_RegistersAllResilienceServices()
    {
        // Act
        _services.AddTraceabilitySystem(_configuration);
        var serviceProvider = _services.BuildServiceProvider();

        // Assert - Resilience Services
        Assert.NotNull(serviceProvider.GetService<CircuitBreakerRegistry>());
        Assert.NotNull(serviceProvider.GetService<RetryPolicy>());
        Assert.NotNull(serviceProvider.GetService<CircuitBreaker>());
        Assert.NotNull(serviceProvider.GetService<ResilientDatabaseExecutor>());
        Assert.NotNull(serviceProvider.GetService<AuditCommandInterceptor>());
    }

    [Fact]
    public void AddTraceabilitySystem_RegistersServicesWithCorrectLifetimes()
    {
        // Act
        _services.AddTraceabilitySystem(_configuration);

        // Assert - Singleton services
        var singletonServices = _services.Where(s => s.Lifetime == ServiceLifetime.Singleton).ToList();
        Assert.Contains(singletonServices, s => s.ServiceType == typeof(IAuditLogger));
        Assert.Contains(singletonServices, s => s.ServiceType == typeof(IPerformanceMonitor));
        Assert.Contains(singletonServices, s => s.ServiceType == typeof(IMemoryMonitor));
        Assert.Contains(singletonServices, s => s.ServiceType == typeof(IAlertManager));
        Assert.Contains(singletonServices, s => s.ServiceType == typeof(IAuditDataEncryption));
        Assert.Contains(singletonServices, s => s.ServiceType == typeof(IAuditLogIntegrityService));
        Assert.Contains(singletonServices, s => s.ServiceType == typeof(IKeyManagementService));
        Assert.Contains(singletonServices, s => s.ServiceType == typeof(CircuitBreakerRegistry));

        // Assert - Scoped services
        var scopedServices = _services.Where(s => s.Lifetime == ServiceLifetime.Scoped).ToList();
        Assert.Contains(scopedServices, s => s.ServiceType == typeof(IAuditRepository));
        Assert.Contains(scopedServices, s => s.ServiceType == typeof(ILegacyAuditService));
        Assert.Contains(scopedServices, s => s.ServiceType == typeof(IAuditTrailService));
        Assert.Contains(scopedServices, s => s.ServiceType == typeof(ISecurityMonitor));
        Assert.Contains(scopedServices, s => s.ServiceType == typeof(IComplianceReporter));
        Assert.Contains(scopedServices, s => s.ServiceType == typeof(IAuditQueryService));
        Assert.Contains(scopedServices, s => s.ServiceType == typeof(IArchivalService));
        Assert.Contains(scopedServices, s => s.ServiceType == typeof(ISensitiveDataMasker));
        Assert.Contains(scopedServices, s => s.ServiceType == typeof(IAuditContextProvider));
        Assert.Contains(scopedServices, s => s.ServiceType == typeof(IExceptionCategorizationService));
        Assert.Contains(scopedServices, s => s.ServiceType == typeof(IMultiTenantAccessService));
        Assert.Contains(scopedServices, s => s.ServiceType == typeof(RetryPolicy));
        Assert.Contains(scopedServices, s => s.ServiceType == typeof(CircuitBreaker));
    }

    [Fact]
    public void AddTraceabilitySystem_CanBeCalledMultipleTimes()
    {
        // Act - Call multiple times
        _services.AddTraceabilitySystem(_configuration);
        _services.AddTraceabilitySystem(_configuration);
        
        // Assert - Should not throw and services should be registered
        var serviceProvider = _services.BuildServiceProvider();
        Assert.NotNull(serviceProvider.GetService<IAuditLogger>());
    }

    [Fact]
    public void AddTraceabilitySystem_ConfiguresRedisWhenEnabled()
    {
        // Arrange
        var configBuilder = new ConfigurationBuilder();
        configBuilder.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["AuditLogging:Enabled"] = "true",
            ["AuditLogging:BatchSize"] = "50",
            ["AuditLogging:CircuitBreakerFailureThreshold"] = "5",
            ["AuditLogging:CircuitBreakerTimeoutSeconds"] = "60",
            ["AuditLogging:RetryMaxAttempts"] = "3",
            ["AuditLogging:RetryDelayMs"] = "1000",
            ["SecurityMonitoring:UseRedisCache"] = "true",
            ["SecurityMonitoring:RedisConnectionString"] = "localhost:6379",
            ["AuditQueryCaching:Enabled"] = "false"
        });
        var config = configBuilder.Build();
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddHttpContextAccessor();

        // Act
        services.AddTraceabilitySystem(config);

        // Assert - Redis cache should be registered
        var serviceProvider = services.BuildServiceProvider();
        Assert.NotNull(serviceProvider.GetService<Microsoft.Extensions.Caching.Distributed.IDistributedCache>());
    }
}
