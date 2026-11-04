using MediatR;
using ThinkOnErp.Application.DTOs.Screen;
using ThinkOnErp.Domain.Interfaces;

namespace ThinkOnErp.Application.Features.Screens.Queries.GetScreenById;

public class GetScreenByIdQueryHandler : IRequestHandler<GetScreenByIdQuery, ScreenDto?>
{
    private readonly IScreenRepository _screenRepository;
    private readonly ISysSettingRepository _settingRepo;

    public GetScreenByIdQueryHandler(IScreenRepository screenRepository, ISysSettingRepository settingRepo)
    {
        _screenRepository = screenRepository;
        _settingRepo = settingRepo;
    }

    public async Task<ScreenDto?> Handle(GetScreenByIdQuery request, CancellationToken cancellationToken)
    {
        var screen = await _screenRepository.GetScreenByIdAsync(request.ScreenId);
        if (screen == null)
            return null;

        var basePath = await GetIconBasePathAsync();
        var sysIds = screen.SystemScreens != null && screen.SystemScreens.Count > 0
            ? screen.SystemScreens.Where(ss => ss.IsActive).Select(ss => ss.SystemId).ToList()
            : (screen.SystemId.HasValue ? new List<long> { screen.SystemId.Value } : new List<long>());

        if (screen.SystemId.HasValue && !sysIds.Contains(screen.SystemId.Value))
        {
            sysIds.Insert(0, screen.SystemId.Value);
        }

        return new ScreenDto
        {
            Id = screen.Id,
            SystemId = screen.SystemId ?? (sysIds.FirstOrDefault()),
            SystemIds = sysIds,
            ParentScreenId = screen.ParentScreenId,
            ScreenCode = screen.ScreenCode,
            ScreenName = screen.ScreenName,
            ScreenNameE = screen.ScreenNameE,
            Route = screen.Route,
            Description = screen.Description,
            DescriptionE = screen.DescriptionE,
            Icon = await LoadIconAsBase64Async(basePath, screen.Icon),
            DisplayOrder = screen.DisplayOrder,
            IsActive = screen.IsActive,
            CreationUser = screen.CreationUser,
            CreationDate = screen.CreationDate,
            UpdateUser = screen.UpdateUser,
            UpdateDate = screen.UpdateDate
        };
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
        var ext = Path.GetExtension(iconPath).TrimStart('.').ToLowerInvariant();
        var mimeType = ext switch
        {
            "png" => "image/png",
            "jpg" or "jpeg" => "image/jpeg",
            "svg" => "image/svg+xml",
            "gif" => "image/gif",
            "webp" => "image/webp",
            _ => "application/octet-stream"
        };
        return $"data:{mimeType};base64,{Convert.ToBase64String(bytes)}";
    }
}
