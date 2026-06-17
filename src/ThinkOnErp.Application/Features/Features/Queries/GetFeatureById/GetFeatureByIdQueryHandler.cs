using MediatR;
using ThinkOnErp.Application.DTOs.Feature;
using ThinkOnErp.Domain.Interfaces;

namespace ThinkOnErp.Application.Features.Features.Queries.GetFeatureById;

public class GetFeatureByIdQueryHandler : IRequestHandler<GetFeatureByIdQuery, FeatureDto?>
{
    private readonly ISysFeatureRepository _featureRepository;
    private readonly ISysSettingRepository _settingRepo;

    public GetFeatureByIdQueryHandler(ISysFeatureRepository featureRepository, ISysSettingRepository settingRepo)
    {
        _featureRepository = featureRepository;
        _settingRepo = settingRepo;
    }

    public async Task<FeatureDto?> Handle(GetFeatureByIdQuery request, CancellationToken cancellationToken)
    {
        var feature = await _featureRepository.GetFeatureByIdAsync(request.FeatureId);
        if (feature == null)
            return null;

        var basePath = await GetIconBasePathAsync();

        return new FeatureDto
        {
            Id = feature.Id,
            FeatureCode = feature.FeatureCode,
            FeatureName = feature.FeatureName,
            FeatureNameE = feature.FeatureNameE,
            Description = feature.Description,
            DescriptionE = feature.DescriptionE,
            Icon = await LoadIconAsBase64Async(basePath, feature.Icon),
            DisplayOrder = feature.DisplayOrder,
            IsActive = feature.IsActive,
            CreationUser = feature.CreationUser,
            CreationDate = feature.CreationDate,
            UpdateUser = feature.UpdateUser,
            UpdateDate = feature.UpdateDate
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
        return Convert.ToBase64String(bytes);
    }
}
