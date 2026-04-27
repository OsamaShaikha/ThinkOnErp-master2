using System.ComponentModel.DataAnnotations;

namespace ThinkOnErp.Infrastructure.Configuration;

/// <summary>
/// Configuration options for secure key management.
/// Supports multiple key storage providers with fallback options.
/// </summary>
public class KeyManagementOptions
{
    /// <summary>
    /// Configuration section name in appsettings.json
    /// </summary>
    public const string SectionName = "KeyManagement";

    /// <summary>
    /// Key storage provider to use.
    /// Supported values: Configuration, LocalStorage, AzureKeyVault, AwsSecretsManager
    /// </summary>
    [Required(ErrorMessage = "Provider is required")]
    [RegularExpression("^(Configuration|LocalStorage|AzureKeyVault|AwsSecretsManager)$",
        ErrorMessage = "Provider must be Configuration, LocalStorage, AzureKeyVault, or AwsSecretsManager")]
    public string Provider { get; set; } = "Configuration";

    /// <summary>
    /// Whether to enable automatic key rotation. Default: false
    /// </summary>
    public bool EnableKeyRotation { get; set; } = false;

    /// <summary>
    /// Key rotation period in days. Default: 90 days
    /// Must be between 30 and 365 days.
    /// </summary>
    [Range(30, 365, ErrorMessage = "KeyRotationDays must be between 30 and 365 days")]
    public int KeyRotationDays { get; set; } = 90;

    /// <summary>
    /// Whether to send alerts when key rotation is due. Default: true
    /// </summary>
    public bool AlertOnRotationDue { get; set; } = true;

    /// <summary>
    /// Number of days before rotation to send warning alerts. Default: 7 days
    /// </summary>
    [Range(1, 30, ErrorMessage = "RotationWarningDays must be between 1 and 30 days")]
    public int RotationWarningDays { get; set; } = 7;

    /// <summary>
    /// Configuration provider settings (when Provider = "Configuration")
    /// </summary>
    public ConfigurationProviderSettings Configuration { get; set; } = new();

    /// <summary>
    /// Local storage provider settings (when Provider = "LocalStorage")
    /// </summary>
    public LocalStorageProviderSettings LocalStorage { get; set; } = new();

    /// <summary>
    /// Azure Key Vault provider settings (when Provider = "AzureKeyVault")
    /// </summary>
    public AzureKeyVaultProviderSettings AzureKeyVault { get; set; } = new();

    /// <summary>
    /// AWS Secrets Manager provider settings (when Provider = "AwsSecretsManager")
    /// </summary>
    public AwsSecretsManagerProviderSettings AwsSecretsManager { get; set; } = new();

    /// <summary>
    /// Fallback provider to use if primary provider fails.
    /// Supported values: None, Configuration, LocalStorage
    /// </summary>
    [RegularExpression("^(None|Configuration|LocalStorage)$",
        ErrorMessage = "FallbackProvider must be None, Configuration, or LocalStorage")]
    public string FallbackProvider { get; set; } = "Configuration";

    /// <summary>
    /// Whether to cache keys in memory for performance. Default: true
    /// </summary>
    public bool EnableCaching { get; set; } = true;

    /// <summary>
    /// Cache duration in minutes. Default: 60 minutes
    /// Must be between 1 and 1440 minutes (24 hours).
    /// </summary>
    [Range(1, 1440, ErrorMessage = "CacheDurationMinutes must be between 1 and 1440 minutes")]
    public int CacheDurationMinutes { get; set; } = 60;
}

/// <summary>
/// Settings for configuration-based key storage (appsettings.json or environment variables)
/// </summary>
public class ConfigurationProviderSettings
{
    /// <summary>
    /// Configuration key for encryption key. Default: "AuditEncryption:Key"
    /// </summary>
    public string EncryptionKeyPath { get; set; } = "AuditEncryption:Key";

    /// <summary>
    /// Configuration key for signing key. Default: "AuditIntegrity:SigningKey"
    /// </summary>
    public string SigningKeyPath { get; set; } = "AuditIntegrity:SigningKey";

    /// <summary>
    /// Whether to read from environment variables. Default: true
    /// </summary>
    public bool UseEnvironmentVariables { get; set; } = true;

    /// <summary>
    /// Environment variable name for encryption key. Default: "AUDIT_ENCRYPTION_KEY"
    /// </summary>
    public string EncryptionKeyEnvironmentVariable { get; set; } = "AUDIT_ENCRYPTION_KEY";

    /// <summary>
    /// Environment variable name for signing key. Default: "AUDIT_SIGNING_KEY"
    /// </summary>
    public string SigningKeyEnvironmentVariable { get; set; } = "AUDIT_SIGNING_KEY";
}

/// <summary>
/// Settings for local file-based key storage (for development)
/// </summary>
public class LocalStorageProviderSettings
{
    /// <summary>
    /// Directory path for storing keys. Default: "Keys"
    /// </summary>
    public string KeyStoragePath { get; set; } = "Keys";

    /// <summary>
    /// File name for encryption key. Default: "encryption.key"
    /// </summary>
    public string EncryptionKeyFileName { get; set; } = "encryption.key";

    /// <summary>
    /// File name for signing key. Default: "signing.key"
    /// </summary>
    public string SigningKeyFileName { get; set; } = "signing.key";

    /// <summary>
    /// Whether to encrypt key files using DPAPI (Windows only). Default: true
    /// </summary>
    public bool UseDataProtection { get; set; } = true;

    /// <summary>
    /// Whether to automatically generate keys if they don't exist. Default: true
    /// </summary>
    public bool AutoGenerateKeys { get; set; } = true;

    /// <summary>
    /// File permissions for key files (Unix-style, e.g., "600"). Default: "600"
    /// </summary>
    public string FilePermissions { get; set; } = "600";
}

/// <summary>
/// Settings for Azure Key Vault key storage (for production)
/// </summary>
public class AzureKeyVaultProviderSettings
{
    /// <summary>
    /// Azure Key Vault URL (e.g., "https://myvault.vault.azure.net/")
    /// </summary>
    public string VaultUrl { get; set; } = string.Empty;

    /// <summary>
    /// Secret name for encryption key in Key Vault. Default: "audit-encryption-key"
    /// </summary>
    public string EncryptionKeySecretName { get; set; } = "audit-encryption-key";

    /// <summary>
    /// Secret name for signing key in Key Vault. Default: "audit-signing-key"
    /// </summary>
    public string SigningKeySecretName { get; set; } = "audit-signing-key";

    /// <summary>
    /// Authentication method: ManagedIdentity, ServicePrincipal, or ClientSecret
    /// </summary>
    [RegularExpression("^(ManagedIdentity|ServicePrincipal|ClientSecret)$",
        ErrorMessage = "AuthenticationMethod must be ManagedIdentity, ServicePrincipal, or ClientSecret")]
    public string AuthenticationMethod { get; set; } = "ManagedIdentity";

    /// <summary>
    /// Azure AD Tenant ID (required for ServicePrincipal or ClientSecret authentication)
    /// </summary>
    public string? TenantId { get; set; }

    /// <summary>
    /// Azure AD Client ID (required for ServicePrincipal or ClientSecret authentication)
    /// </summary>
    public string? ClientId { get; set; }

    /// <summary>
    /// Azure AD Client Secret (required for ClientSecret authentication)
    /// </summary>
    public string? ClientSecret { get; set; }

    /// <summary>
    /// Certificate thumbprint (required for ServicePrincipal authentication)
    /// </summary>
    public string? CertificateThumbprint { get; set; }

    /// <summary>
    /// Connection timeout in seconds. Default: 30 seconds
    /// </summary>
    [Range(5, 120, ErrorMessage = "TimeoutSeconds must be between 5 and 120 seconds")]
    public int TimeoutSeconds { get; set; } = 30;

    /// <summary>
    /// Number of retry attempts for transient failures. Default: 3
    /// </summary>
    [Range(0, 10, ErrorMessage = "RetryAttempts must be between 0 and 10")]
    public int RetryAttempts { get; set; } = 3;
}

/// <summary>
/// Settings for AWS Secrets Manager key storage (for production)
/// </summary>
public class AwsSecretsManagerProviderSettings
{
    /// <summary>
    /// AWS Region (e.g., "us-east-1")
    /// </summary>
    public string Region { get; set; } = "us-east-1";

    /// <summary>
    /// Secret name for encryption key in Secrets Manager. Default: "audit/encryption-key"
    /// </summary>
    public string EncryptionKeySecretName { get; set; } = "audit/encryption-key";

    /// <summary>
    /// Secret name for signing key in Secrets Manager. Default: "audit/signing-key"
    /// </summary>
    public string SigningKeySecretName { get; set; } = "audit/signing-key";

    /// <summary>
    /// Authentication method: IAMRole, AccessKey, or Profile
    /// </summary>
    [RegularExpression("^(IAMRole|AccessKey|Profile)$",
        ErrorMessage = "AuthenticationMethod must be IAMRole, AccessKey, or Profile")]
    public string AuthenticationMethod { get; set; } = "IAMRole";

    /// <summary>
    /// AWS Access Key ID (required for AccessKey authentication)
    /// </summary>
    public string? AccessKeyId { get; set; }

    /// <summary>
    /// AWS Secret Access Key (required for AccessKey authentication)
    /// </summary>
    public string? SecretAccessKey { get; set; }

    /// <summary>
    /// AWS Profile name (required for Profile authentication)
    /// </summary>
    public string? ProfileName { get; set; }

    /// <summary>
    /// Connection timeout in seconds. Default: 30 seconds
    /// </summary>
    [Range(5, 120, ErrorMessage = "TimeoutSeconds must be between 5 and 120 seconds")]
    public int TimeoutSeconds { get; set; } = 30;

    /// <summary>
    /// Number of retry attempts for transient failures. Default: 3
    /// </summary>
    [Range(0, 10, ErrorMessage = "RetryAttempts must be between 0 and 10")]
    public int RetryAttempts { get; set; } = 3;
}
