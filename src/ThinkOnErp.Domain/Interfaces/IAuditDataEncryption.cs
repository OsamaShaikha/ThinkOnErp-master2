namespace ThinkOnErp.Domain.Interfaces;

/// <summary>
/// Interface for encrypting and decrypting sensitive audit data.
/// Uses AES-256-GCM for authenticated encryption with field-level encryption support.
/// </summary>
public interface IAuditDataEncryption
{
    /// <summary>
    /// Encrypts a plain text string using AES-256-GCM encryption.
    /// Returns the encrypted data as a Base64 string with the IV prepended.
    /// </summary>
    /// <param name="plainText">The plain text to encrypt</param>
    /// <returns>Base64 encoded encrypted string with IV, or original string if null/empty</returns>
    string? Encrypt(string? plainText);

    /// <summary>
    /// Decrypts a cipher text string that was encrypted using AES-256-GCM.
    /// Expects the cipher text to be Base64 encoded with the IV prepended.
    /// </summary>
    /// <param name="cipherText">The Base64 encoded cipher text with IV</param>
    /// <returns>Decrypted plain text string, or original string if null/empty</returns>
    string? Decrypt(string? cipherText);

    /// <summary>
    /// Asynchronously encrypts a plain text string using AES-256-GCM encryption.
    /// Returns the encrypted data as a Base64 string with the IV prepended.
    /// </summary>
    /// <param name="plainText">The plain text to encrypt</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Base64 encoded encrypted string with IV, or original string if null/empty</returns>
    Task<string?> EncryptAsync(string? plainText, CancellationToken cancellationToken = default);

    /// <summary>
    /// Asynchronously decrypts a cipher text string that was encrypted using AES-256-GCM.
    /// Expects the cipher text to be Base64 encoded with the IV prepended.
    /// </summary>
    /// <param name="cipherText">The Base64 encoded cipher text with IV</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Decrypted plain text string, or original string if null/empty</returns>
    Task<string?> DecryptAsync(string? cipherText, CancellationToken cancellationToken = default);

    /// <summary>
    /// Encrypts multiple fields in a dictionary.
    /// Only encrypts fields that are marked as sensitive.
    /// </summary>
    /// <param name="data">Dictionary containing field names and values</param>
    /// <param name="sensitiveFields">List of field names to encrypt</param>
    /// <returns>Dictionary with sensitive fields encrypted</returns>
    Dictionary<string, string?> EncryptFields(Dictionary<string, string?> data, IEnumerable<string> sensitiveFields);

    /// <summary>
    /// Decrypts multiple fields in a dictionary.
    /// Only decrypts fields that are marked as sensitive.
    /// </summary>
    /// <param name="data">Dictionary containing field names and encrypted values</param>
    /// <param name="sensitiveFields">List of field names to decrypt</param>
    /// <returns>Dictionary with sensitive fields decrypted</returns>
    Dictionary<string, string?> DecryptFields(Dictionary<string, string?> data, IEnumerable<string> sensitiveFields);
}
