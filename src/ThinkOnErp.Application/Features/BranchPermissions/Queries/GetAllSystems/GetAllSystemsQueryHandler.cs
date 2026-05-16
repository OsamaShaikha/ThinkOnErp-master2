using MediatR;
using ThinkOnErp.Application.DTOs.BranchPermission;
using ThinkOnErp.Domain.Interfaces;

namespace ThinkOnErp.Application.Features.BranchPermissions.Queries.GetAllSystems;

public class GetAllSystemsQueryHandler : IRequestHandler<GetAllSystemsQuery, List<SystemDto>>
{
    private readonly IBranchPermissionRepository _repository;

    public GetAllSystemsQueryHandler(IBranchPermissionRepository repository)
    {
        _repository = repository;
    }

    public async Task<List<SystemDto>> Handle(GetAllSystemsQuery request, CancellationToken cancellationToken)
    {
        var systems = await _repository.GetAllSystemsAsync();

        return systems.Select(s => new SystemDto
        {
            RowId = s.Id,
            SystemCode = s.SystemCode,
            SystemName = s.SystemName,
            SystemNameE = s.SystemNameE,
            Icon = s.Icon,
            DisplayOrder = s.DisplayOrder,
            IsActive = s.IsActive
        }).ToList();
    }
}
