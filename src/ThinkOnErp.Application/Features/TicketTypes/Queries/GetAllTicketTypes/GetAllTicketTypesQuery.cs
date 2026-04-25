using MediatR;
using ThinkOnErp.Application.DTOs.Ticket;

namespace ThinkOnErp.Application.Features.TicketTypes.Queries.GetAllTicketTypes;

/// <summary>
/// Query for retrieving all active ticket types.
/// </summary>
public class GetAllTicketTypesQuery : IRequest<List<TicketTypeDto>>
{
}
