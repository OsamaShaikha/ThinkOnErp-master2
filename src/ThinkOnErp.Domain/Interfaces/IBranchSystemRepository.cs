using ThinkOnErp.Domain.Entities;

namespace ThinkOnErp.Domain.Interfaces;

public interface IBranchSystemRepository
{
    Task<List<SysSystem>> GetSystemsByBranchIdAsync(long branchId);
    Task AssignSystemsToBranchAsync(long branchId, List<long> systemIds, long? grantedBy);
    Task RemoveSystemFromBranchAsync(long branchId, long systemId);
    Task<bool> HasSystemAsync(long branchId, long systemId);
}
