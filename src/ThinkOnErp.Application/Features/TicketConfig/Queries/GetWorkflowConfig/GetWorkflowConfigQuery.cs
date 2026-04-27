using MediatR;
using ThinkOnErp.Application.DTOs.TicketConfig;

namespace ThinkOnErp.Application.Features.TicketConfig.Queries.GetWorkflowConfig;

/// <summary>
/// Query to retrieve workflow configuration settings
/// </summary>
public class GetWorkflowConfigQuery : IRequest<WorkflowConfigDto>
{
}
