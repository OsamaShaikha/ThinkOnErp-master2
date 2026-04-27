# Task 8.7: Correlation ID-Based Query Implementation

## Summary

Successfully implemented the correlation ID-based query endpoint for request tracing in the AuditLogsController. This feature enables administrators to retrieve all audit log entries associated with a single API request using its unique correlation ID, which is essential for debugging and request tracing across the system.

## Implementation Details

### 1. Service Layer (Already Implemented)
The service layer was already fully implemented in previous tasks:
- **IAuditQueryService.GetByCorrelationIdAsync**: Interface method defined
- **AuditQueryService.GetByCorrelationIdAsync**: Implementation with caching support
- **IAuditRepository.GetByCorrelationIdAsync**: Repository interface method
- **AuditRepository.GetByCorrelationIdAsync**: Database query implementation

### 2. API Controller Endpoint (New Implementation)

#### Added IAuditQueryService Dependency
Updated `AuditLogsController` constructor to inject `IAuditQueryService`:
```csharp
public AuditLogsController(
    ILegacyAuditService legacyAuditService,
    IAuditQueryService auditQueryService,
    ILogger<AuditLogsController> logger)
```

#### Implemented GET /api/auditlogs/correlation/{correlationId} Endpoint
- **Route**: `GET /api/auditlogs/correlation/{correlationId}`
- **Authorization**: Requires `AdminOnly` policy
- **Validation**: Validates correlation ID is not null, empty, or whitespace
- **Response**: Returns `ApiResponse<IEnumerable<AuditLogDto>>` with all matching audit logs
- **Error Handling**: Returns 400 Bad Request for invalid correlation IDs
- **Logging**: Logs retrieval attempts and results

#### Key Features:
1. **Input Validation**: Checks for null, empty, or whitespace correlation IDs
2. **Data Transformation**: Converts `AuditLogEntry` domain models to `AuditLogDto` DTOs
3. **Comprehensive Mapping**: Maps all 24 fields from domain model to DTO
4. **Informative Responses**: Includes count of retrieved entries in response message
5. **Error Logging**: Logs errors with correlation ID context

### 3. Data Transfer Object (New Implementation)

Created `AuditLogDto` in `src/ThinkOnErp.Application/DTOs/Audit/AuditLogDto.cs`:
- 24 properties covering all audit log fields
- Comprehensive XML documentation for each property
- Matches the design specification exactly

#### DTO Properties:
- **Identity**: Id, CorrelationId
- **Actor Information**: ActorType, ActorId, ActorName
- **Multi-Tenant Context**: CompanyId, BranchId
- **Action Details**: Action, EntityType, EntityId
- **Change Tracking**: OldValue, NewValue
- **Request Context**: IpAddress, UserAgent, HttpMethod, EndpointPath
- **Performance Metrics**: ExecutionTimeMs, StatusCode
- **Error Information**: ExceptionType, ExceptionMessage
- **Classification**: Severity, EventCategory
- **Timestamp**: Timestamp

### 4. Unit Tests (New Implementation)

Added 6 comprehensive unit tests in `AuditLogsControllerUnitTests.cs`:

1. **GetByCorrelationId_WithValidCorrelationId_ReturnsOkResult**
   - Tests successful retrieval with multiple audit logs
   - Verifies correct response structure and data

2. **GetByCorrelationId_WithEmptyCorrelationId_ReturnsBadRequest**
   - Tests validation for empty string
   - Verifies 400 Bad Request response

3. **GetByCorrelationId_WithNullCorrelationId_ReturnsBadRequest**
   - Tests validation for null value
   - Verifies 400 Bad Request response

4. **GetByCorrelationId_WithWhitespaceCorrelationId_ReturnsBadRequest**
   - Tests validation for whitespace-only string
   - Verifies 400 Bad Request response

5. **GetByCorrelationId_WithNoResults_ReturnsEmptyList**
   - Tests behavior when no matching logs exist
   - Verifies empty list is returned with success status

6. **GetByCorrelationId_MapsAllFieldsCorrectly**
   - Tests complete field mapping from domain model to DTO
   - Verifies all 24 fields are correctly mapped

### Test Results
All 6 tests passed successfully:
```
Test summary: total: 6, failed: 0, succeeded: 6, skipped: 0, duration: 4.3s
```

## Files Modified

1. **src/ThinkOnErp.API/Controllers/AuditLogsController.cs**
   - Added IAuditQueryService dependency injection
   - Implemented GetByCorrelationId endpoint

2. **tests/ThinkOnErp.API.Tests/Controllers/AuditLogsControllerUnitTests.cs**
   - Added IAuditQueryService mock
   - Added 6 unit tests for GetByCorrelationId endpoint

## Files Created

1. **src/ThinkOnErp.Application/DTOs/Audit/AuditLogDto.cs**
   - New DTO for audit log entries
   - 24 properties with comprehensive documentation

## API Endpoint Specification

### Request
```
GET /api/auditlogs/correlation/{correlationId}
Authorization: Bearer <admin-token>
```

### Response (Success - 200 OK)
```json
{
  "success": true,
  "message": "Retrieved 2 audit log entries for correlation ID test-correlation-id-123",
  "data": [
    {
      "id": 1,
      "correlationId": "test-correlation-id-123",
      "actorType": "USER",
      "actorId": 1,
      "actorName": "Test User",
      "companyId": 1,
      "branchId": 1,
      "action": "INSERT",
      "entityType": "SYS_USERS",
      "entityId": 100,
      "oldValue": null,
      "newValue": "{\"name\":\"John Doe\"}",
      "ipAddress": "192.168.1.1",
      "userAgent": "Mozilla/5.0",
      "httpMethod": "POST",
      "endpointPath": "/api/users",
      "executionTimeMs": 150,
      "statusCode": 200,
      "exceptionType": null,
      "exceptionMessage": null,
      "severity": "Info",
      "eventCategory": "DataChange",
      "timestamp": "2024-01-15T10:30:00Z"
    }
  ],
  "statusCode": 200
}
```

### Response (Error - 400 Bad Request)
```json
{
  "success": false,
  "message": "Correlation ID cannot be empty",
  "data": null,
  "statusCode": 400
}
```

## Use Cases

### 1. Request Tracing
Administrators can trace all operations performed during a single API request:
```
GET /api/auditlogs/correlation/abc-123-def-456
```
Returns all audit logs (data changes, authentication events, exceptions) for that request.

### 2. Debugging
When investigating an issue, developers can:
1. Get the correlation ID from logs or error reports
2. Query all audit logs for that correlation ID
3. See the complete sequence of operations and any errors

### 3. Performance Analysis
Analyze request execution by:
1. Retrieving all audit logs for a correlation ID
2. Examining execution times for each operation
3. Identifying bottlenecks in the request flow

## Integration with Existing System

The endpoint integrates seamlessly with:
- **RequestTracingMiddleware**: Generates correlation IDs for all requests
- **AuditLogger**: Associates all audit events with correlation IDs
- **AuditQueryService**: Provides efficient querying with caching
- **Authorization System**: Requires AdminOnly policy for access

## Performance Considerations

1. **Database Query**: Uses indexed CORRELATION_ID column for fast lookups
2. **Caching**: AuditQueryService implements Redis caching for frequently accessed correlation IDs
3. **Efficient Mapping**: Uses LINQ Select for efficient DTO transformation
4. **Logging**: Minimal logging overhead with structured logging

## Security

1. **Authorization**: Endpoint requires AdminOnly policy
2. **Input Validation**: Validates correlation ID format
3. **No Sensitive Data Exposure**: Sensitive fields are already masked by AuditLogger
4. **Audit Trail**: All access to audit logs is itself logged

## Compliance

This feature supports:
- **GDPR**: Enables tracking of all operations related to personal data access
- **SOX**: Provides audit trail for financial data access
- **ISO 27001**: Supports security event investigation and analysis

## Next Steps

Task 8.7 is now complete. The correlation ID-based query functionality is fully implemented and tested. The next tasks in the spec are:
- Task 8.8: Implement entity history query for audit trails
- Task 8.9: Implement user action replay functionality
- Task 8.10: Implement query timeout protection (30 seconds max)

## Verification

To verify the implementation:
1. Run the unit tests: `dotnet test --filter "FullyQualifiedName~AuditLogsControllerUnitTests.GetByCorrelationId"`
2. Start the API and authenticate as an admin user
3. Make an API request and note the correlation ID from the response headers
4. Query the audit logs: `GET /api/auditlogs/correlation/{correlationId}`
5. Verify all audit logs for that request are returned

## Conclusion

Task 8.7 has been successfully completed. The correlation ID-based query endpoint provides administrators with a powerful tool for request tracing, debugging, and compliance reporting. The implementation follows the design specification, includes comprehensive tests, and integrates seamlessly with the existing audit logging infrastructure.
