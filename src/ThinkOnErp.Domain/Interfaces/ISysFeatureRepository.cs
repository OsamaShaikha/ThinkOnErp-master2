using ThinkOnErp.Domain.Entities;

namespace ThinkOnErp.Domain.Interfaces;

public interface ISysFeatureRepository
{
    Task<List<SysFeature>> GetAllFeaturesAsync();
    Task<SysFeature?> GetFeatureByIdAsync(long featureId);
    Task<long> CreateFeatureAsync(SysFeature feature);
    Task UpdateFeatureAsync(SysFeature feature);
    Task DeleteFeatureAsync(long featureId, string updateUser);
}
