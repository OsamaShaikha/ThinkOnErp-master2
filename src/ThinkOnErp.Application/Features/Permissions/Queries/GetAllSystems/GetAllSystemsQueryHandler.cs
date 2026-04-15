using MediatR;
using ThinkOnErp.Application.DTOs.Permissions;
using ThinkOnErp.Domain.Interfaces;

namespace ThinkOnErp.Application.Features.Permissions.Queries.GetAllSystems;

/// <summary>
/// Handler for GetAllSystemsQuery.
/// Retrieves all active systems.
/// </summary>
public class GetAllSystemsQueryHandler : IRequestHandler<GetAllSystemsQuery, List<SystemDto>>
{
    private readonly ISystemRepository _systemRepository;

    public GetAllSystemsQueryHandler(ISystemRepository systemRepository)
    {
        _systemRepository = systemRepository;
    }

    public async Task<List<SystemDto>> Handle(GetAllSystemsQuery request, CancellationToken cancellationToken)
    {
        var systems = await _systemRepository.GetAllSystemsAsync();

        return systems.Select(s => new SystemDto
        {
            SystemId = s.RowId,
            SystemCode = s.SystemCode,
            SystemNameAr = s.SystemName,
            SystemNameEn = s.SystemNameE,
            DescriptionAr = s.Description,
            DescriptionEn = s.DescriptionE,
            Icon = s.Icon,
            DisplayOrder = s.DisplayOrder,
            IsActive = s.IsActive
        }).ToList();
    }
}
