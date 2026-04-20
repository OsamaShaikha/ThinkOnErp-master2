using ThinkOnErp.Domain.Entities;

namespace ThinkOnErp.Domain.Interfaces;

/// <summary>
/// Repository interface for Super Admin operations
/// </summary>
public interface ISuperAdminRepository
{
    /// <summary>
    /// Retrieves all active super admin accounts
    /// </summary>
    Task<List<SysSuperAdmin>> GetAllAsync();

    /// <summary>
    /// Retrieves a super admin by ID
    /// </summary>
    Task<SysSuperAdmin?> GetByIdAsync(Int64 id);

    /// <summary>
    /// Retrieves a super admin by username
    /// </summary>
    Task<SysSuperAdmin?> GetByUsernameAsync(string username);

    /// <summary>
    /// Retrieves a super admin by email
    /// </summary>
    Task<SysSuperAdmin?> GetByEmailAsync(string email);

    /// <summary>
    /// Creates a new super admin account
    /// </summary>
    Task<Int64> CreateAsync(SysSuperAdmin superAdmin);

    /// <summary>
    /// Updates an existing super admin account
    /// </summary>
    Task<Int64> UpdateAsync(SysSuperAdmin superAdmin);

    /// <summary>
    /// Soft deletes a super admin account (sets IS_ACTIVE = '0')
    /// </summary>
    Task<Int64> DeleteAsync(Int64 id);

    /// <summary>
    /// Changes the password for a super admin
    /// </summary>
    Task<Int64> ChangePasswordAsync(Int64 id, string newPasswordHash, string updateUser);

    /// <summary>
    /// Enables 2FA for a super admin
    /// </summary>
    Task<Int64> Enable2FAAsync(Int64 id, string twoFaSecret, string updateUser);

    /// <summary>
    /// Disables 2FA for a super admin
    /// </summary>
    Task<Int64> Disable2FAAsync(Int64 id, string updateUser);

    /// <summary>
    /// Updates the last login date for a super admin
    /// </summary>
    Task<Int64> UpdateLastLoginAsync(Int64 id);

    /// <summary>
    /// Authenticates a super admin by username and password hash
    /// </summary>
    /// <param name="userName">The username to authenticate</param>
    /// <param name="passwordHash">The SHA-256 hashed password</param>
    /// <returns>The SysSuperAdmin entity if credentials are valid and account is active, null otherwise</returns>
    Task<SysSuperAdmin?> AuthenticateAsync(string userName, string passwordHash);

    /// <summary>
    /// Stores a refresh token for a super admin
    /// </summary>
    /// <param name="superAdminId">The super admin ID</param>
    /// <param name="refreshToken">The refresh token to store</param>
    /// <param name="expiryDate">The expiry date of the refresh token</param>
    Task SaveRefreshTokenAsync(long superAdminId, string refreshToken, DateTime expiryDate);

    /// <summary>
    /// Validates a refresh token and retrieves the associated super admin
    /// </summary>
    /// <param name="refreshToken">The refresh token to validate</param>
    /// <returns>The SysSuperAdmin entity if token is valid and not expired, null otherwise</returns>
    Task<SysSuperAdmin?> ValidateRefreshTokenAsync(string refreshToken);
}
