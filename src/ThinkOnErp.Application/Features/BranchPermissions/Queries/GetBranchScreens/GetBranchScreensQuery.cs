using MediatR;
using ThinkOnErp.Application.DTOs.BranchPermission;

namespace ThinkOnErp.Application.Features.BranchPermissions.Queries.GetBranchScreens;

public class GetBranchScreensQuery : IRequest<List<BranchScreenDto>>
{
    public long BranchId { get; set; }
}
