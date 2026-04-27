# SLA Escalation Background Service Implementation

## Overview

This document summarizes the implementation of Task 11.2: Create escalation background service for the Company Request Tickets system.

## Implementation Summary

### 1. Background Service Created

**File:** `src/ThinkOnErp.Infrastructure/Services/SlaEscalationBackgroundService.cs`

A hosted background service that runs periodically to monitor tickets for SLA compliance:

- **Extends:** `BackgroundService` from Microsoft.Extensions.Hosting
- **Purpose:** Automatically monitors tickets approaching or exceeding SLA deadlines
- **Execution:** Runs on a configurable interval (default: 30 minutes)
- **Features:**
  - Configurable enable/disable via appsettings
  - Configurable monitoring interval
  - Automatic escalation notifications
  - Error handling with continued processing
  - Proper service scope management for scoped dependencies

### 2. Service Registration

**File:** `src/ThinkOnErp.Infrastructure/DependencyInjection.cs`

The background service is registered as a hosted service:

```csharp
services.AddHostedService<SlaEscalationBackgroundService>();
```

This ensures the service starts automatically when the application starts and runs continuously in the background.

### 3. Configuration Added

**Files Updated:**
- `src/ThinkOnErp.API/appsettings.json`
- `.env.example`

**Configuration Structure:**

```json
{
  "SlaEscalation": {
    "Enabled": true,
    "ThresholdHours": 2,
    "BackgroundService": {
      "Enabled": true,
      "IntervalMinutes": 30
    }
  }
}
```

**Configuration Options:**

- `SlaEscalation:Enabled` - Enable/disable SLA escalation monitoring (default: true)
- `SlaEscalation:ThresholdHours` - Hours before SLA deadline to send alerts (default: 2)
- `SlaEscalation:BackgroundService:Enabled` - Enable/disable background service (default: true)
- `SlaEscalation:BackgroundService:IntervalMinutes` - Monitoring interval in minutes (default: 30)

### 4. Package Dependencies

**File:** `src/ThinkOnErp.Infrastructure/ThinkOnErp.Infrastructure.csproj`

Added package reference:
```xml
<PackageReference Include="Microsoft.Extensions.Hosting.Abstractions" Version="8.0.0" />
```

### 5. Unit Tests Created

**File:** `tests/ThinkOnErp.Infrastructure.Tests/Services/SlaEscalationBackgroundServiceTests.cs`

Comprehensive unit tests covering:

- Constructor validation (null parameter checks)
- Service lifecycle (start/stop)
- Configuration handling (enabled/disabled states)
- Error handling and resilience

**Test Results:** All 6 tests passing ✓

## How It Works

### Background Service Workflow

1. **Startup:**
   - Service starts automatically when the application starts
   - Checks if background service is enabled in configuration
   - If disabled, logs and exits gracefully

2. **Monitoring Loop:**
   - Runs continuously on configured interval (default: 30 minutes)
   - Creates a service scope to resolve scoped dependencies
   - Calls `ISlaEscalationService.CheckAndEscalateOverdueTicketsAsync()`
   - Handles exceptions and continues processing

3. **Escalation Processing:**
   - Gets tickets approaching SLA deadline (within threshold hours)
   - Gets overdue tickets (past expected resolution date)
   - Sends escalation notifications via `ITicketNotificationService`
   - Logs all activities for monitoring and troubleshooting

4. **Shutdown:**
   - Gracefully stops when application shuts down
   - Completes current processing cycle before stopping

### Integration with Existing Services

The background service integrates with:

- **ISlaEscalationService** (Task 11.1) - Performs the actual escalation logic
- **ITicketRepository** - Queries tickets approaching/exceeding SLA
- **ITicketNotificationService** - Sends escalation alerts
- **ISlaCalculationService** - Calculates SLA deadlines and compliance

## Configuration Examples

### Production Configuration (Conservative)

```json
{
  "SlaEscalation": {
    "Enabled": true,
    "ThresholdHours": 4,
    "BackgroundService": {
      "Enabled": true,
      "IntervalMinutes": 60
    }
  }
}
```

### Development Configuration (Aggressive)

```json
{
  "SlaEscalation": {
    "Enabled": true,
    "ThresholdHours": 1,
    "BackgroundService": {
      "Enabled": true,
      "IntervalMinutes": 5
    }
  }
}
```

### Disabled Configuration

```json
{
  "SlaEscalation": {
    "Enabled": false,
    "BackgroundService": {
      "Enabled": false
    }
  }
}
```

## Monitoring and Logging

The background service provides comprehensive logging:

- **Information:** Service start/stop, escalation check cycles
- **Debug:** Configuration values, ticket counts, timing information
- **Warning:** Configuration issues, low interval values
- **Error:** Processing failures, exception details

**Log Examples:**

```
[INFO] SLA escalation background service started
[INFO] Starting SLA escalation check cycle
[INFO] Found 3 tickets requiring SLA escalation
[INFO] SLA escalation alert sent for ticket 12345
[INFO] SLA escalation check cycle completed successfully
[DEBUG] Next SLA escalation check in 30 minutes
```

## Requirements Satisfied

This implementation satisfies the following requirements:

- **Requirement 4.9:** Escalate tickets that approach SLA deadline without status updates
- **Requirement 10.3:** Send escalation alerts when tickets approach SLA deadlines
- **Requirement 16.7:** Implement background processing for notification delivery
- **Requirement 18.1-18.12:** Error handling and resilience features

## Task Details Completed

✓ Implement background service for SLA monitoring
✓ Add automatic escalation notifications  
✓ Implement overdue ticket identification
✓ Add escalation workflow and assignment rules

## Files Created/Modified

### Created:
1. `src/ThinkOnErp.Infrastructure/Services/SlaEscalationBackgroundService.cs`
2. `tests/ThinkOnErp.Infrastructure.Tests/Services/SlaEscalationBackgroundServiceTests.cs`
3. `SLA_ESCALATION_BACKGROUND_SERVICE_IMPLEMENTATION.md`

### Modified:
1. `src/ThinkOnErp.Infrastructure/DependencyInjection.cs`
2. `src/ThinkOnErp.Infrastructure/ThinkOnErp.Infrastructure.csproj`
3. `src/ThinkOnErp.API/appsettings.json`
4. `.env.example`

## Testing

### Unit Tests
All unit tests pass successfully:
- Constructor validation tests
- Service lifecycle tests
- Configuration handling tests

### Manual Testing Steps

1. **Start the application:**
   ```bash
   dotnet run --project src/ThinkOnErp.API
   ```

2. **Verify service starts:**
   Check logs for: "SLA escalation background service started"

3. **Monitor escalation checks:**
   Watch logs for periodic escalation check cycles

4. **Test configuration:**
   - Modify `appsettings.json` to change interval
   - Restart application and verify new interval is used

5. **Test disable:**
   - Set `BackgroundService:Enabled` to false
   - Restart and verify service doesn't process escalations

## Performance Considerations

- **Interval Selection:** Balance between responsiveness and system load
  - Too frequent: Unnecessary database queries and processing
  - Too infrequent: Delayed escalation notifications
  - Recommended: 15-60 minutes depending on SLA requirements

- **Resource Usage:** Minimal when no tickets need escalation
  - Uses scoped services (proper disposal)
  - Efficient database queries with indexes
  - Asynchronous processing throughout

- **Scalability:** Designed for single-instance deployment
  - For multi-instance: Consider distributed locking
  - For high volume: Consider message queue pattern

## Future Enhancements

Potential improvements for future iterations:

1. **Distributed Locking:** Support multiple application instances
2. **Configurable Schedules:** Different intervals for different times
3. **Escalation Levels:** Multiple escalation tiers based on severity
4. **Dashboard Integration:** Real-time escalation monitoring UI
5. **Metrics Collection:** Track escalation rates and response times

## Conclusion

Task 11.2 has been successfully completed. The SLA escalation background service provides automatic, configurable monitoring of ticket SLA compliance with proper error handling, logging, and integration with existing services.
