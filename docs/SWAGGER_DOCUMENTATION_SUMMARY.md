# Swagger/OpenAPI Documentation Summary

## Overview

The ThinkOnErp API has comprehensive Swagger/OpenAPI documentation configured for the Full Traceability System. This document provides an overview of the documentation structure and how to access it.

## Accessing Swagger UI

The Swagger UI is available at:
- **Development**: `https://localhost:5001/swagger` or `http://localhost:5000/swagger`
- **Production**: `https://your-domain.com/swagger`

**Note**: Swagger is enabled in all environments (not just development) to facilitate API testing and documentation access.

## API Documentation Structure

### 1. AuditLogs Controller (`/api/auditlogs`)

Provides comprehensive audit logging functionality including legacy endpoints that match the logs.png interface.

#### Endpoints:

**Legacy Compatibility Endpoints:**
- `GET /api/auditlogs/legacy` - Get audit logs in legacy format (compatible with logs.png)
  - Supports filtering by company, module, branch, status, date range, and search term
  - Returns paginated results with Error Description, Module, Company, Branch, User, Device, Date & Time, Status
  
- `GET /api/auditlogs/dashboard` - Get dashboard counters (Unresolved, In Progress, Resolved, Critical)
  
- `PUT /api/auditlogs/legacy/{id}/status` - Update audit log status (Unresolved → In Progress → Resolved)
  
- `GET /api/auditlogs/{id}/status` - Get current status of an audit log entry
  
- `POST /api/auditlogs/transform` - Transform comprehensive audit entry to legacy format

**Modern Audit Query Endpoints:**
- `GET /api/auditlogs/correlation/{correlationId}` - Get all audit logs for a specific correlation ID
  - Useful for request tracing and debugging
  
- `GET /api/auditlogs/entity/{entityType}/{entityId}` - Get complete audit history for a specific entity
  - Returns all modifications (INSERT, UPDATE, DELETE) in chronological order
  
- `GET /api/auditlogs/replay/user/{userId}` - Get user action replay for debugging
  - Returns chronological sequence of all user actions with request/response payloads
  
- `POST /api/auditlogs/query` - Query audit logs with comprehensive filtering
  - Supports filtering by date range, actor, entity, action type, and more
  
- `GET /api/auditlogs/search` - Full-text search across audit logs
  
- `POST /api/auditlogs/export/csv` - Export audit logs to CSV format

### 2. Compliance Controller (`/api/compliance`)

Provides REST API endpoints for generating GDPR, SOX, and ISO 27001 compliance reports.

#### GDPR Reports:
- `GET /api/compliance/gdpr/access-report` - Generate GDPR data access report
  - Supports GDPR Article 15 (Right of Access)
  - Parameters: dataSubjectId, startDate, endDate, format (json/csv/pdf)
  
- `GET /api/compliance/gdpr/data-export` - Generate GDPR data export report
  - Supports GDPR Article 20 (Right to Data Portability)
  - Parameters: dataSubjectId, format (json/csv/pdf)

#### SOX Reports:
- `GET /api/compliance/sox/financial-access` - Generate SOX financial data access report
  - Supports SOX Section 404 (Internal Controls)
  - Parameters: startDate, endDate, format (json/csv/pdf)
  
- `GET /api/compliance/sox/segregation-of-duties` - Generate SOX segregation of duties report
  - Identifies potential segregation of duties violations
  - Parameters: format (json/csv/pdf)

#### ISO 27001 Reports:
- `GET /api/compliance/iso27001/security-report` - Generate ISO 27001 security event report
  - Supports ISO 27001 Annex A.12.4 (Logging and Monitoring)
  - Parameters: startDate, endDate, format (json/csv/pdf)

#### General Reports:
- `GET /api/compliance/user-activity` - Generate user activity report
  - Chronological report of all user actions
  - Parameters: userId, startDate, endDate, format (json/csv/pdf)
  
- `GET /api/compliance/data-modification` - Generate data modification report
  - Complete audit trail of all modifications for a specific entity
  - Parameters: entityType, entityId, format (json/csv/pdf)

### 3. Monitoring Controller (`/api/monitoring`)

Provides real-time insights into system resource usage, performance characteristics, and security threats.

#### System Health:
- `GET /api/monitoring/health` - Get comprehensive system health metrics
  - **AllowAnonymous** - No authentication required
  - Returns CPU, memory, database connections, request rate, error rate, audit queue depth
  
- `GET /api/monitoring/memory` - Get detailed memory usage metrics
  - Returns heap sizes (Gen0, Gen1, Gen2, LOH), GC statistics, allocation rate
  
- `GET /api/monitoring/memory/pressure` - Detect current memory pressure level
  - Returns severity (None, Low, Moderate, High, Critical) and recommendations
  
- `GET /api/monitoring/memory/recommendations` - Get memory optimization recommendations
  
- `POST /api/monitoring/memory/optimize` - Trigger memory optimization strategies
  - **WARNING**: Can temporarily impact performance
  
- `POST /api/monitoring/memory/gc` - Force garbage collection
  - **WARNING**: Use sparingly, only during low-traffic periods
  - Parameters: generation (0-2), blocking, compacting

#### Performance Monitoring:
- `GET /api/monitoring/performance/endpoint` - Get performance statistics for an endpoint
  - Parameters: endpoint, periodMinutes
  - Returns request count, avg/min/max execution time, percentiles (p50, p95, p99)
  
- `GET /api/monitoring/performance/slow-requests` - Get slow requests exceeding threshold
  - Parameters: thresholdMs (default: 1000), pageNumber, pageSize
  
- `GET /api/monitoring/performance/slow-queries` - Get slow database queries
  - Parameters: thresholdMs (default: 500), pageNumber, pageSize
  
- `GET /api/monitoring/performance/connection-pool` - Get Oracle connection pool metrics
  - Returns active/idle connections, pool size, utilization percentage

#### Audit System Monitoring:
- `GET /api/monitoring/audit-queue-depth` - Get current audit queue depth
  - Returns queue depth, capacity, utilization percentage, status
  
- `GET /api/monitoring/audit/metrics` - Get comprehensive audit logging system metrics
  - Returns queue depth, circuit breaker state, success/failure rates, pending fallback files

#### Security Monitoring:
- `GET /api/monitoring/security/threats` - Get all active security threats
  - Returns paginated list of threats ordered by severity and detection time
  
- `GET /api/monitoring/security/daily-summary` - Generate daily security summary report
  - Parameters: date (default: today)
  - Returns threat counts by type/severity, top sources, resolution statistics
  
- `GET /api/monitoring/security/check-failed-logins` - Check for failed login patterns
  - Parameters: ipAddress
  - Returns SecurityThreat if pattern detected
  
- `GET /api/monitoring/security/failed-login-count` - Get failed login count for user
  - Parameters: username
  - Returns count and status (Normal, Warning, Blocked)
  
- `POST /api/monitoring/security/check-sql-injection` - Detect SQL injection patterns
  - Body: input text to scan
  
- `POST /api/monitoring/security/check-xss` - Detect XSS patterns
  - Body: input text to scan
  
- `GET /api/monitoring/security/check-anomalous-activity` - Detect anomalous user activity
  - Parameters: userId

### 4. Alerts Controller (`/api/alerts`)

Provides REST API endpoints for managing alert rules, viewing alert history, and configuring notification channels.

#### Alert Rules Management:
- `GET /api/alerts/rules` - Get all configured alert rules
  - Parameters: pageNumber, pageSize
  - Returns paginated list of alert rules with conditions, thresholds, notification settings
  
- `POST /api/alerts/rules` - Create a new alert rule
  - Body: CreateAlertRuleDto (name, description, eventType, severityThreshold, condition, notificationChannels)
  
- `PUT /api/alerts/rules/{id}` - Update an existing alert rule
  - Body: UpdateAlertRuleDto
  
- `DELETE /api/alerts/rules/{id}` - Delete an alert rule

#### Alert History:
- `GET /api/alerts/history` - Get alert history with pagination
  - Parameters: pageNumber (default: 1), pageSize (default: 20, max: 100)
  - Returns historical alerts with acknowledgment and resolution status

#### Alert Acknowledgment and Resolution:
- `POST /api/alerts/{id}/acknowledge` - Acknowledge an alert
  - Body: AcknowledgeAlertDto (optional notes)
  - Records who acknowledged it and when
  
- `POST /api/alerts/{id}/resolve` - Resolve an alert
  - Body: ResolveAlertDto (resolutionNotes required)
  - Updates status to 'Resolved' and records resolution details

#### Notification Channel Testing:
- `POST /api/alerts/test/email` - Test email notification channel
  - Body: array of recipient email addresses
  
- `POST /api/alerts/test/webhook` - Test webhook notification channel
  - Body: webhookUrl
  
- `POST /api/alerts/test/sms` - Test SMS notification channel
  - Body: array of phone numbers (E.164 format)

## Authentication and Authorization

### JWT Bearer Authentication

All endpoints (except `/api/monitoring/health`) require JWT Bearer authentication.

**How to authenticate in Swagger UI:**
1. Click the "Authorize" button at the top right
2. Enter your JWT token in the "Value" field (without "Bearer " prefix)
3. Click "Authorize"
4. The token will be automatically included in all subsequent requests

**Obtaining a JWT token:**
```bash
POST /api/auth/login
Content-Type: application/json

{
  "username": "your-username",
  "password": "your-password"
}
```

Response:
```json
{
  "success": true,
  "data": {
    "token": "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9...",
    "refreshToken": "...",
    "expiresAt": "2024-01-15T12:00:00Z"
  }
}
```

### Authorization Policies

- **AdminOnly**: Required for all audit logs, compliance, monitoring, and alerts endpoints
  - User must have `isAdmin: true` claim in JWT token
  
- **MultiTenantAccess**: Automatically filters data by user's company and branch permissions
  
- **AuditDataAccess**: Controls access to audit data with optional self-access

## Request/Response Format

### Standard API Response Wrapper

All API responses use a standard wrapper format:

```json
{
  "success": true,
  "data": { ... },
  "message": "Operation completed successfully",
  "statusCode": 200
}
```

Error responses:
```json
{
  "success": false,
  "data": null,
  "message": "Error description",
  "statusCode": 400
}
```

### Pagination

Paginated endpoints return:
```json
{
  "items": [ ... ],
  "totalCount": 150,
  "page": 1,
  "pageSize": 50,
  "totalPages": 3,
  "hasNextPage": true,
  "hasPreviousPage": false
}
```

### Export Formats

Compliance and audit export endpoints support multiple formats:
- **JSON** (default): Returns data in JSON format with ApiResponse wrapper
- **CSV**: Returns CSV file with appropriate Content-Type header
- **PDF**: Returns PDF file (implementation pending for some reports)

Specify format using query parameter: `?format=csv` or `?format=pdf`

Or use Accept header:
- `Accept: application/json`
- `Accept: text/csv`
- `Accept: application/pdf`

## Common Query Parameters

### Pagination Parameters
- `pageNumber` (int): Page number (1-based, default: 1)
- `pageSize` (int): Items per page (default: 50, max: 100)

### Date Range Parameters
- `startDate` (DateTime): Start date in ISO 8601 format (e.g., "2024-01-01T00:00:00Z")
- `endDate` (DateTime): End date in ISO 8601 format

### Filter Parameters (Audit Logs)
- `company` (string): Filter by company name
- `module` (string): Filter by business module (POS, HR, Accounting, etc.)
- `branch` (string): Filter by branch name
- `status` (string): Filter by status (Unresolved, In Progress, Resolved, Critical)
- `searchTerm` (string): Search across description, user, device, error code

## Response Status Codes

- **200 OK**: Request succeeded
- **201 Created**: Resource created successfully
- **204 No Content**: Resource deleted successfully
- **400 Bad Request**: Invalid request parameters or data
- **401 Unauthorized**: Authentication required or token invalid
- **403 Forbidden**: User lacks required permissions
- **404 Not Found**: Resource not found
- **500 Internal Server Error**: Server error occurred

## Rate Limiting

- Failed login attempts are tracked and blocked after 5 attempts within 5 minutes
- Alert notifications are rate-limited to prevent flooding (max 10 per rule per hour)
- API requests may be subject to rate limiting to prevent abuse

## Correlation IDs

All API requests are assigned a unique correlation ID for request tracing:
- Automatically generated by RequestTracingMiddleware
- Included in response header: `X-Correlation-ID`
- Used to trace requests through the entire system
- Queryable via `/api/auditlogs/correlation/{correlationId}`

## Examples

### Example 1: Query Audit Logs

```bash
GET /api/auditlogs/legacy?company=Acme%20Corp&status=Unresolved&pageNumber=1&pageSize=20
Authorization: Bearer eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9...
```

Response:
```json
{
  "success": true,
  "data": {
    "items": [
      {
        "id": 12345,
        "errorDescription": "Database connection timeout",
        "module": "POS",
        "company": "Acme Corp",
        "branch": "Main Branch",
        "user": "john.doe",
        "device": "POS Terminal 03",
        "dateTime": "2024-01-15T10:30:00Z",
        "status": "Unresolved",
        "errorCode": "DB_TIMEOUT_001"
      }
    ],
    "totalCount": 45,
    "page": 1,
    "pageSize": 20,
    "totalPages": 3
  },
  "message": "Legacy audit logs retrieved successfully (45 total entries)"
}
```

### Example 2: Generate GDPR Access Report

```bash
GET /api/compliance/gdpr/access-report?dataSubjectId=1001&startDate=2024-01-01T00:00:00Z&endDate=2024-01-31T23:59:59Z&format=json
Authorization: Bearer eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9...
```

### Example 3: Get System Health

```bash
GET /api/monitoring/health
```

Response:
```json
{
  "status": "Healthy",
  "cpuUsagePercent": 45.2,
  "memoryUsedMb": 1024,
  "memoryAvailableMb": 2048,
  "databaseConnectionsActive": 5,
  "databaseConnectionsIdle": 15,
  "requestsPerMinute": 150,
  "errorRate": 0.5,
  "auditQueueDepth": 25,
  "timestamp": "2024-01-15T10:30:00Z"
}
```

### Example 4: Create Alert Rule

```bash
POST /api/alerts/rules
Authorization: Bearer eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9...
Content-Type: application/json

{
  "name": "High Error Rate Alert",
  "description": "Alert when error rate exceeds 5%",
  "eventType": "Exception",
  "severityThreshold": "Error",
  "condition": "ErrorRate > 5",
  "notificationChannels": "Email,Webhook",
  "emailRecipients": ["admin@example.com"],
  "webhookUrl": "https://hooks.example.com/alerts"
}
```

## XML Documentation

All controllers and DTOs include comprehensive XML documentation comments:
- `<summary>`: Brief description of the endpoint or type
- `<remarks>`: Detailed information, usage notes, warnings
- `<param>`: Parameter descriptions
- `<returns>`: Return value description
- `<response>`: HTTP response code documentation
- `<example>`: Usage examples

XML documentation is automatically included in Swagger UI from:
- `ThinkOnErp.API.xml` - Controller documentation
- `ThinkOnErp.Application.xml` - DTO documentation
- `ThinkOnErp.Domain.xml` - Domain model documentation

## Swagger Configuration

Swagger is configured in `Program.cs` with:
- API information (title, version, description, contact, license)
- JWT Bearer authentication support
- XML documentation inclusion from all projects
- Custom schema IDs to avoid naming conflicts
- Example values for common types (DateTime, TimeSpan)
- Action ordering by controller and HTTP method
- Annotation support for enhanced documentation

## Testing with Swagger UI

1. **Navigate to Swagger UI**: Open browser to `/swagger`
2. **Authenticate**: Click "Authorize" and enter JWT token
3. **Explore Endpoints**: Browse available endpoints by controller
4. **Try It Out**: Click "Try it out" on any endpoint
5. **Execute Request**: Fill in parameters and click "Execute"
6. **View Response**: See response body, headers, and status code

## Additional Resources

- **API Source Code**: `src/ThinkOnErp.API/Controllers/`
- **DTO Definitions**: `src/ThinkOnErp.Application/DTOs/`
- **Domain Models**: `src/ThinkOnErp.Domain/Models/`
- **Swagger Configuration**: `src/ThinkOnErp.API/Program.cs`
- **Design Document**: `.kiro/specs/full-traceability-system/design.md`
- **Requirements**: `.kiro/specs/full-traceability-system/requirements.md`

## Support and Feedback

For API support, bug reports, or feature requests:
- Email: support@thinkonerp.com
- Documentation: `/swagger`
- Source Code: Contact development team

---

**Last Updated**: 2024-01-15
**API Version**: v1.0
**Swagger/OpenAPI Version**: 3.0
