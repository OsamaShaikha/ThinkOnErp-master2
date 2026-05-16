using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using ThinkOnErp.Domain.Entities;
using ThinkOnErp.Domain.Interfaces;
using ThinkOnErp.Infrastructure.Data;
using ThinkOnErp.Infrastructure.Exceptions;

namespace ThinkOnErp.Infrastructure.Repositories.EfCore;

/// <summary>
/// EF Core repository implementation for SysUser entity using LINQ queries.
/// Implements IUserRepository interface from the Domain layer.
/// Uses ThinkOnErpDbContext for database operations with automatic change tracking and ID generation.
/// </summary>
public class UserRepository : IUserRepository
{
    private readonly ThinkOnErpDbContext _context;
    private readonly ILogger<UserRepository> _logger;

    /// <summary>
    /// Initializes a new instance of the UserRepository class.
    /// </summary>
    /// <param name="context">The EF Core database context for entity management.</param>
    /// <param name="logger">Logger for recording operations and errors.</param>
    /// <exception cref="ArgumentNullException">Thrown when context or logger is null.</exception>
    public UserRepository(
        ThinkOnErpDbContext context,
        ILogger<UserRepository> logger)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Retrieves all active users from the database using LINQ.
    /// Uses AsNoTracking for read-only query optimization.
    /// Global query filter automatically filters out soft-deleted records (IsActive = false).
    /// </summary>
    /// <returns>A list of all active SysUser entities ordered by UserName</returns>
    public async Task<List<SysUser>> GetAllAsync()
    {
        return await RepositoryExceptionHandler.ExecuteWithExceptionHandlingAsync(
            async () =>
            {
                _logger.LogDebug("Retrieving all users");

                var users = await _context.Users
                    .AsNoTracking()
                    .OrderBy(u => u.UserName)
                    .ToListAsync();

                _logger.LogDebug("Retrieved {Count} users", users.Count);
                return users;
            },
            "GetAllUsers",
            _logger);
    }

    /// <summary>
    /// Retrieves a specific user by its ID using LINQ.
    /// Uses AsNoTracking for read-only query optimization.
    /// Global query filter automatically filters out soft-deleted records (IsActive = false).
    /// </summary>
    /// <param name="rowId">The unique identifier of the user</param>
    /// <returns>The SysUser entity if found, null otherwise</returns>
    public async Task<SysUser?> GetByIdAsync(long rowId)
    {
        return await RepositoryExceptionHandler.ExecuteWithExceptionHandlingAsync(
            async () =>
            {
                _logger.LogDebug("Retrieving user with ID: {RowId}", rowId);

                var user = await _context.Users
                    .AsNoTracking()
                    .FirstOrDefaultAsync(u => u.RowId == rowId);

                if (user == null)
                {
                    _logger.LogDebug("User with ID {RowId} not found", rowId);
                }
                else
                {
                    _logger.LogDebug("Retrieved user: {UserName}", user.UserName);
                }

                return user;
            },
            "GetUserById",
            _logger);
    }

    /// <summary>
    /// Creates a new user in the database using EF Core.
    /// EF Core automatically retrieves the generated ID from Oracle sequence SEQ_SYS_USERS.
    /// </summary>
    /// <param name="user">The user entity to create</param>
    /// <returns>The generated RowId from SEQ_SYS_USERS sequence</returns>
    public async Task<long> CreateAsync(SysUser user)
    {
        return await RepositoryExceptionHandler.ExecuteWithExceptionHandlingAsync(
            async () =>
            {
                _logger.LogDebug("Creating new user: {UserName}", user.UserName);

                // Set creation date if not already set
                if (!user.CreationDate.HasValue)
                {
                    user.CreationDate = DateTime.Now;
                }

                // Set IsActive to true by default
                user.IsActive = true;

                // Add the entity to the context
                _context.Users.Add(user);

                // Save changes - EF Core will automatically retrieve the generated ID from the sequence
                await _context.SaveChangesAsync();

                _logger.LogInformation("Created user with ID: {RowId}, UserName: {UserName}", 
                    user.RowId, user.UserName);

                return user.RowId;
            },
            "CreateUser",
            _logger);
    }

    /// <summary>
    /// Updates an existing user in the database using EF Core.
    /// EF Core automatically tracks changes and generates the appropriate UPDATE statement.
    /// </summary>
    /// <param name="user">The user entity with updated values</param>
    /// <returns>The number of rows affected (1 if successful, 0 if not found)</returns>
    public async Task<long> UpdateAsync(SysUser user)
    {
        return await RepositoryExceptionHandler.ExecuteWithExceptionHandlingAsync(
            async () =>
            {
                _logger.LogDebug("Updating user with ID: {RowId}", user.RowId);

                // Set update date
                user.UpdateDate = DateTime.Now;

                // Update the entity - EF Core will track changes
                _context.Users.Update(user);

                // Save changes and get the number of affected rows
                var rowsAffected = await _context.SaveChangesAsync();

                if (rowsAffected > 0)
                {
                    _logger.LogInformation("Updated user with ID: {RowId}, UserName: {UserName}", 
                        user.RowId, user.UserName);
                }
                else
                {
                    _logger.LogWarning("No rows affected when updating user with ID: {RowId}", 
                        user.RowId);
                }

                return rowsAffected;
            },
            "UpdateUser",
            _logger);
    }

    /// <summary>
    /// Performs a soft delete on a user by setting IS_ACTIVE to false.
    /// Loads the entity, modifies the IsActive property, and saves changes.
    /// </summary>
    /// <param name="rowId">The unique identifier of the user to delete</param>
    /// <returns>The number of rows affected (1 if successful, 0 if not found)</returns>
    public async Task<long> DeleteAsync(long rowId)
    {
        return await RepositoryExceptionHandler.ExecuteWithExceptionHandlingAsync(
            async () =>
            {
                _logger.LogDebug("Soft deleting user with ID: {RowId}", rowId);

                // Find the user to delete - need to ignore query filter to find it even if already deleted
                var user = await _context.Users
                    .IgnoreQueryFilters()
                    .FirstOrDefaultAsync(u => u.RowId == rowId);

                if (user == null)
                {
                    _logger.LogWarning("User with ID {RowId} not found for deletion", rowId);
                    return 0;
                }

                // Perform soft delete by setting IsActive to false
                user.IsActive = false;
                user.UpdateDate = DateTime.Now;

                var rowsAffected = await _context.SaveChangesAsync();

                if (rowsAffected > 0)
                {
                    _logger.LogInformation("Soft deleted user with ID: {RowId}, UserName: {UserName}", 
                        rowId, user.UserName);
                }

                return rowsAffected;
            },
            "DeleteUser",
            _logger);
    }

    /// <summary>
    /// Retrieves all active users for a specific branch using LINQ.
    /// Uses AsNoTracking for read-only query optimization.
    /// Filters by branch ID and active status.
    /// </summary>
    /// <param name="branchId">The unique identifier of the branch</param>
    /// <returns>A list of SysUser entities belonging to the specified branch</returns>
    public async Task<List<SysUser>> GetByBranchIdAsync(long branchId)
    {
        return await RepositoryExceptionHandler.ExecuteWithExceptionHandlingAsync(
            async () =>
            {
                _logger.LogDebug("Retrieving users for branch ID: {BranchId}", branchId);

                var users = await _context.Users
                    .AsNoTracking()
                    .Where(u => u.BranchId == branchId)
                    .OrderBy(u => u.UserName)
                    .ToListAsync();

                _logger.LogDebug("Retrieved {Count} users for branch {BranchId}", users.Count, branchId);
                return users;
            },
            "GetUsersByBranchId",
            _logger);
    }

    /// <summary>
    /// Retrieves all active users for a specific company (through branches) using LINQ.
    /// Uses AsNoTracking for read-only query optimization.
    /// Joins with SYS_BRANCH table to filter by company ID.
    /// </summary>
    /// <param name="companyId">The unique identifier of the company</param>
    /// <returns>A list of SysUser entities belonging to branches of the specified company</returns>
    public async Task<List<SysUser>> GetByCompanyIdAsync(long companyId)
    {
        return await RepositoryExceptionHandler.ExecuteWithExceptionHandlingAsync(
            async () =>
            {
                _logger.LogDebug("Retrieving users for company ID: {CompanyId}", companyId);

                var users = await _context.Users
                    .AsNoTracking()
                    .Where(u => u.BranchId.HasValue && 
                                _context.Branches.Any(b => b.RowId == u.BranchId.Value && b.ParRowId == companyId))
                    .OrderBy(u => u.UserName)
                    .ToListAsync();

                _logger.LogDebug("Retrieved {Count} users for company {CompanyId}", users.Count, companyId);
                return users;
            },
            "GetUsersByCompanyId",
            _logger);
    }

    /// <summary>
    /// Forces logout of a user by setting FORCE_LOGOUT_DATE and clearing refresh tokens.
    /// Loads the entity, updates the force logout date and clears tokens, then saves changes.
    /// </summary>
    /// <param name="userId">The unique identifier of the user to force logout</param>
    /// <param name="adminUser">The username of the admin performing the force logout</param>
    /// <returns>The number of rows affected (1 if successful, 0 if not found)</returns>
    public async Task<int> ForceLogoutAsync(long userId, string adminUser)
    {
        return await RepositoryExceptionHandler.ExecuteWithExceptionHandlingAsync(
            async () =>
            {
                _logger.LogDebug("Forcing logout for user ID: {UserId} by admin: {AdminUser}", userId, adminUser);

                // Load the user entity (need tracking for update)
                var user = await _context.Users
                    .FirstOrDefaultAsync(u => u.RowId == userId);

                if (user == null)
                {
                    _logger.LogWarning("User with ID {UserId} not found for force logout", userId);
                    return 0;
                }

                // Set force logout date and clear refresh token
                user.ForceLogoutDate = DateTime.Now;
                user.RefreshToken = null;
                user.RefreshTokenExpiry = null;
                user.UpdateUser = adminUser;
                user.UpdateDate = DateTime.Now;

                var rowsAffected = await _context.SaveChangesAsync();

                if (rowsAffected > 0)
                {
                    _logger.LogInformation("Forced logout for user ID: {UserId}, UserName: {UserName}", 
                        userId, user.UserName);
                }

                return rowsAffected;
            },
            "ForceLogoutUser",
            _logger);
    }

    /// <summary>
    /// Changes the password for a user.
    /// Loads the entity, updates the password hash, then saves changes.
    /// </summary>
    /// <param name="userId">The unique identifier of the user</param>
    /// <param name="newPasswordHash">The new SHA-256 hashed password</param>
    /// <param name="updateUser">The username of the user performing the change</param>
    /// <returns>The number of rows affected (1 if successful, 0 if not found)</returns>
    public async Task<int> ChangePasswordAsync(long userId, string newPasswordHash, string updateUser)
    {
        return await RepositoryExceptionHandler.ExecuteWithExceptionHandlingAsync(
            async () =>
            {
                _logger.LogDebug("Changing password for user ID: {UserId}", userId);

                // Load the user entity (need tracking for update)
                var user = await _context.Users
                    .FirstOrDefaultAsync(u => u.RowId == userId);

                if (user == null)
                {
                    _logger.LogWarning("User with ID {UserId} not found for password change", userId);
                    return 0;
                }

                // Update the password and audit fields
                user.Password = newPasswordHash;
                user.UpdateUser = updateUser;
                user.UpdateDate = DateTime.Now;

                var rowsAffected = await _context.SaveChangesAsync();

                if (rowsAffected > 0)
                {
                    _logger.LogInformation("Changed password for user ID: {UserId}, UserName: {UserName}", 
                        userId, user.UserName);
                }

                return rowsAffected;
            },
            "ChangeUserPassword",
            _logger);
    }

    /// <summary>
    /// Retrieves all active admin users from the database using LINQ.
    /// Uses AsNoTracking for read-only query optimization.
    /// Filters by IS_ADMIN flag set to true.
    /// </summary>
    /// <returns>A list of active admin SysUser entities</returns>
    public async Task<List<SysUser>> GetAdminUsersAsync()
    {
        return await RepositoryExceptionHandler.ExecuteWithExceptionHandlingAsync(
            async () =>
            {
                _logger.LogDebug("Retrieving admin users");

                var users = await _context.Users
                    .AsNoTracking()
                    .Where(u => u.IsAdmin)
                    .OrderBy(u => u.UserName)
                    .ToListAsync();

                _logger.LogDebug("Retrieved {Count} admin users", users.Count);
                return users;
            },
            "GetAdminUsers",
            _logger);
    }
}
