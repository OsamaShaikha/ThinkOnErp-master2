using MediatR;
using ThinkOnErp.Application.DTOs.TicketConfig;
using ThinkOnErp.Application.Services;

namespace ThinkOnErp.Application.Features.TicketConfig.Queries.GetSlaConfig;

/// <summary>
/// Handler for GetSlaConfigQuery
/// </summary>
public class GetSlaConfigQueryHandler : IRequestHandler<GetSlaConfigQuery, SlaConfigDto>
{
    private readonly ITicketConfigurationService _configService;

    public GetSlaConfigQueryHandler(ITicketConfigurationService configService)
    {
        _configService = configService ?? throw new ArgumentNullException(nameof(configService));
    }

    public async Task<SlaConfigDto> Handle(GetSlaConfigQuery request, CancellationToken cancellationToken)
    {
        return new SlaConfigDto
        {
            LowPriorityHours = await _configService.GetSlaTargetHoursAsync("Low"),
            MediumPriorityHours = await _configService.GetSlaTargetHoursAsync("Medium"),
            HighPriorityHours = await _configService.GetSlaTargetHoursAsync("High"),
            CriticalPriorityHours = await _configService.GetSlaTargetHoursAsync("Critical"),
            EscalationThresholdPercentage = await _configService.GetEscalationThresholdPercentageAsync()
        };
    }
}
