using MediatR;
using ThinkOnErp.Application.DTOs.Permissions;
using ThinkOnErp.Domain.Interfaces;

namespace ThinkOnErp.Application.Features.Permissions.Queries.GetScreensBySystemId;

/// <summary>
/// Handler for GetScreensBySystemIdQuery.
/// Retrieves all screens for a specific system.
/// </summary>
public class GetScreensBySystemIdQueryHandler : IRequestHandler<GetScreensBySystemIdQuery, List<ScreenDto>>
{
    private readonly IScreenRepository _screenRepository;

    public GetScreensBySystemIdQueryHandler(IScreenRepository screenRepository)
    {
        _screenRepository = screenRepository;
    }

    public async Task<List<ScreenDto>> Handle(GetScreensBySystemIdQuery request, CancellationToken cancellationToken)
    {
        var screens = await _screenRepository.GetScreensBySystemIdAsync(request.SystemId);

        return screens.Select(s => new ScreenDto
        {
            ScreenId = s.RowId,
            SystemId = s.SystemId,
            ParentScreenId = s.ParentScreenId,
            ScreenCode = s.ScreenCode,
            ScreenNameAr = s.ScreenName,
            ScreenNameEn = s.ScreenNameE,
            Route = s.Route,
            DescriptionAr = s.Description,
            DescriptionEn = s.DescriptionE,
            Icon = s.Icon,
            DisplayOrder = s.DisplayOrder,
            IsActive = s.IsActive
        }).ToList();
    }
}
