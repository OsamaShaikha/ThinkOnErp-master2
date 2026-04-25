namespace ThinkOnErp.Domain.Entities;

/// <summary>
/// Represents a ticket type entity for categorizing different types of requests.
/// Includes multilingual support, default priority assignment, and SLA target configuration.
/// Maps to the SYS_TICKET_TYPE table in Oracle database.
/// </summary>
public class SysTicketType
{
    /// <summary>
    /// Primary key - generated from SEQ_SYS_TICKET_TYPE sequence
    /// </summary>
    public Int64 RowId { get; set; }

    /// <summary>
    /// Type name in Arabic
    /// </summary>
    public string TypeNameAr { get; set; } = string.Empty;

    /// <summary>
    /// Type name in English
    /// </summary>
    public string TypeNameEn { get; set; } = string.Empty;

    /// <summary>
    /// Description in Arabic (optional)
    /// </summary>
    public string? DescriptionAr { get; set; }

    /// <summary>
    /// Description in English (optional)
    /// </summary>
    public string? DescriptionEn { get; set; }

    /// <summary>
    /// Foreign key to SYS_TICKET_PRIORITY table - default priority for this ticket type
    /// </summary>
    public Int64 DefaultPriorityId { get; set; }

    /// <summary>
    /// SLA target hours for resolution of tickets of this type
    /// </summary>
    public decimal SlaTargetHours { get; set; }

    /// <summary>
    /// Soft delete flag - true for active, false for deleted
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

    // Navigation properties
    /// <summary>
    /// Navigation property to the default priority for this ticket type
    /// </summary>
    public SysTicketPriority? DefaultPriority { get; set; }

    /// <summary>
    /// Navigation property to all tickets of this type
    /// </summary>
    public List<SysRequestTicket> Tickets { get; set; } = new();
}