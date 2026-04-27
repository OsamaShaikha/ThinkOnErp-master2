using Microsoft.Extensions.Options;

namespace ThinkOnErp.Infrastructure.Configuration.Validation;

/// <summary>
/// Custom validator for ArchivalOptions that performs complex validation beyond data annotations.
/// </summary>
public class ArchivalOptionsValidator : IValidateOptions<ArchivalOptions>
{
    public ValidateOptionsResult Validate(string? name, ArchivalOptions options)
    {
        var errors = new List<string>();

        // Validate storage connection string is provided for non-database providers
        if (options.StorageProvider != "Database" && string.IsNullOrWhiteSpace(options.StorageConnectionString))
        {
            errors.Add($"StorageConnectionString is required when StorageProvider is '{options.StorageProvider}'");
        }

        // Validate encryption key is provided when encryption is enabled
        if (options.EncryptArchivedData && string.IsNullOrWhiteSpace(options.EncryptionKeyId))
        {
            errors.Add("EncryptionKeyId is required when EncryptArchivedData is true");
        }

        // Validate time zone is valid
        try
        {
            TimeZoneInfo.FindSystemTimeZoneById(options.TimeZone);
        }
        catch (TimeZoneNotFoundException)
        {
            errors.Add($"TimeZone '{options.TimeZone}' is not a valid time zone identifier");
        }

        // Validate batch size is reasonable for transaction timeout
        if (options.BatchSize > 5000 && options.TransactionTimeoutSeconds < 60)
        {
            errors.Add("TransactionTimeoutSeconds should be at least 60 seconds when BatchSize exceeds 5000");
        }

        if (errors.Any())
        {
            return ValidateOptionsResult.Fail(errors);
        }

        return ValidateOptionsResult.Success;
    }
}
