# AddTraceabilitySystem Extension Method

## Overview

The `AddTraceabilitySystem()` extension method provides a comprehensive, one-line registration of all traceability system services required for audit logging, monitoring, compliance, and security in the ThinkOnErp API.

## Usage

```csharp
// In Program.cs or Startup.cs
services.AddTraceabilitySystem(configuration);
```

## What It Registers

### 1. Audit Logging Services

| Service | Interface | Lifetime | Purpose |
|---------|-----------|----------|---------|
| `AuditLogger` | `IAuditLogger` | Singleton | Core audit logging with async queue processing |
| `AuditRepository` | `IAuditRepository` | Scoped | Database operations for audit logs |
| `LegacyAuditService` | `ILegacyAuditService` | Scoped | Backward compatibility with legacy audit format |
| `AuditTrailService` | `IAuditTrailService` | Scoped | Compliance tracking and audit trails |

**Why these lifetimes?**
- `AuditLogger` is Singleton because it manages a shared async queue and background processing
- Repository and service classes are Scoped to align with request-scoped database contexts

### 2. Monitoring Services

| Service | Interface | Lifetime | Purpose |
|---------|-----------|----------|---------|
| `PerformanceMonitor` | `IPerformanceMonitor` | Singleton | In-memory metrics aggregation and percentile calculations |
| `MemoryMonitor` | `IMemoryMonitor` | Singleton | System-wide memory usage tracking |
| `SecurityMonitor` | `ISecurityMonitor` | Scoped | Request-specific threat detection |
| `SlowQueryRepository` | `ISlowQueryRepository` | Scoped | Database operations for slow query tracking |

**Why these lifetimes?**
- Performance and Memory monitors are Singleton for system-wide metric aggregation
- SecurityMonitor is Scoped to access request-specific context (user, IP, etc.)

### 3. Compliance Services

| Service | Interface | Lifetime | Purpose |
|---------|-----------|----------|---------|
| `ComplianceReporter` | `IComplianceReporter` | Scoped | GDPR, SOX, ISO 27001 report generation |

**Why Scoped?**
- Compliance reports often need request context (user permissions, company scope)

### 4. Query Services

| Service | Interface | Lifetime | Purpose |
|---------|-----------|----------|---------|
| `AuditQueryService` | `IAuditQueryService` | Scoped | Efficient audit log querying with filtering |

**Why Scoped?**
- Query service needs database context and user authorization context

### 5. Archival Services

| Service | Interface | Lifetime | Purpose |
|---------|-----------|----------|---------|
| `ArchivalService` | `IArchivalService` | Scoped | Data retention and cold storage management |
| `CompressionService` | `ICompressionService` | Scoped | GZip compression for archived data |
| `ExternalStorageProviderFactory` | `IExternalStorageProviderFactory` | Singleton | Factory for S3/Azure Blob storage providers |

**Why these lifetimes?**
- Archival and Compression services are Scoped for database transaction management
- Factory is Singleton as it's stateless and creates providers on demand

### 6. Alert Services

| Service | Interface | Lifetime | Purpose |
|---------|-----------|----------|---------|
| `AlertManager` | `IAlertManager` | Singleton | Alert rule management and notification coordination |
| `EmailNotificationService` | `IEmailNotificationChannel` | Singleton | SMTP email notifications |
| `WebhookNotificationService` | `IWebhookNotificationChannel` | Singleton | HTTP webhook notifications |
| `SmsNotificationService` | `ISmsNotificationChannel` | Singleton | Twilio SMS notifications |
| `Channel<AlertNotificationTask>` | - | Singleton | Shared async queue for alert processing |

**Why Singleton?**
- Alert services are stateless and can be shared across all requests
- Shared channel enables efficient background processing of notifications

### 7. Helper Services

| Service | Interface | Lifetime | Purpose |
|---------|-----------|----------|---------|
| `SensitiveDataMasker` | `ISensitiveDataMasker` | Scoped | Mask passwords, tokens, PII in audit logs |
| `AuditContextProvider` | `IAuditContextProvider` | Scoped | Capture request context (user, IP, headers) |
| `ExceptionCategorizationService` | `IExceptionCategorizationService` | Scoped | Classify exceptions by severity |
| `MultiTenantAccessService` | `IMultiTenantAccessService` | Scoped | Enforce multi-tenant data isolation |

**Why Scoped?**
- All helper services need access to request-specific context

### 8. Security Services

| Service | Interface | Lifetime | Purpose |
|---------|-----------|----------|---------|
| `AuditDataEncryption` | `IAuditDataEncryption` | Singleton | AES-256 encryption for sensitive audit data |
| `AuditLogIntegrityService` | `IAuditLogIntegrityService` | Singleton | Cryptographic signatures for tamper detection |
| `KeyManagementService` | `IKeyManagementService` | Singleton | Encryption and signing key management |
| `KeyManagementCli` | - | Scoped | CLI tool for key generation and rotation |

**Why these lifetimes?**
- Encryption and integrity services are Singleton for performance (key caching)
- KeyManagementCli is Scoped as it's used in request-scoped scenarios

### 9. Background Services

| Service | Type | Purpose |
|---------|------|---------|
| `MetricsAggregationBackgroundService` | Hosted Service | Hourly metrics rollups |
| `AlertProcessingBackgroundService` | Hosted Service | Async notification delivery |
| `ScheduledReportGenerationService` | Hosted Service | Scheduled compliance reports |
| `ArchivalBackgroundService` | Hosted Service | Data retention enforcement |
| `KeyRotationBackgroundService` | Hosted Service | Automatic key rotation |

**Note:** Background services are registered as Hosted Services and run continuously in the background.

### 10. Resilience Services

| Service | Interface | Lifetime | Purpose |
|---------|-----------|----------|---------|
| `CircuitBreakerRegistry` | - | Singleton | Manages circuit breakers for all operations |
| `RetryPolicy` | - | Scoped | Retry logic for transient failures |
| `CircuitBreaker` | - | Scoped | Circuit breaker for individual operations |
| `ResilientDatabaseExecutor` | - | Scoped | Combines retry + circuit breaker for DB ops |
| `AuditCommandInterceptor` | - | Scoped | Intercepts DB commands for auditing |

**Why these lifetimes?**
- CircuitBreakerRegistry is Singleton to track state across all requests
- Other resilience services are Scoped to align with database context

### 11. Configuration and Caching

The method also:
- Validates all configuration options using data annotations
- Configures Redis distributed cache if enabled for security monitoring or audit query caching
- Registers HTTP client for webhook notifications

## Configuration Requirements

The method requires the following configuration sections:

```json
{
  "AuditLogging": {
    "Enabled": true,
    "BatchSize": 50,
    "BatchWindowMs": 100,
    "MaxQueueSize": 10000,
    "CircuitBreakerFailureThreshold": 5,
    "CircuitBreakerTimeoutSeconds": 60,
    "RetryMaxAttempts": 3,
    "RetryDelayMs": 1000
  },
  "SecurityMonitoring": {
    "UseRedisCache": false,
    "RedisConnectionString": "localhost:6379"
  },
  "AuditQueryCaching": {
    "Enabled": false,
    "RedisConnectionString": "localhost:6379"
  },
  "KeyManagement": {
    "Provider": "Configuration",
    "EncryptionKey": "base64-encoded-key",
    "SigningKey": "base64-encoded-key"
  }
}
```

## Service Lifetime Summary

| Lifetime | Count | Services |
|----------|-------|----------|
| **Singleton** | 13 | AuditLogger, PerformanceMonitor, MemoryMonitor, AlertManager, Notification Channels (3), AuditDataEncryption, AuditLogIntegrityService, KeyManagementService, ExternalStorageProviderFactory, CircuitBreakerRegistry, Alert Channel |
| **Scoped** | 18 | All repositories, query services, compliance services, helper services, resilience services (except registry) |
| **Hosted** | 6 | All background services (metrics, alerts, reports, archival, key rotation, audit logger) |

## Integration with AddInfrastructure

The `AddTraceabilitySystem()` method is designed to be called independently or as part of `AddInfrastructure()`. It can be used in two ways:

### Option 1: Standalone (Recommended for new projects)
```csharp
services.AddTraceabilitySystem(configuration);
```

### Option 2: As part of AddInfrastructure (Current implementation)
```csharp
services.AddInfrastructure(configuration); // Includes traceability system
```

## Testing

Unit tests are provided in `AddTraceabilitySystemTests.cs` to verify:
- All services are registered
- Services have correct lifetimes
- Configuration validation works
- Redis is configured when enabled
- Method can be called multiple times safely

## Performance Considerations

- **Singleton services** are instantiated once and shared across all requests (efficient)
- **Scoped services** are instantiated per request (necessary for request context)
- **Background services** run continuously and don't impact request processing
- **Async queue** in AuditLogger ensures audit logging doesn't block API requests

## Compliance

This registration method supports:
- **GDPR**: Personal data access tracking and audit trails
- **SOX**: Financial data access controls and segregation of duties
- **ISO 27001**: Security event monitoring and incident response

## Troubleshooting

### Issue: Services not resolving
**Solution**: Ensure `AddTraceabilitySystem()` is called before `BuildServiceProvider()`

### Issue: Redis connection errors
**Solution**: Set `UseRedisCache: false` in configuration if Redis is not available

### Issue: Key management errors
**Solution**: Ensure encryption and signing keys are configured in `KeyManagement` section

### Issue: Background services not starting
**Solution**: Ensure the application is running as a hosted service (not just a console app)

## Related Documentation

- [Audit Logging Design](../../.kiro/specs/full-traceability-system/design.md)
- [Traceability Requirements](../../.kiro/specs/full-traceability-system/requirements.md)
- [Configuration Guide](./Configuration/README.md)
- [Key Management Guide](./Services/KeyManagement/README.md)
