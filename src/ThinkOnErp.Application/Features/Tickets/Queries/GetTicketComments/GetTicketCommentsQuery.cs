using MediatR;
using ThinkOnErp.Application.DTOs.Ticket;

namespace ThinkOnErp.Application.Features.Tickets.Queries.GetTicketComments;

/// <summary>
/// Query for retrieving comments for a specific ticket.
/// </summary>
public class GetTicketCommentsQuery : IRequest<List<TicketCommentDto>>
{
    /// <summary>
    /// Unique identifier of the ticket
    /// </summary>
    public Int64 TicketId { get; set; }

    /// <summary>
    /// Include internal comments (requires admin authorization)
    /// </summary>
    public bool IncludeInternalComments { get; set; } = false;

    /// <summary>
    /// Sort direction for comments (ASC or DESC by creation date)
    /// </summary>
    public string SortDirection { get; set; } = "ASC";

    public GetTicketCommentsQuery(Int64 ticketId)
    {
        TicketId = ticketId;
    }
}