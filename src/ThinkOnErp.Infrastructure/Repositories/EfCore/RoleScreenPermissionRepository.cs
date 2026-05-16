using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using ThinkOnErp.Domain.Entities;
using ThinkOnErp.Infrastructure.Data;
using ThinkOnErp.Infrastructure.Exceptions;

namespace ThinkOnErp.Infrastructure.Repositories.EfCore;

/// <summary>
/// EF Core repository implementation for SysRoleScreenPermission entity using LINQ queries.
/// Handles role-based screen permissions with composite key (RoleId, ScreenId).
/// Uses ThinkOnErpDbContext for database operations with automatic change tracking.
/// </summary>
public class RoleScreenPermissionRepository
{
    private readonly ThinkOnErpDbContext _context;
    private readonly ILogger<RoleScreenPermissionRepository> _logger;

    /// <summary>
    /// Initializes a new instance of the RoleScreenPermissionRepository class.
    /// </summary>
    /// <param name="context">The EF Core database context for entity management.</param>
    /// <param name="logger">Logger for recording operations and errors.</param>
    /// <exception cref="ArgumentNullException">Thrown when context or logger is null.</exception>
    public RoleScreenPermissionRepository(
        ThinkOnErpDbContext context,
        ILogger<RoleScreenPermissionRepository> logger)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Retrieves all screen permissions for a specific role using LINQ.
    /// Uses AsNoTracking for read-only query optimization.
    /// Eagerly loads Role and Screen navigation properties.
    /// </summary>
    /// <param name="roleId">The role ID to get permissions for</param>
    /// <returns>A list of role screen permissions</returns>
    public async Task<List<SysRoleScreenPermission>> GetByRoleIdAsync(long roleId)
    {
        return await RepositoryExceptionHandler.ExecuteWithExceptionHandlingAsync(
            async () =>
            {
                _logger.LogDebug("Retrieving screen permissions for role ID: {RoleId}", roleId);

                var permissions = await _context.RoleScreenPermissions
                    .AsNoTracking()
                    .Include(p => p.Role)
                    .Include(p => p.Screen)
                    .Where(p => p.RoleId == roleId)
                    .OrderBy(p => p.Screen!.DisplayOrder)
                    .ToListAsync();

                _logger.LogDebug("Retrieved {Count} screen permissions for role ID: {RoleId}", 
                    permissions.Count, roleId);
                return permissions;
            },
            "GetRoleScreenPermissionsByRoleId",
            _logger);
    }

    /// <summary>
    /// Retrieves a specific role screen permission by composite key using LINQ.
    /// Uses AsNoTracking for read-only query optimization.
    /// </summary>
    /// <param name="roleId">The role ID</param>
    /// <param name="screenId">The screen ID</param>
    /// <returns>The role screen permission if found, null otherwise</returns>
    public async Task<SysRoleScreenPermission?> GetByIdAsync(long roleId, long screenId)
    {
        return await RepositoryExceptionHandler.ExecuteWithExceptionHandlingAsync(
            async () =>
            {
                _logger.LogDebug("Retrieving role screen permission for RoleId: {RoleId}, ScreenId: {ScreenId}", 
                    roleId, screenId);

                var permission = await _context.RoleScreenPermissions
                    .AsNoTracking()
                    .Include(p => p.Role)
                    .Include(p => p.Screen)
                    .FirstOrDefaultAsync(p => p.RoleId == roleId && p.ScreenId == screenId);

                if (permission == null)
                {
                    _logger.LogDebug("Role screen permission not found for RoleId: {RoleId}, ScreenId: {ScreenId}", 
                        roleId, screenId);
                }

                return permission;
            },
            "GetRoleScreenPermissionById",
            _logger);
    }

    /// <summary>
    /// Creates a new role screen permission in the database using EF Core.
    /// EF Core automatically retrieves the generated ID from Oracle sequence.
    /// </summary>
    /// <param name="permission">The permission entity to create</param>
    /// <returns>The generated RowId from sequence</returns>
    public async Task<long> CreateAsync(SysRoleScreenPermission permission)
    {
        return await RepositoryExceptionHandler.ExecuteWithExceptionHandlingAsync(
            async () =>
            {
                _logger.LogDebug("Creating role screen permission for RoleId: {RoleId}, ScreenId: {ScreenId}", 
                    permission.RoleId, permission.ScreenId);

                // Set creation date if not already set
                if (!permission.CreationDate.HasValue)
                {
                    permission.CreationDate = DateTime.Now;
                }

                // Add the entity to the context
                _context.RoleScreenPermissions.Add(permission);

                // Save changes - EF Core will automatically retrieve the generated ID from the sequence
                await _context.SaveChangesAsync();

                _logger.LogInformation(
                    "Created role screen permission with ID: {RowId}, RoleId: {RoleId}, ScreenId: {ScreenId}", 
                    permission.RowId, permission.RoleId, permission.ScreenId);

                return permission.RowId;
            },
            "CreateRoleScreenPermission",
            _logger);
    }

    /// <summary>
    /// Updates an existing role screen permission in the database using EF Core.
    /// EF Core automatically tracks changes and generates the appropriate UPDATE statement.
    /// </summary>
    /// <param name="permission">The permission entity with updated values</param>
    /// <returns>The number of rows affected (1 if successful, 0 if not found)</returns>
    public async Task<int> UpdateAsync(SysRoleScreenPermission permission)
    {
        return await RepositoryExceptionHandler.ExecuteWithExceptionHandlingAsync(
            async () =>
            {
                _logger.LogDebug("Updating role screen permission for RoleId: {RoleId}, ScreenId: {ScreenId}", 
                    permission.RoleId, permission.ScreenId);

                // Set update date
                permission.UpdateDate = DateTime.Now;

                // Update the entity - EF Core will track changes
                _context.RoleScreenPermissions.Update(permission);

                // Save changes and get the number of affected rows
                var rowsAffected = await _context.SaveChangesAsync();

                if (rowsAffected > 0)
                {
                    _logger.LogInformation(
                        "Updated role screen permission with ID: {RowId}, RoleId: {RoleId}, ScreenId: {ScreenId}", 
                        permission.RowId, permission.RoleId, permission.ScreenId);
                }
                else
                {
                    _logger.LogWarning(
                        "No rows affected when updating role screen permission for RoleId: {RoleId}, ScreenId: {ScreenId}", 
                        permission.RoleId, permission.ScreenId);
                }

                return rowsAffected;
            },
            "UpdateRoleScreenPermission",
            _logger);
    }

    /// <summary>
    /// Deletes a role screen permission by composite key.
    /// Loads the entity and removes it from the context.
    /// </summary>
    /// <param name="roleId">The role ID</param>
    /// <param name="screenId">The screen ID</param>
    /// <returns>The number of rows affected (1 if successful, 0 if not found)</returns>
    public async Task<int> DeleteAsync(long roleId, long screenId)
    {
        return await RepositoryExceptionHandler.ExecuteWithExceptionHandlingAsync(
            async () =>
            {
                _logger.LogDebug("Deleting role screen permission for RoleId: {RoleId}, ScreenId: {ScreenId}", 
                    roleId, screenId);

                // Find the permission to delete
                var permission = await _context.RoleScreenPermissions
                    .FirstOrDefaultAsync(p => p.RoleId == roleId && p.ScreenId == screenId);

                if (permission == null)
                {
                    _logger.LogWarning(
                        "Role screen permission not found for deletion - RoleId: {RoleId}, ScreenId: {ScreenId}", 
                        roleId, screenId);
                    return 0;
                }

                // Remove the entity
                _context.RoleScreenPermissions.Remove(permission);

                var rowsAffected = await _context.SaveChangesAsync();

                if (rowsAffected > 0)
                {
                    _logger.LogInformation(
                        "Deleted role screen permission with ID: {RowId}, RoleId: {RoleId}, ScreenId: {ScreenId}", 
                        permission.RowId, roleId, screenId);
                }

                return rowsAffected;
            },
            "DeleteRoleScreenPermission",
            _logger);
    }

    /// <summary>
    /// Sets or updates a role screen permission (upsert operation).
    /// If the permission exists, updates it; otherwise creates a new one.
    /// </summary>
    /// <param name="roleId">The role ID</param>
    /// <param name="screenId">The screen ID</param>
    /// <param name="canView">Can view permission</param>
    /// <param name="canInsert">Can insert permission</param>
    /// <param name="canUpdate">Can update permission</param>
    /// <param name="canDelete">Can delete permission</param>
    /// <param name="creationUser">Username for audit</param>
    /// <returns>The permission ID (existing or newly created)</returns>
    public async Task<long> SetPermissionAsync(
        long roleId, 
        long screenId, 
        bool canView, 
        bool canInsert, 
        bool canUpdate, 
        bool canDelete, 
        string creationUser)
    {
        return await RepositoryExceptionHandler.ExecuteWithExceptionHandlingAsync(
            async () =>
            {
                _logger.LogDebug("Setting role screen permission for RoleId: {RoleId}, ScreenId: {ScreenId}", 
                    roleId, screenId);

                // Check if permission already exists
                var existing = await _context.RoleScreenPermissions
                    .FirstOrDefaultAsync(p => p.RoleId == roleId && p.ScreenId == screenId);

                if (existing != null)
                {
                    // Update existing permission
                    existing.CanView = canView;
                    existing.CanInsert = canInsert;
                    existing.CanUpdate = canUpdate;
                    existing.CanDelete = canDelete;
                    existing.UpdateUser = creationUser;
                    existing.UpdateDate = DateTime.Now;

                    await _context.SaveChangesAsync();

                    _logger.LogInformation(
                        "Updated existing role screen permission with ID: {RowId}", existing.RowId);

                    return existing.RowId;
                }
                else
                {
                    // Create new permission
                    var newPermission = new SysRoleScreenPermission
                    {
                        RoleId = roleId,
                        ScreenId = screenId,
                        CanView = canView,
                        CanInsert = canInsert,
                        CanUpdate = canUpdate,
                        CanDelete = canDelete,
                        CreationUser = creationUser,
                        CreationDate = DateTime.Now
                    };

                    _context.RoleScreenPermissions.Add(newPermission);
                    await _context.SaveChangesAsync();

                    _logger.LogInformation(
                        "Created new role screen permission with ID: {RowId}", newPermission.RowId);

                    return newPermission.RowId;
                }
            },
            "SetRoleScreenPermission",
            _logger);
    }
}
