namespace ThinkOnErp.Application.DTOs.Audit;

/// <summary>
/// DTO for audit trail export parameters.
/// Validates Requirement 17.7: Provide audit trail export functionality.
/// </summary>
public class AuditTrailExportDto
{
    /// <summary>
    /// Filter by entity type (Ticket, Comment, Attachment, etc.)
    /// </summary>
    public string? EntityType { get; set; }

    /// <summary>
    /// Start date for export (required)
    /// </summary>
    public DateTime FromDate { get; set; }

    /// <summary>
    /// End date for export (required)
    /// </summary>
    public DateTime ToDate { get; set; }

    /// <summary>
    /// Filter by company ID
    /// </summary>
    public Int64? CompanyId { get; set; }

    /// <summary>
    /// Export format: CSV or JSON (default: CSV)
    /// </summary>
    public string Format { get; set; } = "CSV";
}
