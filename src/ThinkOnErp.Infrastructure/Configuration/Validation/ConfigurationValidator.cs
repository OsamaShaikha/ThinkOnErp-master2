using System.ComponentModel.DataAnnotations;
using Microsoft.Extensions.Options;

namespace ThinkOnErp.Infrastructure.Configuration.Validation;

/// <summary>
/// Generic configuration validator that validates options using data annotations.
/// Implements IValidateOptions to integrate with ASP.NET Core options validation.
/// </summary>
/// <typeparam name="TOptions">The options type to validate</typeparam>
public class ConfigurationValidator<TOptions> : IValidateOptions<TOptions> where TOptions : class
{
    private readonly string _name;

    public ConfigurationValidator(string name)
    {
        _name = name;
    }

    /// <summary>
    /// Validates the options instance using data annotations.
    /// </summary>
    /// <param name="name">The name of the options instance</param>
    /// <param name="options">The options instance to validate</param>
    /// <returns>Validation result with errors if validation fails</returns>
    public ValidateOptionsResult Validate(string? name, TOptions options)
    {
        // Skip validation if name doesn't match (for named options)
        if (!string.IsNullOrEmpty(name) && name != _name)
        {
            return ValidateOptionsResult.Skip;
        }

        var validationResults = new List<ValidationResult>();
        var validationContext = new ValidationContext(options);

        bool isValid = Validator.TryValidateObject(
            options,
            validationContext,
            validationResults,
            validateAllProperties: true);

        if (isValid)
        {
            return ValidateOptionsResult.Success;
        }

        var errors = validationResults
            .Select(r => $"{_name}: {r.ErrorMessage}")
            .ToList();

        return ValidateOptionsResult.Fail(errors);
    }
}
