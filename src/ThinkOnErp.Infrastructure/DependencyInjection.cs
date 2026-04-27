using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using ThinkOnErp.Domain.Interfaces;
using ThinkOnErp.Infrastructure.Data;
using ThinkOnErp.Infrastructure.Repositories;
using ThinkOnErp.Infrastructure.Services;
using ThinkOnErp.Infrastructure.Resilience;

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
        // Register OracleDbContext as Scoped
        services.AddScoped<OracleDbContext>();

        // Register resilience services as Singleton
        services.AddSingleton<CircuitBreakerRegistry>();
        services.AddScoped<RetryPolicy>();
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

        // Register background services as Hosted Services
        services.AddHostedService<SlaEscalationBackgroundService>();

        return services;
    }
}
