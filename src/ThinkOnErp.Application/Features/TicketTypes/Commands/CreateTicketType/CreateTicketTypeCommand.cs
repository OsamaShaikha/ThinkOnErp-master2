using MediatR;

namespace ThinkOnErp.Application.Features.TicketTypes.Commands.CreateTicketType;

/// <summary>
/// Command for creating a new ticket type.
/// </summary>
public class CreateTicketTypeCommand : IRequest<Int64>
{
    /// <summary>
    /// Arabic name of the ticket type
    /// </summary>
    public string TypeNameAr { get; set; } = string.Empty;

    /// <summary>
    /// English name of the ticket type
    /// </summary>
    public string TypeNameEn { get; set; } = string.Empty;

    /// <summary>
    /// Arabic description of the ticket type
    /// </summary>
    public string? DescriptionAr { get; set; }

    /// <summary>
    /// English description of the ticket type
    /// </summary>
    public string? DescriptionEn { get; set; }

    /// <summary>
    /// Foreign key to SYS_TICKET_PRIORITY table (default priority for this type)
    /// </summary>
    public Int64 DefaultPriorityId { get; set; }

    /// <summary>
    /// SLA target hours for this ticket type
    /// </summary>
    public decimal SlaTargetHours { get; set; }

    /// <summary>
    /// Username of the user creating this ticket type
    /// </summary>
    public string CreationUser { get; set; } = string.Empty;
}
