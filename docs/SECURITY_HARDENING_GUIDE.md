# Security Hardening Guide

## Overview

This guide covers security hardening procedures for ThinkOnErp in production environments, including JWT configuration, encryption, data masking, network security, and RBAC.

---

## 1. JWT Authentication Hardening

### Strong Secret Key

```json
{
  "JwtSettings": {
    "SecretKey": "<minimum 64 characters, randomly generated>",
    "Issuer": "ThinkOnErpAPI",
    "Audience": "ThinkOnErpClient",
    "ExpiryInMinutes": 15,
    "RefreshTokenExpiryInDays": 1
  }
}
```

**Checklist:**
- [ ] Use a cryptographically random secret key (minimum 256 bits / 64 hex chars)
- [ ] Reduce `ExpiryInMinutes` to 15 (default is 60)
- [ ] Reduce `RefreshTokenExpiryInDays` to 1 for high-security environments
- [ ] Store secret key in environment variables or vault, NOT in `appsettings.json`
- [ ] Rotate JWT secret key periodically

### Force Logout

The system supports forcing logout of compromised sessions:
```http
PUT /api/users/{id}/force-logout
Authorization: Bearer {admin-token}
```

---

## 2. Audit Data Encryption

### Enable Encryption at Rest

```json
{
  "AuditEncryption": {
    "Enabled": true,
    "Algorithm": "AES-256-CBC",
    "KeyRotationDays": 90,
    "EncryptFields": ["REQUEST_PAYLOAD", "RESPONSE_PAYLOAD", "OLD_VALUE", "NEW_VALUE"]
  }
}
```

### Key Management

```json
{
  "KeyManagement": {
    "Provider": "FileSystem",
    "KeyStoragePath": "/secure/keys/",
    "AutoRotation": true,
    "RotationIntervalDays": 90,
    "RetainOldKeysDays": 365
  }
}
```

**Checklist:**
- [ ] Enable encryption for audit data at rest
- [ ] Store encryption keys on encrypted filesystem or hardware security module
- [ ] Enable automatic key rotation (90-day interval recommended)
- [ ] Restrict key storage directory permissions to application service account only
- [ ] Back up keys to a secure, separate location

---

## 3. Sensitive Data Masking

### Configuration

```json
{
  "AuditLogging": {
    "SensitiveFields": [
      "password", "token", "refreshToken", "creditCard", 
      "ssn", "socialSecurityNumber", "secretKey", "apiKey",
      "cvv", "pin", "accountNumber"
    ],
    "MaskingPattern": "***MASKED***",
    "MaxPayloadSize": 10240
  }
}
```

**Checklist:**
- [ ] Review and extend `SensitiveFields` list for your business domain
- [ ] Verify masking is working — check audit logs for masked values
- [ ] Set `MaxPayloadSize` to limit captured data (default 10KB)
- [ ] Never log full credit card numbers, SSNs, or authentication tokens

---

## 4. Audit Log Integrity

### Enable Cryptographic Signing

```json
{
  "AuditIntegrity": {
    "Enabled": true,
    "SigningAlgorithm": "HMAC-SHA256",
    "EnableHashChain": true,
    "VerificationSchedule": "0 0 * * *"
  }
}
```

**Checklist:**
- [ ] Enable hash chain for tamper detection
- [ ] Schedule daily integrity verification
- [ ] Store signing keys separate from encryption keys
- [ ] Set up alerts for integrity verification failures

---

## 5. Network Security

### HTTPS Configuration

- [ ] Enforce HTTPS for all endpoints (redirect HTTP to HTTPS)
- [ ] Use TLS 1.2+ only
- [ ] Configure HSTS headers
- [ ] Use strong cipher suites

### CORS Configuration

- [ ] Restrict allowed origins to known frontend domains
- [ ] Do not use wildcard (`*`) in production
- [ ] Limit allowed HTTP methods to those actually used

### Rate Limiting

The system tracks failed login attempts with Redis sliding window:
- Blocks after 5 failed attempts within 5 minutes
- Security threats are logged and alerted

---

## 6. Role-Based Access Control (RBAC)

### Authorization Policies

| Policy | Purpose | Who |
|---|---|---|
| `AdminOnly` | Administrative operations | Users with `isAdmin=true` claim |
| `MultiTenantAccess` | Data isolation by company/branch | All authenticated users |
| `AuditDataAccess` | Audit log access with self-access | Admin + self-access users |
| `AdminOnlyAuditDataAccess` | Restricted audit access | Admin only |

**Checklist:**
- [ ] Review all users with admin privileges — minimize count
- [ ] Implement principle of least privilege for screen permissions
- [ ] Use `SysUserScreenPermission` for user-specific overrides
- [ ] Audit permission changes via compliance reports
- [ ] Review segregation of duties via SOX report

---

## 7. Security Monitoring

### Enable All Detection

```json
{
  "SecurityMonitoring": {
    "Enabled": true,
    "EnableSqlInjectionDetection": true,
    "EnableXssDetection": true,
    "EnableBruteForceDetection": true,
    "EnableAnomalyDetection": true,
    "FailedLoginThreshold": 5,
    "FailedLoginWindowMinutes": 5,
    "ThreatAlertEnabled": true
  }
}
```

### Configure Alert Notifications

```json
{
  "Alerting": {
    "Enabled": true,
    "Channels": {
      "Email": { "Enabled": true },
      "Sms": { "Enabled": true },
      "Webhook": { "Enabled": true }
    },
    "SecurityThreatAlertDelaySeconds": 0,
    "RateLimitPerMinute": 10
  }
}
```

**Checklist:**
- [ ] Enable all security detection patterns
- [ ] Configure at least one alert notification channel
- [ ] Set security threat alert delay to 0 (immediate)
- [ ] Review security threats daily via `/api/monitoring/security/summary`
- [ ] Monitor failed logins via `/api/monitoring/security/failed-logins`

---

## 8. Database Security

- [ ] Use dedicated Oracle schema with minimum required privileges
- [ ] Do not use SYS or SYSTEM accounts for application connections
- [ ] Enable Oracle audit logging for DDL operations
- [ ] Restrict direct database access — all operations through stored procedures
- [ ] Encrypt Oracle connections using Oracle Native Network Encryption
- [ ] Regular password rotation for database service accounts

---

## 9. Production Deployment Checklist

- [ ] Remove default credentials from seed data
- [ ] Change SuperAdmin default password
- [ ] Set `ASPNETCORE_ENVIRONMENT=Production`
- [ ] Disable Swagger UI in production (or protect with authentication)
- [ ] Enable all security monitoring features
- [ ] Configure external key storage (not filesystem)
- [ ] Set up log aggregation and monitoring dashboards
- [ ] Enable audit data encryption
- [ ] Test disaster recovery procedures
- [ ] Document incident response procedures
