using MediatR;

namespace ThinkOnErp.Application.Features.Permissions.Commands.AssignRoleToUser;

/// <summary>
/// Command to assign a role to a user.
/// </summary>
public class AssignRoleToUserCommand : IRequest<Unit>
{
    /// <summary>
    /// User ID
    /// </summary>
    public Int64 UserId { get; set; }

    /// <summary>
    /// Role ID to assign
    /// </summary>
    public Int64 RoleId { get; set; }

    /// <summary>
    /// User ID who is assigning the role
    /// </summary>
    public Int64? AssignedBy { get; set; }

    /// <summary>
    /// Username for audit
    /// </summary>
    public string CreationUser { get; set; } = string.Empty;
}
