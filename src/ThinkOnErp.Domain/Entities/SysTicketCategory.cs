namespace ThinkOnErp.Domain.Entities;

/// <summary>
/// Represents a ticket category entity for additional classification of tickets.
/// Provides optional categorization beyond ticket types for better organization.
/// Maps to the SYS_TICKET_CATEGORY table in Oracle database.
/// </summary>
public class SysTicketCategory
{
    /// <summary>
    /// Primary key - generated from SEQ_SYS_TICKET_CATEGORY sequence
    /// </summary>
    public Int64 RowId { get; set; }

    /// <summary>
    /// Category name in Arabic
    /// </summary>
    public string CategoryNameAr { get; set; } = string.Empty;

    /// <summary>
    /// Category name in English
    /// </summary>
    public string CategoryNameEn { get; set; } = string.Empty;

    /// <summary>
    /// Category description in Arabic (optional)
    /// </summary>
    public string? DescriptionAr { get; set; }

    /// <summary>
    /// Category description in English (optional)
    /// </summary>
    public string? DescriptionEn { get; set; }

    /// <summary>
    /// Display order for UI sorting
    /// </summary>
    public int DisplayOrder { get; set; }

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
    /// Navigation property to all tickets in this category
    /// </summary>
    public List<SysRequestTicket> Tickets { get; set; } = new();
}