using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using ThinkOnErp.Domain.Interfaces;
using ThinkOnErp.Domain.Models;
using ThinkOnErp.Infrastructure.Configuration;

namespace ThinkOnErp.Infrastructure.Services;

// TODO: Implement IKeyStorageProvider interface and LocalProtectedStorageOptions in Domain layer
/*
/// <summary>
/// Key storage provider that stores keys in local protected storage using DPAPI (Windows) or similar mechanisms.
/// Keys are encrypted at rest using the operating system's data protection APIs.
/// This provides better security than configuration-based storage but is platform-specific.
/// </summary>
internal class LocalProtectedStorageProvider : IKeyStorageProvider
{
    private readonly LocalProtectedStorageOptions _options;
    private readonly ILogger _logger;
    private readonly string _keysDirectory;
    private readonly string _metadataDirectory;

    public LocalProtectedStorageProvider(LocalProtectedStorageOptions options, ILogger logger)
    {
        _options = options ?? throw new ArgumentNullException(nameof(options));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        _keysDirectory = Path.Combine(_options.StoragePath, "keys");
        _metadataDirectory = Path.Combine(_options.StoragePath, "metadata");

        // Ensure directories exist
        Directory.CreateDirectory(_keysDirectory);
        Directory.CreateDirectory(_metadataDirectory);

        _logger.LogInformation(
            "LocalProtectedStorageProvider initialized. Storage path: {StoragePath}, Scope: {Scope}",
            _options.StoragePath,
            _options.UseMachineScope ? "Machine" : "User");
    }

    public async Task StoreKeyAsync(
        string keyId,
        byte[] keyBytes,
        KeyMetadata metadata,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(keyId))
            throw new ArgumentException("Key ID cannot be null or empty", nameof(keyId));

        if (keyBytes == null || keyBytes.Length == 0)
            throw new ArgumentException("Key bytes cannot be null or empty", nameof(keyBytes));

        try
        {
            // Encrypt the key using DPAPI
            var scope = _options.UseMachineScope
                ? DataProtectionScope.LocalMachine
                : DataProtectionScope.CurrentUser;

            var encryptedKey = ProtectedData.Protect(keyBytes, null, scope);

            // Store encrypted key to file
            var keyFilePath = GetKeyFilePath(keyId);
            await File.WriteAllBytesAsync(keyFilePath, encryptedKey, cancellationToken);

            // Store metadata
            var metadataFilePath = GetMetadataFilePath(keyId);
            var metadataJson = JsonSerializer.Serialize(metadata, new JsonSerializerOptions
            {
                WriteIndented = true
            });
            await File.WriteAllTextAsync(metadataFilePath, metadataJson, cancellationToken);

            _logger.LogInformation(
                "Stored {KeyType} key {KeyId} in local protected storage",
                metadata.KeyType, keyId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to store key {KeyId} in local protected storage", keyId);
            throw new InvalidOperationException($"Failed to store key {keyId}", ex);
        }
    }

    public async Task<byte[]?> RetrieveKeyAsync(string keyId, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(keyId))
            throw new ArgumentException("Key ID cannot be null or empty", nameof(keyId));

        try
        {
            var keyFilePath = GetKeyFilePath(keyId);

            if (!File.Exists(keyFilePath))
            {
                _logger.LogWarning("Key file not found for key {KeyId}", keyId);
                return null;
            }

            // Read encrypted key from file
            var encryptedKey = await File.ReadAllBytesAsync(keyFilePath, cancellationToken);

            // Decrypt the key using DPAPI
            var scope = _options.UseMachineScope
                ? DataProtectionScope.LocalMachine
                : DataProtectionScope.CurrentUser;

            var keyBytes = ProtectedData.Unprotect(encryptedKey, null, scope);

            return keyBytes;
        }
        catch (CryptographicException ex)
        {
            _logger.LogError(
                ex,
                "Failed to decrypt key {KeyId}. The key may have been encrypted with a different scope or on a different machine.",
                keyId);
            throw new InvalidOperationException(
                $"Failed to decrypt key {keyId}. Ensure the key was encrypted on this machine with the same scope.",
                ex);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to retrieve key {KeyId} from local protected storage", keyId);
            throw new InvalidOperationException($"Failed to retrieve key {keyId}", ex);
        }
    }

    public async Task<List<KeyMetadata>> GetAllKeysMetadataAsync(
        KeyType keyType,
        CancellationToken cancellationToken)
    {
        var metadataList = new List<KeyMetadata>();

        try
        {
            if (!Directory.Exists(_metadataDirectory))
            {
                return metadataList;
            }

            var metadataFiles = Directory.GetFiles(_metadataDirectory, "*.json");

            foreach (var metadataFile in metadataFiles)
            {
                try
                {
                    var metadataJson = await File.ReadAllTextAsync(metadataFile, cancellationToken);
                    var metadata = JsonSerializer.Deserialize<KeyMetadata>(metadataJson);

                    if (metadata != null && metadata.KeyType == keyType)
                    {
                        metadataList.Add(metadata);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to read metadata file: {MetadataFile}", metadataFile);
                }
            }

            return metadataList.OrderByDescending(m => m.CreatedAt).ToList();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get all keys metadata for key type {KeyType}", keyType);
            throw new InvalidOperationException($"Failed to get keys metadata for {keyType}", ex);
        }
    }

    public async Task UpdateKeyMetadataAsync(KeyMetadata metadata, CancellationToken cancellationToken)
    {
        if (metadata == null)
            throw new ArgumentNullException(nameof(metadata));

        if (string.IsNullOrWhiteSpace(metadata.KeyId))
            throw new ArgumentException("Key ID cannot be null or empty", nameof(metadata.KeyId));

        try
        {
            var metadataFilePath = GetMetadataFilePath(metadata.KeyId);

            if (!File.Exists(metadataFilePath))
            {
                _logger.LogWarning("Metadata file not found for key {KeyId}", metadata.KeyId);
                return;
            }

            var metadataJson = JsonSerializer.Serialize(metadata, new JsonSerializerOptions
            {
                WriteIndented = true
            });

            await File.WriteAllTextAsync(metadataFilePath, metadataJson, cancellationToken);

            _logger.LogInformation("Updated metadata for key {KeyId}", metadata.KeyId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to update metadata for key {KeyId}", metadata.KeyId);
            throw new InvalidOperationException($"Failed to update metadata for key {metadata.KeyId}", ex);
        }
    }

    public Task<bool> DeleteKeyAsync(string keyId, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(keyId))
            throw new ArgumentException("Key ID cannot be null or empty", nameof(keyId));

        try
        {
            var keyFilePath = GetKeyFilePath(keyId);
            var metadataFilePath = GetMetadataFilePath(keyId);

            var deleted = false;

            if (File.Exists(keyFilePath))
            {
                File.Delete(keyFilePath);
                deleted = true;
            }

            if (File.Exists(metadataFilePath))
            {
                File.Delete(metadataFilePath);
                deleted = true;
            }

            if (deleted)
            {
                _logger.LogInformation("Deleted key {KeyId} from local protected storage", keyId);
            }
            else
            {
                _logger.LogWarning("Key {KeyId} not found for deletion", keyId);
            }

            return Task.FromResult(deleted);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to delete key {KeyId} from local protected storage", keyId);
            throw new InvalidOperationException($"Failed to delete key {keyId}", ex);
        }
    }

    private string GetKeyFilePath(string keyId)
    {
        // Sanitize key ID to prevent path traversal
        var sanitizedKeyId = SanitizeFileName(keyId);
        return Path.Combine(_keysDirectory, $"{sanitizedKeyId}.key");
    }

    private string GetMetadataFilePath(string keyId)
    {
        // Sanitize key ID to prevent path traversal
        var sanitizedKeyId = SanitizeFileName(keyId);
        return Path.Combine(_metadataDirectory, $"{sanitizedKeyId}.json");
    }

    private static string SanitizeFileName(string fileName)
    {
        // Remove invalid file name characters
        var invalidChars = Path.GetInvalidFileNameChars();
        var sanitized = new StringBuilder();

        foreach (var c in fileName)
        {
            if (!invalidChars.Contains(c))
            {
                sanitized.Append(c);
            }
        }

        return sanitized.ToString();
    }
}
*/
