using ThinkOnErp.Domain.Entities;

namespace ThinkOnErp.Domain.Interfaces;

public interface IBranchFeatureRepository
{
    Task<List<SysFeature>> GetAvailableFeaturesAsync(long branchId, long screenId);
    Task<List<(long ScreenId, long FeatureId)>> GetRevokedScreenFeaturesAsync(long branchId);
    Task RevokeFeatureAsync(long branchId, long screenId, long featureId, long? revokedBy);
    Task AllowFeatureAsync(long branchId, long screenId, long featureId);
}
