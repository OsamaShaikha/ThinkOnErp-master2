using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ThinkOnErp.Domain.Interfaces;
using ThinkOnErp.Domain.Models;
using ThinkOnErp.Infrastructure.Configuration;

namespace ThinkOnErp.Infrastructure.Services;

// TODO: Implement IKeyStorageProvider interface in Domain layer
/*
/// <summary>
/// Key storage provider that reads keys from application configuration (appsettings.json or environment variables).
/// This is the simplest storage backend but least secure for production use.
/// Keys are stored in plain text in configuration files.
/// </summary>
internal class ConfigurationKeyStorageProvider : IKeyStorageProvider
{
    private readonly IOptions<AuditEncryptionOptions> _encryptionOptions;
    private readonly IOptions<AuditIntegrityOptions> _integrityOptions;
    private readonly ILogger _logger;

    public ConfigurationKeyStorageProvider(
        IOptions<AuditEncryptionOptions> encryptionOptions,
        IOptions<AuditIntegrityOptions> integrityOptions,
        ILogger logger)
    {
        _encryptionOptions = encryptionOptions ?? throw new ArgumentNullException(nameof(encryptionOptions));
        _integrityOptions = integrityOptions ?? throw new ArgumentNullException(nameof(integrityOptions));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public Task StoreKeyAsync(string keyId, byte[] keyBytes, KeyMetadata metadata, CancellationToken cancellationToken)
    {
        _logger.LogWarning(
            "Configuration storage provider does not support storing keys. " +
            "Keys must be manually added to appsettings.json or environment variables. " +
            "Generated key (Base64): {Key}",
            Convert.ToBase64String(keyBytes));

        return Task.CompletedTask;
    }

    public Task<byte[]?> RetrieveKeyAsync(string keyId, CancellationToken cancellationToken)
    {
        // Try to retrieve from configuration based on key type
        // For configuration provider, we only support the current active keys
        if (keyId.StartsWith("enc-") || keyId == "current-encryption")
        {
            if (!string.IsNullOrWhiteSpace(_encryptionOptions.Value.Key))
            {
                return Task.FromResult<byte[]?>(Convert.FromBase64String(_encryptionOptions.Value.Key));
            }
        }
        else if (keyId.StartsWith("sig-") || keyId == "current-signing")
        {
            if (!string.IsNullOrWhiteSpace(_integrityOptions.Value.SigningKey))
            {
                return Task.FromResult<byte[]?>(Convert.FromBase64String(_integrityOptions.Value.SigningKey));
            }
        }

        _logger.LogWarning("Key {KeyId} not found in configuration", keyId);
        return Task.FromResult<byte[]?>(null);
    }

    public Task<List<KeyMetadata>> GetAllKeysMetadataAsync(KeyType keyType, CancellationToken cancellationToken)
    {
        var keys = new List<KeyMetadata>();

        if (keyType == KeyType.Encryption && !string.IsNullOrWhiteSpace(_encryptionOptions.Value.Key))
        {
            keys.Add(new KeyMetadata
            {
                KeyId = "current-encryption",
                KeyType = KeyType.Encryption,
                CreatedAt = DateTime.UtcNow, // Unknown, use current time
                ExpiresAt = null, // Configuration keys don't expire automatically
                IsActive = true,
                StorageBackend = KeyStorageBackend.Configuration,
                Description = "Encryption key from configuration"
            });
        }

        if (keyType == KeyType.Signing && !string.IsNullOrWhiteSpace(_integrityOptions.Value.SigningKey))
        {
            keys.Add(new KeyMetadata
            {
                KeyId = "current-signing",
                KeyType = KeyType.Signing,
                CreatedAt = DateTime.UtcNow, // Unknown, use current time
                ExpiresAt = null, // Configuration keys don't expire automatically
                IsActive = true,
                StorageBackend = KeyStorageBackend.Configuration,
                Description = "Signing key from configuration"
            });
        }

        return Task.FromResult(keys);
    }

    public Task UpdateKeyMetadataAsync(KeyMetadata metadata, CancellationToken cancellationToken)
    {
        _logger.LogWarning(
            "Configuration storage provider does not support updating key metadata. " +
            "Metadata changes are ignored.");

        return Task.CompletedTask;
    }

    public Task<bool> DeleteKeyAsync(string keyId, CancellationToken cancellationToken)
    {
        _logger.LogWarning(
            "Configuration storage provider does not support deleting keys. " +
            "Keys must be manually removed from appsettings.json or environment variables.");

        return Task.FromResult(false);
    }
}
*/
