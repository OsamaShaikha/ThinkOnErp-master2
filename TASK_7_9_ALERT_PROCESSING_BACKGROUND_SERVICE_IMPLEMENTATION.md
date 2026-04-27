# Task 7.9: Alert Processing Background Service Implementation

## Overview

This document summarizes the implementation of the background service for alert processing, completing task 7.9 of the Full Traceability System specification.

## Implementation Summary

### 1. AlertProcessingBackgroundService

**Created:** `src/ThinkOnErp.Infrastructure/Services/AlertProcessingBackgroundService.cs`

A proper ASP.NET Core BackgroundService that processes alert notifications asynchronously with:

- **Lifecycle Management**: Proper startup, shutdown, and graceful termination
- **Controlled Concurrency**: Configurable max concurrent alert processing (default: 5)
- **Statistics Tracking**: Monitors processed/failed alerts and success rates
- **Queue Wait Time Monitoring**: Tracks how long alerts spend in the queue
- **Graceful Shutdown**: Waits for in-flight tasks to complete (30-second timeout)
- **Error Handling**: Continues processing even if individual alerts fail
- **Configuration Support**: Enabled/disabled via appsettings.json

Key features:
```csharp
- ProcessAlertsWithConcurrencyAsync(): Uses SemaphoreSlim for controlled concurrency
- ProcessAlertNotificationAsync(): Processes individual alerts through all configured channels
- Graceful shutdown with 30-second timeout for in-flight tasks
- Statistics logging on service stop
```

### 2. Shared Channel Architecture

**Modified:** `src/ThinkOnErp.Infrastructure/Services/AlertManager.cs`

Refactored AlertManager to use a shared channel instead of internal background processing:

- **Removed**: Internal `ProcessNotificationQueueAsync()` method
- **Removed**: Internal `ProcessNotificationTaskAsync()` method  
- **Removed**: Internal `AlertNotificationTask` class
- **Updated**: Constructor to accept `Channel<AlertNotificationTask>` as a parameter
- **Retained**: All alert triggering, rate limiting, and notification channel logic

The AlertManager now focuses on:
- Alert triggering and validation
- Rate limiting enforcement
- Queuing notifications to the shared channel
- Direct notification sending (email, webhook, SMS)

### 3. AlertNotificationTask Model

**Created:** `src/ThinkOnErp.Infrastructure/Services/AlertProcessingBackgroundService.cs`

Moved AlertNotificationTask to be a public class in the background service file:

```csharp
public class AlertNotificationTask
{
    public Alert Alert { get; set; } = null!;
    public AlertRule Rule { get; set; } = null!;
    public DateTime QueuedAt { get; set; }
}
```

This allows the model to be shared between AlertManager (producer) and AlertProcessingBackgroundService (consumer).

### 4. Dependency Injection Configuration

**Modified:** `src/ThinkOnErp.Infrastructure/DependencyInjection.cs`

Added registration for:

1. **Shared Channel**: Singleton bounded channel with 1000 capacity
   ```csharp
   services.AddSingleton(provider =>
   {
       var channelOptions = new BoundedChannelOptions(1000)
       {
           FullMode = BoundedChannelFullMode.DropOldest,
           SingleReader = false,
           SingleWriter = false
       };
       return Channel.CreateBounded<AlertNotificationTask>(channelOptions);
   });
   ```

2. **Background Service**: Registered as hosted service
   ```csharp
   services.AddHostedService<AlertProcessingBackgroundService>();
   ```

3. **Using Statement**: Added `System.Threading.Channels`

### 5. Configuration

**Modified:** `src/ThinkOnErp.API/appsettings.json`

Added background processing configuration:

```json
"Alerting": {
  "Enabled": true,
  "MaxAlertsPerRulePerHour": 10,
  "RateLimitWindowMinutes": 60,
  "MaxNotificationQueueSize": 1000,
  "BackgroundProcessing": {
    "Enabled": true,
    "MaxConcurrentAlerts": 5
  },
  ...
}
```

Configuration options:
- `Enabled`: Enable/disable the background service
- `MaxConcurrentAlerts`: Maximum number of alerts processed concurrently (1-20)

### 6. Test Updates

**Modified:** 
- `tests/ThinkOnErp.Infrastructure.Tests/Services/AlertManagerTests.cs`
- `tests/ThinkOnErp.Infrastructure.Tests/Services/AlertManagerAcknowledgmentTests.cs`

Updated test files to:
1. Create a shared channel in test setup
2. Pass the channel to AlertManager constructor
3. Added `using System.Threading.Channels;`

All existing tests continue to pass with the new architecture.

## Architecture Benefits

### Separation of Concerns

1. **AlertManager**: Focuses on business logic
   - Alert validation and triggering
   - Rate limiting enforcement
   - Notification channel management
   - Queuing to shared channel

2. **AlertProcessingBackgroundService**: Focuses on infrastructure
   - Background processing lifecycle
   - Concurrency control
   - Statistics tracking
   - Graceful shutdown

### Improved Lifecycle Management

- **Before**: Internal Task.Run with no lifecycle management
- **After**: Proper BackgroundService with:
  - Startup/shutdown hooks
  - Graceful termination
  - Integration with ASP.NET Core hosting
  - Proper cancellation token support

### Better Monitoring

The background service tracks:
- Total alerts processed
- Failed alert count
- Success rate
- Service uptime
- Queue wait times

Statistics are logged on service shutdown:
```
Alert processing service statistics: 
  Uptime=01:23:45, 
  Processed=1234, 
  Failed=5, 
  SuccessRate=99.59%
```

### Configurable Concurrency

Administrators can tune performance by adjusting `MaxConcurrentAlerts`:
- Low traffic: 1-3 concurrent alerts
- Medium traffic: 5 concurrent alerts (default)
- High traffic: 10-20 concurrent alerts

### Graceful Shutdown

When the application stops:
1. Background service stops accepting new alerts
2. Waits up to 30 seconds for in-flight alerts to complete
3. Logs statistics before terminating
4. Prevents alert loss during shutdown

## Testing

### Build Verification

```bash
dotnet build ThinkOnErp.sln
```

Result: ✅ Build succeeded with 0 errors

### Test Verification

All existing AlertManager tests pass:
- `AlertManagerTests`: 24 tests passing
- `AlertManagerAcknowledgmentTests`: All tests passing

## Design Compliance

This implementation satisfies:

- **Requirement 19.1-19.7**: Alert and notification system requirements
- **Design Section 10**: AlertManager interface specification
- **Task 7.9**: "Implement background service for alert processing"

The implementation follows the design pattern established by:
- `MetricsAggregationBackgroundService`
- `SlaEscalationBackgroundService`

## Usage Example

### Triggering an Alert

```csharp
// AlertManager queues the notification
await alertManager.TriggerAlertAsync(new Alert
{
    AlertType = "Exception",
    Severity = "Critical",
    Title = "Database Connection Failed",
    Description = "Unable to connect to Oracle database",
    CorrelationId = "abc-123"
});

// AlertProcessingBackgroundService picks it up and processes it asynchronously
// Sends notifications through configured channels (email, webhook, SMS)
```

### Configuration

```json
{
  "Alerting": {
    "BackgroundProcessing": {
      "Enabled": true,
      "MaxConcurrentAlerts": 5
    }
  }
}
```

### Disabling Background Processing

Set `Enabled` to `false` in configuration:

```json
{
  "Alerting": {
    "BackgroundProcessing": {
      "Enabled": false
    }
  }
}
```

The service will log "Alert processing background service is disabled" and not process any alerts.

## Files Created

1. `src/ThinkOnErp.Infrastructure/Services/AlertProcessingBackgroundService.cs` - Background service implementation

## Files Modified

1. `src/ThinkOnErp.Infrastructure/Services/AlertManager.cs` - Refactored to use shared channel
2. `src/ThinkOnErp.Infrastructure/DependencyInjection.cs` - Added channel and service registration
3. `src/ThinkOnErp.API/appsettings.json` - Added background processing configuration
4. `tests/ThinkOnErp.Infrastructure.Tests/Services/AlertManagerTests.cs` - Updated for new constructor
5. `tests/ThinkOnErp.Infrastructure.Tests/Services/AlertManagerAcknowledgmentTests.cs` - Updated for new constructor

## Next Steps

Recommended follow-up tasks:
1. **Task 8.1-8.10**: Implement Audit Query Service for querying audit logs
2. **Task 9.1-9.14**: Implement Compliance Reporting for GDPR, SOX, ISO 27001
3. Add monitoring dashboard for alert processing statistics
4. Implement alert processing metrics (throughput, latency, queue depth)
5. Add health check endpoint for background service status

## Conclusion

Task 7.9 is complete. The AlertProcessingBackgroundService provides a robust, production-ready solution for asynchronous alert processing with proper lifecycle management, concurrency control, and graceful shutdown capabilities.
