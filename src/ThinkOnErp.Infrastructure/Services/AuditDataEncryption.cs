using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ThinkOnErp.Domain.Interfaces;
using ThinkOnErp.Infrastructure.Configuration;

namespace ThinkOnErp.Infrastructure.Services;

/// <summary>
/// Service for encrypting and decrypting sensitive audit data using AES-256-GCM encryption.
/// Provides thread-safe encryption operations with field-level encryption support.
/// Implements authenticated encryption to prevent tampering.
/// </summary>
public class AuditDataEncryption : IAuditDataEncryption
{
    private readonly byte[] _encryptionKey;
    private readonly ILogger<AuditDataEncryption> _logger;
    private readonly AuditEncryptionOptions _options;
    private readonly SemaphoreSlim _encryptionLock;

    /// <summary>
    /// Initializes a new instance of the AuditDataEncryption service.
    /// </summary>
    /// <param name="options">Encryption configuration options</param>
    /// <param name="logger">Logger instance</param>
    /// <exception cref="InvalidOperationException">Thrown when encryption key is not configured or invalid</exception>
    public AuditDataEncryption(IOptions<AuditEncryptionOptions> options, ILogger<AuditDataEncryption> logger)
    {
        _options = options?.Value ?? throw new ArgumentNullException(nameof(options));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        if (string.IsNullOrWhiteSpace(_options.Key))
        {
            throw new InvalidOperationException("Audit encryption key not configured. Please set AuditEncryption:Key in configuration.");
        }

        try
        {
            _encryptionKey = Convert.FromBase64String(_options.Key);
            
            // Validate key length (must be 32 bytes for AES-256)
            if (_encryptionKey.Length != 32)
            {
                throw new InvalidOperationException($"Encryption key must be 32 bytes (256 bits). Current key is {_encryptionKey.Length} bytes. Generate a valid key using: Convert.ToBase64String(RandomNumberGenerator.GetBytes(32))");
            }
        }
        catch (FormatException ex)
        {
            throw new InvalidOperationException("Encryption key is not a valid Base64 string.", ex);
        }

        // Initialize semaphore for thread-safe operations (allow up to 10 concurrent operations)
        _encryptionLock = new SemaphoreSlim(10, 10);

        _logger.LogInformation("AuditDataEncryption service initialized with AES-256-GCM encryption");
    }

    /// <summary>
    /// Encrypts a plain text string using AES-256-GCM encryption.
    /// </summary>
    /// <param name="plainText">The plain text to encrypt</param>
    /// <returns>Base64 encoded encrypted string with IV and authentication tag, or original string if null/empty</returns>
    public string? Encrypt(string? plainText)
    {
        if (string.IsNullOrEmpty(plainText))
            return plainText;

        if (!_options.Enabled)
        {
            _logger.LogWarning("Encryption is disabled. Returning plain text.");
            return plainText;
        }

        try
        {
            using var aesGcm = new AesGcm(_encryptionKey, AesGcm.TagByteSizes.MaxSize);
            
            // Generate a random nonce (IV) for this encryption operation
            var nonce = new byte[AesGcm.NonceByteSizes.MaxSize]; // 12 bytes for GCM
            RandomNumberGenerator.Fill(nonce);

            // Convert plain text to bytes
            var plainBytes = Encoding.UTF8.GetBytes(plainText);
            
            // Allocate space for cipher text and authentication tag
            var cipherBytes = new byte[plainBytes.Length];
            var tag = new byte[AesGcm.TagByteSizes.MaxSize]; // 16 bytes

            // Encrypt the data
            aesGcm.Encrypt(nonce, plainBytes, cipherBytes, tag);

            // Combine nonce + tag + ciphertext for storage
            var result = new byte[nonce.Length + tag.Length + cipherBytes.Length];
            Buffer.BlockCopy(nonce, 0, result, 0, nonce.Length);
            Buffer.BlockCopy(tag, 0, result, nonce.Length, tag.Length);
            Buffer.BlockCopy(cipherBytes, 0, result, nonce.Length + tag.Length, cipherBytes.Length);

            if (_options.LogEncryptionOperations)
            {
                _logger.LogDebug("Successfully encrypted data. Plain text length: {PlainLength}, Cipher length: {CipherLength}", 
                    plainText.Length, result.Length);
            }

            return Convert.ToBase64String(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to encrypt audit data");
            throw new InvalidOperationException("Encryption operation failed. See inner exception for details.", ex);
        }
    }

    /// <summary>
    /// Decrypts a cipher text string that was encrypted using AES-256-GCM.
    /// </summary>
    /// <param name="cipherText">The Base64 encoded cipher text with nonce and tag</param>
    /// <returns>Decrypted plain text string, or original string if null/empty</returns>
    public string? Decrypt(string? cipherText)
    {
        if (string.IsNullOrEmpty(cipherText))
            return cipherText;

        if (!_options.Enabled)
        {
            _logger.LogWarning("Encryption is disabled. Returning cipher text as-is.");
            return cipherText;
        }

        try
        {
            var buffer = Convert.FromBase64String(cipherText);

            // Extract nonce, tag, and cipher bytes
            var nonceSize = AesGcm.NonceByteSizes.MaxSize; // 12 bytes
            var tagSize = AesGcm.TagByteSizes.MaxSize; // 16 bytes

            if (buffer.Length < nonceSize + tagSize)
            {
                throw new InvalidOperationException($"Cipher text is too short. Expected at least {nonceSize + tagSize} bytes, got {buffer.Length} bytes.");
            }

            var nonce = new byte[nonceSize];
            var tag = new byte[tagSize];
            var cipherBytes = new byte[buffer.Length - nonceSize - tagSize];

            Buffer.BlockCopy(buffer, 0, nonce, 0, nonceSize);
            Buffer.BlockCopy(buffer, nonceSize, tag, 0, tagSize);
            Buffer.BlockCopy(buffer, nonceSize + tagSize, cipherBytes, 0, cipherBytes.Length);

            using var aesGcm = new AesGcm(_encryptionKey, AesGcm.TagByteSizes.MaxSize);
            
            var plainBytes = new byte[cipherBytes.Length];

            // Decrypt and verify authentication tag
            aesGcm.Decrypt(nonce, cipherBytes, tag, plainBytes);

            var result = Encoding.UTF8.GetString(plainBytes);

            if (_options.LogEncryptionOperations)
            {
                _logger.LogDebug("Successfully decrypted data. Cipher length: {CipherLength}, Plain text length: {PlainLength}", 
                    buffer.Length, result.Length);
            }

            return result;
        }
        catch (FormatException ex)
        {
            _logger.LogError(ex, "Failed to decrypt audit data: Invalid Base64 format");
            throw new InvalidOperationException("Decryption failed: Invalid cipher text format.", ex);
        }
        catch (CryptographicException ex)
        {
            _logger.LogError(ex, "Failed to decrypt audit data: Authentication tag verification failed or invalid key");
            throw new InvalidOperationException("Decryption failed: Data may have been tampered with or wrong encryption key.", ex);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to decrypt audit data");
            throw new InvalidOperationException("Decryption operation failed. See inner exception for details.", ex);
        }
    }

    /// <summary>
    /// Asynchronously encrypts a plain text string using AES-256-GCM encryption.
    /// </summary>
    /// <param name="plainText">The plain text to encrypt</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Base64 encoded encrypted string with IV and authentication tag, or original string if null/empty</returns>
    public async Task<string?> EncryptAsync(string? plainText, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(plainText))
            return plainText;

        // Acquire semaphore for thread-safe operation
        await _encryptionLock.WaitAsync(cancellationToken);
        
        try
        {
            // Use Task.Run to offload CPU-intensive encryption to thread pool
            return await Task.Run(() => Encrypt(plainText), cancellationToken);
        }
        finally
        {
            _encryptionLock.Release();
        }
    }

    /// <summary>
    /// Asynchronously decrypts a cipher text string that was encrypted using AES-256-GCM.
    /// </summary>
    /// <param name="cipherText">The Base64 encoded cipher text with nonce and tag</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Decrypted plain text string, or original string if null/empty</returns>
    public async Task<string?> DecryptAsync(string? cipherText, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(cipherText))
            return cipherText;

        // Acquire semaphore for thread-safe operation
        await _encryptionLock.WaitAsync(cancellationToken);
        
        try
        {
            // Use Task.Run to offload CPU-intensive decryption to thread pool
            return await Task.Run(() => Decrypt(cipherText), cancellationToken);
        }
        finally
        {
            _encryptionLock.Release();
        }
    }

    /// <summary>
    /// Encrypts multiple fields in a dictionary.
    /// Only encrypts fields that are marked as sensitive.
    /// </summary>
    /// <param name="data">Dictionary containing field names and values</param>
    /// <param name="sensitiveFields">List of field names to encrypt</param>
    /// <returns>Dictionary with sensitive fields encrypted</returns>
    public Dictionary<string, string?> EncryptFields(Dictionary<string, string?> data, IEnumerable<string> sensitiveFields)
    {
        if (data == null)
            throw new ArgumentNullException(nameof(data));
        
        if (sensitiveFields == null)
            throw new ArgumentNullException(nameof(sensitiveFields));

        var result = new Dictionary<string, string?>(data, StringComparer.OrdinalIgnoreCase);
        var sensitiveFieldSet = new HashSet<string>(sensitiveFields, StringComparer.OrdinalIgnoreCase);

        foreach (var field in sensitiveFieldSet)
        {
            if (result.TryGetValue(field, out var value) && !string.IsNullOrEmpty(value))
            {
                try
                {
                    result[field] = Encrypt(value);
                    
                    if (_options.LogEncryptionOperations)
                    {
                        _logger.LogDebug("Encrypted field: {FieldName}", field);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to encrypt field: {FieldName}", field);
                    // Keep original value if encryption fails
                }
            }
        }

        return result;
    }

    /// <summary>
    /// Decrypts multiple fields in a dictionary.
    /// Only decrypts fields that are marked as sensitive.
    /// </summary>
    /// <param name="data">Dictionary containing field names and encrypted values</param>
    /// <param name="sensitiveFields">List of field names to decrypt</param>
    /// <returns>Dictionary with sensitive fields decrypted</returns>
    public Dictionary<string, string?> DecryptFields(Dictionary<string, string?> data, IEnumerable<string> sensitiveFields)
    {
        if (data == null)
            throw new ArgumentNullException(nameof(data));
        
        if (sensitiveFields == null)
            throw new ArgumentNullException(nameof(sensitiveFields));

        var result = new Dictionary<string, string?>(data, StringComparer.OrdinalIgnoreCase);
        var sensitiveFieldSet = new HashSet<string>(sensitiveFields, StringComparer.OrdinalIgnoreCase);

        foreach (var field in sensitiveFieldSet)
        {
            if (result.TryGetValue(field, out var value) && !string.IsNullOrEmpty(value))
            {
                try
                {
                    result[field] = Decrypt(value);
                    
                    if (_options.LogEncryptionOperations)
                    {
                        _logger.LogDebug("Decrypted field: {FieldName}", field);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to decrypt field: {FieldName}. Data may be corrupted or use wrong key.", field);
                    // Keep encrypted value if decryption fails
                }
            }
        }

        return result;
    }
}
