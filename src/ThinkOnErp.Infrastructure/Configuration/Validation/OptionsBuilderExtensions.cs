using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace ThinkOnErp.Infrastructure.Configuration.Validation;

/// <summary>
/// Extension methods for OptionsBuilder to add data annotation validation.
/// </summary>
public static class OptionsBuilderExtensions
{
    /// <summary>
    /// Adds data annotation validation to the options builder.
    /// Validates all properties decorated with validation attributes.
    /// </summary>
    /// <typeparam name="TOptions">The options type to validate</typeparam>
    /// <param name="optionsBuilder">The options builder</param>
    /// <returns>The options builder for chaining</returns>
    public static OptionsBuilder<TOptions> ValidateDataAnnotations<TOptions>(
        this OptionsBuilder<TOptions> optionsBuilder) where TOptions : class
    {
        optionsBuilder.Services.AddSingleton<IValidateOptions<TOptions>>(
            new ConfigurationValidator<TOptions>(optionsBuilder.Name));

        return optionsBuilder;
    }

    /// <summary>
    /// Adds data annotation validation and validates on startup.
    /// Throws an exception if validation fails during application startup.
    /// </summary>
    /// <typeparam name="TOptions">The options type to validate</typeparam>
    /// <param name="optionsBuilder">The options builder</param>
    /// <returns>The options builder for chaining</returns>
    public static OptionsBuilder<TOptions> ValidateDataAnnotationsOnStart<TOptions>(
        this OptionsBuilder<TOptions> optionsBuilder) where TOptions : class
    {
        optionsBuilder.ValidateDataAnnotations();
        optionsBuilder.ValidateOnStart();

        return optionsBuilder;
    }
}
