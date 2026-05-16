using System.Security.Cryptography;

namespace ThinkOnErp.Infrastructure.Services;

/// <summary>
/// Service for hashing passwords using PBKDF2 with HMAC-SHA256 and a random salt
/// </summary>
public class PasswordHashingService
{
    private const int SaltSize = 16;
    private const int HashSize = 32;
    private const int Iterations = 100000;

    /// <summary>
    /// Hashes a password using PBKDF2 with a random salt
    /// </summary>
    /// <param name="password">The plain text password to hash</param>
    /// <returns>Base64-encoded string containing salt + hash</returns>
    public virtual string HashPassword(string password)
    {
        if (string.IsNullOrEmpty(password))
        {
            throw new ArgumentException("Password cannot be null or empty", nameof(password));
        }

        var salt = RandomNumberGenerator.GetBytes(SaltSize);
        var hash = Rfc2898DeriveBytes.Pbkdf2(password, salt, Iterations, HashAlgorithmName.SHA256, HashSize);

        var result = new byte[SaltSize + HashSize];
        Buffer.BlockCopy(salt, 0, result, 0, SaltSize);
        Buffer.BlockCopy(hash, 0, result, SaltSize, HashSize);

        return Convert.ToBase64String(result);
    }

    /// <summary>
    /// Verifies a password against a hash created by HashPassword
    /// </summary>
    /// <param name="password">The plain text password to verify</param>
    /// <param name="hashedPassword">The stored Base64-encoded hash</param>
    /// <returns>True if the password matches the hash</returns>
    public virtual bool VerifyPassword(string password, string hashedPassword)
    {
        if (string.IsNullOrEmpty(password) || string.IsNullOrEmpty(hashedPassword))
        {
            return false;
        }

        var hashBytes = Convert.FromBase64String(hashedPassword);
        if (hashBytes.Length != SaltSize + HashSize)
        {
            return false;
        }

        var salt = new byte[SaltSize];
        var storedHash = new byte[HashSize];
        Buffer.BlockCopy(hashBytes, 0, salt, 0, SaltSize);
        Buffer.BlockCopy(hashBytes, SaltSize, storedHash, 0, HashSize);

        var computedHash = Rfc2898DeriveBytes.Pbkdf2(password, salt, Iterations, HashAlgorithmName.SHA256, HashSize); 
        return CryptographicOperations.FixedTimeEquals(storedHash, computedHash);


    }
}
