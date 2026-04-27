using Cronos;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ThinkOnErp.Domain.Interfaces;
using ThinkOnErp.Infrastructure.Configuration;

namespace ThinkOnErp.Infrastructure.Services;

/// <summary>
/// Background service that manages automated archival of audit data based on retention policies.
/// Runs on a configurable cron schedule (default: daily at 2 AM) to archive expired audit data.
/// Processes data in batches to avoid long-running transactions and minimize performance impact.
/// Integrates with IArchivalService to perform the actual archival operations.
/// </summary>
public class ArchivalBackgroundService : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<ArchivalBackgroundService> _logger;
    private readonly ArchivalOptions _options;
    private readonly CronExpression _cronExpression;
    private readonly TimeZoneInfo _timeZone;

    public ArchivalBackgroundService(
        IServiceProvider serviceProvider,
        ILogger<ArchivalBackgroundService> logger,
        IOptions<ArchivalOptions> options)
    {
        _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _options = options?.Value ?? throw new ArgumentNullException(nameof(options));

        // Parse and validate cron expression
        try
        {
            _cronExpression = CronExpression.Parse(_options.Schedule, CronFormat.Standard);
            _logger.LogInformation("Archival service initialized with schedule: {Schedule}", _options.Schedule);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Invalid cron expression: {Schedule}. Using default schedule.", _options.Schedule);
            _cronExpression = CronExpression.Parse("0 2 * * *", CronFormat.Standard); // Default: daily at 2 AM
        }

        // Parse time zone
        try
        {
            _timeZone = TimeZoneInfo.FindSystemTimeZoneById(_options.TimeZone);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Invalid time zone: {TimeZone}. Using UTC.", _options.TimeZone);
            _timeZone = TimeZoneInfo.Utc;
        }
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_options.Enabled)
        {
            _logger.LogInformation("Archival background service is disabled");
            return;
        }

        _logger.LogInformation(
            "Archival background service started with schedule: {Schedule} (TimeZone: {TimeZone})",
            _options.Schedule,
            _timeZone.Id);

        // Run immediately on startup if configured (useful for testing)
        if (_options.RunOnStartup)
        {
            _logger.LogInformation("RunOnStartup is enabled, executing archival immediately");
            await ExecuteArchivalAsync(stoppingToken);
        }

        // Main scheduling loop
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                // Calculate next occurrence based on cron expression
                var now = TimeZoneInfo.ConvertTime(DateTime.UtcNow, _timeZone);
                var nextOccurrence = _cronExpression.GetNextOccurrence(now, _timeZone);

                if (nextOccurrence.HasValue)
                {
                    var delay = nextOccurrence.Value - now;
                    
                    if (delay.TotalMilliseconds > 0)
                    {
                        _logger.LogInformation(
                            "Next archival scheduled for {NextRun} ({TimeZone}), waiting {Delay}",
                            nextOccurrence.Value,
                            _timeZone.Id,
                            FormatDelay(delay));

                        await Task.Delay(delay, stoppingToken);
                    }

                    // Execute archival
                    if (!stoppingToken.IsCancellationRequested)
                    {
                        await ExecuteArchivalAsync(stoppingToken);
                    }
                }
                else
                {
                    _logger.LogWarning("No next occurrence found for cron expression: {Schedule}", _options.Schedule);
                    // Wait 1 hour before trying again
                    await Task.Delay(TimeSpan.FromHours(1), stoppingToken);
                }
            }
            catch (OperationCanceledException)
            {
                // Expected when service is stopping
                _logger.LogInformation("Archival background service is stopping");
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in archival scheduling loop");
                // Wait 5 minutes before retrying to avoid tight error loops
                await Task.Delay(TimeSpan.FromMinutes(5), stoppingToken);
            }
        }

        _logger.LogInformation("Archival background service stopped");
    }

    private async Task ExecuteArchivalAsync(CancellationToken cancellationToken)
    {
        var startTime = DateTime.UtcNow;
        _logger.LogInformation("Starting archival cycle at {StartTime} UTC", startTime);

        try
        {
            // Create a scope to resolve scoped services
            using var scope = _serviceProvider.CreateScope();
            var archivalService = scope.ServiceProvider.GetRequiredService<IArchivalService>();

            // Create a timeout cancellation token
            using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeoutCts.CancelAfter(TimeSpan.FromMinutes(_options.TimeoutMinutes));

            // Execute archival with timeout
            var results = await archivalService.ArchiveExpiredDataAsync(timeoutCts.Token);

            // Log results
            var totalRecordsArchived = results.Sum(r => r.RecordsArchived);
            var successfulArchives = results.Count(r => r.IsSuccess);
            var failedArchives = results.Count(r => !r.IsSuccess);

            if (failedArchives > 0)
            {
                _logger.LogWarning(
                    "Archival cycle completed with {SuccessCount} successes and {FailureCount} failures. " +
                    "Total records archived: {TotalRecords}",
                    successfulArchives,
                    failedArchives,
                    totalRecordsArchived);

                // Log details of failures
                foreach (var failedResult in results.Where(r => !r.IsSuccess))
                {
                    _logger.LogError(
                        "Archival failed for archive ID {ArchiveId}: {ErrorMessage}",
                        failedResult.ArchiveId,
                        failedResult.ErrorMessage);
                }
            }
            else
            {
                var duration = DateTime.UtcNow - startTime;
                _logger.LogInformation(
                    "Archival cycle completed successfully. Archived {TotalRecords} records across {ArchiveCount} archives in {Duration}ms",
                    totalRecordsArchived,
                    results.Count(),
                    duration.TotalMilliseconds);
            }

            // Log compression statistics if available
            var resultsWithCompression = results.Where(r => r.UncompressedSize > 0).ToList();
            if (resultsWithCompression.Any())
            {
                var avgCompressionRatio = resultsWithCompression.Average(r => r.CompressionRatio);
                var totalSaved = resultsWithCompression.Sum(r => r.UncompressedSize - r.CompressedSize);
                
                _logger.LogInformation(
                    "Compression statistics - Average ratio: {Ratio:P2}, Total space saved: {SavedMB:N2} MB",
                    avgCompressionRatio,
                    totalSaved / (1024.0 * 1024.0));
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            _logger.LogWarning("Archival cycle was cancelled due to service shutdown");
        }
        catch (OperationCanceledException)
        {
            _logger.LogError(
                "Archival cycle timed out after {TimeoutMinutes} minutes",
                _options.TimeoutMinutes);
        }
        catch (Exception ex)
        {
            var duration = DateTime.UtcNow - startTime;
            _logger.LogError(
                ex,
                "Archival cycle failed after {Duration}ms: {ErrorMessage}",
                duration.TotalMilliseconds,
                ex.Message);
        }
    }

    private static string FormatDelay(TimeSpan delay)
    {
        if (delay.TotalDays >= 1)
        {
            return $"{delay.TotalDays:N1} days";
        }
        else if (delay.TotalHours >= 1)
        {
            return $"{delay.TotalHours:N1} hours";
        }
        else if (delay.TotalMinutes >= 1)
        {
            return $"{delay.TotalMinutes:N1} minutes";
        }
        else
        {
            return $"{delay.TotalSeconds:N1} seconds";
        }
    }

    public override Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Archival background service is stopping gracefully");
        return base.StopAsync(cancellationToken);
    }
}
