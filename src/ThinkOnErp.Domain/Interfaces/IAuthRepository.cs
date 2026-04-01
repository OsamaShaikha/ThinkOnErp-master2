using ThinkOnErp.Domain.Entities;

namespace ThinkOnErp.Domain.Interfaces;

/// <summary>
/// Repository interface for authentication operations.
/// Defines the contract for user authentication in the Domain layer with zero external dependencies.
/// </summary>
public interface IAuthRepository
{
    /// <summary>
    /// Authenticates a user by username and password hash.
    /// Calls SP_SYS_USERS_LOGIN stored procedure.
    /// </summary>
    /// <param name="userName">The username to authenticate</param>
    /// <param name="passwordHash">The SHA-256 hashed password as hexadecimal string</param>
    /// <returns>The SysUser entity if credentials are valid and user is active, null otherwise</returns>
    Task<SysUser?> AuthenticateAsync(string userName, string passwordHash);
}
