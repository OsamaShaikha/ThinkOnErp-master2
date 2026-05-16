using ThinkOnErp.Domain.Entities;

namespace ThinkOnErp.Domain.Interfaces;

/// <summary>
/// Repository interface for branch-level permissions (systems and screens)
/// </summary>
public interface IBranchPermissionRepository
{
    // Branch System Permissions
    Task<List<SysBranchSystem>> GetBranchSystemsAsync(long branchId);
    Task<SysBranchSystem?> GetBranchSystemAsync(long branchId, long systemId);
    Task<long> GrantSystemAccessAsync(long branchId, long systemId, string grantedBy, string? notes, string creationUser);
    Task<long> RevokeSystemAccessAsync(long branchId, long systemId, string updateUser);
    Task<bool> IsBranchSystemAllowedAsync(long branchId, long systemId);

    // Branch Screen Permissions
    Task<List<SysBranchScreen>> GetBranchScreensAsync(long branchId);
    Task<SysBranchScreen?> GetBranchScreenAsync(long branchId, long screenId);
    Task<long> GrantScreenAccessAsync(long branchId, long screenId, string grantedBy, string? notes, string creationUser);
    Task<long> RevokeScreenAccessAsync(long branchId, long screenId, string updateUser);
    Task<bool> IsBranchScreenAllowedAsync(long branchId, long screenId);

    // Bulk Operations
    Task<int> GrantMultipleSystemsAsync(long branchId, List<long> systemIds, string grantedBy, string creationUser);
    Task<int> GrantMultipleScreensAsync(long branchId, List<long> screenIds, string grantedBy, string creationUser);
    
    // Get all systems/screens
    Task<List<SysSystem>> GetAllSystemsAsync();
    Task<List<SysScreen>> GetAllScreensAsync();
    Task<List<SysScreen>> GetScreensBySystemAsync(long systemId);
}
