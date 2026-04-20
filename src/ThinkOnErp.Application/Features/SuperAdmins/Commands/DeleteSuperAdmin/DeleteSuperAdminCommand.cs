using MediatR;

namespace ThinkOnErp.Application.Features.SuperAdmins.Commands.DeleteSuperAdmin;

public class DeleteSuperAdminCommand : IRequest<bool>
{
    public Int64 SuperAdminId { get; set; }
}
