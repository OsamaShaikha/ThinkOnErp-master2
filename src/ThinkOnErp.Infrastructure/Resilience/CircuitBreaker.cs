using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;

namespace ThinkOnErp.Infrastructure.Resilience;

/// <summary>
/// Implements circuit breaker pattern to prevent cascading failures.
/// Implements Requirements 18.1, 18.3, 18.11
/// </summary>
public class CircuitBreaker
{
    private readonly ILogger<CircuitBreaker> _logger;
    private readonly int _failureThreshold;
    private readonly TimeSpan _openDuration;
    private readonly TimeSpan _halfOpenTimeout;

    private CircuitState _state = CircuitState.Closed;
    private int _failureCount = 0;
    private DateTime _lastFailureTime = DateTime.MinValue;
    private DateTime _openedAt = DateTime.MinValue;
    private readonly object _lock = new object();

    public CircuitBreaker(
        ILogger<CircuitBreaker> logger,
        int failureThreshold = 5,
        TimeSpan? openDuration = null,
        TimeSpan? halfOpenTimeout = null)
    {
        _logger = logger;
        _failureThreshold = failureThreshold;
        _openDuration = openDuration ?? TimeSpan.FromSeconds(60);
        _halfOpenTimeout = halfOpenTimeout ?? TimeSpan.FromSeconds(30);
    }

    /// <summary>
    /// Current state of the circuit breaker.
    /// </summary>
    public CircuitState State
    {
        get
        {
            lock (_lock)
            {
                return _state;
            }
        }
    }

    /// <summary>
    /// Executes an operation through the circuit breaker.
    /// </summary>
    public async Task<T> ExecuteAsync<T>(Func<Task<T>> operation, string operationName)
    {
        EnsureCircuitClosed(operationName);

        try
        {
            var result = await operation();
            OnSuccess(operationName);
            return result;
        }
        catch (Exception ex)
        {
            OnFailure(operationName, ex);
            throw;
        }
    }

    /// <summary>
    /// Executes an operation through the circuit breaker (void return).
    /// </summary>
    public async Task ExecuteAsync(Func<Task> operation, string operationName)
    {
        await ExecuteAsync(async () =>
        {
            await operation();
            return true;
        }, operationName);
    }

    /// <summary>
    /// Ensures the circuit is not open before allowing operation execution.
    /// </summary>
    private void EnsureCircuitClosed(string operationName)
    {
        lock (_lock)
        {
            if (_state == CircuitState.Open)
            {
                var timeSinceOpen = DateTime.UtcNow - _openedAt;
                if (timeSinceOpen >= _openDuration)
                {
                    _logger.LogInformation(
                        "Circuit breaker for {OperationName} transitioning to Half-Open state",
                        operationName);
                    _state = CircuitState.HalfOpen;
                }
                else
                {
                    _logger.LogWarning(
                        "Circuit breaker for {OperationName} is Open. Rejecting request. Time remaining: {TimeRemaining}s",
                        operationName, (_openDuration - timeSinceOpen).TotalSeconds);
                    throw new InvalidOperationException(
                        $"Circuit breaker is open for operation '{operationName}'. Service is temporarily unavailable.");
                }
            }
        }
    }

    /// <summary>
    /// Handles successful operation execution.
    /// </summary>
    private void OnSuccess(string operationName)
    {
        lock (_lock)
        {
            if (_state == CircuitState.HalfOpen)
            {
                _logger.LogInformation(
                    "Circuit breaker for {OperationName} transitioning to Closed state after successful execution",
                    operationName);
                _state = CircuitState.Closed;
                _failureCount = 0;
            }
            else if (_state == CircuitState.Closed && _failureCount > 0)
            {
                // Reset failure count on success
                _failureCount = 0;
            }
        }
    }

    /// <summary>
    /// Handles failed operation execution.
    /// </summary>
    private void OnFailure(string operationName, Exception exception)
    {
        lock (_lock)
        {
            _failureCount++;
            _lastFailureTime = DateTime.UtcNow;

            _logger.LogWarning(exception,
                "Circuit breaker for {OperationName} recorded failure {FailureCount}/{Threshold}",
                operationName, _failureCount, _failureThreshold);

            if (_state == CircuitState.HalfOpen)
            {
                // Immediately open on failure in half-open state
                _logger.LogWarning(
                    "Circuit breaker for {OperationName} transitioning to Open state after failure in Half-Open state",
                    operationName);
                _state = CircuitState.Open;
                _openedAt = DateTime.UtcNow;
            }
            else if (_failureCount >= _failureThreshold)
            {
                _logger.LogError(
                    "Circuit breaker for {OperationName} transitioning to Open state after {FailureCount} failures",
                    operationName, _failureCount);
                _state = CircuitState.Open;
                _openedAt = DateTime.UtcNow;
            }
        }
    }

    /// <summary>
    /// Resets the circuit breaker to closed state.
    /// </summary>
    public void Reset()
    {
        lock (_lock)
        {
            _state = CircuitState.Closed;
            _failureCount = 0;
            _lastFailureTime = DateTime.MinValue;
            _openedAt = DateTime.MinValue;
        }
    }
}

/// <summary>
/// Circuit breaker states.
/// </summary>
public enum CircuitState
{
    /// <summary>
    /// Circuit is closed, operations are allowed.
    /// </summary>
    Closed,

    /// <summary>
    /// Circuit is open, operations are rejected.
    /// </summary>
    Open,

    /// <summary>
    /// Circuit is half-open, testing if service has recovered.
    /// </summary>
    HalfOpen
}
