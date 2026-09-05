using MediatR;

namespace ThinkOnErp.Application.Features.SuperAdmins.Commands.CreateSuperAdmin;

public class CreateSuperAdminCommand : IRequest<Int64>
{
    public string NameLocal { get; set; } = string.Empty;
    public string NameEn { get; set; } = string.Empty;
    public string UserName { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public string? Email { get; set; }
    public string? Phone { get; set; }
    public string? PinHash { get; set; }
    public string CreationUser { get; set; } = string.Empty;
}
