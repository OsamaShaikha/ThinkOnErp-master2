# Traceability System Service Lifetime Reference

## Quick Reference Table

| # | Service | Interface | Lifetime | Category | Reason |
|---|---------|-----------|----------|----------|--------|
| 1 | `AuditLogger` | `IAuditLogger` | Singleton + Hosted | Audit Logging | Manages shared async queue |
| 2 | `AuditRepository` | `IAuditRepository` | Scoped | Audit Logging | Database transactions |
| 3 | `LegacyAuditService` | `ILegacyAuditService` | Scoped | Audit Logging | Database transactions |
| 4 | `AuditTrailService` | `IAuditTrailService` | Scoped | Audit Logging | Database transactions |
| 5 | `PerformanceMonitor` | `IPerformanceMonitor` | Singleton | Monitoring | System-wide metrics |
| 6 | `MemoryMonitor` | `IMemoryMonitor` | Singleton | Monitoring | System-wide tracking |
| 7 | `SecurityMonitor` | `ISecurityMonitor` | Scoped | Monitoring | Request context needed |
| 8 | `SlowQueryRepository` | `ISlowQueryRepository` | Scoped | Monitoring | Database transactions |
| 9 | `ComplianceReporter` | `IComplianceReporter` | Scoped | Compliance | User authorization context |
| 10 | `AuditQueryService` | `IAuditQueryService` | Scoped | Query | User authorization context |
| 11 | `ArchivalService` | `IArchivalService` | Scoped | Archival | Database transactions |
| 12 | `CompressionService` | `ICompressionService` | Scoped | Archival | Used with scoped services |
| 13 | `ExternalStorageProviderFactory` | `IExternalStorageProviderFactory` | Singleton | Archival | Stateless factory |
| 14 | `AlertManager` | `IAlertManager` | Singleton | Alerts | System-wide rate limiting |
| 15 | `EmailNotificationService` | `IEmailNotificationChannel` | Singleton | Alerts | Stateless SMTP client |
| 16 | `WebhookNotificationService` | `IWebhookNotificationChannel` | Singleton | Alerts | Stateless HTTP client |
| 17 | `SmsNotificationService` | `ISmsNotificationChannel` | Singleton | Alerts | Stateless Twilio client |
| 18 | `Channel<AlertNotificationTask>` | - | Singleton | Alerts | Shared async queue |
| 19 | `SensitiveDataMasker` | `ISensitiveDataMasker` | Scoped | Helper | Request context needed |
| 20 | `AuditContextProvider` | `IAuditContextProvider` | Scoped | Helper | Captures request context |
| 21 | `ExceptionCategorizationService` | `IExceptionCategorizationService` | Scoped | Helper | Request context needed |
| 22 | `MultiTenantAccessService` | `IMultiTenantAccessService` | Scoped | Helper | Tenant isolation per request |
| 23 | `AuditDataEncryption` | `IAuditDataEncryption` | Singleton | Security | Key caching for performance |
| 24 | `AuditLogIntegrityService` | `IAuditLogIntegrityService` | Singleton | Security | Key caching for performance |
| 25 | `KeyManagementService` | `IKeyManagementService` | Singleton | Security | Key lifecycle management |
| 26 | `KeyManagementCli` | - | Scoped | Security | Request-scoped CLI tool |
| 27 | `CircuitBreakerRegistry` | - | Singleton | Resilience | System-wide failure tracking |
| 28 | `RetryPolicy` | - | Scoped | Resilience | Per-request configuration |
| 29 | `CircuitBreaker` | - | Scoped | Resilience | Per-operation breaker |
| 30 | `ResilientDatabaseExecutor` | - | Scoped | Resilience | Aligns with DbContext |
| 31 | `AuditCommandInterceptor` | - | Scoped | Resilience | Aligns with DbContext |
| 32 | `MetricsAggregationBackgroundService` | - | Hosted | Background | Hourly metrics rollups |
| 33 | `AlertProcessingBackgroundService` | - | Hosted | Background | Async notification delivery |
| 34 | `ScheduledReportGenerationService` | - | Hosted | Background | Scheduled reports |
| 35 | `ArchivalBackgroundService` | - | Hosted | Background | Data retention |
| 36 | `KeyRotationBackgroundService` | - | Hosted | Background | Key rotation |
| 37 | `HttpClient` (WebhookClient) | - | Singleton | Infrastructure | HTTP client factory |

## Lifetime Summary

| Lifetime | Count | Percentage |
|----------|-------|------------|
| Singleton | 13 | 35.1% |
| Scoped | 18 | 48.6% |
| Hosted | 6 | 16.2% |
| **Total** | **37** | **100%** |

## Lifetime Decision Matrix

### Use Singleton When:
- ✅ Service is stateless
- ✅ Service manages system-wide state (queues, metrics, circuit breakers)
- ✅ Service benefits from connection pooling (HTTP clients, SMTP clients)
- ✅ Service caches expensive resources (encryption keys)
- ✅ Service needs to be shared across all requests

### Use Scoped When:
- ✅ Service performs database operations (needs DbContext)
- ✅ Service needs request-specific context (user, IP, headers)
- ✅ Service enforces security boundaries (authorization, tenant isolation)
- ✅ Service participates in transactions
- ✅ Service should not maintain state across requests

### Use Hosted When:
- ✅ Service is a long-running background task
- ✅ Service processes queues asynchronously
- ✅ Service runs on a schedule (cron jobs)
- ✅ Service should not block request processing

## Common Patterns

### Pattern 1: Singleton Service with Hosted Background Worker
```csharp
// AuditLogger is both Singleton (for IAuditLogger interface) and Hosted (for background processing)
services.AddSingleton<AuditLogger>();
services.AddSingleton<IAuditLogger>(provider => provider.GetRequiredService<AuditLogger>());
services.AddHostedService<AuditLogger>(provider => provider.GetRequiredService<AuditLogger>());
```

**Use Case**: Service needs to be injectable (IAuditLogger) and also run background tasks (queue processing)

### Pattern 2: Scoped Repository with Singleton Monitor
```csharp
// Repository is scoped (database transactions)
services.AddScoped<IAuditRepository, AuditRepository>();

// Monitor is singleton (system-wide metrics)
services.AddSingleton<IPerformanceMonitor, PerformanceMonitor>();
```

**Use Case**: Repository needs DbContext (scoped), but monitor aggregates metrics across all requests (singleton)

### Pattern 3: Singleton Factory with Scoped Products
```csharp
// Factory is singleton (stateless)
services.AddSingleton<IExternalStorageProviderFactory, ExternalStorageProviderFactory>();

// Service that uses factory is scoped (database transactions)
services.AddScoped<IArchivalService, ArchivalService>();
```

**Use Case**: Factory creates providers on demand, but the service using them needs scoped lifetime

### Pattern 4: Singleton Channel with Hosted Consumer
```csharp
// Shared channel is singleton
services.AddSingleton(provider => Channel.CreateBounded<AlertNotificationTask>(1000));

// Background service consumes from channel
services.AddHostedService<AlertProcessingBackgroundService>();
```

**Use Case**: Multiple producers (scoped services) write to shared queue, single consumer (hosted service) processes

## Anti-Patterns to Avoid

### ❌ Anti-Pattern 1: Singleton Service with Scoped Dependency
```csharp
// BAD: Singleton service depends on scoped DbContext
services.AddSingleton<IAuditRepository, AuditRepository>(); // ❌ WRONG
```

**Problem**: Singleton will capture first DbContext instance and reuse it across all requests (connection leaks, stale data)

**Solution**: Make repository scoped
```csharp
services.AddScoped<IAuditRepository, AuditRepository>(); // ✅ CORRECT
```

### ❌ Anti-Pattern 2: Scoped Service with Mutable Shared State
```csharp
// BAD: Scoped service maintains state across requests
public class BadService
{
    private static List<string> _sharedState = new(); // ❌ WRONG
}
```

**Problem**: Scoped services are recreated per request, but static fields are shared (race conditions, memory leaks)

**Solution**: Use singleton service or remove shared state

### ❌ Anti-Pattern 3: Hosted Service Blocking Request Processing
```csharp
// BAD: Hosted service blocks on synchronous operations
public class BadBackgroundService : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        Thread.Sleep(1000); // ❌ WRONG - blocks thread pool
    }
}
```

**Problem**: Blocking operations in hosted services can exhaust thread pool

**Solution**: Use async/await throughout
```csharp
await Task.Delay(1000, stoppingToken); // ✅ CORRECT
```

## Thread Safety Considerations

### Singleton Services (Must Be Thread-Safe)
- ✅ `AuditLogger`: Uses `Channel<T>` (thread-safe)
- ✅ `PerformanceMonitor`: Uses `ConcurrentDictionary` (thread-safe)
- ✅ `AlertManager`: Uses `Channel<T>` and locks (thread-safe)
- ✅ `CircuitBreakerRegistry`: Uses `ConcurrentDictionary` (thread-safe)

### Scoped Services (No Thread Safety Needed)
- ✅ Scoped services are created per request (single-threaded access)
- ✅ No need for locks or concurrent collections

### Hosted Services (Must Be Thread-Safe)
- ✅ All hosted services use `CancellationToken` for graceful shutdown
- ✅ All hosted services use thread-safe collections for shared state

## Memory Management

### Singleton Services
- **Lifetime**: Application lifetime (never disposed until app shutdown)
- **Memory**: ~13 instances × average size (~1-10 KB each) = ~13-130 KB
- **Connections**: Pooled and reused (efficient)

### Scoped Services
- **Lifetime**: Request lifetime (disposed after request completes)
- **Memory**: ~18 instances × average size (~1-5 KB each) × concurrent requests
- **Example**: 100 concurrent requests = ~1.8-9 MB
- **Connections**: Pooled via DbContext (efficient)

### Hosted Services
- **Lifetime**: Application lifetime (never disposed until app shutdown)
- **Memory**: ~6 instances × average size (~1-5 KB each) = ~6-30 KB
- **Connections**: Managed independently (efficient)

**Total Memory Footprint**: ~20-170 KB (singletons + hosted) + ~1.8-9 MB per 100 concurrent requests (scoped)

## Performance Characteristics

| Lifetime | Creation Cost | Disposal Cost | Memory Overhead | Thread Safety Required |
|----------|---------------|---------------|-----------------|------------------------|
| Singleton | Once (startup) | Once (shutdown) | Low (shared) | Yes |
| Scoped | Per request | Per request | Medium (per request) | No |
| Hosted | Once (startup) | Once (shutdown) | Low (shared) | Yes |

## Troubleshooting Guide

### Issue: "Cannot resolve scoped service from singleton"
**Cause**: Singleton service depends on scoped service (e.g., DbContext)

**Solution**: Change singleton to scoped or inject `IServiceProvider` and create scope manually

### Issue: "DbContext disposed" errors
**Cause**: Singleton service captured scoped DbContext

**Solution**: Make service scoped to align with DbContext lifetime

### Issue: Memory leaks in singleton services
**Cause**: Singleton service holds references to scoped services

**Solution**: Don't inject scoped services into singleton services

### Issue: Race conditions in singleton services
**Cause**: Singleton service uses non-thread-safe collections

**Solution**: Use `ConcurrentDictionary`, `Channel<T>`, or locks

## Related Documentation

- [ASP.NET Core Dependency Injection](https://docs.microsoft.com/en-us/aspnet/core/fundamentals/dependency-injection)
- [Service Lifetimes](https://docs.microsoft.com/en-us/dotnet/core/extensions/dependency-injection#service-lifetimes)
- [Background Services](https://docs.microsoft.com/en-us/aspnet/core/fundamentals/host/hosted-services)
- [Thread Safety in .NET](https://docs.microsoft.com/en-us/dotnet/standard/threading/thread-safety)

