using MediatR;
using ThinkOnErp.Application.DTOs.BranchPermission;
using ThinkOnErp.Domain.Interfaces;

namespace ThinkOnErp.Application.Features.BranchPermissions.Queries.GetBranchScreens;

public class GetBranchScreensQueryHandler : IRequestHandler<GetBranchScreensQuery, List<BranchScreenDto>>
{
    private readonly IBranchPermissionRepository _repository;

    public GetBranchScreensQueryHandler(IBranchPermissionRepository repository)
    {
        _repository = repository;
    }

    public async Task<List<BranchScreenDto>> Handle(GetBranchScreensQuery request, CancellationToken cancellationToken)
    {
        var branchScreens = await _repository.GetBranchScreensAsync(request.BranchId);

        return branchScreens.Select(bs => new BranchScreenDto
        {
            Id = bs.Id,
            BranchId = bs.BranchId,
            ScreenId = bs.ScreenId,
            CanView = bs.CanView,
            CanInsert = bs.CanInsert,
            CanUpdate = bs.CanUpdate,
            CanDelete = bs.CanDelete,
            GrantedBy = bs.GrantedBy,
            GrantedDate = bs.GrantedDate,
            Notes = bs.Notes
        }).ToList();
    }
}
