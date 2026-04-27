using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace ThinkOnErp.Infrastructure.Configuration.Validation;

/// <summary>
/// Extension methods for registering configuration validation in the DI container.
/// </summary>
public static class ConfigurationValidationExtensions
{
    /// <summary>
    /// Registers all traceability system configuration options with validation.
    /// Validates configuration on application startup and throws if invalid.
    /// </summary>
    /// <param name="services">The service collection</param>
    /// <param name="configuration">The configuration root</param>
    /// <returns>The service collection for chaining</returns>
    public static IServiceCollection AddTraceabilityConfigurationValidation(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // Register AuditLoggingOptions with data annotation validation
        services.AddOptions<AuditLoggingOptions>()
            .Bind(configuration.GetSection(AuditLoggingOptions.SectionName))
            .ValidateDataAnnotationsOnStart();

        // Register RequestTracingOptions with data annotation and custom validation
        services.AddOptions<RequestTracingOptions>()
            .Bind(configuration.GetSection(RequestTracingOptions.SectionName))
            .ValidateDataAnnotationsOnStart();
        services.AddSingleton<IValidateOptions<RequestTracingOptions>, RequestTracingOptionsValidator>();

        // Register PerformanceMonitoringOptions with data annotation validation
        services.AddOptions<PerformanceMonitoringOptions>()
            .Bind(configuration.GetSection(PerformanceMonitoringOptions.SectionName))
            .ValidateDataAnnotationsOnStart();

        // Register SecurityMonitoringOptions with data annotation and custom validation
        services.AddOptions<SecurityMonitoringOptions>()
            .Bind(configuration.GetSection(SecurityMonitoringOptions.SectionName))
            .ValidateDataAnnotationsOnStart();
        services.AddSingleton<IValidateOptions<SecurityMonitoringOptions>, SecurityMonitoringOptionsValidator>();

        // Register ArchivalOptions with data annotation and custom validation
        services.AddOptions<ArchivalOptions>()
            .Bind(configuration.GetSection(ArchivalOptions.SectionName))
            .ValidateDataAnnotationsOnStart();
        services.AddSingleton<IValidateOptions<ArchivalOptions>, ArchivalOptionsValidator>();

        // Register AlertingOptions with data annotation and custom validation
        services.AddOptions<AlertingOptions>()
            .Bind(configuration.GetSection(AlertingOptions.SectionName))
            .ValidateDataAnnotationsOnStart();
        services.AddSingleton<IValidateOptions<AlertingOptions>, AlertingOptionsValidator>();

        // Register AuditEncryptionOptions with data annotation and custom validation
        services.AddOptions<AuditEncryptionOptions>()
            .Bind(configuration.GetSection(AuditEncryptionOptions.SectionName))
            .ValidateDataAnnotationsOnStart();
        services.AddSingleton<IValidateOptions<AuditEncryptionOptions>, AuditEncryptionOptionsValidator>();

        // Register AuditIntegrityOptions with data annotation and custom validation
        services.AddOptions<AuditIntegrityOptions>()
            .Bind(configuration.GetSection(AuditIntegrityOptions.SectionName))
            .ValidateDataAnnotationsOnStart();
        services.AddSingleton<IValidateOptions<AuditIntegrityOptions>, AuditIntegrityOptionsValidator>();

        // Register AuditQueryCachingOptions with data annotation and custom validation
        services.AddOptions<AuditQueryCachingOptions>()
            .Bind(configuration.GetSection(AuditQueryCachingOptions.SectionName))
            .ValidateDataAnnotationsOnStart();
        services.AddSingleton<IValidateOptions<AuditQueryCachingOptions>, AuditQueryCachingOptionsValidator>();

        // Register KeyManagementOptions with data annotation and custom validation
        services.AddOptions<KeyManagementOptions>()
            .Bind(configuration.GetSection(KeyManagementOptions.SectionName))
            .ValidateDataAnnotationsOnStart();
        services.AddSingleton<IValidateOptions<KeyManagementOptions>, KeyManagementOptionsValidator>();

        return services;
    }

    /// <summary>
    /// Validates all registered configuration options and returns validation results.
    /// Useful for diagnostics and testing.
    /// </summary>
    /// <param name="serviceProvider">The service provider</param>
    /// <returns>Dictionary of configuration section names to validation results</returns>
    public static Dictionary<string, ValidateOptionsResult> ValidateAllConfigurations(
        this IServiceProvider serviceProvider)
    {
        var results = new Dictionary<string, ValidateOptionsResult>();

        // Validate AuditLoggingOptions
        results[AuditLoggingOptions.SectionName] = ValidateOptions<AuditLoggingOptions>(serviceProvider);

        // Validate RequestTracingOptions
        results[RequestTracingOptions.SectionName] = ValidateOptions<RequestTracingOptions>(serviceProvider);

        // Validate PerformanceMonitoringOptions
        results[PerformanceMonitoringOptions.SectionName] = ValidateOptions<PerformanceMonitoringOptions>(serviceProvider);

        // Validate SecurityMonitoringOptions
        results[SecurityMonitoringOptions.SectionName] = ValidateOptions<SecurityMonitoringOptions>(serviceProvider);

        // Validate ArchivalOptions
        results[ArchivalOptions.SectionName] = ValidateOptions<ArchivalOptions>(serviceProvider);

        // Validate AlertingOptions
        results[AlertingOptions.SectionName] = ValidateOptions<AlertingOptions>(serviceProvider);

        // Validate AuditEncryptionOptions
        results[AuditEncryptionOptions.SectionName] = ValidateOptions<AuditEncryptionOptions>(serviceProvider);

        // Validate AuditIntegrityOptions
        results[AuditIntegrityOptions.SectionName] = ValidateOptions<AuditIntegrityOptions>(serviceProvider);

        // Validate AuditQueryCachingOptions
        results[AuditQueryCachingOptions.SectionName] = ValidateOptions<AuditQueryCachingOptions>(serviceProvider);

        // Validate KeyManagementOptions
        results[KeyManagementOptions.SectionName] = ValidateOptions<KeyManagementOptions>(serviceProvider);

        return results;
    }

    private static ValidateOptionsResult ValidateOptions<TOptions>(IServiceProvider serviceProvider) where TOptions : class
    {
        try
        {
            var options = serviceProvider.GetRequiredService<IOptions<TOptions>>().Value;
            var validators = serviceProvider.GetServices<IValidateOptions<TOptions>>();

            foreach (var validator in validators)
            {
                var result = validator.Validate(null, options);
                if (result.Failed)
                {
                    return result;
                }
            }

            return ValidateOptionsResult.Success;
        }
        catch (OptionsValidationException ex)
        {
            return ValidateOptionsResult.Fail(ex.Failures);
        }
        catch (Exception ex)
        {
            return ValidateOptionsResult.Fail(new[] { ex.Message });
        }
    }
}
