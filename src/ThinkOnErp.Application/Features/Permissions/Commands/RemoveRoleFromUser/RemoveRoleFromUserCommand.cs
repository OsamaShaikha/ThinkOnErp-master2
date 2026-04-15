using MediatR;

namespace ThinkOnErp.Application.Features.Permissions.Commands.RemoveRoleFromUser;

/// <summary>
/// Command to remove a role from a user.
/// </summary>
public class RemoveRoleFromUserCommand : IRequest<Unit>
{
    /// <summary>
    /// User ID
    /// </summary>
    public Int64 UserId { get; set; }

    /// <summary>
    /// Role ID to remove
    /// </summary>
    public Int64 RoleId { get; set; }
}
