using ThinkOnErp.Domain.Entities;

namespace ThinkOnErp.Domain.Interfaces;

public interface IRoleScreenPermissionRepository
{
    Task<List<SysRoleScreenPermission>> GetByBranchAndRoleAsync(long branchId, long roleId);
    Task BulkSetAsync(long branchId, long roleId, List<SysRoleScreenPermission> permissions);
    Task DeleteAsync(long branchId, long roleId, long screenId, long featureId);
}
