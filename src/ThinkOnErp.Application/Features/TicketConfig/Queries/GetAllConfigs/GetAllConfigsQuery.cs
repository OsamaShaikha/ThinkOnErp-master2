using MediatR;
using ThinkOnErp.Application.DTOs.TicketConfig;

namespace ThinkOnErp.Application.Features.TicketConfig.Queries.GetAllConfigs;

/// <summary>
/// Query to retrieve all ticket configuration settings
/// </summary>
public class GetAllConfigsQuery : IRequest<List<TicketConfigDto>>
{
}
