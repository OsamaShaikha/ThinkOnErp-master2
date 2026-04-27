# Key Management Guide

## Overview

The Key Management Service provides secure generation, storage, rotation, and management of encryption and signing keys used by the Full Traceability System. This guide covers initial setup, key rotation, and best practices for key security.

## Key Types

### Encryption Keys
- **Purpose**: Encrypt sensitive data in audit logs using AES-256-GCM
- **Key Size**: 256 bits (32 bytes)
- **Algorithm**: AES-256-GCM (Galois/Counter Mode)
- **Usage**: Encrypting sensitive fields like passwords, tokens, PII

### Signing Keys
- **Purpose**: Generate cryptographic signatures for audit log integrity verification
- **Key Size**: 512 bits (64 bytes) 
- **Algorithm**: HMAC-SHA256 or HMAC-SHA512
- **Usage**: Tamper detection through hash comparison

## Initial Setup

### 1. Generate Initial Keys

When setting up the system for the first time, generate encryption and signing keys:

```bash
dotnet run --project src/ThinkOnErp.API -- key-management generate-initial
```

This will:
- Generate a new 256-bit encryption key for AES-256
- Generate a new 512-bit signing key for HMAC
- Save keys to the configured storage location
- Display the keys and configuration snippet

### 2. Configure Application

Add the generated keys to your `appsettings.json`:

```json
{
  "AuditEncryption": {
    "Enabled": true,
    "Key": "BASE64_ENCODED_ENCRYPTION_KEY_HERE",
    "LogEncryptionOperations": false
  },
  "AuditIntegrity": {
    "Enabled": true,
    "SigningKey": "BASE64_ENCODED_SIGNING_KEY_HERE",
    "HashAlgorithm": "HMACSHA256",
    "LogIntegrityOperations": false,
    "AlertOnTampering": true,
    "BatchSize": 100
  },
  "KeyManagement": {
    "KeyStoragePath": "/secure/path/to/keys",
    "KeyRotationDays": 90,
    "KeyRotationWarningDays": 7,
    "EnableAutoRotation": false,
    "CacheRefreshMinutes": 60,
    "InactiveKeyRetentionDays": 365,
    "LogKeyOperations": true
  }
}
```

### 3. Secure Key Storage

**CRITICAL SECURITY REQUIREMENTS:**

1. **Never commit keys to source control**
   - Add `appsettings.Production.json` to `.gitignore`
   - Use environment variables or Azure Key Vault in production

2. **Restrict file system permissions**
   ```bash
   # Linux/Mac
   chmod 600 /path/to/keys/*.key.json
   chown app-user:app-group /path/to/keys
   
   # Windows
   icacls "C:\path\to\keys" /inheritance:r /grant:r "AppUser:(OI)(CI)F"
   ```

3. **Use secure key storage in production**
   - Azure Key Vault
   - AWS Secrets Manager
   - HashiCorp Vault
   - Hardware Security Modules (HSM)

## Key Rotation

### Why Rotate Keys?

- **Security Best Practice**: Limits exposure if a key is compromised
- **Compliance Requirements**: Many regulations require periodic key rotation
- **Cryptographic Hygiene**: Reduces the amount of data encrypted with a single key

### Automatic Key Rotation

Enable automatic rotation in configuration:

```json
{
  "KeyManagement": {
    "EnableAutoRotation": true,
    "KeyRotationDays": 90,
    "KeyRotationWarningDays": 7
  }
}
```

The `KeyRotationBackgroundService` will:
- Check daily if keys are expiring
- Automatically rotate keys when they reach expiration
- Send alerts on rotation success/failure
- Retain old keys for decrypting historical data

### Manual Key Rotation

Rotate keys manually using the CLI:

```bash
# Check if rotation is needed
dotnet run --project src/ThinkOnErp.API -- key-management check-rotation

# Perform key rotation
dotnet run --project src/ThinkOnErp.API -- key-management rotate-keys
```

**Key Rotation Process:**

1. New encryption and signing keys are generated
2. Old keys are marked as inactive but retained
3. New keys become active for all new operations
4. Old keys remain available for decrypting historical data
5. Configuration must be updated with new keys

### Post-Rotation Steps

1. **Update Configuration**
   ```bash
   # Update appsettings.json with new keys
   # Or update environment variables/secrets manager
   ```

2. **Restart Application**
   ```bash
   # Restart to load new keys
   systemctl restart thinkonerp-api
   ```

3. **Verify Key Rotation**
   ```bash
   dotnet run --project src/ThinkOnErp.API -- key-management validate-keys
   ```

## Key Management Operations

### List All Keys

View all keys in storage:

```bash
# List active keys only
dotnet run --project src/ThinkOnErp.API -- key-management list-keys

# List all keys including inactive
dotnet run --project src/ThinkOnErp.API -- key-management list-keys --include-inactive
```

### Validate Keys

Check that all required keys are present and valid:

```bash
dotnet run --project src/ThinkOnErp.API -- key-management validate-keys
```

Validation checks:
- Active encryption key exists
- Active signing key exists
- Keys are valid Base64 strings
- Keys have correct length (32 bytes for encryption, ≥32 bytes for signing)
- Keys are not expired or expiring soon

### Export Key Metadata

Export key metadata (without key values) for backup or auditing:

```bash
# Print to console
dotnet run --project src/ThinkOnErp.API -- key-management export-metadata

# Save to file
dotnet run --project src/ThinkOnErp.API -- key-management export-metadata --output keys-metadata.json
```

### Purge Old Keys

Delete inactive keys that are past their retention period:

```bash
# Purge keys inactive for more than 365 days
dotnet run --project src/ThinkOnErp.API -- key-management purge-old-keys --retention-days 365
```

**WARNING:** This permanently deletes keys. Data encrypted with these keys will become unrecoverable.

## Key Storage

### File System Storage

Keys are stored as JSON files in the configured directory:

```
/path/to/keys/
├── enc-a1b2c3d4.key.json    # Encryption key
├── sig-e5f6g7h8.key.json    # Signing key
└── enc-i9j0k1l2.key.json    # Old encryption key (inactive)
```

**Key File Format:**
```json
{
  "KeyId": "enc-a1b2c3d4e5f6g7h8",
  "KeyType": "Encryption",
  "KeyValue": "BASE64_ENCODED_KEY_HERE",
  "CreatedAt": "2026-05-01T10:00:00Z",
  "ExpiresAt": "2026-07-30T10:00:00Z",
  "IsActive": true,
  "DeactivatedAt": null,
  "Version": 1
}
```

### External Key Storage (Production)

For production environments, integrate with external key management systems:

#### Azure Key Vault

```csharp
// Configure Azure Key Vault
services.AddAzureKeyVault(configuration);

// Retrieve keys from Key Vault
var encryptionKey = await keyVaultClient.GetSecretAsync("audit-encryption-key");
var signingKey = await keyVaultClient.GetSecretAsync("audit-signing-key");
```

#### AWS Secrets Manager

```csharp
// Configure AWS Secrets Manager
var client = new AmazonSecretsManagerClient();

// Retrieve keys
var encryptionKey = await client.GetSecretValueAsync(new GetSecretValueRequest
{
    SecretId = "audit-encryption-key"
});
```

## Security Best Practices

### 1. Key Generation

- **Use cryptographically secure random number generators**
  - .NET: `RandomNumberGenerator.GetBytes()`
  - Never use `Random` class for key generation

- **Generate keys with sufficient entropy**
  - Encryption keys: 256 bits minimum
  - Signing keys: 256 bits minimum (512 bits recommended)

### 2. Key Storage

- **Encrypt keys at rest**
  - Use OS-level encryption (BitLocker, LUKS)
  - Use hardware security modules (HSM) for high-security environments

- **Restrict access**
  - Limit file system permissions to application user only
  - Use principle of least privilege

- **Separate keys from application code**
  - Never hardcode keys in source code
  - Store keys in separate configuration files or external systems

### 3. Key Usage

- **Use different keys for different purposes**
  - Separate encryption and signing keys
  - Consider separate keys per environment (dev, staging, prod)

- **Implement key versioning**
  - Support multiple key versions simultaneously
  - Retain old keys for decrypting historical data

- **Monitor key usage**
  - Log key rotation events
  - Alert on key expiration
  - Track which keys are used for which operations

### 4. Key Rotation

- **Rotate keys regularly**
  - Recommended: Every 90 days
  - Compliance requirements may mandate specific intervals

- **Plan for key rotation**
  - Test rotation process in non-production environments
  - Document rotation procedures
  - Have rollback plan in case of issues

- **Retain old keys**
  - Keep inactive keys for at least 1 year
  - Needed to decrypt historical audit data
  - Purge only after data retention period expires

### 5. Key Backup and Recovery

- **Backup keys securely**
  - Encrypt backups
  - Store in separate location from primary keys
  - Test recovery procedures regularly

- **Document key recovery process**
  - Who has access to backups?
  - How to restore keys in emergency?
  - What is the RTO (Recovery Time Objective)?

## Troubleshooting

### Issue: "Encryption key not configured"

**Cause:** No encryption key in configuration

**Solution:**
```bash
# Generate new keys
dotnet run --project src/ThinkOnErp.API -- key-management generate-initial

# Add keys to appsettings.json
```

### Issue: "Encryption key must be 32 bytes"

**Cause:** Invalid key length

**Solution:**
```bash
# Generate a valid 256-bit key
dotnet run --project src/ThinkOnErp.API -- key-management generate-initial
```

### Issue: "Decryption failed: Data may have been tampered with"

**Cause:** Wrong encryption key or corrupted data

**Solution:**
1. Check if encryption key was rotated
2. Retrieve old key from key storage
3. Use old key to decrypt historical data

### Issue: "No active encryption key found"

**Cause:** All keys expired or no keys generated

**Solution:**
```bash
# Check key status
dotnet run --project src/ThinkOnErp.API -- key-management list-keys

# Generate new keys if needed
dotnet run --project src/ThinkOnErp.API -- key-management generate-initial
```

### Issue: "TAMPERING DETECTED: Audit log integrity verification failed"

**Cause:** Audit log data modified or wrong signing key

**Solution:**
1. Investigate potential security breach
2. Check if signing key was rotated
3. Verify integrity using old signing key
4. Review audit logs for unauthorized access

## Compliance Considerations

### GDPR

- **Right to be Forgotten**: Retain encryption keys to decrypt personal data for deletion
- **Data Portability**: Retain keys to decrypt data for export
- **Audit Trail**: Log all key management operations

### SOX

- **Access Controls**: Restrict key access to authorized personnel only
- **Change Management**: Document and approve all key rotation activities
- **Audit Trail**: Maintain complete history of key operations

### ISO 27001

- **Key Management Policy**: Document key lifecycle procedures
- **Cryptographic Controls**: Use approved algorithms (AES-256, HMAC-SHA256)
- **Key Storage**: Implement secure key storage mechanisms

### PCI DSS

- **Key Rotation**: Rotate keys at least annually
- **Key Strength**: Use minimum 256-bit keys
- **Key Access**: Implement dual control for key access

## Monitoring and Alerting

### Key Expiration Alerts

Configure alerts for key expiration:

```json
{
  "KeyManagement": {
    "KeyRotationWarningDays": 7
  }
}
```

Alerts are sent when:
- Keys expire in 7 days or less
- Automatic rotation succeeds
- Automatic rotation fails
- Key validation finds issues

### Key Usage Metrics

Monitor key management operations:
- Key generation events
- Key rotation events
- Key validation results
- Decryption failures (may indicate wrong key)

### Security Monitoring

Alert on suspicious activities:
- Multiple decryption failures
- Unauthorized key access attempts
- Key file modifications
- Missing or corrupted keys

## API Integration

### Programmatic Key Management

Use the `IKeyManagementService` interface in your code:

```csharp
public class MyService
{
    private readonly IKeyManagementService _keyManagement;
    
    public MyService(IKeyManagementService keyManagement)
    {
        _keyManagement = keyManagement;
    }
    
    public async Task RotateKeysAsync()
    {
        // Check if rotation needed
        if (await _keyManagement.ShouldRotateKeysAsync())
        {
            // Perform rotation
            var newKeys = await _keyManagement.RotateKeysAsync();
            
            // Update configuration
            await UpdateConfigurationAsync(newKeys);
        }
    }
    
    public async Task ValidateKeysAsync()
    {
        var result = await _keyManagement.ValidateKeysAsync();
        
        if (!result.IsValid)
        {
            // Handle validation failures
            foreach (var issue in result.Issues)
            {
                _logger.LogWarning("Key validation issue: {Issue}", issue);
            }
        }
    }
}
```

## References

- [NIST Key Management Guidelines](https://csrc.nist.gov/publications/detail/sp/800-57-part-1/rev-5/final)
- [OWASP Cryptographic Storage Cheat Sheet](https://cheatsheetseries.owasp.org/cheatsheets/Cryptographic_Storage_Cheat_Sheet.html)
- [Azure Key Vault Best Practices](https://docs.microsoft.com/en-us/azure/key-vault/general/best-practices)
- [AWS Key Management Best Practices](https://docs.aws.amazon.com/kms/latest/developerguide/best-practices.html)
