using ThinkOnErp.Domain.Entities;

namespace ThinkOnErp.Domain.Interfaces;

/// <summary>
/// Repository interface for permission-related operations.
/// Provides methods for checking permissions, managing role/user permissions, and system assignments.
/// </summary>
public interface IPermissionRepository
{
    // =====================================================
    // Permission Check
    // =====================================================
    
    /// <summary>
    /// Checks if a user has permission to perform an action on a screen.
    /// Uses FN_CHECK_USER_PERMISSION function which implements full permission resolution logic.
    /// </summary>
    /// <param name="userId">User ID to check</param>
    /// <param name="screenCode">Screen code (e.g., 'invoices', 'products')</param>
    /// <param name="action">Action: VIEW, INSERT, UPDATE, DELETE</param>
    /// <returns>True if allowed, false if denied</returns>
    Task<bool> CheckUserPermissionAsync(long userId, string screenCode, string action);

    // =====================================================
    // User Role Management
    // =====================================================
    
    /// <summary>
    /// Gets all roles assigned to a user.
    /// </summary>
    /// <param name="userId">User ID</param>
    /// <returns>List of user role assignments with role details</returns>
    Task<List<SysUserRole>> GetUserRolesAsync(long userId);

    /// <summary>
    /// Assigns a role to a user.
    /// </summary>
    /// <param name="userId">User ID</param>
    /// <param name="roleId">Role ID</param>
    /// <param name="assignedBy">User ID who is assigning the role</param>
    /// <param name="creationUser">Username for audit</param>
    Task AssignRoleToUserAsync(long userId, long roleId, long? assignedBy, string creationUser);

    /// <summary>
    /// Removes a role from a user.
    /// </summary>
    /// <param name="userId">User ID</param>
    /// <param name="roleId">Role ID</param>
    Task RemoveRoleFromUserAsync(long userId, long roleId);

    // =====================================================
    // Role Screen Permissions
    // =====================================================
    
    /// <summary>
    /// Gets all screen permissions for a role.
    /// </summary>
    /// <param name="roleId">Role ID</param>
    /// <returns>List of role screen permissions</returns>
    Task<List<SysRoleScreenPermission>> GetRoleScreenPermissionsAsync(long roleId);

    /// <summary>
    /// Sets screen permission for a role (insert or update).
    /// </summary>
    /// <param name="roleId">Role ID</param>
    /// <param name="screenId">Screen ID</param>
    /// <param name="canView">Can view permission</param>
    /// <param name="canInsert">Can insert permission</param>
    /// <param name="canUpdate">Can update permission</param>
    /// <param name="canDelete">Can delete permission</param>
    /// <param name="creationUser">Username for audit</param>
    Task SetRoleScreenPermissionAsync(long roleId, long screenId, bool canView, bool canInsert, bool canUpdate, bool canDelete, string creationUser);

    /// <summary>
    /// Deletes a role screen permission.
    /// </summary>
    /// <param name="roleId">Role ID</param>
    /// <param name="screenId">Screen ID</param>
    Task DeleteRoleScreenPermissionAsync(long roleId, long screenId);

    // =====================================================
    // User Screen Permission Overrides
    // =====================================================
    
    /// <summary>
    /// Gets all screen permission overrides for a user.
    /// </summary>
    /// <param name="userId">User ID</param>
    /// <returns>List of user screen permission overrides</returns>
    Task<List<SysUserScreenPermission>> GetUserScreenPermissionsAsync(long userId);

    /// <summary>
    /// Sets screen permission override for a user (insert or update).
    /// </summary>
    /// <param name="userId">User ID</param>
    /// <param name="screenId">Screen ID</param>
    /// <param name="canView">Can view permission</param>
    /// <param name="canInsert">Can insert permission</param>
    /// <param name="canUpdate">Can update permission</param>
    /// <param name="canDelete">Can delete permission</param>
    /// <param name="assignedBy">User ID who is setting the override</param>
    /// <param name="notes">Optional notes</param>
    /// <param name="creationUser">Username for audit</param>
    Task SetUserScreenPermissionAsync(long userId, long screenId, bool canView, bool canInsert, bool canUpdate, bool canDelete, long? assignedBy, string? notes, string creationUser);

    /// <summary>
    /// Deletes a user screen permission override.
    /// </summary>
    /// <param name="userId">User ID</param>
    /// <param name="screenId">Screen ID</param>
    Task DeleteUserScreenPermissionAsync(long userId, long screenId);

    // =====================================================
    // Company System Assignments
    // =====================================================
    
    /// <summary>
    /// Gets all system assignments for a company.
    /// </summary>
    /// <param name="companyId">Company ID</param>
    /// <returns>List of company system assignments</returns>
    Task<List<SysCompanySystem>> GetCompanySystemsAsync(long companyId);

    /// <summary>
    /// Sets system access for a company (allow or block).
    /// </summary>
    /// <param name="companyId">Company ID</param>
    /// <param name="systemId">System ID</param>
    /// <param name="isAllowed">True to allow, false to block</param>
    /// <param name="grantedBy">Super Admin ID who is granting/revoking</param>
    /// <param name="notes">Optional notes</param>
    /// <param name="creationUser">Username for audit</param>
    Task SetCompanySystemAsync(long companyId, long systemId, bool isAllowed, long? grantedBy, string? notes, string creationUser);
}
