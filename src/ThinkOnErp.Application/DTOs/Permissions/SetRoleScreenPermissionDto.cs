namespace ThinkOnErp.Application.DTOs.Permissions;

/// <summary>
/// Data transfer object for setting role screen permissions.
/// </summary>
public class SetRoleScreenPermissionDto
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
