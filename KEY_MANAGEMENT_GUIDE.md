# Secure Key Management Guide

## Overview

The ThinkOnErp audit system uses encryption and signing keys to protect sensitive audit data and ensure audit log integrity. This guide explains how to securely manage these keys across different environments.

## Key Types

### 1. Encryption Key
- **Purpose**: Encrypts sensitive fields in audit logs (passwords, tokens, PII)
- **Algorithm**: AES-256-GCM
- **Key Size**: 32 bytes (256 bits)
- **Format**: Base64 encoded string

### 2. Signing Key
- **Purpose**: Generates cryptographic signatures for audit log integrity verification
- **Algorithm**: HMAC-SHA256
- **Key Size**: Minimum 32 bytes (256 bits)
- **Format**: Base64 encoded string

## Key Storage Providers

The system supports multiple key storage providers with automatic fallback:

### 1. Configuration Provider (Development)
**Use Case**: Development and testing environments

**Pros**:
- Simple setup
- No external dependencies
- Fast access

**Cons**:
- Keys stored in configuration files or environment variables
- Not suitable for production
- No automatic key rotation

**Configuration**:
```json
{
  "KeyManagement": {
    "Provider": "Configuration",
    "Configuration": {
      "EncryptionKeyPath": "AuditEncryption:Key",
      "SigningKeyPath": "AuditIntegrity:SigningKey",
      "UseEnvironmentVariables": true,
      "EncryptionKeyEnvironmentVariable": "AUDIT_ENCRYPTION_KEY",
      "SigningKeyEnvironmentVariable": "AUDIT_SIGNING_KEY"
    }
  }
}
```

**Setup**:
1. Generate keys using PowerShell:
```powershell
$bytes = New-Object byte[] 32
$rng = [System.Security.Cryptography.RandomNumberGenerator]::Create()
$rng.GetBytes($bytes)
$key = [Convert]::ToBase64String($bytes)
Write-Host "Generated Key: $key"
```

2. Set environment variables:
```bash
# Linux/Mac
export AUDIT_ENCRYPTION_KEY="your-base64-key-here"
export AUDIT_SIGNING_KEY="your-base64-key-here"

# Windows PowerShell
$env:AUDIT_ENCRYPTION_KEY="your-base64-key-here"
$env:AUDIT_SIGNING_KEY="your-base64-key-here"
```

3. Or add to appsettings.json (not recommended for production):
```json
{
  "AuditEncryption": {
    "Key": "your-base64-key-here"
  },
  "AuditIntegrity": {
    "SigningKey": "your-base64-key-here"
  }
}
```

### 2. Local Storage Provider (Development/Testing)
**Use Case**: Development environments where automatic key generation is desired

**Pros**:
- Automatic key generation
- Keys encrypted using DPAPI (Windows) or file permissions (Linux/Mac)
- Supports key rotation
- No external dependencies

**Cons**:
- Keys stored on local filesystem
- Not suitable for production
- Limited to single server

**Configuration**:
```json
{
  "KeyManagement": {
    "Provider": "LocalStorage",
    "LocalStorage": {
      "KeyStoragePath": "Keys",
      "EncryptionKeyFileName": "encryption.key",
      "SigningKeyFileName": "signing.key",
      "UseDataProtection": true,
      "AutoGenerateKeys": true,
      "FilePermissions": "600"
    }
  }
}
```

**Setup**:
1. Set provider to "LocalStorage" in configuration
2. Keys will be automatically generated on first run
3. Keys are stored in the `Keys` directory with restricted permissions
4. On Windows, keys are encrypted using DPAPI
5. On Linux/Mac, keys are protected by file permissions (chmod 600)

**Key Rotation**:
```bash
# Keys can be rotated programmatically or via API
# Old keys are backed up with timestamp
```

### 3. Azure Key Vault Provider (Production)
**Use Case**: Production environments on Azure

**Pros**:
- Centralized key management
- Hardware security module (HSM) backed
- Automatic key rotation
- Audit logging
- Access control via Azure RBAC
- High availability

**Cons**:
- Requires Azure subscription
- Additional cost
- Network dependency

**Configuration**:
```json
{
  "KeyManagement": {
    "Provider": "AzureKeyVault",
    "AzureKeyVault": {
      "VaultUrl": "https://your-vault.vault.azure.net/",
      "EncryptionKeySecretName": "audit-encryption-key",
      "SigningKeySecretName": "audit-signing-key",
      "AuthenticationMethod": "ManagedIdentity",
      "TimeoutSeconds": 30,
      "RetryAttempts": 3
    }
  }
}
```

**Setup**:

1. **Install NuGet Packages**:
```bash
dotnet add package Azure.Identity
dotnet add package Azure.Security.KeyVault.Secrets
```

2. **Create Azure Key Vault**:
```bash
# Create resource group
az group create --name myResourceGroup --location eastus

# Create Key Vault
az keyvault create --name myKeyVault --resource-group myResourceGroup --location eastus

# Enable soft delete and purge protection (recommended)
az keyvault update --name myKeyVault --enable-soft-delete true --enable-purge-protection true
```

3. **Generate and Store Keys**:
```bash
# Generate encryption key
$encryptionKey = [Convert]::ToBase64String([System.Security.Cryptography.RandomNumberGenerator]::GetBytes(32))

# Store in Key Vault
az keyvault secret set --vault-name myKeyVault --name audit-encryption-key --value $encryptionKey

# Generate signing key
$signingKey = [Convert]::ToBase64String([System.Security.Cryptography.RandomNumberGenerator]::GetBytes(32))

# Store in Key Vault
az keyvault secret set --vault-name myKeyVault --name audit-signing-key --value $signingKey
```

4. **Configure Access**:

**Option A: Managed Identity (Recommended)**:
```bash
# Enable system-assigned managed identity for your App Service
az webapp identity assign --name myApp --resource-group myResourceGroup

# Grant access to Key Vault
az keyvault set-policy --name myKeyVault --object-id <managed-identity-object-id> --secret-permissions get list
```

**Option B: Service Principal**:
```bash
# Create service principal
az ad sp create-for-rbac --name myApp --role contributor

# Grant access to Key Vault
az keyvault set-policy --name myKeyVault --spn <service-principal-app-id> --secret-permissions get list
```

5. **Update Configuration**:
```json
{
  "KeyManagement": {
    "Provider": "AzureKeyVault",
    "AzureKeyVault": {
      "VaultUrl": "https://myKeyVault.vault.azure.net/",
      "AuthenticationMethod": "ManagedIdentity"
    }
  }
}
```

### 4. AWS Secrets Manager Provider (Production)
**Use Case**: Production environments on AWS

**Pros**:
- Centralized key management
- Automatic key rotation
- Audit logging via CloudTrail
- Access control via IAM
- High availability
- Encryption at rest

**Cons**:
- Requires AWS account
- Additional cost
- Network dependency

**Configuration**:
```json
{
  "KeyManagement": {
    "Provider": "AwsSecretsManager",
    "AwsSecretsManager": {
      "Region": "us-east-1",
      "EncryptionKeySecretName": "audit/encryption-key",
      "SigningKeySecretName": "audit/signing-key",
      "AuthenticationMethod": "IAMRole",
      "TimeoutSeconds": 30,
      "RetryAttempts": 3
    }
  }
}
```

**Setup**:

1. **Install NuGet Package**:
```bash
dotnet add package AWSSDK.SecretsManager
```

2. **Generate and Store Keys**:
```bash
# Generate encryption key
ENCRYPTION_KEY=$(openssl rand -base64 32)

# Store in Secrets Manager
aws secretsmanager create-secret \
    --name audit/encryption-key \
    --secret-string "$ENCRYPTION_KEY" \
    --region us-east-1

# Generate signing key
SIGNING_KEY=$(openssl rand -base64 32)

# Store in Secrets Manager
aws secretsmanager create-secret \
    --name audit/signing-key \
    --secret-string "$SIGNING_KEY" \
    --region us-east-1
```

3. **Configure IAM Permissions**:

Create IAM policy:
```json
{
  "Version": "2012-10-17",
  "Statement": [
    {
      "Effect": "Allow",
      "Action": [
        "secretsmanager:GetSecretValue",
        "secretsmanager:DescribeSecret"
      ],
      "Resource": [
        "arn:aws:secretsmanager:us-east-1:*:secret:audit/encryption-key-*",
        "arn:aws:secretsmanager:us-east-1:*:secret:audit/signing-key-*"
      ]
    }
  ]
}
```

Attach policy to EC2 instance role or ECS task role:
```bash
aws iam attach-role-policy \
    --role-name myAppRole \
    --policy-arn arn:aws:iam::aws:policy/custom/AuditKeyAccess
```

4. **Update Configuration**:
```json
{
  "KeyManagement": {
    "Provider": "AwsSecretsManager",
    "AwsSecretsManager": {
      "Region": "us-east-1",
      "AuthenticationMethod": "IAMRole"
    }
  }
}
```

## Fallback Configuration

Configure a fallback provider in case the primary provider fails:

```json
{
  "KeyManagement": {
    "Provider": "AzureKeyVault",
    "FallbackProvider": "Configuration",
    "AzureKeyVault": {
      "VaultUrl": "https://myKeyVault.vault.azure.net/"
    },
    "Configuration": {
      "UseEnvironmentVariables": true
    }
  }
}
```

**Fallback Order**:
1. Primary provider (e.g., AzureKeyVault)
2. Fallback provider (e.g., Configuration)
3. Error if both fail

## Key Rotation

### Automatic Key Rotation

Enable automatic key rotation monitoring:

```json
{
  "KeyManagement": {
    "EnableKeyRotation": true,
    "KeyRotationDays": 90,
    "AlertOnRotationDue": true,
    "RotationWarningDays": 7
  }
}
```

**Rotation Alerts**:
- Warning alert sent 7 days before rotation due
- Critical alert sent when rotation is overdue
- Alerts sent via configured alert channels (email, webhook, SMS)

### Manual Key Rotation

**Via API** (requires admin role):
```bash
# Rotate encryption key
POST /api/keymanagement/rotate-encryption-key

# Rotate signing key
POST /api/keymanagement/rotate-signing-key
```

**Via Code**:
```csharp
var keyManagement = serviceProvider.GetRequiredService<IKeyManagementService>();

// Rotate encryption key
var newEncryptionVersion = await keyManagement.RotateEncryptionKeyAsync();
Console.WriteLine($"New encryption key version: {newEncryptionVersion}");

// Rotate signing key
var newSigningVersion = await keyManagement.RotateSigningKeyAsync();
Console.WriteLine($"New signing key version: {newSigningVersion}");
```

### Key Rotation Best Practices

1. **Schedule Regular Rotations**: Rotate keys every 90 days minimum
2. **Backup Old Keys**: Keep old keys for decrypting historical data
3. **Test Rotation**: Test key rotation in staging before production
4. **Monitor Alerts**: Respond to rotation alerts promptly
5. **Document Rotation**: Log all key rotation events
6. **Re-encrypt Data**: Consider re-encrypting old data with new keys

## Key Caching

Keys are cached in memory for performance:

```json
{
  "KeyManagement": {
    "EnableCaching": true,
    "CacheDurationMinutes": 60
  }
}
```

**Cache Behavior**:
- Keys cached for 60 minutes by default
- Cache cleared on key rotation
- Cache cleared on application restart
- Reduces calls to external key providers

## Security Best Practices

### 1. Never Commit Keys to Source Control
- Use `.gitignore` to exclude key files
- Use environment variables or external key stores
- Scan repositories for accidentally committed keys

### 2. Use Strong Keys
- Minimum 32 bytes (256 bits)
- Generated using cryptographically secure random number generator
- Never use predictable or weak keys

### 3. Restrict Access
- Limit who can access keys
- Use role-based access control (RBAC)
- Audit key access regularly

### 4. Encrypt Keys at Rest
- Use DPAPI on Windows
- Use file permissions on Linux/Mac
- Use HSM-backed storage in production

### 5. Rotate Keys Regularly
- Rotate every 90 days minimum
- Rotate immediately if compromise suspected
- Keep old keys for decryption

### 6. Monitor Key Usage
- Enable audit logging
- Monitor for unusual access patterns
- Alert on failed key retrievals

### 7. Use Managed Identity
- Prefer managed identity over credentials
- Avoid storing credentials in configuration
- Use Azure Managed Identity or AWS IAM Roles

### 8. Implement Fallback
- Configure fallback provider
- Test fallback scenarios
- Monitor fallback usage

## Troubleshooting

### Issue: "Encryption key not configured"
**Solution**: 
- Check that `AuditEncryption:Key` is set in configuration or environment variable
- Verify key is not a placeholder value
- Check key provider configuration

### Issue: "Signing key must be at least 32 bytes"
**Solution**:
- Generate a new key using the provided scripts
- Ensure Base64 string is at least 44 characters
- Verify key is properly encoded

### Issue: "Failed to retrieve key from Azure Key Vault"
**Solution**:
- Verify Key Vault URL is correct
- Check managed identity has access to Key Vault
- Verify secrets exist in Key Vault
- Check network connectivity to Azure

### Issue: "Failed to retrieve key from AWS Secrets Manager"
**Solution**:
- Verify region is correct
- Check IAM role has required permissions
- Verify secrets exist in Secrets Manager
- Check network connectivity to AWS

### Issue: "Key rotation failed"
**Solution**:
- Check provider supports key rotation
- Verify write permissions to key store
- Check disk space (for LocalStorage provider)
- Review error logs for details

## Migration Guide

### Migrating from Configuration to Azure Key Vault

1. **Generate keys in Azure Key Vault** (see Azure Key Vault setup above)

2. **Update configuration**:
```json
{
  "KeyManagement": {
    "Provider": "AzureKeyVault",
    "FallbackProvider": "Configuration",
    "AzureKeyVault": {
      "VaultUrl": "https://myKeyVault.vault.azure.net/"
    }
  }
}
```

3. **Test in staging environment**

4. **Deploy to production**

5. **Remove fallback after verification**:
```json
{
  "KeyManagement": {
    "Provider": "AzureKeyVault",
    "FallbackProvider": "None"
  }
}
```

### Migrating from LocalStorage to AWS Secrets Manager

1. **Read existing keys from local storage**:
```bash
# Keys are in the Keys directory
cat Keys/encryption.key
cat Keys/signing.key
```

2. **Store keys in AWS Secrets Manager** (see AWS setup above)

3. **Update configuration** and deploy

4. **Verify keys work correctly**

5. **Delete local key files** after verification

## Monitoring and Alerts

### Key Rotation Monitoring

The system automatically monitors key rotation status:

```csharp
// Get key rotation metadata
var metadata = await keyManagement.GetKeyRotationMetadataAsync();

Console.WriteLine($"Encryption Key Version: {metadata.EncryptionKeyVersion}");
Console.WriteLine($"Last Rotated: {metadata.EncryptionKeyLastRotated}");
Console.WriteLine($"Next Rotation: {metadata.EncryptionKeyNextRotation}");
Console.WriteLine($"Rotation Overdue: {metadata.EncryptionKeyRotationOverdue}");
```

### Alert Configuration

Configure alerts for key rotation:

```json
{
  "KeyManagement": {
    "AlertOnRotationDue": true,
    "RotationWarningDays": 7
  },
  "Alerting": {
    "Email": {
      "Enabled": true,
      "DefaultRecipients": ["security@company.com"]
    }
  }
}
```

## API Endpoints

### Get Key Rotation Status
```
GET /api/keymanagement/rotation-status
```

Response:
```json
{
  "encryptionKeyVersion": "20240115120000",
  "signingKeyVersion": "20240115120000",
  "encryptionKeyLastRotated": "2024-01-15T12:00:00Z",
  "signingKeyLastRotated": "2024-01-15T12:00:00Z",
  "encryptionKeyNextRotation": "2024-04-15T12:00:00Z",
  "signingKeyNextRotation": "2024-04-15T12:00:00Z",
  "encryptionKeyRotationOverdue": false,
  "signingKeyRotationOverdue": false,
  "storageProvider": "AzureKeyVault"
}
```

### Rotate Encryption Key
```
POST /api/keymanagement/rotate-encryption-key
Authorization: Bearer <admin-token>
```

Response:
```json
{
  "newVersion": "20240115130000",
  "rotatedAt": "2024-01-15T13:00:00Z"
}
```

### Rotate Signing Key
```
POST /api/keymanagement/rotate-signing-key
Authorization: Bearer <admin-token>
```

### Validate Keys
```
GET /api/keymanagement/validate
```

Response:
```json
{
  "valid": true,
  "encryptionKeyValid": true,
  "signingKeyValid": true,
  "provider": "AzureKeyVault"
}
```

## Compliance Considerations

### GDPR
- Keys used to encrypt personal data must be securely managed
- Key access must be audited
- Keys must be rotated regularly
- Backup keys must be retained for data recovery

### SOX
- Financial data encryption keys must be protected
- Key access must be restricted and audited
- Key rotation must be documented
- Separation of duties for key management

### ISO 27001
- Cryptographic key management policy required
- Keys must be protected throughout lifecycle
- Key generation, storage, rotation, and destruction must be documented
- Regular key management audits required

## Summary

| Provider | Use Case | Pros | Cons | Setup Complexity |
|----------|----------|------|------|------------------|
| Configuration | Development | Simple, fast | Not secure | Low |
| LocalStorage | Development/Testing | Auto-generation, rotation | Single server | Low |
| Azure Key Vault | Production (Azure) | HSM-backed, HA, rotation | Cost, dependency | Medium |
| AWS Secrets Manager | Production (AWS) | Managed, HA, rotation | Cost, dependency | Medium |

**Recommended Setup**:
- **Development**: LocalStorage with auto-generation
- **Staging**: Azure Key Vault or AWS Secrets Manager (same as production)
- **Production**: Azure Key Vault or AWS Secrets Manager with managed identity

**Next Steps**:
1. Choose appropriate provider for your environment
2. Generate secure keys
3. Configure provider settings
4. Test key retrieval
5. Enable key rotation monitoring
6. Configure alerts
7. Document key management procedures
