using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using ThinkOnErp.Infrastructure.Configuration;

namespace ThinkOnErp.Infrastructure.Services.KeyManagement;

/// <summary>
/// Key provider that reads keys from configuration (appsettings.json or environment variables).
/// This is the simplest provider, suitable for development and testing.
/// </summary>
public class ConfigurationKeyProvider : IKeyProvider
{
    private readonly IConfiguration _configuration;
    private readonly ConfigurationProviderSettings _settings;
    private readonly ILogger<ConfigurationKeyProvider> _logger;

    public ConfigurationKeyProvider(
        IConfiguration configuration,
        ConfigurationProviderSettings settings,
        ILogger<ConfigurationKeyProvider> logger)
    {
        _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
        _settings = settings ?? throw new ArgumentNullException(nameof(settings));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public string ProviderName => "Configuration";

    public async Task<string> GetEncryptionKeyAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("Retrieving encryption key from configuration");

        // Try environment variable first if enabled
        if (_settings.UseEnvironmentVariables)
        {
            var envKey = Environment.GetEnvironmentVariable(_settings.EncryptionKeyEnvironmentVariable);
            if (!string.IsNullOrWhiteSpace(envKey))
            {
                _logger.LogInformation("Encryption key retrieved from environment variable: {Variable}",
                    _settings.EncryptionKeyEnvironmentVariable);
                return await Task.FromResult(envKey);
            }
        }

        // Fall back to configuration
        var configKey = _configuration[_settings.EncryptionKeyPath];
        if (string.IsNullOrWhiteSpace(configKey))
        {
            throw new InvalidOperationException(
                $"Encryption key not found in configuration at path: {_settings.EncryptionKeyPath}");
        }

        // Check if it's a placeholder value
        if (configKey.Contains("REPLACE_WITH") || configKey.Contains("GENERATED"))
        {
            throw new InvalidOperationException(
                $"Encryption key at {_settings.EncryptionKeyPath} is a placeholder. " +
                "Please generate a secure key using: Convert.ToBase64String(RandomNumberGenerator.GetBytes(32))");
        }

        _logger.LogInformation("Encryption key retrieved from configuration path: {Path}",
            _settings.EncryptionKeyPath);
        return await Task.FromResult(configKey);
    }

    public async Task<string> GetSigningKeyAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("Retrieving signing key from configuration");

        // Try environment variable first if enabled
        if (_settings.UseEnvironmentVariables)
        {
            var envKey = Environment.GetEnvironmentVariable(_settings.SigningKeyEnvironmentVariable);
            if (!string.IsNullOrWhiteSpace(envKey))
            {
                _logger.LogInformation("Signing key retrieved from environment variable: {Variable}",
                    _settings.SigningKeyEnvironmentVariable);
                return await Task.FromResult(envKey);
            }
        }

        // Fall back to configuration
        var configKey = _configuration[_settings.SigningKeyPath];
        if (string.IsNullOrWhiteSpace(configKey))
        {
            throw new InvalidOperationException(
                $"Signing key not found in configuration at path: {_settings.SigningKeyPath}");
        }

        // Check if it's a placeholder value
        if (configKey.Contains("REPLACE_WITH") || configKey.Contains("GENERATED"))
        {
            throw new InvalidOperationException(
                $"Signing key at {_settings.SigningKeyPath} is a placeholder. " +
                "Please generate a secure key using: Convert.ToBase64String(RandomNumberGenerator.GetBytes(32))");
        }

        _logger.LogInformation("Signing key retrieved from configuration path: {Path}",
            _settings.SigningKeyPath);
        return await Task.FromResult(configKey);
    }

    public Task<string> GetEncryptionKeyVersionAsync(string version, CancellationToken cancellationToken = default)
    {
        // Configuration provider doesn't support versioning
        _logger.LogWarning("Configuration provider does not support key versioning. Returning current key.");
        return GetEncryptionKeyAsync(cancellationToken);
    }

    public Task<string> GetSigningKeyVersionAsync(string version, CancellationToken cancellationToken = default)
    {
        // Configuration provider doesn't support versioning
        _logger.LogWarning("Configuration provider does not support key versioning. Returning current key.");
        return GetSigningKeyAsync(cancellationToken);
    }

    public Task<string> RotateEncryptionKeyAsync(CancellationToken cancellationToken = default)
    {
        throw new NotSupportedException(
            "Configuration provider does not support automatic key rotation. " +
            "Please manually update the configuration and restart the application.");
    }

    public Task<string> RotateSigningKeyAsync(CancellationToken cancellationToken = default)
    {
        throw new NotSupportedException(
            "Configuration provider does not support automatic key rotation. " +
            "Please manually update the configuration and restart the application.");
    }

    public Task<string> GetCurrentEncryptionKeyVersionAsync(CancellationToken cancellationToken = default)
    {
        // Configuration provider doesn't support versioning
        return Task.FromResult("current");
    }

    public Task<string> GetCurrentSigningKeyVersionAsync(CancellationToken cancellationToken = default)
    {
        // Configuration provider doesn't support versioning
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

    public Task<DateTime?> GetEncryptionKeyLastRotatedAsync(CancellationToken cancellationToken = default)
    {
        // Configuration provider doesn't track rotation dates
        return Task.FromResult<DateTime?>(null);
    }

    public Task<DateTime?> GetSigningKeyLastRotatedAsync(CancellationToken cancellationToken = default)
    {
        // Configuration provider doesn't track rotation dates
        return Task.FromResult<DateTime?>(null);
    }
}
