using MediatR;

namespace ThinkOnErp.Application.Features.Tickets.Commands.UpdateTicket;

/// <summary>
/// Command for updating an existing ticket.
/// </summary>
public class UpdateTicketCommand : IRequest<Int64>
{
    /// <summary>
    /// Unique identifier of the ticket to update
    /// </summary>
    public Int64 TicketId { get; set; }

    /// <summary>
    /// Arabic title of the ticket
    /// </summary>
    public string TitleAr { get; set; } = string.Empty;

    /// <summary>
    /// English title of the ticket
    /// </summary>
    public string TitleEn { get; set; } = string.Empty;

    /// <summary>
    /// Detailed description of the ticket
    /// </summary>
    public string Description { get; set; } = string.Empty;

    /// <summary>
    /// Foreign key to SYS_TICKET_TYPE table
    /// </summary>
    public Int64 TicketTypeId { get; set; }

    /// <summary>
    /// Foreign key to SYS_TICKET_PRIORITY table
    /// </summary>
    public Int64 TicketPriorityId { get; set; }

    /// <summary>
    /// Foreign key to SYS_TICKET_CATEGORY table (optional)
    /// </summary>
    public Int64? TicketCategoryId { get; set; }

    /// <summary>
    /// Username of the user updating this ticket
    /// </summary>
    public string UpdateUser { get; set; } = string.Empty;
}