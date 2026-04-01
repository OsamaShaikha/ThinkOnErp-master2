using System.Security.Cryptography;
using System.Text;

namespace ThinkOnErp.Infrastructure.Services;

/// <summary>
/// Service for hashing passwords using SHA-256 algorithm
/// </summary>
public class PasswordHashingService
{
    /// <summary>
    /// Hashes a password using SHA-256 and returns the hexadecimal string representation
    /// </summary>
    /// <param name="password">The plain text password to hash</param>
    /// <returns>SHA-256 hash as hexadecimal string</returns>
    public virtual string HashPassword(string password)
    {
        if (string.IsNullOrEmpty(password))
        {
            throw new ArgumentException("Password cannot be null or empty", nameof(password));
        }

        using var sha256 = SHA256.Create();
        var passwordBytes = Encoding.UTF8.GetBytes(password);
        var hashBytes = sha256.ComputeHash(passwordBytes);
        
        // Convert hash bytes to hexadecimal string
        return Convert.ToHexString(hashBytes);
    }
}
