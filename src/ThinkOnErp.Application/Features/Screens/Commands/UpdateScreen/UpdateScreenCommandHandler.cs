using MediatR;
using ThinkOnErp.Domain.Interfaces;

namespace ThinkOnErp.Application.Features.Screens.Commands.UpdateScreen;

public class UpdateScreenCommandHandler : IRequestHandler<UpdateScreenCommand, long>
{
    private readonly IScreenRepository _screenRepository;
    private readonly ISysSettingRepository _settingRepo;

    public UpdateScreenCommandHandler(IScreenRepository screenRepository, ISysSettingRepository settingRepo)
    {
        _screenRepository = screenRepository;
        _settingRepo = settingRepo;
    }

    public async Task<long> Handle(UpdateScreenCommand request, CancellationToken cancellationToken)
    {
        var screen = await _screenRepository.GetScreenByIdAsync(request.ScreenId);
        if (screen == null)
            return 0;

        screen.SystemId = request.SystemId;
        screen.ParentScreenId = request.ParentScreenId;
        screen.ScreenCode = request.ScreenCode;
        screen.ScreenName = request.ScreenName;
        screen.ScreenNameE = request.ScreenNameE;
        screen.Route = "/" + request.ScreenCode.ToLower();
        screen.Description = request.Description;
        screen.DescriptionE = request.DescriptionE;
        screen.DisplayOrder = request.DisplayOrder;
        screen.UpdateUser = request.UpdateUser;
        screen.UpdateDate = DateTime.UtcNow;

        if (request.IconFile != null && !string.IsNullOrEmpty(request.IconFileName))
        {
            screen.Icon = await SaveIconAsync(request.IconFile, request.IconFileName);
        }

        await _screenRepository.UpdateScreenAsync(screen);
        return 1;
    }

    private async Task<string?> SaveIconAsync(byte[] iconFile, string fileName)
    {
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
