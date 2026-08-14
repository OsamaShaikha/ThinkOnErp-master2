using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Scrutor;
using System.Threading.Channels;
using ThinkOnErp.Domain.Interfaces;
using ThinkOnErp.Domain.Interfaces.Accounting;
using ThinkOnErp.Infrastructure.Resilience;
using ThinkOnErp.Infrastructure.Configuration;
using ThinkOnErp.Infrastructure.Configuration.Validation;
using ThinkOnErp.Infrastructure.Data;
using ThinkOnErp.Infrastructure.Repositories;
using ThinkOnErp.Infrastructure.Repositories.Accounting;
using ThinkOnErp.Infrastructure.Services;
using ThinkOnErp.Infrastructure.Services.Accounting;
using ThinkOnErp.Application.Services.Accounting;


namespace ThinkOnErp.Infrastructure;

/// <summary>
/// Extension methods for registering Infrastructure layer services.
/// Configures Entity Framework Core database context, repositories, and services.
/// </summary>
public static class DependencyInjection
{
    /// <summary>
    /// Registers Infrastructure layer services including EF Core DbContext, repositories, and services.
    /// </summary>
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        // Register all configuration options with data annotation validation
        services.AddTraceabilityConfigurationValidation(configuration);

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

        // Register EF Core DbContext with Oracle provider
        var connectionString = configuration.GetConnectionString("OracleDb")
            ?? throw new InvalidOperationException("Oracle connection string 'OracleDb' not found in configuration.");
        
        services.AddDbContext<OracleDbContext>(options =>
            options.UseOracle(connectionString, b =>
            {
                b.MigrationsAssembly(typeof(OracleDbContext).Assembly.FullName);
                b.UseOracleSQLCompatibility(OracleSQLCompatibility.DatabaseVersion19);
            }));

        // Register resilience services as Singleton
        services.AddSingleton<CircuitBreakerRegistry>(sp =>
        {
            var loggerFactory = sp.GetRequiredService<ILoggerFactory>();
            var configuration = sp.GetRequiredService<IConfiguration>();
            
            var auditOptions = new AuditLoggingOptions();
            configuration.GetSection(AuditLoggingOptions.SectionName).Bind(auditOptions);
            
            return new CircuitBreakerRegistry(
                loggerFactory,
                auditOptions.CircuitBreakerFailureThreshold,
                TimeSpan.FromSeconds(auditOptions.CircuitBreakerTimeoutSeconds));
        });
        
        services.AddSingleton<RetryPolicy>(sp =>
        {
            var logger = sp.GetRequiredService<ILogger<RetryPolicy>>();
            var configuration = sp.GetRequiredService<IConfiguration>();
            
            var auditOptions = new AuditLoggingOptions();
            configuration.GetSection(AuditLoggingOptions.SectionName).Bind(auditOptions);
            
            return RetryPolicy.FromOptions(logger, auditOptions);
        });
        
        services.AddSingleton<CircuitBreaker>(sp =>
        {
            var loggerFactory = sp.GetRequiredService<ILoggerFactory>();
            var logger = loggerFactory.CreateLogger<CircuitBreaker>();
            return new CircuitBreaker(logger);
        });
        //services.AddScoped<ResilientDatabaseExecutor>();

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
        services.AddScoped<ISysFeatureRepository, SysFeatureRepository>();
        services.AddScoped<ISysScreenFeatureRepository, SysScreenFeatureRepository>();
        services.AddScoped<IBranchSystemRepository, BranchSystemRepository>();
        services.AddScoped<IBranchScreenRepository, BranchScreenRepository>();
        services.AddScoped<IBranchFeatureRepository, BranchFeatureRepository>();
        services.AddScoped<ISysCodeRepository, SysCodeRepository>();
        services.AddScoped<ISysCodeService, SysCodeService>();
        services.AddScoped<ISysSettingRepository, SysSettingRepository>();
        services.AddScoped<IGlAccountRepository, GlAccountRepository>();

        // Register company-level permission repositories
        services.AddScoped<IRoleScreenPermissionRepository, RoleScreenPermissionRepository>();
        services.AddScoped<IUserScreenPermissionRepository, UserScreenPermissionRepository>();
        services.AddScoped<IPermissionService, PermissionService>();
        
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
        services.AddScoped<IOracleSchemaService, OracleSchemaService>();
        services.AddScoped<ISlaCalculationService, SlaCalculationService>();
        services.AddScoped<ISlaEscalationService, SlaEscalationService>();
        services.AddScoped<IAuditTrailService, AuditTrailService>();
        services.AddScoped<ILegacyAuditService, LegacyAuditService>();
        services.AddScoped<ICurrentTenantContext, HttpCurrentTenantContext>();
        services.AddScoped<ICoaWorkbookReader, XlsxCoaWorkbookReader>();
        services.AddScoped<IGlVoucherRepository, GlVoucherRepository>();
        services.AddScoped<IGlCostCenterRepository, GlCostCenterRepository>();

        // Register audit logging services
        services.AddScoped<IAuditRepository, AuditRepository>();
        services.AddScoped<ISensitiveDataMasker, SensitiveDataMasker>();
        services.AddSingleton<IAuditDataEncryption, AuditDataEncryption>();
        services.AddScoped<IAuditLogIntegrityService, AuditLogIntegrityService>();
        services.AddScoped<IAuditQueryService, AuditQueryService>();
        services.AddScoped<IAuditContextProvider, AuditContextProvider>();
        services.AddScoped<IExceptionCategorizationService, ExceptionCategorizationService>();
        services.AddScoped<IEntityStateProvider, EntityStateProvider>();
        
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

        // Register document management services
        services.AddScoped<IDocumentRepository, DocumentRepository>();
        services.AddScoped<IDocumentStorageService, DocumentStorageService>();

        // Register logo storage service (singleton, but resolves ISysSettingRepository via scope at startup)
        services.AddSingleton<ILogoStorageService>(sp =>
        {
            var logger = sp.GetRequiredService<ILogger<LogoStorageService>>();
            var configuration = sp.GetRequiredService<IConfiguration>();

            string? basePath = null;
            try
            {
                using var scope = sp.CreateScope();
                var settingRepo = scope.ServiceProvider.GetRequiredService<ISysSettingRepository>();
                var setting = settingRepo.GetByCodeAsync(6).GetAwaiter().GetResult();
                if (setting != null && !string.IsNullOrEmpty(setting.SettingValue))
                    basePath = setting.SettingValue;
            }
            catch { }

            basePath ??= configuration.GetValue<string>("LogoStorage:BasePath");
            basePath ??= "/THINKON_FILES/LOGOS";

            return new LogoStorageService(logger, basePath);
        });

        // Register multi-tenant access control services
        services.AddScoped<IMultiTenantAccessService, MultiTenantAccessService>();

        // Register key management services
        services.AddSingleton<KeyManagementService>();
        services.AddSingleton<Services.KeyManagement.KeyProviderFactory>();
        services.AddSingleton<IKeyManagementService>(sp =>
        {
            var optionsAccessor = sp.GetRequiredService<
                Microsoft.Extensions.Options.IOptions<KeyManagementOptions>>();
            var options = optionsAccessor.Value;
            var factory = sp.GetRequiredService<Services.KeyManagement.KeyProviderFactory>();
            var primaryProvider = factory.CreateProvider(options.Provider, options);
            var fallbackProvider =
                string.Equals(options.FallbackProvider, "None", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(options.FallbackProvider, options.Provider, StringComparison.OrdinalIgnoreCase)
                    ? null
                    : factory.CreateProvider(options.FallbackProvider, options);

            return new Services.KeyManagement.KeyManagementService(
                primaryProvider,
                fallbackProvider,
                optionsAccessor,
                sp.GetRequiredService<
                    ILogger<Services.KeyManagement.KeyManagementService>>(),
                sp.GetService<Microsoft.Extensions.Caching.Memory.IMemoryCache>(),
                sp.GetService<IAlertManager>());
        });
        services.AddScoped<KeyManagementCli>();

        // Register background services as Hosted Services
        services.AddHostedService<SlaEscalationBackgroundService>();
        
        // Register AuditLogger as both hosted service and IAuditLogger interface
        services.AddSingleton<AuditLogger>();
        services.AddSingleton<IAuditLogger>(provider => provider.GetRequiredService<AuditLogger>());
        services.AddHostedService<AuditLogger>(provider => provider.GetRequiredService<AuditLogger>());

        // Wrap with ResilientAuditLogger decorator that adds circuit breaker, retry, and file fallback
        services.Decorate<IAuditLogger>((inner, sp) =>
        {
            var circuitBreaker = sp.GetRequiredService<CircuitBreaker>();
            var retryPolicy = sp.GetRequiredService<RetryPolicy>();
            var logger = sp.GetRequiredService<ILogger<ResilientAuditLogger>>();
            var options = new ResilientAuditLoggerOptions();
            sp.GetRequiredService<IConfiguration>().GetSection("ResilientAuditLogger").Bind(options);
            var fileFallback = sp.GetService<FileSystemAuditFallback>();
            return new ResilientAuditLogger(inner, circuitBreaker, retryPolicy, logger, options, fileFallback);
        });
        
        // Register FileSystemAuditFallback for fallback storage when DB is down
        services.AddSingleton<FileSystemAuditFallback>(sp =>
        {
            var logger = sp.GetRequiredService<ILogger<FileSystemAuditFallback>>();
            var options = new FileSystemAuditFallbackOptions();
            sp.GetRequiredService<IConfiguration>().GetSection("FileSystemAuditFallback").Bind(options);
            // Read fallback path from SYS_SETTINGS (CODE 5), override config/default
            try
            {
                using var scope = sp.CreateScope();
                var settingRepo = scope.ServiceProvider.GetRequiredService<ISysSettingRepository>();
                var setting = settingRepo.GetByCodeAsync(5).GetAwaiter().GetResult();
                if (setting != null && !string.IsNullOrEmpty(setting.SettingValue))
                    options.FallbackPath = setting.SettingValue;
            }
            catch { /* fallback to default or config value */ }
            return new FileSystemAuditFallback(logger, options);
        });
        
        services.AddHostedService<MetricsAggregationBackgroundService>();
        services.AddHostedService<AlertProcessingBackgroundService>();
        services.AddHostedService<ScheduledReportGenerationService>();
        services.AddHostedService<ArchivalBackgroundService>();
        services.AddHostedService<KeyRotationBackgroundService>();

        return services;
    }
}
