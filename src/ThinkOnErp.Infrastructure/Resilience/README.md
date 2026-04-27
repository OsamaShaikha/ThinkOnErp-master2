# Error Handling and Resilience Infrastructure

This directory contains comprehensive error handling and resilience patterns for the ThinkOnERP system.

## Overview

The resilience infrastructure implements Requirements 18.1-18.12 from the Company Request Tickets specification, providing:

1. **Custom Exception Types** - Domain-specific exceptions with error codes and context
2. **Global Exception Handling** - Centralized exception handling middleware
3. **Retry Logic** - Automatic retry with exponential backoff for transient failures
4. **Circuit Breaker Pattern** - Prevents cascading failures in distributed systems
5. **Health Monitoring** - Health check endpoints for system status monitoring

## Components

### Custom Exception Types

Located in `src/ThinkOnErp.Domain/Exceptions/`:

- **DomainException** - Base class for all domain exceptions
- **TicketNotFoundException** - Thrown when a ticket is not found
- **InvalidStatusTransitionException** - Thrown for invalid status transitions
- **UnauthorizedTicketAccessException** - Thrown for unauthorized access attempts
- **AttachmentSizeExceededException** - Thrown when file size exceeds limits
- **InvalidFileTypeException** - Thrown for unsupported file types
- **DatabaseConnectionException** - Thrown for database connection failures
- **ExternalServiceException** - Thrown for external service failures
- **ConcurrentModificationException** - Thrown for concurrent modification conflicts

Each exception includes:
- Error code for categorization
- Context dictionary for additional information
- Structured logging support

### Retry Policy

**File:** `RetryPolicy.cs`

Implements automatic retry logic with exponential backoff:

```csharp
var retryPolicy = new RetryPolicy(logger, maxRetries: 3);

var result = await retryPolicy.ExecuteAsync(
    async () => await DatabaseOperation(),
    "MyDatabaseOperation"
);
```

Features:
- Configurable max retries (default: 3)
- Exponential backoff with jitter
- Transient exception detection
- Comprehensive logging

### Circuit Breaker

**File:** `CircuitBreaker.cs`

Implements circuit breaker pattern to prevent cascading failures:

```csharp
var circuitBreaker = new CircuitBreaker(logger, failureThreshold: 5);

var result = await circuitBreaker.ExecuteAsync(
    async () => await ExternalServiceCall(),
    "ExternalService"
);
```

States:
- **Closed** - Normal operation, requests pass through
- **Open** - Too many failures, requests are rejected
- **Half-Open** - Testing if service has recovered

Features:
- Configurable failure threshold (default: 5)
- Configurable open duration (default: 60 seconds)
- Automatic state transitions
- Comprehensive logging

### Circuit Breaker Registry

**File:** `CircuitBreakerRegistry.cs`

Manages multiple circuit breakers by service name:

```csharp
var registry = new CircuitBreakerRegistry(loggerFactory);
var breaker = registry.GetOrCreate("DatabaseService");
```

Features:
- Centralized circuit breaker management
- Service-specific circuit breakers
- State monitoring across all services

### Resilient Database Executor

**File:** `ResilientDatabaseExecutor.cs`

Combines retry logic and circuit breaker for database operations:

```csharp
var executor = new ResilientDatabaseExecutor(retryPolicy, circuitBreaker, logger);

var result = await executor.ExecuteAsync(
    async () => await repository.GetByIdAsync(id),
    "GetTicketById"
);
```

Features:
- Automatic retry for transient database errors
- Circuit breaker protection
- Oracle-specific error code handling
- Timeout management
- Transaction rollback support

Transient Oracle Error Codes:
- ORA-00051: Timeout waiting for resource
- ORA-00054: Resource busy
- ORA-01012: Not logged on
- ORA-01033: Initialization in progress
- ORA-01034: Oracle not available
- ORA-03113: End-of-file on communication channel
- ORA-03114: Not connected
- ORA-12xxx: TNS errors (network issues)

### Global Exception Handling Middleware

**File:** `src/ThinkOnErp.API/Middleware/ExceptionHandlingMiddleware.cs`

Centralized exception handling for all API requests:

Features:
- Catches all unhandled exceptions
- Maps exceptions to appropriate HTTP status codes
- Returns consistent ApiResponse format
- Structured logging with context
- Prevents stack trace leakage in production

Exception Mapping:
- ValidationException → 400 Bad Request
- TicketNotFoundException → 404 Not Found
- UnauthorizedTicketAccessException → 403 Forbidden
- InvalidStatusTransitionException → 400 Bad Request
- AttachmentSizeExceededException → 400 Bad Request
- InvalidFileTypeException → 400 Bad Request
- DatabaseConnectionException → 503 Service Unavailable
- ExternalServiceException → 503 Service Unavailable
- ConcurrentModificationException → 409 Conflict
- Other exceptions → 500 Internal Server Error

### Health Check Controller

**File:** `src/ThinkOnErp.API/Controllers/HealthController.cs`

Provides health monitoring endpoints:

**Endpoints:**

1. `GET /api/health` - Basic health check
   ```json
   {
     "status": "Healthy",
     "timestamp": "2024-01-15T10:30:00Z",
     "circuitBreakers": {
       "DatabaseService": "Closed",
       "EmailService": "Closed"
     }
   }
   ```

2. `GET /api/health/detailed` - Detailed health information
   ```json
   {
     "status": "Degraded",
     "timestamp": "2024-01-15T10:30:00Z",
     "services": [
       {
         "serviceName": "DatabaseService",
         "circuitState": "Closed",
         "isHealthy": true
       },
       {
         "serviceName": "EmailService",
         "circuitState": "Open",
         "isHealthy": false
       }
     ]
   }
   ```

## Usage Examples

### Example 1: Repository with Resilience

```csharp
public class TicketRepository : ITicketRepository
{
    private readonly ResilientDatabaseExecutor _executor;
    private readonly OracleDbContext _context;

    public async Task<SysRequestTicket?> GetByIdAsync(Int64 id)
    {
        return await _executor.ExecuteAsync(async () =>
        {
            using var connection = _context.CreateConnection();
            // Database operation
            return await FetchTicketFromDatabase(connection, id);
        }, "GetTicketById");
    }
}
```

### Example 2: External Service with Circuit Breaker

```csharp
public class EmailNotificationService
{
    private readonly CircuitBreaker _circuitBreaker;

    public async Task SendEmailAsync(string to, string subject, string body)
    {
        try
        {
            await _circuitBreaker.ExecuteAsync(async () =>
            {
                await _emailClient.SendAsync(to, subject, body);
            }, "EmailService");
        }
        catch (InvalidOperationException ex) when (ex.Message.Contains("Circuit breaker"))
        {
            // Circuit is open, log and use fallback
            _logger.LogWarning("Email service unavailable, using fallback notification");
            await _fallbackNotificationService.NotifyAsync(to, subject);
        }
    }
}
```

### Example 3: Custom Exception Handling

```csharp
public class TicketService
{
    public async Task<TicketDto> GetTicketAsync(Int64 ticketId, Int64 userId)
    {
        var ticket = await _repository.GetByIdAsync(ticketId);
        
        if (ticket == null)
        {
            throw new TicketNotFoundException(ticketId);
        }

        if (!CanUserAccessTicket(ticket, userId))
        {
            throw new UnauthorizedTicketAccessException(ticketId, userId);
        }

        return MapToDto(ticket);
    }
}
```

## Configuration

### Dependency Injection Setup

In `DependencyInjection.cs`:

```csharp
// Register resilience services
services.AddSingleton<CircuitBreakerRegistry>();
services.AddScoped<RetryPolicy>();
services.AddScoped<CircuitBreaker>();
services.AddScoped<ResilientDatabaseExecutor>();
```

### Middleware Registration

In `Program.cs`:

```csharp
// Add exception handling middleware (should be early in pipeline)
app.UseMiddleware<ExceptionHandlingMiddleware>();
```

## Monitoring and Observability

### Logging

All resilience components use structured logging:

```
[Warning] Transient error in GetTicketById. Attempt 1/3. Retrying in 100ms
[Error] Circuit breaker for EmailService transitioning to Open state after 5 failures
[Information] Circuit breaker for EmailService transitioning to Closed state after successful execution
```

### Health Checks

Monitor system health:
- Check circuit breaker states
- Identify degraded services
- Trigger alerts for open circuits

### Metrics

Key metrics to monitor:
- Retry attempt counts
- Circuit breaker state transitions
- Exception rates by type
- Database operation latencies

## Best Practices

1. **Use Specific Exceptions** - Throw domain-specific exceptions with context
2. **Add Context** - Use `AddContext()` to include relevant information
3. **Log Appropriately** - Use correct log levels (Warning for expected, Error for unexpected)
4. **Handle Gracefully** - Provide fallback mechanisms when services are unavailable
5. **Monitor Health** - Regularly check health endpoints
6. **Test Resilience** - Test retry and circuit breaker behavior
7. **Configure Timeouts** - Set appropriate timeouts for operations
8. **Document Errors** - Document error codes and recovery procedures

## Testing

### Unit Testing Retry Logic

```csharp
[Fact]
public async Task RetryPolicy_RetriesOnTransientFailure()
{
    var attempts = 0;
    var result = await _retryPolicy.ExecuteAsync(async () =>
    {
        attempts++;
        if (attempts < 3)
            throw new DatabaseConnectionException("Test", "Transient error");
        return "Success";
    }, "TestOperation");

    Assert.Equal("Success", result);
    Assert.Equal(3, attempts);
}
```

### Testing Circuit Breaker

```csharp
[Fact]
public async Task CircuitBreaker_OpensAfterThreshold()
{
    for (int i = 0; i < 5; i++)
    {
        await Assert.ThrowsAsync<Exception>(async () =>
            await _circuitBreaker.ExecuteAsync(
                async () => throw new Exception("Failure"),
                "TestService"));
    }

    Assert.Equal(CircuitState.Open, _circuitBreaker.State);
}
```

## Requirements Coverage

This implementation satisfies the following requirements:

- **18.1** - Database connection failure handling with retry logic
- **18.2** - Meaningful error messages for validation failures
- **18.3** - Exception handling for external service calls with fallback
- **18.4** - File upload failure handling with cleanup
- **18.5** - Transaction rollback for failed operations
- **18.6** - Fallback mechanisms for notification delivery
- **18.7** - Concurrent modification conflict handling
- **18.8** - Timeout handling for long-running queries
- **18.9** - Health check endpoints for monitoring
- **18.10** - Comprehensive error logging with context
- **18.11** - Graceful degradation for non-critical features
- **18.12** - Recovery mechanisms for corrupted data

## Future Enhancements

Potential improvements:
- Distributed tracing integration
- Metrics collection (Prometheus/Grafana)
- Advanced circuit breaker patterns (bulkhead, rate limiting)
- Chaos engineering testing
- Automated recovery procedures
