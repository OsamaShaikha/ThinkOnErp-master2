using MediatR;

namespace ThinkOnErp.Application.Features.Users.Commands.ResetUserPassword;

/// <summary>
/// Command to reset a user's password (admin-initiated)
/// Generates a secure temporary password
/// </summary>
public class ResetUserPasswordCommand : IRequest<string>
{
    /// <summary>
    /// The unique identifier of the user whose password will be reset
    /// </summary>
    public Int64 UserId { get; set; }

    /// <summary>
    /// The username of the admin performing the reset
    /// </summary>
    public string UpdateUser { get; set; } = string.Empty;
}
