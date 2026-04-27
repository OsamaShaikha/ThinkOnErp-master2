# Full Traceability System - Configuration Reference Guide

## Table of Contents

1. [Overview](#overview)
2. [Configuration Structure](#configuration-structure)
3. [Audit Logging Configuration](#audit-logging-configuration)
4. [Request Tracing Configuration](#request-tracing-configuration)
5. [Performance Monitoring Configuration](#performance-monitoring-configuration)
6. [Security Monitoring Configuration](#security-monitoring-configuration)
7. [Archival Configuration](#archival-configuration)
8. [Alerting Configuration](#alerting-configuration)
9. [Compliance Reporting Configuration](#compliance-reporting-configuration)
10. [Encryption and Integrity Configuration](#encryption-and-integrity-configuration)
11. [Key Management Configuration](#key-management-configuration)
12. [Legacy Audit Service Configuration](#legacy-audit-service-configuration)
13. [Audit Query Configuration](#audit-query-configuration)
14. [Health Checks Configuration](#health-checks-configuration)
15. [Environment-Specific Configuration](#environment-specific-configuration)
16. [Configuration Validation](#configuration-validation)
17. [Troubleshooting](#troubleshooting)

---

## Overview

The Full Traceability System provides comprehensive audit logging, request tracing, performance monitoring, security monitoring, and compliance reporting capabilities for the ThinkOnErp API. This guide documents all configuration options available in `appsettings.json`.

### Key Features

- **Audit Logging**: Capture all data changes, authentication events, and system operations
- **Request Tracing**: Track API requests with unique correlation IDs
- **Performance Monitoring**: Monitor system performance and identify bottlenecks
- **Security Monitoring**: Detect and alert on suspicious activities
- **Compliance Reporting**: Generate GDPR, SOX, and ISO 27001 compliance reports
- **Data Archival**: Automatically archive old audit data based on retention policies
- **Alerting**: Send notifications for critical events via email, webhook, or SMS

### Configuration Files

- **appsettings.json**: Base configuration for all environments
- **appsettings.Development.json**: Development-specific overrides
- **appsettings.Production.json**: Production-specific overrides
- **appsettings.audit.example.json**: Example audit configuration
- **appsettings.encryption.example.json**: Example encryption configuration
- **appsettings.security.example.json**: Example security configuration
- **appsettings.integrity.example.json**: Example integrity configuration

---

## Configuration Structure

All traceability system configuration is located in the root of `appsettings.json`. The main configuration sections are:

```json
{
  "AuditLogging": { },
  "RequestTracing": { },
  "PerformanceMonitoring": { },
  "SecurityMonitoring": { },
  "Archival": { },
  "Alerting": { },
  "ComplianceReporting": { },
  "AuditEncryption": { },
  "AuditIntegrity": { },
  "KeyManagement": { },
  "LegacyAuditService": { },
  "AuditQuery": { },
  "AuditQueryCaching": { },
  "HealthChecks": { }
}
```

---

## Audit Logging Configuration

The `AuditLogging` section controls how audit events are captured, batched, and persisted to the database.

### Configuration Options

```json
{
  "AuditLogging": {
    "Enabled": true,
    "BatchSize": 50,
    "BatchWindowMs": 100,
    "MaxQueueSize": 10000,
    "SensitiveFields": [
      "password",
      "token",
      "refreshToken",
      "accessToken",
      "creditCard",
      "cvv",
      "ssn",
      "socialSecurityNumber",
      "taxId",
      "bankAccount",
      "routingNumber",
      "pin",
      "securityAnswer"
    ],
    "MaskingPattern": "***MASKED***",
    "MaxPayloadSize": 10240,
    "DatabaseTimeoutSeconds": 30,
    "EnableCircuitBreaker": true,
    "CircuitBreakerFailureThreshold": 5,
    "CircuitBreakerTimeoutSeconds": 60,
    "EnableRetryPolicy": true,
    "MaxRetryAttempts": 3,
    "InitialRetryDelayMs": 100,
    "MaxRetryDelayMs": 5000,
    "UseRetryJitter": true,
    "EncryptSensitiveData": false,
    "LogSuccessfulOperations": true,
    "LogFailedOperations": true,
    "EnableFileSystemFallback": true,
    "FallbackDirectory": "AuditFallback"
  }
}
```

### Parameter Descriptions

| Parameter | Type | Default | Description |
|-----------|------|---------|-------------|
| `Enabled` | boolean | `true` | Master switch to enable/disable audit logging |
| `BatchSize` | integer | `50` | Number of audit events to batch before writing to database |
| `BatchWindowMs` | integer | `100` | Maximum time (ms) to wait before writing a batch |
| `MaxQueueSize` | integer | `10000` | Maximum number of events in the async queue before backpressure |
| `SensitiveFields` | array | See above | List of field names to mask in audit logs |
| `MaskingPattern` | string | `"***MASKED***"` | Pattern to use when masking sensitive data |
| `MaxPayloadSize` | integer | `10240` | Maximum payload size (bytes) to log before truncation |
| `DatabaseTimeoutSeconds` | integer | `30` | Timeout for database write operations |
| `EnableCircuitBreaker` | boolean | `true` | Enable circuit breaker pattern for database failures |
| `CircuitBreakerFailureThreshold` | integer | `5` | Number of failures before opening circuit breaker |
| `CircuitBreakerTimeoutSeconds` | integer | `60` | Duration circuit breaker stays open |
| `EnableRetryPolicy` | boolean | `true` | Enable automatic retry for transient failures |
| `MaxRetryAttempts` | integer | `3` | Maximum number of retry attempts |
| `InitialRetryDelayMs` | integer | `100` | Initial delay before first retry |
| `MaxRetryDelayMs` | integer | `5000` | Maximum delay between retries |
| `UseRetryJitter` | boolean | `true` | Add random jitter to retry delays |
| `EncryptSensitiveData` | boolean | `false` | Encrypt sensitive data before storage |
| `LogSuccessfulOperations` | boolean | `true` | Log successful operations |
| `LogFailedOperations` | boolean | `true` | Log failed operations |
| `EnableFileSystemFallback` | boolean | `true` | Use file system fallback when database unavailable |
| `FallbackDirectory` | string | `"AuditFallback"` | Directory for fallback audit files |

### Recommended Settings

**Development Environment:**
```json
{
  "BatchSize": 10,
  "BatchWindowMs": 50,
  "EnableCircuitBreaker": false,
  "EnableRetryPolicy": false
}
```

**Production Environment:**
```json
{
  "BatchSize": 100,
  "BatchWindowMs": 200,
  "EnableCircuitBreaker": true,
  "EnableRetryPolicy": true,
  "EncryptSensitiveData": true
}
```

### Performance Tuning

- **High Volume (>10,000 req/min)**: Increase `BatchSize` to 100-200, increase `BatchWindowMs` to 200-500
- **Low Latency Requirements**: Decrease `BatchSize` to 20-30, decrease `BatchWindowMs` to 50-100
- **Memory Constrained**: Decrease `MaxQueueSize` to 5000, enable backpressure earlier

---

## Request Tracing Configuration

The `RequestTracing` section controls how API requests are traced with correlation IDs and how request/response payloads are logged.

### Configuration Options

```json
{
  "RequestTracing": {
    "Enabled": true,
    "LogPayloads": true,
    "PayloadLoggingLevel": "Full",
    "MaxPayloadSize": 10240,
    "ExcludedPaths": [
      "/health",
      "/metrics",
      "/swagger",
      "/api/health",
      "/api/metrics"
    ],
    "CorrelationIdHeader": "X-Correlation-ID",
    "PopulateLegacyFields": true,
    "LogRequestStart": false,
    "LogRequestEnd": true,
    "IncludeHeaders": true,
    "ExcludedHeaders": [
      "Authorization",
      "Cookie",
      "Set-Cookie",
      "X-API-Key"
    ],
    "IncludeQueryString": true,
    "TruncateLargePayloads": true,
    "LogResponseBody": true
  }
}
```

### Parameter Descriptions

| Parameter | Type | Default | Description |
|-----------|------|---------|-------------|
| `Enabled` | boolean | `true` | Enable request tracing middleware |
| `LogPayloads` | boolean | `true` | Log request and response payloads |
| `PayloadLoggingLevel` | string | `"Full"` | Logging level: `"None"`, `"MetadataOnly"`, `"Full"` |
| `MaxPayloadSize` | integer | `10240` | Maximum payload size (bytes) before truncation |
| `ExcludedPaths` | array | See above | Paths to exclude from request tracing |
| `CorrelationIdHeader` | string | `"X-Correlation-ID"` | HTTP header name for correlation ID |
| `PopulateLegacyFields` | boolean | `true` | Populate legacy audit log fields for backward compatibility |
| `LogRequestStart` | boolean | `false` | Log when request starts (creates 2 log entries per request) |
| `LogRequestEnd` | boolean | `true` | Log when request completes |
| `IncludeHeaders` | boolean | `true` | Include HTTP headers in audit logs |
| `ExcludedHeaders` | array | See above | Headers to exclude from logging (security) |
| `IncludeQueryString` | boolean | `true` | Include query string parameters in audit logs |
| `TruncateLargePayloads` | boolean | `true` | Truncate payloads exceeding `MaxPayloadSize` |
| `LogResponseBody` | boolean | `true` | Log response body content |

### Payload Logging Levels

- **None**: No payload logging, only metadata (method, path, status code)
- **MetadataOnly**: Log payload size and content type, but not actual content
- **Full**: Log complete request and response payloads (subject to size limits)

### Recommended Settings

**Development Environment:**
```json
{
  "PayloadLoggingLevel": "Full",
  "LogRequestStart": true,
  "IncludeHeaders": true
}
```

**Production Environment:**
```json
{
  "PayloadLoggingLevel": "MetadataOnly",
  "LogRequestStart": false,
  "MaxPayloadSize": 5120
}
```

---

## Performance Monitoring Configuration

The `PerformanceMonitoring` section controls performance metrics collection, slow request detection, and system health monitoring.

### Configuration Options

```json
{
  "PerformanceMonitoring": {
    "Enabled": true,
    "SlowRequestThresholdMs": 1000,
    "SlowQueryThresholdMs": 500,
    "TrackMemoryMetrics": true,
    "TrackDatabaseMetrics": true,
    "TrackCpuMetrics": true,
    "MetricsRetentionHours": 24,
    "AggregateMetricsHourly": true,
    "EnablePercentileCalculations": true,
    "SlidingWindowSizeMinutes": 60,
    "MetricsAggregation": {
      "Enabled": true,
      "IntervalMinutes": 60,
      "BatchSize": 1000,
      "RetentionDays": 90
    },
    "SlowRequestLogging": {
      "Enabled": true,
      "MaxRecordsPerHour": 100
    },
    "SlowQueryLogging": {
      "Enabled": true,
      "MaxRecordsPerHour": 100,
      "LogQueryParameters": true
    }
  }
}
```

### Parameter Descriptions

| Parameter | Type | Default | Description |
|-----------|------|---------|-------------|
| `Enabled` | boolean | `true` | Enable performance monitoring |
| `SlowRequestThresholdMs` | integer | `1000` | Threshold (ms) for flagging slow requests |
| `SlowQueryThresholdMs` | integer | `500` | Threshold (ms) for flagging slow database queries |
| `TrackMemoryMetrics` | boolean | `true` | Track memory usage and GC metrics |
| `TrackDatabaseMetrics` | boolean | `true` | Track database query performance |
| `TrackCpuMetrics` | boolean | `true` | Track CPU utilization |
| `MetricsRetentionHours` | integer | `24` | Hours to retain detailed metrics in memory |
| `AggregateMetricsHourly` | boolean | `true` | Aggregate metrics to hourly summaries |
| `EnablePercentileCalculations` | boolean | `true` | Calculate p50, p95, p99 percentiles |
| `SlidingWindowSizeMinutes` | integer | `60` | Size of sliding window for metrics |
| `MetricsAggregation.Enabled` | boolean | `true` | Enable background metrics aggregation |
| `MetricsAggregation.IntervalMinutes` | integer | `60` | Interval for aggregation |
| `MetricsAggregation.BatchSize` | integer | `1000` | Batch size for aggregation |
| `MetricsAggregation.RetentionDays` | integer | `90` | Days to retain aggregated metrics |
| `SlowRequestLogging.Enabled` | boolean | `true` | Log slow requests to database |
| `SlowRequestLogging.MaxRecordsPerHour` | integer | `100` | Maximum slow requests to log per hour |
| `SlowQueryLogging.Enabled` | boolean | `true` | Log slow queries to database |
| `SlowQueryLogging.MaxRecordsPerHour` | integer | `100` | Maximum slow queries to log per hour |
| `SlowQueryLogging.LogQueryParameters` | boolean | `true` | Include query parameters in slow query logs |

### Recommended Settings

**Development Environment:**
```json
{
  "SlowRequestThresholdMs": 500,
  "SlowQueryThresholdMs": 200,
  "MetricsRetentionHours": 1
}
```

**Production Environment:**
```json
{
  "SlowRequestThresholdMs": 2000,
  "SlowQueryThresholdMs": 1000,
  "MetricsRetentionHours": 24,
  "AggregateMetricsHourly": true
}
```

---

## Security Monitoring Configuration

The `SecurityMonitoring` section controls threat detection, failed login tracking, and security alert generation.

### Configuration Options

```json
{
  "SecurityMonitoring": {
    "Enabled": true,
    "FailedLoginThreshold": 5,
    "FailedLoginWindowMinutes": 5,
    "AnomalousActivityThreshold": 1000,
    "AnomalousActivityWindowHours": 1,
    "RateLimitPerIp": 100,
    "RateLimitPerUser": 200,
    "EnableSqlInjectionDetection": true,
    "EnableXssDetection": true,
    "EnableUnauthorizedAccessDetection": true,
    "EnableAnomalousActivityDetection": true,
    "EnableGeographicAnomalyDetection": false,
    "AutoBlockSuspiciousIps": false,
    "IpBlockDurationMinutes": 60,
    "SendEmailAlerts": true,
    "AlertEmailRecipients": "admin@thinkonerp.com",
    "SendWebhookAlerts": false,
    "AlertWebhookUrl": null,
    "MinimumAlertSeverity": "High",
    "MaxAlertsPerHour": 10,
    "FailedLoginRetentionDays": 7,
    "ThreatRetentionDays": 365,
    "EnableVerboseLogging": false,
    "UseRedisCache": false,
    "RedisConnectionString": "localhost:6379",
    "RegexTimeoutMs": 100
  }
}
```

### Parameter Descriptions

| Parameter | Type | Default | Description |
|-----------|------|---------|-------------|
| `Enabled` | boolean | `true` | Enable security monitoring |
| `FailedLoginThreshold` | integer | `5` | Number of failed logins before flagging IP |
| `FailedLoginWindowMinutes` | integer | `5` | Time window for failed login detection |
| `AnomalousActivityThreshold` | integer | `1000` | Request count threshold for anomaly detection |
| `AnomalousActivityWindowHours` | integer | `1` | Time window for anomaly detection |
| `RateLimitPerIp` | integer | `100` | Maximum requests per IP per minute |
| `RateLimitPerUser` | integer | `200` | Maximum requests per user per minute |
| `EnableSqlInjectionDetection` | boolean | `true` | Detect SQL injection patterns |
| `EnableXssDetection` | boolean | `true` | Detect XSS attack patterns |
| `EnableUnauthorizedAccessDetection` | boolean | `true` | Detect unauthorized access attempts |
| `EnableAnomalousActivityDetection` | boolean | `true` | Detect unusual activity patterns |
| `EnableGeographicAnomalyDetection` | boolean | `false` | Detect requests from unusual locations (requires GeoIP) |
| `AutoBlockSuspiciousIps` | boolean | `false` | Automatically block suspicious IP addresses |
| `IpBlockDurationMinutes` | integer | `60` | Duration to block suspicious IPs |
| `SendEmailAlerts` | boolean | `true` | Send email alerts for security threats |
| `AlertEmailRecipients` | string | `"admin@thinkonerp.com"` | Email recipients for security alerts |
| `SendWebhookAlerts` | boolean | `false` | Send webhook alerts for security threats |
| `AlertWebhookUrl` | string | `null` | Webhook URL for security alerts |
| `MinimumAlertSeverity` | string | `"High"` | Minimum severity for alerts: `"Low"`, `"Medium"`, `"High"`, `"Critical"` |
| `MaxAlertsPerHour` | integer | `10` | Maximum alerts to send per hour (rate limiting) |
| `FailedLoginRetentionDays` | integer | `7` | Days to retain failed login records |
| `ThreatRetentionDays` | integer | `365` | Days to retain security threat records |
| `EnableVerboseLogging` | boolean | `false` | Enable verbose security logging |
| `UseRedisCache` | boolean | `false` | Use Redis for failed login tracking |
| `RedisConnectionString` | string | `"localhost:6379"` | Redis connection string |
| `RegexTimeoutMs` | integer | `100` | Timeout for regex pattern matching |

### Threat Detection Patterns

The security monitor detects the following patterns:

- **SQL Injection**: `' OR '1'='1`, `UNION SELECT`, `DROP TABLE`, etc.
- **XSS**: `<script>`, `javascript:`, `onerror=`, etc.
- **Path Traversal**: `../`, `..\\`, etc.
- **Command Injection**: `; rm -rf`, `| cat /etc/passwd`, etc.

### Recommended Settings

**Development Environment:**
```json
{
  "FailedLoginThreshold": 10,
  "AutoBlockSuspiciousIps": false,
  "EnableVerboseLogging": true
}
```

**Production Environment:**
```json
{
  "FailedLoginThreshold": 5,
  "AutoBlockSuspiciousIps": true,
  "IpBlockDurationMinutes": 120,
  "UseRedisCache": true
}
```

---

## Archival Configuration

The `Archival` section controls automatic archival of old audit data based on retention policies.

### Configuration Options

```json
{
  "Archival": {
    "Enabled": true,
    "Schedule": "0 2 * * *",
    "BatchSize": 1000,
    "TransactionTimeoutSeconds": 30,
    "CompressionEnabled": true,
    "CompressionAlgorithm": "GZip",
    "CompressionLevel": "Optimal",
    "StorageProvider": "Database",
    "StorageConnectionString": null,
    "VerifyIntegrity": true,
    "TimeoutMinutes": 60,
    "EncryptArchivedData": false,
    "EncryptionKeyId": null,
    "RunOnStartup": false,
    "TimeZone": "UTC",
    "RetentionPolicies": {
      "Authentication": 365,
      "DataChange": 1095,
      "Financial": 2555,
      "PersonalData": 1095,
      "Security": 730,
      "Configuration": 1825,
      "Exception": 365,
      "Request": 90
    },
    "ExternalStorage": {
      "Enabled": false,
      "Provider": "S3",
      "S3": {
        "BucketName": "",
        "Region": "us-east-1",
        "AccessKeyId": "",
        "SecretAccessKey": "",
        "UseServerSideEncryption": true
      },
      "Azure": {
        "ConnectionString": "",
        "ContainerName": "audit-archive",
        "UseEncryption": true
      }
    },
    "CleanupOldArchives": {
      "Enabled": false,
      "RetentionYears": 10
    }
  }
}
```

### Parameter Descriptions

| Parameter | Type | Default | Description |
|-----------|------|---------|-------------|
| `Enabled` | boolean | `true` | Enable automatic archival |
| `Schedule` | string | `"0 2 * * *"` | Cron expression for archival schedule (2 AM daily) |
| `BatchSize` | integer | `1000` | Number of records to archive per batch |
| `TransactionTimeoutSeconds` | integer | `30` | Timeout for archival transactions |
| `CompressionEnabled` | boolean | `true` | Compress archived data |
| `CompressionAlgorithm` | string | `"GZip"` | Compression algorithm: `"GZip"`, `"Deflate"`, `"Brotli"` |
| `CompressionLevel` | string | `"Optimal"` | Compression level: `"Fastest"`, `"Optimal"`, `"SmallestSize"` |
| `StorageProvider` | string | `"Database"` | Storage provider: `"Database"`, `"S3"`, `"Azure"` |
| `StorageConnectionString` | string | `null` | Connection string for external storage |
| `VerifyIntegrity` | boolean | `true` | Verify data integrity after archival using checksums |
| `TimeoutMinutes` | integer | `60` | Overall timeout for archival process |
| `EncryptArchivedData` | boolean | `false` | Encrypt archived data |
| `EncryptionKeyId` | string | `null` | Key ID for encryption |
| `RunOnStartup` | boolean | `false` | Run archival process on application startup |
| `TimeZone` | string | `"UTC"` | Time zone for schedule |
| `RetentionPolicies.*` | integer | Various | Retention period (days) for each event category |
| `ExternalStorage.Enabled` | boolean | `false` | Enable external storage (S3, Azure) |
| `ExternalStorage.Provider` | string | `"S3"` | External storage provider |
| `CleanupOldArchives.Enabled` | boolean | `false` | Delete archives older than retention period |
| `CleanupOldArchives.RetentionYears` | integer | `10` | Years to retain archived data |

### Retention Policies

| Event Category | Default Retention (Days) | Compliance Requirement |
|----------------|--------------------------|------------------------|
| Authentication | 365 (1 year) | General security |
| DataChange | 1095 (3 years) | GDPR |
| Financial | 2555 (7 years) | SOX |
| PersonalData | 1095 (3 years) | GDPR |
| Security | 730 (2 years) | ISO 27001 |
| Configuration | 1825 (5 years) | Change management |
| Exception | 365 (1 year) | Troubleshooting |
| Request | 90 (3 months) | Performance analysis |

### Cron Schedule Examples

- `"0 2 * * *"` - Daily at 2:00 AM
- `"0 3 * * 0"` - Weekly on Sunday at 3:00 AM
- `"0 4 1 * *"` - Monthly on the 1st at 4:00 AM
- `"0 */6 * * *"` - Every 6 hours

### Recommended Settings

**Development Environment:**
```json
{
  "Enabled": false,
  "RunOnStartup": false
}
```

**Production Environment:**
```json
{
  "Enabled": true,
  "Schedule": "0 2 * * *",
  "CompressionEnabled": true,
  "VerifyIntegrity": true,
  "ExternalStorage": {
    "Enabled": true,
    "Provider": "S3"
  }
}
```

---

## Alerting Configuration

The `Alerting` section controls alert rules, notification channels, and alert delivery.

### Configuration Options

```json
{
  "Alerting": {
    "Enabled": true,
    "MaxAlertsPerRulePerHour": 10,
    "RateLimitWindowMinutes": 60,
    "MaxNotificationQueueSize": 1000,
    "NotificationTimeoutSeconds": 30,
    "NotificationRetryAttempts": 3,
    "RetryDelaySeconds": 5,
    "UseExponentialBackoff": true,
    "BackgroundProcessing": {
      "Enabled": true,
      "MaxConcurrentAlerts": 5,
      "ProcessingIntervalSeconds": 10
    },
    "Email": {
      "Enabled": false,
      "SmtpHost": null,
      "SmtpPort": 587,
      "SmtpUsername": null,
      "SmtpPassword": null,
      "SmtpUseSsl": true,
      "FromEmailAddress": null,
      "FromDisplayName": "ThinkOnErp Alerts",
      "DefaultRecipients": ["admin@thinkonerp.com"],
      "EnableHtmlEmails": true,
      "IncludeLogoInEmails": false
    },
    "Webhook": {
      "Enabled": false,
      "DefaultUrl": null,
      "AuthHeaderName": "X-API-Key",
      "AuthHeaderValue": null,
      "TimeoutSeconds": 30,
      "RetryOnFailure": true,
      "IncludeFullPayload": true
    },
    "Sms": {
      "Enabled": false,
      "Provider": "Twilio",
      "TwilioAccountSid": null,
      "TwilioAuthToken": null,
      "TwilioFromPhoneNumber": null,
      "MaxSmsLength": 160,
      "DefaultRecipients": []
    },
    "AlertRules": {
      "CriticalException": {
        "Enabled": true,
        "Severity": "Critical",
        "Channels": ["Email"]
      },
      "SecurityThreat": {
        "Enabled": true,
        "Severity": "High",
        "Channels": ["Email", "Webhook"]
      }
    }
  }
}
```

### Alert Rules

The system includes pre-configured alert rules for common scenarios:

#### Security Alert Rules

- **FailedLoginPattern**: Multiple failed login attempts from same IP
- **SqlInjectionAttempt**: SQL injection pattern detected
- **XssAttempt**: XSS pattern detected
- **UnauthorizedAccessAttempt**: Access to unauthorized data
- **AnomalousUserActivity**: Unusual activity pattern
- **GeographicAnomaly**: Request from unusual location
- **PrivilegeEscalationAttempt**: Unauthorized permission elevation

#### Performance Alert Rules

- **PerformanceDegradation**: System performance below threshold
- **HighFailureRate**: High percentage of failed requests
- **ConnectionPoolWarning**: Connection pool utilization >80%
- **ConnectionPoolCritical**: Connection pool utilization >95%

#### Audit System Alert Rules

- **AuditCircuitBreakerOpen**: Audit logging circuit breaker opened
- **AuditQueueOverflow**: Audit queue at 90% capacity
- **AuditQueueFull**: Audit queue full, backpressure applied
- **AuditDatabaseWriteFailure**: Audit database writes failing
- **AuditFallbackActivated**: Fallback storage activated
- **AuditProcessingDelayed**: Audit processing delayed
- **AuditHealthCheckFailed**: Audit health check failed
- **AuditFallbackReplayFailed**: Failed to replay fallback events
- **AuditHighFailureRate**: Audit failure rate >20%

### Alert Rule Configuration

Each alert rule supports the following properties:

```json
{
  "RuleName": {
    "Enabled": true,
    "Severity": "High",
    "Description": "Human-readable description",
    "Channels": ["Email", "Webhook", "Sms"],
    "CheckIntervalMinutes": 5,
    "RateLimitPerHour": 10,
    "AutoBlock": false,
    "BlockDurationMinutes": 30,
    "RequireInvestigation": false,
    "LogFullRequest": true
  }
}
```

### Notification Channels

#### Email Configuration

```json
{
  "Email": {
    "Enabled": true,
    "SmtpHost": "smtp.gmail.com",
    "SmtpPort": 587,
    "SmtpUsername": "alerts@thinkonerp.com",
    "SmtpPassword": "your-password",
    "SmtpUseSsl": true,
    "FromEmailAddress": "alerts@thinkonerp.com",
    "FromDisplayName": "ThinkOnErp Alerts",
    "DefaultRecipients": ["admin@thinkonerp.com", "security@thinkonerp.com"]
  }
}
```

#### Webhook Configuration

```json
{
  "Webhook": {
    "Enabled": true,
    "DefaultUrl": "https://hooks.slack.com/services/YOUR/WEBHOOK/URL",
    "AuthHeaderName": "X-API-Key",
    "AuthHeaderValue": "your-api-key",
    "TimeoutSeconds": 30,
    "RetryOnFailure": true,
    "IncludeFullPayload": true
  }
}
```

#### SMS Configuration (Twilio)

```json
{
  "Sms": {
    "Enabled": true,
    "Provider": "Twilio",
    "TwilioAccountSid": "ACxxxxxxxxxxxxxxxxxxxxxxxxxxxxx",
    "TwilioAuthToken": "your-auth-token",
    "TwilioFromPhoneNumber": "+1234567890",
    "DefaultRecipients": ["+1234567890", "+0987654321"]
  }
}
```

### Recommended Settings

**Development Environment:**
```json
{
  "Enabled": false,
  "Email": {
    "Enabled": false
  }
}
```

**Production Environment:**
```json
{
  "Enabled": true,
  "MaxAlertsPerRulePerHour": 10,
  "Email": {
    "Enabled": true,
    "SmtpHost": "smtp.yourcompany.com"
  },
  "Webhook": {
    "Enabled": true,
    "DefaultUrl": "https://your-webhook-url"
  }
}
```

---

## Compliance Reporting Configuration

The `ComplianceReporting` section controls compliance report generation for GDPR, SOX, and ISO 27001.

### Configuration Options

```json
{
  "ComplianceReporting": {
    "Enabled": true,
    "ReportCacheDurationMinutes": 30,
    "MaxReportSizeMB": 50,
    "EnablePdfGeneration": true,
    "EnableCsvExport": true,
    "EnableJsonExport": true,
    "ScheduledReports": {
      "Enabled": true,
      "CheckIntervalMinutes": 15,
      "Reports": [
        {
          "Name": "Daily Security Summary",
          "Type": "SecuritySummary",
          "Schedule": "0 8 * * *",
          "Format": "PDF",
          "Recipients": ["security@thinkonerp.com"],
          "Enabled": false
        },
        {
          "Name": "Weekly GDPR Access Report",
          "Type": "GdprAccess",
          "Schedule": "0 9 * * 1",
          "Format": "PDF",
          "Recipients": ["compliance@thinkonerp.com"],
          "Enabled": false
        },
        {
          "Name": "Monthly SOX Financial Report",
          "Type": "SoxFinancial",
          "Schedule": "0 10 1 * *",
          "Format": "PDF",
          "Recipients": ["finance@thinkonerp.com"],
          "Enabled": false
        }
      ]
    },
    "GdprReporting": {
      "Enabled": true,
      "DefaultRetentionYears": 3,
      "IncludeArchivedData": true
    },
    "SoxReporting": {
      "Enabled": true,
      "DefaultRetentionYears": 7,
      "RequireApprovalWorkflow": false
    },
    "Iso27001Reporting": {
      "Enabled": true,
      "DefaultRetentionYears": 2
    }
  }
}
```

### Parameter Descriptions

| Parameter | Type | Default | Description |
|-----------|------|---------|-------------|
| `Enabled` | boolean | `true` | Enable compliance reporting |
| `ReportCacheDurationMinutes` | integer | `30` | Cache duration for generated reports |
| `MaxReportSizeMB` | integer | `50` | Maximum report size before pagination |
| `EnablePdfGeneration` | boolean | `true` | Enable PDF report generation |
| `EnableCsvExport` | boolean | `true` | Enable CSV export |
| `EnableJsonExport` | boolean | `true` | Enable JSON export |
| `ScheduledReports.Enabled` | boolean | `true` | Enable scheduled report generation |
| `ScheduledReports.CheckIntervalMinutes` | integer | `15` | Interval to check for scheduled reports |
| `GdprReporting.Enabled` | boolean | `true` | Enable GDPR reporting |
| `GdprReporting.DefaultRetentionYears` | integer | `3` | Default retention for GDPR data |
| `GdprReporting.IncludeArchivedData` | boolean | `true` | Include archived data in GDPR reports |
| `SoxReporting.Enabled` | boolean | `true` | Enable SOX reporting |
| `SoxReporting.DefaultRetentionYears` | integer | `7` | Default retention for SOX data |
| `SoxReporting.RequireApprovalWorkflow` | boolean | `false` | Require approval for SOX reports |
| `Iso27001Reporting.Enabled` | boolean | `true` | Enable ISO 27001 reporting |
| `Iso27001Reporting.DefaultRetentionYears` | integer | `2` | Default retention for ISO 27001 data |

### Report Types

- **SecuritySummary**: Daily security event summary
- **GdprAccess**: GDPR data access report for a data subject
- **GdprDataExport**: GDPR data export for a data subject
- **SoxFinancial**: SOX financial data access report
- **SoxSegregation**: SOX segregation of duties report
- **Iso27001Security**: ISO 27001 security event report
- **UserActivity**: User activity report
- **DataModification**: Data modification history report

### Scheduled Report Configuration

```json
{
  "Name": "Report Name",
  "Type": "ReportType",
  "Schedule": "0 8 * * *",
  "Format": "PDF",
  "Recipients": ["email@example.com"],
  "Enabled": true,
  "Parameters": {
    "StartDate": "2024-01-01",
    "EndDate": "2024-12-31"
  }
}
```

---

## Encryption and Integrity Configuration

### Audit Encryption Configuration

The `AuditEncryption` section controls encryption of sensitive audit data.

```json
{
  "AuditEncryption": {
    "Enabled": false,
    "Key": "REPLACE_WITH_BASE64_ENCODED_32_BYTE_KEY",
    "EncryptOldValue": false,
    "EncryptNewValue": false,
    "EncryptRequestPayload": false,
    "EncryptResponsePayload": false,
    "Algorithm": "AES256",
    "KeyRotationEnabled": false,
    "KeyRotationDays": 90
  }
}
```

### Audit Integrity Configuration

The `AuditIntegrity` section controls cryptographic signing for tamper detection.

```json
{
  "AuditIntegrity": {
    "Enabled": true,
    "SigningKey": "REPLACE_WITH_BASE64_KEY_GENERATED_USING_RandomNumberGenerator",
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

### Parameter Descriptions

#### Encryption Parameters

| Parameter | Type | Default | Description |
|-----------|------|---------|-------------|
| `Enabled` | boolean | `false` | Enable audit data encryption |
| `Key` | string | Required | Base64-encoded 32-byte encryption key |
| `EncryptOldValue` | boolean | `false` | Encrypt old values in audit logs |
| `EncryptNewValue` | boolean | `false` | Encrypt new values in audit logs |
| `EncryptRequestPayload` | boolean | `false` | Encrypt request payloads |
| `EncryptResponsePayload` | boolean | `false` | Encrypt response payloads |
| `Algorithm` | string | `"AES256"` | Encryption algorithm |
| `KeyRotationEnabled` | boolean | `false` | Enable automatic key rotation |
| `KeyRotationDays` | integer | `90` | Days between key rotations |

#### Integrity Parameters

| Parameter | Type | Default | Description |
|-----------|------|---------|-------------|
| `Enabled` | boolean | `true` | Enable integrity signing |
| `SigningKey` | string | Required | Base64-encoded signing key |
| `AutoGenerateHashes` | boolean | `true` | Automatically generate hashes for audit entries |
| `VerifyOnRead` | boolean | `false` | Verify integrity when reading audit logs |
| `LogIntegrityOperations` | boolean | `false` | Log integrity operations |
| `BatchSize` | integer | `100` | Batch size for integrity verification |
| `VerificationTimeoutMs` | integer | `10000` | Timeout for verification operations |
| `AlertOnTampering` | boolean | `true` | Send alert if tampering detected |
| `HashAlgorithm` | string | `"HMACSHA256"` | Hash algorithm for integrity |

### Generating Encryption Keys

Use the following C# code to generate secure keys:

```csharp
using System.Security.Cryptography;

// Generate 32-byte encryption key
byte[] encryptionKey = new byte[32];
using (var rng = RandomNumberGenerator.Create())
{
    rng.GetBytes(encryptionKey);
}
string encryptionKeyBase64 = Convert.ToBase64String(encryptionKey);

// Generate 32-byte signing key
byte[] signingKey = new byte[32];
using (var rng = RandomNumberGenerator.Create())
{
    rng.GetBytes(signingKey);
}
string signingKeyBase64 = Convert.ToBase64String(signingKey);

Console.WriteLine($"Encryption Key: {encryptionKeyBase64}");
Console.WriteLine($"Signing Key: {signingKeyBase64}");
```

### Security Best Practices

1. **Never commit keys to source control**
2. **Use environment variables or key vaults in production**
3. **Rotate keys regularly (every 90 days)**
4. **Use different keys for different environments**
5. **Store keys securely with restricted access**
6. **Enable encryption for sensitive data in production**
7. **Enable integrity signing to detect tampering**

---

## Key Management Configuration

The `KeyManagement` section controls how encryption and signing keys are stored and managed.

### Configuration Options

```json
{
  "KeyManagement": {
    "Provider": "Configuration",
    "EnableKeyRotation": false,
    "KeyRotationDays": 90,
    "AlertOnRotationDue": true,
    "RotationWarningDays": 7,
    "Configuration": {
      "EncryptionKeyPath": "AuditEncryption:Key",
      "SigningKeyPath": "AuditIntegrity:SigningKey",
      "UseEnvironmentVariables": true,
      "EncryptionKeyEnvironmentVariable": "AUDIT_ENCRYPTION_KEY",
      "SigningKeyEnvironmentVariable": "AUDIT_SIGNING_KEY"
    },
    "LocalStorage": {
      "KeyStoragePath": "Keys",
      "EncryptionKeyFileName": "encryption.key",
      "SigningKeyFileName": "signing.key",
      "UseDataProtection": true,
      "AutoGenerateKeys": true,
      "FilePermissions": "600"
    },
    "AzureKeyVault": {
      "VaultUrl": "",
      "EncryptionKeySecretName": "audit-encryption-key",
      "SigningKeySecretName": "audit-signing-key",
      "AuthenticationMethod": "ManagedIdentity",
      "TimeoutSeconds": 30,
      "RetryAttempts": 3
    },
    "AwsSecretsManager": {
      "Region": "us-east-1",
      "EncryptionKeySecretName": "audit/encryption-key",
      "SigningKeySecretName": "audit/signing-key",
      "AuthenticationMethod": "IAMRole",
      "TimeoutSeconds": 30,
      "RetryAttempts": 3
    },
    "FallbackProvider": "Configuration",
    "EnableCaching": true,
    "CacheDurationMinutes": 60
  }
}
```

### Key Management Providers

#### Configuration Provider

Stores keys in `appsettings.json` or environment variables.

**Pros**: Simple, no external dependencies
**Cons**: Less secure, keys in configuration files

**Use for**: Development, testing

#### Local Storage Provider

Stores keys in encrypted files on disk.

**Pros**: More secure than configuration, automatic key generation
**Cons**: Keys on local disk, not suitable for distributed systems

**Use for**: Single-server deployments

#### Azure Key Vault Provider

Stores keys in Azure Key Vault.

**Pros**: Highly secure, centralized key management, audit logging
**Cons**: Requires Azure subscription, additional cost

**Use for**: Production deployments on Azure

#### AWS Secrets Manager Provider

Stores keys in AWS Secrets Manager.

**Pros**: Highly secure, centralized key management, automatic rotation
**Cons**: Requires AWS account, additional cost

**Use for**: Production deployments on AWS

### Recommended Settings

**Development Environment:**
```json
{
  "Provider": "Configuration",
  "EnableKeyRotation": false
}
```

**Production Environment:**
```json
{
  "Provider": "AzureKeyVault",
  "EnableKeyRotation": true,
  "KeyRotationDays": 90,
  "AlertOnRotationDue": true
}
```

---

## Legacy Audit Service Configuration

The `LegacyAuditService` section controls backward compatibility with the existing audit log UI.

### Configuration Options

```json
{
  "LegacyAuditService": {
    "Enabled": true,
    "ModuleMappings": {
      "SYS_USERS": "User Management",
      "SYS_COMPANY": "Company Management",
      "SYS_BRANCH": "Branch Management",
      "SYS_ROLE": "Role Management",
      "SYS_CURRENCY": "Currency Management",
      "TICKET": "Ticket System",
      "TICKET_SUPPORT": "Support System",
      "Default": "System"
    },
    "DeviceIdentifierPatterns": {
      "Mobile": "Mobile Device",
      "Tablet": "Tablet Device",
      "Desktop": "Desktop",
      "API": "API Client",
      "Default": "Unknown Device"
    },
    "ErrorCodePrefixes": {
      "Database": "DB",
      "Validation": "VAL",
      "Authentication": "AUTH",
      "Authorization": "AUTHZ",
      "NotFound": "NF",
      "Conflict": "CONF",
      "Default": "ERR"
    },
    "StatusWorkflow": {
      "DefaultStatus": "Unresolved",
      "AllowedTransitions": {
        "Unresolved": ["In Progress", "Resolved", "Critical"],
        "In Progress": ["Resolved", "Unresolved", "Critical"],
        "Critical": ["In Progress", "Resolved"],
        "Resolved": ["Unresolved"]
      }
    }
  }
}
```

### Parameter Descriptions

| Parameter | Type | Description |
|-----------|------|-------------|
| `Enabled` | boolean | Enable legacy audit service |
| `ModuleMappings` | object | Map entity types to business modules |
| `DeviceIdentifierPatterns` | object | Map user agent patterns to device types |
| `ErrorCodePrefixes` | object | Map exception types to error code prefixes |
| `StatusWorkflow.DefaultStatus` | string | Default status for new audit entries |
| `StatusWorkflow.AllowedTransitions` | object | Allowed status transitions |

### Status Workflow

The legacy audit service supports the following statuses:

- **Unresolved**: New error, not yet investigated
- **In Progress**: Error is being investigated
- **Resolved**: Error has been fixed
- **Critical**: Critical error requiring immediate attention

---

## Audit Query Configuration

The `AuditQuery` section controls audit log querying and export capabilities.

### Configuration Options

```json
{
  "AuditQuery": {
    "MaxPageSize": 1000,
    "DefaultPageSize": 50,
    "MaxDateRangeDays": 365,
    "QueryTimeoutSeconds": 30,
    "EnableFullTextSearch": false,
    "FullTextSearchMinLength": 3,
    "EnableParallelQueries": false,
    "MaxParallelDegree": 4,
    "ExportMaxRecords": 100000,
    "ExportTimeoutMinutes": 10
  }
}
```

### Audit Query Caching Configuration

```json
{
  "AuditQueryCaching": {
    "Enabled": false,
    "CacheDurationMinutes": 5,
    "RedisConnectionString": "localhost:6379",
    "CacheKeyPrefix": "audit_query:",
    "MaxCachedResultSizeKB": 1024,
    "EnableSlidingExpiration": true,
    "CacheCommonQueries": true,
    "CommonQueryCacheDurationMinutes": 15
  }
}
```

### Parameter Descriptions

| Parameter | Type | Default | Description |
|-----------|------|---------|-------------|
| `MaxPageSize` | integer | `1000` | Maximum page size for queries |
| `DefaultPageSize` | integer | `50` | Default page size |
| `MaxDateRangeDays` | integer | `365` | Maximum date range for queries |
| `QueryTimeoutSeconds` | integer | `30` | Timeout for query execution |
| `EnableFullTextSearch` | boolean | `false` | Enable full-text search (requires Oracle Text) |
| `FullTextSearchMinLength` | integer | `3` | Minimum search term length |
| `EnableParallelQueries` | boolean | `false` | Enable parallel query execution |
| `MaxParallelDegree` | integer | `4` | Maximum parallel degree |
| `ExportMaxRecords` | integer | `100000` | Maximum records for export |
| `ExportTimeoutMinutes` | integer | `10` | Timeout for export operations |

---

## Health Checks Configuration

The `HealthChecks` section controls health check endpoints for monitoring.

### Configuration Options

```json
{
  "HealthChecks": {
    "Enabled": true,
    "AuditLogging": {
      "Enabled": true,
      "TimeoutSeconds": 5,
      "FailureThreshold": 3
    },
    "Database": {
      "Enabled": true,
      "TimeoutSeconds": 10
    },
    "Redis": {
      "Enabled": false,
      "TimeoutSeconds": 5
    },
    "ExternalStorage": {
      "Enabled": false,
      "TimeoutSeconds": 10
    }
  }
}
```

### Health Check Endpoints

- **GET /health**: Overall system health
- **GET /health/ready**: Readiness probe (for Kubernetes)
- **GET /health/live**: Liveness probe (for Kubernetes)

### Health Check Response

```json
{
  "status": "Healthy",
  "totalDuration": "00:00:00.1234567",
  "entries": {
    "audit_logging": {
      "status": "Healthy",
      "duration": "00:00:00.0123456",
      "data": {
        "queueDepth": 42,
        "circuitBreakerState": "Closed"
      }
    },
    "database": {
      "status": "Healthy",
      "duration": "00:00:00.0234567"
    }
  }
}
```

---

## Environment-Specific Configuration

### Development Environment

**appsettings.Development.json**

```json
{
  "AuditLogging": {
    "BatchSize": 10,
    "BatchWindowMs": 50,
    "EnableCircuitBreaker": false,
    "EnableRetryPolicy": false,
    "EncryptSensitiveData": false
  },
  "RequestTracing": {
    "PayloadLoggingLevel": "Full",
    "LogRequestStart": true,
    "IncludeHeaders": true
  },
  "PerformanceMonitoring": {
    "SlowRequestThresholdMs": 500,
    "SlowQueryThresholdMs": 200,
    "MetricsRetentionHours": 1
  },
  "SecurityMonitoring": {
    "FailedLoginThreshold": 10,
    "AutoBlockSuspiciousIps": false,
    "EnableVerboseLogging": true
  },
  "Archival": {
    "Enabled": false,
    "RunOnStartup": false
  },
  "Alerting": {
    "Enabled": false
  },
  "ComplianceReporting": {
    "ScheduledReports": {
      "Enabled": false
    }
  },
  "AuditEncryption": {
    "Enabled": false
  },
  "KeyManagement": {
    "Provider": "Configuration",
    "EnableKeyRotation": false
  }
}
```

### Production Environment

**appsettings.Production.json**

```json
{
  "AuditLogging": {
    "BatchSize": 100,
    "BatchWindowMs": 200,
    "EnableCircuitBreaker": true,
    "EnableRetryPolicy": true,
    "EncryptSensitiveData": true,
    "EnableFileSystemFallback": true
  },
  "RequestTracing": {
    "PayloadLoggingLevel": "MetadataOnly",
    "LogRequestStart": false,
    "MaxPayloadSize": 5120
  },
  "PerformanceMonitoring": {
    "SlowRequestThresholdMs": 2000,
    "SlowQueryThresholdMs": 1000,
    "MetricsRetentionHours": 24,
    "AggregateMetricsHourly": true
  },
  "SecurityMonitoring": {
    "FailedLoginThreshold": 5,
    "AutoBlockSuspiciousIps": true,
    "IpBlockDurationMinutes": 120,
    "UseRedisCache": true,
    "SendEmailAlerts": true
  },
  "Archival": {
    "Enabled": true,
    "Schedule": "0 2 * * *",
    "CompressionEnabled": true,
    "VerifyIntegrity": true,
    "ExternalStorage": {
      "Enabled": true,
      "Provider": "S3"
    }
  },
  "Alerting": {
    "Enabled": true,
    "MaxAlertsPerRulePerHour": 10,
    "Email": {
      "Enabled": true
    },
    "Webhook": {
      "Enabled": true
    }
  },
  "ComplianceReporting": {
    "ScheduledReports": {
      "Enabled": true
    }
  },
  "AuditEncryption": {
    "Enabled": true,
    "EncryptOldValue": true,
    "EncryptNewValue": true
  },
  "AuditIntegrity": {
    "Enabled": true,
    "AutoGenerateHashes": true,
    "AlertOnTampering": true
  },
  "KeyManagement": {
    "Provider": "AzureKeyVault",
    "EnableKeyRotation": true,
    "KeyRotationDays": 90,
    "AlertOnRotationDue": true
  },
  "AuditQueryCaching": {
    "Enabled": true,
    "CacheDurationMinutes": 5,
    "CacheCommonQueries": true
  }
}
```

### Staging Environment

**appsettings.Staging.json**

```json
{
  "AuditLogging": {
    "BatchSize": 75,
    "BatchWindowMs": 150,
    "EnableCircuitBreaker": true,
    "EnableRetryPolicy": true,
    "EncryptSensitiveData": true
  },
  "RequestTracing": {
    "PayloadLoggingLevel": "Full",
    "MaxPayloadSize": 10240
  },
  "PerformanceMonitoring": {
    "SlowRequestThresholdMs": 1000,
    "SlowQueryThresholdMs": 500
  },
  "SecurityMonitoring": {
    "FailedLoginThreshold": 5,
    "AutoBlockSuspiciousIps": false,
    "SendEmailAlerts": false
  },
  "Archival": {
    "Enabled": true,
    "Schedule": "0 3 * * *"
  },
  "Alerting": {
    "Enabled": true,
    "Email": {
      "Enabled": false
    }
  },
  "KeyManagement": {
    "Provider": "LocalStorage",
    "EnableKeyRotation": false
  }
}
```

---

## Configuration Validation

### Validation Rules

The traceability system validates configuration on startup and logs warnings for invalid settings.

#### Required Configuration

The following configuration must be provided:

1. **Database Connection String**: `ConnectionStrings:OracleDb`
2. **JWT Settings**: `JwtSettings:SecretKey`, `JwtSettings:Issuer`, `JwtSettings:Audience`

#### Optional but Recommended

1. **Encryption Key**: `AuditEncryption:Key` (if encryption enabled)
2. **Signing Key**: `AuditIntegrity:SigningKey` (if integrity enabled)
3. **SMTP Settings**: `Alerting:Email:*` (if email alerts enabled)
4. **External Storage**: `Archival:ExternalStorage:*` (if external storage enabled)

### Validation Errors

Common validation errors and solutions:

| Error | Cause | Solution |
|-------|-------|----------|
| `AuditEncryption:Key is required when encryption is enabled` | Encryption enabled but no key provided | Generate and configure encryption key |
| `AuditIntegrity:SigningKey is required when integrity is enabled` | Integrity enabled but no key provided | Generate and configure signing key |
| `Invalid cron expression in Archival:Schedule` | Invalid cron format | Use valid cron expression (e.g., `"0 2 * * *"`) |
| `BatchSize must be between 1 and 1000` | Invalid batch size | Set batch size between 1 and 1000 |
| `MaxQueueSize must be greater than BatchSize` | Queue size too small | Increase `MaxQueueSize` |
| `SMTP settings incomplete` | Missing SMTP configuration | Provide all SMTP settings or disable email alerts |

### Configuration Validation Tool

Use the following endpoint to validate configuration:

```bash
GET /api/monitoring/configuration/validate
```

Response:

```json
{
  "isValid": true,
  "errors": [],
  "warnings": [
    "AuditEncryption is disabled. Consider enabling encryption in production."
  ],
  "recommendations": [
    "Increase BatchSize to 100 for better performance in production."
  ]
}
```

---

## Troubleshooting

### Common Issues and Solutions

#### Issue: Audit Logging Not Working

**Symptoms:**
- No audit logs appearing in database
- Health check shows audit logging as unhealthy

**Possible Causes:**
1. `AuditLogging:Enabled` is `false`
2. Database connection issues
3. Circuit breaker is open
4. Queue is full

**Solutions:**
1. Check `AuditLogging:Enabled` is `true`
2. Verify database connection string
3. Check circuit breaker state: `GET /api/monitoring/health`
4. Check queue depth: `GET /api/monitoring/health`
5. Review logs for errors: `Logs/log-*.txt`

#### Issue: High Memory Usage

**Symptoms:**
- Application memory usage increasing
- Out of memory exceptions

**Possible Causes:**
1. `MaxQueueSize` too large
2. Audit events not being processed
3. Memory leak in application code

**Solutions:**
1. Reduce `MaxQueueSize` to 5000
2. Check if database is slow or unavailable
3. Enable backpressure: ensure `MaxQueueSize` is set
4. Monitor queue depth: `GET /api/monitoring/health`
5. Restart application if memory leak suspected

#### Issue: Slow API Performance

**Symptoms:**
- API requests taking longer than expected
- High latency in performance metrics

**Possible Causes:**
1. Audit logging adding too much overhead
2. Synchronous audit writes
3. Database slow or overloaded

**Solutions:**
1. Verify audit writes are asynchronous
2. Increase `BatchSize` and `BatchWindowMs`
3. Disable payload logging: `RequestTracing:LogPayloads = false`
4. Reduce `MaxPayloadSize` to 5120
5. Check database performance
6. Consider disabling audit logging temporarily

#### Issue: Circuit Breaker Open

**Symptoms:**
- Audit logs not being written
- Health check shows circuit breaker open
- Fallback storage being used

**Possible Causes:**
1. Database unavailable or slow
2. Too many database errors
3. Network issues

**Solutions:**
1. Check database connectivity
2. Review database logs for errors
3. Wait for circuit breaker to close (60 seconds by default)
4. Increase `CircuitBreakerFailureThreshold` if transient errors
5. Check fallback directory for queued events: `AuditFallback/`

#### Issue: Alerts Not Being Sent

**Symptoms:**
- No alert emails received
- Alert history shows failed deliveries

**Possible Causes:**
1. `Alerting:Enabled` is `false`
2. SMTP settings incorrect
3. Rate limiting preventing alerts
4. Network issues

**Solutions:**
1. Check `Alerting:Enabled` is `true`
2. Verify SMTP settings: host, port, credentials
3. Check rate limit: `MaxAlertsPerRulePerHour`
4. Test SMTP connectivity
5. Review alert history: `GET /api/alerts/history`

#### Issue: Archival Not Running

**Symptoms:**
- Old audit data not being archived
- Database growing too large

**Possible Causes:**
1. `Archival:Enabled` is `false`
2. Invalid cron schedule
3. Archival process timing out
4. Database transaction issues

**Solutions:**
1. Check `Archival:Enabled` is `true`
2. Verify cron schedule is valid
3. Increase `Archival:TimeoutMinutes`
4. Reduce `Archival:BatchSize` to 500
5. Check archival logs for errors
6. Run archival manually: `POST /api/archival/run`

#### Issue: Query Performance Slow

**Symptoms:**
- Audit log queries taking too long
- Query timeout errors

**Possible Causes:**
1. Large date range
2. Missing database indexes
3. Too many records
4. No query caching

**Solutions:**
1. Reduce date range to 30 days or less
2. Verify database indexes exist (see Database Design section)
3. Enable query caching: `AuditQueryCaching:Enabled = true`
4. Use pagination with smaller page sizes
5. Consider archiving old data
6. Run database statistics: `EXEC DBMS_STATS.GATHER_TABLE_STATS('THINKON_ERP', 'SYS_AUDIT_LOG')`

#### Issue: Sensitive Data Not Masked

**Symptoms:**
- Passwords or tokens visible in audit logs
- Compliance violation

**Possible Causes:**
1. Field name not in `SensitiveFields` list
2. Masking not working correctly
3. Encryption disabled

**Solutions:**
1. Add field name to `AuditLogging:SensitiveFields`
2. Verify `MaskingPattern` is set
3. Enable encryption: `AuditEncryption:Enabled = true`
4. Review audit logs to confirm masking
5. Update existing logs if needed

### Diagnostic Endpoints

Use these endpoints to diagnose issues:

```bash
# System health
GET /api/monitoring/health

# Audit logging status
GET /api/monitoring/audit-status

# Performance metrics
GET /api/monitoring/performance/endpoint?endpoint=/api/users&period=1h

# Slow requests
GET /api/monitoring/performance/slow-requests?thresholdMs=1000&limit=100

# Slow queries
GET /api/monitoring/performance/slow-queries?thresholdMs=500&limit=100

# Security threats
GET /api/monitoring/security/threats

# Alert history
GET /api/alerts/history?pageNumber=1&pageSize=50

# Configuration validation
GET /api/monitoring/configuration/validate
```

### Logging

The traceability system logs to the following locations:

- **Console**: Real-time logs during development
- **File**: `Logs/log-YYYYMMDD.txt` (rotated daily, retained 30 days)
- **Fallback Storage**: `AuditFallback/` (when database unavailable)

### Log Levels

- **Trace**: Very detailed diagnostic information
- **Debug**: Detailed diagnostic information
- **Information**: General informational messages
- **Warning**: Warning messages for potential issues
- **Error**: Error messages for failures
- **Critical**: Critical failures requiring immediate attention

### Enabling Verbose Logging

For troubleshooting, enable verbose logging:

```json
{
  "Serilog": {
    "MinimumLevel": {
      "Default": "Debug",
      "Override": {
        "ThinkOnErp.Infrastructure.Services.AuditLogger": "Trace",
        "ThinkOnErp.API.Middleware.RequestTracingMiddleware": "Trace"
      }
    }
  },
  "AuditLogging": {
    "EnableVerboseLogging": true
  },
  "SecurityMonitoring": {
    "EnableVerboseLogging": true
  }
}
```

---

## Performance Tuning Guide

### High-Volume Scenarios (>10,000 req/min)

```json
{
  "AuditLogging": {
    "BatchSize": 200,
    "BatchWindowMs": 500,
    "MaxQueueSize": 20000
  },
  "RequestTracing": {
    "PayloadLoggingLevel": "MetadataOnly",
    "LogRequestStart": false
  },
  "PerformanceMonitoring": {
    "SlowRequestThresholdMs": 2000,
    "MetricsRetentionHours": 12
  }
}
```

### Low-Latency Requirements (<5ms overhead)

```json
{
  "AuditLogging": {
    "BatchSize": 20,
    "BatchWindowMs": 50,
    "MaxQueueSize": 5000
  },
  "RequestTracing": {
    "LogPayloads": false,
    "LogRequestStart": false
  },
  "PerformanceMonitoring": {
    "TrackMemoryMetrics": false,
    "TrackCpuMetrics": false
  }
}
```

### Memory-Constrained Environments

```json
{
  "AuditLogging": {
    "BatchSize": 30,
    "MaxQueueSize": 3000
  },
  "PerformanceMonitoring": {
    "MetricsRetentionHours": 1,
    "SlidingWindowSizeMinutes": 15
  },
  "AuditQueryCaching": {
    "Enabled": false
  }
}
```

### Database-Constrained Environments

```json
{
  "AuditLogging": {
    "BatchSize": 100,
    "BatchWindowMs": 1000,
    "EnableCircuitBreaker": true,
    "EnableRetryPolicy": true,
    "MaxRetryAttempts": 5
  },
  "Archival": {
    "BatchSize": 500,
    "TransactionTimeoutSeconds": 60
  }
}
```

---

## Security Hardening

### Production Security Checklist

- [ ] Enable encryption: `AuditEncryption:Enabled = true`
- [ ] Enable integrity signing: `AuditIntegrity:Enabled = true`
- [ ] Use Azure Key Vault or AWS Secrets Manager for keys
- [ ] Enable key rotation: `KeyManagement:EnableKeyRotation = true`
- [ ] Disable payload logging: `RequestTracing:PayloadLoggingLevel = "MetadataOnly"`
- [ ] Enable auto-blocking: `SecurityMonitoring:AutoBlockSuspiciousIps = true`
- [ ] Configure alert recipients: `Alerting:Email:DefaultRecipients`
- [ ] Enable external storage: `Archival:ExternalStorage:Enabled = true`
- [ ] Restrict health check endpoints to internal network
- [ ] Use HTTPS for all API endpoints
- [ ] Enable RBAC for audit data access
- [ ] Review and update `SensitiveFields` list
- [ ] Configure firewall rules for database access
- [ ] Enable database connection encryption
- [ ] Implement network segmentation
- [ ] Regular security audits and penetration testing

---

## Migration Guide

### Upgrading from Legacy Audit System

1. **Backup existing audit data**
   ```sql
   CREATE TABLE SYS_AUDIT_LOG_BACKUP AS SELECT * FROM SYS_AUDIT_LOG;
   ```

2. **Run database migration script**
   ```sql
   -- See Database/Scripts/13_Extend_SYS_AUDIT_LOG_For_Traceability.sql
   ```

3. **Update configuration**
   - Add new configuration sections to `appsettings.json`
   - Generate encryption and signing keys
   - Configure alert recipients

4. **Test in staging environment**
   - Verify audit logs are being written
   - Test query performance
   - Verify alerts are working
   - Test archival process

5. **Deploy to production**
   - Deploy during low-traffic period
   - Monitor system health
   - Verify audit logs are being captured
   - Check for any errors in logs

6. **Post-deployment verification**
   - Run health checks: `GET /api/monitoring/health`
   - Verify audit logs: `GET /api/audit-logs/query`
   - Check performance metrics: `GET /api/monitoring/performance/endpoint`
   - Test alert delivery

---

## Support and Resources

### Documentation

- **API Documentation**: `/swagger`
- **Operational Runbooks**: `docs/OPERATIONAL_RUNBOOKS.md`
- **APM Configuration**: `docs/APM_CONFIGURATION_GUIDE.md`
- **Design Document**: `.kiro/specs/full-traceability-system/design.md`
- **Requirements**: `.kiro/specs/full-traceability-system/requirements.md`

### Monitoring Dashboards

- **System Health**: `GET /api/monitoring/health`
- **Performance Metrics**: `GET /api/monitoring/performance/endpoint`
- **Security Threats**: `GET /api/monitoring/security/threats`
- **Alert History**: `GET /api/alerts/history`

### Contact

For support or questions:
- **Email**: support@thinkonerp.com
- **Documentation**: https://docs.thinkonerp.com
- **Issue Tracker**: https://github.com/thinkonerp/api/issues

---

## Appendix: Complete Configuration Example

### Complete appsettings.json for Production

```json
{
  "ConnectionStrings": {
    "OracleDb": "Data Source=(DESCRIPTION=(ADDRESS=(PROTOCOL=TCP)(HOST=prod-db.example.com)(PORT=1521))(CONNECT_DATA=(SERVICE_NAME=PROD)));User Id=THINKON_ERP;Password=***;Pooling=true;Min Pool Size=10;Max Pool Size=200;",
    "Redis": "prod-redis.example.com:6379,password=***,ssl=true,abortConnect=false"
  },
  "JwtSettings": {
    "SecretKey": "***",
    "Issuer": "ThinkOnErpAPI",
    "Audience": "ThinkOnErpClient",
    "ExpiryInMinutes": 60,
    "RefreshTokenExpiryInDays": 7
  },
  "AuditLogging": {
    "Enabled": true,
    "BatchSize": 100,
    "BatchWindowMs": 200,
    "MaxQueueSize": 20000,
    "SensitiveFields": ["password", "token", "refreshToken", "accessToken", "creditCard", "cvv", "ssn", "taxId", "bankAccount"],
    "MaskingPattern": "***MASKED***",
    "MaxPayloadSize": 5120,
    "EnableCircuitBreaker": true,
    "EnableRetryPolicy": true,
    "EncryptSensitiveData": true,
    "EnableFileSystemFallback": true
  },
  "RequestTracing": {
    "Enabled": true,
    "PayloadLoggingLevel": "MetadataOnly",
    "MaxPayloadSize": 5120,
    "ExcludedPaths": ["/health", "/metrics", "/swagger"]
  },
  "PerformanceMonitoring": {
    "Enabled": true,
    "SlowRequestThresholdMs": 2000,
    "SlowQueryThresholdMs": 1000,
    "AggregateMetricsHourly": true
  },
  "SecurityMonitoring": {
    "Enabled": true,
    "FailedLoginThreshold": 5,
    "AutoBlockSuspiciousIps": true,
    "UseRedisCache": true,
    "SendEmailAlerts": true
  },
  "Archival": {
    "Enabled": true,
    "Schedule": "0 2 * * *",
    "CompressionEnabled": true,
    "VerifyIntegrity": true,
    "ExternalStorage": {
      "Enabled": true,
      "Provider": "S3",
      "S3": {
        "BucketName": "thinkonerp-audit-archive",
        "Region": "us-east-1",
        "UseServerSideEncryption": true
      }
    }
  },
  "Alerting": {
    "Enabled": true,
    "Email": {
      "Enabled": true,
      "SmtpHost": "smtp.example.com",
      "SmtpPort": 587,
      "SmtpUseSsl": true,
      "DefaultRecipients": ["admin@thinkonerp.com", "security@thinkonerp.com"]
    },
    "Webhook": {
      "Enabled": true,
      "DefaultUrl": "https://hooks.example.com/alerts"
    }
  },
  "ComplianceReporting": {
    "Enabled": true,
    "ScheduledReports": {
      "Enabled": true
    }
  },
  "AuditEncryption": {
    "Enabled": true,
    "EncryptOldValue": true,
    "EncryptNewValue": true
  },
  "AuditIntegrity": {
    "Enabled": true,
    "AutoGenerateHashes": true,
    "AlertOnTampering": true
  },
  "KeyManagement": {
    "Provider": "AzureKeyVault",
    "EnableKeyRotation": true,
    "KeyRotationDays": 90,
    "AzureKeyVault": {
      "VaultUrl": "https://thinkonerp-keyvault.vault.azure.net/",
      "AuthenticationMethod": "ManagedIdentity"
    }
  },
  "AuditQueryCaching": {
    "Enabled": true,
    "CacheDurationMinutes": 5,
    "CacheCommonQueries": true
  },
  "HealthChecks": {
    "Enabled": true,
    "AuditLogging": {
      "Enabled": true
    },
    "Database": {
      "Enabled": true
    },
    "Redis": {
      "Enabled": true
    }
  }
}
```

---

**Document Version**: 1.0  
**Last Updated**: 2024-01-15  
**Maintained By**: ThinkOnErp Development Team

