using Microsoft.Extensions.Logging;
using ThinkOnErp.Domain.Exceptions;

namespace ThinkOnErp.Infrastructure.Resilience;

/// <summary>
/// Provides retry logic for transient failures with exponential backoff.
/// Implements Requirements 18.1, 18.3, 18.6
/// </summary>
public class RetryPolicy
{
    private readonly ILogger<RetryPolicy> _logger;
    private readonly int _maxRetries;
    private readonly TimeSpan _initialDelay;

    public RetryPolicy(ILogger<RetryPolicy> logger, int maxRetries = 3, TimeSpan? initialDelay = null)
    {
        _logger = logger;
        _maxRetries = maxRetries;
        _initialDelay = initialDelay ?? TimeSpan.FromMilliseconds(100);
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
    /// Calculates exponential backoff delay with jitter.
    /// </summary>
    private TimeSpan CalculateDelay(int attempt)
    {
        var exponentialDelay = _initialDelay.TotalMilliseconds * Math.Pow(2, attempt - 1);
        var jitter = Random.Shared.NextDouble() * 0.3 * exponentialDelay; // 30% jitter
        return TimeSpan.FromMilliseconds(exponentialDelay + jitter);
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
