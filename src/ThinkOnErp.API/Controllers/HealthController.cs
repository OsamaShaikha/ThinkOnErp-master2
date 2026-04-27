using Microsoft.AspNetCore.Mvc;
using ThinkOnErp.Application.Common;
using ThinkOnErp.Infrastructure.Resilience;

namespace ThinkOnErp.API.Controllers;

/// <summary>
/// Health check controller for monitoring system status and circuit breaker states.
/// Implements Requirement 18.9
/// </summary>
[ApiController]
[Route("api/health")]
public class HealthController : ControllerBase
{
    private readonly CircuitBreakerRegistry _circuitBreakerRegistry;
    private readonly ILogger<HealthController> _logger;

    public HealthController(
        CircuitBreakerRegistry circuitBreakerRegistry,
        ILogger<HealthController> logger)
    {
        _circuitBreakerRegistry = circuitBreakerRegistry;
        _logger = logger;
    }

    /// <summary>
    /// Basic health check endpoint.
    /// </summary>
    [HttpGet]
    public ActionResult<ApiResponse<HealthStatus>> GetHealth()
    {
        var circuitStates = _circuitBreakerRegistry.GetAllStates();
        var hasOpenCircuits = circuitStates.Any(kvp => kvp.Value == CircuitState.Open);

        var status = new HealthStatus
        {
            Status = hasOpenCircuits ? "Degraded" : "Healthy",
            Timestamp = DateTime.UtcNow,
            CircuitBreakers = circuitStates.ToDictionary(
                kvp => kvp.Key,
                kvp => kvp.Value.ToString())
        };

        _logger.LogInformation("Health check performed. Status: {Status}", status.Status);

        return Ok(ApiResponse<HealthStatus>.CreateSuccess(status, "Health check completed successfully"));
    }

    /// <summary>
    /// Detailed health check with circuit breaker information.
    /// </summary>
    [HttpGet("detailed")]
    public ActionResult<ApiResponse<DetailedHealthStatus>> GetDetailedHealth()
    {
        var circuitStates = _circuitBreakerRegistry.GetAllStates();

        var status = new DetailedHealthStatus
        {
            Status = circuitStates.Any(kvp => kvp.Value == CircuitState.Open) ? "Degraded" : "Healthy",
            Timestamp = DateTime.UtcNow,
            Services = circuitStates.Select(kvp => new ServiceHealth
            {
                ServiceName = kvp.Key,
                CircuitState = kvp.Value.ToString(),
                IsHealthy = kvp.Value != CircuitState.Open
            }).ToList()
        };

        return Ok(ApiResponse<DetailedHealthStatus>.CreateSuccess(status, "Detailed health check completed successfully"));
    }
}

/// <summary>
/// Basic health status response.
/// </summary>
public class HealthStatus
{
    public string Status { get; set; } = string.Empty;
    public DateTime Timestamp { get; set; }
    public Dictionary<string, string> CircuitBreakers { get; set; } = new();
}

/// <summary>
/// Detailed health status response.
/// </summary>
public class DetailedHealthStatus
{
    public string Status { get; set; } = string.Empty;
    public DateTime Timestamp { get; set; }
    public List<ServiceHealth> Services { get; set; } = new();
}

/// <summary>
/// Individual service health information.
/// </summary>
public class ServiceHealth
{
    public string ServiceName { get; set; } = string.Empty;
    public string CircuitState { get; set; } = string.Empty;
    public bool IsHealthy { get; set; }
}
