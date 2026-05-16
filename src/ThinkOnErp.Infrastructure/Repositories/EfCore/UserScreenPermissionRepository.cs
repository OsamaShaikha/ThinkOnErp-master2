using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using ThinkOnErp.Domain.Entities;
using ThinkOnErp.Infrastructure.Data;
using ThinkOnErp.Infrastructure.Exceptions;

namespace ThinkOnErp.Infrastructure.Repositories.EfCore;

/// <summary>
/// EF Core repository implementation for SysUserScreenPermission entity using LINQ queries.
/// Handles user-level screen permission overrides with composite key (UserId, ScreenId).
/// Uses ThinkOnErpDbContext for database operations with automatic change tracking.
/// </summary>
public class UserScreenPermissionRepository
{
    private readonly ThinkOnErpDbContext _context;
    private readonly ILogger<UserScreenPermissionRepository> _logger;

    /// <summary>
    /// Initializes a new instance of the UserScreenPermissionRepository class.
    /// </summary>
    /// <param name="context">The EF Core database context for entity management.</param>
    /// <param name="logger">Logger for recording operations and errors.</param>
    /// <exception cref="ArgumentNullException">Thrown when context or logger is null.</exception>
    public UserScreenPermissionRepository(
        ThinkOnErpDbContext context,
        ILogger<UserScreenPermissionRepository> logger)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Retrieves all screen permission overrides for a specific user using LINQ.
    /// Uses AsNoTracking for read-only query optimization.
    /// Eagerly loads User and Screen navigation properties.
    /// </summary>
    /// <param name="userId">The user ID to get permission overrides for</param>
    /// <returns>A list of user screen permission overrides</returns>
    public async Task<List<SysUserScreenPermission>> GetByUserIdAsync(long userId)
    {
        return await RepositoryExceptionHandler.ExecuteWithExceptionHandlingAsync(
            async () =>
            {
                _logger.LogDebug("Retrieving screen permission overrides for user ID: {UserId}", userId);

                var permissions = await _context.UserScreenPermissions
                    .AsNoTracking()
                    .Include(p => p.User)
                    .Include(p => p.Screen)
                    .Where(p => p.UserId == userId)
                    .OrderBy(p => p.Screen!.DisplayOrder)
                    .ToListAsync();

                _logger.LogDebug("Retrieved {Count} screen permission overrides for user ID: {UserId}", 
                    permissions.Count, userId);
                return permissions;
            },
            "GetUserScreenPermissionsByUserId",
            _logger);
    }

    /// <summary>
    /// Retrieves a specific user screen permission override by composite key using LINQ.
    /// Uses AsNoTracking for read-only query optimization.
    /// </summary>
    /// <param name="userId">The user ID</param>
    /// <param name="screenId">The screen ID</param>
    /// <returns>The user screen permission override if found, null otherwise</returns>
    public async Task<SysUserScreenPermission?> GetByIdAsync(long userId, long screenId)
    {
        return await RepositoryExceptionHandler.ExecuteWithExceptionHandlingAsync(
            async () =>
            {
                _logger.LogDebug("Retrieving user screen permission for UserId: {UserId}, ScreenId: {ScreenId}", 
                    userId, screenId);

                var permission = await _context.UserScreenPermissions
                    .AsNoTracking()
                    .Include(p => p.User)
                    .Include(p => p.Screen)
                    .FirstOrDefaultAsync(p => p.UserId == userId && p.ScreenId == screenId);

                if (permission == null)
                {
                    _logger.LogDebug("User screen permission not found for UserId: {UserId}, ScreenId: {ScreenId}", 
                        userId, screenId);
                }

                return permission;
            },
            "GetUserScreenPermissionById",
            _logger);
    }

    /// <summary>
    /// Creates a new user screen permission override in the database using EF Core.
    /// EF Core automatically retrieves the generated ID from Oracle sequence.
    /// </summary>
    /// <param name="permission">The permission override entity to create</param>
    /// <returns>The generated RowId from sequence</returns>
    public async Task<long> CreateAsync(SysUserScreenPermission permission)
    {
        return await RepositoryExceptionHandler.ExecuteWithExceptionHandlingAsync(
            async () =>
            {
                _logger.LogDebug("Creating user screen permission for UserId: {UserId}, ScreenId: {ScreenId}", 
                    permission.UserId, permission.ScreenId);

                // Set creation date if not already set
                if (!permission.CreationDate.HasValue)
                {
                    permission.CreationDate = DateTime.Now;
                }

                // Set assigned date if not already set
                if (!permission.AssignedDate.HasValue)
                {
                    permission.AssignedDate = DateTime.Now;
                }

                // Add the entity to the context
                _context.UserScreenPermissions.Add(permission);

                // Save changes - EF Core will automatically retrieve the generated ID from the sequence
                await _context.SaveChangesAsync();

                _logger.LogInformation(
                    "Created user screen permission with ID: {RowId}, UserId: {UserId}, ScreenId: {ScreenId}", 
                    permission.RowId, permission.UserId, permission.ScreenId);

                return permission.RowId;
            },
            "CreateUserScreenPermission",
            _logger);
    }

    /// <summary>
    /// Updates an existing user screen permission override in the database using EF Core.
    /// EF Core automatically tracks changes and generates the appropriate UPDATE statement.
    /// </summary>
    /// <param name="permission">The permission override entity with updated values</param>
    /// <returns>The number of rows affected (1 if successful, 0 if not found)</returns>
    public async Task<int> UpdateAsync(SysUserScreenPermission permission)
    {
        return await RepositoryExceptionHandler.ExecuteWithExceptionHandlingAsync(
            async () =>
            {
                _logger.LogDebug("Updating user screen permission for UserId: {UserId}, ScreenId: {ScreenId}", 
                    permission.UserId, permission.ScreenId);

                // Set update date
                permission.UpdateDate = DateTime.Now;

                // Update the entity - EF Core will track changes
                _context.UserScreenPermissions.Update(permission);

                // Save changes and get the number of affected rows
                var rowsAffected = await _context.SaveChangesAsync();

                if (rowsAffected > 0)
                {
                    _logger.LogInformation(
                        "Updated user screen permission with ID: {RowId}, UserId: {UserId}, ScreenId: {ScreenId}", 
                        permission.RowId, permission.UserId, permission.ScreenId);
                }
                else
                {
                    _logger.LogWarning(
                        "No rows affected when updating user screen permission for UserId: {UserId}, ScreenId: {ScreenId}", 
                        permission.UserId, permission.ScreenId);
                }

                return rowsAffected;
            },
            "UpdateUserScreenPermission",
            _logger);
    }

    /// <summary>
    /// Deletes a user screen permission override by composite key.
    /// Loads the entity and removes it from the context.
    /// </summary>
    /// <param name="userId">The user ID</param>
    /// <param name="screenId">The screen ID</param>
    /// <returns>The number of rows affected (1 if successful, 0 if not found)</returns>
    public async Task<int> DeleteAsync(long userId, long screenId)
    {
        return await RepositoryExceptionHandler.ExecuteWithExceptionHandlingAsync(
            async () =>
            {
                _logger.LogDebug("Deleting user screen permission for UserId: {UserId}, ScreenId: {ScreenId}", 
                    userId, screenId);

                // Find the permission to delete
                var permission = await _context.UserScreenPermissions
                    .FirstOrDefaultAsync(p => p.UserId == userId && p.ScreenId == screenId);

                if (permission == null)
                {
                    _logger.LogWarning(
                        "User screen permission not found for deletion - UserId: {UserId}, ScreenId: {ScreenId}", 
                        userId, screenId);
                    return 0;
                }

                // Remove the entity
                _context.UserScreenPermissions.Remove(permission);

                var rowsAffected = await _context.SaveChangesAsync();

                if (rowsAffected > 0)
                {
                    _logger.LogInformation(
                        "Deleted user screen permission with ID: {RowId}, UserId: {UserId}, ScreenId: {ScreenId}", 
                        permission.RowId, userId, screenId);
                }

                return rowsAffected;
            },
            "DeleteUserScreenPermission",
            _logger);
    }

    /// <summary>
    /// Sets or updates a user screen permission override (upsert operation).
    /// If the permission exists, updates it; otherwise creates a new one.
    /// </summary>
    /// <param name="userId">The user ID</param>
    /// <param name="screenId">The screen ID</param>
    /// <param name="canView">Can view permission</param>
    /// <param name="canInsert">Can insert permission</param>
    /// <param name="canUpdate">Can update permission</param>
    /// <param name="canDelete">Can delete permission</param>
    /// <param name="assignedBy">User ID who is setting the override</param>
    /// <param name="notes">Optional notes about the override</param>
    /// <param name="creationUser">Username for audit</param>
    /// <returns>The permission ID (existing or newly created)</returns>
    public async Task<long> SetPermissionAsync(
        long userId, 
        long screenId, 
        bool canView, 
        bool canInsert, 
        bool canUpdate, 
        bool canDelete,
        long? assignedBy,
        string? notes,
        string creationUser)
    {
        return await RepositoryExceptionHandler.ExecuteWithExceptionHandlingAsync(
            async () =>
            {
                _logger.LogDebug("Setting user screen permission for UserId: {UserId}, ScreenId: {ScreenId}", 
                    userId, screenId);

                // Check if permission already exists
                var existing = await _context.UserScreenPermissions
                    .FirstOrDefaultAsync(p => p.UserId == userId && p.ScreenId == screenId);

                if (existing != null)
                {
                    // Update existing permission
                    existing.CanView = canView;
                    existing.CanInsert = canInsert;
                    existing.CanUpdate = canUpdate;
                    existing.CanDelete = canDelete;
                    existing.AssignedBy = assignedBy;
                    existing.AssignedDate = DateTime.Now;
                    existing.Notes = notes;
                    existing.UpdateUser = creationUser;
                    existing.UpdateDate = DateTime.Now;

                    await _context.SaveChangesAsync();

                    _logger.LogInformation(
                        "Updated existing user screen permission with ID: {RowId}", existing.RowId);

                    return existing.RowId;
                }
                else
                {
                    // Create new permission
                    var newPermission = new SysUserScreenPermission
                    {
                        UserId = userId,
                        ScreenId = screenId,
                        CanView = canView,
                        CanInsert = canInsert,
                        CanUpdate = canUpdate,
                        CanDelete = canDelete,
                        AssignedBy = assignedBy,
                        AssignedDate = DateTime.Now,
                        Notes = notes,
                        CreationUser = creationUser,
                        CreationDate = DateTime.Now
                    };

                    _context.UserScreenPermissions.Add(newPermission);
                    await _context.SaveChangesAsync();

                    _logger.LogInformation(
                        "Created new user screen permission with ID: {RowId}", newPermission.RowId);

                    return newPermission.RowId;
                }
            },
            "SetUserScreenPermission",
            _logger);
    }
}
