using MediatR;
using ThinkOnErp.Application.DTOs.Ticket;

namespace ThinkOnErp.Application.Features.Tickets.Queries.GetTicketAttachments;

/// <summary>
/// Query for retrieving attachments for a specific ticket.
/// </summary>
public class GetTicketAttachmentsQuery : IRequest<List<TicketAttachmentDto>>
{
    /// <summary>
    /// Unique identifier of the ticket
    /// </summary>
    public Int64 TicketId { get; set; }

    /// <summary>
    /// Sort direction for attachments (ASC or DESC by creation date)
    /// </summary>
    public string SortDirection { get; set; } = "ASC";

    public GetTicketAttachmentsQuery(Int64 ticketId)
    {
        TicketId = ticketId;
    }
}