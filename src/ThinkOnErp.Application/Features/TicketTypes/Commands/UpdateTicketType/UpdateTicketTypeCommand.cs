using MediatR;

namespace ThinkOnErp.Application.Features.TicketTypes.Commands.UpdateTicketType;

/// <summary>
/// Command for updating an existing ticket type.
/// </summary>
public class UpdateTicketTypeCommand : IRequest<Int64>
{
    /// <summary>
    /// Unique identifier of the ticket type to update
    /// </summary>
    public Int64 TicketTypeId { get; set; }

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
    /// Username of the user updating this ticket type
    /// </summary>
    public string UpdateUser { get; set; } = string.Empty;
}
