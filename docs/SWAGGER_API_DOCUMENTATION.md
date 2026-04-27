# Swagger API Documentation Guide

## Overview

The ThinkOnErp API includes comprehensive Swagger/OpenAPI documentation for all audit trail endpoints. This guide explains how to access, use, and extend the API documentation.

## Accessing Swagger UI

### Development Environment
Navigate to: `https://localhost:5001/swagger` or `http://localhost:5000/swagger`

### Production Environment
Navigate to: `https://your-domain.com/swagger`

**Note:** Swagger UI is enabled in all environments for this API. For production deployments, consider restricting access using authentication or IP whitelisting.

## Features

### 1. Comprehensive API Information
- **Title**: ThinkOnErp API - Full Traceability System
- **Version**: v1.0
- **Description**: Detailed overview of all API capabilities
- **Contact Information**: Development team contact details
- **License**: Proprietary license information

### 2. JWT Bearer Authentication
- Integrated JWT authentication in Swagger UI
- Click "Authorize" button to enter your JWT token
- Token is automatically included in all API requests
- No need to manually add "Bearer " prefix

### 3. XML Documentation Comments
All controllers, actions, parameters, and DTOs include comprehensive XML documentation:
- **Summary**: Brief description of the endpoint
- **Remarks**: Detailed usage information and examples
- **Parameters**: Description of each parameter with validation rules
- **Returns**: Description of response types
- **Response Codes**: HTTP status codes with descriptions

### 4. Request/Response Examples
- Automatic schema generation for all DTOs
- Example values for common types (DateTime, TimeSpan)
- Full request body examples
- Response body examples with all properties

### 5. Organized Endpoint Groups
Endpoints are organized by controller:
- **AuditLogs**: Audit log querying and management
- **Compliance**: GDPR, SOX, and ISO 27001 reports
- **Monitoring**: System health, performance, and security
- **Alerts**: Alert rule management and notification testing
- **Auth**: Authentication and authorization
- **Users, Companies, Branches**: Core entity management

## Audit Trail API Endpoints

### AuditLogs Controller

#### GET /api/auditlogs/legacy
**Purpose**: Retrieve audit logs in legacy format (compatible with logs.png interface)

**Parameters**:
- `company` (optional): Filter by company name
- `module` (optional): Filter by business module (POS, HR, Accounting)
- `branch` (optional): Filter by branch name
- `status` (optional): Filter by status (Unresolved, In Progress, Resolved, Critical)
- `startDate` (optional): Start date for date range filter
- `endDate` (optional): End date for date range filter
- `searchTerm` (optional): Search across description, user, device, error code
- `pageNumber` (default: 1): Page number for pagination
- `pageSize` (default: 50, max: 100): Items per page

**Response**: Paged list of legacy audit log entries with dashboard counters

**Authorization**: Requires AdminOnly policy

#### GET /api/auditlogs/dashboard
**Purpose**: Get dashboard counters for legacy view

**Response**: 
```json
{
  "unresolvedCount": 3,
  "inProgressCount": 3,
  "resolvedCount": 4,
  "criticalErrorsCount": 2
}
```

**Authorization**: Requires AdminOnly policy

#### PUT /api/auditlogs/legacy/{id}/status
**Purpose**: Update status of audit log entry for error resolution workflow

**Parameters**:
- `id` (path): Audit log entry ID
- Request body:
  ```json
  {
    "status": "In Progress",
    "resolutionNotes": "Investigating database timeout issue",
    "assignedToUserId": 123
  }
  ```

**Authorization**: Requires AdminOnly policy

#### GET /api/auditlogs/correlation/{correlationId}
**Purpose**: Get all audit logs for a specific correlation ID (request tracing)

**Parameters**:
- `correlationId` (path): Correlation ID to search for

**Response**: List of all audit log entries associated with the request

**Use Case**: Debugging and request tracing across the entire system

**Authorization**: Requires AdminOnly policy

#### GET /api/auditlogs/entity/{entityType}/{entityId}
**Purpose**: Get complete audit history for a specific entity

**Parameters**:
- `entityType` (path): Entity type (e.g., "SysUser", "SysCompany")
- `entityId` (path): Entity unique identifier

**Response**: Chronological list of all modifications to the entity

**Use Case**: Compliance audits, data lineage tracking, investigating entity changes

**Authorization**: Requires AdminOnly policy

#### GET /api/auditlogs/replay/user/{userId}
**Purpose**: Get user action replay for debugging and analysis

**Parameters**:
- `userId` (path): User unique identifier
- `startDate` (query): Start date of replay range
- `endDate` (query): End date of replay range

**Response**: Complete chronological sequence of user actions with full context

**Use Case**: Reproducing bugs, understanding user workflows, investigating behavior

**Authorization**: Requires AdminOnly policy

### Compliance Controller

#### GET /api/compliance/gdpr/access-report
**Purpose**: Generate GDPR data access report for a data subject

**Parameters**:
- `dataSubjectId` (query): User ID of the data subject
- `startDate` (query): Report period start date
- `endDate` (query): Report period end date
- `format` (query, default: "json"): Export format (json, csv, pdf)

**Response**: Comprehensive report of all access to personal data

**Compliance**: GDPR Article 15 (Right of Access)

**Authorization**: Requires AdminOnly policy

#### GET /api/compliance/gdpr/data-export
**Purpose**: Generate GDPR data export report for data portability

**Parameters**:
- `dataSubjectId` (query): User ID of the data subject
- `format` (query, default: "json"): Export format (json, csv, pdf)

**Response**: Complete export of all personal data

**Compliance**: GDPR Article 20 (Right to Data Portability)

**Authorization**: Requires AdminOnly policy

#### GET /api/compliance/sox/financial-access
**Purpose**: Generate SOX financial data access report

**Parameters**:
- `startDate` (query): Report period start date
- `endDate` (query): Report period end date
- `format` (query, default: "json"): Export format (json, csv, pdf)

**Response**: Comprehensive report of financial data access events

**Compliance**: SOX Section 404 (Internal Controls)

**Authorization**: Requires AdminOnly policy

#### GET /api/compliance/sox/segregation-of-duties
**Purpose**: Generate SOX segregation of duties report

**Parameters**:
- `format` (query, default: "json"): Export format (json, csv, pdf)

**Response**: Report identifying potential segregation of duties violations

**Compliance**: SOX Section 404 (Internal Controls)

**Authorization**: Requires AdminOnly policy

#### GET /api/compliance/iso27001/security-report
**Purpose**: Generate ISO 27001 security event report

**Parameters**:
- `startDate` (query): Report period start date
- `endDate` (query): Report period end date
- `format` (query, default: "json"): Export format (json, csv, pdf)

**Response**: Comprehensive security events report

**Compliance**: ISO 27001 Annex A.12.4 (Logging and Monitoring)

**Authorization**: Requires AdminOnly policy

#### GET /api/compliance/user-activity
**Purpose**: Generate user activity report

**Parameters**:
- `userId` (query): User unique identifier
- `startDate` (query): Report period start date
- `endDate` (query): Report period end date
- `format` (query, default: "json"): Export format (json, csv, pdf)

**Response**: Chronological report of all user actions

**Use Case**: User behavior analysis, compliance audits, security investigations

**Authorization**: Requires AdminOnly policy

#### GET /api/compliance/data-modification
**Purpose**: Generate data modification report for an entity

**Parameters**:
- `entityType` (query): Entity type (e.g., "SysUser")
- `entityId` (query): Entity unique identifier
- `format` (query, default: "json"): Export format (json, csv, pdf)

**Response**: Complete audit trail of entity modifications

**Use Case**: Data lineage tracking, compliance audits, debugging

**Authorization**: Requires AdminOnly policy

### Monitoring Controller

#### GET /api/monitoring/health
**Purpose**: Get comprehensive system health metrics

**Response**: 
```json
{
  "status": "Healthy",
  "cpuUtilization": 45.2,
  "memoryUsageMB": 512,
  "databaseConnectionPoolUtilization": 30,
  "auditQueueDepth": 150,
  "requestRate": 1250,
  "errorRate": 0.5,
  "healthChecks": [...]
}
```

**Authorization**: AllowAnonymous (public health check)

#### GET /api/monitoring/memory
**Purpose**: Get detailed memory usage metrics

**Response**: Heap sizes, GC statistics, memory allocation rate, pressure indicators

**Authorization**: Requires AdminOnly policy

#### GET /api/monitoring/memory/pressure
**Purpose**: Detect current memory pressure level

**Response**: Severity level, percentage, description, recommendations

**Authorization**: Requires AdminOnly policy

#### POST /api/monitoring/memory/optimize
**Purpose**: Trigger memory optimization strategies

**Warning**: Can temporarily impact performance. Use during low-traffic periods.

**Authorization**: Requires AdminOnly policy

#### POST /api/monitoring/memory/gc
**Purpose**: Force garbage collection

**Parameters**:
- `generation` (query, default: 2): GC generation (0, 1, or 2)
- `blocking` (query, default: true): Wait for GC to complete
- `compacting` (query, default: true): Compact the heap

**Warning**: Can impact performance. Use sparingly.

**Authorization**: Requires AdminOnly policy

#### GET /api/monitoring/performance/endpoint
**Purpose**: Get performance statistics for a specific endpoint

**Parameters**:
- `endpoint` (query): Endpoint path
- `periodMinutes` (query, default: 60): Time period in minutes

**Response**: Request count, average/min/max execution times, percentiles (p50, p95, p99)

**Authorization**: Requires AdminOnly policy

#### GET /api/monitoring/performance/slow-requests
**Purpose**: Get slow requests exceeding threshold

**Parameters**:
- `thresholdMs` (query, default: 1000): Execution time threshold
- `pageNumber` (query, default: 1): Page number
- `pageSize` (query, default: 50, max: 100): Items per page

**Response**: Paged list of slow requests with full context

**Authorization**: Requires AdminOnly policy

#### GET /api/monitoring/performance/slow-queries
**Purpose**: Get slow database queries exceeding threshold

**Parameters**:
- `thresholdMs` (query, default: 500): Execution time threshold
- `pageNumber` (query, default: 1): Page number
- `pageSize` (query, default: 50, max: 100): Items per page

**Response**: Paged list of slow queries with SQL statements

**Authorization**: Requires AdminOnly policy

#### GET /api/monitoring/audit-queue-depth
**Purpose**: Get current audit queue depth

**Response**: Queue depth, max size, utilization percentage, status

**Authorization**: Requires AdminOnly policy

#### GET /api/monitoring/security/threats
**Purpose**: Get all active security threats

**Parameters**:
- `pageNumber` (query, default: 1): Page number
- `pageSize` (query, default: 50, max: 100): Items per page

**Response**: Paged list of active threats ordered by severity

**Authorization**: Requires AdminOnly policy

#### GET /api/monitoring/security/daily-summary
**Purpose**: Generate daily security summary report

**Parameters**:
- `date` (query, optional): Date for summary (default: today)

**Response**: Threat counts by type/severity, top sources, resolution stats, trends

**Authorization**: Requires AdminOnly policy

#### GET /api/monitoring/security/check-failed-logins
**Purpose**: Check for failed login patterns from an IP address

**Parameters**:
- `ipAddress` (query): IP address to check

**Response**: SecurityThreat if pattern detected, 404 if none

**Authorization**: Requires AdminOnly policy

#### GET /api/monitoring/security/failed-login-count
**Purpose**: Get failed login count for a user

**Parameters**:
- `username` (query): Username to check

**Response**: Count, threshold, status (Normal, Warning, Blocked)

**Authorization**: Requires AdminOnly policy

#### POST /api/monitoring/security/check-sql-injection
**Purpose**: Detect SQL injection patterns in input

**Request Body**: Input text to scan

**Response**: SecurityThreat if detected, 404 if none

**Authorization**: Requires AdminOnly policy

#### POST /api/monitoring/security/check-xss
**Purpose**: Detect XSS patterns in input

**Request Body**: Input text to scan

**Response**: SecurityThreat if detected, 404 if none

**Authorization**: Requires AdminOnly policy

#### GET /api/monitoring/security/check-anomalous-activity
**Purpose**: Detect anomalous activity for a user

**Parameters**:
- `userId` (query): User ID to check

**Response**: SecurityThreat if detected, 404 if none

**Authorization**: Requires AdminOnly policy

#### GET /api/monitoring/performance/connection-pool
**Purpose**: Get Oracle connection pool metrics

**Response**: Active/idle connections, pool size, utilization, health status

**Authorization**: Requires AdminOnly policy

### Alerts Controller

#### GET /api/alerts/rules
**Purpose**: Get all configured alert rules

**Parameters**:
- `pageNumber` (query, default: 1): Page number
- `pageSize` (query, default: 50, max: 100): Items per page

**Response**: Paged list of alert rules with conditions and notification settings

**Authorization**: Requires AdminOnly policy

#### POST /api/alerts/rules
**Purpose**: Create a new alert rule

**Request Body**:
```json
{
  "name": "High CPU Alert",
  "description": "Alert when CPU exceeds 80%",
  "eventType": "PerformanceMetric",
  "severityThreshold": "Warning",
  "condition": "cpuUtilization > 80",
  "notificationChannels": "email,webhook",
  "emailRecipients": ["admin@example.com"],
  "webhookUrl": "https://hooks.slack.com/...",
  "smsRecipients": ["+1234567890"]
}
```

**Authorization**: Requires AdminOnly policy

#### PUT /api/alerts/rules/{id}
**Purpose**: Update an existing alert rule

**Parameters**:
- `id` (path): Alert rule ID

**Request Body**: Same as create, all fields optional

**Authorization**: Requires AdminOnly policy

#### DELETE /api/alerts/rules/{id}
**Purpose**: Delete an alert rule

**Parameters**:
- `id` (path): Alert rule ID

**Authorization**: Requires AdminOnly policy

#### GET /api/alerts/history
**Purpose**: Get alert history with pagination

**Parameters**:
- `pageNumber` (query, default: 1): Page number
- `pageSize` (query, default: 20, max: 100): Items per page

**Response**: Paged list of triggered alerts with acknowledgment/resolution status

**Authorization**: Requires AdminOnly policy

#### POST /api/alerts/{id}/acknowledge
**Purpose**: Acknowledge an alert

**Parameters**:
- `id` (path): Alert ID

**Request Body** (optional):
```json
{
  "notes": "Investigating the issue"
}
```

**Authorization**: Requires AdminOnly policy

#### POST /api/alerts/{id}/resolve
**Purpose**: Resolve an alert

**Parameters**:
- `id` (path): Alert ID

**Request Body**:
```json
{
  "resolutionNotes": "Issue resolved by restarting service"
}
```

**Authorization**: Requires AdminOnly policy

#### POST /api/alerts/test/email
**Purpose**: Test email notification channel

**Request Body**: Array of email addresses

**Authorization**: Requires AdminOnly policy

#### POST /api/alerts/test/webhook
**Purpose**: Test webhook notification channel

**Request Body**: Webhook URL string

**Authorization**: Requires AdminOnly policy

#### POST /api/alerts/test/sms
**Purpose**: Test SMS notification channel

**Request Body**: Array of phone numbers (E.164 format)

**Authorization**: Requires AdminOnly policy

## Using Swagger UI

### Step 1: Authenticate
1. Click the "Authorize" button at the top right
2. Enter your JWT token (without "Bearer " prefix)
3. Click "Authorize" then "Close"
4. All subsequent requests will include the token

### Step 2: Explore Endpoints
1. Browse endpoints by controller group
2. Click on an endpoint to expand details
3. Read the description, parameters, and response schemas

### Step 3: Try It Out
1. Click "Try it out" button
2. Fill in required parameters
3. Modify request body if needed
4. Click "Execute"
5. View the response with status code, headers, and body

### Step 4: Copy cURL Command
1. After executing a request, scroll to "Curl" section
2. Copy the generated cURL command
3. Use it in scripts or command line

## Best Practices

### For API Consumers
1. **Always authenticate first**: Use `/api/auth/login` to get a JWT token
2. **Check response codes**: Handle 400, 401, 403, 404, 500 appropriately
3. **Use pagination**: Don't request all data at once, use pageSize and pageNumber
4. **Respect rate limits**: Failed login attempts are tracked and blocked
5. **Use correlation IDs**: Include X-Correlation-ID header for request tracing

### For API Developers
1. **Add XML comments**: Document all public APIs with `<summary>`, `<remarks>`, `<param>`, `<returns>`
2. **Use ProducesResponseType**: Specify all possible response types and status codes
3. **Provide examples**: Add example values in XML comments using `<example>` tags
4. **Group related endpoints**: Use consistent controller naming and routing
5. **Document authorization**: Clearly specify required policies and claims

## Extending Documentation

### Adding XML Comments to Controllers
```csharp
/// <summary>
/// Brief description of the endpoint
/// </summary>
/// <remarks>
/// Detailed description with usage examples and important notes.
/// 
/// **Example Request:**
/// ```
/// GET /api/example?param=value
/// ```
/// 
/// **Example Response:**
/// ```json
/// {
///   "id": 123,
///   "name": "Example"
/// }
/// ```
/// </remarks>
/// <param name="id">Description of the parameter</param>
/// <returns>Description of the return value</returns>
/// <response code="200">Success response description</response>
/// <response code="400">Bad request description</response>
/// <response code="404">Not found description</response>
[HttpGet("{id}")]
[ProducesResponseType(typeof(ApiResponse<ExampleDto>), StatusCodes.Status200OK)]
[ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
[ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
public async Task<IActionResult> GetExample(long id)
{
    // Implementation
}
```

### Adding XML Comments to DTOs
```csharp
/// <summary>
/// Data transfer object for example entity
/// </summary>
public class ExampleDto
{
    /// <summary>
    /// Unique identifier of the example
    /// </summary>
    /// <example>123</example>
    public long Id { get; set; }

    /// <summary>
    /// Name of the example (required, max 100 characters)
    /// </summary>
    /// <example>Example Name</example>
    [Required]
    [MaxLength(100)]
    public string Name { get; set; } = null!;

    /// <summary>
    /// Creation timestamp in UTC
    /// </summary>
    /// <example>2024-01-15T10:30:00Z</example>
    public DateTime CreatedAt { get; set; }
}
```

## Troubleshooting

### Swagger UI Not Loading
- Check that Swagger middleware is registered in Program.cs
- Verify XML documentation file is being generated (check .csproj)
- Check browser console for JavaScript errors

### Missing XML Comments
- Ensure `<GenerateDocumentationFile>true</GenerateDocumentationFile>` in .csproj
- Add `<NoWarn>$(NoWarn);1591</NoWarn>` to suppress missing comment warnings
- Rebuild the project to generate XML file

### Authentication Not Working
- Verify JWT token is valid and not expired
- Check token includes required claims (userId, companyId, isAdmin)
- Ensure "Bearer " prefix is NOT included when entering token in Swagger UI

### 403 Forbidden Errors
- Verify user has required authorization policy (AdminOnly)
- Check JWT token includes `isAdmin: true` claim
- Verify user has access to the requested company/branch

## Additional Resources

- [Swashbuckle Documentation](https://github.com/domaindrivendev/Swashbuckle.AspNetCore)
- [OpenAPI Specification](https://swagger.io/specification/)
- [XML Documentation Comments](https://learn.microsoft.com/en-us/dotnet/csharp/language-reference/xmldoc/)
- [ASP.NET Core Web API Documentation](https://learn.microsoft.com/en-us/aspnet/core/tutorials/web-api-help-pages-using-swagger)

## Support

For questions or issues with the API documentation:
- Contact: support@thinkonerp.com
- Internal Wiki: [API Documentation Guidelines](https://wiki.thinkonerp.com/api-docs)
- Slack Channel: #api-support
