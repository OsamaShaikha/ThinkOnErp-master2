using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ThinkOnErp.Domain.Interfaces;
using ThinkOnErp.Infrastructure.Services;

namespace ThinkOnErp.API.Controllers;

/// <summary>
/// Health check controller for audit logging system.
/// Provides visibility into audit logging health without blocking API requests.
/// </summary>
[ApiController]
[Route("api/[controller]")]
[AllowAnonymous] // Health checks should be accessible without authentication
public class AuditHealthController : ControllerBase
{
    private readonly IAuditLogger _auditLogger;
    private readonly ILogger<AuditHealthController> _logger;

    public AuditHealthController(
        IAuditLogger auditLogger,
        ILogger<AuditHealthController> logger)
    {
        _auditLogger = auditLogger;
        _logger = logger;
    }

    /// <summary>
    /// Get the health status of the audit logging system.
    /// Returns 200 OK if healthy, 503 Service Unavailable if unhealthy.
    /// This endpoint does NOT block API requests - it only reports status.
    /// </summary>
    /// <remarks>
    /// Health check includes:
    /// - Background processing task status
    /// - Circuit breaker state
    /// - Queue depth and capacity
    /// - Database connectivity
    /// 
    /// Even if this endpoint returns unhealthy, the API continues to operate normally.
    /// Audit events are queued and will be processed when the system recovers.
    /// </remarks>
    [HttpGet("status")]
    [ProducesResponseType(typeof(AuditHealthStatus), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(AuditHealthStatus), StatusCodes.Status503ServiceUnavailable)]
    public async Task<IActionResult> GetHealthStatus()
    {
        try
        {
            var isHealthy = await _auditLogger.IsHealthyAsync();
            
            var status = new AuditHealthStatus
            {
                IsHealthy = isHealthy,
                Status = isHealthy ? "Healthy" : "Degraded",
                Timestamp = DateTime.UtcNow,
                Message = isHealthy 
                    ? "Audit logging system is operating normally" 
                    : "Audit logging system is degraded but API requests continue to operate normally"
            };

            // Get additional metrics if available
            if (_auditLogger is ResilientAuditLogger resilientLogger)
            {
                var metrics = resilientLogger.GetMetrics();
                status.Metrics = new AuditHealthMetrics
                {
                    TotalRequests = metrics.TotalRequests,
                    SuccessfulRequests = metrics.SuccessfulRequests,
                    FailedRequests = metrics.FailedRequests,
                    CircuitBreakerRejections = metrics.CircuitBreakerRejections,
                    RetriedRequests = metrics.RetriedRequests,
                    CircuitState = metrics.CircuitState.ToString(),
                    SuccessRate = metrics.SuccessRate,
                    FailureRate = metrics.FailureRate,
                    RejectionRate = metrics.RejectionRate,
                    QueueDepth = resilientLogger.GetQueueDepth(),
                    PendingFallbackFiles = resilientLogger.GetPendingFallbackCount()
                };
            }
            else if (_auditLogger is AuditLogger auditLogger)
            {
                status.Metrics = new AuditHealthMetrics
                {
                    QueueDepth = auditLogger.GetQueueDepth()
                };
            }

            return isHealthy 
                ? Ok(status) 
                : StatusCode(StatusCodes.Status503ServiceUnavailable, status);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to check audit logging health");
            
            var status = new AuditHealthStatus
            {
                IsHealthy = false,
                Status = "Error",
                Timestamp = DateTime.UtcNow,
                Message = "Failed to check audit logging health. API requests continue to operate normally.",
                Error = ex.Message
            };

            return StatusCode(StatusCodes.Status503ServiceUnavailable, status);
        }
    }

    /// <summary>
    /// Get detailed metrics about the audit logging system.
    /// Requires admin authorization.
    /// </summary>
    [HttpGet("metrics")]
    [Authorize(Policy = "AdminOnly")]
    [ProducesResponseType(typeof(AuditHealthMetrics), StatusCodes.Status200OK)]
    public IActionResult GetMetrics()
    {
        try
        {
            if (_auditLogger is ResilientAuditLogger resilientLogger)
            {
                var metrics = resilientLogger.GetMetrics();
                return Ok(new AuditHealthMetrics
                {
                    TotalRequests = metrics.TotalRequests,
                    SuccessfulRequests = metrics.SuccessfulRequests,
                    FailedRequests = metrics.FailedRequests,
                    CircuitBreakerRejections = metrics.CircuitBreakerRejections,
                    RetriedRequests = metrics.RetriedRequests,
                    CircuitState = metrics.CircuitState.ToString(),
                    SuccessRate = metrics.SuccessRate,
                    FailureRate = metrics.FailureRate,
                    RejectionRate = metrics.RejectionRate,
                    QueueDepth = resilientLogger.GetQueueDepth(),
                    PendingFallbackFiles = resilientLogger.GetPendingFallbackCount()
                });
            }
            else if (_auditLogger is AuditLogger auditLogger)
            {
                return Ok(new AuditHealthMetrics
                {
                    QueueDepth = auditLogger.GetQueueDepth()
                });
            }

            return Ok(new AuditHealthMetrics());
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get audit logging metrics");
            return StatusCode(StatusCodes.Status500InternalServerError, new { error = ex.Message });
        }
    }

    /// <summary>
    /// Replay fallback events from file system to database.
    /// This should be called manually after database becomes available again.
    /// Requires admin authorization.
    /// </summary>
    [HttpPost("replay-fallback")]
    [Authorize(Policy = "AdminOnly")]
    [ProducesResponseType(typeof(ReplayResult), StatusCodes.Status200OK)]
    public async Task<IActionResult> ReplayFallbackEvents()
    {
        try
        {
            if (_auditLogger is not ResilientAuditLogger resilientLogger)
            {
                return BadRequest(new { error = "Fallback replay is only available with ResilientAuditLogger" });
            }

            _logger.LogInformation("Manual fallback replay initiated by admin");
            
            var replayedCount = await resilientLogger.ReplayFallbackEventsAsync();
            
            return Ok(new ReplayResult
            {
                Success = true,
                ReplayedCount = replayedCount,
                Message = $"Successfully replayed {replayedCount} fallback events"
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to replay fallback events");
            return StatusCode(StatusCodes.Status500InternalServerError, new ReplayResult
            {
                Success = false,
                ReplayedCount = 0,
                Message = "Failed to replay fallback events",
                Error = ex.Message
            });
        }
    }
}

/// <summary>
/// Health status response for audit logging system.
/// </summary>
public class AuditHealthStatus
{
    public bool IsHealthy { get; set; }
    public string Status { get; set; } = null!;
    public DateTime Timestamp { get; set; }
    public string Message { get; set; } = null!;
    public string? Error { get; set; }
    public AuditHealthMetrics? Metrics { get; set; }
}

/// <summary>
/// Detailed metrics for audit logging system.
/// </summary>
public class AuditHealthMetrics
{
    public long TotalRequests { get; set; }
    public long SuccessfulRequests { get; set; }
    public long FailedRequests { get; set; }
    public long CircuitBreakerRejections { get; set; }
    public long RetriedRequests { get; set; }
    public string? CircuitState { get; set; }
    public double SuccessRate { get; set; }
    public double FailureRate { get; set; }
    public double RejectionRate { get; set; }
    public int QueueDepth { get; set; }
    public int PendingFallbackFiles { get; set; }
}

/// <summary>
/// Result of fallback replay operation.
/// </summary>
public class ReplayResult
{
    public bool Success { get; set; }
    public int ReplayedCount { get; set; }
    public string Message { get; set; } = null!;
    public string? Error { get; set; }
}
