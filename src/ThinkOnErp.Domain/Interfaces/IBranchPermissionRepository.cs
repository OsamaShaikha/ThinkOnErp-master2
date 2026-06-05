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
    Task<long> GrantSystemAccessAsync(long branchId, long systemId, long grantedBy, string? notes, string creationUser);
    Task<long> RevokeSystemAccessAsync(long branchId, long systemId, string updateUser);
    Task<bool> IsBranchSystemAllowedAsync(long branchId, long systemId);

    // Branch Screen Permissions
    Task<List<SysBranchScreenPermission>> GetBranchScreensAsync(long branchId);
    Task<SysBranchScreenPermission?> GetBranchScreenAsync(long branchId, long screenId);
    Task<long> GrantScreenAccessAsync(long branchId, long screenId, long grantedBy, string? notes, string creationUser);
    Task<long> RevokeScreenAccessAsync(long branchId, long screenId, string updateUser);
    Task<bool> IsBranchScreenAllowedAsync(long branchId, long screenId);

    // Bulk Operations
    Task<int> GrantMultipleSystemsAsync(long branchId, List<long> systemIds, long grantedBy, string creationUser);
    Task<int> GrantMultipleScreensAsync(long branchId, List<long> screenIds, long grantedBy, string creationUser);

    // System + All Its Screens Operations
    Task GrantSystemWithAllScreensAsync(long branchId, long systemId, long grantedBy, string creationUser);
    Task RevokeSystemWithAllScreensAsync(long branchId, long systemId, string updateUser);

    // Get all systems/screens
    Task<List<SysSystem>> GetAllSystemsAsync();
    Task<List<SysScreen>> GetAllScreensAsync();
    Task<List<SysScreen>> GetScreensBySystemAsync(long systemId);
}
