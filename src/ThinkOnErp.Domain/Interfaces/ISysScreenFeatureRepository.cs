using ThinkOnErp.Domain.Entities;

namespace ThinkOnErp.Domain.Interfaces;

public interface ISysScreenFeatureRepository
{
    /// <summary>
    /// Gets all active features assigned to a screen (legacy / global).
    /// </summary>
    Task<List<SysFeature>> GetFeaturesByScreenIdAsync(long screenId);

    /// <summary>
    /// Assigns features to a screen globally (legacy).
    /// </summary>
    Task AssignFeaturesToScreenAsync(long screenId, List<long> featureIds);

    /// <summary>
    /// Removes a feature from a screen globally (legacy).
    /// </summary>
    Task RemoveFeatureFromScreenAsync(long screenId, long featureId);

    /// <summary>
    /// Gets active features for a screen within a specific system context.
    /// If screenId is null, returns system-level features.
    /// </summary>
    Task<List<SysFeature>> GetFeaturesBySystemAndScreenAsync(long systemId, long? screenId);

    /// <summary>
    /// Gets system-wide features (where screenId is null).
    /// </summary>
    Task<List<SysFeature>> GetSystemLevelFeaturesAsync(long systemId);

    /// <summary>
    /// Assigns features to a system-screen context (or system-wide if screenId is null).
    /// </summary>
    Task AssignFeaturesToSystemScreenAsync(long systemId, long? screenId, List<long> featureIds, string username);

    /// <summary>
    /// Removes a feature from a system-screen context.
    /// </summary>
    Task RemoveFeatureFromSystemScreenAsync(long systemId, long? screenId, long featureId);
}
