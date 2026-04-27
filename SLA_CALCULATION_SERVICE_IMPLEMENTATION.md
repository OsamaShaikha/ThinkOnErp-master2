# SLA Calculation Service Implementation Summary

## Task 11.1: Create SLA Calculation Service

### Overview
Implemented a comprehensive SLA calculation service that handles SLA target calculation, business hours computation, escalation threshold monitoring, and SLA compliance tracking for the Company Request Tickets system.

### Files Created

#### 1. Domain Layer
- **`src/ThinkOnErp.Domain/Interfaces/ISlaCalculationService.cs`**
  - Interface defining the contract for SLA-related operations
  - Includes methods for SLA deadline calculation, escalation monitoring, and compliance tracking
  - Defines `SlaStatus` enum (NotSet, OnTrack, AtRisk, Breached, Met)

#### 2. Infrastructure Layer
- **`src/ThinkOnErp.Infrastructure/Services/SlaCalculationService.cs`**
  - Complete implementation of ISlaCalculationService
  - Business hours calculation (8 AM to 5 PM, 9 hours per day)
  - Weekend and holiday exclusion support
  - Escalation threshold monitoring
  - SLA compliance rate calculation

#### 3. Test Layer
- **`tests/ThinkOnErp.Infrastructure.Tests/Services/SlaCalculationServiceTests.cs`**
  - 21 comprehensive unit tests covering all service functionality
  - Tests for SLA deadline calculation with business hours
  - Tests for weekend exclusion logic
  - Tests for escalation monitoring
  - Tests for SLA status determination
  - Tests for compliance rate calculation
  - All tests passing ✓

### Key Features Implemented

#### 1. SLA Deadline Calculation
- Calculates SLA deadlines based on priority and creation date
- Supports business hours calculation (8 AM - 5 PM)
- Excludes weekends when configured
- Excludes holidays when configured (placeholder for future holiday calendar integration)
- Automatically adjusts start times to next business hour if created outside business hours

#### 2. Business Hours Calculation
- Configurable business hours (default: 8 AM to 5 PM)
- Calculates business hours between two dates
- Skips weekends and holidays as configured
- Handles multi-day calculations correctly

#### 3. Escalation Monitoring
- Calculates escalation alert times based on priority thresholds
- Determines if tickets need escalation
- Supports business hours for escalation calculations
- Prevents escalation of resolved tickets

#### 4. SLA Status Tracking
- **NotSet**: No SLA deadline configured
- **OnTrack**: Ticket is progressing normally
- **AtRisk**: Within 20% of SLA deadline
- **Breached**: SLA deadline has passed
- **Met**: Ticket resolved within SLA deadline

#### 5. Compliance Tracking
- Calculates SLA compliance rate as percentage
- Handles edge cases (no tickets = 100% compliance)
- Provides time remaining until breach
- Supports reporting and analytics

### Integration Points

#### Dependency Injection
Updated `src/ThinkOnErp.Infrastructure/DependencyInjection.cs`:
```csharp
services.AddScoped<ISlaCalculationService, SlaCalculationService>();
```

#### Dependencies
- `ITicketPriorityRepository`: For retrieving priority SLA targets
- `ILogger<SlaCalculationService>`: For logging and diagnostics

### Requirements Satisfied

**Requirement 4.1-4.12: Ticket Priority System**
- ✓ 4.2: Default SLA target hours for each priority level
- ✓ 4.5: Automatically calculate expected resolution date
- ✓ 4.6: Highlight overdue tickets that exceed SLA targets
- ✓ 4.9: Escalate tickets that approach SLA deadline
- ✓ 4.12: Calculate SLA compliance based on status transitions

### Business Hours Configuration

```csharp
// Current configuration (can be made configurable via appsettings.json)
Business Hours: 8 AM to 5 PM (9 hours per day)
Weekend Days: Saturday, Sunday
Holiday Calendar: Placeholder (to be implemented with database table)
```

### Usage Examples

#### Calculate SLA Deadline
```csharp
var deadline = await _slaService.CalculateSlaDeadlineAsync(
    priorityId: 1,
    creationDate: DateTime.Now,
    excludeWeekends: true,
    excludeHolidays: true
);
```

#### Check Escalation Need
```csharp
var needsEscalation = await _slaService.NeedsEscalationAsync(
    priorityId: 1,
    creationDate: ticket.CreationDate,
    lastUpdateDate: ticket.UpdateDate,
    isResolved: ticket.TicketStatusId == resolvedStatusId
);
```

#### Get SLA Status
```csharp
var status = _slaService.GetSlaStatus(
    expectedResolutionDate: ticket.ExpectedResolutionDate,
    actualResolutionDate: ticket.ActualResolutionDate,
    isResolved: ticket.TicketStatusId == resolvedStatusId
);
```

#### Calculate Compliance Rate
```csharp
var complianceRate = _slaService.CalculateSlaComplianceRate(
    totalTickets: 100,
    ticketsMetSla: 85
); // Returns 85.00
```

### Future Enhancements

1. **Holiday Calendar Integration**
   - Create `SYS_HOLIDAY_CALENDAR` table
   - Implement holiday lookup in `IsHoliday()` method
   - Support company-specific and regional holidays

2. **Configurable Business Hours**
   - Move business hours to configuration (appsettings.json)
   - Support different business hours per company/branch
   - Support different time zones

3. **SLA Pause/Resume**
   - Implement SLA clock pause when ticket is "Pending Customer"
   - Resume SLA clock when customer responds
   - Track paused time in audit trail

4. **Advanced Escalation Rules**
   - Multi-level escalation (Level 1, Level 2, Level 3)
   - Escalation based on ticket type and priority combination
   - Automatic assignment on escalation

### Test Results

```
Test summary: total: 21, failed: 0, succeeded: 21, skipped: 0
All tests passing ✓
```

### Test Coverage

- SLA deadline calculation with valid priority
- SLA deadline calculation with weekend exclusion
- SLA deadline calculation with invalid priority (exception handling)
- Escalation alert time calculation
- SLA status determination (NotSet, OnTrack, AtRisk, Breached, Met)
- Escalation need detection
- Compliance rate calculation (0%, 100%, partial)
- Time remaining until breach
- Business hours calculation (same day, multiple days, with weekends)

### Notes

- The service uses business hours by default (8 AM - 5 PM)
- Weekend exclusion is enabled by default
- Holiday calendar is a placeholder for future implementation
- All calculations respect business hours to provide accurate SLA tracking
- The service is fully tested and ready for integration with ticket creation and reporting

### Next Steps

Task 11.2 will implement the escalation background service that uses this SLA calculation service to automatically monitor and escalate tickets approaching their SLA deadlines.
