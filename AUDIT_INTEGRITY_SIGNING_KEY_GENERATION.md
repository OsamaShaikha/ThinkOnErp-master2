# Audit Integrity Signing Key Generation Guide

## Overview

The Audit Integrity feature uses cryptographic signatures (HMAC-SHA256 or HMAC-SHA512) to ensure audit log entries are tamper-evident. Each audit log entry is signed with a secret key, and the signature is stored in the metadata field. This allows verification that audit logs have not been modified after creation.

## Generating a Signing Key

The signing key must be a Base64-encoded string representing at least 32 bytes (256 bits) of random data for security.

### Method 1: Using C# Code

```csharp
using System.Security.Cryptography;

// Generate 32 bytes of cryptographically secure random data
byte[] keyBytes = RandomNumberGenerator.GetBytes(32);

// Convert to Base64 string
string signingKey = Convert.ToBase64String(keyBytes);

Console.WriteLine($"Signing Key: {signingKey}");
```

### Method 2: Using PowerShell

```powershell
# Generate 32 random bytes and convert to Base64
$bytes = New-Object byte[] 32
$rng = [System.Security.Cryptography.RandomNumberGenerator]::Create()
$rng.GetBytes($bytes)
$signingKey = [Convert]::ToBase64String($bytes)
Write-Host "Signing Key: $signingKey"
```

### Method 3: Using .NET CLI

Create a simple console app:

```bash
dotnet new console -n KeyGenerator
cd KeyGenerator
```

Edit `Program.cs`:

```csharp
using System.Security.Cryptography;

var keyBytes = RandomNumberGenerator.GetBytes(32);
var signingKey = Convert.ToBase64String(keyBytes);
Console.WriteLine($"Signing Key: {signingKey}");
```

Run it:

```bash
dotnet run
```

### Method 4: Using OpenSSL (Linux/Mac)

```bash
openssl rand -base64 32
```

## Configuration

Once you have generated a signing key, add it to your configuration:

### Option 1: appsettings.json

```json
{
  "AuditIntegrity": {
    "Enabled": true,
    "SigningKey": "YOUR_GENERATED_BASE64_KEY_HERE",
    "AutoGenerateHashes": true,
    "VerifyOnRead": false,
    "LogIntegrityOperations": false,
    "BatchSize": 100,
    "VerificationTimeoutMs": 10000,
    "AlertOnTampering": true,
    "HashAlgorithm": "HMACSHA256"
  }
}
```

### Option 2: Environment Variables

```bash
AUDIT_INTEGRITY_ENABLED=true
AUDIT_INTEGRITY_SIGNING_KEY=YOUR_GENERATED_BASE64_KEY_HERE
AUDIT_INTEGRITY_AUTO_GENERATE_HASHES=true
AUDIT_INTEGRITY_HASH_ALGORITHM=HMACSHA256
```

## Security Considerations

1. **Keep the Key Secret**: The signing key should be treated as a secret. Never commit it to source control.

2. **Key Length**: Use at least 32 bytes (256 bits) for HMACSHA256 or 64 bytes (512 bits) for HMACSHA512.

3. **Key Rotation**: Plan for key rotation:
   - Generate a new key
   - Update configuration with the new key
   - Old audit logs will still have signatures from the old key
   - Consider storing multiple keys with key IDs for verification

4. **Secure Storage**: Store the key in:
   - Azure Key Vault
   - AWS Secrets Manager
   - Environment variables (for development)
   - Encrypted configuration files

## How It Works

### Signature Generation

When an audit log entry is created:

1. The system generates a canonical representation of the entry:
   ```
   rowId|actorId|action|entityType|entityId|creationDate|oldValue|newValue
   ```

2. HMAC-SHA256 is computed over this canonical string using the signing key

3. The signature is Base64-encoded and stored in the `Metadata` field as JSON:
   ```json
   {
     "integrity_hash": "BASE64_SIGNATURE_HERE"
   }
   ```

### Signature Verification

To verify an audit log entry:

1. Retrieve the entry from the database
2. Extract the stored signature from the metadata
3. Recompute the signature using the same canonical representation
4. Compare the stored signature with the computed signature
5. If they match, the entry is valid; if not, tampering is detected

## API Endpoints for Verification

### Verify Single Entry

```http
GET /api/monitoring/audit-integrity/{auditLogId}
```

### Verify Batch

```http
POST /api/monitoring/audit-integrity/verify-batch
Content-Type: application/json

{
  "auditLogIds": [1, 2, 3, 4, 5]
}
```

### Detect Tampering in Date Range

```http
POST /api/monitoring/audit-integrity/detect-tampering
Content-Type: application/json

{
  "startDate": "2024-01-01T00:00:00Z",
  "endDate": "2024-12-31T23:59:59Z"
}
```

## Troubleshooting

### Error: "Signing key not configured"

**Solution**: Add the `AuditIntegrity:SigningKey` configuration value.

### Error: "Signing key must be at least 32 bytes"

**Solution**: Generate a new key using one of the methods above. The Base64 string should be at least 44 characters long (32 bytes encoded).

### Error: "Signing key is not a valid Base64 string"

**Solution**: Ensure the key is properly Base64-encoded. Use one of the generation methods above.

## Example: Complete Setup

1. Generate a key:
   ```bash
   openssl rand -base64 32
   ```
   Output: `xK9mP2vR8tY5wZ3nB7cF1dH4jL6qS0uA9eG2iM5oN8p=`

2. Add to appsettings.json:
   ```json
   {
     "AuditIntegrity": {
       "Enabled": true,
       "SigningKey": "xK9mP2vR8tY5wZ3nB7cF1dH4jL6qS0uA9eG2iM5oN8p=",
       "AutoGenerateHashes": true,
       "AlertOnTampering": true,
       "HashAlgorithm": "HMACSHA256"
     }
   }
   ```

3. Restart the application

4. Verify it's working:
   - Create an audit log entry (e.g., login, create a record)
   - Check the database: `SELECT METADATA FROM SYS_AUDIT_LOG WHERE ROW_ID = <latest_id>`
   - You should see JSON with an `integrity_hash` field

## Performance Impact

- **Signature Generation**: ~0.1-0.5ms per entry (negligible)
- **Batch Signature Generation**: Optimized for batch inserts
- **Signature Verification**: ~0.1-0.5ms per entry
- **Storage Overhead**: ~50-100 bytes per entry (Base64 signature in metadata)

## Compliance Benefits

- **SOX**: Demonstrates tamper-evident audit trails for financial data
- **GDPR**: Ensures personal data access logs cannot be modified
- **ISO 27001**: Provides cryptographic integrity verification for security events
- **HIPAA**: Ensures healthcare audit logs are tamper-evident

## References

- [HMAC-SHA256 Specification (RFC 2104)](https://tools.ietf.org/html/rfc2104)
- [NIST Guidelines on Cryptographic Key Management](https://csrc.nist.gov/publications/detail/sp/800-57-part-1/rev-5/final)
- [OWASP Cryptographic Storage Cheat Sheet](https://cheatsheetseries.owasp.org/cheatsheets/Cryptographic_Storage_Cheat_Sheet.html)
