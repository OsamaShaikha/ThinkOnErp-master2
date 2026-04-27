# Logging Infrastructure

This directory contains logging-related infrastructure components for the ThinkOnErp system.

## CorrelationIdEnricher

The `CorrelationIdEnricher` is a Serilog enricher that automatically adds correlation IDs to all log entries.

### Purpose

- Enables request tracing across the entire system
- Adds correlation ID from `CorrelationContext` to every log entry
- Supports debugging and troubleshooting by linking related log entries

### How It Works

1. The enricher reads the current correlation ID from `CorrelationContext.Current`
2. If a correlation ID exists, it adds it as a property to the log event
3. The correlation ID appears in all structured log outputs

### Configuration

The enricher is configured in `Program.cs`:

```csharp
Log.Logger = new LoggerConfiguration()
    .Enrich.With<CorrelationIdEnricher>()
    .Enrich.FromLogContext()
    // ... other configuration
    .CreateLogger();
```

### Output Template

To include the correlation ID in log output, add `{CorrelationId}` to the output template:

```csharp
.WriteTo.Console(
    outputTemplate: "[{Timestamp:yyyy-MM-dd HH:mm:ss.fff}] [{Level:u3}] [{CorrelationId}] {Message:lj}{NewLine}{Exception}")
```

### Integration with Request Tracing

The `RequestTracingMiddleware` sets the correlation ID at the start of each request:

```csharp
var correlationId = Guid.NewGuid().ToString();
CorrelationContext.Current = correlationId;
```

All subsequent log entries within that request will automatically include this correlation ID.

### Benefits

- **Request Tracing**: Track a single request through multiple services and layers
- **Debugging**: Quickly find all log entries related to a specific request
- **Troubleshooting**: Reproduce issues by following the complete request flow
- **Compliance**: Meet audit requirements for request traceability

### Example Log Output

```
[2024-01-15 10:30:45.123] [INF] [abc-123-def-456] Request started: GET /api/users
[2024-01-15 10:30:45.234] [INF] [abc-123-def-456] Executing query: SELECT * FROM SYS_USERS
[2024-01-15 10:30:45.345] [INF] [abc-123-def-456] Request completed: 200 OK (222ms)
```

All three log entries share the same correlation ID `abc-123-def-456`, making it easy to trace the request.
