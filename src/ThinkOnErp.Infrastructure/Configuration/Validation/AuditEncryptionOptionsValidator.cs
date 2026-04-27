using Microsoft.Extensions.Options;

namespace ThinkOnErp.Infrastructure.Configuration.Validation;

/// <summary>
/// Custom validator for AuditEncryptionOptions that performs complex validation beyond data annotations.
/// </summary>
public class AuditEncryptionOptionsValidator : IValidateOptions<AuditEncryptionOptions>
{
    public ValidateOptionsResult Validate(string? name, AuditEncryptionOptions options)
    {
        var errors = new List<string>();

        if (!options.Enabled)
        {
            return ValidateOptionsResult.Success;
        }

        // Validate encryption key format and length
        if (!string.IsNullOrWhiteSpace(options.Key))
        {
            try
            {
                var keyBytes = Convert.FromBase64String(options.Key);
                if (keyBytes.Length != 32)
                {
                    errors.Add($"Encryption key must be exactly 32 bytes (256 bits) when decoded. Current length: {keyBytes.Length} bytes");
                }
            }
            catch (FormatException)
            {
                errors.Add("Encryption key must be a valid Base64 encoded string");
            }
        }

        // Validate HSM configuration if HSM is enabled
        if (options.UseHsm)
        {
            if (string.IsNullOrWhiteSpace(options.HsmKeyId))
            {
                errors.Add("HsmKeyId is required when UseHsm is true");
            }

            // When using HSM, the Key property can be empty
            // But if both are provided, warn that HSM takes precedence
            if (!string.IsNullOrWhiteSpace(options.Key))
            {
                // This is a warning, not an error - HSM will take precedence
                // We'll allow it but could log a warning at runtime
            }
        }
        else
        {
            // When not using HSM, Key must be provided
            if (string.IsNullOrWhiteSpace(options.Key))
            {
                errors.Add("Key is required when UseHsm is false");
            }
        }

        // Validate encrypted fields array
        if (options.EncryptedFields == null || options.EncryptedFields.Length == 0)
        {
            errors.Add("At least one encrypted field must be specified");
        }

        if (errors.Any())
        {
            return ValidateOptionsResult.Fail(errors);
        }

        return ValidateOptionsResult.Success;
    }
}
