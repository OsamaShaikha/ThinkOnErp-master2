using MediatR;
using ThinkOnErp.Application.DTOs.TicketConfig;

namespace ThinkOnErp.Application.Features.TicketConfig.Queries.GetSlaConfig;

/// <summary>
/// Query to retrieve SLA configuration settings
/// </summary>
public class GetSlaConfigQuery : IRequest<SlaConfigDto>
{
}
