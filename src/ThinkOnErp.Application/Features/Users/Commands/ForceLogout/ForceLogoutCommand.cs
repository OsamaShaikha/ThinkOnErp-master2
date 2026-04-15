using MediatR;

namespace ThinkOnErp.Application.Features.Users.Commands.ForceLogout;

/// <summary>
/// Command to force logout a user by invalidating all their tokens.
/// Only super admins can execute this command.
/// </summary>
public class ForceLogoutCommand : IRequest<int>
{
    /// <summary>
    /// The ID of the user to force logout
    /// </summary>
    public long UserId { get; set; }

    /// <summary>
    /// The username of the admin performing the force logout
    /// </summary>
    public string AdminUser { get; set; } = string.Empty;
}
