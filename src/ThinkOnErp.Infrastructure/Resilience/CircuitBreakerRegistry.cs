using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;

namespace ThinkOnErp.Infrastructure.Resilience;

/// <summary>
/// Registry for managing multiple circuit breakers by service name.
/// Implements Requirements 18.1, 18.3
/// </summary>
public class CircuitBreakerRegistry
{
    private readonly ILoggerFactory _loggerFactory;
    private readonly ConcurrentDictionary<string, CircuitBreaker> _circuitBreakers;
    private readonly int _failureThreshold;
    private readonly TimeSpan _openDuration;

    public CircuitBreakerRegistry(
        ILoggerFactory loggerFactory,
        int failureThreshold = 5,
        TimeSpan? openDuration = null)
    {
        _loggerFactory = loggerFactory;
        _circuitBreakers = new ConcurrentDictionary<string, CircuitBreaker>();
        _failureThreshold = failureThreshold;
        _openDuration = openDuration ?? TimeSpan.FromSeconds(60);
    }

    /// <summary>
    /// Gets or creates a circuit breaker for the specified service.
    /// </summary>
    public CircuitBreaker GetOrCreate(string serviceName)
    {
        return _circuitBreakers.GetOrAdd(serviceName, name =>
        {
            var logger = _loggerFactory.CreateLogger<CircuitBreaker>();
            return new CircuitBreaker(logger, _failureThreshold, _openDuration);
        });
    }

    /// <summary>
    /// Resets all circuit breakers.
    /// </summary>
    public void ResetAll()
    {
        foreach (var breaker in _circuitBreakers.Values)
        {
            breaker.Reset();
        }
    }

    /// <summary>
    /// Gets the state of all circuit breakers.
    /// </summary>
    public Dictionary<string, CircuitState> GetAllStates()
    {
        return _circuitBreakers.ToDictionary(
            kvp => kvp.Key,
            kvp => kvp.Value.State);
    }
}
