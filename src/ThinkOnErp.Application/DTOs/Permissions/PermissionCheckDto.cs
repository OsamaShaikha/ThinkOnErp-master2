namespace ThinkOnErp.Application.DTOs.Permissions;

/// <summary>
/// Data transfer object for permission check requests.
/// </summary>
public class PermissionCheckDto
{
    /// <summary>
    /// User ID to check permissions for
    /// </summary>
    public Int64 UserId { get; set; }

    /// <summary>
    /// Screen code to check access to
    /// </summary>
    public string ScreenCode { get; set; } = string.Empty;

    /// <summary>
    /// Action to check: VIEW, INSERT, UPDATE, DELETE
    /// </summary>
    public string Action { get; set; } = string.Empty;
}
