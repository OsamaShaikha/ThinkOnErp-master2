using MediatR;

namespace ThinkOnErp.Application.Features.Permissions.Commands.SetRoleScreenPermission;

/// <summary>
/// Command to set screen permission for a role.
/// </summary>
public class SetRoleScreenPermissionCommand : IRequest<Unit>
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

    /// <summary>
    /// Username for audit
    /// </summary>
    public string CreationUser { get; set; } = string.Empty;
}
