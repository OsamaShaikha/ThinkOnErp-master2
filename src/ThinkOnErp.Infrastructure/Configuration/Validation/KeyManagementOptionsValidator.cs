using Microsoft.Extensions.Options;

namespace ThinkOnErp.Infrastructure.Configuration.Validation;

/// <summary>
/// Custom validator for KeyManagementOptions that performs complex validation beyond data annotations.
/// </summary>
public class KeyManagementOptionsValidator : IValidateOptions<KeyManagementOptions>
{
    public ValidateOptionsResult Validate(string? name, KeyManagementOptions options)
    {
        var errors = new List<string>();

        // Validate key rotation warning days is less than rotation days
        if (options.RotationWarningDays >= options.KeyRotationDays)
        {
            errors.Add("RotationWarningDays must be less than KeyRotationDays");
        }

        // Validate key storage path if provided for LocalStorage
        if (options.Provider == "LocalStorage" && !string.IsNullOrWhiteSpace(options.LocalStorage.KeyStoragePath))
        {
            try
            {
                // Check if path is valid (not checking if it exists, just if format is valid)
                var fullPath = Path.GetFullPath(options.LocalStorage.KeyStoragePath);
            }
            catch (Exception ex) when (ex is ArgumentException || ex is NotSupportedException)
            {
                errors.Add($"LocalStorage.KeyStoragePath '{options.LocalStorage.KeyStoragePath}' is not a valid path");
            }
        }

        if (errors.Any())
        {
            return ValidateOptionsResult.Fail(errors);
        }

        return ValidateOptionsResult.Success;
    }
}
