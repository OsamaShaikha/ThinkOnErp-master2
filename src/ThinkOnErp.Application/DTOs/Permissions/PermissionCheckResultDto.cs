namespace ThinkOnErp.Application.DTOs.Permissions;

/// <summary>
/// Data transfer object for permission check results.
/// </summary>
public class PermissionCheckResultDto
{
    /// <summary>
    /// Indicates if the permission is allowed
    /// </summary>
    public bool Allowed { get; set; }

    /// <summary>
    /// Optional reason for denial
    /// </summary>
    public string? Reason { get; set; }
}
