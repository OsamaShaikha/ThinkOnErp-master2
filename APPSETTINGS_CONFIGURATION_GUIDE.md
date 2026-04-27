# Full Traceability System - Configuration Guide

This document provides a comprehensive guide to configuring the Full Traceability System through `appsettings.json`.

## Table of Contents

1. [Connection Strings](#connection-strings)
2. [JWT Settings](#jwt-settings)
3. [Audit Logging](#audit-logging)
4. [Audit Integrity](#audit-integrity)
5. [Request Tracing](#request-tracing)
6. [Performance Monitoring](#performance-monitoring)
7. [Security Monitoring](#security-monitoring)
8. [Compliance Reporting](#compliance-reporting)
9. [Archival Service](#archival-service)
10. [Alert Manager](#alert-manager)
11. [Audit Query Service](#audit-query-service)
12. [Legacy Audit Service](#legacy-audit-service)
13. [Serilog Configuration](#serilog-configuration)
14. [Health Checks](#health-checks)

---

## Connection Strings

### OracleDb
Primary database connection string with optimized connection pooling settings.

**Key Settings:**
- `Min Pool Size`: 5 - Minimum connections maintained in pool
- `Max Pool Size`: 100 - Maximum connections allowed
- `Connection Timeout`: 15 seconds
- `Connection Lifetime`: 300 seconds (5 minutes)
- `Statement Cache Size`: 50 - Cached prepared statements for performance

### Redis
Optional Redis connection string for caching and distributed locking.

**Format:** `host:port,abortConnect=false,connectTimeout=5000,syncTimeout=5000`

---

## JWT Settings

Configuration for JSON Web Token authentication.

| Setting | Description | Default |
|---------|-------------|---------|
| `SecretKey` | Secret key for signing JWTs (min 32 characters) | Required |
| `Issuer` | Token issuer identifier | ThinkOnErpAPI |
| `Audience` | Token audience identifier | ThinkOnErpClient |
| `ExpiryInMinutes` | Access token expiration time | 60 |
| `RefreshTokenExpiryInDays` | Refresh token expiration time | 7 |

---

## Audit Logging

Core audit logging configuration for capturing all system events.

### Batch Processing

| Setting | Description | Default |
|---------|-------------|---------|
| `Enabled` | Enable/disable audit logging | true |
| `BatchSize` | Number of events to batch before writing | 50 |
| `BatchWindowMs` | Maximum time to wait before writing batch | 100ms |
| `MaxQueueSize` | Maximum events in memory queue | 10000 |

### Sensitive Data Masking

| Setting | Description |
|---------|-------------|
| `SensitiveFields` | Array of field names to mask (password, token, creditCard, etc.) |
| `MaskingPattern` | Pattern to replace sensitive data | `***MASKED***` |
| `MaxPayloadSize` | Maximum payload size to log (bytes) | 10240 (10KB) |

### Resilience Settings

| Setting | Description | Default |
|---------|-------------|---------|
| `EnableCircuitBreaker` | Enable circuit breaker for database failures | true |
| `CircuitBreakerFailureThreshold` | Failures before opening circuit | 5 |
| `CircuitBreakerTimeoutSeconds` | Time before attempting to close circuit | 60 |
| `EnableRetryPolicy` | Enable retry on transient failures | true |
| `MaxRetryAttempts` | Maximum retry attempts | 3 |
| `InitialRetryDelayMs` | Initial delay between retries | 100ms |
| `MaxRetryDelayMs` | Maximum delay between retries | 5000ms |
| `UseRetryJitter` | Add randomness to retry delays | true |

### Fallback Options

| Setting | Description | Default |
|---------|-------------|---------|
| `EnableFileSystemFallback` | Write to file system if database unavailable | true |
| `FallbackDirectory` | Directory for fallback audit files | AuditFallback |

---

## Audit Integrity

Cryptographic signing and tamper detection for audit logs.

| Setting | Description | Default |
|---------|-------------|---------|
| `Enabled` | Enable integrity checking | true |
| `SigningKey` | Base64-encoded signing key (generate using RandomNumberGenerator) | Required |
| `AutoGenerateHashes` | Automatically generate hashes on write | true |
| `VerifyOnRead` | Verify hashes when reading audit logs | false |
| `AlertOnTampering` | Send alert if tampering detected | true |
| `HashAlgorithm` | Algorithm for hashing | HMACSHA256 |

**Generate Signing Key:**
```csharp
using System.Security.Cryptography;
var key = new byte[32];
RandomNumberGenerator.Fill(key);
var base64Key = Convert.ToBase64String(key);
```

---

## Request Tracing

HTTP request/response tracking with correlation IDs.

### Basic Settings

| Setting | Description | Default |
|---------|-------------|---------|
| `Enabled` | Enable request tracing | true |
| `LogPayloads` | Log request/response bodies | true |
| `PayloadLoggingLevel` | Logging level: None, Metadata, Full | Full |
| `MaxPayloadSize` | Maximum payload size to log (bytes) | 10240 |
| `CorrelationIdHeader` | HTTP header for correlation ID | X-Correlation-ID |

### Exclusions

| Setting | Description |
|---------|-------------|
| `ExcludedPaths` | Paths to exclude from tracing (health checks, metrics) |
| `ExcludedHeaders` | Headers to exclude from logging (Authorization, Cookie) |

### Advanced Options

| Setting | Description | Default |
|---------|-------------|---------|
| `PopulateLegacyFields` | Populate legacy audit log fields | true |
| `LogRequestStart` | Log when request starts | false |
| `LogRequestEnd` | Log when request completes | true |
| `IncludeHeaders` | Include HTTP headers in logs | true |
| `IncludeQueryString` | Include query string in logs | true |
| `TruncateLargePayloads` | Truncate payloads exceeding MaxPayloadSize | true |
| `LogResponseBody` | Log response body | true |

---

## Performance Monitoring

System performance tracking and metrics collection.

### Thresholds

| Setting | Description | Default |
|---------|-------------|---------|
| `Enabled` | Enable performance monitoring | true |
| `SlowRequestThresholdMs` | Threshold for slow request detection | 1000ms |
| `SlowQueryThresholdMs` | Threshold for slow query detection | 500ms |

### Metrics Collection

| Setting | Description | Default |
|---------|-------------|---------|
| `TrackMemoryMetrics` | Track memory usage | true |
| `TrackDatabaseMetrics` | Track database performance | true |
| `TrackCpuMetrics` | Track CPU usage | true |
| `MetricsRetentionHours` | Hours to retain detailed metrics | 24 |
| `AggregateMetricsHourly` | Aggregate metrics hourly | true |
| `EnablePercentileCalculations` | Calculate p50, p95, p99 | true |

### Metrics Aggregation

| Setting | Description | Default |
|---------|-------------|---------|
| `IntervalMinutes` | Aggregation interval | 60 |
| `BatchSize` | Metrics to aggregate per batch | 1000 |
| `RetentionDays` | Days to retain aggregated metrics | 90 |

---

## Security Monitoring

Threat detection and security event monitoring.

### Threat Detection

| Setting | Description | Default |
|---------|-------------|---------|
| `Enabled` | Enable security monitoring | true |
| `FailedLoginThreshold` | Failed logins before flagging | 5 |
| `FailedLoginWindowMinutes` | Time window for failed login detection | 5 |
| `AnomalousActivityThreshold` | Requests per hour threshold | 1000 |
| `AnomalousActivityWindowHours` | Time window for anomaly detection | 1 |

### Rate Limiting

| Setting | Description | Default |
|---------|-------------|---------|
| `RateLimitPerIp` | Max requests per IP per minute | 100 |
| `RateLimitPerUser` | Max requests per user per minute | 200 |

### Detection Features

| Setting | Description | Default |
|---------|-------------|---------|
| `EnableSqlInjectionDetection` | Detect SQL injection attempts | true |
| `EnableXssDetection` | Detect XSS attempts | true |
| `EnableUnauthorizedAccessDetection` | Detect unauthorized access | true |
| `EnableAnomalousActivityDetection` | Detect unusual activity patterns | true |
| `EnableGeographicAnomalyDetection` | Detect geographic anomalies | false |

### Response Actions

| Setting | Description | Default |
|---------|-------------|---------|
| `AutoBlockSuspiciousIps` | Automatically block suspicious IPs | false |
| `IpBlockDurationMinutes` | Duration to block IPs | 60 |
| `SendEmailAlerts` | Send email alerts for threats | true |
| `SendWebhookAlerts` | Send webhook alerts for threats | false |
| `MinimumAlertSeverity` | Minimum severity to alert: Low, Medium, High, Critical | High |

---

## Compliance Reporting

Automated compliance report generation for GDPR, SOX, and ISO 27001.

### General Settings

| Setting | Description | Default |
|---------|-------------|---------|
| `Enabled` | Enable compliance reporting | true |
| `ReportCacheDurationMinutes` | Cache duration for generated reports | 30 |
| `MaxReportSizeMB` | Maximum report size | 50 |
| `EnablePdfGeneration` | Enable PDF report generation | true |
| `EnableCsvExport` | Enable CSV export | true |
| `EnableJsonExport` | Enable JSON export | true |

### Scheduled Reports

Configure automated report generation with cron schedules.

**Example Report Configuration:**
```json
{
  "Name": "Daily Security Summary",
  "Type": "SecuritySummary",
  "Schedule": "0 8 * * *",
  "Format": "PDF",
  "Recipients": ["security@thinkonerp.com"],
  "Enabled": false
}
```

**Cron Schedule Format:** `minute hour day month dayOfWeek`
- `0 8 * * *` - Daily at 8:00 AM
- `0 9 * * 1` - Weekly on Monday at 9:00 AM
- `0 10 1 * *` - Monthly on 1st at 10:00 AM

### Compliance Standards

| Standard | Setting | Default Retention |
|----------|---------|-------------------|
| GDPR | `GdprReporting.Enabled` | 3 years |
| SOX | `SoxReporting.Enabled` | 7 years |
| ISO 27001 | `Iso27001Reporting.Enabled` | 2 years |

---

## Archival Service

Automated data archival based on retention policies.

### Basic Settings

| Setting | Description | Default |
|---------|-------------|---------|
| `Enabled` | Enable archival service | true |
| `Schedule` | Cron schedule for archival | 0 2 * * * (2 AM daily) |
| `BatchSize` | Records to archive per batch | 1000 |
| `TransactionTimeoutSeconds` | Timeout for archival transactions | 30 |
| `TimeoutMinutes` | Overall archival timeout | 60 |
| `TimeZone` | Timezone for scheduling | UTC |

### Compression

| Setting | Description | Default |
|---------|-------------|---------|
| `CompressionEnabled` | Enable compression | true |
| `CompressionAlgorithm` | Algorithm: GZip, Deflate | GZip |
| `CompressionLevel` | Level: Optimal, Fastest, NoCompression | Optimal |

### Retention Policies

Retention periods in days for different event categories:

| Category | Default Days | Compliance |
|----------|--------------|------------|
| Authentication | 365 | 1 year |
| DataChange | 1095 | 3 years |
| Financial | 2555 | 7 years (SOX) |
| PersonalData | 1095 | 3 years (GDPR) |
| Security | 730 | 2 years |
| Configuration | 1825 | 5 years |
| Exception | 365 | 1 year |
| Request | 90 | 90 days |

### External Storage

Configure external storage for archived data (S3 or Azure Blob Storage).

**S3 Configuration:**
```json
{
  "Provider": "S3",
  "S3": {
    "BucketName": "audit-archive",
    "Region": "us-east-1",
    "AccessKeyId": "YOUR_ACCESS_KEY",
    "SecretAccessKey": "YOUR_SECRET_KEY",
    "UseServerSideEncryption": true
  }
}
```

**Azure Configuration:**
```json
{
  "Provider": "Azure",
  "Azure": {
    "ConnectionString": "YOUR_CONNECTION_STRING",
    "ContainerName": "audit-archive",
    "UseEncryption": true
  }
}
```

---

## Alert Manager

Multi-channel alerting system for critical events.

### General Settings

| Setting | Description | Default |
|---------|-------------|---------|
| `Enabled` | Enable alerting | true |
| `MaxAlertsPerRulePerHour` | Rate limit per rule | 10 |
| `RateLimitWindowMinutes` | Rate limit window | 60 |
| `MaxNotificationQueueSize` | Max queued notifications | 1000 |
| `NotificationTimeoutSeconds` | Timeout for sending notifications | 30 |
| `NotificationRetryAttempts` | Retry attempts on failure | 3 |
| `UseExponentialBackoff` | Use exponential backoff for retries | true |

### Email Notifications

| Setting | Description |
|---------|-------------|
| `Email.Enabled` | Enable email notifications |
| `Email.SmtpHost` | SMTP server hostname |
| `Email.SmtpPort` | SMTP server port (587 for TLS) |
| `Email.SmtpUsername` | SMTP authentication username |
| `Email.SmtpPassword` | SMTP authentication password |
| `Email.SmtpUseSsl` | Use SSL/TLS |
| `Email.FromEmailAddress` | Sender email address |
| `Email.FromDisplayName` | Sender display name |
| `Email.DefaultRecipients` | Default recipient email addresses |

### Webhook Notifications

| Setting | Description |
|---------|-------------|
| `Webhook.Enabled` | Enable webhook notifications |
| `Webhook.DefaultUrl` | Default webhook URL |
| `Webhook.AuthHeaderName` | Authentication header name |
| `Webhook.AuthHeaderValue` | Authentication header value |
| `Webhook.TimeoutSeconds` | Request timeout |
| `Webhook.IncludeFullPayload` | Include full event data |

### SMS Notifications (Twilio)

| Setting | Description |
|---------|-------------|
| `Sms.Enabled` | Enable SMS notifications |
| `Sms.Provider` | SMS provider (Twilio) |
| `Sms.TwilioAccountSid` | Twilio account SID |
| `Sms.TwilioAuthToken` | Twilio auth token |
| `Sms.TwilioFromPhoneNumber` | Sender phone number |
| `Sms.MaxSmsLength` | Maximum SMS length |

### Alert Rules

Pre-configured alert rules for common scenarios:

| Rule | Severity | Default Channels |
|------|----------|------------------|
| CriticalException | Critical | Email |
| SecurityThreat | High | Email, Webhook |
| PerformanceDegradation | Medium | Email |
| HighFailureRate | High | Email |

---

## Audit Query Service

Configuration for querying and searching audit logs.

| Setting | Description | Default |
|---------|-------------|---------|
| `MaxPageSize` | Maximum records per page | 1000 |
| `DefaultPageSize` | Default records per page | 50 |
| `MaxDateRangeDays` | Maximum date range for queries | 365 |
| `QueryTimeoutSeconds` | Query timeout | 30 |
| `EnableFullTextSearch` | Enable full-text search | false |
| `FullTextSearchMinLength` | Minimum search term length | 3 |
| `EnableParallelQueries` | Enable parallel query execution | false |
| `MaxParallelDegree` | Max parallel threads | 4 |
| `ExportMaxRecords` | Maximum records for export | 100000 |
| `ExportTimeoutMinutes` | Export timeout | 10 |

### Query Caching

| Setting | Description | Default |
|---------|-------------|---------|
| `AuditQueryCaching.Enabled` | Enable query result caching | false |
| `AuditQueryCaching.CacheDurationMinutes` | Cache duration | 5 |
| `AuditQueryCaching.RedisConnectionString` | Redis connection | localhost:6379 |
| `AuditQueryCaching.CacheKeyPrefix` | Cache key prefix | audit_query: |
| `AuditQueryCaching.MaxCachedResultSizeKB` | Max cached result size | 1024 |

---

## Legacy Audit Service

Backward compatibility layer for existing audit log interfaces.

### Module Mappings

Maps entity types to business modules for legacy UI:

```json
{
  "SYS_USERS": "User Management",
  "SYS_COMPANY": "Company Management",
  "SYS_BRANCH": "Branch Management",
  "TICKET": "Ticket System"
}
```

### Device Identifier Patterns

Maps User-Agent patterns to device types:

```json
{
  "Mobile": "Mobile Device",
  "Tablet": "Tablet Device",
  "Desktop": "Desktop",
  "API": "API Client"
}
```

### Error Code Prefixes

Prefixes for standardized error codes:

| Prefix | Category |
|--------|----------|
| DB | Database errors |
| VAL | Validation errors |
| AUTH | Authentication errors |
| AUTHZ | Authorization errors |
| NF | Not found errors |
| CONF | Conflict errors |

### Status Workflow

Defines allowed status transitions for error resolution:

- **Unresolved** → In Progress, Resolved, Critical
- **In Progress** → Resolved, Unresolved, Critical
- **Critical** → In Progress, Resolved
- **Resolved** → Unresolved

---

## Serilog Configuration

Structured logging configuration with correlation ID enrichment.

### Enrichers

- `FromLogContext` - Include log context properties
- `WithMachineName` - Include machine name
- `WithThreadId` - Include thread ID

### Sinks

**Console Sink:**
- Real-time console output
- Simplified format for development

**File Sink:**
- Rolling daily log files
- 30-day retention
- Includes correlation ID in output template

**Output Template:**
```
[{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} {CorrelationId} {Level:u3}] {Message:lj} {Properties:j}{NewLine}{Exception}
```

---

## Health Checks

System health monitoring configuration.

| Component | Setting | Default Timeout |
|-----------|---------|-----------------|
| Audit Logging | `HealthChecks.AuditLogging.Enabled` | 5 seconds |
| Database | `HealthChecks.Database.Enabled` | 10 seconds |
| Redis | `HealthChecks.Redis.Enabled` | 5 seconds |
| External Storage | `HealthChecks.ExternalStorage.Enabled` | 10 seconds |

---

## Environment-Specific Configuration

### Development (appsettings.Development.json)

Recommended overrides for development:
- Enable verbose logging
- Disable email/SMS alerts
- Reduce batch sizes for faster feedback
- Enable file system fallback
- Disable encryption for easier debugging

### Production (appsettings.Production.json)

Recommended overrides for production:
- Enable all security features
- Configure real SMTP/SMS credentials
- Enable Redis caching
- Enable external storage for archival
- Enable encryption for sensitive data
- Configure appropriate retention policies

---

## Security Best Practices

1. **Signing Keys**: Generate unique signing keys using cryptographically secure random number generators
2. **Encryption Keys**: Use 32-byte (256-bit) keys for AES encryption
3. **Key Rotation**: Implement key rotation policies (90-day default)
4. **Secrets Management**: Store sensitive configuration in Azure Key Vault or AWS Secrets Manager
5. **Connection Strings**: Never commit connection strings with production credentials
6. **SMTP Credentials**: Use app-specific passwords or OAuth2 for email
7. **API Keys**: Rotate webhook and SMS API keys regularly

---

## Performance Tuning

### High-Volume Environments (>10,000 req/min)

1. **Increase Batch Size**: Set `AuditLogging.BatchSize` to 100-200
2. **Reduce Batch Window**: Set `AuditLogging.BatchWindowMs` to 50-75ms
3. **Enable Redis Caching**: Set `AuditQueryCaching.Enabled` to true
4. **Increase Connection Pool**: Adjust Oracle connection pool settings
5. **Enable Parallel Queries**: Set `AuditQuery.EnableParallelQueries` to true
6. **Partition Tables**: Implement table partitioning for SYS_AUDIT_LOG

### Low-Volume Environments (<1,000 req/min)

1. **Reduce Batch Size**: Set `AuditLogging.BatchSize` to 25
2. **Increase Batch Window**: Set `AuditLogging.BatchWindowMs` to 200ms
3. **Disable Caching**: Set `AuditQueryCaching.Enabled` to false
4. **Reduce Connection Pool**: Lower Min/Max pool sizes
5. **Simplify Monitoring**: Disable CPU/memory tracking if not needed

---

## Troubleshooting

### Audit Logging Not Working

1. Check `AuditLogging.Enabled` is true
2. Verify database connection string
3. Check circuit breaker status (may be open after failures)
4. Review fallback directory for file system fallback logs
5. Check application logs for audit logger errors

### Performance Issues

1. Check `PerformanceMonitoring` metrics for bottlenecks
2. Review slow query logs
3. Verify batch processing settings
4. Check database connection pool utilization
5. Consider enabling Redis caching

### Missing Audit Logs

1. Verify retention policies haven't archived data
2. Check archival service logs
3. Verify query date ranges
4. Check multi-tenant filtering (company/branch access)
5. Review audit log integrity status

### Alert Not Sending

1. Verify `Alerting.Enabled` is true
2. Check notification channel configuration (SMTP, webhook, SMS)
3. Review alert rate limiting settings
4. Check alert rule configuration
5. Verify network connectivity to notification services

---

## Migration Guide

### Upgrading from Previous Versions

1. **Backup Configuration**: Save existing appsettings.json
2. **Review New Settings**: Compare with this guide
3. **Update Connection Strings**: Add Redis if using caching
4. **Configure Retention Policies**: Set appropriate retention periods
5. **Test in Staging**: Validate configuration in non-production environment
6. **Monitor After Deployment**: Watch for errors in first 24 hours

### First-Time Setup

1. **Generate Keys**: Create signing and encryption keys
2. **Configure Database**: Ensure Oracle connection is valid
3. **Set Retention Policies**: Configure based on compliance requirements
4. **Configure Alerts**: Set up email/webhook notifications
5. **Test Health Checks**: Verify all health checks pass
6. **Run Archival**: Test archival service with small dataset

---

## Support and Documentation

For additional help:
- Review design document: `.kiro/specs/full-traceability-system/design.md`
- Review requirements: `.kiro/specs/full-traceability-system/requirements.md`
- Check implementation tasks: `.kiro/specs/full-traceability-system/tasks.md`
- Review API documentation: Swagger UI at `/swagger`

---

**Last Updated**: 2024
**Version**: 1.0
