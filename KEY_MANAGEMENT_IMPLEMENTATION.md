# Key Management Implementation Summary

## Overview

Implemented comprehensive encryption and signing key management for the Full Traceability System. This provides secure key generation, storage, rotation, and lifecycle management for audit log encryption and integrity verification.

## Components Implemented

### 1. Core Services

#### KeyManagementService (`src/ThinkOnErp.Infrastructure/Services/KeyManagementService.cs`)
- **Purpose**: Central service for managing encryption and signing keys
- **Features**:
  - Generate 256-bit encryption keys for AES-256-GCM
  - Generate 512-bit signing keys for HMAC-SHA256/SHA512
  - Retrieve active keys by type
  - Retrieve historical keys by ID (for decrypting old data)
  - Automatic key rotation with old key retention
  - Key validation and health checks
  - Key metadata export for auditing
  - Secure key storage with file system permissions
  - In-memory caching with configurable refresh

#### KeyRotationBackgroundService (`src/ThinkOnErp.Infrastructure/Services/KeyRotationBackgroundService.cs`)
- **Purpose**: Automatic key rotation background service
- **Features**:
  - Periodic checks for key expiration (daily)
  - Automatic rotation when keys expire
  - Alert notifications on rotation success/failure
  - Key validation after rotation
  - Configurable enable/disable via settings

#### KeyManagementCli (`src/ThinkOnErp.Infrastructure/Services/KeyManagementCli.cs`)
- **Purpose**: Command-line interface for key management operations
- **Commands**:
  - `generate-initial`: Generate initial encryption and signing keys
  - `rotate-keys`: Manually rotate keys
  - `list-keys`: List all keys with status
  - `validate-keys`: Validate key configuration
  - `export-metadata`: Export key metadata for backup
  - `purge-old-keys`: Delete old inactive keys
  - `check-rotation`: Check if rotation is needed

### 2. Configuration

#### KeyManagementOptions (`src/ThinkOnErp.Infrastructure/Configuration/KeyManagementOptions.cs`)
- **Settings**:
  - `KeyStoragePath`: Directory for key storage
  - `KeyRotationDays`: Days before key expiration (default: 90)
  - `KeyRotationWarningDays`: Warning threshold (default: 7)
  - `EnableAutoRotation`: Enable automatic rotation (default: false)
  - `CacheRefreshMinutes`: Key cache refresh interval (default: 60)
  - `InactiveKeyRetentionDays`: Retention for old keys (default: 365)
  - `LogKeyOperations`: Enable operation logging (default: true)

### 3. Interfaces

#### IKeyManagementService (`src/ThinkOnErp.Domain/Interfaces/IKeyManagementService.cs`)
- Defines contract for key management operations
- Supports dependency injection and testing
- Enables future implementation of external key stores (Azure Key Vault, AWS KMS)

### 4. Documentation

#### Key Management Guide (`docs/KEY_MANAGEMENT_GUIDE.md`)
- Comprehensive guide covering:
  - Initial setup and key generation
  - Key rotation procedures
  - Security best practices
  - Troubleshooting common issues
  - Compliance considerations (GDPR, SOX, ISO 27001, PCI DSS)
  - Monitoring and alerting
  - API integration examples

## Key Features

### 1. Secure Key Generation
- Uses `RandomNumberGenerator.GetBytes()` for cryptographically secure random keys
- Generates 256-bit keys for AES-256 encryption
- Generates 512-bit keys for HMAC signatures
- Keys are Base64-encoded for easy configuration

### 2. Key Rotation
- **Automatic Rotation**: Background service checks daily and rotates expired keys
- **Manual Rotation**: CLI commands for on-demand rotation
- **Old Key Retention**: Inactive keys retained for decrypting historical data
- **Zero Downtime**: New keys become active immediately, old keys remain for decryption

### 3. Key Storage
- **File System**: JSON files with restricted permissions
- **Versioning**: Multiple key versions supported simultaneously
- **Metadata**: Tracks creation date, expiration, active status
- **Caching**: In-memory cache with configurable refresh interval

### 4. Key Validation
- Validates key presence and format
- Checks key length (32 bytes for encryption, ≥32 bytes for signing)
- Verifies Base64 encoding
- Warns about expiring keys
- Returns detailed validation results

### 5. Security Features
- **Separation of Concerns**: Separate keys for encryption and signing
- **Key Versioning**: Support for multiple key versions
- **Access Control**: File system permissions restrict key access
- **Audit Trail**: All key operations logged
- **Alert Integration**: Notifications on rotation events

## Usage Examples

### Initial Setup

```bash
# Generate initial keys
dotnet run --project src/ThinkOnErp.API -- key-management generate-initial

# Output:
# === Generating Initial Keys ===
# 
# Generating encryption key...
# ✓ Encryption key generated: enc-a1b2c3d4e5f6g7h8
#   Key: dGhpc2lzYXNlY3VyZWtleWZvcmVuY3J5cHRpb24=
#   Expires: 2026-07-30
# 
# Generating signing key...
# ✓ Signing key generated: sig-i9j0k1l2m3n4o5p6
#   Key: dGhpc2lzYXNlY3VyZWtleWZvcnNpZ25pbmc=
#   Expires: 2026-07-30
```

### Key Rotation

```bash
# Check if rotation needed
dotnet run --project src/ThinkOnErp.API -- key-management check-rotation

# Rotate keys
dotnet run --project src/ThinkOnErp.API -- key-management rotate-keys

# Validate new keys
dotnet run --project src/ThinkOnErp.API -- key-management validate-keys
```

### Programmatic Usage

```csharp
public class AuditService
{
    private readonly IKeyManagementService _keyManagement;
    private readonly IAuditDataEncryption _encryption;
    
    public AuditService(
        IKeyManagementService keyManagement,
        IAuditDataEncryption encryption)
    {
        _keyManagement = keyManagement;
        _encryption = encryption;
    }
    
    public async Task EncryptSensitiveDataAsync(string data)
    {
        // Get active encryption key
        var key = await _keyManagement.GetActiveEncryptionKeyAsync();
        
        if (key == null)
        {
            throw new InvalidOperationException("No active encryption key found");
        }
        
        // Encrypt data
        var encrypted = await _encryption.EncryptAsync(data);
        
        return encrypted;
    }
    
    public async Task RotateKeysIfNeededAsync()
    {
        if (await _keyManagement.ShouldRotateKeysAsync())
        {
            var newKeys = await _keyManagement.RotateKeysAsync();
            
            // Update configuration with new keys
            await UpdateConfigurationAsync(newKeys);
        }
    }
}
```

## Configuration Example

```json
{
  "AuditEncryption": {
    "Enabled": true,
    "Key": "dGhpc2lzYXNlY3VyZWtleWZvcmVuY3J5cHRpb24=",
    "LogEncryptionOperations": false
  },
  "AuditIntegrity": {
    "Enabled": true,
    "SigningKey": "dGhpc2lzYXNlY3VyZWtleWZvcnNpZ25pbmc=",
    "HashAlgorithm": "HMACSHA256",
    "LogIntegrityOperations": false,
    "AlertOnTampering": true
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

## Security Considerations

### 1. Key Storage
- **File System Permissions**: Restrict access to application user only
- **Encryption at Rest**: Use OS-level encryption (BitLocker, LUKS)
- **External Storage**: Use Azure Key Vault or AWS KMS in production

### 2. Key Rotation
- **Regular Rotation**: Rotate keys every 90 days (configurable)
- **Old Key Retention**: Keep inactive keys for 1 year minimum
- **Zero Downtime**: Rotation doesn't interrupt service

### 3. Access Control
- **Principle of Least Privilege**: Only key management service accesses keys
- **Audit Trail**: All key operations logged
- **Alert Notifications**: Alerts on rotation and validation failures

### 4. Compliance
- **GDPR**: Retain keys to decrypt personal data for deletion/export
- **SOX**: Document key rotation procedures and access controls
- **ISO 27001**: Use approved algorithms (AES-256, HMAC-SHA256)
- **PCI DSS**: Rotate keys annually, use 256-bit minimum

## Integration with Existing Services

### AuditDataEncryption
- Uses active encryption key from KeyManagementService
- Falls back to configuration key if service unavailable
- Supports decryption with old keys by key ID

### AuditLogIntegrityService
- Uses active signing key from KeyManagementService
- Falls back to configuration key if service unavailable
- Supports verification with old keys by key ID

### AlertManager
- Receives notifications on key rotation events
- Sends alerts on rotation success/failure
- Alerts on key validation issues

## Testing

### Unit Tests
- Key generation produces valid keys
- Key rotation creates new keys and deactivates old ones
- Key validation detects invalid configurations
- Key retrieval returns correct keys by ID and type

### Integration Tests
- Key storage and retrieval from file system
- Key rotation with background service
- Alert notifications on rotation events
- Configuration integration with encryption/integrity services

## Deployment Checklist

- [ ] Generate initial encryption and signing keys
- [ ] Configure key storage path with restricted permissions
- [ ] Add keys to production configuration (environment variables or secrets manager)
- [ ] Enable automatic key rotation if desired
- [ ] Configure alert notifications for key rotation events
- [ ] Document key recovery procedures
- [ ] Test key rotation in staging environment
- [ ] Schedule regular key rotation (if not automatic)
- [ ] Monitor key expiration dates
- [ ] Backup keys securely

## Future Enhancements

### 1. External Key Storage
- Azure Key Vault integration
- AWS Secrets Manager integration
- HashiCorp Vault integration
- Hardware Security Module (HSM) support

### 2. Key Derivation
- Derive multiple keys from master key
- Key hierarchy for different purposes
- Per-tenant key isolation

### 3. Key Escrow
- Secure key backup to escrow service
- Emergency key recovery procedures
- Multi-party key recovery

### 4. Advanced Rotation
- Gradual key rollover (dual-key period)
- Scheduled rotation windows
- Rollback capability

## References

- [NIST SP 800-57: Key Management Guidelines](https://csrc.nist.gov/publications/detail/sp/800-57-part-1/rev-5/final)
- [OWASP Cryptographic Storage Cheat Sheet](https://cheatsheetseries.owasp.org/cheatsheets/Cryptographic_Storage_Cheat_Sheet.html)
- [Azure Key Vault Best Practices](https://docs.microsoft.com/en-us/azure/key-vault/general/best-practices)
- [AWS KMS Best Practices](https://docs.aws.amazon.com/kms/latest/developerguide/best-practices.html)

## Support

For issues or questions:
1. Check the Key Management Guide: `docs/KEY_MANAGEMENT_GUIDE.md`
2. Review logs for key management operations
3. Run key validation: `dotnet run -- key-management validate-keys`
4. Contact system administrator for key recovery
