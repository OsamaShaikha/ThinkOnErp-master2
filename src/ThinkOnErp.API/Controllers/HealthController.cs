using Microsoft.AspNetCore.Mvc;
using ThinkOnErp.Domain.Interfaces;

namespace ThinkOnErp.API.Controllers;

/// <summary>
/// Health check controller for monitoring API status.
/// Provides endpoints to check if the API and database are operational.
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class HealthController : ControllerBase
{
    private readonly ILogger<HealthController> _logger;
    private readonly IConfiguration _configuration;

    public HealthController(
        ILogger<HealthController> logger,
        IConfiguration configuration)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
    }

    /// <summary>
    /// Basic health check endpoint.
    /// Returns 200 OK if the API is running.
    /// </summary>
    /// <returns>Health status</returns>
    /// <response code="200">API is healthy</response>
    [HttpGet]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public IActionResult Get()
    {
        return Ok(new
        {
            status = "Healthy",
            timestamp = DateTime.UtcNow,
            service = "ThinkOnErp API",
            version = "1.0.0"
        });
    }

    /// <summary>
    /// Detailed health check with component status.
    /// Checks API and configuration availability.
    /// </summary>
    /// <returns>Detailed health status</returns>
    /// <response code="200">All components are healthy</response>
    /// <response code="503">One or more components are unhealthy</response>
    [HttpGet("detailed")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status503ServiceUnavailable)]
    public IActionResult GetDetailed()
    {
        var checks = new Dictionary<string, object>();
        var isHealthy = true;

        // Check API
        checks["api"] = new
        {
            status = "Healthy",
            timestamp = DateTime.UtcNow
        };

        // Check configuration
        try
        {
            var connectionString = _configuration.GetConnectionString("OracleDb");
            var jwtSecret = _configuration["JwtSettings:SecretKey"];

            checks["configuration"] = new
            {
                status = !string.IsNullOrEmpty(connectionString) && !string.IsNullOrEmpty(jwtSecret) 
                    ? "Healthy" 
                    : "Unhealthy",
                hasConnectionString = !string.IsNullOrEmpty(connectionString),
                hasJwtSettings = !string.IsNullOrEmpty(jwtSecret)
            };

            if (string.IsNullOrEmpty(connectionString) || string.IsNullOrEmpty(jwtSecret))
            {
                isHealthy = false;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking configuration health");
            checks["configuration"] = new
            {
                status = "Unhealthy",
                error = ex.Message
            };
            isHealthy = false;
        }

        var response = new
        {
            status = isHealthy ? "Healthy" : "Unhealthy",
            timestamp = DateTime.UtcNow,
            service = "ThinkOnErp API",
            version = "1.0.0",
            checks
        };

        return isHealthy 
            ? Ok(response) 
            : StatusCode(StatusCodes.Status503ServiceUnavailable, response);
    }

    /// <summary>
    /// Liveness probe endpoint.
    /// Used by container orchestrators to check if the application is alive.
    /// </summary>
    /// <returns>200 OK if alive</returns>
    /// <response code="200">Application is alive</response>
    [HttpGet("live")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public IActionResult Live()
    {
        return Ok(new { status = "Alive" });
    }

    /// <summary>
    /// Readiness probe endpoint.
    /// Used by container orchestrators to check if the application is ready to serve traffic.
    /// </summary>
    /// <returns>200 OK if ready</returns>
    /// <response code="200">Application is ready</response>
    /// <response code="503">Application is not ready</response>
    [HttpGet("ready")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status503ServiceUnavailable)]
    public IActionResult Ready()
    {
        try
        {
            // Check if essential configuration is available
            var connectionString = _configuration.GetConnectionString("OracleDb");
            var jwtSecret = _configuration["JwtSettings:SecretKey"];

            if (string.IsNullOrEmpty(connectionString) || string.IsNullOrEmpty(jwtSecret))
            {
                return StatusCode(StatusCodes.Status503ServiceUnavailable, new
                {
                    status = "Not Ready",
                    reason = "Missing configuration"
                });
            }

            return Ok(new { status = "Ready" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Readiness check failed");
            return StatusCode(StatusCodes.Status503ServiceUnavailable, new
            {
                status = "Not Ready",
                reason = ex.Message
            });
        }
    }
}
