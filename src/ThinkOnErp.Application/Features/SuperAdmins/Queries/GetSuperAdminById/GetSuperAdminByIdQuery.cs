using MediatR;
using ThinkOnErp.Application.DTOs.SuperAdmin;

namespace ThinkOnErp.Application.Features.SuperAdmins.Queries.GetSuperAdminById;

public class GetSuperAdminByIdQuery : IRequest<SuperAdminDto?>
{
    public Int64 SuperAdminId { get; set; }
}
