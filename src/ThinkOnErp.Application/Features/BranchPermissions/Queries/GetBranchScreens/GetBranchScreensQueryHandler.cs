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
            RowId = bs.RowId,
            BranchId = bs.BranchId,
            ScreenId = bs.ScreenId,
            IsAllowed = bs.IsAllowed,
            GrantedBy = bs.GrantedBy,
            GrantedDate = bs.GrantedDate,
            RevokedDate = bs.RevokedDate,
            Notes = bs.Notes
        }).ToList();
    }
}
