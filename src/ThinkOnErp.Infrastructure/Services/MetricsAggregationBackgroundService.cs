using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using ThinkOnErp.Domain.Entities;
using ThinkOnErp.Domain.Interfaces;
using ThinkOnErp.Infrastructure.Data;

namespace ThinkOnErp.Infrastructure.Services;

/// <summary>
/// Background service that aggregates performance metrics hourly from the in-memory sliding window.
/// Stores aggregated metrics to the SYS_PERFORMANCE_METRICS database table for long-term analysis.
/// Calculates summary statistics (average, min, max, count, percentiles) per endpoint.
/// </summary>
public class MetricsAggregationBackgroundService : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<MetricsAggregationBackgroundService> _logger;
    private readonly IConfiguration _configuration;

    // Configuration keys
    private const string EnabledKey = "PerformanceMonitoring:MetricsAggregation:Enabled";
    private const string IntervalMinutesKey = "PerformanceMonitoring:MetricsAggregation:IntervalMinutes";
    private const int DefaultIntervalMinutes = 60; // Run hourly by default

    public MetricsAggregationBackgroundService(
        IServiceProvider serviceProvider,
        ILogger<MetricsAggregationBackgroundService> logger,
        IConfiguration configuration)
    {
        _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!IsBackgroundServiceEnabled())
        {
            _logger.LogInformation("Metrics aggregation background service is disabled");
            return;
        }

        _logger.LogInformation("Metrics aggregation background service started");

        // Wait until the top of the next hour to start
        await WaitUntilNextHourAsync(stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await AggregateMetricsAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error occurred during metrics aggregation");
            }

            // Wait for the configured interval before next execution
            var intervalMinutes = GetIntervalMinutes();
            _logger.LogDebug("Next metrics aggregation in {Minutes} minutes", intervalMinutes);
            
            await Task.Delay(TimeSpan.FromMinutes(intervalMinutes), stoppingToken);
        }

        _logger.LogInformation("Metrics aggregation background service stopped");
    }

    private async Task AggregateMetricsAsync(CancellationToken cancellationToken)
    {
        var aggregationStartTime = DateTime.UtcNow;
        _logger.LogInformation("Starting metrics aggregation cycle for hour ending {Time}", aggregationStartTime);

        // Create a scope to resolve scoped services
        using var scope = _serviceProvider.CreateScope();
        var performanceMonitor = scope.ServiceProvider.GetRequiredService<IPerformanceMonitor>();
        var dbContext = scope.ServiceProvider.GetRequiredService<OracleDbContext>();

        try
        {
            // Get the hour timestamp (truncated to the hour)
            var hourTimestamp = new DateTime(
                aggregationStartTime.Year,
                aggregationStartTime.Month,
                aggregationStartTime.Day,
                aggregationStartTime.Hour,
                0,
                0,
                DateTimeKind.Utc);

            // Get all endpoints that have metrics in the last hour
            var endpoints = await GetEndpointsWithMetricsAsync(performanceMonitor);

            if (!endpoints.Any())
            {
                _logger.LogInformation("No endpoints with metrics to aggregate");
                return;
            }

            _logger.LogInformation("Aggregating metrics for {Count} endpoints", endpoints.Count);

            var aggregatedCount = 0;
            foreach (var endpoint in endpoints)
            {
                try
                {
                    await AggregateEndpointMetricsAsync(
                        endpoint,
                        hourTimestamp,
                        performanceMonitor,
                        dbContext,
                        cancellationToken);
                    
                    aggregatedCount++;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to aggregate metrics for endpoint {Endpoint}", endpoint);
                }
            }

            var duration = DateTime.UtcNow - aggregationStartTime;
            _logger.LogInformation(
                "Metrics aggregation cycle completed successfully. Aggregated {Count} endpoints in {Duration}ms",
                aggregatedCount,
                duration.TotalMilliseconds);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to complete metrics aggregation cycle");
            throw;
        }
    }

    private async Task<List<string>> GetEndpointsWithMetricsAsync(IPerformanceMonitor performanceMonitor)
    {
        // Get all tracked endpoints from the performance monitor
        var endpoints = performanceMonitor.GetTrackedEndpoints().ToList();
        
        _logger.LogDebug("Found {Count} tracked endpoints", endpoints.Count);
        
        return endpoints;
    }

    private async Task AggregateEndpointMetricsAsync(
        string endpoint,
        DateTime hourTimestamp,
        IPerformanceMonitor performanceMonitor,
        OracleDbContext dbContext,
        CancellationToken cancellationToken)
    {
        // Get statistics for the last hour
        var statistics = await performanceMonitor.GetEndpointStatisticsAsync(endpoint, TimeSpan.FromHours(1));

        if (statistics.RequestCount == 0)
        {
            _logger.LogDebug("No requests for endpoint {Endpoint} in the last hour", endpoint);
            return;
        }

        // Get percentile metrics
        var percentiles = await performanceMonitor.GetPercentileMetricsAsync(endpoint, TimeSpan.FromHours(1));

        // Insert aggregated metrics into database
        await InsertAggregatedMetricsAsync(
            endpoint,
            hourTimestamp,
            statistics,
            percentiles,
            dbContext,
            cancellationToken);

        _logger.LogDebug(
            "Aggregated metrics for endpoint {Endpoint}: {RequestCount} requests, avg {AvgTime}ms",
            endpoint,
            statistics.RequestCount,
            statistics.AverageExecutionTimeMs);
    }

    private async Task InsertAggregatedMetricsAsync(
        string endpoint,
        DateTime hourTimestamp,
        Domain.Models.PerformanceStatistics statistics,
        Domain.Models.PercentileMetrics percentiles,
        OracleDbContext dbContext,
        CancellationToken cancellationToken)
    {
        var avgDatabaseTimeMs = statistics.RequestCount > 0
            ? (decimal)(statistics.AverageExecutionTimeMs * statistics.DatabaseTimePercentage / 100)
            : 0m;

        var metric = new SysPerformanceMetric
        {
            EndpointPath = endpoint,
            HourTimestamp = hourTimestamp,
            RequestCount = (int)statistics.RequestCount,
            AvgExecutionTimeMs = (decimal)statistics.AverageExecutionTimeMs,
            MinExecutionTimeMs = statistics.MinExecutionTimeMs,
            MaxExecutionTimeMs = statistics.MaxExecutionTimeMs,
            P50ExecutionTimeMs = percentiles.P50,
            P95ExecutionTimeMs = percentiles.P95,
            P99ExecutionTimeMs = percentiles.P99,
            AvgDatabaseTimeMs = avgDatabaseTimeMs,
            AvgQueryCount = (decimal)statistics.AverageQueryCount,
            ErrorCount = (decimal)statistics.ErrorCount,
            CreationDate = DateTime.UtcNow
        };

        dbContext.SysPerformanceMetrics.Add(metric);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private async Task WaitUntilNextHourAsync(CancellationToken cancellationToken)
    {
        var now = DateTime.UtcNow;
        var nextHour = new DateTime(now.Year, now.Month, now.Day, now.Hour, 0, 0, DateTimeKind.Utc)
            .AddHours(1);
        var delay = nextHour - now;

        if (delay.TotalMilliseconds > 0)
        {
            _logger.LogInformation("Waiting {Minutes} minutes until next hour to start aggregation", 
                delay.TotalMinutes);
            await Task.Delay(delay, cancellationToken);
        }
    }

    private bool IsBackgroundServiceEnabled()
    {
        return _configuration.GetValue<bool>(EnabledKey, true);
    }

    private int GetIntervalMinutes()
    {
        var intervalMinutes = _configuration.GetValue<int>(IntervalMinutesKey, DefaultIntervalMinutes);
        
        // Ensure minimum interval of 1 minute
        if (intervalMinutes < 1)
        {
            _logger.LogWarning("Configured interval {Interval} is too low, using minimum of 1 minute", intervalMinutes);
            return 1;
        }

        return intervalMinutes;
    }

    public override Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Metrics aggregation background service is stopping");
        return base.StopAsync(cancellationToken);
    }
}
