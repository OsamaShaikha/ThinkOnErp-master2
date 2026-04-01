using MediatR;

namespace ThinkOnErp.Application.Features.Users.Commands.ChangePassword;

public class ChangePasswordCommand : IRequest<bool>
{
    public decimal UserId { get; set; }
    public string CurrentPassword { get; set; } = string.Empty;
    public string NewPassword { get; set; } = string.Empty;
    public string ConfirmPassword { get; set; } = string.Empty;
}
