using Microsoft.Extensions.Logging;
using ThinkOnErp.Domain.Exceptions;
using ThinkOnErp.Infrastructure.Configuration;

namespace ThinkOnErp.Infrastructure.Resilience;

/// <summary>
/// Provides retry logic for transient failures with exponential backoff.
/// Implements Requirements 18.1, 18.3, 18.6
/// Implements Task 16.2: Configurable retry policy for transient database failures
/// </summary>
public class RetryPolicy
{
    private readonly ILogger<RetryPolicy> _logger;
    private readonly int _maxRetries;
    private readonly TimeSpan _initialDelay;
    private readonly TimeSpan _maxDelay;
    private readonly bool _useJitter;

    public RetryPolicy(
        ILogger<RetryPolicy> logger, 
        int maxRetries = 3, 
        TimeSpan? initialDelay = null,
        TimeSpan? maxDelay = null,
        bool useJitter = true)
    {
        _logger = logger;
        _maxRetries = maxRetries;
        _initialDelay = initialDelay ?? TimeSpan.FromMilliseconds(100);
        _maxDelay = maxDelay ?? TimeSpan.FromMilliseconds(5000);
        _useJitter = useJitter;
    }

    /// <summary>
    /// Creates a RetryPolicy from AuditLoggingOptions configuration.
    /// </summary>
    public static RetryPolicy FromOptions(ILogger<RetryPolicy> logger, AuditLoggingOptions options)
    {
        return new RetryPolicy(
            logger,
            options.MaxRetryAttempts,
            TimeSpan.FromMilliseconds(options.InitialRetryDelayMs),
            TimeSpan.FromMilliseconds(options.MaxRetryDelayMs),
            options.UseRetryJitter);
    }

    /// <summary>
    /// Executes an operation with retry logic for transient failures.
    /// Uses exponential backoff strategy.
    /// </summary>
    public async Task<T> ExecuteAsync<T>(
        Func<Task<T>> operation,
        string operationName,
        Func<Exception, bool>? isTransient = null)
    {
        isTransient ??= IsTransientException;

        int attempt = 0;
        while (true)
        {
            attempt++;
            try
            {
                return await operation();
            }
            catch (Exception ex) when (isTransient(ex) && attempt < _maxRetries)
            {
                var delay = CalculateDelay(attempt);
                _logger.LogWarning(ex,
                    "Transient error in {OperationName}. Attempt {Attempt}/{MaxRetries}. Retrying in {Delay}ms",
                    operationName, attempt, _maxRetries, delay.TotalMilliseconds);

                await Task.Delay(delay);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex,
                    "Operation {OperationName} failed after {Attempt} attempts",
                    operationName, attempt);
                throw;
            }
        }
    }

    /// <summary>
    /// Executes an operation with retry logic (void return).
    /// </summary>
    public async Task ExecuteAsync(
        Func<Task> operation,
        string operationName,
        Func<Exception, bool>? isTransient = null)
    {
        await ExecuteAsync(async () =>
        {
            await operation();
            return true;
        }, operationName, isTransient);
    }

    /// <summary>
    /// Calculates exponential backoff delay with optional jitter.
    /// Formula: min(initialDelay * 2^(attempt-1), maxDelay) + jitter
    /// </summary>
    private TimeSpan CalculateDelay(int attempt)
    {
        // Calculate exponential backoff: initialDelay * 2^(attempt-1)
        var exponentialDelay = _initialDelay.TotalMilliseconds * Math.Pow(2, attempt - 1);
        
        // Cap at maximum delay
        exponentialDelay = Math.Min(exponentialDelay, _maxDelay.TotalMilliseconds);
        
        // Add jitter if enabled (±30% random variation)
        if (_useJitter)
        {
            var jitter = Random.Shared.NextDouble() * 0.3 * exponentialDelay; // 30% jitter
            exponentialDelay += jitter;
        }
        
        return TimeSpan.FromMilliseconds(exponentialDelay);
    }

    /// <summary>
    /// Determines if an exception is transient and should be retried.
    /// </summary>
    private bool IsTransientException(Exception ex)
    {
        return ex is DatabaseConnectionException ||
               ex is TimeoutException ||
               ex is System.Data.Common.DbException ||
               (ex.InnerException != null && IsTransientException(ex.InnerException));
    }
}
