namespace ThinkOnErp.Application.DTOs.Audit;

/// <summary>
/// DTO for ticket-specific audit trail query parameters.
/// Validates Requirement 17.11: Provide audit trail search and filtering capabilities.
/// </summary>
public class TicketAuditTrailDto
{
    /// <summary>
    /// Optional start date filter
    /// </summary>
    public DateTime? FromDate { get; set; }

    /// <summary>
    /// Optional end date filter
    /// </summary>
    public DateTime? ToDate { get; set; }

    /// <summary>
    /// Optional action filter (INSERT, UPDATE, DELETE, etc.)
    /// </summary>
    public string? ActionFilter { get; set; }

    /// <summary>
    /// Optional user ID filter
    /// </summary>
    public Int64? UserIdFilter { get; set; }
}
