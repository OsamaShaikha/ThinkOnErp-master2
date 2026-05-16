using MediatR;
using ThinkOnErp.Application.DTOs.BranchPermission;
using ThinkOnErp.Domain.Interfaces;

namespace ThinkOnErp.Application.Features.BranchPermissions.Queries.GetAllScreens;

public class GetAllScreensQueryHandler : IRequestHandler<GetAllScreensQuery, List<ScreenDto>>
{
    private readonly IBranchPermissionRepository _repository;

    public GetAllScreensQueryHandler(IBranchPermissionRepository repository)
    {
        _repository = repository;
    }

    public async Task<List<ScreenDto>> Handle(GetAllScreensQuery request, CancellationToken cancellationToken)
    {
        var screens = await _repository.GetAllScreensAsync();

        return screens.Select(s => new ScreenDto
        {
            RowId = s.RowId,
            SystemId = s.SystemId,
            ScreenCode = s.ScreenCode,
            ScreenName = s.ScreenName,
            ScreenNameE = s.ScreenNameE,
            Route = s.Route,
            DisplayOrder = s.DisplayOrder,
            IsActive = s.IsActive
        }).ToList();
    }
}
