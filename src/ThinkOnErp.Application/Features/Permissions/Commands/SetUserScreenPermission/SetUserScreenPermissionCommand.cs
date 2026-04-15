using MediatR;

namespace ThinkOnErp.Application.Features.Permissions.Commands.SetUserScreenPermission;

/// <summary>
/// Command to set screen permission override for a user.
/// </summary>
public class SetUserScreenPermissionCommand : IRequest<Unit>
{
    /// <summary>
    /// User ID
    /// </summary>
    public Int64 UserId { get; set; }

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
    /// User ID who is setting the override
    /// </summary>
    public Int64? AssignedBy { get; set; }

    /// <summary>
    /// Optional notes
    /// </summary>
    public string? Notes { get; set; }

    /// <summary>
    /// Username for audit
    /// </summary>
    public string CreationUser { get; set; } = string.Empty;
}
