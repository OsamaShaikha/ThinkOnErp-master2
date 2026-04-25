namespace ThinkOnErp.Application.DTOs.Ticket;

/// <summary>
/// Data transfer object for ticket type information returned from API endpoints.
/// Used for read operations (GET requests).
/// </summary>
public class TicketTypeDto
{
    /// <summary>
    /// Unique identifier for the ticket type
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
    /// Default priority name for display
    /// </summary>
    public string? DefaultPriorityName { get; set; }

    /// <summary>
    /// SLA target hours for this ticket type
    /// </summary>
    public decimal SlaTargetHours { get; set; }

    /// <summary>
    /// Indicates if the ticket type is active
    /// </summary>
    public bool IsActive { get; set; }

    /// <summary>
    /// Username of the user who created this record
    /// </summary>
    public string CreationUser { get; set; } = string.Empty;

    /// <summary>
    /// Timestamp when the record was created
    /// </summary>
    public DateTime? CreationDate { get; set; }

    /// <summary>
    /// Username of the user who last updated this record
    /// </summary>
    public string? UpdateUser { get; set; }

    /// <summary>
    /// Timestamp when the record was last updated
    /// </summary>
    public DateTime? UpdateDate { get; set; }
}
