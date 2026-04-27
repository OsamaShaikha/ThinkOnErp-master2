using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System.Threading.Channels;
using ThinkOnErp.Domain.Interfaces;
using ThinkOnErp.Infrastructure.Data;
using ThinkOnErp.Infrastructure.Repositories;
using ThinkOnErp.Infrastructure.Services;
using ThinkOnErp.Infrastructure.Resilience;
using ThinkOnErp.Infrastructure.Configuration;
using ThinkOnErp.Infrastructure.Configuration.Validation;

namespace ThinkOnErp.Infrastructure;

/// <summary>
/// Extension methods for registering Infrastructure layer services.
/// Configures database context, repositories, and infrastructure services.
/// </summary>
public static class DependencyInjection
{
    /// <summary>
    /// Registers Infrastructure layer services including database context, repositories, and services.
    /// </summary>
    /// <param name="services">The service collection</param>
    /// <param name="configuration">The configuration</param>
    /// <returns>The service collection for chaining</returns>
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        // Register all configuration options with data annotation validation
        // This validates configuration on application startup and throws if invalid
        services.AddTraceabilityConfigurationValidation(configuration);

        // Configure Redis distributed cache if enabled for security monitoring OR audit query caching
        var securityOptions = new SecurityMonitoringOptions();
        configuration.GetSection(SecurityMonitoringOptions.SectionName).Bind(securityOptions);
        
        var auditCachingOptions = new AuditQueryCachingOptions();
        configuration.GetSection(AuditQueryCachingOptions.SectionName).Bind(auditCachingOptions);
        
        // Register Redis if either security monitoring or audit caching needs it
        var needsRedis = (securityOptions.UseRedisCache && !string.IsNullOrWhiteSpace(securityOptions.RedisConnectionString)) ||
                        (auditCachingOptions.Enabled && !string.IsNullOrWhiteSpace(auditCachingOptions.RedisConnectionString));
        
        if (needsRedis)
        {
            // Use the first available connection string (prefer audit caching if both are configured)
            var redisConnectionString = auditCachingOptions.Enabled && !string.IsNullOrWhiteSpace(auditCachingOptions.RedisConnectionString)
                ? auditCachingOptions.RedisConnectionString
                : securityOptions.RedisConnectionString;
                
            services.AddStackExchangeRedisCache(options =>
            {
                options.Configuration = redisConnectionString;
                options.InstanceName = "ThinkOnErp:";
            });
        }

        // Register OracleDbContext as Scoped
        services.AddScoped<OracleDbContext>();

        // Register audit command interceptor for database operation auditing
        services.AddScoped<AuditCommandInterceptor>();

        // Register resilience services as Singleton
        services.AddSingleton<CircuitBreakerRegistry>(sp =>
        {
            var loggerFactory = sp.GetRequiredService<ILoggerFactory>();
            var configuration = sp.GetRequiredService<IConfiguration>();
            
            // Get audit logging options for circuit breaker configuration
            var auditOptions = new AuditLoggingOptions();
            configuration.GetSection(AuditLoggingOptions.SectionName).Bind(auditOptions);
            
            return new CircuitBreakerRegistry(
                loggerFactory,
                auditOptions.CircuitBreakerFailureThreshold,
                TimeSpan.FromSeconds(auditOptions.CircuitBreakerTimeoutSeconds));
        });
        
        // Register RetryPolicy with configuration from AuditLoggingOptions
        services.AddScoped<RetryPolicy>(sp =>
        {
            var logger = sp.GetRequiredService<ILogger<RetryPolicy>>();
            var configuration = sp.GetRequiredService<IConfiguration>();
            
            // Get audit logging options for retry policy configuration
            var auditOptions = new AuditLoggingOptions();
            configuration.GetSection(AuditLoggingOptions.SectionName).Bind(auditOptions);
            
            return RetryPolicy.FromOptions(logger, auditOptions);
        });
        
        services.AddScoped<CircuitBreaker>(sp =>
        {
            var loggerFactory = sp.GetRequiredService<ILoggerFactory>();
            var logger = loggerFactory.CreateLogger<CircuitBreaker>();
            return new CircuitBreaker(logger);
        });
        services.AddScoped<ResilientDatabaseExecutor>();

        // Register all repositories as Scoped
        services.AddScoped<IRoleRepository, RoleRepository>();
        services.AddScoped<ICurrencyRepository, CurrencyRepository>();
        services.AddScoped<ICompanyRepository, CompanyRepository>();
        services.AddScoped<IBranchRepository, BranchRepository>();
        services.AddScoped<IUserRepository, UserRepository>();
        services.AddScoped<IAuthRepository, AuthRepository>();
        services.AddScoped<IFiscalYearRepository, FiscalYearRepository>();
        
        // Register permission system repositories
        services.AddScoped<ISuperAdminRepository, SuperAdminRepository>();
        services.AddScoped<ISystemRepository, SystemRepository>();
        services.AddScoped<IScreenRepository, ScreenRepository>();
        services.AddScoped<IPermissionRepository, PermissionRepository>();
        
        // Register ticket system repositories
        services.AddScoped<ITicketRepository, TicketRepository>();
        services.AddScoped<ITicketTypeRepository, TicketTypeRepository>();
        services.AddScoped<ITicketPriorityRepository, TicketPriorityRepository>();
        services.AddScoped<ITicketStatusRepository, TicketStatusRepository>();
        services.AddScoped<ITicketCommentRepository, TicketCommentRepository>();
        services.AddScoped<ITicketAttachmentRepository, TicketAttachmentRepository>();
        services.AddScoped<ISavedSearchRepository, SavedSearchRepository>();
        services.AddScoped<ISearchAnalyticsRepository, SearchAnalyticsRepository>();
        services.AddScoped<ITicketConfigRepository, TicketConfigRepository>();

        // Register infrastructure services as Scoped
        services.AddScoped<PasswordHashingService>();
        services.AddScoped<JwtTokenService>();
        services.AddScoped<ITicketNotificationService, TicketNotificationService>();
        services.AddScoped<IAttachmentService, AttachmentService>();
        services.AddScoped<ISlaCalculationService, SlaCalculationService>();
        services.AddScoped<ISlaEscalationService, SlaEscalationService>();
        services.AddScoped<IAuditTrailService, AuditTrailService>();
        services.AddScoped<ILegacyAuditService, LegacyAuditService>();

        // Register audit logging services
        services.AddScoped<IAuditRepository, AuditRepository>();
        services.AddScoped<ISensitiveDataMasker, SensitiveDataMasker>();
        services.AddSingleton<IAuditDataEncryption, AuditDataEncryption>();
        services.AddScoped<IAuditLogIntegrityService, AuditLogIntegrityService>();
        services.AddScoped<IAuditQueryService, AuditQueryService>();
        services.AddScoped<IAuditContextProvider, AuditContextProvider>();
        services.AddScoped<IExceptionCategorizationService, ExceptionCategorizationService>();
        
        // Register compliance reporting services
        services.AddScoped<IComplianceReporter, ComplianceReporter>();

        // Register performance monitoring services
        services.AddScoped<ISlowQueryRepository, SlowQueryRepository>();
        services.AddSingleton<IPerformanceMonitor, PerformanceMonitor>();
        
        // Register memory monitoring services
        services.AddSingleton<IMemoryMonitor, MemoryMonitor>();

        // Register security monitoring services
        services.AddScoped<ISecurityMonitor, SecurityMonitor>();

        // Register alert management services
        services.AddHttpClient("WebhookClient")
            .ConfigureHttpClient(client =>
            {
                client.DefaultRequestHeaders.Add("User-Agent", "ThinkOnErp-AlertManager/1.0");
            });
        
        // Register shared channel for alert notifications
        services.AddSingleton(provider =>
        {
            var channelOptions = new BoundedChannelOptions(1000)
            {
                FullMode = BoundedChannelFullMode.DropOldest, // Drop oldest notifications if queue is full
                SingleReader = false, // Multiple background workers can process notifications
                SingleWriter = false // Multiple threads can queue notifications
            };
            return Channel.CreateBounded<AlertNotificationTask>(channelOptions);
        });
        
        services.AddSingleton<IEmailNotificationChannel, EmailNotificationService>();
        services.AddSingleton<IWebhookNotificationChannel, WebhookNotificationService>();
        services.AddSingleton<ISmsNotificationChannel, SmsNotificationService>();
        services.AddSingleton<IAlertManager, AlertManager>();

        // Register archival services
        services.AddScoped<ICompressionService, CompressionService>();
        services.AddSingleton<IExternalStorageProviderFactory, ExternalStorageProviderFactory>();
        services.AddScoped<IArchivalService, ArchivalService>();

        // Register multi-tenant access control services
        services.AddScoped<IMultiTenantAccessService, MultiTenantAccessService>();

        // Register key management services
        services.AddSingleton<KeyManagementService>();
        services.AddSingleton<IKeyManagementService>(sp => sp.GetRequiredService<KeyManagementService>());
        services.AddScoped<KeyManagementCli>();

        // Register background services as Hosted Services
        services.AddHostedService<SlaEscalationBackgroundService>();
        
        // Register AuditLogger as both hosted service and IAuditLogger interface
        services.AddSingleton<AuditLogger>();
        services.AddSingleton<IAuditLogger>(provider => provider.GetRequiredService<AuditLogger>());
        services.AddHostedService<AuditLogger>(provider => provider.GetRequiredService<AuditLogger>());
        
        services.AddHostedService<MetricsAggregationBackgroundService>();
        services.AddHostedService<AlertProcessingBackgroundService>();
        services.AddHostedService<ScheduledReportGenerationService>();
        services.AddHostedService<ArchivalBackgroundService>();
        services.AddHostedService<KeyRotationBackgroundService>();

        return services;
    }

    /// <summary>
    /// Registers all traceability system services with appropriate lifetimes.
    /// This includes audit logging, monitoring, compliance, archival, and alert services.
    /// </summary>
    /// <param name="services">The service collection</param>
    /// <param name="configuration">The configuration</param>
    /// <returns>The service collection for chaining</returns>
    public static IServiceCollection AddTraceabilitySystem(this IServiceCollection services, IConfiguration configuration)
    {
        // ===== Audit Logging Services =====
        // Core audit logging with async queue processing
        services.AddSingleton<AuditLogger>();
        services.AddSingleton<IAuditLogger>(provider => provider.GetRequiredService<AuditLogger>());
        services.AddHostedService<AuditLogger>(provider => provider.GetRequiredService<AuditLogger>());
        
        // Audit repository for database operations
        services.AddScoped<IAuditRepository, AuditRepository>();
        
        // Legacy audit service for backward compatibility
        services.AddScoped<ILegacyAuditService, LegacyAuditService>();
        
        // Audit trail service for compliance tracking
        services.AddScoped<IAuditTrailService, AuditTrailService>();
        
        // ===== Monitoring Services =====
        // Performance monitoring (Singleton for in-memory metrics aggregation)
        services.AddSingleton<IPerformanceMonitor, PerformanceMonitor>();
        services.AddScoped<ISlowQueryRepository, SlowQueryRepository>();
        
        // Memory monitoring (Singleton for system-wide tracking)
        services.AddSingleton<IMemoryMonitor, MemoryMonitor>();
        
        // Security monitoring (Scoped for request-specific threat detection)
        services.AddScoped<ISecurityMonitor, SecurityMonitor>();
        
        // ===== Repository Services =====
        // Already covered by IAuditRepository above
        
        // ===== Compliance Services =====
        // Compliance reporting for GDPR, SOX, ISO 27001
        services.AddScoped<IComplianceReporter, ComplianceReporter>();
        
        // ===== Query Services =====
        // Audit query service for efficient audit log querying
        services.AddScoped<IAuditQueryService, AuditQueryService>();
        
        // ===== Archival Services =====
        // Archival service for data retention and cold storage
        services.AddScoped<IArchivalService, ArchivalService>();
        services.AddScoped<ICompressionService, CompressionService>();
        services.AddSingleton<IExternalStorageProviderFactory, ExternalStorageProviderFactory>();
        
        // ===== Alert Services =====
        // Alert manager for critical event notifications
        services.AddSingleton<IAlertManager, AlertManager>();
        
        // Notification channels
        services.AddSingleton<IEmailNotificationChannel, EmailNotificationService>();
        services.AddSingleton<IWebhookNotificationChannel, WebhookNotificationService>();
        services.AddSingleton<ISmsNotificationChannel, SmsNotificationService>();
        
        // Shared channel for alert notifications
        services.AddSingleton(provider =>
        {
            var channelOptions = new BoundedChannelOptions(1000)
            {
                FullMode = BoundedChannelFullMode.DropOldest,
                SingleReader = false,
                SingleWriter = false
            };
            return Channel.CreateBounded<AlertNotificationTask>(channelOptions);
        });
        
        // HTTP client for webhook notifications
        services.AddHttpClient("WebhookClient")
            .ConfigureHttpClient(client =>
            {
                client.DefaultRequestHeaders.Add("User-Agent", "ThinkOnErp-AlertManager/1.0");
            });
        
        // ===== Helper Services =====
        // Sensitive data masking
        services.AddScoped<ISensitiveDataMasker, SensitiveDataMasker>();
        
        // Correlation context for request tracking (uses AsyncLocal, no registration needed)
        // CorrelationContext is a static class with AsyncLocal storage
        
        // Audit context provider for capturing request context
        services.AddScoped<IAuditContextProvider, AuditContextProvider>();
        
        // Exception categorization for severity classification
        services.AddScoped<IExceptionCategorizationService, ExceptionCategorizationService>();
        
        // Multi-tenant access control
        services.AddScoped<IMultiTenantAccessService, MultiTenantAccessService>();
        
        // ===== Security Services =====
        // Audit data encryption for sensitive data
        services.AddSingleton<IAuditDataEncryption, AuditDataEncryption>();
        
        // Audit log integrity service for tamper detection
        services.AddScoped<IAuditLogIntegrityService, AuditLogIntegrityService>();
        
        // Key management for encryption and signing keys
        services.AddSingleton<KeyManagementService>();
        services.AddSingleton<IKeyManagementService>(sp => sp.GetRequiredService<KeyManagementService>());
        services.AddScoped<KeyManagementCli>();
        
        // ===== Background Services =====
        // Metrics aggregation (hourly rollups)
        services.AddHostedService<MetricsAggregationBackgroundService>();
        
        // Alert processing (async notification delivery)
        services.AddHostedService<AlertProcessingBackgroundService>();
        
        // Connection pool monitoring (database connection pool exhaustion alerts)
        services.AddHostedService<ConnectionPoolMonitoringService>();
        
        // Scheduled report generation
        services.AddHostedService<ScheduledReportGenerationService>();
        
        // Archival background service (data retention)
        services.AddHostedService<ArchivalBackgroundService>();
        
        // Key rotation background service
        services.AddHostedService<KeyRotationBackgroundService>();
        
        // ===== Resilience Services =====
        // Circuit breaker registry for fault tolerance
        services.AddSingleton<CircuitBreakerRegistry>(sp =>
        {
            var loggerFactory = sp.GetRequiredService<ILoggerFactory>();
            var config = sp.GetRequiredService<IConfiguration>();
            
            var auditOptions = new AuditLoggingOptions();
            config.GetSection(AuditLoggingOptions.SectionName).Bind(auditOptions);
            
            return new CircuitBreakerRegistry(
                loggerFactory,
                auditOptions.CircuitBreakerFailureThreshold,
                TimeSpan.FromSeconds(auditOptions.CircuitBreakerTimeoutSeconds));
        });
        
        // Retry policy for transient failures
        services.AddScoped<RetryPolicy>(sp =>
        {
            var logger = sp.GetRequiredService<ILogger<RetryPolicy>>();
            var config = sp.GetRequiredService<IConfiguration>();
            
            var auditOptions = new AuditLoggingOptions();
            config.GetSection(AuditLoggingOptions.SectionName).Bind(auditOptions);
            
            return RetryPolicy.FromOptions(logger, auditOptions);
        });
        
        services.AddScoped<CircuitBreaker>(sp =>
        {
            var loggerFactory = sp.GetRequiredService<ILoggerFactory>();
            var logger = loggerFactory.CreateLogger<CircuitBreaker>();
            return new CircuitBreaker(logger);
        });
        
        services.AddScoped<ResilientDatabaseExecutor>();
        
        // Audit command interceptor for database operation auditing
        services.AddScoped<AuditCommandInterceptor>();
        
        // ===== Configuration Validation =====
        // Register all configuration options with data annotation validation
        services.AddTraceabilityConfigurationValidation(configuration);
        
        // ===== Redis Cache Configuration =====
        // Configure Redis distributed cache if enabled for security monitoring OR audit query caching
        var securityOptions = new SecurityMonitoringOptions();
        configuration.GetSection(SecurityMonitoringOptions.SectionName).Bind(securityOptions);
        
        var auditCachingOptions = new AuditQueryCachingOptions();
        configuration.GetSection(AuditQueryCachingOptions.SectionName).Bind(auditCachingOptions);
        
        var needsRedis = (securityOptions.UseRedisCache && !string.IsNullOrWhiteSpace(securityOptions.RedisConnectionString)) ||
                        (auditCachingOptions.Enabled && !string.IsNullOrWhiteSpace(auditCachingOptions.RedisConnectionString));
        
        if (needsRedis)
        {
            var redisConnectionString = auditCachingOptions.Enabled && !string.IsNullOrWhiteSpace(auditCachingOptions.RedisConnectionString)
                ? auditCachingOptions.RedisConnectionString
                : securityOptions.RedisConnectionString;
                
            services.AddStackExchangeRedisCache(options =>
            {
                options.Configuration = redisConnectionString;
                options.InstanceName = "ThinkOnErp:";
            });
        }
        
        return services;
    }
}
