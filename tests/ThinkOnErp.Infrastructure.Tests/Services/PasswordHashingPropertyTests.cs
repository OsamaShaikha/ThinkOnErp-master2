using FsCheck;
using FsCheck.Xunit;
using ThinkOnErp.Infrastructure.Services;
using Xunit;

namespace ThinkOnErp.Infrastructure.Tests.Services;

/// <summary>
/// Property-based tests for password hashing using FsCheck.
/// These tests validate universal properties that should hold for all valid inputs.
/// </summary>
public class PasswordHashingPropertyTests
{
    private const int MinIterations = 100;

    /// <summary>
    /// **Validates: Requirements 3.1, 3.4**
    /// 
    /// Property 5: Password Hashing on Storage
    /// 
    /// For any user password, when stored in the database, it must be hashed using SHA-256 
    /// and stored as a hexadecimal string representation, never in plain text.
    /// </summary>
    [Property(MaxTest = MinIterations, Arbitrary = new[] { typeof(Generators) })]
    public Property PasswordHashingOnStorage_AlwaysProducesValidSHA256HexHash(string password)
    {
        // Arrange
        var service = new PasswordHashingService();

        // Act: Hash the password (simulating storage)
        var hashedPassword = service.HashPassword(password);

        // Property 1: Hashed password must not be null or empty
        var isNotNullOrEmpty = !string.IsNullOrEmpty(hashedPassword);

        // Property 2: Hashed password must be exactly 64 characters (SHA-256 produces 32 bytes = 64 hex chars)
        var hasCorrectLength = hashedPassword.Length == 64;

        // Property 3: Hashed password must be a valid hexadecimal string (only 0-9, A-F)
        var isValidHex = System.Text.RegularExpressions.Regex.IsMatch(hashedPassword, "^[0-9A-F]+$");

        // Property 4: Hashed password must not equal the plain text password (never stored in plain text)
        var isNotPlainText = hashedPassword != password;

        // Property 5: Same password always produces the same hash (deterministic)
        var secondHash = service.HashPassword(password);
        var isDeterministic = hashedPassword == secondHash;

        // Property 6: Hash must be uppercase hexadecimal (as per Convert.ToHexString behavior)
        var isUppercase = hashedPassword == hashedPassword.ToUpper();

        // Combine all properties with descriptive labels
        var result = isNotNullOrEmpty
            && hasCorrectLength
            && isValidHex
            && isNotPlainText
            && isDeterministic
            && isUppercase;

        return result
            .Label($"Is not null or empty: {isNotNullOrEmpty}")
            .Label($"Has correct length (64): {hasCorrectLength}")
            .Label($"Is valid hexadecimal: {isValidHex}")
            .Label($"Is not plain text: {isNotPlainText}")
            .Label($"Is deterministic: {isDeterministic}")
            .Label($"Is uppercase: {isUppercase}");
    }

    /// <summary>
    /// Custom generators for property-based testing.
    /// </summary>
    public static class Generators
    {
        /// <summary>
        /// Generates arbitrary valid password strings for property testing.
        /// Passwords must be non-null and non-empty as per PasswordHashingService validation.
        /// </summary>
        public static Arbitrary<string> Password()
        {
            var allowedChars = "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789!@#$%^&*()_+-=[]{}|;:,.<>?".ToCharArray();
            
            var passwordGenerator = from length in Gen.Choose(1, 100)
                                   from chars in Gen.ArrayOf(length, Gen.Elements(allowedChars))
                                   select new string(chars);

            return Arb.From(passwordGenerator.Where(p => !string.IsNullOrEmpty(p)));
        }
    }
}
