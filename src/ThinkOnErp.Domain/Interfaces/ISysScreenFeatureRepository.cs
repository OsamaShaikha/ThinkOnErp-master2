using ThinkOnErp.Domain.Entities;

namespace ThinkOnErp.Domain.Interfaces;

public interface ISysScreenFeatureRepository
{
    Task<List<SysFeature>> GetFeaturesByScreenIdAsync(long screenId);
    Task AssignFeaturesToScreenAsync(long screenId, List<long> featureIds);
    Task RemoveFeatureFromScreenAsync(long screenId, long featureId);
}
