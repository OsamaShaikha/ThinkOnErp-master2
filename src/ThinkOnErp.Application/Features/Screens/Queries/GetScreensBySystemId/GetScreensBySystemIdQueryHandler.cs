using MediatR;
using ThinkOnErp.Application.DTOs.Screen;
using ThinkOnErp.Domain.Interfaces;

namespace ThinkOnErp.Application.Features.Screens.Queries.GetScreensBySystemId;

public class GetScreensBySystemIdQueryHandler : IRequestHandler<GetScreensBySystemIdQuery, List<ScreenDto>>
{
    private readonly IScreenRepository _screenRepository;
    private readonly ISysSettingRepository _settingRepo;

    public GetScreensBySystemIdQueryHandler(IScreenRepository screenRepository, ISysSettingRepository settingRepo)
    {
        _screenRepository = screenRepository;
        _settingRepo = settingRepo;
    }

    public async Task<List<ScreenDto>> Handle(GetScreensBySystemIdQuery request, CancellationToken cancellationToken)
    {
        var basePath = await GetIconBasePathAsync();
        var screens = await _screenRepository.GetScreensBySystemIdAsync(request.SystemId);
        var dtos = new List<ScreenDto>(screens.Count);

        foreach (var s in screens)
        {
            dtos.Add(new ScreenDto
            {
                Id = s.Id,
                SystemId = s.SystemId,
                ParentScreenId = s.ParentScreenId,
                ScreenCode = s.ScreenCode,
                ScreenName = s.ScreenName,
                ScreenNameE = s.ScreenNameE,
                Route = s.Route,
                Description = s.Description,
                DescriptionE = s.DescriptionE,
                Icon = await LoadIconAsBase64Async(basePath, s.Icon),
                DisplayOrder = s.DisplayOrder,
                IsActive = s.IsActive,
                CreationUser = s.CreationUser,
                CreationDate = s.CreationDate,
                UpdateUser = s.UpdateUser,
                UpdateDate = s.UpdateDate
            });
        }

        return dtos;
    }

    private async Task<string?> GetIconBasePathAsync()
    {
        var setting = await _settingRepo.GetByCodeAsync(7);
        return setting?.SettingValue ?? Path.Combine("THINKON_FILES", "ICONS");
    }

    private static async Task<string?> LoadIconAsBase64Async(string? basePath, string? iconPath)
    {
        if (string.IsNullOrEmpty(iconPath) || string.IsNullOrEmpty(basePath))
            return null;

        var fullPath = Path.GetFullPath(Path.Combine(basePath, iconPath));
        if (!File.Exists(fullPath))
            return null;

        var bytes = await File.ReadAllBytesAsync(fullPath);
        return Convert.ToBase64String(bytes);
    }
}
