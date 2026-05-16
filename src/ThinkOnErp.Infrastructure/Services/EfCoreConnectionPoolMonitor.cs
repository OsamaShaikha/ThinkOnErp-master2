using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Oracle.ManagedDataAccess.Client;
using System.Data;
using ThinkOnErp.Infrastructure.Data;
using ThinkOnErp.Domain.Models;

namespace ThinkOnErp.Infrastructure.Services;

/// <summary>
/// Monitors EF Core connection pool usage and provides metrics.
/// Implements REQ-21: Monitoring and Observability
/// </summary>
public class EfCoreConnectionPoolMonitor
{
    private readonly ILogger<EfCoreConnectionPoolMonitor> _logger;
    private readonly ThinkOnErpDbContext _dbContext;

    public EfCoreConnectionPoolMonitor(
        ILogger<EfCoreConnectionPoolMonitor> logger,
        ThinkOnErpDbContext dbContext)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
    }

    /// <summary>
    /// Gets the current connection pool metrics.
    /// </summary>
    public async Task<ConnectionPoolMetrics> GetConnectionPoolMetricsAsync()
    {
        try
        {
            var connection = _dbContext.Database.GetDbConnection();
            
            if (connection is not OracleConnection oracleConnection)
            {
                _logger.LogWarning("Connection is not an OracleConnection, cannot retrieve pool metrics");
                return new ConnectionPoolMetrics
                {
                    MinPoolSize = 0,
                    MaxPoolSize = 0,
                    ActiveConnections = 0,
                    IdleConnections = 0,
                    ConnectionTimeoutSeconds = 0,
                    ConnectionLifetimeSeconds = 0,
                    ValidateConnection = false,
                    Timestamp = DateTime.UtcNow
                };
            }

            // Parse connection string to get pool configuration
            var connectionString = oracleConnection.ConnectionString;
            var builder = new OracleConnectionStringBuilder(connectionString);

            var minPoolSize = builder.MinPoolSize;
            var maxPoolSize = builder.MaxPoolSize;

            // Oracle doesn't expose current pool size directly, so we estimate based on connection state
            // In a production environment, you might use Oracle performance views or custom monitoring
            var metrics = new ConnectionPoolMetrics
            {
                MinPoolSize = minPoolSize,
                MaxPoolSize = maxPoolSize,
                ActiveConnections = 0, // Would need Oracle performance monitoring for accurate value
                IdleConnections = 0, // Would need Oracle performance monitoring for accurate value
                ConnectionTimeoutSeconds = builder.ConnectionTimeout,
                ConnectionLifetimeSeconds = builder.ConnectionLifeTime,
                ValidateConnection = builder.ValidateConnection,
                Timestamp = DateTime.UtcNow
            };

            // Test connection to verify pool is working
            if (connection.State != ConnectionState.Open)
            {
                await connection.OpenAsync();
                await connection.CloseAsync();
            }

            _logger.LogDebug(
                "Connection pool metrics retrieved. Min: {MinPoolSize}, Max: {MaxPoolSize}, Pooling: {Pooling}",
                minPoolSize,
                maxPoolSize,
                builder.Pooling);

            return metrics;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to retrieve connection pool metrics");
            return new ConnectionPoolMetrics
            {
                MinPoolSize = 0,
                MaxPoolSize = 0,
                ActiveConnections = 0,
                IdleConnections = 0,
                ConnectionTimeoutSeconds = 0,
                ConnectionLifetimeSeconds = 0,
                ValidateConnection = false,
                Timestamp = DateTime.UtcNow
            };
        }
    }

    /// <summary>
    /// Checks if the connection pool is healthy.
    /// </summary>
    public async Task<bool> IsHealthyAsync()
    {
        try
        {
            // Test database connectivity
            var canConnect = await _dbContext.Database.CanConnectAsync();
            
            if (!canConnect)
            {
                _logger.LogWarning("EF Core DbContext cannot connect to database");
                return false;
            }

            // Execute a simple query to verify connection pool is working
            await _dbContext.Database.ExecuteSqlRawAsync("SELECT 1 FROM DUAL");
            
            _logger.LogDebug("EF Core connection pool health check passed");
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "EF Core connection pool health check failed");
            return false;
        }
    }

    /// <summary>
    /// Gets detailed connection information for diagnostics.
    /// </summary>
    public async Task<ConnectionDiagnostics> GetConnectionDiagnosticsAsync()
    {
        var diagnostics = new ConnectionDiagnostics
        {
            Timestamp = DateTime.UtcNow
        };

        try
        {
            var connection = _dbContext.Database.GetDbConnection();
            
            diagnostics.ConnectionType = connection.GetType().Name;
            diagnostics.ConnectionState = connection.State.ToString();
            diagnostics.Database = connection.Database;
            diagnostics.DataSource = connection.DataSource;
            diagnostics.ServerVersion = connection.ServerVersion;

            // Test connectivity
            var stopwatch = System.Diagnostics.Stopwatch.StartNew();
            diagnostics.CanConnect = await _dbContext.Database.CanConnectAsync();
            stopwatch.Stop();
            diagnostics.ConnectionTestDurationMs = stopwatch.ElapsedMilliseconds;

            if (diagnostics.CanConnect)
            {
                // Execute test query
                stopwatch.Restart();
                await _dbContext.Database.ExecuteSqlRawAsync("SELECT 1 FROM DUAL");
                stopwatch.Stop();
                diagnostics.QueryTestDurationMs = stopwatch.ElapsedMilliseconds;
            }

            _logger.LogDebug(
                "Connection diagnostics completed. CanConnect: {CanConnect}, ConnectionTime: {ConnectionTime}ms, QueryTime: {QueryTime}ms",
                diagnostics.CanConnect,
                diagnostics.ConnectionTestDurationMs,
                diagnostics.QueryTestDurationMs);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to retrieve connection diagnostics");
            diagnostics.ErrorMessage = ex.Message;
        }

        return diagnostics;
    }
}

/// <summary>
/// Connection diagnostics information.
/// </summary>
public class ConnectionDiagnostics
{
    public string ConnectionType { get; set; } = string.Empty;
    public string ConnectionState { get; set; } = string.Empty;
    public string Database { get; set; } = string.Empty;
    public string DataSource { get; set; } = string.Empty;
    public string ServerVersion { get; set; } = string.Empty;
    public bool CanConnect { get; set; }
    public long ConnectionTestDurationMs { get; set; }
    public long QueryTestDurationMs { get; set; }
    public string? ErrorMessage { get; set; }
    public DateTime Timestamp { get; set; }
}
