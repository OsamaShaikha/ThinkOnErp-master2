using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using ThinkOnErp.Infrastructure.Configuration;

namespace ThinkOnErp.Infrastructure.Services.KeyManagement;

/// <summary>
/// Factory for creating key provider instances based on configuration.
/// </summary>
public class KeyProviderFactory
{
    private readonly IConfiguration _configuration;
    private readonly ILoggerFactory _loggerFactory;

    public KeyProviderFactory(IConfiguration configuration, ILoggerFactory loggerFactory)
    {
        _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
        _loggerFactory = loggerFactory ?? throw new ArgumentNullException(nameof(loggerFactory));
    }

    /// <summary>
    /// Creates a key provider based on the specified provider type.
    /// </summary>
    public IKeyProvider CreateProvider(string providerType, KeyManagementOptions options)
    {
        return providerType switch
        {
            "Configuration" => CreateConfigurationProvider(options),
            "LocalStorage" => CreateLocalStorageProvider(options),
            "AzureKeyVault" => CreateAzureKeyVaultProvider(options),
            "AwsSecretsManager" => CreateAwsSecretsManagerProvider(options),
            _ => throw new ArgumentException($"Unknown provider type: {providerType}", nameof(providerType))
        };
    }

    private IKeyProvider CreateConfigurationProvider(KeyManagementOptions options)
    {
        var logger = _loggerFactory.CreateLogger<ConfigurationKeyProvider>();
        return new ConfigurationKeyProvider(_configuration, options.Configuration, logger);
    }

    private IKeyProvider CreateLocalStorageProvider(KeyManagementOptions options)
    {
        var logger = _loggerFactory.CreateLogger<LocalStorageKeyProvider>();
        return new LocalStorageKeyProvider(options.LocalStorage, logger);
    }

    private IKeyProvider CreateAzureKeyVaultProvider(KeyManagementOptions options)
    {
        var logger = _loggerFactory.CreateLogger<AzureKeyVaultKeyProvider>();
        return new AzureKeyVaultKeyProvider(options.AzureKeyVault, logger);
    }

    private IKeyProvider CreateAwsSecretsManagerProvider(KeyManagementOptions options)
    {
        var logger = _loggerFactory.CreateLogger<AwsSecretsManagerKeyProvider>();
        return new AwsSecretsManagerKeyProvider(options.AwsSecretsManager, logger);
    }
}
