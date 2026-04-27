using Microsoft.Extensions.Options;

namespace ThinkOnErp.Infrastructure.Configuration.Validation;

/// <summary>
/// Custom validator for RequestTracingOptions that performs complex validation beyond data annotations.
/// </summary>
public class RequestTracingOptionsValidator : IValidateOptions<RequestTracingOptions>
{
    public ValidateOptionsResult Validate(string? name, RequestTracingOptions options)
    {
        var errors = new List<string>();

        // Validate payload logging level
        var validLevels = new[] { "None", "MetadataOnly", "Full" };
        if (!validLevels.Contains(options.PayloadLoggingLevel, StringComparer.OrdinalIgnoreCase))
        {
            errors.Add($"PayloadLoggingLevel must be one of: {string.Join(", ", validLevels)}");
        }

        // Validate excluded paths format
        if (options.ExcludedPaths != null)
        {
            foreach (var path in options.ExcludedPaths)
            {
                if (!path.StartsWith("/"))
                {
                    errors.Add($"Excluded path '{path}' must start with '/'");
                }
            }
        }

        // Validate correlation ID header name
        if (string.IsNullOrWhiteSpace(options.CorrelationIdHeader))
        {
            errors.Add("CorrelationIdHeader cannot be empty");
        }
        else if (options.CorrelationIdHeader.Any(c => char.IsWhiteSpace(c)))
        {
            errors.Add("CorrelationIdHeader cannot contain whitespace");
        }

        if (errors.Any())
        {
            return ValidateOptionsResult.Fail(errors);
        }

        return ValidateOptionsResult.Success;
    }
}
