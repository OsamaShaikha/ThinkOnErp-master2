# Administrator Guide: System Configuration

## Overview

This guide covers configuration of the ThinkOnErp traceability system including audit logging, monitoring, security, alerting, and archival. All configuration is managed via `appsettings.json` with environment-specific overrides.

---

## Configuration File Hierarchy

```
appsettings.json                    ← Base configuration
appsettings.Development.json        ← Development overrides
appsettings.Production.json         ← Production overrides
Environment variables               ← Highest priority overrides
```

---

## 1. Audit Logging

```json
{
  "AuditLogging": {
    "Enabled": true,
    "BatchSize": 50,
    "BatchWindowMs": 100,
    "MaxQueueSize": 10000,
    "SensitiveFields": ["password", "token", "refreshToken", "creditCard", "ssn"],
    "MaskingPattern": "***MASKED***",
    "MaxPayloadSize": 10240,
    "DatabaseTimeoutSeconds": 30,
    "EnableCircuitBreaker": true,
    "CircuitBreakerFailureThreshold": 5,
    "CircuitBreakerTimeoutSeconds": 60
  }
}
```

| Parameter | Description | Default |
|---|---|---|
| `Enabled` | Enable/disable audit logging | `true` |
| `BatchSize` | Events per database batch | `50` |
| `BatchWindowMs` | Max wait before flushing batch | `100` |
| `MaxQueueSize` | Queue capacity (backpressure limit) | `10000` |
| `SensitiveFields` | Field names to mask in payloads | See above |
| `MaxPayloadSize` | Max captured request/response size (bytes) | `10240` |

---

## 2. Request Tracing

```json
{
  "RequestTracing": {
    "Enabled": true,
    "CaptureRequestBody": true,
    "CaptureResponseBody": true,
    "ExcludedPaths": ["/health", "/metrics", "/swagger"],
    "PopulateLegacyFields": true,
    "CorrelationIdHeader": "X-Correlation-ID"
  }
}
```

---

## 3. Performance Monitoring

```json
{
  "PerformanceMonitoring": {
    "Enabled": true,
    "SlowRequestThresholdMs": 5000,
    "SlowQueryThresholdMs": 2000,
    "MetricsWindowMinutes": 60,
    "AggregationIntervalMinutes": 60,
    "MaxMetricsInMemory": 10000,
    "EnablePercentileCalculation": true
  }
}
```

---

## 4. Security Monitoring

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
    "AnomalyDetectionSensitivity": 0.8
  }
}
```

---

## 5. Alerting

```json
{
  "Alerting": {
    "Enabled": true,
    "ProcessingIntervalSeconds": 30,
    "RateLimitPerMinute": 10,
    "Email": {
      "Enabled": true,
      "SmtpServer": "smtp.example.com",
      "SmtpPort": 587,
      "UseSsl": true,
      "FromAddress": "alerts@thinkonerp.com",
      "Username": "smtp-user",
      "Password": "<from-env-var>"
    },
    "Sms": {
      "Enabled": false,
      "AccountSid": "<twilio-sid>",
      "AuthToken": "<twilio-token>",
      "FromNumber": "+1234567890"
    },
    "Webhook": {
      "Enabled": true,
      "DefaultUrl": "https://hooks.example.com/alerts",
      "TimeoutSeconds": 10,
      "MaxRetries": 3
    }
  }
}
```

---

## 6. Data Archival

```json
{
  "Archival": {
    "Enabled": true,
    "RunIntervalHours": 24,
    "BatchSize": 1000,
    "CompressionLevel": "Optimal",
    "ExternalStorage": {
      "Provider": "S3",
      "BucketName": "thinkonerp-audit-archive",
      "Region": "us-east-1"
    },
    "DefaultRetentionDays": 365
  }
}
```

---

## 7. Encryption & Integrity

```json
{
  "AuditEncryption": {
    "Enabled": true,
    "Algorithm": "AES-256-CBC"
  },
  "AuditIntegrity": {
    "Enabled": true,
    "SigningAlgorithm": "HMAC-SHA256",
    "EnableHashChain": true
  },
  "KeyManagement": {
    "Provider": "FileSystem",
    "KeyStoragePath": "./keys/",
    "AutoRotation": true,
    "RotationIntervalDays": 90
  }
}
```

---

## 8. Using Environment Variables

Override any setting using double-underscore notation:

```bash
# Override audit batch size
AuditLogging__BatchSize=100

# Override JWT secret (recommended for production)
JwtSettings__SecretKey=<your-secret>

# Override connection string
ConnectionStrings__OracleDb="Data Source=prod-db:1521/ORCL;..."
```

---

## 9. Validation

All configuration classes have validators that run at startup. Invalid configuration will prevent the application from starting with a clear error message.

Check current configuration status:
```http
GET /api/configuration/status
Authorization: Bearer {admin-token}
```
