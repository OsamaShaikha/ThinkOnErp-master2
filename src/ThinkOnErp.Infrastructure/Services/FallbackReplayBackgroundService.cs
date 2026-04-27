using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace ThinkOnErp.Infrastructure.Services;

/// <summary>
/// Background service that periodically checks for fallback audit events
/// and replays them to the database when it becomes available.
/// 
/// Implements Task 16.4: Fallback event replay mechanism
/// </summary>
public class FallbackReplayBackgroundService : BackgroundService
{
    private readonly ILogger<FallbackReplayBackgroundService> _logger;
    private readonly ResilientAuditLogger _resilientAuditLogger;
    private readonly FallbackReplayOptions _options;

    public FallbackReplayBackgroundService(
        ILogger<FallbackReplayBackgroundService> logger,
        ResilientAuditLogger resilientAuditLogger,
        FallbackReplayOptions? options = null)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _resilientAuditLogger = resilientAuditLogger ?? throw new ArgumentNullException(nameof(resilientAuditLogger));
        _options = options ?? new FallbackReplayOptions();
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation(
            "FallbackReplayBackgroundService started. Check interval: {Interval} seconds",
            _options.CheckIntervalSeconds);

        // Wait for initial delay before first check
        await Task.Delay(TimeSpan.FromSeconds(_options.InitialDelaySeconds), stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await CheckAndReplayFallbackEventsAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in FallbackReplayBackgroundService");
            }

            // Wait for next check interval
            await Task.Delay(TimeSpan.FromSeconds(_options.CheckIntervalSeconds), stoppingToken);
        }

        _logger.LogInformation("FallbackReplayBackgroundService stopped");
    }

    private async Task CheckAndReplayFallbackEventsAsync(CancellationToken cancellationToken)
    {
        try
        {
            // Check if there are pending fallback files
            var pendingCount = _resilientAuditLogger.GetPendingFallbackCount();
            
            if (pendingCount == 0)
            {
                _logger.LogDebug("No pending fallback files to replay");
                return;
            }

            _logger.LogInformation("Found {Count} pending fallback files, attempting replay", pendingCount);

            // Check if the audit logger is healthy (database is available)
            var isHealthy = await _resilientAuditLogger.IsHealthyAsync();
            
            if (!isHealthy)
            {
                _logger.LogWarning(
                    "Audit logger is not healthy, skipping replay. Will retry in {Interval} seconds",
                    _options.CheckIntervalSeconds);
                return;
            }

            // Attempt to replay fallback events
            var replayedCount = await _resilientAuditLogger.ReplayFallbackEventsAsync(cancellationToken);

            if (replayedCount > 0)
            {
                _logger.LogInformation(
                    "Successfully replayed {Replayed} out of {Total} fallback events",
                    replayedCount, pendingCount);
            }
            else
            {
                _logger.LogWarning("Failed to replay any fallback events");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to check and replay fallback events");
        }
    }
}

/// <summary>
/// Configuration options for FallbackReplayBackgroundService.
/// </summary>
public class FallbackReplayOptions
{
    /// <summary>
    /// Initial delay before first replay check in seconds.
    /// Default: 60 seconds
    /// </summary>
    public int InitialDelaySeconds { get; set; } = 60;

    /// <summary>
    /// Interval between replay checks in seconds.
    /// Default: 300 seconds (5 minutes)
    /// </summary>
    public int CheckIntervalSeconds { get; set; } = 300;
}
