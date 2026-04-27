using System.ComponentModel.DataAnnotations;

namespace ThinkOnErp.Infrastructure.Configuration;

/// <summary>
/// Configuration options for audit data encryption.
/// Controls encryption key management and encryption settings.
/// Supports configuration binding from appsettings.json.
/// </summary>
public class AuditEncryptionOptions
{
    /// <summary>
    /// Configuration section name in appsettings.json
    /// </summary>
    public const string SectionName = "AuditEncryption";

    /// <summary>
    /// Whether audit data encryption is enabled. Default: true
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// Base64 encoded encryption key for AES-256 encryption.
    /// Must be 32 bytes (256 bits) when decoded.
    /// Generate using: Convert.ToBase64String(RandomNumberGenerator.GetBytes(32))
    /// </summary>
    [Required(ErrorMessage = "Encryption key is required")]
    [MinLength(44, ErrorMessage = "Encryption key must be at least 44 characters (32 bytes Base64 encoded)")]
    public string Key { get; set; } = string.Empty;

    /// <summary>
    /// Key rotation period in days. Default: 90 days
    /// After this period, a new key should be generated and old data re-encrypted.
    /// Must be between 30 and 365 days.
    /// </summary>
    [Range(30, 365, ErrorMessage = "KeyRotationDays must be between 30 and 365 days")]
    public int KeyRotationDays { get; set; } = 90;

    /// <summary>
    /// Whether to use hardware security module (HSM) for key storage. Default: false
    /// When enabled, keys are stored in HSM instead of configuration.
    /// </summary>
    public bool UseHsm { get; set; } = false;

    /// <summary>
    /// HSM key identifier when UseHsm is enabled.
    /// </summary>
    public string? HsmKeyId { get; set; }

    /// <summary>
    /// List of field names that should be encrypted in audit logs.
    /// Default: password, token, refreshToken, creditCard, ssn, apiKey
    /// </summary>
    [Required(ErrorMessage = "EncryptedFields array is required")]
    [MinLength(1, ErrorMessage = "At least one encrypted field must be specified")]
    public string[] EncryptedFields { get; set; } = 
    {
        "password", 
        "token", 
        "refreshToken", 
        "creditCard", 
        "ssn", 
        "socialSecurityNumber",
        "apiKey",
        "secretKey",
        "privateKey"
    };

    /// <summary>
    /// Whether to log encryption operations for debugging. Default: false
    /// WARNING: This may expose sensitive information in logs.
    /// </summary>
    public bool LogEncryptionOperations { get; set; } = false;

    /// <summary>
    /// Timeout in milliseconds for encryption operations. Default: 5000ms (5 seconds)
    /// Must be between 100 and 30000 milliseconds.
    /// </summary>
    [Range(100, 30000, ErrorMessage = "EncryptionTimeoutMs must be between 100 and 30000 milliseconds")]
    public int EncryptionTimeoutMs { get; set; } = 5000;
}
