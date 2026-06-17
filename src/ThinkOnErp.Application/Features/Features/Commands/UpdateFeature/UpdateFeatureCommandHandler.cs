using MediatR;
using ThinkOnErp.Domain.Interfaces;

namespace ThinkOnErp.Application.Features.Features.Commands.UpdateFeature;

public class UpdateFeatureCommandHandler : IRequestHandler<UpdateFeatureCommand, long>
{
    private readonly ISysFeatureRepository _featureRepository;
    private readonly ISysSettingRepository _settingRepo;

    public UpdateFeatureCommandHandler(ISysFeatureRepository featureRepository, ISysSettingRepository settingRepo)
    {
        _featureRepository = featureRepository;
        _settingRepo = settingRepo;
    }

    public async Task<long> Handle(UpdateFeatureCommand request, CancellationToken cancellationToken)
    {
        var feature = await _featureRepository.GetFeatureByIdAsync(request.FeatureId);
        if (feature == null)
            return 0;

        feature.FeatureCode = request.FeatureCode;
        feature.FeatureName = request.FeatureName;
        feature.FeatureNameE = request.FeatureNameE;
        feature.Description = request.Description;
        feature.DescriptionE = request.DescriptionE;
        feature.DisplayOrder = request.DisplayOrder;
        feature.UpdateUser = request.UpdateUser;
        feature.UpdateDate = DateTime.UtcNow;

        if (request.IconFile != null && !string.IsNullOrEmpty(request.IconFileName))
        {
            feature.Icon = await SaveIconAsync(request.IconFile, request.IconFileName);
        }

        await _featureRepository.UpdateFeatureAsync(feature);
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
