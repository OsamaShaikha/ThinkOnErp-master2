using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Logging;
using System.Threading.Channels;
using ThinkOnErp.Domain.Interfaces;
using ThinkOnErp.Infrastructure.Data;
using ThinkOnErp.Infrastructure.Interceptors;
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

        // Register OracleDbContext as Scoped (legacy ADO.NET)
        services.AddScoped<OracleDbContext>();

        // Register EF Core DbContext with Oracle provider
        var isDevelopment = configuration.GetValue<bool>("IsDevelopment", false);
        services.AddDbContext<ThinkOnErpDbContext>((serviceProvider, options) =>
        {
            var connectionString = configuration.GetConnectionString("OracleDb");
            if (string.IsNullOrWhiteSpace(connectionString))
            {
                throw new InvalidOperationException("Oracle connection string 'OracleDb' is not configured.");
            }

            options.UseOracle(connectionString, oracleOptions =>
            {
                // Configure Oracle-specific options
                // oracleOptions.UseOracleSQLCompatibility("11"); // Commented out due to type mismatch - needs investigation
                oracleOptions.CommandTimeout(30);
                
                // Performance Optimization: Enable connection pooling
                // Oracle connection pooling is enabled by default in Oracle.EntityFrameworkCore
                // Pool size is controlled by connection string parameters:
                // - Min Pool Size: Minimum number of connections in the pool (default: 1)
                // - Max Pool Size: Maximum number of connections in the pool (default: 100)
                // - Connection Lifetime: Maximum lifetime of a connection in seconds (default: 0 = no limit)
                // - Incr Pool Size: Number of connections to add when pool is exhausted (default: 5)
                // - Decr Pool Size: Number of connections to remove when pool is idle (default: 1)
                // These are configured in the connection string in appsettings.json
                
                // Performance Optimization: Configure query splitting strategy
                // UseQuerySplittingBehavior.SplitQuery prevents cartesian explosion in joins
                // by splitting queries with multiple Include() into separate SQL queries
                oracleOptions.UseQuerySplittingBehavior(QuerySplittingBehavior.SplitQuery);
            })
            .EnableSensitiveDataLogging(isDevelopment)
            .EnableDetailedErrors(isDevelopment);

            // Register EF Core audit interceptor
            var efCoreAuditInterceptor = serviceProvider.GetService<EfCoreAuditInterceptor>();
            if (efCoreAuditInterceptor != null)
            {
                options.AddInterceptors(efCoreAuditInterceptor);
            }

            // Register EF Core performance interceptor for monitoring and observability (REQ-21)
            var efCorePerformanceInterceptor = serviceProvider.GetService<EfCorePerformanceInterceptor>();
            if (efCorePerformanceInterceptor != null)
            {
                options.AddInterceptors(efCorePerformanceInterceptor);
            }
        }, ServiceLifetime.Scoped);

        // Register audit command interceptor for database operation auditing (legacy ADO.NET)
        services.AddScoped<AuditCommandInterceptor>();
        
        // Register EF Core audit interceptor for EF Core operations
        services.AddScoped<EfCoreAuditInterceptor>();

        // Register EF Core performance interceptor for monitoring and observability (REQ-21)
        services.AddScoped<EfCorePerformanceInterceptor>(sp =>
        {
            var logger = sp.GetRequiredService<ILogger<EfCorePerformanceInterceptor>>();
            var slowQueryRepository = sp.GetService<ISlowQueryRepository>();
            
            // Get performance monitoring options from configuration
            var perfOptions = new PerformanceMonitoringOptions();
            configuration.GetSection(PerformanceMonitoringOptions.SectionName).Bind(perfOptions);
            
            // Get EF Core logging options from configuration
            var efCoreLoggingEnabled = configuration.GetValue<bool>("EfCore:Logging:Enabled", true);
            var logSqlQueries = configuration.GetValue<bool>("EfCore:Logging:LogSqlQueries", true);
            var logQueryExecutionTime = configuration.GetValue<bool>("EfCore:Logging:LogQueryExecutionTime", true);
            var slowQueryThresholdMs = configuration.GetValue<int>("EfCore:Logging:SlowQueryThresholdMs", 500);
            
            return new EfCorePerformanceInterceptor(
                logger,
                slowQueryRepository,
                slowQueryThresholdMs,
                logSqlQueries && efCoreLoggingEnabled,
                logQueryExecutionTime && efCoreLoggingEnabled);
        });

        // Register EF Core connection pool monitor for monitoring connection pool usage (REQ-21)
        services.AddScoped<EfCoreConnectionPoolMonitor>();

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
        // Feature flag support for gradual migration (UseEfCore:RepositoryName)
        // When UseEfCore:CurrencyRepository is true, use EF Core implementation
        // Otherwise, use legacy ADO.NET implementation
        
        // RoleRepository - Feature flag: UseEfCore:RoleRepository
        if (configuration.GetValue<bool>("UseEfCore:RoleRepository", false))
        {
            services.AddScoped<IRoleRepository, ThinkOnErp.Infrastructure.Repositories.EfCore.RoleRepository>();
        }
        else
        {
            services.AddScoped<IRoleRepository, RoleRepository>();
        }
        
        // Pilot repositories with EF Core migration support
        // CurrencyRepository - Feature flag: UseEfCore:CurrencyRepository
        if (configuration.GetValue<bool>("UseEfCore:CurrencyRepository", false))
        {
            services.AddScoped<ICurrencyRepository, ThinkOnErp.Infrastructure.Repositories.EfCore.CurrencyRepository>();
        }
        else
        {
            services.AddScoped<ICurrencyRepository, CurrencyRepository>();
        }
        
        // CompanyRepository - Feature flag: UseEfCore:CompanyRepository
        if (configuration.GetValue<bool>("UseEfCore:CompanyRepository", false))
        {
            services.AddScoped<ICompanyRepository, ThinkOnErp.Infrastructure.Repositories.EfCore.CompanyRepository>();
        }
        else
        {
            services.AddScoped<ICompanyRepository, CompanyRepository>();
        }
        
        // BranchRepository - Feature flag: UseEfCore:BranchRepository
        if (configuration.GetValue<bool>("UseEfCore:BranchRepository", false))
        {
            services.AddScoped<IBranchRepository, ThinkOnErp.Infrastructure.Repositories.EfCore.BranchRepository>();
        }
        else
        {
            services.AddScoped<IBranchRepository, BranchRepository>();
        }
        
        // UserRepository - Feature flag: UseEfCore:UserRepository
        if (configuration.GetValue<bool>("UseEfCore:UserRepository", false))
        {
            services.AddScoped<IUserRepository, ThinkOnErp.Infrastructure.Repositories.EfCore.UserRepository>();
        }
        else
        {
            services.AddScoped<IUserRepository, UserRepository>();
        }
        
        services.AddScoped<IAuthRepository, AuthRepository>();
        
        // FiscalYearRepository - Feature flag: UseEfCore:FiscalYearRepository
        if (configuration.GetValue<bool>("UseEfCore:FiscalYearRepository", false))
        {
            services.AddScoped<IFiscalYearRepository, ThinkOnErp.Infrastructure.Repositories.EfCore.FiscalYearRepository>();
        }
        else
        {
            services.AddScoped<IFiscalYearRepository, FiscalYearRepository>();
        }
        
        // Register permission system repositories
        // SuperAdminRepository - Feature flag: UseEfCore:SuperAdminRepository
        if (configuration.GetValue<bool>("UseEfCore:SuperAdminRepository", false))
        {
            services.AddScoped<ISuperAdminRepository, ThinkOnErp.Infrastructure.Repositories.EfCore.SuperAdminRepository>();
        }
        else
        {
            services.AddScoped<ISuperAdminRepository, SuperAdminRepository>();
        }
        
        // SystemRepository - Feature flag: UseEfCore:SystemRepository
        if (configuration.GetValue<bool>("UseEfCore:SystemRepository", false))
        {
            services.AddScoped<ISystemRepository, ThinkOnErp.Infrastructure.Repositories.EfCore.SystemRepository>();
        }
        else
        {
            services.AddScoped<ISystemRepository, SystemRepository>();
        }
        
        // ScreenRepository - Feature flag: UseEfCore:ScreenRepository
        if (configuration.GetValue<bool>("UseEfCore:ScreenRepository", false))
        {
            services.AddScoped<IScreenRepository, ThinkOnErp.Infrastructure.Repositories.EfCore.ScreenRepository>();
        }
        else
        {
            services.AddScoped<IScreenRepository, ScreenRepository>();
        }
        
        services.AddScoped<IPermissionRepository, PermissionRepository>();
        
        // RoleScreenPermissionRepository - Feature flag: UseEfCore:RoleScreenPermissionRepository
        // Note: This repository doesn't have an interface, registered as concrete class
        if (configuration.GetValue<bool>("UseEfCore:RoleScreenPermissionRepository", false))
        {
            services.AddScoped<ThinkOnErp.Infrastructure.Repositories.EfCore.RoleScreenPermissionRepository>();
        }
        
        // UserScreenPermissionRepository - Feature flag: UseEfCore:UserScreenPermissionRepository
        // Note: This repository doesn't have an interface, registered as concrete class
        if (configuration.GetValue<bool>("UseEfCore:UserScreenPermissionRepository", false))
        {
            services.AddScoped<ThinkOnErp.Infrastructure.Repositories.EfCore.UserScreenPermissionRepository>();
        }
        
        // UserRoleRepository - Feature flag: UseEfCore:UserRoleRepository
        // Note: This repository doesn't have an interface, registered as concrete class
        if (configuration.GetValue<bool>("UseEfCore:UserRoleRepository", false))
        {
            services.AddScoped<ThinkOnErp.Infrastructure.Repositories.EfCore.UserRoleRepository>();
        }
        
        // CompanySystemRepository - Feature flag: UseEfCore:CompanySystemRepository
        // Note: This repository doesn't have an interface, registered as concrete class
        if (configuration.GetValue<bool>("UseEfCore:CompanySystemRepository", false))
        {
            services.AddScoped<ThinkOnErp.Infrastructure.Repositories.EfCore.CompanySystemRepository>();
        }
        
        // Register ticket system repositories
        // TicketRepository - Feature flag: UseEfCore:TicketRepository
        if (configuration.GetValue<bool>("UseEfCore:TicketRepository", false))
        {
            services.AddScoped<ITicketRepository, ThinkOnErp.Infrastructure.Repositories.EfCore.TicketRepository>();
        }
        else
        {
            services.AddScoped<ITicketRepository, TicketRepository>();
        }
        
        // TicketTypeRepository - Feature flag: UseEfCore:TicketTypeRepository
        if (configuration.GetValue<bool>("UseEfCore:TicketTypeRepository", false))
        {
            services.AddScoped<ITicketTypeRepository, ThinkOnErp.Infrastructure.Repositories.EfCore.TicketTypeRepository>();
        }
        else
        {
            services.AddScoped<ITicketTypeRepository, TicketTypeRepository>();
        }
        
        // TicketPriorityRepository - Feature flag: UseEfCore:TicketPriorityRepository
        if (configuration.GetValue<bool>("UseEfCore:TicketPriorityRepository", false))
        {
            services.AddScoped<ITicketPriorityRepository, ThinkOnErp.Infrastructure.Repositories.EfCore.TicketPriorityRepository>();
        }
        else
        {
            services.AddScoped<ITicketPriorityRepository, TicketPriorityRepository>();
        }
        
        // TicketStatusRepository - Feature flag: UseEfCore:TicketStatusRepository
        if (configuration.GetValue<bool>("UseEfCore:TicketStatusRepository", false))
        {
            services.AddScoped<ITicketStatusRepository, ThinkOnErp.Infrastructure.Repositories.EfCore.TicketStatusRepository>();
        }
        else
        {
            services.AddScoped<ITicketStatusRepository, TicketStatusRepository>();
        }
        
        // TicketCategoryRepository - Feature flag: UseEfCore:TicketCategoryRepository
        if (configuration.GetValue<bool>("UseEfCore:TicketCategoryRepository", false))
        {
            services.AddScoped<ITicketCategoryRepository, ThinkOnErp.Infrastructure.Repositories.EfCore.TicketCategoryRepository>();
        }
        else
        {
            services.AddScoped<ITicketCategoryRepository, ThinkOnErp.Infrastructure.Repositories.EfCore.TicketCategoryRepository>();
        }
        
        // TicketCommentRepository - Feature flag: UseEfCore:TicketCommentRepository
        if (configuration.GetValue<bool>("UseEfCore:TicketCommentRepository", false))
        {
            services.AddScoped<ITicketCommentRepository, ThinkOnErp.Infrastructure.Repositories.EfCore.TicketCommentRepository>();
        }
        else
        {
            services.AddScoped<ITicketCommentRepository, TicketCommentRepository>();
        }
        
        // TicketAttachmentRepository - Feature flag: UseEfCore:TicketAttachmentRepository
        if (configuration.GetValue<bool>("UseEfCore:TicketAttachmentRepository", false))
        {
            services.AddScoped<ITicketAttachmentRepository, ThinkOnErp.Infrastructure.Repositories.EfCore.TicketAttachmentRepository>();
        }
        else
        {
            services.AddScoped<ITicketAttachmentRepository, TicketAttachmentRepository>();
        }
        
        // TicketConfigRepository - Feature flag: UseEfCore:TicketConfigRepository
        if (configuration.GetValue<bool>("UseEfCore:TicketConfigRepository", false))
        {
            services.AddScoped<ITicketConfigRepository, ThinkOnErp.Infrastructure.Repositories.EfCore.TicketConfigRepository>();
        }
        else
        {
            services.AddScoped<ITicketConfigRepository, TicketConfigRepository>();
        }
        
        // SavedSearchRepository - Feature flag: UseEfCore:SavedSearchRepository
        if (configuration.GetValue<bool>("UseEfCore:SavedSearchRepository", false))
        {
            services.AddScoped<ISavedSearchRepository, ThinkOnErp.Infrastructure.Repositories.EfCore.SavedSearchRepository>();
        }
        else
        {
            services.AddScoped<ISavedSearchRepository, SavedSearchRepository>();
        }
        
        // SearchAnalyticsRepository - Feature flag: UseEfCore:SearchAnalyticsRepository
        if (configuration.GetValue<bool>("UseEfCore:SearchAnalyticsRepository", false))
        {
            services.AddScoped<ISearchAnalyticsRepository, ThinkOnErp.Infrastructure.Repositories.EfCore.SearchAnalyticsRepository>();
        }
        else
        {
            services.AddScoped<ISearchAnalyticsRepository, SearchAnalyticsRepository>();
        }

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
        // AuditRepository - Feature flag: UseEfCore:AuditRepository
        if (configuration.GetValue<bool>("UseEfCore:AuditRepository", false))
        {
            services.AddScoped<IAuditRepository, ThinkOnErp.Infrastructure.Repositories.EfCore.AuditLogRepository>();
        }
        else
        {
            services.AddScoped<IAuditRepository, AuditRepository>();
        }
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

        // Register health checks for monitoring system components (REQ-21)
        var healthChecksBuilder = services.AddHealthChecks();
        
        // Add EF Core DbContext health check
        var efCoreHealthCheckEnabled = configuration.GetValue<bool>("HealthChecks:EfCoreDbContext:Enabled", true);
        if (efCoreHealthCheckEnabled)
        {
            var testQuery = configuration.GetValue<string>("HealthChecks:EfCoreDbContext:TestQuery", "SELECT 1 FROM DUAL");
            var checkConnectionPool = configuration.GetValue<bool>("HealthChecks:EfCoreDbContext:CheckConnectionPool", true);
            var timeoutSeconds = configuration.GetValue<int>("HealthChecks:EfCoreDbContext:TimeoutSeconds", 10);
            
            healthChecksBuilder.AddCheck<ThinkOnErp.Infrastructure.HealthChecks.EfCoreDbContextHealthCheck>(
                "efcore_dbcontext",
                failureStatus: HealthStatus.Unhealthy,
                tags: new[] { "db", "efcore", "oracle", "ready" });
        }

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
        // AuditRepository - Feature flag: UseEfCore:AuditRepository
        if (configuration.GetValue<bool>("UseEfCore:AuditRepository", false))
        {
            services.AddScoped<IAuditRepository, ThinkOnErp.Infrastructure.Repositories.EfCore.AuditLogRepository>();
        }
        else
        {
            services.AddScoped<IAuditRepository, AuditRepository>();
        }
        
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
