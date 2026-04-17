namespace ThinkOnErp.Domain.Entities.Audit;

/// <summary>
/// Audit event for permission and role changes.
/// Tracks role assignments, permission grants/revocations for compliance.
/// </summary>
public class PermissionChangeAuditEvent : AuditEvent
{
    /// <summary>
    /// ID of the role being assigned/revoked or modified
    /// </summary>
    public long? RoleId { get; set; }

    /// <summary>
    /// ID of the permission being granted/revoked
    /// </summary>
    public long? PermissionId { get; set; }

    /// <summary>
    /// JSON representation of permission state before the change
    /// </summary>
    public string? PermissionBefore { get; set; }

    /// <summary>
    /// JSON representation of permission state after the change
    /// </summary>
    public string? PermissionAfter { get; set; }
}
