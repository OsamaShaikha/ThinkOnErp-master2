using MediatR;
using ThinkOnErp.Domain.Entities;
using ThinkOnErp.Domain.Interfaces;

namespace ThinkOnErp.Application.Features.Screens.Commands.CreateScreen;

public class CreateScreenCommandHandler : IRequestHandler<CreateScreenCommand, long>
{
    private readonly IScreenRepository _screenRepository;
    private readonly ISysSettingRepository _settingRepo;

    public CreateScreenCommandHandler(IScreenRepository screenRepository, ISysSettingRepository settingRepo)
    {
        _screenRepository = screenRepository;
        _settingRepo = settingRepo;
    }

    public async Task<long> Handle(CreateScreenCommand request, CancellationToken cancellationToken)
    {
        var iconPath = await SaveIconAsync(request.IconFile, request.IconFileName);

        var screen = new SysScreen
        {
            SystemId = request.SystemId,
            ParentScreenId = request.ParentScreenId,
            ScreenCode = request.ScreenCode,
            ScreenName = request.ScreenName,
            ScreenNameE = request.ScreenNameE,
            Route = "/" + request.ScreenCode.ToLower(),
            Description = request.Description,
            DescriptionE = request.DescriptionE,
            Icon = iconPath,
            DisplayOrder = request.DisplayOrder,
            IsActive = true,
            CreationUser = request.CreationUser,
            CreationDate = DateTime.UtcNow
        };
        return await _screenRepository.CreateScreenAsync(screen);
    }

    private async Task<string?> SaveIconAsync(byte[]? iconFile, string? fileName)
    {
        if (iconFile == null || string.IsNullOrEmpty(fileName))
            return null;

        var setting = await _settingRepo.GetByCodeAsync(7);
        var basePath = setting?.SettingValue ?? Path.Combine("THINKON_FILES", "ICONS");

        var ext = Path.GetExtension(fileName);
        var uniqueName = $"{Guid.NewGuid()}{ext}";
        var fullDir = Path.GetFullPath(Path.Combine(basePath));
        Directory.CreateDirectory(fullDir);
        var fullPath = Path.Combine(fullDir, uniqueName);

        await File.WriteAllBytesAsync(fullPath, iconFile);
        return uniqueName;
    }
}
