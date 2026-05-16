using MediatR;
using ThinkOnErp.Application.DTOs.BranchPermission;

namespace ThinkOnErp.Application.Features.BranchPermissions.Queries.GetBranchSystems;

public class GetBranchSystemsQuery : IRequest<List<BranchSystemDto>>
{
    public long BranchId { get; set; }
}
