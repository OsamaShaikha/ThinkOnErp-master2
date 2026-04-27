using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ThinkOnErp.Domain.Interfaces;
using ThinkOnErp.Infrastructure.Configuration;

namespace ThinkOnErp.Infrastructure.Services.KeyManagement;

/// <summary>
/// Main key management service that orchestrates key providers and implements caching.
/// Supports fallback providers and automatic key rotation monitoring.
/// </summary>
public class KeyManagementService : IKeyManagementService
{
    private readonly IKeyProvider _primaryProvider;
    private readonly IKeyProvider? _fallbackProvider;
    private readonly KeyManagementOptions _options;
    private readonly IMemoryCache? _cache;
    private readonly ILogger<KeyManagementService> _logger;
    private readonly IAlertManager? _alertManager;

    private const string EncryptionKeyCacheKey = "KeyManagement:EncryptionKey";
    private const string SigningKeyCacheKey = "KeyManagement:SigningKey";

    public KeyManagementService(
        IKeyProvider primaryProvider,
        IKeyProvider? fallbackProvider,
        IOptions<KeyManagementOptions> options,
        ILogger<KeyManagementService> logger,
        IMemoryCache? cache = null,
        IAlertManager? alertManager = null)
    {
        _primaryProvider = primaryProvider ?? throw new ArgumentNullException(nameof(primaryProvider));
        _fallbackProvider = fallbackProvider;
        _options = options?.Value ?? throw new ArgumentNullException(nameof(options));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _cache = cache;
        _alertManager = alertManager;

        _logger.LogInformation(
            "KeyManagementService initialized with primary provider: {PrimaryProvider}, fallback: {FallbackProvider}",
            _primaryProvider.ProviderName,
            _fallbackProvider?.ProviderName ?? "None");
    }

    public async Task<string> GetEncryptionKeyAsync(CancellationToken cancellationToken = default)
    {
        // Check cache first if enabled
        if (_options.EnableCaching && _cache != null)
        {
            if (_cache.TryGetValue(EncryptionKeyCacheKey, out string? cachedKey) && cachedKey != null)
            {
                _logger.LogDebug("Encryption key retrieved from cache");
                return cachedKey;
            }
        }

        // Try primary provider
        try
        {
            var key = await _primaryProvider.GetEncryptionKeyAsync(cancellationToken);
            CacheKey(EncryptionKeyCacheKey, key);
            return key;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to retrieve encryption key from primary provider: {Provider}",
                _primaryProvider.ProviderName);

            // Try fallback provider
            if (_fallbackProvider != null)
            {
                _logger.LogWarning("Attempting to retrieve encryption key from fallback provider: {Provider}",
                    _fallbackProvider.ProviderName);

                try
                {
                    var key = await _fallbackProvider.GetEncryptionKeyAsync(cancellationToken);
                    CacheKey(EncryptionKeyCacheKey, key);
                    return key;
                }
                catch (Exception fallbackEx)
                {
                    _logger.LogError(fallbackEx, "Failed to retrieve encryption key from fallback provider: {Provider}",
                        _fallbackProvider.ProviderName);
                }
            }

            throw new InvalidOperationException(
                "Failed to retrieve encryption key from all configured providers", ex);
        }
    }

    public async Task<string> GetSigningKeyAsync(CancellationToken cancellationToken = default)
    {
        // Check cache first if enabled
        if (_options.EnableCaching && _cache != null)
        {
            if (_cache.TryGetValue(SigningKeyCacheKey, out string? cachedKey) && cachedKey != null)
            {
                _logger.LogDebug("Signing key retrieved from cache");
                return cachedKey;
            }
        }

        // Try primary provider
        try
        {
            var key = await _primaryProvider.GetSigningKeyAsync(cancellationToken);
            CacheKey(SigningKeyCacheKey, key);
            return key;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to retrieve signing key from primary provider: {Provider}",
                _primaryProvider.ProviderName);

            // Try fallback provider
            if (_fallbackProvider != null)
            {
                _logger.LogWarning("Attempting to retrieve signing key from fallback provider: {Provider}",
                    _fallbackProvider.ProviderName);

                try
                {
                    var key = await _fallbackProvider.GetSigningKeyAsync(cancellationToken);
                    CacheKey(SigningKeyCacheKey, key);
                    return key;
                }
                catch (Exception fallbackEx)
                {
                    _logger.LogError(fallbackEx, "Failed to retrieve signing key from fallback provider: {Provider}",
                        _fallbackProvider.ProviderName);
                }
            }

            throw new InvalidOperationException(
                "Failed to retrieve signing key from all configured providers", ex);
        }
    }

    public Task<string> GetEncryptionKeyVersionAsync(string version, CancellationToken cancellationToken = default)
    {
        return _primaryProvider.GetEncryptionKeyVersionAsync(version, cancellationToken);
    }

    public Task<string> GetSigningKeyVersionAsync(string version, CancellationToken cancellationToken = default)
    {
        return _primaryProvider.GetSigningKeyVersionAsync(version, cancellationToken);
    }

    public async Task<string> RotateEncryptionKeyAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Initiating encryption key rotation");

        try
        {
            var newVersion = await _primaryProvider.RotateEncryptionKeyAsync(cancellationToken);

            // Clear cache to force reload of new key
            if (_cache != null)
            {
                _cache.Remove(EncryptionKeyCacheKey);
            }

            _logger.LogInformation("Encryption key rotated successfully. New version: {Version}", newVersion);
            return newVersion;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to rotate encryption key");
            throw;
        }
    }

    public async Task<string> RotateSigningKeyAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Initiating signing key rotation");

        try
        {
            var newVersion = await _primaryProvider.RotateSigningKeyAsync(cancellationToken);

            // Clear cache to force reload of new key
            if (_cache != null)
            {
                _cache.Remove(SigningKeyCacheKey);
            }

            _logger.LogInformation("Signing key rotated successfully. New version: {Version}", newVersion);
            return newVersion;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to rotate signing key");
            throw;
        }
    }

    public Task<string> GetCurrentEncryptionKeyVersionAsync(CancellationToken cancellationToken = default)
    {
        return _primaryProvider.GetCurrentEncryptionKeyVersionAsync(cancellationToken);
    }

    public Task<string> GetCurrentSigningKeyVersionAsync(CancellationToken cancellationToken = default)
    {
        return _primaryProvider.GetCurrentSigningKeyVersionAsync(cancellationToken);
    }

    public async Task<bool> ValidateKeysAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Validating keys from primary provider: {Provider}", _primaryProvider.ProviderName);

        var isValid = await _primaryProvider.ValidateKeysAsync(cancellationToken);

        if (!isValid)
        {
            _logger.LogError("Key validation failed for primary provider: {Provider}", _primaryProvider.ProviderName);

            // Try fallback provider
            if (_fallbackProvider != null)
            {
                _logger.LogWarning("Validating keys from fallback provider: {Provider}",
                    _fallbackProvider.ProviderName);
                isValid = await _fallbackProvider.ValidateKeysAsync(cancellationToken);
            }
        }

        return isValid;
    }

    public async Task<KeyRotationMetadata> GetKeyRotationMetadataAsync(CancellationToken cancellationToken = default)
    {
        var encryptionKeyVersion = await _primaryProvider.GetCurrentEncryptionKeyVersionAsync(cancellationToken);
        var signingKeyVersion = await _primaryProvider.GetCurrentSigningKeyVersionAsync(cancellationToken);
        var encryptionKeyLastRotated = await _primaryProvider.GetEncryptionKeyLastRotatedAsync(cancellationToken);
        var signingKeyLastRotated = await _primaryProvider.GetSigningKeyLastRotatedAsync(cancellationToken);

        var now = DateTime.UtcNow;
        var rotationPeriod = TimeSpan.FromDays(_options.KeyRotationDays);

        var metadata = new KeyRotationMetadata
        {
            EncryptionKeyVersion = encryptionKeyVersion,
            SigningKeyVersion = signingKeyVersion,
            EncryptionKeyLastRotated = encryptionKeyLastRotated,
            SigningKeyLastRotated = signingKeyLastRotated,
            StorageProvider = _primaryProvider.ProviderName
        };

        // Calculate next rotation dates
        if (encryptionKeyLastRotated.HasValue)
        {
            metadata.EncryptionKeyNextRotation = encryptionKeyLastRotated.Value.Add(rotationPeriod);
            metadata.EncryptionKeyRotationOverdue = now > metadata.EncryptionKeyNextRotation;
        }

        if (signingKeyLastRotated.HasValue)
        {
            metadata.SigningKeyNextRotation = signingKeyLastRotated.Value.Add(rotationPeriod);
            metadata.SigningKeyRotationOverdue = now > metadata.SigningKeyNextRotation;
        }

        // Check if rotation warnings should be sent
        if (_options.AlertOnRotationDue && _alertManager != null)
        {
            await CheckAndSendRotationAlertsAsync(metadata, cancellationToken);
        }

        return metadata;
    }

    private void CacheKey(string cacheKey, string key)
    {
        if (_options.EnableCaching && _cache != null)
        {
            var cacheOptions = new MemoryCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(_options.CacheDurationMinutes),
                Priority = CacheItemPriority.High
            };

            _cache.Set(cacheKey, key, cacheOptions);
            _logger.LogDebug("Cached key with expiration: {Minutes} minutes", _options.CacheDurationMinutes);
        }
    }

    private async Task CheckAndSendRotationAlertsAsync(
        KeyRotationMetadata metadata,
        CancellationToken cancellationToken)
    {
        if (_alertManager == null)
            return;

        var now = DateTime.UtcNow;
        var warningThreshold = TimeSpan.FromDays(_options.RotationWarningDays);

        // Check encryption key rotation
        if (metadata.EncryptionKeyNextRotation.HasValue)
        {
            var timeUntilRotation = metadata.EncryptionKeyNextRotation.Value - now;

            if (metadata.EncryptionKeyRotationOverdue)
            {
                await SendRotationAlertAsync(
                    "Encryption",
                    "Overdue",
                    $"Encryption key rotation is overdue by {Math.Abs(timeUntilRotation.Days)} days",
                    "Critical",
                    cancellationToken);
            }
            else if (timeUntilRotation <= warningThreshold)
            {
                await SendRotationAlertAsync(
                    "Encryption",
                    "Warning",
                    $"Encryption key rotation due in {timeUntilRotation.Days} days",
                    "High",
                    cancellationToken);
            }
        }

        // Check signing key rotation
        if (metadata.SigningKeyNextRotation.HasValue)
        {
            var timeUntilRotation = metadata.SigningKeyNextRotation.Value - now;

            if (metadata.SigningKeyRotationOverdue)
            {
                await SendRotationAlertAsync(
                    "Signing",
                    "Overdue",
                    $"Signing key rotation is overdue by {Math.Abs(timeUntilRotation.Days)} days",
                    "Critical",
                    cancellationToken);
            }
            else if (timeUntilRotation <= warningThreshold)
            {
                await SendRotationAlertAsync(
                    "Signing",
                    "Warning",
                    $"Signing key rotation due in {timeUntilRotation.Days} days",
                    "High",
                    cancellationToken);
            }
        }
    }

    private async Task SendRotationAlertAsync(
        string keyType,
        string alertType,
        string message,
        string severity,
        CancellationToken cancellationToken)
    {
        if (_alertManager == null)
            return;

        try
        {
            var alert = new Domain.Models.Alert
            {
                AlertType = $"KeyRotation{alertType}",
                Severity = severity,
                Title = $"{keyType} Key Rotation {alertType}",
                Description = message,
                Metadata = System.Text.Json.JsonSerializer.Serialize(new
                {
                    KeyType = keyType,
                    AlertType = alertType,
                    Provider = _primaryProvider.ProviderName,
                    Timestamp = DateTime.UtcNow
                })
            };

            await _alertManager.TriggerAlertAsync(alert);
            _logger.LogInformation("Sent key rotation alert: {KeyType} - {AlertType}", keyType, alertType);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send key rotation alert for {KeyType}", keyType);
        }
    }
}
