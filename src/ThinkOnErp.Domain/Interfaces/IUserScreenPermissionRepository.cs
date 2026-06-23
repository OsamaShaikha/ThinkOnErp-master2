using ThinkOnErp.Domain.Entities;

namespace ThinkOnErp.Domain.Interfaces;

public interface IUserScreenPermissionRepository
{
    Task<List<SysUserScreenPermission>> GetByBranchAndUserAsync(long branchId, long userId);
    Task BulkSetAsync(long branchId, long userId, List<SysUserScreenPermission> permissions);
    Task DeleteAsync(long branchId, long userId, long screenId, long featureId);
}
