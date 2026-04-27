using Microsoft.Extensions.Logging;
using ThinkOnErp.Domain.Interfaces;

namespace ThinkOnErp.Infrastructure.Services;

/// <summary>
/// Command-line interface for key management operations.
/// Provides methods for generating, rotating, and managing encryption and signing keys.
/// </summary>
public class KeyManagementCli
{
    private readonly KeyManagementService _keyManagementService;
    private readonly ILogger<KeyManagementCli> _logger;

    /// <summary>
    /// Initializes a new instance of the KeyManagementCli.
    /// </summary>
    /// <param name="keyManagementService">Key management service</param>
    /// <param name="logger">Logger instance</param>
    public KeyManagementCli(
        KeyManagementService keyManagementService,
        ILogger<KeyManagementCli> logger)
    {
        _keyManagementService = keyManagementService ?? throw new ArgumentNullException(nameof(keyManagementService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Generates initial encryption and signing keys for a new installation.
    /// </summary>
    public async Task<int> GenerateInitialKeysAsync()
    {
        try
        {
            Console.WriteLine("=== Generating Initial Keys ===");
            Console.WriteLine();

            // Check if keys already exist
            var existingEncKey = await _keyManagementService.GetActiveEncryptionKeyAsync();
            var existingSigKey = await _keyManagementService.GetActiveSigningKeyAsync();

            if (existingEncKey != null || existingSigKey != null)
            {
                Console.WriteLine("WARNING: Active keys already exist!");
                Console.WriteLine($"  Encryption Key: {existingEncKey?.KeyId ?? "None"}");
                Console.WriteLine($"  Signing Key: {existingSigKey?.KeyId ?? "None"}");
                Console.WriteLine();
                Console.Write("Do you want to generate new keys anyway? This will deactivate existing keys. (yes/no): ");
                
                var response = Console.ReadLine()?.Trim().ToLower();
                if (response != "yes" && response != "y")
                {
                    Console.WriteLine("Operation cancelled.");
                    return 0;
                }
            }

            // Generate encryption key
            Console.WriteLine("Generating encryption key...");
            var encKey = await _keyManagementService.GenerateEncryptionKeyAsync();
            Console.WriteLine($"✓ Encryption key generated: {encKey.KeyId}");
            Console.WriteLine($"  Key: {encKey.KeyValue}");
            Console.WriteLine($"  Expires: {encKey.ExpiresAt:yyyy-MM-dd}");
            Console.WriteLine();

            // Generate signing key
            Console.WriteLine("Generating signing key...");
            var sigKey = await _keyManagementService.GenerateSigningKeyAsync();
            Console.WriteLine($"✓ Signing key generated: {sigKey.KeyId}");
            Console.WriteLine($"  Key: {sigKey.KeyValue}");
            Console.WriteLine($"  Expires: {sigKey.ExpiresAt:yyyy-MM-dd}");
            Console.WriteLine();

            Console.WriteLine("=== Configuration ===");
            Console.WriteLine("Add the following to your appsettings.json:");
            Console.WriteLine();
            Console.WriteLine("{");
            Console.WriteLine("  \"AuditEncryption\": {");
            Console.WriteLine("    \"Enabled\": true,");
            Console.WriteLine($"    \"Key\": \"{encKey.KeyValue}\"");
            Console.WriteLine("  },");
            Console.WriteLine("  \"AuditIntegrity\": {");
            Console.WriteLine("    \"Enabled\": true,");
            Console.WriteLine($"    \"SigningKey\": \"{sigKey.KeyValue}\"");
            Console.WriteLine("  }");
            Console.WriteLine("}");
            Console.WriteLine();

            Console.WriteLine("✓ Initial keys generated successfully!");
            return 0;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"ERROR: Failed to generate initial keys: {ex.Message}");
            _logger.LogError(ex, "Failed to generate initial keys");
            return 1;
        }
    }

    /// <summary>
    /// Rotates encryption and signing keys.
    /// </summary>
    public async Task<int> RotateKeysAsync()
    {
        try
        {
            Console.WriteLine("=== Key Rotation ===");
            Console.WriteLine();

            // Show current keys
            var currentEncKey = await _keyManagementService.GetActiveEncryptionKeyAsync();
            var currentSigKey = await _keyManagementService.GetActiveSigningKeyAsync();

            Console.WriteLine("Current Active Keys:");
            Console.WriteLine($"  Encryption: {currentEncKey?.KeyId ?? "None"} (Expires: {currentEncKey?.ExpiresAt:yyyy-MM-dd})");
            Console.WriteLine($"  Signing: {currentSigKey?.KeyId ?? "None"} (Expires: {currentSigKey?.ExpiresAt:yyyy-MM-dd})");
            Console.WriteLine();

            Console.Write("Proceed with key rotation? (yes/no): ");
            var response = Console.ReadLine()?.Trim().ToLower();
            if (response != "yes" && response != "y")
            {
                Console.WriteLine("Operation cancelled.");
                return 0;
            }

            Console.WriteLine();
            Console.WriteLine("Rotating keys...");

            var newKeys = await _keyManagementService.RotateKeysAsync();

            Console.WriteLine();
            Console.WriteLine("✓ Key rotation completed successfully!");
            Console.WriteLine();
            Console.WriteLine("New Keys:");
            Console.WriteLine($"  Encryption: {newKeys["encryption"].KeyId}");
            Console.WriteLine($"    Key: {newKeys["encryption"].KeyValue}");
            Console.WriteLine($"    Expires: {newKeys["encryption"].ExpiresAt:yyyy-MM-dd}");
            Console.WriteLine();
            Console.WriteLine($"  Signing: {newKeys["signing"].KeyId}");
            Console.WriteLine($"    Key: {newKeys["signing"].KeyValue}");
            Console.WriteLine($"    Expires: {newKeys["signing"].ExpiresAt:yyyy-MM-dd}");
            Console.WriteLine();

            Console.WriteLine("Update your appsettings.json with the new keys:");
            Console.WriteLine();
            Console.WriteLine("{");
            Console.WriteLine("  \"AuditEncryption\": {");
            Console.WriteLine($"    \"Key\": \"{newKeys["encryption"].KeyValue}\"");
            Console.WriteLine("  },");
            Console.WriteLine("  \"AuditIntegrity\": {");
            Console.WriteLine($"    \"SigningKey\": \"{newKeys["signing"].KeyValue}\"");
            Console.WriteLine("  }");
            Console.WriteLine("}");
            Console.WriteLine();

            Console.WriteLine("NOTE: Old keys have been deactivated but retained for decrypting historical data.");

            return 0;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"ERROR: Failed to rotate keys: {ex.Message}");
            _logger.LogError(ex, "Failed to rotate keys");
            return 1;
        }
    }

    /// <summary>
    /// Lists all keys in storage.
    /// </summary>
    public async Task<int> ListKeysAsync(bool includeInactive = false)
    {
        try
        {
            Console.WriteLine("=== Key List ===");
            Console.WriteLine();

            var keys = await _keyManagementService.ListKeysAsync(includeInactive);

            if (!keys.Any())
            {
                Console.WriteLine("No keys found.");
                return 0;
            }

            Console.WriteLine($"Found {keys.Count} key(s):");
            Console.WriteLine();

            foreach (var key in keys)
            {
                var status = key.IsActive ? "ACTIVE" : "INACTIVE";
                var expiration = key.ExpiresAt > DateTime.UtcNow ? $"Expires: {key.ExpiresAt:yyyy-MM-dd}" : "EXPIRED";
                
                Console.WriteLine($"[{status}] {key.KeyId}");
                Console.WriteLine($"  Type: {key.KeyType}");
                Console.WriteLine($"  Created: {key.CreatedAt:yyyy-MM-dd HH:mm:ss}");
                Console.WriteLine($"  {expiration}");
                
                if (key.DeactivatedAt.HasValue)
                {
                    Console.WriteLine($"  Deactivated: {key.DeactivatedAt.Value:yyyy-MM-dd HH:mm:ss}");
                }
                
                Console.WriteLine();
            }

            return 0;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"ERROR: Failed to list keys: {ex.Message}");
            _logger.LogError(ex, "Failed to list keys");
            return 1;
        }
    }

    /// <summary>
    /// Validates that all required keys are present and active.
    /// </summary>
    public async Task<int> ValidateKeysAsync()
    {
        try
        {
            Console.WriteLine("=== Key Validation ===");
            Console.WriteLine();

            var result = await _keyManagementService.ValidateKeysAsync();

            if (result.IsValid)
            {
                Console.WriteLine("✓ All keys are valid!");
                
                var encKey = await _keyManagementService.GetActiveEncryptionKeyAsync();
                var sigKey = await _keyManagementService.GetActiveSigningKeyAsync();
                
                Console.WriteLine();
                Console.WriteLine("Active Keys:");
                Console.WriteLine($"  Encryption: {encKey?.KeyId} (Expires: {encKey?.ExpiresAt:yyyy-MM-dd})");
                Console.WriteLine($"  Signing: {sigKey?.KeyId} (Expires: {sigKey?.ExpiresAt:yyyy-MM-dd})");
                
                return 0;
            }
            else
            {
                Console.WriteLine("✗ Key validation failed!");
                Console.WriteLine();
                Console.WriteLine("Issues found:");
                foreach (var issue in result.Issues)
                {
                    Console.WriteLine($"  - {issue}");
                }
                
                return 1;
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"ERROR: Failed to validate keys: {ex.Message}");
            _logger.LogError(ex, "Failed to validate keys");
            return 1;
        }
    }

    /// <summary>
    /// Exports key metadata for backup or auditing.
    /// </summary>
    public async Task<int> ExportMetadataAsync(string? outputPath = null)
    {
        try
        {
            Console.WriteLine("=== Export Key Metadata ===");
            Console.WriteLine();

            var metadata = await _keyManagementService.ExportKeyMetadataAsync();

            if (string.IsNullOrWhiteSpace(outputPath))
            {
                Console.WriteLine(metadata);
            }
            else
            {
                await File.WriteAllTextAsync(outputPath, metadata);
                Console.WriteLine($"✓ Key metadata exported to: {outputPath}");
            }

            return 0;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"ERROR: Failed to export metadata: {ex.Message}");
            _logger.LogError(ex, "Failed to export metadata");
            return 1;
        }
    }

    /// <summary>
    /// Purges old inactive keys.
    /// </summary>
    public async Task<int> PurgeOldKeysAsync(int retentionDays)
    {
        try
        {
            Console.WriteLine("=== Purge Old Keys ===");
            Console.WriteLine();

            Console.WriteLine($"WARNING: This will permanently delete inactive keys older than {retentionDays} days.");
            Console.WriteLine("Data encrypted with these keys will become UNRECOVERABLE!");
            Console.WriteLine();
            Console.Write("Are you sure you want to proceed? (yes/no): ");
            
            var response = Console.ReadLine()?.Trim().ToLower();
            if (response != "yes" && response != "y")
            {
                Console.WriteLine("Operation cancelled.");
                return 0;
            }

            Console.WriteLine();
            Console.WriteLine("Purging old keys...");

            var deletedCount = await _keyManagementService.PurgeOldKeysAsync(retentionDays);

            Console.WriteLine();
            Console.WriteLine($"✓ Purged {deletedCount} old key(s).");

            return 0;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"ERROR: Failed to purge old keys: {ex.Message}");
            _logger.LogError(ex, "Failed to purge old keys");
            return 1;
        }
    }

    /// <summary>
    /// Checks if keys need rotation.
    /// </summary>
    public async Task<int> CheckRotationNeededAsync()
    {
        try
        {
            Console.WriteLine("=== Check Key Rotation Status ===");
            Console.WriteLine();

            var shouldRotate = await _keyManagementService.ShouldRotateKeysAsync();

            if (shouldRotate)
            {
                Console.WriteLine("⚠ Key rotation is NEEDED!");
                Console.WriteLine();
                
                var encKey = await _keyManagementService.GetActiveEncryptionKeyAsync();
                var sigKey = await _keyManagementService.GetActiveSigningKeyAsync();
                
                if (encKey != null)
                {
                    var daysUntilExpiry = (encKey.ExpiresAt - DateTime.UtcNow).Days;
                    Console.WriteLine($"  Encryption key expires in {daysUntilExpiry} day(s): {encKey.ExpiresAt:yyyy-MM-dd}");
                }
                
                if (sigKey != null)
                {
                    var daysUntilExpiry = (sigKey.ExpiresAt - DateTime.UtcNow).Days;
                    Console.WriteLine($"  Signing key expires in {daysUntilExpiry} day(s): {sigKey.ExpiresAt:yyyy-MM-dd}");
                }
                
                Console.WriteLine();
                Console.WriteLine("Run 'rotate-keys' command to rotate keys.");
                
                return 1; // Return non-zero to indicate action needed
            }
            else
            {
                Console.WriteLine("✓ Keys are current. No rotation needed at this time.");
                
                var encKey = await _keyManagementService.GetActiveEncryptionKeyAsync();
                var sigKey = await _keyManagementService.GetActiveSigningKeyAsync();
                
                if (encKey != null)
                {
                    var daysUntilExpiry = (encKey.ExpiresAt - DateTime.UtcNow).Days;
                    Console.WriteLine($"  Encryption key expires in {daysUntilExpiry} day(s)");
                }
                
                if (sigKey != null)
                {
                    var daysUntilExpiry = (sigKey.ExpiresAt - DateTime.UtcNow).Days;
                    Console.WriteLine($"  Signing key expires in {daysUntilExpiry} day(s)");
                }
                
                return 0;
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"ERROR: Failed to check rotation status: {ex.Message}");
            _logger.LogError(ex, "Failed to check rotation status");
            return 1;
        }
    }
}
