namespace ThinkOnErp.Domain.Interfaces;

public interface IPermissionService
{
    Task<bool> CanAccessAsync(long userId, long screenId, long featureId);
    Task<bool> CanAccessByCodeAsync(long userId, string screenCode, string featureCode);
}
