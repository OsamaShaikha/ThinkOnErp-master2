using MediatR;
using ThinkOnErp.Application.DTOs.BranchPermission;

namespace ThinkOnErp.Application.Features.BranchPermissions.Queries.GetAllSystems;

public class GetAllSystemsQuery : IRequest<List<SystemDto>>
{
}
