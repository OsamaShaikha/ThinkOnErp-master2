using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using ThinkOnErp.Domain.Interfaces;

namespace ThinkOnErp.Infrastructure.Services;

/// <summary>
/// Background service that periodically monitors tickets for SLA compliance.
/// Runs on a configurable interval to check for tickets approaching or exceeding SLA deadlines.
/// </summary>
public class SlaEscalationBackgroundService : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<SlaEscalationBackgroundService> _logger;
    private readonly IConfiguration _configuration;

    // Configuration keys
    private const string EnabledKey = "SlaEscalation:BackgroundService:Enabled";
    private const string IntervalMinutesKey = "SlaEscalation:BackgroundService:IntervalMinutes";
    private const int DefaultIntervalMinutes = 30;

    public SlaEscalationBackgroundService(
        IServiceProvider serviceProvider,
        ILogger<SlaEscalationBackgroundService> logger,
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
            _logger.LogInformation("SLA escalation background service is disabled");
            return;
        }

        _logger.LogInformation("SLA escalation background service started");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ProcessSlaEscalationsAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error occurred during SLA escalation processing");
            }

            // Wait for the configured interval before next execution
            var intervalMinutes = GetIntervalMinutes();
            _logger.LogDebug("Next SLA escalation check in {Minutes} minutes", intervalMinutes);
            
            await Task.Delay(TimeSpan.FromMinutes(intervalMinutes), stoppingToken);
        }

        _logger.LogInformation("SLA escalation background service stopped");
    }

    private async Task ProcessSlaEscalationsAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Starting SLA escalation check cycle");

        // Create a scope to resolve scoped services
        using var scope = _serviceProvider.CreateScope();
        var escalationService = scope.ServiceProvider.GetRequiredService<ISlaEscalationService>();

        try
        {
            await escalationService.CheckAndEscalateOverdueTicketsAsync();
            _logger.LogInformation("SLA escalation check cycle completed successfully");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to complete SLA escalation check cycle");
            throw;
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
        _logger.LogInformation("SLA escalation background service is stopping");
        return base.StopAsync(cancellationToken);
    }
}
