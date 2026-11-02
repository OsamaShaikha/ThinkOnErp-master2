namespace ThinkOnErp.Domain.Interfaces;

public interface IPermissionService
{
    Task<bool> CanAccessAsync(long userId, long screenId, long featureId);
    Task<bool> CanAccessByCodeAsync(long userId, string screenCode, string featureCode);
    Task<bool> CanAccessSystemFeatureAsync(long userId, long systemId, long featureId);
    Task<bool> CanAccessSystemFeatureByCodeAsync(long userId, string systemCode, string featureCode);
}
