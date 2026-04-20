using MediatR;

namespace ThinkOnErp.Application.Features.SuperAdmins.Commands.UpdateSuperAdmin;

public class UpdateSuperAdminCommand : IRequest<bool>
{
    public Int64 SuperAdminId { get; set; }
    public string NameAr { get; set; } = string.Empty;
    public string NameEn { get; set; } = string.Empty;
    public string? Email { get; set; }
    public string? Phone { get; set; }
    public string UpdateUser { get; set; } = string.Empty;
}
