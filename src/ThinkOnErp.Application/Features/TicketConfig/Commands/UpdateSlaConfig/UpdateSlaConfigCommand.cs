using MediatR;
using ThinkOnErp.Application.DTOs.TicketConfig;

namespace ThinkOnErp.Application.Features.TicketConfig.Commands.UpdateSlaConfig;

/// <summary>
/// Command to update SLA configuration settings in bulk
/// </summary>
public class UpdateSlaConfigCommand : IRequest<bool>
{
    public decimal LowPriorityHours { get; set; }
    public decimal MediumPriorityHours { get; set; }
    public decimal HighPriorityHours { get; set; }
    public decimal CriticalPriorityHours { get; set; }
    public int EscalationThresholdPercentage { get; set; }
    public string UpdateUser { get; set; } = string.Empty;
}
