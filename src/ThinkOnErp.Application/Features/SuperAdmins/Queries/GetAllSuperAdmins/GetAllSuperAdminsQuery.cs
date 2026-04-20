using MediatR;
using ThinkOnErp.Application.DTOs.SuperAdmin;

namespace ThinkOnErp.Application.Features.SuperAdmins.Queries.GetAllSuperAdmins;

public class GetAllSuperAdminsQuery : IRequest<List<SuperAdminDto>>
{
}
