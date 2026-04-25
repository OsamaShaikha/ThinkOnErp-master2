using MediatR;
using ThinkOnErp.Application.DTOs.Ticket;

namespace ThinkOnErp.Application.Features.Tickets.Commands.CreateTicket;

/// <summary>
/// Command for creating a new ticket.
/// </summary>
public class CreateTicketCommand : IRequest<Int64>
{
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
    /// Foreign key to SYS_COMPANY table
    /// </summary>
    public Int64 CompanyId { get; set; }

    /// <summary>
    /// Foreign key to SYS_BRANCH table
    /// </summary>
    public Int64 BranchId { get; set; }

    /// <summary>
    /// Foreign key to SYS_USERS table (requester)
    /// </summary>
    public Int64 RequesterId { get; set; }

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
    /// List of file attachments to be uploaded with the ticket
    /// </summary>
    public List<CreateAttachmentDto>? Attachments { get; set; }

    /// <summary>
    /// Username of the user creating this ticket
    /// </summary>
    public string CreationUser { get; set; } = string.Empty;
}