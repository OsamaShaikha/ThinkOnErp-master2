# Full Traceability System - Postman Collection

This Postman collection provides comprehensive API testing coverage for the ThinkOnErp Full Traceability System.

## Overview

The Full Traceability System provides:
- **Comprehensive Audit Logging**: Track all data modifications, authentication events, and API requests
- **Compliance Reporting**: Generate GDPR, SOX, and ISO 27001 compliance reports
- **Performance Monitoring**: Monitor system health, performance metrics, and slow queries
- **Security Monitoring**: Detect and alert on security threats and suspicious activities
- **Alert Management**: Configure alert rules and manage notifications

## Collection Contents

### 1. Authentication
- **Login (Admin)**: Obtain JWT token for admin access
- **Refresh Token**: Refresh expired JWT tokens

### 2. Audit Logs
- **Get Legacy Audit Logs**: Query audit logs in legacy format (compatible with logs.png interface)
- **Get Dashboard Counters**: Get status counters (Unresolved, In Progress, Resolved, Critical)
- **Update Audit Log Status**: Update status for error resolution workflow
- **Get Audit Log Status**: Get current status of an audit log entry
- **Get Logs by Correlation ID**: Trace all logs for a specific request
- **Get Entity History**: Get complete audit trail for an entity
- **Get User Action Replay**: Replay user actions for debugging
- **Search Audit Logs**: Full-text search across audit logs
- **Export Audit Logs**: Export to CSV or JSON format

### 3. Compliance Reports

#### GDPR Reports
- **GDPR Access Report**: Show all access to personal data (Article 15)
- **GDPR Data Export**: Complete personal data export (Article 20)

#### SOX Reports
- **SOX Financial Access Report**: Financial data access audit (Section 404)
- **SOX Segregation of Duties Report**: Identify segregation violations

#### ISO 27001 Reports
- **ISO 27001 Security Report**: Security event report (Annex A.12.4)

#### General Reports
- **User Activity Report**: Chronological user action report
- **Data Modification Report**: Complete entity modification audit trail

### 4. Monitoring

#### Health & Performance
- **Get System Health**: CPU, memory, database, request rate metrics
- **Get Endpoint Statistics**: Performance stats for specific endpoints
- **Get Slow Requests**: Requests exceeding execution time threshold
- **Get Slow Queries**: Database queries exceeding threshold
- **Get Connection Pool Metrics**: Oracle connection pool status
- **Get Audit Queue Depth**: Audit logging queue utilization
- **Get Audit Metrics**: Comprehensive audit system metrics

#### Memory Management
- **Get Memory Metrics**: Heap sizes, GC statistics, allocation rate
- **Get Memory Pressure**: Current memory pressure level
- **Get Memory Optimization Recommendations**: Optimization suggestions
- **Optimize Memory**: Trigger memory optimization (GC + compaction)
- **Force Garbage Collection**: Force GC for specific generation

#### Security Monitoring
- **Get Active Security Threats**: All active security threats
- **Get Daily Security Summary**: Daily threat summary report
- **Check Failed Login Pattern**: Check IP for failed login threshold
- **Get Failed Login Count**: Failed login count for user
- **Check SQL Injection**: Scan input for SQL injection patterns
- **Check XSS**: Scan input for XSS patterns
- **Check Anomalous Activity**: Detect unusual user activity

### 5. Alerts

#### Alert Rules
- **Get Alert Rules**: List all configured alert rules
- **Create Alert Rule**: Create new alert rule with conditions
- **Update Alert Rule**: Modify existing alert rule
- **Delete Alert Rule**: Remove alert rule

#### Alert History
- **Get Alert History**: View historical alerts
- **Acknowledge Alert**: Mark alert as acknowledged
- **Resolve Alert**: Mark alert as resolved with notes

#### Notification Testing
- **Test Email Notification**: Send test email alert
- **Test Webhook Notification**: Send test webhook alert
- **Test SMS Notification**: Send test SMS alert

## Setup Instructions

### 1. Import the Collection

1. Open Postman
2. Click **Import** button
3. Select `Full-Traceability-System-API.postman_collection.json`
4. Click **Import**

### 2. Configure Environment Variables

The collection uses the following variables:

| Variable | Description | Default Value |
|----------|-------------|---------------|
| `baseUrl` | API base URL | `http://localhost:5000` |
| `token` | JWT access token | Auto-populated after login |
| `refreshToken` | JWT refresh token | Auto-populated after login |
| `correlationId` | Sample correlation ID for testing | Empty (set manually) |
| `auditLogId` | Sample audit log ID for testing | Auto-populated from queries |
| `alertRuleId` | Sample alert rule ID for testing | Auto-populated from queries |

**To configure:**
1. Click on the collection name
2. Go to the **Variables** tab
3. Update the `baseUrl` if your API is not running on `http://localhost:5000`
4. Save changes

### 3. Authenticate

Before using protected endpoints:

1. Open **Authentication > Login (Admin)**
2. Update the request body with valid admin credentials:
   ```json
   {
     "username": "admin",
     "password": "Admin@123"
   }
   ```
3. Click **Send**
4. The `token` and `refreshToken` variables will be automatically set

### 4. Start Testing

All requests are now ready to use! The collection automatically includes the JWT token in the Authorization header.

## Usage Tips

### Auto-Population of Variables

Several requests automatically populate collection variables:

- **Login**: Sets `token` and `refreshToken`
- **Get Logs by Correlation ID**: Sets `auditLogId` from first result
- **Get Alert Rules**: Sets `alertRuleId` from first result

### Query Parameters

Most endpoints support optional query parameters. In Postman:
- Enabled parameters are included in the request
- Disabled parameters (checkbox unchecked) are excluded
- Toggle parameters on/off to test different scenarios

### Export Formats

Compliance reports support multiple formats:
- **JSON**: Default format, returns structured data
- **CSV**: Comma-separated values for Excel/spreadsheet import
- **PDF**: Formatted PDF report (if implemented)

Change the `format` query parameter to switch formats.

### Pagination

List endpoints support pagination:
- `pageNumber`: Page number (1-based)
- `pageSize`: Items per page (max: 100)

Example: `?pageNumber=2&pageSize=50`

### Date Filters

Date parameters accept ISO 8601 format:
- Date only: `2024-01-15`
- Date and time: `2024-01-15T10:30:00Z`

## Common Workflows

### 1. Investigate an Error

1. **Get Legacy Audit Logs** with `status=Unresolved`
2. Note the `correlationId` from an error entry
3. **Get Logs by Correlation ID** to see all related logs
4. **Update Audit Log Status** to mark as "In Progress"
5. After fixing, **Update Audit Log Status** to "Resolved"

### 2. Generate Compliance Report

1. **Login** to get authentication token
2. Choose report type:
   - **GDPR Access Report** for data subject access
   - **SOX Financial Access Report** for financial audit
   - **ISO 27001 Security Report** for security audit
3. Set date range and format (json/csv/pdf)
4. Download and review the report

### 3. Monitor System Performance

1. **Get System Health** for overall status
2. **Get Slow Requests** to identify performance issues
3. **Get Slow Queries** to find database bottlenecks
4. **Get Endpoint Statistics** for specific endpoint analysis
5. **Get Memory Metrics** if memory issues suspected

### 4. Investigate Security Threat

1. **Get Active Security Threats** to see current threats
2. **Check Failed Login Pattern** for specific IP
3. **Get Failed Login Count** for specific user
4. **Get Daily Security Summary** for trend analysis
5. Review and take appropriate action

### 5. Configure Alert Rules

1. **Get Alert Rules** to see existing rules
2. **Create Alert Rule** with conditions and thresholds
3. **Test Email Notification** to verify email setup
4. **Test Webhook Notification** to verify webhook setup
5. **Get Alert History** to see triggered alerts
6. **Acknowledge Alert** or **Resolve Alert** as needed

## Authentication

All endpoints (except Login, Refresh Token, and Get System Health) require JWT authentication.

The collection is configured to automatically include the Bearer token in the Authorization header:

```
Authorization: Bearer {{token}}
```

If you receive `401 Unauthorized` errors:
1. Check that you've logged in successfully
2. Verify the `token` variable is set (View > Show Postman Console)
3. Try refreshing the token using **Refresh Token** endpoint
4. Re-login if the refresh token has also expired

## Response Format

All API responses follow the standard `ApiResponse` format:

```json
{
  "success": true,
  "message": "Operation completed successfully",
  "data": { ... },
  "statusCode": 200
}
```

Error responses:

```json
{
  "success": false,
  "message": "Error description",
  "data": null,
  "statusCode": 400
}
```

## Testing Scenarios

### Scenario 1: Audit Log Investigation
```
1. Get Legacy Audit Logs (filter by status=Unresolved)
2. Get Logs by Correlation ID (use correlationId from step 1)
3. Get Entity History (use entityType and entityId from logs)
4. Update Audit Log Status (mark as In Progress)
5. Update Audit Log Status (mark as Resolved with notes)
```

### Scenario 2: GDPR Data Subject Request
```
1. GDPR Access Report (dataSubjectId=1, last 12 months)
2. GDPR Data Export (dataSubjectId=1, format=json)
3. User Activity Report (userId=1, last 12 months)
4. Get Entity History (entityType=SysUser, entityId=1)
```

### Scenario 3: Performance Investigation
```
1. Get System Health (check overall status)
2. Get Slow Requests (thresholdMs=1000)
3. Get Slow Queries (thresholdMs=500)
4. Get Connection Pool Metrics (check pool utilization)
5. Get Memory Metrics (check memory usage)
6. Get Endpoint Statistics (analyze specific endpoint)
```

### Scenario 4: Security Incident Response
```
1. Get Active Security Threats (identify current threats)
2. Check Failed Login Pattern (ipAddress=suspicious_ip)
3. Get Failed Login Count (username=affected_user)
4. Check Anomalous Activity (userId=affected_user_id)
5. Get Daily Security Summary (review trends)
```

## Troubleshooting

### 401 Unauthorized
- **Cause**: Missing or expired JWT token
- **Solution**: Run **Login (Admin)** request to obtain a new token

### 403 Forbidden
- **Cause**: User does not have admin privileges
- **Solution**: Ensure you're logging in with an admin account

### 400 Bad Request
- **Cause**: Invalid request parameters or body
- **Solution**: Check the request documentation and ensure all required fields are provided with valid values

### 404 Not Found
- **Cause**: Resource does not exist (e.g., invalid audit log ID)
- **Solution**: Verify the ID exists by querying the list endpoint first

### 500 Internal Server Error
- **Cause**: Server-side error
- **Solution**: Check server logs for details. Contact system administrator if the issue persists.

## Support

For issues or questions:
1. Check the API documentation in the design document
2. Review the requirements document for expected behavior
3. Check server logs for detailed error messages
4. Contact the development team

## Version History

- **v1.0.0** (2024-01-15): Initial release
  - Complete audit log endpoints
  - GDPR, SOX, and ISO 27001 compliance reports
  - Performance and security monitoring
  - Alert management and notification testing

## License

This Postman collection is part of the ThinkOnErp Full Traceability System project.
