using Microsoft.Extensions.Options;

namespace ThinkOnErp.Infrastructure.Configuration.Validation;

/// <summary>
/// Custom validator for AuditIntegrityOptions that performs complex validation beyond data annotations.
/// </summary>
public class AuditIntegrityOptionsValidator : IValidateOptions<AuditIntegrityOptions>
{
    public ValidateOptionsResult Validate(string? name, AuditIntegrityOptions options)
    {
        var errors = new List<string>();

        if (!options.Enabled)
        {
            return ValidateOptionsResult.Success;
        }

        // Validate signing key format and length
        if (!string.IsNullOrWhiteSpace(options.SigningKey))
        {
            try
            {
                var keyBytes = Convert.FromBase64String(options.SigningKey);
                if (keyBytes.Length < 32)
                {
                    errors.Add($"Signing key must be at least 32 bytes (256 bits) when decoded. Current length: {keyBytes.Length} bytes");
                }
            }
            catch (FormatException)
            {
                errors.Add("Signing key must be a valid Base64 encoded string");
            }
        }

        // Validate hash algorithm
        var validAlgorithms = new[] { "HMACSHA256", "HMACSHA512" };
        if (!validAlgorithms.Contains(options.HashAlgorithm, StringComparer.OrdinalIgnoreCase))
        {
            errors.Add($"HashAlgorithm must be one of: {string.Join(", ", validAlgorithms)}");
        }

        // Warn if VerifyOnRead is enabled (performance impact)
        if (options.VerifyOnRead && !options.LogIntegrityOperations)
        {
            // This is more of a recommendation, but we'll allow it
            // Could log a warning at runtime about performance impact
        }

        if (errors.Any())
        {
            return ValidateOptionsResult.Fail(errors);
        }

        return ValidateOptionsResult.Success;
    }
}
