using MediatR;
using Microsoft.Extensions.Logging;
using ThinkOnErp.Application.Services;

namespace ThinkOnErp.Application.Features.TicketConfig.Commands.UpdateSlaConfig;

/// <summary>
/// Handler for UpdateSlaConfigCommand
/// Updates all SLA configuration settings in a single transaction
/// </summary>
public class UpdateSlaConfigCommandHandler : IRequestHandler<UpdateSlaConfigCommand, bool>
{
    private readonly ITicketConfigurationService _configService;
    private readonly ILogger<UpdateSlaConfigCommandHandler> _logger;

    public UpdateSlaConfigCommandHandler(
        ITicketConfigurationService configService,
        ILogger<UpdateSlaConfigCommandHandler> logger)
    {
        _configService = configService ?? throw new ArgumentNullException(nameof(configService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<bool> Handle(UpdateSlaConfigCommand request, CancellationToken cancellationToken)
    {
        _logger.LogInformation(
            "Updating SLA configuration by user {User}: Low={Low}h, Medium={Medium}h, High={High}h, Critical={Critical}h, Escalation={Escalation}%",
            request.UpdateUser,
            request.LowPriorityHours,
            request.MediumPriorityHours,
            request.HighPriorityHours,
            request.CriticalPriorityHours,
            request.EscalationThresholdPercentage);

        try
        {
            // Update each SLA configuration setting
            var lowResult = await _configService.UpdateConfigValueAsync(
                "SLA.Priority.Low.Hours",
                request.LowPriorityHours.ToString(),
                request.UpdateUser);

            var mediumResult = await _configService.UpdateConfigValueAsync(
                "SLA.Priority.Medium.Hours",
                request.MediumPriorityHours.ToString(),
                request.UpdateUser);

            var highResult = await _configService.UpdateConfigValueAsync(
                "SLA.Priority.High.Hours",
                request.HighPriorityHours.ToString(),
                request.UpdateUser);

            var criticalResult = await _configService.UpdateConfigValueAsync(
                "SLA.Priority.Critical.Hours",
                request.CriticalPriorityHours.ToString(),
                request.UpdateUser);

            var escalationResult = await _configService.UpdateConfigValueAsync(
                "SLA.Escalation.Threshold.Percentage",
                request.EscalationThresholdPercentage.ToString(),
                request.UpdateUser);

            var allSuccess = lowResult && mediumResult && highResult && criticalResult && escalationResult;

            if (allSuccess)
            {
                _logger.LogInformation("SLA configuration updated successfully by user {User}", request.UpdateUser);
            }
            else
            {
                _logger.LogWarning("Failed to update one or more SLA configuration settings");
            }

            return allSuccess;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating SLA configuration");
            throw;
        }
    }
}
