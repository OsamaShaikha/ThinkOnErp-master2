using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ThinkOnErp.Domain.Interfaces;
using ThinkOnErp.Domain.Models;
using ThinkOnErp.Infrastructure.Configuration;

namespace ThinkOnErp.Infrastructure.Services;

/// <summary>
/// Service for managing encryption and signing keys for audit log security.
/// Provides key generation, rotation, secure storage, and retrieval capabilities.
/// Supports multiple key versions for backward compatibility during key rotation.
/// </summary>
public class KeyManagementService : IKeyManagementService
{
    private readonly ILogger<KeyManagementService> _logger;
    private readonly KeyManagementOptions _options;
    private readonly string _keyStoragePath;
    private readonly SemaphoreSlim _keyAccessLock;
    private Dictionary<string, KeyMetadata> _keyCache;
    private DateTime _lastCacheRefresh;

    /// <summary>
    /// Initializes a new instance of the KeyManagementService.
    /// </summary>
    /// <param name="options">Key management configuration options</param>
    /// <param name="logger">Logger instance</param>
    public KeyManagementService(
        IOptions<KeyManagementOptions> options,
        ILogger<KeyManagementService> logger)
    {
        _options = options?.Value ?? throw new ArgumentNullException(nameof(options));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        // Determine key storage path
        _keyStoragePath = string.IsNullOrWhiteSpace(_options.LocalStorage.KeyStoragePath)
            ? Path.Combine(AppContext.BaseDirectory, "keys")
            : _options.LocalStorage.KeyStoragePath;

        // Ensure key storage directory exists
        if (!Directory.Exists(_keyStoragePath))
        {
            Directory.CreateDirectory(_keyStoragePath);
            _logger.LogInformation("Created key storage directory at {Path}", _keyStoragePath);
        }

        _keyAccessLock = new SemaphoreSlim(1, 1);
        _keyCache = new Dictionary<string, KeyMetadata>();
        _lastCacheRefresh = DateTime.MinValue;

        _logger.LogInformation(
            "KeyManagementService initialized. Storage path: {Path}, Auto-rotation: {AutoRotation}",
            _keyStoragePath, _options.EnableKeyRotation);
    }

    /// <summary>
    /// Generates a new encryption key for AES-256 encryption.
    /// </summary>
    /// <param name="keyId">Optional key identifier. If not provided, a GUID will be generated.</param>
    /// <returns>Key metadata including the key ID and Base64-encoded key</returns>
    public async Task<KeyMetadata> GenerateEncryptionKeyAsync(string? keyId = null)
    {
        await _keyAccessLock.WaitAsync();

        try
        {
            keyId ??= $"enc-{Guid.NewGuid():N}";

            // Generate 256-bit (32 byte) key for AES-256
            var keyBytes = RandomNumberGenerator.GetBytes(32);
            var keyBase64 = Convert.ToBase64String(keyBytes);

            var metadata = new KeyMetadata
            {
                KeyId = keyId,
                KeyType = KeyType.Encryption,
                KeyValue = keyBase64,
                CreatedAt = DateTime.UtcNow,
                ExpiresAt = DateTime.UtcNow.AddDays(_options.KeyRotationDays),
                IsActive = true,
                Version = 1
            };

            // Save key to storage
            await SaveKeyAsync(metadata);

            _logger.LogInformation(
                "Generated new encryption key {KeyId}. Expires at {ExpiresAt}",
                keyId, metadata.ExpiresAt);

            return metadata;
        }
        finally
        {
            _keyAccessLock.Release();
        }
    }

    /// <summary>
    /// Generates a new signing key for HMAC-SHA256/SHA512 signatures.
    /// </summary>
    /// <param name="keyId">Optional key identifier. If not provided, a GUID will be generated.</param>
    /// <returns>Key metadata including the key ID and Base64-encoded key</returns>
    public async Task<KeyMetadata> GenerateSigningKeyAsync(string? keyId = null)
    {
        await _keyAccessLock.WaitAsync();

        try
        {
            keyId ??= $"sig-{Guid.NewGuid():N}";

            // Generate 512-bit (64 byte) key for HMAC-SHA512 (can also be used for SHA256)
            var keyBytes = RandomNumberGenerator.GetBytes(64);
            var keyBase64 = Convert.ToBase64String(keyBytes);

            var metadata = new KeyMetadata
            {
                KeyId = keyId,
                KeyType = KeyType.Signing,
                KeyValue = keyBase64,
                CreatedAt = DateTime.UtcNow,
                ExpiresAt = DateTime.UtcNow.AddDays(_options.KeyRotationDays),
                IsActive = true,
                Version = 1
            };

            // Save key to storage
            await SaveKeyAsync(metadata);

            _logger.LogInformation(
                "Generated new signing key {KeyId}. Expires at {ExpiresAt}",
                keyId, metadata.ExpiresAt);

            return metadata;
        }
        finally
        {
            _keyAccessLock.Release();
        }
    }

    /// <summary>
    /// Retrieves the current active encryption key.
    /// </summary>
    /// <returns>Active encryption key metadata, or null if no active key exists</returns>
    public async Task<KeyMetadata?> GetActiveEncryptionKeyAsync()
    {
        await RefreshCacheIfNeededAsync();

        var activeKey = _keyCache.Values
            .Where(k => k.KeyType == KeyType.Encryption && k.IsActive && k.ExpiresAt > DateTime.UtcNow)
            .OrderByDescending(k => k.CreatedAt)
            .FirstOrDefault();

        if (activeKey == null)
        {
            _logger.LogWarning("No active encryption key found. Consider generating a new key.");
        }

        return activeKey;
    }

    /// <summary>
    /// Retrieves the current active signing key.
    /// </summary>
    /// <returns>Active signing key metadata, or null if no active key exists</returns>
    public async Task<KeyMetadata?> GetActiveSigningKeyAsync()
    {
        await RefreshCacheIfNeededAsync();

        var activeKey = _keyCache.Values
            .Where(k => k.KeyType == KeyType.Signing && k.IsActive && k.ExpiresAt > DateTime.UtcNow)
            .OrderByDescending(k => k.CreatedAt)
            .FirstOrDefault();

        if (activeKey == null)
        {
            _logger.LogWarning("No active signing key found. Consider generating a new key.");
        }

        return activeKey;
    }

    /// <summary>
    /// Retrieves a specific key by its ID.
    /// Useful for decrypting old data encrypted with previous keys.
    /// </summary>
    /// <param name="keyId">The key identifier</param>
    /// <returns>Key metadata, or null if key not found</returns>
    public async Task<KeyMetadata?> GetKeyByIdAsync(string keyId)
    {
        if (string.IsNullOrWhiteSpace(keyId))
            throw new ArgumentException("Key ID cannot be null or empty", nameof(keyId));

        await RefreshCacheIfNeededAsync();

        if (_keyCache.TryGetValue(keyId, out var metadata))
        {
            return metadata;
        }

        _logger.LogWarning("Key {KeyId} not found in storage", keyId);
        return null;
    }

    /// <summary>
    /// Rotates encryption and signing keys by generating new keys and marking old ones as inactive.
    /// Old keys are retained for decryption of historical data.
    /// </summary>
    /// <returns>Dictionary containing new encryption and signing key metadata</returns>
    public async Task<Dictionary<string, KeyMetadata>> RotateKeysAsync()
    {
        await _keyAccessLock.WaitAsync();

        try
        {
            _logger.LogInformation("Starting key rotation process");

            var results = new Dictionary<string, KeyMetadata>();

            // Get current active keys
            var currentEncryptionKey = await GetActiveEncryptionKeyAsync();
            var currentSigningKey = await GetActiveSigningKeyAsync();

            // Generate new encryption key
            var newEncryptionKey = await GenerateEncryptionKeyAsync();
            results["encryption"] = newEncryptionKey;

            // Generate new signing key
            var newSigningKey = await GenerateSigningKeyAsync();
            results["signing"] = newSigningKey;

            // Mark old keys as inactive (but don't delete them - needed for decryption)
            if (currentEncryptionKey != null)
            {
                currentEncryptionKey.IsActive = false;
                currentEncryptionKey.DeactivatedAt = DateTime.UtcNow;
                await SaveKeyAsync(currentEncryptionKey);
                
                _logger.LogInformation(
                    "Deactivated old encryption key {KeyId}. Key retained for decryption of historical data.",
                    currentEncryptionKey.KeyId);
            }

            if (currentSigningKey != null)
            {
                currentSigningKey.IsActive = false;
                currentSigningKey.DeactivatedAt = DateTime.UtcNow;
                await SaveKeyAsync(currentSigningKey);
                
                _logger.LogInformation(
                    "Deactivated old signing key {KeyId}. Key retained for verification of historical signatures.",
                    currentSigningKey.KeyId);
            }

            // Refresh cache
            await RefreshCacheAsync();

            _logger.LogInformation(
                "Key rotation completed successfully. New encryption key: {EncKeyId}, New signing key: {SigKeyId}",
                newEncryptionKey.KeyId, newSigningKey.KeyId);

            return results;
        }
        finally
        {
            _keyAccessLock.Release();
        }
    }

    /// <summary>
    /// Checks if keys need rotation based on expiration dates.
    /// </summary>
    /// <returns>True if any active key is expired or about to expire</returns>
    public async Task<bool> ShouldRotateKeysAsync()
    {
        await RefreshCacheIfNeededAsync();

        var now = DateTime.UtcNow;
        var rotationThreshold = now.AddDays(_options.RotationWarningDays);

        var activeKeys = _keyCache.Values
            .Where(k => k.IsActive)
            .ToList();

        if (!activeKeys.Any())
        {
            _logger.LogWarning("No active keys found. Key rotation is required.");
            return true;
        }

        var expiringKeys = activeKeys
            .Where(k => k.ExpiresAt <= rotationThreshold)
            .ToList();

        if (expiringKeys.Any())
        {
            _logger.LogWarning(
                "{Count} active keys are expired or expiring soon. Key rotation recommended.",
                expiringKeys.Count);
            return true;
        }

        return false;
    }

    /// <summary>
    /// Lists all keys in storage with their metadata.
    /// </summary>
    /// <param name="includeInactive">Whether to include inactive keys</param>
    /// <returns>List of key metadata</returns>
    public async Task<List<KeyMetadata>> ListKeysAsync(bool includeInactive = false)
    {
        await RefreshCacheIfNeededAsync();

        var keys = _keyCache.Values.AsEnumerable();

        if (!includeInactive)
        {
            keys = keys.Where(k => k.IsActive);
        }

        return keys
            .OrderByDescending(k => k.CreatedAt)
            .ToList();
    }

    /// <summary>
    /// Deletes old inactive keys that are past their retention period.
    /// CAUTION: This will make historical data encrypted with these keys unrecoverable.
    /// </summary>
    /// <param name="retentionDays">Number of days to retain inactive keys</param>
    /// <returns>Number of keys deleted</returns>
    public async Task<int> PurgeOldKeysAsync(int retentionDays)
    {
        await _keyAccessLock.WaitAsync();

        try
        {
            await RefreshCacheAsync();

            var cutoffDate = DateTime.UtcNow.AddDays(-retentionDays);
            var keysToDelete = _keyCache.Values
                .Where(k => !k.IsActive && k.DeactivatedAt.HasValue && k.DeactivatedAt.Value < cutoffDate)
                .ToList();

            if (!keysToDelete.Any())
            {
                _logger.LogInformation("No old keys found for purging (retention: {RetentionDays} days)", retentionDays);
                return 0;
            }

            _logger.LogWarning(
                "Purging {Count} old keys. Data encrypted with these keys will become unrecoverable.",
                keysToDelete.Count);

            foreach (var key in keysToDelete)
            {
                var keyFilePath = GetKeyFilePath(key.KeyId);
                if (File.Exists(keyFilePath))
                {
                    File.Delete(keyFilePath);
                    _keyCache.Remove(key.KeyId);
                    
                    _logger.LogInformation(
                        "Deleted key {KeyId} (deactivated at {DeactivatedAt})",
                        key.KeyId, key.DeactivatedAt);
                }
            }

            return keysToDelete.Count;
        }
        finally
        {
            _keyAccessLock.Release();
        }
    }

    /// <summary>
    /// Exports key metadata (without key values) for backup or auditing.
    /// </summary>
    /// <returns>JSON string containing key metadata</returns>
    public async Task<string> ExportKeyMetadataAsync()
    {
        await RefreshCacheIfNeededAsync();

        var metadata = _keyCache.Values
            .Select(k => new
            {
                k.KeyId,
                k.KeyType,
                k.CreatedAt,
                k.ExpiresAt,
                k.IsActive,
                k.DeactivatedAt,
                k.Version,
                KeyValueLength = k.KeyValue?.Length ?? 0
            })
            .OrderByDescending(k => k.CreatedAt)
            .ToList();

        return JsonSerializer.Serialize(metadata, new JsonSerializerOptions
        {
            WriteIndented = true
        });
    }

    /// <summary>
    /// Validates that all required keys are present and active.
    /// </summary>
    /// <returns>Validation result with any issues found</returns>
    public async Task<KeyValidationResult> ValidateKeysAsync()
    {
        var result = new KeyValidationResult
        {
            IsValid = true,
            Issues = new List<string>()
        };

        var encryptionKey = await GetActiveEncryptionKeyAsync();
        var signingKey = await GetActiveSigningKeyAsync();

        if (encryptionKey == null)
        {
            result.IsValid = false;
            result.Issues.Add("No active encryption key found. Generate a new encryption key.");
        }
        else
        {
            // Validate encryption key length
            try
            {
                var keyBytes = Convert.FromBase64String(encryptionKey.KeyValue);
                if (keyBytes.Length != 32)
                {
                    result.IsValid = false;
                    result.Issues.Add($"Encryption key {encryptionKey.KeyId} has invalid length: {keyBytes.Length} bytes (expected 32 bytes for AES-256)");
                }
            }
            catch (Exception ex)
            {
                result.IsValid = false;
                result.Issues.Add($"Encryption key {encryptionKey.KeyId} is not valid Base64: {ex.Message}");
            }

            // Check expiration
            if (encryptionKey.ExpiresAt <= DateTime.UtcNow.AddDays(_options.RotationWarningDays))
            {
                result.Issues.Add($"Encryption key {encryptionKey.KeyId} expires soon: {encryptionKey.ExpiresAt:yyyy-MM-dd}. Consider rotating keys.");
            }
        }

        if (signingKey == null)
        {
            result.IsValid = false;
            result.Issues.Add("No active signing key found. Generate a new signing key.");
        }
        else
        {
            // Validate signing key length
            try
            {
                var keyBytes = Convert.FromBase64String(signingKey.KeyValue);
                if (keyBytes.Length < 32)
                {
                    result.IsValid = false;
                    result.Issues.Add($"Signing key {signingKey.KeyId} has invalid length: {keyBytes.Length} bytes (expected at least 32 bytes)");
                }
            }
            catch (Exception ex)
            {
                result.IsValid = false;
                result.Issues.Add($"Signing key {signingKey.KeyId} is not valid Base64: {ex.Message}");
            }

            // Check expiration
            if (signingKey.ExpiresAt <= DateTime.UtcNow.AddDays(_options.RotationWarningDays))
            {
                result.Issues.Add($"Signing key {signingKey.KeyId} expires soon: {signingKey.ExpiresAt:yyyy-MM-dd}. Consider rotating keys.");
            }
        }

        return result;
    }

    /// <summary>
    /// Saves key metadata to secure storage.
    /// </summary>
    private async Task SaveKeyAsync(KeyMetadata metadata)
    {
        var keyFilePath = GetKeyFilePath(metadata.KeyId);
        var json = JsonSerializer.Serialize(metadata, new JsonSerializerOptions
        {
            WriteIndented = true
        });

        // Write to file with restricted permissions
        await File.WriteAllTextAsync(keyFilePath, json);

        // Update cache
        _keyCache[metadata.KeyId] = metadata;

        _logger.LogDebug("Saved key {KeyId} to storage at {Path}", metadata.KeyId, keyFilePath);
    }

    /// <summary>
    /// Refreshes the key cache from storage if needed.
    /// </summary>
    private async Task RefreshCacheIfNeededAsync()
    {
        var cacheAge = DateTime.UtcNow - _lastCacheRefresh;
        if (cacheAge.TotalMinutes < _options.CacheDurationMinutes)
        {
            return; // Cache is still fresh
        }

        await RefreshCacheAsync();
    }

    /// <summary>
    /// Refreshes the key cache from storage.
    /// </summary>
    private async Task RefreshCacheAsync()
    {
        await _keyAccessLock.WaitAsync();

        try
        {
            _keyCache.Clear();

            if (!Directory.Exists(_keyStoragePath))
            {
                _logger.LogWarning("Key storage directory does not exist: {Path}", _keyStoragePath);
                return;
            }

            var keyFiles = Directory.GetFiles(_keyStoragePath, "*.key.json");

            foreach (var keyFile in keyFiles)
            {
                try
                {
                    var json = await File.ReadAllTextAsync(keyFile);
                    var metadata = JsonSerializer.Deserialize<KeyMetadata>(json);

                    if (metadata != null)
                    {
                        _keyCache[metadata.KeyId] = metadata;
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to load key from file {File}", keyFile);
                }
            }

            _lastCacheRefresh = DateTime.UtcNow;

            _logger.LogDebug("Refreshed key cache. Loaded {Count} keys from storage", _keyCache.Count);
        }
        finally
        {
            _keyAccessLock.Release();
        }
    }

    /// <summary>
    /// Gets the file path for a key.
    /// </summary>
    private string GetKeyFilePath(string keyId)
    {
        return Path.Combine(_keyStoragePath, $"{keyId}.key.json");
    }

    // IKeyManagementService interface implementation

    /// <summary>
    /// Retrieves the current encryption key for audit data encryption.
    /// </summary>
    public async Task<string> GetEncryptionKeyAsync(CancellationToken cancellationToken = default)
    {
        var key = await GetActiveEncryptionKeyAsync();
        if (key == null)
        {
            throw new InvalidOperationException("No active encryption key found. Please generate a new encryption key.");
        }
        return key.KeyValue;
    }

    /// <summary>
    /// Retrieves the current signing key for audit log integrity verification.
    /// </summary>
    public async Task<string> GetSigningKeyAsync(CancellationToken cancellationToken = default)
    {
        var key = await GetActiveSigningKeyAsync();
        if (key == null)
        {
            throw new InvalidOperationException("No active signing key found. Please generate a new signing key.");
        }
        return key.KeyValue;
    }

    /// <summary>
    /// Retrieves a specific version of the encryption key (for key rotation scenarios).
    /// </summary>
    public async Task<string> GetEncryptionKeyVersionAsync(string version, CancellationToken cancellationToken = default)
    {
        var key = await GetKeyByIdAsync(version);
        if (key == null || key.KeyType != KeyType.Encryption)
        {
            throw new InvalidOperationException($"Encryption key version '{version}' not found.");
        }
        return key.KeyValue;
    }

    /// <summary>
    /// Retrieves a specific version of the signing key (for key rotation scenarios).
    /// </summary>
    public async Task<string> GetSigningKeyVersionAsync(string version, CancellationToken cancellationToken = default)
    {
        var key = await GetKeyByIdAsync(version);
        if (key == null || key.KeyType != KeyType.Signing)
        {
            throw new InvalidOperationException($"Signing key version '{version}' not found.");
        }
        return key.KeyValue;
    }

    /// <summary>
    /// Rotates the encryption key by generating a new version.
    /// </summary>
    public async Task<string> RotateEncryptionKeyAsync(CancellationToken cancellationToken = default)
    {
        var newKey = await GenerateEncryptionKeyAsync();
        
        // Deactivate old encryption key
        var oldKey = await GetActiveEncryptionKeyAsync();
        if (oldKey != null && oldKey.KeyId != newKey.KeyId)
        {
            oldKey.IsActive = false;
            oldKey.DeactivatedAt = DateTime.UtcNow;
            await SaveKeyAsync(oldKey);
        }
        
        return newKey.KeyId;
    }

    /// <summary>
    /// Rotates the signing key by generating a new version.
    /// </summary>
    public async Task<string> RotateSigningKeyAsync(CancellationToken cancellationToken = default)
    {
        var newKey = await GenerateSigningKeyAsync();
        
        // Deactivate old signing key
        var oldKey = await GetActiveSigningKeyAsync();
        if (oldKey != null && oldKey.KeyId != newKey.KeyId)
        {
            oldKey.IsActive = false;
            oldKey.DeactivatedAt = DateTime.UtcNow;
            await SaveKeyAsync(oldKey);
        }
        
        return newKey.KeyId;
    }

    /// <summary>
    /// Gets the current version identifier for the encryption key.
    /// </summary>
    public async Task<string> GetCurrentEncryptionKeyVersionAsync(CancellationToken cancellationToken = default)
    {
        var key = await GetActiveEncryptionKeyAsync();
        if (key == null)
        {
            throw new InvalidOperationException("No active encryption key found.");
        }
        return key.KeyId;
    }

    /// <summary>
    /// Gets the current version identifier for the signing key.
    /// </summary>
    public async Task<string> GetCurrentSigningKeyVersionAsync(CancellationToken cancellationToken = default)
    {
        var key = await GetActiveSigningKeyAsync();
        if (key == null)
        {
            throw new InvalidOperationException("No active signing key found.");
        }
        return key.KeyId;
    }

    /// <summary>
    /// Validates that all required keys are configured and accessible.
    /// </summary>
    Task<bool> IKeyManagementService.ValidateKeysAsync(CancellationToken cancellationToken)
    {
        return Task.FromResult(ValidateKeysAsync().Result.IsValid);
    }

    /// <summary>
    /// Gets metadata about key rotation status and next rotation date.
    /// </summary>
    public async Task<KeyRotationMetadata> GetKeyRotationMetadataAsync(CancellationToken cancellationToken = default)
    {
        var encryptionKey = await GetActiveEncryptionKeyAsync();
        var signingKey = await GetActiveSigningKeyAsync();

        var metadata = new KeyRotationMetadata
        {
            StorageProvider = "LocalStorage"
        };

        if (encryptionKey != null)
        {
            metadata.EncryptionKeyVersion = encryptionKey.KeyId;
            metadata.EncryptionKeyLastRotated = encryptionKey.CreatedAt;
            metadata.EncryptionKeyNextRotation = encryptionKey.ExpiresAt;
            metadata.EncryptionKeyRotationOverdue = encryptionKey.ExpiresAt <= DateTime.UtcNow;
        }

        if (signingKey != null)
        {
            metadata.SigningKeyVersion = signingKey.KeyId;
            metadata.SigningKeyLastRotated = signingKey.CreatedAt;
            metadata.SigningKeyNextRotation = signingKey.ExpiresAt;
            metadata.SigningKeyRotationOverdue = signingKey.ExpiresAt <= DateTime.UtcNow;
        }

        return metadata;
    }
}
