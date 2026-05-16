using MediatR;
using ThinkOnErp.Application.DTOs.BranchPermission;
using ThinkOnErp.Domain.Interfaces;

namespace ThinkOnErp.Application.Features.BranchPermissions.Queries.GetBranchSystems;

public class GetBranchSystemsQueryHandler : IRequestHandler<GetBranchSystemsQuery, List<BranchSystemDto>>
{
    private readonly IBranchPermissionRepository _repository;

    public GetBranchSystemsQueryHandler(IBranchPermissionRepository repository)
    {
        _repository = repository;
    }

    public async Task<List<BranchSystemDto>> Handle(GetBranchSystemsQuery request, CancellationToken cancellationToken)
    {
        var branchSystems = await _repository.GetBranchSystemsAsync(request.BranchId);

        return branchSystems.Select(bs => new BranchSystemDto
        {
            RowId = bs.Id,
            BranchId = bs.BranchId,
            SystemId = bs.SystemId,
            IsAllowed = bs.IsAllowed,
            GrantedBy = bs.GrantedBy,
            GrantedDate = bs.GrantedDate,
            RevokedDate = bs.RevokedDate,
            Notes = bs.Notes
        }).ToList();
    }
}
