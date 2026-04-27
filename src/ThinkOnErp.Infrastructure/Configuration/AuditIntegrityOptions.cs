using System.ComponentModel.DataAnnotations;

namespace ThinkOnErp.Infrastructure.Configuration;

/// <summary>
/// Configuration options for audit log integrity verification.
/// Controls cryptographic signing key and integrity check settings.
/// Supports configuration binding from appsettings.json.
/// </summary>
public class AuditIntegrityOptions
{
    /// <summary>
    /// Configuration section name in appsettings.json
    /// </summary>
    public const string SectionName = "AuditIntegrity";

    /// <summary>
    /// Whether audit log integrity verification is enabled. Default: true
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// Base64 encoded signing key for HMAC-SHA256 hash generation.
    /// Must be at least 32 bytes (256 bits) when decoded for security.
    /// Generate using: Convert.ToBase64String(RandomNumberGenerator.GetBytes(32))
    /// </summary>
    [Required(ErrorMessage = "Signing key is required")]
    [MinLength(44, ErrorMessage = "Signing key must be at least 44 characters (32 bytes Base64 encoded)")]
    public string SigningKey { get; set; } = string.Empty;

    /// <summary>
    /// Whether to automatically generate integrity hashes for new audit entries. Default: true
    /// When enabled, hashes are computed and stored with each audit log entry.
    /// </summary>
    public bool AutoGenerateHashes { get; set; } = true;

    /// <summary>
    /// Whether to verify integrity on audit log retrieval. Default: false
    /// When enabled, integrity is verified each time an audit log is read.
    /// WARNING: This adds overhead to read operations.
    /// </summary>
    public bool VerifyOnRead { get; set; } = false;

    /// <summary>
    /// Whether to log integrity verification operations for debugging. Default: false
    /// </summary>
    public bool LogIntegrityOperations { get; set; } = false;

    /// <summary>
    /// Batch size for bulk integrity verification operations. Default: 100
    /// Must be between 10 and 1000.
    /// </summary>
    [Range(10, 1000, ErrorMessage = "BatchSize must be between 10 and 1000")]
    public int BatchSize { get; set; } = 100;

    /// <summary>
    /// Timeout in milliseconds for integrity verification operations. Default: 10000ms (10 seconds)
    /// Must be between 1000 and 60000 milliseconds.
    /// </summary>
    [Range(1000, 60000, ErrorMessage = "VerificationTimeoutMs must be between 1000 and 60000 milliseconds")]
    public int VerificationTimeoutMs { get; set; } = 10000;

    /// <summary>
    /// Whether to trigger alerts when tampering is detected. Default: true
    /// </summary>
    public bool AlertOnTampering { get; set; } = true;

    /// <summary>
    /// Hash algorithm to use. Default: HMACSHA256
    /// Supported values: HMACSHA256, HMACSHA512
    /// </summary>
    [RegularExpression("^(HMACSHA256|HMACSHA512)$", ErrorMessage = "HashAlgorithm must be HMACSHA256 or HMACSHA512")]
    public string HashAlgorithm { get; set; } = "HMACSHA256";
}
