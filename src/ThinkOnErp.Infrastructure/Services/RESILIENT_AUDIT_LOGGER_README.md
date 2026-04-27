# ResilientAuditLogger Implementation

## Overview

The `ResilientAuditLogger` is a decorator that wraps the base `AuditLogger` service to provide additional resilience patterns for handling database failures gracefully. This implementation fulfills **Task 16.1** from the full-traceability-system spec.

## Features

### 1. Circuit Breaker Pattern

The circuit breaker prevents cascading failures when the database is unavailable:

- **Closed State**: Normal operation, all requests pass through
- **Open State**: After threshold failures (default: 5), circuit opens and rejects requests
- **Half-Open State**: After timeout period (default: 60s), allows test requests to check if service recovered

**Configuration**:
```csharp
var options = new ResilientAuditLoggerOptions
{
    EnableCircuitBreaker = true  // Default: true
};
```

### 2. Retry Policy

Automatically retries transient database failures with exponential backoff:

- **Max Retries**: 3 attempts (configurable)
- **Initial Delay**: 100ms (configurable)
- **Backoff Strategy**: Exponential with jitter
- **Transient Errors**: Detects Oracle-specific transient errors (deadlocks, timeouts, connection issues)

**Configuration**:
```csharp
var options = new ResilientAuditLoggerOptions
{
    EnableRetryPolicy = true  // Default: true
};
```

### 3. Fallback Mechanisms

When the circuit breaker is open, the system applies a fallback strategy:

#### Available Strategies:

1. **LogToFile** (Default)
   - Writes audit events to a fallback file
   - Preserves audit trail even when database is down
   - File path: `logs/audit-fallback.log` (configurable)

2. **LogToConsole**
   - Writes audit events to console output
   - Useful for containerized environments

3. **Silent**
   - Logs warning but doesn't persist audit event
   - Minimal overhead, suitable for non-critical auditing

4. **Throw**
   - Throws exception when circuit is open
   - Not recommended for production

**Configuration**:
```csharp
var options = new ResilientAuditLoggerOptions
{
    FallbackStrategy = FallbackStrategy.LogToFile,
    FallbackFilePath = "logs/audit-fallback.log"
};
```

### 4. Metrics Tracking

Tracks resilience metrics for monitoring and alerting:

- **TotalRequests**: Total audit logging requests
- **SuccessfulRequests**: Successfully logged events
- **FailedRequests**: Failed logging attempts
- **CircuitBreakerRejections**: Requests rejected due to open circuit
- **RetriedRequests**: Requests that required retry
- **CircuitState**: Current circuit breaker state
- **SuccessRate**: Percentage of successful requests
- **FailureRate**: Percentage of failed requests
- **RejectionRate**: Percentage of rejected requests

**Usage**:
```csharp
var metrics = resilientLogger.GetMetrics();
Console.WriteLine($"Success Rate: {metrics.SuccessRate:F2}%");
Console.WriteLine($"Circuit State: {metrics.CircuitState}");
```

## Architecture

```
┌─────────────────────────────────────┐
│   Application Code                  │
│   (Controllers, Services)           │
└──────────────┬──────────────────────┘
               │
               ▼
┌─────────────────────────────────────┐
│   ResilientAuditLogger (Decorator)  │
│   ┌─────────────────────────────┐   │
│   │  Circuit Breaker            │   │
│   │  ┌─────────────────────┐    │   │
│   │  │  Retry Policy       │    │   │
│   │  │  ┌─────────────┐    │    │   │
│   │  │  │ AuditLogger │    │    │   │
│   │  │  └─────────────┘    │    │   │
│   │  └─────────────────────┘    │   │
│   └─────────────────────────────┘   │
└──────────────┬──────────────────────┘
               │
               ▼
┌─────────────────────────────────────┐
│   Database (Oracle)                 │
└─────────────────────────────────────┘
```

## Usage

### Basic Setup

```csharp
// In DependencyInjection.cs or Program.cs
services.AddSingleton<CircuitBreaker>(sp =>
{
    var logger = sp.GetRequiredService<ILogger<CircuitBreaker>>();
    return new CircuitBreaker(
        logger,
        failureThreshold: 5,
        openDuration: TimeSpan.FromMinutes(1),
        halfOpenTimeout: TimeSpan.FromSeconds(30));
});

services.AddSingleton<RetryPolicy>(sp =>
{
    var logger = sp.GetRequiredService<ILogger<RetryPolicy>>();
    return new RetryPolicy(
        logger,
        maxRetries: 3,
        initialDelay: TimeSpan.FromMilliseconds(100));
});

// Register ResilientAuditLogger as decorator
services.Decorate<IAuditLogger>((inner, sp) =>
{
    var circuitBreaker = sp.GetRequiredService<CircuitBreaker>();
    var retryPolicy = sp.GetRequiredService<RetryPolicy>();
    var logger = sp.GetRequiredService<ILogger<ResilientAuditLogger>>();
    var options = new ResilientAuditLoggerOptions
    {
        EnableCircuitBreaker = true,
        EnableRetryPolicy = true,
        FallbackStrategy = FallbackStrategy.LogToFile,
        FallbackFilePath = "logs/audit-fallback.log"
    };
    
    return new ResilientAuditLogger(inner, circuitBreaker, retryPolicy, logger, options);
});
```

### Using the Logger

```csharp
public class MyService
{
    private readonly IAuditLogger _auditLogger;
    
    public MyService(IAuditLogger auditLogger)
    {
        _auditLogger = auditLogger;
    }
    
    public async Task UpdateUserAsync(User user)
    {
        // Your business logic
        
        // Log audit event - resilience is handled automatically
        await _auditLogger.LogDataChangeAsync(new DataChangeAuditEvent
        {
            CorrelationId = CorrelationContext.Current,
            ActorType = "USER",
            ActorId = user.Id,
            Action = "UPDATE",
            EntityType = "User",
            EntityId = user.Id,
            OldValue = JsonSerializer.Serialize(oldUser),
            NewValue = JsonSerializer.Serialize(user)
        });
    }
}
```

### Monitoring Health

```csharp
public class HealthCheckService
{
    private readonly IAuditLogger _auditLogger;
    
    public async Task<bool> CheckAuditSystemHealth()
    {
        var isHealthy = await _auditLogger.IsHealthyAsync();
        
        if (!isHealthy && _auditLogger is ResilientAuditLogger resilientLogger)
        {
            var metrics = resilientLogger.GetMetrics();
            
            if (metrics.CircuitState == CircuitState.Open)
            {
                // Alert: Circuit breaker is open
                // Database connectivity issues detected
            }
            
            if (metrics.FailureRate > 10.0)
            {
                // Alert: High failure rate
                // Investigate database performance
            }
        }
        
        return isHealthy;
    }
}
```

## Transient vs Permanent Failures

The `ResilientAuditLogger` distinguishes between transient and permanent failures:

### Transient Failures (Retried)
- Database connection timeouts
- Deadlocks (ORA-00060)
- TNS connection errors (ORA-12170, ORA-12541)
- End-of-file on communication channel (ORA-03113)
- Database not available (ORA-01034)

### Permanent Failures (Not Retried)
- Invalid credentials (ORA-01017)
- Validation errors
- Authorization errors
- Application logic errors

## Configuration Options

```csharp
public class ResilientAuditLoggerOptions
{
    /// <summary>
    /// Enable circuit breaker pattern.
    /// Default: true
    /// </summary>
    public bool EnableCircuitBreaker { get; set; } = true;

    /// <summary>
    /// Enable retry policy for transient failures.
    /// Default: true
    /// </summary>
    public bool EnableRetryPolicy { get; set; } = true;

    /// <summary>
    /// Fallback strategy when circuit breaker is open.
    /// Default: LogToFile
    /// </summary>
    public FallbackStrategy FallbackStrategy { get; set; } = FallbackStrategy.LogToFile;

    /// <summary>
    /// File path for fallback logging when circuit breaker is open.
    /// Default: "logs/audit-fallback.log"
    /// </summary>
    public string FallbackFilePath { get; set; } = "logs/audit-fallback.log";
}
```

## Testing

Unit tests are provided in `ResilientAuditLoggerTests.cs`:

- Circuit breaker opens after threshold failures
- Circuit breaker rejects requests when open
- Circuit breaker transitions to half-open and recovers
- Retry policy retries transient failures
- Retry policy doesn't retry permanent failures
- Fallback mechanisms activate when circuit is open
- Metrics track all resilience events

Run tests:
```bash
dotnet test --filter "FullyQualifiedName~ResilientAuditLoggerTests"
```

## Performance Considerations

1. **Minimal Overhead**: When circuit is closed and no failures occur, overhead is negligible (<1ms)
2. **Retry Delays**: Exponential backoff adds delay only for transient failures
3. **Circuit Breaker**: Prevents wasted resources when database is down
4. **Async Operations**: All operations are fully asynchronous

## Monitoring and Alerting

### Recommended Alerts

1. **Circuit Breaker Open**
   - Condition: `CircuitState == Open`
   - Severity: Critical
   - Action: Check database connectivity and health

2. **High Failure Rate**
   - Condition: `FailureRate > 10%`
   - Severity: Warning
   - Action: Investigate database performance

3. **High Rejection Rate**
   - Condition: `RejectionRate > 5%`
   - Severity: Warning
   - Action: Circuit breaker is frequently open, check database stability

4. **Fallback File Growing**
   - Condition: Fallback file size > 100MB
   - Severity: Warning
   - Action: Database has been down for extended period

### Metrics Endpoint

```csharp
[HttpGet("api/monitoring/audit-resilience")]
public IActionResult GetAuditResilienceMetrics()
{
    if (_auditLogger is ResilientAuditLogger resilientLogger)
    {
        var metrics = resilientLogger.GetMetrics();
        return Ok(metrics);
    }
    
    return NotFound("Resilient audit logger not configured");
}
```

## Troubleshooting

### Circuit Breaker Stuck Open

**Symptoms**: Circuit breaker remains open even after database recovers

**Solution**:
1. Check database connectivity manually
2. Verify circuit breaker timeout settings
3. Check logs for half-open state transitions
4. Manually reset circuit breaker if needed

### High Retry Rate

**Symptoms**: Many requests require multiple retries

**Solution**:
1. Check database performance and query execution times
2. Review connection pool settings
3. Investigate network latency
4. Consider increasing database resources

### Fallback File Growing Rapidly

**Symptoms**: Fallback log file size increasing quickly

**Solution**:
1. Database is down or severely degraded
2. Check database health immediately
3. Implement fallback file rotation
4. Consider increasing circuit breaker threshold

## Related Components

- **AuditLogger**: Base audit logging service with async queue and batching
- **CircuitBreaker**: Generic circuit breaker implementation
- **RetryPolicy**: Generic retry policy with exponential backoff
- **CircuitBreakerRegistry**: Manages multiple circuit breakers by service name

## References

- Design Document: `.kiro/specs/full-traceability-system/design.md`
- Requirements: `.kiro/specs/full-traceability-system/requirements.md`
- Task: `.kiro/specs/full-traceability-system/tasks.md` (Task 16.1)
- Circuit Breaker Pattern: `src/ThinkOnErp.Infrastructure/Resilience/README.md`
