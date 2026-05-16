using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using ThinkOnErp.Domain.Entities;
using ThinkOnErp.Domain.Interfaces;
using ThinkOnErp.Infrastructure.Data;
using ThinkOnErp.Infrastructure.Exceptions;

namespace ThinkOnErp.Infrastructure.Repositories.EfCore;

/// <summary>
/// EF Core repository implementation for SysSuperAdmin entity using LINQ queries.
/// Implements ISuperAdminRepository interface from the Domain layer.
/// Uses ThinkOnErpDbContext for database operations with automatic change tracking and ID generation.
/// Supports super admin authentication, 2FA management, and refresh token handling.
/// </summary>
public class SuperAdminRepository : ISuperAdminRepository
{
    private readonly ThinkOnErpDbContext _context;
    private readonly ILogger<SuperAdminRepository> _logger;

    /// <summary>
    /// Initializes a new instance of the SuperAdminRepository class.
    /// </summary>
    /// <param name="context">The EF Core database context for entity management.</param>
    /// <param name="logger">Logger for recording operations and errors.</param>
    /// <exception cref="ArgumentNullException">Thrown when context or logger is null.</exception>
    public SuperAdminRepository(
        ThinkOnErpDbContext context,
        ILogger<SuperAdminRepository> logger)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Retrieves all active super admins from the database using LINQ.
    /// Uses AsNoTracking for read-only query optimization.
    /// Global query filter automatically filters by IsActive = true.
    /// </summary>
    /// <returns>A list of all active SysSuperAdmin entities ordered by RowDesc</returns>
    public async Task<List<SysSuperAdmin>> GetAllAsync()
    {
        return await RepositoryExceptionHandler.ExecuteWithExceptionHandlingAsync(
            async () =>
            {
                _logger.LogDebug("Retrieving all super admins");

                var superAdmins = await _context.SuperAdmins
                    .AsNoTracking()
                    .OrderBy(s => s.RowDesc)
                    .ToListAsync();

                _logger.LogDebug("Retrieved {Count} super admins", superAdmins.Count);
                return superAdmins;
            },
            "GetAllSuperAdmins",
            _logger);
    }

    /// <summary>
    /// Retrieves a specific super admin by ID using LINQ.
    /// Uses AsNoTracking for read-only query optimization.
    /// </summary>
    /// <param name="id">The unique identifier of the super admin</param>
    /// <returns>The SysSuperAdmin entity if found, null otherwise</returns>
    public async Task<SysSuperAdmin?> GetByIdAsync(long id)
    {
        return await RepositoryExceptionHandler.ExecuteWithExceptionHandlingAsync(
            async () =>
            {
                _logger.LogDebug("Retrieving super admin with ID: {Id}", id);

                var superAdmin = await _context.SuperAdmins
                    .AsNoTracking()
                    .FirstOrDefaultAsync(s => s.RowId == id);

                if (superAdmin == null)
                {
                    _logger.LogDebug("Super admin with ID {Id} not found", id);
                }
                else
                {
                    _logger.LogDebug("Retrieved super admin: {UserName}", superAdmin.UserName);
                }

                return superAdmin;
            },
            "GetSuperAdminById",
            _logger);
    }

    /// <summary>
    /// Retrieves a super admin by username using LINQ.
    /// Uses AsNoTracking for read-only query optimization.
    /// </summary>
    /// <param name="username">The username to search for</param>
    /// <returns>The SysSuperAdmin entity if found, null otherwise</returns>
    public async Task<SysSuperAdmin?> GetByUsernameAsync(string username)
    {
        return await RepositoryExceptionHandler.ExecuteWithExceptionHandlingAsync(
            async () =>
            {
                _logger.LogDebug("Retrieving super admin with username: {Username}", username);

                var superAdmin = await _context.SuperAdmins
                    .AsNoTracking()
                    .FirstOrDefaultAsync(s => s.UserName == username);

                if (superAdmin == null)
                {
                    _logger.LogDebug("Super admin with username {Username} not found", username);
                }
                else
                {
                    _logger.LogDebug("Retrieved super admin: {UserName}", superAdmin.UserName);
                }

                return superAdmin;
            },
            "GetSuperAdminByUsername",
            _logger);
    }

    /// <summary>
    /// Retrieves a super admin by email using LINQ.
    /// Uses AsNoTracking for read-only query optimization.
    /// </summary>
    /// <param name="email">The email to search for</param>
    /// <returns>The SysSuperAdmin entity if found, null otherwise</returns>
    public async Task<SysSuperAdmin?> GetByEmailAsync(string email)
    {
        return await RepositoryExceptionHandler.ExecuteWithExceptionHandlingAsync(
            async () =>
            {
                _logger.LogDebug("Retrieving super admin with email: {Email}", email);

                var superAdmin = await _context.SuperAdmins
                    .AsNoTracking()
                    .FirstOrDefaultAsync(s => s.Email == email);

                if (superAdmin == null)
                {
                    _logger.LogDebug("Super admin with email {Email} not found", email);
                }
                else
                {
                    _logger.LogDebug("Retrieved super admin: {UserName}", superAdmin.UserName);
                }

                return superAdmin;
            },
            "GetSuperAdminByEmail",
            _logger);
    }

    /// <summary>
    /// Creates a new super admin in the database using EF Core.
    /// EF Core automatically retrieves the generated ID from Oracle sequence SEQ_SYS_SUPER_ADMIN.
    /// </summary>
    /// <param name="superAdmin">The super admin entity to create</param>
    /// <returns>The generated RowId from SEQ_SYS_SUPER_ADMIN sequence</returns>
    public async Task<long> CreateAsync(SysSuperAdmin superAdmin)
    {
        return await RepositoryExceptionHandler.ExecuteWithExceptionHandlingAsync(
            async () =>
            {
                _logger.LogDebug("Creating new super admin: {UserName}", superAdmin.UserName);

                // Set creation date if not already set
                if (!superAdmin.CreationDate.HasValue)
                {
                    superAdmin.CreationDate = DateTime.Now;
                }

                // Set default values
                if (!superAdmin.IsActive)
                {
                    superAdmin.IsActive = true;
                }

                // Add the entity to the context
                _context.SuperAdmins.Add(superAdmin);

                // Save changes - EF Core will automatically retrieve the generated ID from the sequence
                await _context.SaveChangesAsync();

                _logger.LogInformation("Created super admin with ID: {RowId}, Username: {UserName}", 
                    superAdmin.RowId, superAdmin.UserName);

                return superAdmin.RowId;
            },
            "CreateSuperAdmin",
            _logger);
    }

    /// <summary>
    /// Updates an existing super admin in the database using EF Core.
    /// EF Core automatically tracks changes and generates the appropriate UPDATE statement.
    /// </summary>
    /// <param name="superAdmin">The super admin entity with updated values</param>
    /// <returns>The number of rows affected (1 if successful, 0 if not found)</returns>
    public async Task<long> UpdateAsync(SysSuperAdmin superAdmin)
    {
        return await RepositoryExceptionHandler.ExecuteWithExceptionHandlingAsync(
            async () =>
            {
                _logger.LogDebug("Updating super admin with ID: {RowId}", superAdmin.RowId);

                // Set update date
                superAdmin.UpdateDate = DateTime.Now;

                // Update the entity - EF Core will track changes
                _context.SuperAdmins.Update(superAdmin);

                // Save changes and get the number of affected rows
                var rowsAffected = await _context.SaveChangesAsync();

                if (rowsAffected > 0)
                {
                    _logger.LogInformation("Updated super admin with ID: {RowId}, Username: {UserName}", 
                        superAdmin.RowId, superAdmin.UserName);
                }
                else
                {
                    _logger.LogWarning("No rows affected when updating super admin with ID: {RowId}", 
                        superAdmin.RowId);
                }

                return rowsAffected;
            },
            "UpdateSuperAdmin",
            _logger);
    }

    /// <summary>
    /// Performs a soft delete on a super admin by setting IS_ACTIVE to false.
    /// Loads the entity, modifies the IsActive property, and saves changes.
    /// </summary>
    /// <param name="id">The unique identifier of the super admin to delete</param>
    /// <returns>The number of rows affected (1 if successful, 0 if not found)</returns>
    public async Task<long> DeleteAsync(long id)
    {
        return await RepositoryExceptionHandler.ExecuteWithExceptionHandlingAsync(
            async () =>
            {
                _logger.LogDebug("Soft deleting super admin with ID: {Id}", id);

                // Find the super admin to delete (need to ignore query filter to find it)
                var superAdmin = await _context.SuperAdmins
                    .IgnoreQueryFilters()
                    .FirstOrDefaultAsync(s => s.RowId == id);

                if (superAdmin == null)
                {
                    _logger.LogWarning("Super admin with ID {Id} not found for deletion", id);
                    return 0;
                }

                // Soft delete by setting IsActive to false
                superAdmin.IsActive = false;
                superAdmin.UpdateDate = DateTime.Now;

                var rowsAffected = await _context.SaveChangesAsync();

                if (rowsAffected > 0)
                {
                    _logger.LogInformation("Soft deleted super admin with ID: {Id}, Username: {UserName}", 
                        id, superAdmin.UserName);
                }

                return rowsAffected;
            },
            "DeleteSuperAdmin",
            _logger);
    }

    /// <summary>
    /// Changes the password for a super admin.
    /// Loads the entity, updates the password hash, and saves changes.
    /// </summary>
    /// <param name="id">The unique identifier of the super admin</param>
    /// <param name="newPasswordHash">The new hashed password</param>
    /// <param name="updateUser">The username of the user performing the update</param>
    /// <returns>The number of rows affected (1 if successful, 0 if not found)</returns>
    public async Task<long> ChangePasswordAsync(long id, string newPasswordHash, string updateUser)
    {
        return await RepositoryExceptionHandler.ExecuteWithExceptionHandlingAsync(
            async () =>
            {
                _logger.LogDebug("Changing password for super admin with ID: {Id}", id);

                var superAdmin = await _context.SuperAdmins
                    .FirstOrDefaultAsync(s => s.RowId == id);

                if (superAdmin == null)
                {
                    _logger.LogWarning("Super admin with ID {Id} not found for password change", id);
                    return 0;
                }

                superAdmin.Password = newPasswordHash;
                superAdmin.UpdateUser = updateUser;
                superAdmin.UpdateDate = DateTime.Now;

                var rowsAffected = await _context.SaveChangesAsync();

                if (rowsAffected > 0)
                {
                    _logger.LogInformation("Changed password for super admin with ID: {Id}", id);
                }

                return rowsAffected;
            },
            "ChangeSuperAdminPassword",
            _logger);
    }

    /// <summary>
    /// Enables two-factor authentication for a super admin.
    /// Loads the entity, sets the 2FA secret and enabled flag, and saves changes.
    /// </summary>
    /// <param name="id">The unique identifier of the super admin</param>
    /// <param name="twoFaSecret">The TOTP secret for 2FA</param>
    /// <param name="updateUser">The username of the user performing the update</param>
    /// <returns>The number of rows affected (1 if successful, 0 if not found)</returns>
    public async Task<long> Enable2FAAsync(long id, string twoFaSecret, string updateUser)
    {
        return await RepositoryExceptionHandler.ExecuteWithExceptionHandlingAsync(
            async () =>
            {
                _logger.LogDebug("Enabling 2FA for super admin with ID: {Id}", id);

                var superAdmin = await _context.SuperAdmins
                    .FirstOrDefaultAsync(s => s.RowId == id);

                if (superAdmin == null)
                {
                    _logger.LogWarning("Super admin with ID {Id} not found for 2FA enable", id);
                    return 0;
                }

                superAdmin.TwoFaSecret = twoFaSecret;
                superAdmin.TwoFaEnabled = true;
                superAdmin.UpdateUser = updateUser;
                superAdmin.UpdateDate = DateTime.Now;

                var rowsAffected = await _context.SaveChangesAsync();

                if (rowsAffected > 0)
                {
                    _logger.LogInformation("Enabled 2FA for super admin with ID: {Id}", id);
                }

                return rowsAffected;
            },
            "EnableSuperAdmin2FA",
            _logger);
    }

    /// <summary>
    /// Disables two-factor authentication for a super admin.
    /// Loads the entity, clears the 2FA secret and disabled flag, and saves changes.
    /// </summary>
    /// <param name="id">The unique identifier of the super admin</param>
    /// <param name="updateUser">The username of the user performing the update</param>
    /// <returns>The number of rows affected (1 if successful, 0 if not found)</returns>
    public async Task<long> Disable2FAAsync(long id, string updateUser)
    {
        return await RepositoryExceptionHandler.ExecuteWithExceptionHandlingAsync(
            async () =>
            {
                _logger.LogDebug("Disabling 2FA for super admin with ID: {Id}", id);

                var superAdmin = await _context.SuperAdmins
                    .FirstOrDefaultAsync(s => s.RowId == id);

                if (superAdmin == null)
                {
                    _logger.LogWarning("Super admin with ID {Id} not found for 2FA disable", id);
                    return 0;
                }

                superAdmin.TwoFaSecret = null;
                superAdmin.TwoFaEnabled = false;
                superAdmin.UpdateUser = updateUser;
                superAdmin.UpdateDate = DateTime.Now;

                var rowsAffected = await _context.SaveChangesAsync();

                if (rowsAffected > 0)
                {
                    _logger.LogInformation("Disabled 2FA for super admin with ID: {Id}", id);
                }

                return rowsAffected;
            },
            "DisableSuperAdmin2FA",
            _logger);
    }

    /// <summary>
    /// Updates the last login date for a super admin.
    /// Loads the entity, sets the last login date to current time, and saves changes.
    /// </summary>
    /// <param name="id">The unique identifier of the super admin</param>
    /// <returns>The number of rows affected (1 if successful, 0 if not found)</returns>
    public async Task<long> UpdateLastLoginAsync(long id)
    {
        return await RepositoryExceptionHandler.ExecuteWithExceptionHandlingAsync(
            async () =>
            {
                _logger.LogDebug("Updating last login for super admin with ID: {Id}", id);

                var superAdmin = await _context.SuperAdmins
                    .FirstOrDefaultAsync(s => s.RowId == id);

                if (superAdmin == null)
                {
                    _logger.LogWarning("Super admin with ID {Id} not found for last login update", id);
                    return 0;
                }

                superAdmin.LastLoginDate = DateTime.Now;

                var rowsAffected = await _context.SaveChangesAsync();

                if (rowsAffected > 0)
                {
                    _logger.LogDebug("Updated last login for super admin with ID: {Id}", id);
                }

                return rowsAffected;
            },
            "UpdateSuperAdminLastLogin",
            _logger);
    }

    /// <summary>
    /// Authenticates a super admin by username and password hash.
    /// Uses AsNoTracking for read-only query optimization.
    /// Updates the last login date upon successful authentication.
    /// </summary>
    /// <param name="userName">The username to authenticate</param>
    /// <param name="passwordHash">The hashed password to verify</param>
    /// <returns>The SysSuperAdmin entity if authentication succeeds, null otherwise</returns>
    public async Task<SysSuperAdmin?> AuthenticateAsync(string userName, string passwordHash)
    {
        return await RepositoryExceptionHandler.ExecuteWithExceptionHandlingAsync(
            async () =>
            {
                _logger.LogDebug("Authenticating super admin with username: {UserName}", userName);

                var superAdmin = await _context.SuperAdmins
                    .AsNoTracking()
                    .FirstOrDefaultAsync(s => s.UserName == userName && s.Password == passwordHash);

                if (superAdmin == null)
                {
                    _logger.LogWarning("Authentication failed for username: {UserName}", userName);
                    return null;
                }

                // Update last login date
                await UpdateLastLoginAsync(superAdmin.RowId);

                _logger.LogInformation("Successfully authenticated super admin: {UserName}", userName);
                return superAdmin;
            },
            "AuthenticateSuperAdmin",
            _logger);
    }

    /// <summary>
    /// Saves a refresh token for a super admin.
    /// Loads the entity, updates the refresh token and expiry date, and saves changes.
    /// </summary>
    /// <param name="superAdminId">The unique identifier of the super admin</param>
    /// <param name="refreshToken">The refresh token to save</param>
    /// <param name="expiryDate">The expiry date of the refresh token</param>
    public async Task SaveRefreshTokenAsync(long superAdminId, string refreshToken, DateTime expiryDate)
    {
        await RepositoryExceptionHandler.ExecuteWithExceptionHandlingAsync(
            async () =>
            {
                _logger.LogDebug("Saving refresh token for super admin with ID: {SuperAdminId}", superAdminId);

                // Note: The SysSuperAdmin entity doesn't have RefreshToken and RefreshTokenExpiry properties
                // This would need to be added to the entity and configuration if refresh token support is required
                // For now, we'll use raw SQL to update these columns if they exist in the database

                await _context.Database.ExecuteSqlInterpolatedAsync(
                    $@"UPDATE SYS_SUPER_ADMIN 
                       SET REFRESH_TOKEN = {refreshToken}, 
                           REFRESH_TOKEN_EXPIRY = {expiryDate},
                           UPDATE_DATE = SYSDATE
                       WHERE ROW_ID = {superAdminId}");

                _logger.LogDebug("Saved refresh token for super admin with ID: {SuperAdminId}", superAdminId);
            },
            "SaveSuperAdminRefreshToken",
            _logger);
    }

    /// <summary>
    /// Validates a refresh token and retrieves the associated super admin.
    /// Uses AsNoTracking for read-only query optimization.
    /// Note: This requires RefreshToken and RefreshTokenExpiry columns in the database.
    /// </summary>
    /// <param name="refreshToken">The refresh token to validate</param>
    /// <returns>The SysSuperAdmin entity if the token is valid, null otherwise</returns>
    public async Task<SysSuperAdmin?> ValidateRefreshTokenAsync(string refreshToken)
    {
        return await RepositoryExceptionHandler.ExecuteWithExceptionHandlingAsync(
            async () =>
            {
                _logger.LogDebug("Validating refresh token");

                // Note: The SysSuperAdmin entity doesn't have RefreshToken and RefreshTokenExpiry properties
                // This would need to be added to the entity and configuration if refresh token support is required
                // For now, we'll use raw SQL to query these columns if they exist in the database

                var superAdmins = await _context.SuperAdmins
                    .FromSqlInterpolated(
                        $@"SELECT 
                            ROW_ID,
                            ROW_DESC,
                            ROW_DESC_E,
                            USER_NAME,
                            PASSWORD,
                            EMAIL,
                            PHONE,
                            TWO_FA_SECRET,
                            CASE WHEN TWO_FA_ENABLED = '1' THEN 1 ELSE 0 END AS TWO_FA_ENABLED,
                            CASE WHEN IS_ACTIVE = '1' THEN 1 ELSE 0 END AS IS_ACTIVE,
                            LAST_LOGIN_DATE,
                            CREATION_USER,
                            CREATION_DATE,
                            UPDATE_USER,
                            UPDATE_DATE
                        FROM SYS_SUPER_ADMIN 
                        WHERE REFRESH_TOKEN = {refreshToken} 
                          AND REFRESH_TOKEN_EXPIRY > SYSDATE
                          AND IS_ACTIVE = '1'")
                    .AsNoTracking()
                    .ToListAsync();

                var superAdmin = superAdmins.FirstOrDefault();

                if (superAdmin == null)
                {
                    _logger.LogWarning("Invalid or expired refresh token");
                }
                else
                {
                    _logger.LogDebug("Validated refresh token for super admin: {UserName}", superAdmin.UserName);
                }

                return superAdmin;
            },
            "ValidateSuperAdminRefreshToken",
            _logger);
    }
}
