using MediatR;

namespace ThinkOnErp.Application.Features.SuperAdmins.Commands.ChangeSuperAdminPassword;

/// <summary>
/// Command to change super admin password
/// </summary>
public class ChangeSuperAdminPasswordCommand : IRequest<bool>
{
    public Int64 SuperAdminId { get; set; }
    public string CurrentPassword { get; set; } = string.Empty;
    public string NewPassword { get; set; } = string.Empty;
    public string ConfirmPassword { get; set; } = string.Empty;
    public string UpdateUser { get; set; } = string.Empty;
}
