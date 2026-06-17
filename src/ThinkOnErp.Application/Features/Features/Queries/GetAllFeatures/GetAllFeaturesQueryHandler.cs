using MediatR;
using ThinkOnErp.Application.DTOs.Feature;
using ThinkOnErp.Domain.Interfaces;

namespace ThinkOnErp.Application.Features.Features.Queries.GetAllFeatures;

public class GetAllFeaturesQueryHandler : IRequestHandler<GetAllFeaturesQuery, List<FeatureDto>>
{
    private readonly ISysFeatureRepository _featureRepository;
    private readonly ISysSettingRepository _settingRepo;

    public GetAllFeaturesQueryHandler(ISysFeatureRepository featureRepository, ISysSettingRepository settingRepo)
    {
        _featureRepository = featureRepository;
        _settingRepo = settingRepo;
    }

    public async Task<List<FeatureDto>> Handle(GetAllFeaturesQuery request, CancellationToken cancellationToken)
    {
        var basePath = await GetIconBasePathAsync();
        var features = await _featureRepository.GetAllFeaturesAsync();

        var dtos = new List<FeatureDto>(features.Count);
        foreach (var f in features)
        {
            dtos.Add(new FeatureDto
            {
                Id = f.Id,
                FeatureCode = f.FeatureCode,
                FeatureName = f.FeatureName,
                FeatureNameE = f.FeatureNameE,
                Description = f.Description,
                DescriptionE = f.DescriptionE,
                Icon = await LoadIconAsBase64Async(basePath, f.Icon),
                DisplayOrder = f.DisplayOrder,
                IsActive = f.IsActive,
                CreationUser = f.CreationUser,
                CreationDate = f.CreationDate,
                UpdateUser = f.UpdateUser,
                UpdateDate = f.UpdateDate
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
