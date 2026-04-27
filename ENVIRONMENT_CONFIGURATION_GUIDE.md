# Environment-Specific Configuration Guide

This document explains the environment-specific configuration files for the Full Traceability System and how they differ from the base `appsettings.json`.

## Overview

The ThinkOnErp API uses three configuration files:

1. **appsettings.json** - Base configuration with default values
2. **appsettings.Development.json** - Development environment overrides
3. **appsettings.Production.json** - Production environment overrides

ASP.NET Core automatically merges these files based on the `ASPNETCORE_ENVIRONMENT` variable, with environment-specific settings overriding base settings.

---

## Development Environment (appsettings.Development.json)

### Purpose
Optimized for local development with verbose logging, faster feedback, and disabled security features for easier debugging.

### Key Differences from Base Configuration

#### 1. Audit Logging
- **Reduced Batch Size**: 25 (vs 50) - Faster feedback during development
- **Longer Batch Window**: 200ms (vs 100ms) - Less aggressive batching
- **Smaller Queue**: 5000 (vs 10000) - Lower memory usage
- **Encryption Disabled**: `EncryptSensitiveData: false` - Easier debugging
- **File System Fallback Enabled**: Local `AuditFallback` directory

#### 2. Audit Integrity
- **Completely Disabled**: No cryptographic signing in development
- **No Hash Generation**: `AutoGenerateHashes: false`
- **No Verification**: `VerifyOnRead: false`
- **No Tampering Alerts**: `AlertOnTampering: false`

**Rationale**: Simplifies debugging and allows easier data inspection without cryptographic overhead.

#### 3. Request Tracing
- **Full Payload Logging**: `PayloadLoggingLevel: Full`
- **Request Start Logging**: `LogRequestStart: true` - See when requests begin
- **Include All Headers**: `IncludeHeaders: true`
- **Log Response Bodies**: `LogResponseBody: true`

**Rationale**: Maximum visibility for debugging API issues.

#### 4. Performance Monitoring
- **Lower Thresholds**: 
  - Slow Request: 500ms (vs 1000ms)
  - Slow Query: 250ms (vs 500ms)
- **Shorter Retention**: 12 hours (vs 24 hours)
- **More Logging**: 200 records/hour (vs 100)
- **Query Parameters Logged**: `LogQueryParameters: true`

**Rationale**: Catch performance issues early with more aggressive thresholds.

#### 5. Compliance Reporting
- **Reduced Cache**: 5 minutes (vs 30 minutes)
- **Scheduled Reports Disabled**: `ScheduledReports.Enabled: false`

**Rationale**: Fresh data for testing, no automated report generation.

#### 6. Archival Service
- **Completely Disabled**: `Enabled: false`
- **Shorter Retention**: 7-180 days (vs 30-2555 days)
- **No Compression**: `CompressionEnabled: false`
- **No External Storage**: `ExternalStorage.Enabled: false`

**Rationale**: No need for archival in development; keeps all data accessible.

#### 7. Security Monitoring
- **Relaxed Thresholds**:
  - Failed Login: 10 attempts (vs 5)
  - Time Window: 10 minutes (vs 5)
  - Activity Threshold: 2000 req/hour (vs 1000)
- **Anomaly Detection Disabled**: `EnableAnomalousActivityDetection: false`
- **Geographic Detection Disabled**: `EnableGeographicAnomalyDetection: false`
- **No Auto-Blocking**: `AutoBlockSuspiciousIps: false`
- **No Alerts**: Email and webhook alerts disabled
- **Verbose Logging**: `EnableVerboseLogging: true`
- **No Redis**: `UseRedisCache: false`

**Rationale**: Avoid false positives during testing; use console logging instead.

#### 8. Alerting System
- **Completely Disabled**: `Enabled: false`
- **All Channels Off**: Email, Webhook, SMS disabled
- **All Rules Disabled**: No alert rules active

**Rationale**: Prevent alert spam during development.

#### 9. Logging (Serilog)
- **Debug Level**: `MinimumLevel.Default: Debug`
- **More Verbose Framework Logs**: `Microsoft: Information`
- **EF Core Logging**: `Microsoft.EntityFrameworkCore: Information`
- **Shorter Retention**: 7 days (vs 30 days)
- **Dev Log Files**: `dev-log-.txt` prefix
- **Correlation ID in Console**: Visible in console output

**Rationale**: Maximum visibility for debugging.

#### 10. Query Service
- **Smaller Pages**: Default 25 (vs 50), Max 500 (vs 1000)
- **Shorter Date Range**: 90 days (vs 365 days)
- **Longer Timeout**: 60 seconds (vs 30 seconds) - Allow debugging
- **No Caching**: `AuditQueryCaching.Enabled: false`
- **No Parallel Queries**: `EnableParallelQueries: false`

**Rationale**: Simpler queries for debugging, no caching for fresh data.

#### 11. Health Checks
- **Redis Disabled**: `Redis.Enabled: false`
- **External Storage Disabled**: `ExternalStorage.Enabled: false`

**Rationale**: Only check services actually used in development.

---

## Production Environment (appsettings.Production.json)

### Purpose
Optimized for security, performance, reliability, and compliance in production deployments.

### Key Differences from Base Configuration

#### 1. Connection Strings
- **Placeholders for Production Values**: All connection strings must be replaced
- **Redis Required**: Production Redis connection string needed

**Action Required**: Replace all `REPLACE_WITH_*` placeholders with actual production values.

#### 2. JWT Settings
- **Production Secret Key**: Must be replaced with secure key
- **Shorter Token Expiry**: 30 minutes (vs 60 minutes)

**Rationale**: Reduced token lifetime improves security.

#### 3. Audit Logging
- **Increased Batch Size**: 100 (vs 50) - Better throughput
- **Shorter Batch Window**: 50ms (vs 100ms) - Lower latency
- **Larger Queue**: 20000 (vs 10000) - Handle traffic spikes
- **Stricter Circuit Breaker**: 3 failures (vs 5), 30s timeout (vs 60s)
- **Encryption Enabled**: `EncryptSensitiveData: true`
- **Production Fallback Path**: `/var/log/thinkonerp/audit-fallback`

**Rationale**: Optimized for high throughput with security enabled.

#### 4. Audit Integrity
- **Fully Enabled**: All integrity features active
- **Production Signing Key**: Must be replaced with secure key
- **Verification on Read**: `VerifyOnRead: true`
- **Tampering Alerts**: `AlertOnTampering: true`

**Action Required**: Generate and configure production signing key.

#### 5. Request Tracing
- **Metadata Only**: `PayloadLoggingLevel: Metadata` - Reduced storage
- **Smaller Payloads**: 5KB (vs 10KB)
- **No Request Start**: `LogRequestStart: false`
- **No Headers**: `IncludeHeaders: false` - Privacy
- **No Response Bodies**: `LogResponseBody: false` - Reduced storage

**Rationale**: Balance between traceability and storage/privacy.

#### 6. Performance Monitoring
- **Standard Thresholds**: 1000ms requests, 500ms queries
- **Longer Retention**: 24 hours detailed, 365 days aggregated
- **Larger Batches**: 2000 (vs 1000)
- **Less Logging**: 50 records/hour (vs 100)
- **No Query Parameters**: `LogQueryParameters: false` - Privacy

**Rationale**: Production-appropriate thresholds with privacy protection.

#### 7. Compliance Reporting
- **Longer Cache**: 60 minutes (vs 30 minutes)
- **Larger Reports**: 100MB (vs 50MB)
- **Scheduled Reports Enabled**: All reports active
- **SOX Approval Required**: `RequireApprovalWorkflow: true`

**Action Required**: Replace email placeholders with actual recipients.

#### 8. Archival Service
- **Fully Enabled**: All archival features active
- **Larger Batches**: 2000 (vs 1000)
- **Longer Timeout**: 120 minutes (vs 60 minutes)
- **Compression Enabled**: GZip compression
- **Encryption Enabled**: `EncryptArchivedData: true`
- **External Storage Enabled**: S3 or Azure Blob Storage
- **Full Retention Periods**: Compliance-appropriate retention
- **Cleanup Enabled**: 10-year archive retention

**Action Required**: Configure S3 or Azure storage credentials.

#### 9. Security Monitoring
- **Strict Thresholds**: 5 failed logins in 5 minutes
- **All Detection Enabled**: SQL injection, XSS, unauthorized access, anomalies, geographic
- **Auto-Blocking Enabled**: `AutoBlockSuspiciousIps: true` - 2 hour blocks
- **Alerts Enabled**: Email and webhook alerts
- **Longer Retention**: 30 days failed logins, 730 days threats
- **Redis Caching**: `UseRedisCache: true`
- **No Verbose Logging**: `EnableVerboseLogging: false`

**Action Required**: Configure alert email and webhook URL.

#### 10. Query Caching
- **Fully Enabled**: Redis caching active
- **Longer Cache**: 10 minutes (vs 5 minutes)
- **Larger Cache**: 2MB (vs 1MB)
- **Common Queries**: 30-minute cache

**Rationale**: Improved query performance with caching.

#### 11. Alerting System
- **Fully Enabled**: All alerting features active
- **Larger Queue**: 2000 (vs 1000)
- **More Concurrent**: 10 alerts (vs 5)
- **Faster Processing**: 5-second interval (vs 10 seconds)
- **Email Enabled**: SMTP configuration required
- **Webhook Enabled**: Webhook URL required
- **SMS Enabled**: Twilio configuration required
- **All Rules Active**: Critical exceptions, security threats, performance, failure rate
- **Enhanced Channels**: SMS added for critical alerts

**Action Required**: Configure SMTP, webhook, and Twilio credentials.

#### 12. Logging (Serilog)
- **Information Level**: `MinimumLevel.Default: Information`
- **Minimal Framework Logs**: `Microsoft: Warning`
- **Production Log Path**: `/var/log/thinkonerp/log-.txt`
- **Longer Retention**: 90 days (vs 30 days)
- **File Size Limits**: 100MB per file with rollover
- **Simplified Console**: No properties in console output

**Rationale**: Production-appropriate logging with size management.

#### 13. Encryption
- **Fully Enabled**: All encryption features active
- **All Fields Encrypted**: Old/new values, request/response payloads
- **Key Rotation Enabled**: 90-day rotation

**Action Required**: Generate and configure production encryption key.

#### 14. Query Service
- **Full Capabilities**: Max 1000 records, 365-day range
- **Standard Timeout**: 30 seconds
- **Full-Text Search Enabled**: `EnableFullTextSearch: true`
- **Parallel Queries Enabled**: `EnableParallelQueries: true`
- **Large Exports**: 100,000 records

**Rationale**: Full query capabilities for production use.

#### 15. Health Checks
- **All Enabled**: Database, Redis, External Storage
- **Comprehensive Monitoring**: All components checked

**Rationale**: Complete health monitoring for production.

#### 16. Allowed Hosts
- **Domain Restriction**: Must specify production domain

**Action Required**: Replace with actual production domain.

---

## Configuration Checklist

### Before Deploying to Production

#### Required Replacements
- [ ] `ConnectionStrings.OracleDb` - Production database connection
- [ ] `ConnectionStrings.Redis` - Production Redis connection
- [ ] `JwtSettings.SecretKey` - Secure JWT secret (min 32 chars)
- [ ] `AuditIntegrity.SigningKey` - Base64 signing key (generate using RandomNumberGenerator)
- [ ] `AuditEncryption.Key` - Base64 AES-256 key (32 bytes)
- [ ] `Archival.EncryptionKeyId` - Encryption key identifier
- [ ] `Archival.ExternalStorage.S3.*` - AWS S3 credentials and bucket
- [ ] `Archival.ExternalStorage.Azure.*` - Azure Blob Storage credentials
- [ ] `SecurityMonitoring.AlertEmailRecipients` - Security team email
- [ ] `SecurityMonitoring.AlertWebhookUrl` - Security webhook URL
- [ ] `SecurityMonitoring.RedisConnectionString` - Redis connection
- [ ] `AuditQueryCaching.RedisConnectionString` - Redis connection
- [ ] `ComplianceReporting.ScheduledReports[].Recipients` - Report recipients
- [ ] `Alerting.Email.*` - SMTP configuration
- [ ] `Alerting.Webhook.*` - Webhook configuration
- [ ] `Alerting.Sms.*` - Twilio configuration
- [ ] `AllowedHosts` - Production domain

#### Key Generation Commands

**Generate Signing Key (C#)**:
```csharp
using System.Security.Cryptography;
var key = new byte[32];
RandomNumberGenerator.Fill(key);
var base64Key = Convert.ToBase64String(key);
Console.WriteLine(base64Key);
```

**Generate Signing Key (PowerShell)**:
```powershell
$bytes = New-Object byte[] 32
[Security.Cryptography.RandomNumberGenerator]::Create().GetBytes($bytes)
[Convert]::ToBase64String($bytes)
```

**Generate Signing Key (Bash)**:
```bash
openssl rand -base64 32
```

#### Security Best Practices
1. **Never commit production credentials** to source control
2. **Use Azure Key Vault or AWS Secrets Manager** for sensitive configuration
3. **Rotate keys every 90 days** (encryption key rotation enabled)
4. **Use app-specific passwords** for SMTP
5. **Enable MFA** for all service accounts
6. **Restrict network access** to production databases
7. **Use TLS/SSL** for all external connections
8. **Monitor key usage** and audit access logs

---

## Environment Variable Overrides

You can override any configuration setting using environment variables with the format:

```
SECTION__SUBSECTION__SETTING=value
```

### Examples

**Override Database Connection**:
```bash
export ConnectionStrings__OracleDb="Data Source=..."
```

**Override JWT Secret**:
```bash
export JwtSettings__SecretKey="your-secret-key"
```

**Override Audit Batch Size**:
```bash
export AuditLogging__BatchSize=200
```

**Override Redis Connection**:
```bash
export ConnectionStrings__Redis="production-redis:6379"
```

This is useful for:
- Docker deployments
- Kubernetes ConfigMaps/Secrets
- CI/CD pipelines
- Cloud platform configuration

---

## Docker Deployment

### Development
```dockerfile
ENV ASPNETCORE_ENVIRONMENT=Development
```

### Production
```dockerfile
ENV ASPNETCORE_ENVIRONMENT=Production
ENV ConnectionStrings__OracleDb="..."
ENV JwtSettings__SecretKey="..."
# ... other production settings
```

---

## Kubernetes Deployment

### ConfigMap for Non-Sensitive Settings
```yaml
apiVersion: v1
kind: ConfigMap
metadata:
  name: thinkonerp-config
data:
  ASPNETCORE_ENVIRONMENT: "Production"
  AuditLogging__BatchSize: "100"
  PerformanceMonitoring__Enabled: "true"
```

### Secret for Sensitive Settings
```yaml
apiVersion: v1
kind: Secret
metadata:
  name: thinkonerp-secrets
type: Opaque
stringData:
  ConnectionStrings__OracleDb: "..."
  JwtSettings__SecretKey: "..."
  AuditIntegrity__SigningKey: "..."
  AuditEncryption__Key: "..."
```

---

## Verification

### Development Environment
```bash
# Set environment
export ASPNETCORE_ENVIRONMENT=Development

# Run application
dotnet run --project src/ThinkOnErp.API

# Verify settings
curl http://localhost:5000/api/health
```

### Production Environment
```bash
# Set environment
export ASPNETCORE_ENVIRONMENT=Production

# Verify configuration (before deployment)
dotnet run --project src/ThinkOnErp.API --no-launch-profile

# Check for REPLACE_WITH_* placeholders
grep -r "REPLACE_WITH_" src/ThinkOnErp.API/appsettings.Production.json
```

---

## Troubleshooting

### Configuration Not Loading
1. Check `ASPNETCORE_ENVIRONMENT` variable is set correctly
2. Verify file names match exactly (case-sensitive on Linux)
3. Check JSON syntax is valid
4. Review application startup logs

### Settings Not Overriding
1. Verify environment-specific file exists
2. Check setting path matches exactly (case-sensitive)
3. Ensure environment variable format is correct
4. Review configuration merge order

### Missing Required Settings
1. Check for `REPLACE_WITH_*` placeholders
2. Verify all required keys are generated
3. Ensure external services (Redis, S3) are accessible
4. Review health check endpoint for failures

---

## Migration from Base Configuration

If you have an existing `appsettings.json` with custom settings:

1. **Backup Current Configuration**
   ```bash
   cp src/ThinkOnErp.API/appsettings.json src/ThinkOnErp.API/appsettings.json.backup
   ```

2. **Identify Custom Settings**
   - Compare your current file with the new base configuration
   - Note any custom values or new sections

3. **Apply to Environment Files**
   - Add development-specific customizations to `appsettings.Development.json`
   - Add production-specific customizations to `appsettings.Production.json`

4. **Test in Development**
   ```bash
   export ASPNETCORE_ENVIRONMENT=Development
   dotnet run --project src/ThinkOnErp.API
   ```

5. **Validate Production Configuration**
   - Review all `REPLACE_WITH_*` placeholders
   - Generate required keys
   - Test in staging environment first

---

## Summary

### Development Environment
- **Focus**: Debugging and fast feedback
- **Security**: Minimal (encryption/signing disabled)
- **Logging**: Verbose (Debug level)
- **Performance**: Relaxed thresholds
- **Alerts**: Disabled
- **Archival**: Disabled
- **Caching**: Disabled

### Production Environment
- **Focus**: Security, performance, compliance
- **Security**: Maximum (all features enabled)
- **Logging**: Appropriate (Information level)
- **Performance**: Optimized thresholds
- **Alerts**: Fully configured
- **Archival**: Enabled with external storage
- **Caching**: Enabled with Redis

---

**Last Updated**: 2024
**Version**: 1.0
