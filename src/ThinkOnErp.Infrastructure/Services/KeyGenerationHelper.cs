using System.Security.Cryptography;

namespace ThinkOnErp.Infrastructure.Services;

/// <summary>
/// Helper class for generating cryptographic keys for audit logging.
/// Provides static methods for key generation that can be used in CLI tools or setup scripts.
/// </summary>
public static class KeyGenerationHelper
{
    /// <summary>
    /// Generates a new 256-bit (32-byte) encryption key for AES-256 encryption.
    /// </summary>
    /// <returns>Base64 encoded encryption key</returns>
    public static string GenerateEncryptionKey()
    {
        var keyBytes = RandomNumberGenerator.GetBytes(32);
        return Convert.ToBase64String(keyBytes);
    }

    /// <summary>
    /// Generates a new 256-bit (32-byte) signing key for HMAC-SHA256 signatures.
    /// </summary>
    /// <returns>Base64 encoded signing key</returns>
    public static string GenerateSigningKey()
    {
        var keyBytes = RandomNumberGenerator.GetBytes(32);
        return Convert.ToBase64String(keyBytes);
    }

    /// <summary>
    /// Generates both encryption and signing keys.
    /// </summary>
    /// <returns>Tuple containing (encryptionKey, signingKey) both Base64 encoded</returns>
    public static (string encryptionKey, string signingKey) GenerateBothKeys()
    {
        return (GenerateEncryptionKey(), GenerateSigningKey());
    }

    /// <summary>
    /// Validates that a Base64 encoded key is the correct length (32 bytes when decoded).
    /// </summary>
    /// <param name="base64Key">Base64 encoded key to validate</param>
    /// <returns>True if valid, false otherwise</returns>
    public static bool ValidateKeyLength(string base64Key)
    {
        if (string.IsNullOrWhiteSpace(base64Key))
            return false;

        try
        {
            var keyBytes = Convert.FromBase64String(base64Key);
            return keyBytes.Length == 32;
        }
        catch (FormatException)
        {
            return false;
        }
    }

    /// <summary>
    /// Generates a configuration snippet for appsettings.json with new keys.
    /// </summary>
    /// <returns>JSON configuration snippet</returns>
    public static string GenerateConfigurationSnippet()
    {
        var (encryptionKey, signingKey) = GenerateBothKeys();

        return $@"{{
  ""AuditEncryption"": {{
    ""Enabled"": true,
    ""Key"": ""{encryptionKey}"",
    ""KeyRotationDays"": 90,
    ""UseHsm"": false,
    ""EncryptedFields"": [
      ""password"",
      ""token"",
      ""refreshToken"",
      ""creditCard"",
      ""ssn"",
      ""apiKey""
    ],
    ""LogEncryptionOperations"": false,
    ""EncryptionTimeoutMs"": 5000
  }},
  ""AuditIntegrity"": {{
    ""Enabled"": true,
    ""SigningKey"": ""{signingKey}"",
    ""AutoGenerateHashes"": true,
    ""VerifyOnRead"": false,
    ""LogIntegrityOperations"": false,
    ""BatchSize"": 100,
    ""VerificationTimeoutMs"": 10000,
    ""AlertOnTampering"": true,
    ""HashAlgorithm"": ""HMACSHA256""
  }},
  ""KeyManagement"": {{
    ""StorageBackend"": ""Configuration"",
    ""AutoRotationEnabled"": false,
    ""RotationPeriodDays"": 90,
    ""RotationWarningDays"": 7,
    ""RetainOldKeys"": true,
    ""MaxOldKeysToRetain"": 3,
    ""LogKeyOperations"": true
  }}
}}";
    }

    /// <summary>
    /// Generates PowerShell commands for key generation.
    /// </summary>
    /// <returns>PowerShell script</returns>
    public static string GeneratePowerShellScript()
    {
        return @"# PowerShell script to generate encryption and signing keys

# Generate encryption key (32 bytes = 256 bits)
$encryptionKeyBytes = New-Object byte[] 32
$rng = [System.Security.Cryptography.RandomNumberGenerator]::Create()
$rng.GetBytes($encryptionKeyBytes)
$encryptionKey = [Convert]::ToBase64String($encryptionKeyBytes)

# Generate signing key (32 bytes = 256 bits)
$signingKeyBytes = New-Object byte[] 32
$rng.GetBytes($signingKeyBytes)
$signingKey = [Convert]::ToBase64String($signingKeyBytes)

Write-Host ""Generated Keys:""
Write-Host ""================""
Write-Host """"
Write-Host ""Encryption Key (AuditEncryption:Key):""
Write-Host $encryptionKey
Write-Host """"
Write-Host ""Signing Key (AuditIntegrity:SigningKey):""
Write-Host $signingKey
Write-Host """"
Write-Host ""Add these keys to your appsettings.json or set as environment variables:""
Write-Host ""  - AuditEncryption__Key=$encryptionKey""
Write-Host ""  - AuditIntegrity__SigningKey=$signingKey""
";
    }

    /// <summary>
    /// Generates Bash commands for key generation (Linux/macOS).
    /// </summary>
    /// <returns>Bash script</returns>
    public static string GenerateBashScript()
    {
        return @"#!/bin/bash
# Bash script to generate encryption and signing keys

# Generate encryption key (32 bytes = 256 bits)
ENCRYPTION_KEY=$(openssl rand -base64 32)

# Generate signing key (32 bytes = 256 bits)
SIGNING_KEY=$(openssl rand -base64 32)

echo ""Generated Keys:""
echo ""================""
echo """"
echo ""Encryption Key (AuditEncryption:Key):""
echo $ENCRYPTION_KEY
echo """"
echo ""Signing Key (AuditIntegrity:SigningKey):""
echo $SIGNING_KEY
echo """"
echo ""Add these keys to your appsettings.json or set as environment variables:""
echo ""  export AuditEncryption__Key=$ENCRYPTION_KEY""
echo ""  export AuditIntegrity__SigningKey=$SIGNING_KEY""
";
    }

    /// <summary>
    /// Generates a .NET CLI command for key generation.
    /// </summary>
    /// <returns>.NET CLI command</returns>
    public static string GenerateDotNetCliCommand()
    {
        return @"# .NET CLI command to generate keys using C# script

dotnet-script eval ""
using System;
using System.Security.Cryptography;

var encKey = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));
var sigKey = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));

Console.WriteLine(\""Encryption Key: \"" + encKey);
Console.WriteLine(\""Signing Key: \"" + sigKey);
""
";
    }
}
