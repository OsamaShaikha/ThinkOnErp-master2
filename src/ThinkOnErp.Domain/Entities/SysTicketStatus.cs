namespace ThinkOnErp.Domain.Entities;

/// <summary>
/// Represents a ticket status entity for managing ticket workflow states.
/// Includes multilingual support, workflow control, and display ordering.
/// Maps to the SYS_TICKET_STATUS table in Oracle database.
/// </summary>
public class SysTicketStatus
{
    /// <summary>
    /// Primary key - generated from SEQ_SYS_TICKET_STATUS sequence
    /// </summary>
    public Int64 RowId { get; set; }

    /// <summary>
    /// Status name in Arabic
    /// </summary>
    public string StatusNameAr { get; set; } = string.Empty;

    /// <summary>
    /// Status name in English
    /// </summary>
    public string StatusNameEn { get; set; } = string.Empty;

    /// <summary>
    /// Unique status code for programmatic identification (e.g., OPEN, IN_PROGRESS, RESOLVED)
    /// </summary>
    public string StatusCode { get; set; } = string.Empty;

    /// <summary>
    /// Display order for UI sorting and workflow progression
    /// </summary>
    public int DisplayOrder { get; set; }

    /// <summary>
    /// Indicates if this is a final status (Closed, Cancelled) that prevents further changes
    /// </summary>
    public bool IsFinalStatus { get; set; }

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

    // Navigation properties
    /// <summary>
    /// Navigation property to all tickets with this status
    /// </summary>
    public List<SysRequestTicket> Tickets { get; set; } = new();

    // Business logic properties
    /// <summary>
    /// Predefined status codes for common workflow states
    /// </summary>
    public static class StatusCodes
    {
        public const string Open = "OPEN";
        public const string InProgress = "IN_PROGRESS";
        public const string PendingCustomer = "PENDING_CUSTOMER";
        public const string Resolved = "RESOLVED";
        public const string Closed = "CLOSED";
        public const string Cancelled = "CANCELLED";
    }

    /// <summary>
    /// Indicates if this status allows ticket modifications
    /// </summary>
    public bool AllowsModification => !IsFinalStatus;

    /// <summary>
    /// Indicates if this status represents a resolved state
    /// </summary>
    public bool IsResolvedStatus => StatusCode == StatusCodes.Resolved || 
                                   StatusCode == StatusCodes.Closed;
}