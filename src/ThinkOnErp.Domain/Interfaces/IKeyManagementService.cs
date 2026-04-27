namespace ThinkOnErp.Domain.Interfaces;

/// <summary>
/// Service for secure management of encryption and signing keys.
/// Supports multiple key storage providers (Azure Key Vault, AWS Secrets Manager, local storage).
/// Provides key rotation and versioning capabilities.
/// </summary>
public interface IKeyManagementService
{
    /// <summary>
    /// Retrieves the current encryption key for audit data encryption.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Base64 encoded encryption key</returns>
    Task<string> GetEncryptionKeyAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves the current signing key for audit log integrity verification.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Base64 encoded signing key</returns>
    Task<string> GetSigningKeyAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves a specific version of the encryption key (for key rotation scenarios).
    /// </summary>
    /// <param name="version">Key version identifier</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Base64 encoded encryption key for the specified version</returns>
    Task<string> GetEncryptionKeyVersionAsync(string version, CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves a specific version of the signing key (for key rotation scenarios).
    /// </summary>
    /// <param name="version">Key version identifier</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Base64 encoded signing key for the specified version</returns>
    Task<string> GetSigningKeyVersionAsync(string version, CancellationToken cancellationToken = default);

    /// <summary>
    /// Rotates the encryption key by generating a new version.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>New key version identifier</returns>
    Task<string> RotateEncryptionKeyAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Rotates the signing key by generating a new version.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>New key version identifier</returns>
    Task<string> RotateSigningKeyAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the current version identifier for the encryption key.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Current encryption key version</returns>
    Task<string> GetCurrentEncryptionKeyVersionAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the current version identifier for the signing key.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Current signing key version</returns>
    Task<string> GetCurrentSigningKeyVersionAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Validates that all required keys are configured and accessible.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>True if all keys are valid and accessible</returns>
    Task<bool> ValidateKeysAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets metadata about key rotation status and next rotation date.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Key rotation metadata</returns>
    Task<KeyRotationMetadata> GetKeyRotationMetadataAsync(CancellationToken cancellationToken = default);
}

/// <summary>
/// Metadata about key rotation status.
/// </summary>
public class KeyRotationMetadata
{
    /// <summary>
    /// Current encryption key version
    /// </summary>
    public string EncryptionKeyVersion { get; set; } = string.Empty;

    /// <summary>
    /// Current signing key version
    /// </summary>
    public string SigningKeyVersion { get; set; } = string.Empty;

    /// <summary>
    /// Date when encryption key was last rotated
    /// </summary>
    public DateTime? EncryptionKeyLastRotated { get; set; }

    /// <summary>
    /// Date when signing key was last rotated
    /// </summary>
    public DateTime? SigningKeyLastRotated { get; set; }

    /// <summary>
    /// Date when encryption key should be rotated next
    /// </summary>
    public DateTime? EncryptionKeyNextRotation { get; set; }

    /// <summary>
    /// Date when signing key should be rotated next
    /// </summary>
    public DateTime? SigningKeyNextRotation { get; set; }

    /// <summary>
    /// Whether encryption key rotation is overdue
    /// </summary>
    public bool EncryptionKeyRotationOverdue { get; set; }

    /// <summary>
    /// Whether signing key rotation is overdue
    /// </summary>
    public bool SigningKeyRotationOverdue { get; set; }

    /// <summary>
    /// Key storage provider being used (AzureKeyVault, AwsSecretsManager, LocalStorage, Configuration)
    /// </summary>
    public string StorageProvider { get; set; } = string.Empty;
}
