using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ThinkOnErp.Domain.Interfaces;
using ThinkOnErp.Domain.Models;

namespace ThinkOnErp.API.Controllers;

/// <summary>
/// Controller for system monitoring endpoints including performance, memory, health, and security metrics.
/// Provides real-time insights into system resource usage, performance characteristics, and security threats.
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Authorize(Policy = "AdminOnly")]
public class MonitoringController : ControllerBase
{
    private readonly IPerformanceMonitor _performanceMonitor;
    private readonly IMemoryMonitor _memoryMonitor;
    private readonly ISecurityMonitor _securityMonitor;
    private readonly IAuditLogger _auditLogger;
    private readonly IAlertManager _alertManager;
    private readonly ILogger<MonitoringController> _logger;

    public MonitoringController(
        IPerformanceMonitor performanceMonitor,
        IMemoryMonitor memoryMonitor,
        ISecurityMonitor securityMonitor,
        IAuditLogger auditLogger,
        IAlertManager alertManager,
        ILogger<MonitoringController> logger)
    {
        _performanceMonitor = performanceMonitor;
        _memoryMonitor = memoryMonitor;
        _securityMonitor = securityMonitor;
        _auditLogger = auditLogger;
        _alertManager = alertManager;
        _logger = logger;
    }

    /// <summary>
    /// Get comprehensive system health metrics including CPU, memory, and database connections.
    /// </summary>
    /// <remarks>
    /// Returns detailed health information about the system including:
    /// - CPU utilization
    /// - Memory usage and GC statistics
    /// - Database connection pool status
    /// - Request rate and error rate
    /// - Audit queue depth
    /// - Individual health checks for each subsystem
    /// </remarks>
    [HttpGet("health")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(SystemHealthMetrics), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetSystemHealth()
    {
        try
        {
            var healthMetrics = await _performanceMonitor.GetSystemHealthAsync();
            return Ok(healthMetrics);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get system health metrics");
            return StatusCode(500, new { error = "Failed to retrieve system health metrics" });
        }
    }

    /// <summary>
    /// Get detailed memory usage metrics including heap sizes and GC statistics.
    /// </summary>
    /// <remarks>
    /// Returns comprehensive memory information including:
    /// - Total allocated and available memory
    /// - Generation heap sizes (Gen0, Gen1, Gen2, LOH)
    /// - GC collection counts and frequency
    /// - Memory allocation rate
    /// - Memory pressure indicators
    /// - Optimization recommendations
    /// </remarks>
    [HttpGet("memory")]
    [ProducesResponseType(typeof(MemoryMetrics), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetMemoryMetrics()
    {
        try
        {
            var memoryMetrics = await _memoryMonitor.GetMemoryMetricsAsync();
            return Ok(memoryMetrics);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get memory metrics");
            return StatusCode(500, new { error = "Failed to retrieve memory metrics" });
        }
    }

    /// <summary>
    /// Detect current memory pressure level and get recommendations.
    /// </summary>
    /// <remarks>
    /// Analyzes current memory usage and returns:
    /// - Memory pressure severity (None, Low, Moderate, High, Critical)
    /// - Pressure level percentage (0-100)
    /// - Description of the current situation
    /// - Actionable recommendations to reduce memory pressure
    /// - Whether immediate action is required
    /// </remarks>
    [HttpGet("memory/pressure")]
    [ProducesResponseType(typeof(MemoryPressureInfo), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetMemoryPressure()
    {
        try
        {
            var pressureInfo = await _memoryMonitor.DetectMemoryPressureAsync();
            return Ok(pressureInfo);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to detect memory pressure");
            return StatusCode(500, new { error = "Failed to detect memory pressure" });
        }
    }

    /// <summary>
    /// Get memory optimization recommendations based on current usage patterns.
    /// </summary>
    /// <remarks>
    /// Analyzes memory usage patterns and provides specific recommendations such as:
    /// - Garbage collection strategies
    /// - Object pooling opportunities
    /// - Heap fragmentation mitigation
    /// - Large object allocation optimization
    /// </remarks>
    [HttpGet("memory/recommendations")]
    [ProducesResponseType(typeof(List<string>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetMemoryOptimizationRecommendations()
    {
        try
        {
            var recommendations = await _memoryMonitor.GetOptimizationRecommendationsAsync();
            return Ok(recommendations);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get memory optimization recommendations");
            return StatusCode(500, new { error = "Failed to retrieve optimization recommendations" });
        }
    }

    /// <summary>
    /// Trigger memory optimization strategies including garbage collection and heap compaction.
    /// </summary>
    /// <remarks>
    /// **WARNING:** This operation can temporarily impact performance.
    /// 
    /// Performs the following optimizations:
    /// - Forces a full garbage collection (Gen2) with heap compaction
    /// - Compacts the Large Object Heap (LOH)
    /// - Trims the working set (Windows only)
    /// 
    /// Use this endpoint during low-traffic periods or when memory pressure is high.
    /// </remarks>
    [HttpPost("memory/optimize")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> OptimizeMemory()
    {
        try
        {
            _logger.LogInformation("Memory optimization triggered by user: {UserId}", User.Identity?.Name);
            
            await _memoryMonitor.OptimizeMemoryAsync();
            
            return Ok(new { message = "Memory optimization completed successfully" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to optimize memory");
            return StatusCode(500, new { error = "Failed to optimize memory" });
        }
    }

    /// <summary>
    /// Force garbage collection for a specific generation.
    /// </summary>
    /// <param name="generation">GC generation to collect (0, 1, or 2). Default is 2 (full collection).</param>
    /// <param name="blocking">Whether to wait for GC to complete. Default is true.</param>
    /// <param name="compacting">Whether to compact the heap. Default is true.</param>
    /// <remarks>
    /// **WARNING:** Forcing garbage collection can impact performance.
    /// 
    /// Use this endpoint sparingly and only when:
    /// - Memory pressure is high
    /// - During low-traffic periods
    /// - For diagnostic purposes
    /// 
    /// Generation levels:
    /// - 0: Collects short-lived objects (fastest)
    /// - 1: Collects Gen0 and Gen1 objects
    /// - 2: Full collection including all generations and LOH (slowest but most thorough)
    /// </remarks>
    [HttpPost("memory/gc")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public IActionResult ForceGarbageCollection(
        [FromQuery] int generation = 2,
        [FromQuery] bool blocking = true,
        [FromQuery] bool compacting = true)
    {
        try
        {
            if (generation < 0 || generation > 2)
            {
                return BadRequest(new { error = "Generation must be 0, 1, or 2" });
            }
            
            _logger.LogInformation(
                "Garbage collection forced by user: {UserId}, Generation={Generation}, Blocking={Blocking}, Compacting={Compacting}",
                User.Identity?.Name, generation, blocking, compacting);
            
            _memoryMonitor.ForceGarbageCollection(generation, blocking, compacting);
            
            return Ok(new 
            { 
                message = $"Garbage collection (Gen{generation}) completed successfully",
                generation,
                blocking,
                compacting
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to force garbage collection");
            return StatusCode(500, new { error = "Failed to force garbage collection" });
        }
    }

    /// <summary>
    /// Get performance statistics for a specific endpoint over a time period.
    /// </summary>
    /// <param name="endpoint">The endpoint path to get statistics for</param>
    /// <param name="periodMinutes">Time period in minutes (default: 60)</param>
    [HttpGet("performance/endpoint")]
    [ProducesResponseType(typeof(PerformanceStatistics), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetEndpointStatistics(
        [FromQuery] string endpoint,
        [FromQuery] int periodMinutes = 60)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(endpoint))
            {
                return BadRequest(new { error = "Endpoint parameter is required" });
            }
            
            var period = TimeSpan.FromMinutes(periodMinutes);
            var statistics = await _performanceMonitor.GetEndpointStatisticsAsync(endpoint, period);
            
            return Ok(statistics);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get endpoint statistics for {Endpoint}", endpoint);
            return StatusCode(500, new { error = "Failed to retrieve endpoint statistics" });
        }
    }

    /// <summary>
    /// Get slow requests that exceeded the specified execution time threshold.
    /// </summary>
    /// <param name="thresholdMs">Execution time threshold in milliseconds (default: 1000)</param>
    /// <param name="pageNumber">Page number (1-based, default: 1)</param>
    /// <param name="pageSize">Number of items per page (default: 50, max: 100)</param>
    [HttpGet("performance/slow-requests")]
    [ProducesResponseType(typeof(PagedResult<SlowRequest>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetSlowRequests(
        [FromQuery] int thresholdMs = 1000,
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 50)
    {
        try
        {
            // Validate pagination parameters
            if (pageNumber < 1)
            {
                return BadRequest(new { error = "Page number must be greater than 0" });
            }

            if (pageSize < 1 || pageSize > 100)
            {
                return BadRequest(new { error = "Page size must be between 1 and 100" });
            }

            var pagination = new PaginationOptions
            {
                PageNumber = pageNumber,
                PageSize = pageSize
            };

            var result = await _performanceMonitor.GetSlowRequestsAsync(thresholdMs, pagination);
            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get slow requests");
            return StatusCode(500, new { error = "Failed to retrieve slow requests" });
        }
    }

    /// <summary>
    /// Get slow database queries that exceeded the specified execution time threshold.
    /// </summary>
    /// <param name="thresholdMs">Execution time threshold in milliseconds (default: 500)</param>
    /// <param name="pageNumber">Page number (1-based, default: 1)</param>
    /// <param name="pageSize">Number of items per page (default: 50, max: 100)</param>
    [HttpGet("performance/slow-queries")]
    [ProducesResponseType(typeof(PagedResult<SlowQuery>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetSlowQueries(
        [FromQuery] int thresholdMs = 500,
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 50)
    {
        try
        {
            // Validate pagination parameters
            if (pageNumber < 1)
            {
                return BadRequest(new { error = "Page number must be greater than 0" });
            }

            if (pageSize < 1 || pageSize > 100)
            {
                return BadRequest(new { error = "Page size must be between 1 and 100" });
            }

            var pagination = new PaginationOptions
            {
                PageNumber = pageNumber,
                PageSize = pageSize
            };

            var result = await _performanceMonitor.GetSlowQueriesAsync(thresholdMs, pagination);
            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get slow queries");
            return StatusCode(500, new { error = "Failed to retrieve slow queries" });
        }
    }

    /// <summary>
    /// Get the current audit queue depth.
    /// </summary>
    /// <remarks>
    /// Returns the number of audit events currently queued for processing.
    /// High queue depth may indicate:
    /// - Database performance issues
    /// - High system load
    /// - Need for backpressure application
    /// </remarks>
    [HttpGet("audit-queue-depth")]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    public IActionResult GetAuditQueueDepth()
    {
        try
        {
            var queueDepth = _memoryMonitor.GetAuditQueueDepth();
            var maxQueueSize = 10000; // Should match AuditLoggingOptions.MaxQueueSize
            var utilizationPercent = maxQueueSize > 0 ? (double)queueDepth / maxQueueSize * 100 : 0;
            
            return Ok(new
            {
                queueDepth,
                maxQueueSize,
                utilizationPercent,
                status = utilizationPercent >= 90 ? "Critical" :
                         utilizationPercent >= 70 ? "Warning" :
                         "Healthy"
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get audit queue depth");
            return StatusCode(500, new { error = "Failed to retrieve audit queue depth" });
        }
    }

    /// <summary>
    /// Get all active security threats that have not been resolved.
    /// </summary>
    /// <remarks>
    /// Returns a list of active security threats including:
    /// - Failed login patterns
    /// - Unauthorized access attempts
    /// - SQL injection attempts
    /// - XSS attempts
    /// - Anomalous user activity
    /// 
    /// Results are ordered by severity (Critical first) and detection time (newest first).
    /// </remarks>
    /// <param name="pageNumber">Page number (1-based, default: 1)</param>
    /// <param name="pageSize">Number of items per page (default: 50, max: 100)</param>
    [HttpGet("security/threats")]
    [ProducesResponseType(typeof(PagedResult<SecurityThreat>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetActiveSecurityThreats(
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 50)
    {
        try
        {
            // Validate pagination parameters
            if (pageNumber < 1)
            {
                return BadRequest(new { error = "Page number must be greater than 0" });
            }

            if (pageSize < 1 || pageSize > 100)
            {
                return BadRequest(new { error = "Page size must be between 1 and 100" });
            }

            var pagination = new PaginationOptions
            {
                PageNumber = pageNumber,
                PageSize = pageSize
            };

            var result = await _securityMonitor.GetActiveThreatsAsync(pagination);
            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get active security threats");
            return StatusCode(500, new { error = "Failed to retrieve active security threats" });
        }
    }

    /// <summary>
    /// Generate a daily security summary report for a specific date.
    /// </summary>
    /// <param name="date">Date to generate the summary report for (default: today)</param>
    /// <remarks>
    /// Returns a comprehensive security summary including:
    /// - Total threat count by type and severity
    /// - Top threat sources (IP addresses)
    /// - Resolution statistics
    /// - Trend analysis compared to previous day
    /// 
    /// Used for daily security monitoring and trend analysis.
    /// </remarks>
    [HttpGet("security/daily-summary")]
    [ProducesResponseType(typeof(SecuritySummaryReport), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetDailySecuritySummary([FromQuery] DateTime? date = null)
    {
        try
        {
            var reportDate = date ?? DateTime.UtcNow.Date;
            var summary = await _securityMonitor.GenerateDailySummaryAsync(reportDate);
            return Ok(summary);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to generate daily security summary for {Date}", date);
            return StatusCode(500, new { error = "Failed to generate daily security summary" });
        }
    }

    /// <summary>
    /// Check for failed login patterns from a specific IP address.
    /// </summary>
    /// <param name="ipAddress">IP address to check for failed login patterns</param>
    /// <remarks>
    /// Checks if the specified IP address has exceeded the failed login threshold
    /// within the configured time window. Returns a SecurityThreat if detected.
    /// 
    /// Uses Redis sliding window for distributed tracking when available,
    /// falls back to database queries.
    /// </remarks>
    [HttpGet("security/check-failed-logins")]
    [ProducesResponseType(typeof(SecurityThreat), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> CheckFailedLoginPattern([FromQuery] string ipAddress)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(ipAddress))
            {
                return BadRequest(new { error = "IP address parameter is required" });
            }

            var threat = await _securityMonitor.DetectFailedLoginPatternAsync(ipAddress);
            
            if (threat == null)
            {
                return NotFound(new { message = "No failed login pattern detected for this IP address" });
            }

            return Ok(threat);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to check failed login pattern for IP {IpAddress}", ipAddress);
            return StatusCode(500, new { error = "Failed to check failed login pattern" });
        }
    }

    /// <summary>
    /// Get the count of failed login attempts for a specific user.
    /// </summary>
    /// <param name="username">Username to check for failed login attempts</param>
    /// <remarks>
    /// Returns the count of failed login attempts for the specified user
    /// within the configured time window (typically 5 minutes).
    /// 
    /// Supports per-user rate limiting in addition to per-IP rate limiting.
    /// Uses Redis sliding window when available, falls back to database.
    /// </remarks>
    [HttpGet("security/failed-login-count")]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetFailedLoginCount([FromQuery] string username)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(username))
            {
                return BadRequest(new { error = "Username parameter is required" });
            }

            var count = await _securityMonitor.GetFailedLoginCountForUserAsync(username);
            
            return Ok(new
            {
                username,
                failedLoginCount = count,
                threshold = 5, // Should match SecurityMonitoringOptions.FailedLoginThreshold
                status = count >= 5 ? "Blocked" : count >= 3 ? "Warning" : "Normal"
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get failed login count for user {Username}", username);
            return StatusCode(500, new { error = "Failed to get failed login count" });
        }
    }

    /// <summary>
    /// Detect SQL injection patterns in input text.
    /// </summary>
    /// <param name="input">Input text to scan for SQL injection patterns</param>
    /// <remarks>
    /// Scans the input for common SQL injection patterns including:
    /// - UNION statements
    /// - SELECT/INSERT/UPDATE/DELETE keywords
    /// - Comment sequences (-- and /* */)
    /// - String concatenation attempts
    /// 
    /// Returns a SecurityThreat if SQL injection pattern is detected.
    /// </remarks>
    [HttpPost("security/check-sql-injection")]
    [ProducesResponseType(typeof(SecurityThreat), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> CheckSqlInjection([FromBody] string input)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(input))
            {
                return BadRequest(new { error = "Input parameter is required" });
            }

            var threat = await _securityMonitor.DetectSqlInjectionAsync(input);
            
            if (threat == null)
            {
                return NotFound(new { message = "No SQL injection pattern detected" });
            }

            return Ok(threat);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to check SQL injection pattern");
            return StatusCode(500, new { error = "Failed to check SQL injection pattern" });
        }
    }

    /// <summary>
    /// Detect XSS (Cross-Site Scripting) patterns in input text.
    /// </summary>
    /// <param name="input">Input text to scan for XSS patterns</param>
    /// <remarks>
    /// Scans the input for common XSS patterns including:
    /// - Script tags
    /// - Event handlers (onclick, onerror, etc.)
    /// - JavaScript protocol URLs
    /// - Data URIs with scripts
    /// 
    /// Returns a SecurityThreat if XSS pattern is detected.
    /// </remarks>
    [HttpPost("security/check-xss")]
    [ProducesResponseType(typeof(SecurityThreat), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> CheckXss([FromBody] string input)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(input))
            {
                return BadRequest(new { error = "Input parameter is required" });
            }

            var threat = await _securityMonitor.DetectXssAsync(input);
            
            if (threat == null)
            {
                return NotFound(new { message = "No XSS pattern detected" });
            }

            return Ok(threat);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to check XSS pattern");
            return StatusCode(500, new { error = "Failed to check XSS pattern" });
        }
    }

    /// <summary>
    /// Detect anomalous activity for a specific user.
    /// </summary>
    /// <param name="userId">User ID to check for anomalous activity</param>
    /// <remarks>
    /// Checks for unusual patterns such as:
    /// - Unusually high API request volumes
    /// - Requests at unusual times (outside normal working hours)
    /// - Rapid succession of different operations
    /// - Geographic anomalies (requests from unusual locations)
    /// 
    /// Returns a SecurityThreat if anomalous activity is detected.
    /// </remarks>
    [HttpGet("security/check-anomalous-activity")]
    [ProducesResponseType(typeof(SecurityThreat), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> CheckAnomalousActivity([FromQuery] long userId)
    {
        try
        {
            if (userId <= 0)
            {
                return BadRequest(new { error = "Valid user ID is required" });
            }

            var threat = await _securityMonitor.DetectAnomalousActivityAsync(userId);
            
            if (threat == null)
            {
                return NotFound(new { message = "No anomalous activity detected for this user" });
            }

            return Ok(threat);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to check anomalous activity for user {UserId}", userId);
            return StatusCode(500, new { error = "Failed to check anomalous activity" });
        }
    }

    /// <summary>
    /// Get detailed Oracle connection pool metrics.
    /// </summary>
    /// <remarks>
    /// Returns comprehensive connection pool information including:
    /// - Active and idle connection counts
    /// - Pool size configuration (min/max)
    /// - Connection timeout settings
    /// - Pool utilization percentage
    /// - Health status and recommendations
    /// 
    /// Requires SELECT privilege on V$SESSION for real-time statistics.
    /// Falls back to configuration-only metrics if privileges are insufficient.
    /// </remarks>
    [HttpGet("performance/connection-pool")]
    [ProducesResponseType(typeof(ConnectionPoolMetrics), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetConnectionPoolMetrics()
    {
        try
        {
            var metrics = await _performanceMonitor.GetConnectionPoolMetricsAsync();
            return Ok(metrics);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get connection pool metrics");
            return StatusCode(500, new { error = "Failed to retrieve connection pool metrics" });
        }
    }

    /// <summary>
    /// Get comprehensive audit logging system metrics.
    /// </summary>
    /// <remarks>
    /// Returns detailed metrics about the audit logging system including:
    /// - Queue depth and capacity
    /// - Circuit breaker state
    /// - Success/failure rates
    /// - Pending fallback files
    /// - Processing status
    /// 
    /// Use this endpoint to monitor audit logging health and detect issues.
    /// </remarks>
    [HttpGet("audit/metrics")]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAuditMetrics()
    {
        try
        {
            // Get queue depth
            int queueDepth = 0;
            if (_auditLogger is ThinkOnErp.Infrastructure.Services.AuditLogger auditLogger)
            {
                queueDepth = auditLogger.GetQueueDepth();
            }

            // Get resilient logger metrics if available
            object? resilientMetrics = null;
            if (_auditLogger is ThinkOnErp.Infrastructure.Services.ResilientAuditLogger resilientLogger)
            {
                var metrics = resilientLogger.GetMetrics();
                resilientMetrics = new
                {
                    totalRequests = metrics.TotalRequests,
                    successfulRequests = metrics.SuccessfulRequests,
                    failedRequests = metrics.FailedRequests,
                    circuitBreakerRejections = metrics.CircuitBreakerRejections,
                    retriedRequests = metrics.RetriedRequests,
                    circuitState = metrics.CircuitState.ToString(),
                    successRate = metrics.SuccessRate,
                    failureRate = metrics.FailureRate,
                    rejectionRate = metrics.RejectionRate
                };
                
                queueDepth = resilientLogger.GetQueueDepth();
            }

            // Check health
            var isHealthy = await _auditLogger.IsHealthyAsync();

            // Get fallback status if available
            int pendingFallbackCount = 0;
            if (_auditLogger is ThinkOnErp.Infrastructure.Services.ResilientAuditLogger resilientLogger2)
            {
                pendingFallbackCount = resilientLogger2.GetPendingFallbackCount();
            }

            const int maxQueueSize = 10000; // Should match AuditLoggingOptions.MaxQueueSize
            var utilizationPercent = maxQueueSize > 0 ? (double)queueDepth / maxQueueSize * 100 : 0;

            var response = new
            {
                queueDepth,
                queueCapacity = maxQueueSize,
                queueUtilizationPercent = utilizationPercent,
                isHealthy,
                pendingFallbackFiles = pendingFallbackCount,
                resilience = resilientMetrics,
                status = !isHealthy ? "Unhealthy" :
                         utilizationPercent >= 100 ? "Critical - Queue Full" :
                         utilizationPercent >= 90 ? "Warning - High Queue Depth" :
                         pendingFallbackCount > 0 ? "Warning - Fallback Active" :
                         "Healthy",
                timestamp = DateTime.UtcNow
            };

            // Trigger alerts based on metrics
            await CheckAndTriggerAuditAlertsAsync(queueDepth, maxQueueSize, isHealthy, pendingFallbackCount, resilientMetrics);

            return Ok(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get audit metrics");
            return StatusCode(500, new { error = "Failed to retrieve audit metrics" });
        }
    }

    /// <summary>
    /// Get audit logging fallback status.
    /// </summary>
    /// <remarks>
    /// Returns information about fallback file storage including:
    /// - Whether fallback is currently active
    /// - Number of pending fallback files
    /// - Oldest file timestamp
    /// - Total pending events estimate
    /// - Fallback directory path
    /// </remarks>
    [HttpGet("audit/fallback-status")]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    public IActionResult GetAuditFallbackStatus()
    {
        try
        {
            int pendingFileCount = 0;
            bool fallbackActive = false;
            string? circuitState = null;

            if (_auditLogger is ThinkOnErp.Infrastructure.Services.ResilientAuditLogger resilientLogger)
            {
                pendingFileCount = resilientLogger.GetPendingFallbackCount();
                var metrics = resilientLogger.GetMetrics();
                circuitState = metrics.CircuitState.ToString();
                fallbackActive = circuitState == "Open";
            }

            return Ok(new
            {
                fallbackActive,
                pendingFileCount,
                circuitState,
                fallbackDirectory = "AuditFallback",
                status = fallbackActive ? "Active - Database Unavailable" :
                         pendingFileCount > 0 ? "Pending Replay" :
                         "Inactive",
                timestamp = DateTime.UtcNow
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get audit fallback status");
            return StatusCode(500, new { error = "Failed to retrieve audit fallback status" });
        }
    }

    /// <summary>
    /// Manually trigger replay of fallback audit events to database.
    /// </summary>
    /// <remarks>
    /// Attempts to replay all pending fallback events from file storage to the database.
    /// This should be called after database connectivity is restored.
    /// 
    /// Returns the number of successfully replayed events.
    /// </remarks>
    [HttpPost("audit/replay-fallback")]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    public async Task<IActionResult> ReplayFallbackEvents()
    {
        try
        {
            if (_auditLogger is not ThinkOnErp.Infrastructure.Services.ResilientAuditLogger resilientLogger)
            {
                return BadRequest(new { error = "Fallback replay is only available with ResilientAuditLogger" });
            }

            _logger.LogInformation("Manual fallback replay triggered by user: {UserId}", User.Identity?.Name);

            var replayedCount = await resilientLogger.ReplayFallbackEventsAsync();

            _logger.LogInformation("Fallback replay completed: {Count} events replayed", replayedCount);

            return Ok(new
            {
                message = "Fallback replay completed",
                replayedCount,
                timestamp = DateTime.UtcNow
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to replay fallback events");
            return StatusCode(500, new { error = "Failed to replay fallback events" });
        }
    }

    /// <summary>
    /// Test alert delivery by sending a test alert through configured channels.
    /// </summary>
    /// <param name="alertType">Type of alert to test (default: "Test")</param>
    /// <param name="severity">Severity level (Low, Medium, High, Critical)</param>
    /// <param name="channels">Comma-separated list of channels to test (Email, Webhook, SMS)</param>
    [HttpPost("test-alert")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> TestAlert(
        [FromQuery] string alertType = "Test",
        [FromQuery] string severity = "Low",
        [FromQuery] string channels = "Email")
    {
        try
        {
            _logger.LogInformation(
                "Test alert triggered by user: {UserId}, Type={AlertType}, Severity={Severity}, Channels={Channels}",
                User.Identity?.Name, alertType, severity, channels);

            var alert = new Alert
            {
                AlertType = alertType,
                Severity = severity,
                Title = $"Test Alert - {alertType}",
                Description = $"This is a test alert triggered manually by {User.Identity?.Name} at {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss UTC}.\n\n" +
                             $"Alert Type: {alertType}\n" +
                             $"Severity: {severity}\n" +
                             $"Channels: {channels}\n\n" +
                             "If you received this alert, your notification system is working correctly.",
                TriggeredAt = DateTime.UtcNow,
                CorrelationId = Guid.NewGuid().ToString(),
                Metadata = System.Text.Json.JsonSerializer.Serialize(new Dictionary<string, object>
                {
                    ["TestAlert"] = true,
                    ["TriggeredBy"] = User.Identity?.Name ?? "Unknown",
                    ["Channels"] = channels
                })
            };

            await _alertManager.TriggerAlertAsync(alert);

            return Ok(new
            {
                message = "Test alert sent successfully",
                alertType,
                severity,
                channels,
                timestamp = DateTime.UtcNow
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send test alert");
            return StatusCode(500, new { error = "Failed to send test alert" });
        }
    }

    /// <summary>
    /// Check and trigger alerts based on audit logging metrics.
    /// </summary>
    private async Task CheckAndTriggerAuditAlertsAsync(
        int queueDepth,
        int maxQueueSize,
        bool isHealthy,
        int pendingFallbackCount,
        object? resilientMetrics)
    {
        try
        {
            var utilizationPercent = maxQueueSize > 0 ? (double)queueDepth / maxQueueSize * 100 : 0;

            // Check for queue overflow (90% threshold)
            if (utilizationPercent >= 90 && utilizationPercent < 100)
            {
                await _alertManager.TriggerAlertAsync(new Alert
                {
                    AlertType = "AuditQueueOverflow",
                    Severity = "High",
                    Title = "Audit Queue Overflow Warning",
                    Description = $"Audit logging queue is at {utilizationPercent:F1}% capacity ({queueDepth}/{maxQueueSize}).\n\n" +
                                 "The system may be under heavy load or database writes are slower than event generation rate.\n\n" +
                                 "Recommended Actions:\n" +
                                 "- Monitor queue depth trend\n" +
                                 "- Check database query performance\n" +
                                 "- Review slow query logs\n" +
                                 "- Consider increasing batch size",
                    TriggeredAt = DateTime.UtcNow,
                    CorrelationId = Guid.NewGuid().ToString()
                });
            }

            // Check for queue full (100% threshold)
            if (utilizationPercent >= 100)
            {
                await _alertManager.TriggerAlertAsync(new Alert
                {
                    AlertType = "AuditQueueFull",
                    Severity = "Critical",
                    Title = "Audit Queue Full - Critical",
                    Description = $"Audit logging queue is completely full ({queueDepth}/{maxQueueSize}).\n\n" +
                                 "Backpressure is being applied which may impact API response times.\n\n" +
                                 "IMMEDIATE Actions Required:\n" +
                                 "- Check database connectivity\n" +
                                 "- Verify circuit breaker state\n" +
                                 "- Review database performance metrics\n" +
                                 "- Check for blocking transactions",
                    TriggeredAt = DateTime.UtcNow,
                    CorrelationId = Guid.NewGuid().ToString()
                });
            }

            // Check for health check failure
            if (!isHealthy)
            {
                await _alertManager.TriggerAlertAsync(new Alert
                {
                    AlertType = "AuditHealthCheckFailed",
                    Severity = "Critical",
                    Title = "Audit Logging Health Check Failed",
                    Description = "Audit logging system health check has failed.\n\n" +
                                 "The system may not be capturing audit events.\n\n" +
                                 "CRITICAL Actions Required:\n" +
                                 "- Check application logs for exceptions\n" +
                                 "- Verify background service is running\n" +
                                 "- Check database connectivity\n" +
                                 "- Consider restarting the application",
                    TriggeredAt = DateTime.UtcNow,
                    CorrelationId = Guid.NewGuid().ToString()
                });
            }

            // Check for fallback activation
            if (pendingFallbackCount > 0)
            {
                await _alertManager.TriggerAlertAsync(new Alert
                {
                    AlertType = "AuditFallbackActivated",
                    Severity = "High",
                    Title = "Audit Fallback Storage Activated",
                    Description = $"Audit logging has fallen back to file system storage.\n\n" +
                                 $"Pending fallback files: {pendingFallbackCount}\n\n" +
                                 "Database is unavailable or experiencing severe issues.\n\n" +
                                 "Actions Required:\n" +
                                 "- Restore database connectivity\n" +
                                 "- Monitor fallback directory size\n" +
                                 "- Check disk space on application server\n" +
                                 "- Fallback events will automatically replay after recovery",
                    TriggeredAt = DateTime.UtcNow,
                    CorrelationId = Guid.NewGuid().ToString()
                });
            }

            // Check circuit breaker state if resilient metrics available
            if (resilientMetrics != null)
            {
                var metricsType = resilientMetrics.GetType();
                var circuitStateProperty = metricsType.GetProperty("circuitState");
                var circuitState = circuitStateProperty?.GetValue(resilientMetrics)?.ToString();

                if (circuitState == "Open")
                {
                    await _alertManager.TriggerAlertAsync(new Alert
                    {
                        AlertType = "AuditCircuitBreakerOpen",
                        Severity = "Critical",
                        Title = "Audit Circuit Breaker Open",
                        Description = "Audit logging circuit breaker is open due to repeated database failures.\n\n" +
                                     "Audit writes are being queued to fallback storage instead of the database.\n\n" +
                                     "Actions Required:\n" +
                                     "- Check database connectivity and health\n" +
                                     "- Review database logs for errors\n" +
                                     "- Check connection pool utilization\n" +
                                     "- Verify database disk space\n" +
                                     "- Circuit breaker will automatically close when database recovers",
                        TriggeredAt = DateTime.UtcNow,
                        CorrelationId = Guid.NewGuid().ToString()
                    });
                }
            }
        }
        catch (Exception ex)
        {
            // Don't let alert triggering failures break monitoring
            _logger.LogError(ex, "Failed to check and trigger audit alerts");
        }
    }
}

