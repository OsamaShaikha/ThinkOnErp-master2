using ThinkOnErp.Domain.Entities;

namespace ThinkOnErp.Domain.Interfaces;

public interface IBranchScreenRepository
{
    Task<List<SysScreen>> GetAvailableScreensAsync(long branchId);
    Task<List<long>> GetRevokedScreenIdsAsync(long branchId);
    Task RevokeScreenAsync(long branchId, long screenId, long? revokedBy);
    Task AllowScreenAsync(long branchId, long screenId);
}
