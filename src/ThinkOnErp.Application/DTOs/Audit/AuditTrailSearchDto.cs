namespace ThinkOnErp.Application.DTOs.Audit;

/// <summary>
/// DTO for audit trail search parameters.
/// Validates Requirement 17.11: Provide audit trail search and filtering capabilities.
/// </summary>
public class AuditTrailSearchDto
{
    /// <summary>
    /// Filter by entity type (Ticket, Comment, Attachment, etc.)
    /// </summary>
    public string? EntityType { get; set; }

    /// <summary>
    /// Filter by entity ID
    /// </summary>
    public Int64? EntityId { get; set; }

    /// <summary>
    /// Filter by user ID
    /// </summary>
    public Int64? UserId { get; set; }

    /// <summary>
    /// Filter by company ID
    /// </summary>
    public Int64? CompanyId { get; set; }

    /// <summary>
    /// Filter by branch ID
    /// </summary>
    public Int64? BranchId { get; set; }

    /// <summary>
    /// Filter by action (INSERT, UPDATE, DELETE, etc.)
    /// </summary>
    public string? Action { get; set; }

    /// <summary>
    /// Start date filter
    /// </summary>
    public DateTime? FromDate { get; set; }

    /// <summary>
    /// End date filter
    /// </summary>
    public DateTime? ToDate { get; set; }

    /// <summary>
    /// Filter by severity level (Info, Warning, Error)
    /// </summary>
    public string? Severity { get; set; }

    /// <summary>
    /// Filter by event category (DataChange, Request, Permission, Configuration)
    /// </summary>
    public string? EventCategory { get; set; }

    /// <summary>
    /// Page number for pagination (default: 1)
    /// </summary>
    public int Page { get; set; } = 1;

    /// <summary>
    /// Records per page (default: 50, max: 100)
    /// </summary>
    public int PageSize { get; set; } = 50;
}
