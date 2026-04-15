namespace ThinkOnErp.Application.DTOs.Permissions;

/// <summary>
/// Data transfer object for role screen permissions.
/// </summary>
public class RoleScreenPermissionDto
{
    /// <summary>
    /// Role ID
    /// </summary>
    public Int64 RoleId { get; set; }

    /// <summary>
    /// Screen ID
    /// </summary>
    public Int64 ScreenId { get; set; }

    /// <summary>
    /// Screen code for easy identification
    /// </summary>
    public string ScreenCode { get; set; } = string.Empty;

    /// <summary>
    /// Arabic screen name
    /// </summary>
    public string ScreenNameAr { get; set; } = string.Empty;

    /// <summary>
    /// English screen name
    /// </summary>
    public string ScreenNameEn { get; set; } = string.Empty;

    /// <summary>
    /// System ID the screen belongs to
    /// </summary>
    public Int64 SystemId { get; set; }

    /// <summary>
    /// Permission to view/read
    /// </summary>
    public bool CanView { get; set; }

    /// <summary>
    /// Permission to create
    /// </summary>
    public bool CanInsert { get; set; }

    /// <summary>
    /// Permission to edit
    /// </summary>
    public bool CanUpdate { get; set; }

    /// <summary>
    /// Permission to delete
    /// </summary>
    public bool CanDelete { get; set; }
}
