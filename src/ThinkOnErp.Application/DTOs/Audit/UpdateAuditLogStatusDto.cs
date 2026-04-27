namespace ThinkOnErp.Application.DTOs.Audit;

/// <summary>
/// Request model for updating audit log status
/// </summary>
public class UpdateAuditLogStatusDto
{
    /// <summary>
    /// New status (Unresolved, In Progress, Resolved, Critical)
    /// </summary>
    public string Status { get; set; } = null!;
    
    /// <summary>
    /// Optional resolution notes (max 4000 characters)
    /// </summary>
    public string? ResolutionNotes { get; set; }
    
    /// <summary>
    /// Optional user ID to assign the issue to
    /// </summary>
    public long? AssignedToUserId { get; set; }
}