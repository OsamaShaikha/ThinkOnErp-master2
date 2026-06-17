using MediatR;
using ThinkOnErp.Domain.Entities;
using ThinkOnErp.Domain.Interfaces;

namespace ThinkOnErp.Application.Features.Features.Commands.CreateFeature;

public class CreateFeatureCommandHandler : IRequestHandler<CreateFeatureCommand, long>
{
    private readonly ISysFeatureRepository _featureRepository;
    private readonly ISysSettingRepository _settingRepo;

    public CreateFeatureCommandHandler(ISysFeatureRepository featureRepository, ISysSettingRepository settingRepo)
    {
        _featureRepository = featureRepository;
        _settingRepo = settingRepo;
    }

    public async Task<long> Handle(CreateFeatureCommand request, CancellationToken cancellationToken)
    {
        var iconPath = await SaveIconAsync(request.IconFile, request.IconFileName);

        var feature = new SysFeature
        {
            FeatureCode = request.FeatureCode,
            FeatureName = request.FeatureName,
            FeatureNameE = request.FeatureNameE,
            Description = request.Description,
            DescriptionE = request.DescriptionE,
            Icon = iconPath,
            DisplayOrder = request.DisplayOrder,
            IsActive = true,
            CreationUser = request.CreationUser,
            CreationDate = DateTime.UtcNow
        };
        return await _featureRepository.CreateFeatureAsync(feature);
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
