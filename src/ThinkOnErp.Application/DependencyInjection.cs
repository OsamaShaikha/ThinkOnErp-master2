using FluentValidation;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using System.Reflection;
using ThinkOnErp.Application.Behaviors;
using ThinkOnErp.Application.Services;

namespace ThinkOnErp.Application;

/// <summary>
/// Extension methods for registering Application layer services.
/// Configures MediatR, FluentValidation, and pipeline behaviors.
/// </summary>
public static class DependencyInjection
{
    /// <summary>
    /// Registers Application layer services including MediatR, FluentValidation, and pipeline behaviors.
    /// </summary>
    /// <param name="services">The service collection</param>
    /// <returns>The service collection for chaining</returns>
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        var assembly = Assembly.GetExecutingAssembly();

        // Register MediatR and scan for handlers in this assembly
        services.AddMediatR(cfg =>
        {
            cfg.RegisterServicesFromAssembly(assembly);
            
            // Register pipeline behaviors in order: Logging -> Validation -> Handler
            cfg.AddBehavior(typeof(IPipelineBehavior<,>), typeof(LoggingBehavior<,>));
            cfg.AddBehavior(typeof(IPipelineBehavior<,>), typeof(ValidationBehavior<,>));
        });

        // Register FluentValidation validators from this assembly
        services.AddValidatorsFromAssembly(assembly);

        // Register application services
        services.AddScoped<ITicketConfigurationService, TicketConfigurationService>();

        return services;
    }
}
