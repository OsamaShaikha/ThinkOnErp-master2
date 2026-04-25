using MediatR;
using ThinkOnErp.Application.DTOs.Ticket;

namespace ThinkOnErp.Application.Features.Tickets.Queries.GetTicketById;

/// <summary>
/// Query for retrieving a specific ticket by ID with detailed information.
/// </summary>
public class GetTicketByIdQuery : IRequest<TicketDetailDto?>
{
    /// <summary>
    /// Unique identifier of the ticket to retrieve
    /// </summary>
    public Int64 TicketId { get; set; }

    /// <summary>
    /// Include comments in the response
    /// </summary>
    public bool IncludeComments { get; set; } = true;

    /// <summary>
    /// Include attachments in the response
    /// </summary>
    public bool IncludeAttachments { get; set; } = true;

    /// <summary>
    /// Include internal comments (requires admin authorization)
    /// </summary>
    public bool IncludeInternalComments { get; set; } = false;

    public GetTicketByIdQuery(Int64 ticketId)
    {
        TicketId = ticketId;
    }
}