using Microsoft.Extensions.Options;

namespace ThinkOnErp.Infrastructure.Configuration.Validation;

/// <summary>
/// Custom validator for AuditQueryCachingOptions that performs complex validation beyond data annotations.
/// </summary>
public class AuditQueryCachingOptionsValidator : IValidateOptions<AuditQueryCachingOptions>
{
    public ValidateOptionsResult Validate(string? name, AuditQueryCachingOptions options)
    {
        var errors = new List<string>();

        // Validate Redis connection string if caching is enabled
        if (options.Enabled)
        {
            if (string.IsNullOrWhiteSpace(options.RedisConnectionString))
            {
                errors.Add("RedisConnectionString is required when caching is enabled");
            }
            else
            {
                // Basic validation of Redis connection string format
                // Should contain at least host:port
                if (!options.RedisConnectionString.Contains(':'))
                {
                    errors.Add("RedisConnectionString must be in format 'host:port' or 'host:port,password=xxx'");
                }
            }
        }

        // Validate parallel query configuration
        if (options.ParallelQueriesEnabled)
        {
            if (options.ParallelQueryChunkSizeDays > options.ParallelQueryThresholdDays)
            {
                errors.Add("ParallelQueryChunkSizeDays should be less than or equal to ParallelQueryThresholdDays");
            }

            if (options.MaxParallelQueries < 2)
            {
                errors.Add("MaxParallelQueries should be at least 2 when parallel queries are enabled");
            }
        }

        if (errors.Any())
        {
            return ValidateOptionsResult.Fail(errors);
        }

        return ValidateOptionsResult.Success;
    }
}
