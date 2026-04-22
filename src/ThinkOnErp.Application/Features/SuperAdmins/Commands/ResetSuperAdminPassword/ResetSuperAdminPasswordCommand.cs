using MediatR;

namespace ThinkOnErp.Application.Features.SuperAdmins.Commands.ResetSuperAdminPassword;

/// <summary>
/// Command to reset super admin password (admin-initiated)
/// Generates a new temporary password
/// </summary>
public class ResetSuperAdminPasswordCommand : IRequest<string>
{
    public Int64 SuperAdminId { get; set; }
    public string UpdateUser { get; set; } = string.Empty;
}
