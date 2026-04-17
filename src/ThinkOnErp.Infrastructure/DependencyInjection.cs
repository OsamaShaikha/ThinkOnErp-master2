using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using ThinkOnErp.Domain.Interfaces;
using ThinkOnErp.Infrastructure.Data;
using ThinkOnErp.Infrastructure.Repositories;
using ThinkOnErp.Infrastructure.Services;

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

        // Register all repositories as Scoped
        services.AddScoped<IRoleRepository, RoleRepository>();
        services.AddScoped<ICurrencyRepository, CurrencyRepository>();
        services.AddScoped<ICompanyRepository, CompanyRepository>();
        services.AddScoped<IBranchRepository, BranchRepository>();
        services.AddScoped<IUserRepository, UserRepository>();
        services.AddScoped<IAuthRepository, AuthRepository>();
        services.AddScoped<IFiscalYearRepository, FiscalYearRepository>();
        
        // Register permission system repositories
        services.AddScoped<ISystemRepository, SystemRepository>();
        services.AddScoped<IScreenRepository, ScreenRepository>();
        services.AddScoped<IPermissionRepository, PermissionRepository>();

        // Register infrastructure services as Scoped
        services.AddScoped<PasswordHashingService>();
        services.AddScoped<JwtTokenService>();

        return services;
    }
}
