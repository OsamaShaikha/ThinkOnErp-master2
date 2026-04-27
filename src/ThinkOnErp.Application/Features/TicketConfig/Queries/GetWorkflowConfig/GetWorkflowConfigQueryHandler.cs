using MediatR;
using ThinkOnErp.Application.DTOs.TicketConfig;
using ThinkOnErp.Application.Services;

namespace ThinkOnErp.Application.Features.TicketConfig.Queries.GetWorkflowConfig;

/// <summary>
/// Handler for GetWorkflowConfigQuery
/// </summary>
public class GetWorkflowConfigQueryHandler : IRequestHandler<GetWorkflowConfigQuery, WorkflowConfigDto>
{
    private readonly ITicketConfigurationService _configService;

    public GetWorkflowConfigQueryHandler(ITicketConfigurationService configService)
    {
        _configService = configService ?? throw new ArgumentNullException(nameof(configService));
    }

    public async Task<WorkflowConfigDto> Handle(GetWorkflowConfigQuery request, CancellationToken cancellationToken)
    {
        return new WorkflowConfigDto
        {
            AllowedStatusTransitions = await _configService.GetAllowedStatusTransitionsAsync(),
            AutoCloseResolvedAfterDays = await _configService.GetAutoCloseResolvedAfterDaysAsync()
        };
    }
}
