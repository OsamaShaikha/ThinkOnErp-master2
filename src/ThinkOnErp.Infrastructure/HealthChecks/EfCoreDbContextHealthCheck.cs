using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Logging;
using ThinkOnErp.Infrastructure.Data;

namespace ThinkOnErp.Infrastructure.HealthChecks;

/// <summary>
/// Health check for EF Core DbContext connectivity and database operations.
/// Implements REQ-21: Monitoring and Observability - Health Check Endpoints
/// </summary>
public class EfCoreDbContextHealthCheck : IHealthCheck
{
    private readonly ThinkOnErpDbContext _dbContext;
    private readonly ILogger<EfCoreDbContextHealthCheck> _logger;
    private readonly string _testQuery;
    private readonly bool _checkConnectionPool;
    private readonly int _timeoutSeconds;

    public EfCoreDbContextHealthCheck(
        ThinkOnErpDbContext dbContext,
        ILogger<EfCoreDbContextHealthCheck> logger,
        string testQuery = "SELECT 1 FROM DUAL",
        bool checkConnectionPool = true,
        int timeoutSeconds = 10)
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _testQuery = testQuery;
        _checkConnectionPool = checkConnectionPool;
        _timeoutSeconds = timeoutSeconds;
    }

    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var startTime = DateTime.UtcNow;
            var data = new Dictionary<string, object>();

            // Test database connectivity with a simple query
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(_timeoutSeconds));
            using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, cts.Token);

            var canConnect = await _dbContext.Database.CanConnectAsync(linkedCts.Token);
            
            if (!canConnect)
            {
                _logger.LogError("EF Core DbContext health check failed: Cannot connect to database");
                return HealthCheckResult.Unhealthy(
                    "Cannot connect to database",
                    data: data);
            }

            // Execute test query to verify database operations
            try
            {
                await _dbContext.Database.ExecuteSqlRawAsync(_testQuery, linkedCts.Token);
                var queryDuration = (DateTime.UtcNow - startTime).TotalMilliseconds;
                data["queryExecutionTimeMs"] = queryDuration;
                
                _logger.LogDebug("EF Core DbContext health check query executed in {Duration}ms", queryDuration);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "EF Core DbContext health check failed: Test query execution failed");
                return HealthCheckResult.Unhealthy(
                    $"Test query execution failed: {ex.Message}",
                    exception: ex,
                    data: data);
            }

            // Check connection pool status if enabled
            if (_checkConnectionPool)
            {
                try
                {
                    var connection = _dbContext.Database.GetDbConnection();
                    data["connectionState"] = connection.State.ToString();
                    data["connectionString"] = MaskConnectionString(connection.ConnectionString);
                    data["database"] = connection.Database;
                    
                    _logger.LogDebug("EF Core DbContext connection state: {State}", connection.State);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to retrieve connection pool information");
                    data["connectionPoolWarning"] = "Failed to retrieve connection pool information";
                }
            }

            var totalDuration = (DateTime.UtcNow - startTime).TotalMilliseconds;
            data["totalCheckDurationMs"] = totalDuration;
            data["timestamp"] = DateTime.UtcNow;

            _logger.LogInformation("EF Core DbContext health check passed in {Duration}ms", totalDuration);

            return HealthCheckResult.Healthy(
                "EF Core DbContext is healthy",
                data: data);
        }
        catch (OperationCanceledException)
        {
            _logger.LogError("EF Core DbContext health check timed out after {Timeout} seconds", _timeoutSeconds);
            return HealthCheckResult.Unhealthy(
                $"Health check timed out after {_timeoutSeconds} seconds");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "EF Core DbContext health check failed with unexpected error");
            return HealthCheckResult.Unhealthy(
                $"Health check failed: {ex.Message}",
                exception: ex);
        }
    }

    private static string MaskConnectionString(string? connectionString)
    {
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            return "N/A";
        }

        // Mask password in connection string for security
        var masked = System.Text.RegularExpressions.Regex.Replace(
            connectionString,
            @"(Password|Pwd)\s*=\s*[^;]+",
            "$1=***MASKED***",
            System.Text.RegularExpressions.RegexOptions.IgnoreCase);

        return masked;
    }
}
