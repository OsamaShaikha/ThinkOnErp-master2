using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using ThinkOnErp.Domain.Entities;
using ThinkOnErp.Infrastructure.Data;
using ThinkOnErp.Infrastructure.Exceptions;

namespace ThinkOnErp.Infrastructure.Repositories.EfCore;

/// <summary>
/// EF Core repository implementation for SysUserRole entity using LINQ queries.
/// Handles many-to-many relationship between users and roles with composite key (UserId, RoleId).
/// Uses ThinkOnErpDbContext for database operations with automatic change tracking.
/// </summary>
public class UserRoleRepository
{
    private readonly ThinkOnErpDbContext _context;
    private readonly ILogger<UserRoleRepository> _logger;

    /// <summary>
    /// Initializes a new instance of the UserRoleRepository class.
    /// </summary>
    /// <param name="context">The EF Core database context for entity management.</param>
    /// <param name="logger">Logger for recording operations and errors.</param>
    /// <exception cref="ArgumentNullException">Thrown when context or logger is null.</exception>
    public UserRoleRepository(
        ThinkOnErpDbContext context,
        ILogger<UserRoleRepository> logger)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Retrieves all role assignments for a specific user using LINQ.
    /// Uses AsNoTracking for read-only query optimization.
    /// Eagerly loads User and Role navigation properties.
    /// </summary>
    /// <param name="userId">The user ID to get role assignments for</param>
    /// <returns>A list of user role assignments</returns>
    public async Task<List<SysUserRole>> GetByUserIdAsync(long userId)
    {
        return await RepositoryExceptionHandler.ExecuteWithExceptionHandlingAsync(
            async () =>
            {
                _logger.LogDebug("Retrieving role assignments for user ID: {UserId}", userId);

                var userRoles = await _context.UserRoles
                    .AsNoTracking()
                    .Include(ur => ur.User)
                    .Include(ur => ur.Role)
                    .Where(ur => ur.UserId == userId)
                    .OrderBy(ur => ur.Role!.RowDesc)
                    .ToListAsync();

                _logger.LogDebug("Retrieved {Count} role assignments for user ID: {UserId}", 
                    userRoles.Count, userId);
                return userRoles;
            },
            "GetUserRolesByUserId",
            _logger);
    }

    /// <summary>
    /// Retrieves all user assignments for a specific role using LINQ.
    /// Uses AsNoTracking for read-only query optimization.
    /// Eagerly loads User and Role navigation properties.
    /// </summary>
    /// <param name="roleId">The role ID to get user assignments for</param>
    /// <returns>A list of user role assignments</returns>
    public async Task<List<SysUserRole>> GetByRoleIdAsync(long roleId)
    {
        return await RepositoryExceptionHandler.ExecuteWithExceptionHandlingAsync(
            async () =>
            {
                _logger.LogDebug("Retrieving user assignments for role ID: {RoleId}", roleId);

                var userRoles = await _context.UserRoles
                    .AsNoTracking()
                    .Include(ur => ur.User)
                    .Include(ur => ur.Role)
                    .Where(ur => ur.RoleId == roleId)
                    .OrderBy(ur => ur.User!.FullName)
                    .ToListAsync();

                _logger.LogDebug("Retrieved {Count} user assignments for role ID: {RoleId}", 
                    userRoles.Count, roleId);
                return userRoles;
            },
            "GetUserRolesByRoleId",
            _logger);
    }

    /// <summary>
    /// Retrieves a specific user role assignment by composite key using LINQ.
    /// Uses AsNoTracking for read-only query optimization.
    /// </summary>
    /// <param name="userId">The user ID</param>
    /// <param name="roleId">The role ID</param>
    /// <returns>The user role assignment if found, null otherwise</returns>
    public async Task<SysUserRole?> GetByIdAsync(long userId, long roleId)
    {
        return await RepositoryExceptionHandler.ExecuteWithExceptionHandlingAsync(
            async () =>
            {
                _logger.LogDebug("Retrieving user role for UserId: {UserId}, RoleId: {RoleId}", 
                    userId, roleId);

                var userRole = await _context.UserRoles
                    .AsNoTracking()
                    .Include(ur => ur.User)
                    .Include(ur => ur.Role)
                    .FirstOrDefaultAsync(ur => ur.UserId == userId && ur.RoleId == roleId);

                if (userRole == null)
                {
                    _logger.LogDebug("User role not found for UserId: {UserId}, RoleId: {RoleId}", 
                        userId, roleId);
                }

                return userRole;
            },
            "GetUserRoleById",
            _logger);
    }

    /// <summary>
    /// Checks if a user has a specific role assigned using LINQ.
    /// Uses AsNoTracking for read-only query optimization.
    /// </summary>
    /// <param name="userId">The user ID</param>
    /// <param name="roleId">The role ID</param>
    /// <returns>True if the user has the role, false otherwise</returns>
    public async Task<bool> HasRoleAsync(long userId, long roleId)
    {
        return await RepositoryExceptionHandler.ExecuteWithExceptionHandlingAsync(
            async () =>
            {
                _logger.LogDebug("Checking if user {UserId} has role {RoleId}", userId, roleId);

                var hasRole = await _context.UserRoles
                    .AsNoTracking()
                    .AnyAsync(ur => ur.UserId == userId && ur.RoleId == roleId);

                _logger.LogDebug("User {UserId} has role {RoleId}: {HasRole}", userId, roleId, hasRole);
                return hasRole;
            },
            "CheckUserHasRole",
            _logger);
    }

    /// <summary>
    /// Creates a new user role assignment in the database using EF Core.
    /// EF Core automatically retrieves the generated ID from Oracle sequence.
    /// </summary>
    /// <param name="userRole">The user role assignment entity to create</param>
    /// <returns>The generated RowId from sequence</returns>
    public async Task<long> CreateAsync(SysUserRole userRole)
    {
        return await RepositoryExceptionHandler.ExecuteWithExceptionHandlingAsync(
            async () =>
            {
                _logger.LogDebug("Creating user role assignment for UserId: {UserId}, RoleId: {RoleId}", 
                    userRole.UserId, userRole.RoleId);

                // Set creation date if not already set
                if (!userRole.CreationDate.HasValue)
                {
                    userRole.CreationDate = DateTime.Now;
                }

                // Set assigned date if not already set
                if (!userRole.AssignedDate.HasValue)
                {
                    userRole.AssignedDate = DateTime.Now;
                }

                // Add the entity to the context
                _context.UserRoles.Add(userRole);

                // Save changes - EF Core will automatically retrieve the generated ID from the sequence
                await _context.SaveChangesAsync();

                _logger.LogInformation(
                    "Created user role assignment with ID: {RowId}, UserId: {UserId}, RoleId: {RoleId}", 
                    userRole.RowId, userRole.UserId, userRole.RoleId);

                return userRole.RowId;
            },
            "CreateUserRole",
            _logger);
    }

    /// <summary>
    /// Assigns a role to a user (creates a new user role assignment).
    /// Checks if the assignment already exists before creating.
    /// </summary>
    /// <param name="userId">The user ID</param>
    /// <param name="roleId">The role ID</param>
    /// <param name="assignedBy">User ID who is assigning the role</param>
    /// <param name="creationUser">Username for audit</param>
    /// <returns>The user role assignment ID (existing or newly created)</returns>
    public async Task<long> AssignRoleAsync(long userId, long roleId, long? assignedBy, string creationUser)
    {
        return await RepositoryExceptionHandler.ExecuteWithExceptionHandlingAsync(
            async () =>
            {
                _logger.LogDebug("Assigning role {RoleId} to user {UserId}", roleId, userId);

                // Check if assignment already exists
                var existing = await _context.UserRoles
                    .FirstOrDefaultAsync(ur => ur.UserId == userId && ur.RoleId == roleId);

                if (existing != null)
                {
                    _logger.LogInformation(
                        "User role assignment already exists with ID: {RowId}", existing.RowId);
                    return existing.RowId;
                }

                // Create new assignment
                var userRole = new SysUserRole
                {
                    UserId = userId,
                    RoleId = roleId,
                    AssignedBy = assignedBy,
                    AssignedDate = DateTime.Now,
                    CreationUser = creationUser,
                    CreationDate = DateTime.Now
                };

                _context.UserRoles.Add(userRole);
                await _context.SaveChangesAsync();

                _logger.LogInformation(
                    "Assigned role {RoleId} to user {UserId} with ID: {RowId}", 
                    roleId, userId, userRole.RowId);

                return userRole.RowId;
            },
            "AssignRoleToUser",
            _logger);
    }

    /// <summary>
    /// Deletes a user role assignment by composite key.
    /// Loads the entity and removes it from the context.
    /// </summary>
    /// <param name="userId">The user ID</param>
    /// <param name="roleId">The role ID</param>
    /// <returns>The number of rows affected (1 if successful, 0 if not found)</returns>
    public async Task<int> DeleteAsync(long userId, long roleId)
    {
        return await RepositoryExceptionHandler.ExecuteWithExceptionHandlingAsync(
            async () =>
            {
                _logger.LogDebug("Deleting user role assignment for UserId: {UserId}, RoleId: {RoleId}", 
                    userId, roleId);

                // Find the user role to delete
                var userRole = await _context.UserRoles
                    .FirstOrDefaultAsync(ur => ur.UserId == userId && ur.RoleId == roleId);

                if (userRole == null)
                {
                    _logger.LogWarning(
                        "User role assignment not found for deletion - UserId: {UserId}, RoleId: {RoleId}", 
                        userId, roleId);
                    return 0;
                }

                // Remove the entity
                _context.UserRoles.Remove(userRole);

                var rowsAffected = await _context.SaveChangesAsync();

                if (rowsAffected > 0)
                {
                    _logger.LogInformation(
                        "Deleted user role assignment with ID: {RowId}, UserId: {UserId}, RoleId: {RoleId}", 
                        userRole.RowId, userId, roleId);
                }

                return rowsAffected;
            },
            "DeleteUserRole",
            _logger);
    }

    /// <summary>
    /// Removes a role from a user (deletes the user role assignment).
    /// </summary>
    /// <param name="userId">The user ID</param>
    /// <param name="roleId">The role ID</param>
    /// <returns>True if the role was removed, false if the assignment didn't exist</returns>
    public async Task<bool> RemoveRoleAsync(long userId, long roleId)
    {
        return await RepositoryExceptionHandler.ExecuteWithExceptionHandlingAsync(
            async () =>
            {
                _logger.LogDebug("Removing role {RoleId} from user {UserId}", roleId, userId);

                var rowsAffected = await DeleteAsync(userId, roleId);
                var removed = rowsAffected > 0;

                if (removed)
                {
                    _logger.LogInformation("Removed role {RoleId} from user {UserId}", roleId, userId);
                }
                else
                {
                    _logger.LogWarning("Role {RoleId} was not assigned to user {UserId}", roleId, userId);
                }

                return removed;
            },
            "RemoveRoleFromUser",
            _logger);
    }

    /// <summary>
    /// Removes all role assignments for a user.
    /// Useful when deactivating or deleting a user.
    /// </summary>
    /// <param name="userId">The user ID</param>
    /// <returns>The number of role assignments removed</returns>
    public async Task<int> RemoveAllRolesAsync(long userId)
    {
        return await RepositoryExceptionHandler.ExecuteWithExceptionHandlingAsync(
            async () =>
            {
                _logger.LogDebug("Removing all role assignments for user {UserId}", userId);

                var userRoles = await _context.UserRoles
                    .Where(ur => ur.UserId == userId)
                    .ToListAsync();

                if (userRoles.Count == 0)
                {
                    _logger.LogDebug("No role assignments found for user {UserId}", userId);
                    return 0;
                }

                _context.UserRoles.RemoveRange(userRoles);
                var rowsAffected = await _context.SaveChangesAsync();

                _logger.LogInformation(
                    "Removed {Count} role assignments for user {UserId}", rowsAffected, userId);

                return rowsAffected;
            },
            "RemoveAllUserRoles",
            _logger);
    }
}
