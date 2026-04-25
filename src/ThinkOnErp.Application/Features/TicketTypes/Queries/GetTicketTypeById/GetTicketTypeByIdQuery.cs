using MediatR;
using ThinkOnErp.Application.DTOs.Ticket;

namespace ThinkOnErp.Application.Features.TicketTypes.Queries.GetTicketTypeById;

/// <summary>
/// Query for retrieving a specific ticket type by its ID.
/// </summary>
public class GetTicketTypeByIdQuery : IRequest<TicketTypeDto?>
{
    /// <summary>
    /// Unique identifier of the ticket type
    /// </summary>
    public Int64 TicketTypeId { get; set; }
}
