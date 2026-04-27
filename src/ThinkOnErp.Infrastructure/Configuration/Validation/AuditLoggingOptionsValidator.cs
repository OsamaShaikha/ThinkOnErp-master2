using Microsoft.Extensions.Options;

namespace ThinkOnErp.Infrastructure.Configuration.Validation;

/// <summary>
/// Validator for AuditLoggingOptions that performs complex validation logic
/// beyond what data annotations can express.
/// </summary>
public class AuditLoggingOptionsValidator : IValidateOptions<AuditLoggingOptions>
{
    public ValidateOptionsResult Validate(string? name, AuditLoggingOptions options)
    {
        var failures = new List<string>();

        // Validate batch window is reasonable relative to batch size
        if (options.BatchSize > 100 && options.BatchWindowMs < 50)
        {
            failures.Add("BatchWindowMs should be at least 50ms when BatchSize exceeds 100 to allow efficient batching.");
        }

        // Validate queue size is reasonable relative to batch size
        if (options.MaxQueueSize < options.BatchSize * 10)
        {
            failures.Add($"MaxQueueSize ({options.MaxQueueSize}) should be at least 10 times BatchSize ({options.BatchSize}) to handle traffic bursts.");
        }

        // Validate circuit breaker settings are consistent
        if (options.EnableCircuitBreaker)
        {
            if (options.CircuitBreakerFailureThreshold > options.MaxRetryAttempts * 2)
            {
                failures.Add("CircuitBreakerFailureThreshold should not be more than twice MaxRetryAttempts to prevent excessive failures before circuit opens.");
            }
        }

        // Validate retry settings are reasonable
        if (options.EnableRetryPolicy)
        {
            if (options.InitialRetryDelayMs > options.MaxRetryDelayMs)
            {
                failures.Add("InitialRetryDelayMs cannot be greater than MaxRetryDelayMs.");
            }

            // Calculate maximum total retry time
            var maxTotalRetryTime = CalculateMaxRetryTime(options);
            if (maxTotalRetryTime > options.DatabaseTimeoutSeconds * 1000)
            {
                failures.Add($"Maximum total retry time ({maxTotalRetryTime}ms) exceeds DatabaseTimeoutSeconds ({options.DatabaseTimeoutSeconds}s). Reduce retry attempts or delays.");
            }
        }

        // Validate sensitive fields array is not empty
        if (options.SensitiveFields == null || options.SensitiveFields.Length == 0)
        {
            failures.Add("SensitiveFields must contain at least one field name.");
        }

        // Validate masking pattern is not empty or whitespace
        if (string.IsNullOrWhiteSpace(options.MaskingPattern))
        {
            failures.Add("MaskingPattern cannot be empty or whitespace.");
        }

        return failures.Count > 0
            ? ValidateOptionsResult.Fail(failures)
            : ValidateOptionsResult.Success;
    }

    private static int CalculateMaxRetryTime(AuditLoggingOptions options)
    {
        var totalTime = 0;
        var currentDelay = options.InitialRetryDelayMs;

        for (int i = 0; i < options.MaxRetryAttempts; i++)
        {
            totalTime += Math.Min(currentDelay, options.MaxRetryDelayMs);
            currentDelay *= 2; // Exponential backoff
        }

        return totalTime;
    }
}
