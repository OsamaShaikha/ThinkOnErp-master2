using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using ThinkOnErp.Infrastructure.Configuration;

namespace ThinkOnErp.Infrastructure.Services.KeyManagement;

/// <summary>
/// Key provider that stores keys in local encrypted files.
/// Suitable for development and testing environments.
/// Uses DPAPI (Windows) or file permissions (Linux/Mac) for protection.
/// </summary>
public class LocalStorageKeyProvider : IKeyProvider
{
    private readonly LocalStorageProviderSettings _settings;
    private readonly ILogger<LocalStorageKeyProvider> _logger;
    private readonly string _encryptionKeyPath;
    private readonly string _signingKeyPath;
    private readonly string _metadataPath;
    private readonly SemaphoreSlim _fileLock = new(1, 1);

    public LocalStorageKeyProvider(
        LocalStorageProviderSettings settings,
        ILogger<LocalStorageKeyProvider> logger)
    {
        _settings = settings ?? throw new ArgumentNullException(nameof(settings));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        // Ensure key storage directory exists
        if (!Directory.Exists(_settings.KeyStoragePath))
        {
            Directory.CreateDirectory(_settings.KeyStoragePath);
            _logger.LogInformation("Created key storage directory: {Path}", _settings.KeyStoragePath);
        }

        _encryptionKeyPath = Path.Combine(_settings.KeyStoragePath, _settings.EncryptionKeyFileName);
        _signingKeyPath = Path.Combine(_settings.KeyStoragePath, _settings.SigningKeyFileName);
        _metadataPath = Path.Combine(_settings.KeyStoragePath, "metadata.json");

        // Auto-generate keys if they don't exist
        if (_settings.AutoGenerateKeys)
        {
            EnsureKeysExist();
        }
    }

    public string ProviderName => "LocalStorage";

    public async Task<string> GetEncryptionKeyAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("Retrieving encryption key from local storage");

        await _fileLock.WaitAsync(cancellationToken);
        try
        {
            if (!File.Exists(_encryptionKeyPath))
            {
                throw new InvalidOperationException(
                    $"Encryption key file not found at: {_encryptionKeyPath}. " +
                    "Set AutoGenerateKeys=true to automatically generate keys.");
            }

            var encryptedData = await File.ReadAllBytesAsync(_encryptionKeyPath, cancellationToken);
            var key = DecryptKeyData(encryptedData);

            _logger.LogInformation("Encryption key retrieved from local storage");
            return key;
        }
        finally
        {
            _fileLock.Release();
        }
    }

    public async Task<string> GetSigningKeyAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("Retrieving signing key from local storage");

        await _fileLock.WaitAsync(cancellationToken);
        try
        {
            if (!File.Exists(_signingKeyPath))
            {
                throw new InvalidOperationException(
                    $"Signing key file not found at: {_signingKeyPath}. " +
                    "Set AutoGenerateKeys=true to automatically generate keys.");
            }

            var encryptedData = await File.ReadAllBytesAsync(_signingKeyPath, cancellationToken);
            var key = DecryptKeyData(encryptedData);

            _logger.LogInformation("Signing key retrieved from local storage");
            return key;
        }
        finally
        {
            _fileLock.Release();
        }
    }

    public Task<string> GetEncryptionKeyVersionAsync(string version, CancellationToken cancellationToken = default)
    {
        // Local storage provider doesn't support versioning yet
        _logger.LogWarning("Local storage provider does not support key versioning. Returning current key.");
        return GetEncryptionKeyAsync(cancellationToken);
    }

    public Task<string> GetSigningKeyVersionAsync(string version, CancellationToken cancellationToken = default)
    {
        // Local storage provider doesn't support versioning yet
        _logger.LogWarning("Local storage provider does not support key versioning. Returning current key.");
        return GetSigningKeyAsync(cancellationToken);
    }

    public async Task<string> RotateEncryptionKeyAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Rotating encryption key in local storage");

        await _fileLock.WaitAsync(cancellationToken);
        try
        {
            // Generate new key
            var newKey = GenerateKey();

            // Backup old key
            if (File.Exists(_encryptionKeyPath))
            {
                var backupPath = $"{_encryptionKeyPath}.{DateTime.UtcNow:yyyyMMddHHmmss}.bak";
                File.Copy(_encryptionKeyPath, backupPath);
                _logger.LogInformation("Backed up old encryption key to: {Path}", backupPath);
            }

            // Save new key
            var encryptedData = EncryptKeyData(newKey);
            await File.WriteAllBytesAsync(_encryptionKeyPath, encryptedData, cancellationToken);
            SetFilePermissions(_encryptionKeyPath);

            // Update metadata
            await UpdateMetadataAsync("encryption", cancellationToken);

            var version = DateTime.UtcNow.ToString("yyyyMMddHHmmss");
            _logger.LogInformation("Encryption key rotated successfully. New version: {Version}", version);
            return version;
        }
        finally
        {
            _fileLock.Release();
        }
    }

    public async Task<string> RotateSigningKeyAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Rotating signing key in local storage");

        await _fileLock.WaitAsync(cancellationToken);
        try
        {
            // Generate new key
            var newKey = GenerateKey();

            // Backup old key
            if (File.Exists(_signingKeyPath))
            {
                var backupPath = $"{_signingKeyPath}.{DateTime.UtcNow:yyyyMMddHHmmss}.bak";
                File.Copy(_signingKeyPath, backupPath);
                _logger.LogInformation("Backed up old signing key to: {Path}", backupPath);
            }

            // Save new key
            var encryptedData = EncryptKeyData(newKey);
            await File.WriteAllBytesAsync(_signingKeyPath, encryptedData, cancellationToken);
            SetFilePermissions(_signingKeyPath);

            // Update metadata
            await UpdateMetadataAsync("signing", cancellationToken);

            var version = DateTime.UtcNow.ToString("yyyyMMddHHmmss");
            _logger.LogInformation("Signing key rotated successfully. New version: {Version}", version);
            return version;
        }
        finally
        {
            _fileLock.Release();
        }
    }

    public Task<string> GetCurrentEncryptionKeyVersionAsync(CancellationToken cancellationToken = default)
    {
        return Task.FromResult("current");
    }

    public Task<string> GetCurrentSigningKeyVersionAsync(CancellationToken cancellationToken = default)
    {
        return Task.FromResult("current");
    }

    public async Task<bool> ValidateKeysAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var encryptionKey = await GetEncryptionKeyAsync(cancellationToken);
            var signingKey = await GetSigningKeyAsync(cancellationToken);

            // Validate Base64 format
            var encryptionBytes = Convert.FromBase64String(encryptionKey);
            var signingBytes = Convert.FromBase64String(signingKey);

            // Validate key lengths
            if (encryptionBytes.Length != 32)
            {
                _logger.LogError("Encryption key must be 32 bytes (256 bits). Current: {Length} bytes",
                    encryptionBytes.Length);
                return false;
            }

            if (signingBytes.Length < 32)
            {
                _logger.LogError("Signing key must be at least 32 bytes (256 bits). Current: {Length} bytes",
                    signingBytes.Length);
                return false;
            }

            _logger.LogInformation("Key validation successful");
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Key validation failed");
            return false;
        }
    }

    public async Task<DateTime?> GetEncryptionKeyLastRotatedAsync(CancellationToken cancellationToken = default)
    {
        var metadata = await LoadMetadataAsync(cancellationToken);
        return metadata?.EncryptionKeyLastRotated;
    }

    public async Task<DateTime?> GetSigningKeyLastRotatedAsync(CancellationToken cancellationToken = default)
    {
        var metadata = await LoadMetadataAsync(cancellationToken);
        return metadata?.SigningKeyLastRotated;
    }

    private void EnsureKeysExist()
    {
        if (!File.Exists(_encryptionKeyPath))
        {
            _logger.LogInformation("Encryption key not found. Generating new key...");
            var key = GenerateKey();
            var encryptedData = EncryptKeyData(key);
            File.WriteAllBytes(_encryptionKeyPath, encryptedData);
            SetFilePermissions(_encryptionKeyPath);
            _logger.LogInformation("Generated new encryption key at: {Path}", _encryptionKeyPath);
        }

        if (!File.Exists(_signingKeyPath))
        {
            _logger.LogInformation("Signing key not found. Generating new key...");
            var key = GenerateKey();
            var encryptedData = EncryptKeyData(key);
            File.WriteAllBytes(_signingKeyPath, encryptedData);
            SetFilePermissions(_signingKeyPath);
            _logger.LogInformation("Generated new signing key at: {Path}", _signingKeyPath);
        }
    }

    private string GenerateKey()
    {
        var keyBytes = RandomNumberGenerator.GetBytes(32); // 256 bits
        return Convert.ToBase64String(keyBytes);
    }

    private byte[] EncryptKeyData(string key)
    {
        if (!_settings.UseDataProtection)
        {
            // Store as plain text (not recommended for production)
            _logger.LogWarning("Data protection is disabled. Keys will be stored in plain text.");
            return Encoding.UTF8.GetBytes(key);
        }

        try
        {
            // Use DPAPI on Windows, or just encode on other platforms
            if (OperatingSystem.IsWindows())
            {
                var plainBytes = Encoding.UTF8.GetBytes(key);
                return System.Security.Cryptography.ProtectedData.Protect(
                    plainBytes,
                    null,
                    System.Security.Cryptography.DataProtectionScope.LocalMachine);
            }
            else
            {
                // On non-Windows platforms, rely on file permissions
                _logger.LogWarning("DPAPI not available on this platform. Relying on file permissions for security.");
                return Encoding.UTF8.GetBytes(key);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to encrypt key data. Storing as plain text.");
            return Encoding.UTF8.GetBytes(key);
        }
    }

    private string DecryptKeyData(byte[] encryptedData)
    {
        if (!_settings.UseDataProtection)
        {
            return Encoding.UTF8.GetString(encryptedData);
        }

        try
        {
            if (OperatingSystem.IsWindows())
            {
                var plainBytes = System.Security.Cryptography.ProtectedData.Unprotect(
                    encryptedData,
                    null,
                    System.Security.Cryptography.DataProtectionScope.LocalMachine);
                return Encoding.UTF8.GetString(plainBytes);
            }
            else
            {
                return Encoding.UTF8.GetString(encryptedData);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to decrypt key data. Attempting to read as plain text.");
            return Encoding.UTF8.GetString(encryptedData);
        }
    }

    private void SetFilePermissions(string filePath)
    {
        try
        {
            if (!OperatingSystem.IsWindows())
            {
                // Set file permissions on Unix-like systems (chmod 600)
                var fileInfo = new FileInfo(filePath);
                fileInfo.UnixFileMode = UnixFileMode.UserRead | UnixFileMode.UserWrite;
                _logger.LogDebug("Set file permissions to {Permissions} for: {Path}",
                    _settings.FilePermissions, filePath);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to set file permissions for: {Path}", filePath);
        }
    }

    private async Task UpdateMetadataAsync(string keyType, CancellationToken cancellationToken)
    {
        var metadata = await LoadMetadataAsync(cancellationToken) ?? new KeyMetadata();

        if (keyType == "encryption")
        {
            metadata.EncryptionKeyLastRotated = DateTime.UtcNow;
        }
        else if (keyType == "signing")
        {
            metadata.SigningKeyLastRotated = DateTime.UtcNow;
        }

        var json = JsonSerializer.Serialize(metadata, new JsonSerializerOptions { WriteIndented = true });
        await File.WriteAllTextAsync(_metadataPath, json, cancellationToken);
    }

    private async Task<KeyMetadata?> LoadMetadataAsync(CancellationToken cancellationToken)
    {
        if (!File.Exists(_metadataPath))
            return null;

        try
        {
            var json = await File.ReadAllTextAsync(_metadataPath, cancellationToken);
            return JsonSerializer.Deserialize<KeyMetadata>(json);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to load key metadata");
            return null;
        }
    }

    private class KeyMetadata
    {
        public DateTime? EncryptionKeyLastRotated { get; set; }
        public DateTime? SigningKeyLastRotated { get; set; }
    }
}
