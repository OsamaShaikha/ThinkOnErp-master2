namespace ThinkOnErp.Infrastructure.Services.KeyManagement;

/// <summary>
/// Interface for key storage providers.
/// Implementations provide keys from different storage backends (configuration, files, cloud services).
/// </summary>
public interface IKeyProvider
{
    /// <summary>
    /// Name of the provider (for logging and diagnostics)
    /// </summary>
    string ProviderName { get; }

    /// <summary>
    /// Retrieves the current encryption key.
    /// </summary>
    Task<string> GetEncryptionKeyAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves the current signing key.
    /// </summary>
    Task<string> GetSigningKeyAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves a specific version of the encryption key.
    /// </summary>
    Task<string> GetEncryptionKeyVersionAsync(string version, CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves a specific version of the signing key.
    /// </summary>
    Task<string> GetSigningKeyVersionAsync(string version, CancellationToken cancellationToken = default);

    /// <summary>
    /// Rotates the encryption key and returns the new version identifier.
    /// </summary>
    Task<string> RotateEncryptionKeyAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Rotates the signing key and returns the new version identifier.
    /// </summary>
    Task<string> RotateSigningKeyAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the current version identifier for the encryption key.
    /// </summary>
    Task<string> GetCurrentEncryptionKeyVersionAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the current version identifier for the signing key.
    /// </summary>
    Task<string> GetCurrentSigningKeyVersionAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Validates that keys are accessible and properly formatted.
    /// </summary>
    Task<bool> ValidateKeysAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the date when the encryption key was last rotated.
    /// </summary>
    Task<DateTime?> GetEncryptionKeyLastRotatedAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the date when the signing key was last rotated.
    /// </summary>
    Task<DateTime?> GetSigningKeyLastRotatedAsync(CancellationToken cancellationToken = default);
}
