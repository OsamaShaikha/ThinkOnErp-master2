# Compliance API - Complete Explanation

## Overview

The **Compliance API** is a comprehensive regulatory reporting system built into ThinkOnERP that helps organizations meet various compliance requirements including **GDPR**, **SOX**, and **ISO 27001**. It generates detailed audit reports based on the system's audit logs.

---

## Purpose

The Compliance API serves several critical business needs:

1. **Regulatory Compliance**: Meet legal requirements for data protection and financial controls
2. **Audit Trail**: Provide detailed reports for internal and external audits
3. **Security Monitoring**: Track and analyze security events and access patterns
4. **Data Subject Rights**: Support GDPR rights like data access and portability
5. **Risk Management**: Identify potential security risks and policy violations

---

## Key Features

### 🔒 **Admin-Only Access**
- All compliance endpoints require admin privileges (`AdminOnly` policy)
- Ensures sensitive compliance data is only accessible to authorized personnel

### 📊 **Multiple Report Types**
- GDPR reports (data access, data export)
- SOX reports (financial access, segregation of duties)
- ISO 27001 reports (security events)
- General reports (user activity, data modifications)

### 📁 **Multiple Export Formats**
- **JSON**: For API integration and programmatic access
- **CSV**: For spreadsheet analysis and offline processing
- **PDF**: For formal documentation and presentations (planned)

---

## API Endpoints

### Base URL
```
/api/compliance
```

All endpoints require:
- **Authentication**: Valid JWT token
- **Authorization**: Admin role

---

## 1. GDPR Reports

### 1.1 GDPR Data Access Report

**Endpoint**: `GET /api/compliance/gdpr/access-report`

**Purpose**: Shows who accessed a specific person's data and when (GDPR Article 15 - Right of Access)

**Parameters**:
- `dataSubjectId` (required): User ID whose data access to track
- `startDate` (required): Start date of report period
- `endDate` (required): End date of report period
- `format` (optional): Export format (json, csv, pdf) - default: json

**Example Request**:
```bash
GET /api/compliance/gdpr/access-report?dataSubjectId=5&startDate=2026-01-01&endDate=2026-05-06&format=json
```

**What It Returns**:
- List of all times someone accessed the user's data
- Who accessed it (actor name and ID)
- What data was accessed (entity type and ID)
- When it was accessed (timestamp)
- Why it was accessed (purpose and legal basis)
- Where it was accessed from (IP address)
- Summary statistics by entity type and actor

**Use Cases**:
- Responding to GDPR data access requests
- Auditing who viewed customer information
- Investigating potential data breaches
- Compliance reporting for regulators

---

### 1.2 GDPR Data Export Report

**Endpoint**: `GET /api/compliance/gdpr/data-export`

**Purpose**: Exports all personal data stored for a specific person (GDPR Article 20 - Right to Data Portability)

**Parameters**:
- `dataSubjectId` (required): User ID whose data to export
- `format` (optional): Export format (json, csv, pdf) - default: json

**Example Request**:
```bash
GET /api/compliance/gdpr/data-export?dataSubjectId=5&format=csv
```

**What It Returns**:
- Complete export of all personal data
- User profile information
- Audit log history (all actions by the user)
- Authentication history (logins, tokens)
- Data organized by category/entity type
- Total record counts

**Use Cases**:
- Fulfilling GDPR data portability requests
- User account data export
- Data migration between systems
- Compliance documentation

---

## 2. SOX Reports (Sarbanes-Oxley)

### 2.1 SOX Financial Access Report

**Endpoint**: `GET /api/compliance/sox/financial-access`

**Purpose**: Tracks all access to financial data for SOX Section 404 compliance

**Parameters**:
- `startDate` (required): Start date of report period
- `endDate` (required): End date of report period
- `format` (optional): Export format (json, csv, pdf) - default: json

**Example Request**:
```bash
GET /api/compliance/sox/financial-access?startDate=2026-04-01&endDate=2026-04-30&format=json
```

**What It Returns**:
- All financial data access events
- Out-of-hours access (outside 8 AM - 6 PM weekdays)
- Access summary by user
- Access summary by entity type
- Suspicious access patterns detected
- Total access event count

**Use Cases**:
- SOX compliance audits
- Detecting unauthorized financial data access
- Monitoring after-hours activity
- Internal control testing

---

### 2.2 SOX Segregation of Duties Report

**Endpoint**: `GET /api/compliance/sox/segregation-of-duties`

**Purpose**: Identifies users with conflicting role assignments that violate segregation of duties principles

**Parameters**:
- `format` (optional): Export format (json, csv, pdf) - default: json

**Example Request**:
```bash
GET /api/compliance/sox/segregation-of-duties?format=json
```

**What It Returns**:
- List of segregation of duties violations
- Users with conflicting roles
- Violation severity levels
- Total users analyzed
- Violations grouped by severity

**Use Cases**:
- SOX compliance audits
- Role assignment reviews
- Internal control assessments
- Risk management

**Example Violations**:
- User has both "Accountant" and "Approver" roles
- User can both create and approve transactions
- User has access to both cash handling and reconciliation

---

## 3. ISO 27001 Reports

### 3.1 ISO 27001 Security Report

**Endpoint**: `GET /api/compliance/iso27001/security-report`

**Purpose**: Comprehensive security event report for ISO 27001 Annex A.12.4 compliance

**Parameters**:
- `startDate` (required): Start date of report period
- `endDate` (required): End date of report period
- `format` (optional): Export format (json, csv, pdf) - default: json

**Example Request**:
```bash
GET /api/compliance/iso27001/security-report?startDate=2026-05-01&endDate=2026-05-06&format=json
```

**What It Returns**:
- All security-related events
- Critical events count (severity: Critical or Error)
- Failed login attempts
- Unauthorized access attempts
- Events grouped by severity
- Events grouped by type
- Incidents requiring immediate attention

**Use Cases**:
- ISO 27001 certification audits
- Security incident reporting
- Threat detection and analysis
- Compliance documentation

**Event Types Tracked**:
- Failed login attempts
- Unauthorized access attempts
- Permission violations
- System errors
- Configuration changes
- Security exceptions

---

## 4. General Reports

### 4.1 User Activity Report

**Endpoint**: `GET /api/compliance/user-activity`

**Purpose**: Chronological report of all actions performed by a specific user

**Parameters**:
- `userId` (required): User ID to track
- `startDate` (required): Start date of report period
- `endDate` (required): End date of report period
- `format` (optional): Export format (json, csv, pdf) - default: json

**Example Request**:
```bash
GET /api/compliance/user-activity?userId=5&startDate=2026-05-01&endDate=2026-05-06&format=json
```

**What It Returns**:
- Chronological list of all user actions
- User information (name, email)
- Action details (type, entity, timestamp)
- IP addresses used
- Correlation IDs for request tracking
- Summary by action type
- Summary by entity type

**Use Cases**:
- User behavior analysis
- Security investigations
- Productivity monitoring
- Compliance audits
- Debugging user-reported issues

**Actions Tracked**:
- Data creation (INSERT)
- Data updates (UPDATE)
- Data deletion (DELETE)
- Login/logout events
- Permission changes
- Configuration changes

---

### 4.2 Data Modification Report

**Endpoint**: `GET /api/compliance/data-modification`

**Purpose**: Complete audit trail of all changes to a specific entity (data lineage)

**Parameters**:
- `entityType` (required): Type of entity (e.g., "SysUser", "SysCompany")
- `entityId` (required): Entity ID to track
- `format` (optional): Export format (json, csv, pdf) - default: json

**Example Request**:
```bash
GET /api/compliance/data-modification?entityType=SysCompany&entityId=1&format=json
```

**What It Returns**:
- Complete modification history
- Who made each change (actor name and ID)
- When changes were made (timestamp)
- What changed (old value vs new value)
- Changed field names (for UPDATE operations)
- IP addresses of changes
- Summary by action type
- Summary by user

**Use Cases**:
- Data lineage tracking
- Debugging data issues
- Compliance audits
- Change management
- Dispute resolution

**Example Output**:
```json
{
  "entityType": "SysCompany",
  "entityId": 1,
  "modifications": [
    {
      "modifiedAt": "2026-05-01T10:30:00Z",
      "action": "INSERT",
      "actorName": "admin",
      "newValue": "{\"name\":\"Acme Corp\",\"email\":\"info@acme.com\"}"
    },
    {
      "modifiedAt": "2026-05-03T14:15:00Z",
      "action": "UPDATE",
      "actorName": "john.doe",
      "oldValue": "{\"email\":\"info@acme.com\"}",
      "newValue": "{\"email\":\"contact@acme.com\"}",
      "changedFields": ["email"]
    }
  ]
}
```

---

## Export Formats

### JSON Format
- Default format
- Best for API integration
- Includes full data structure
- Wrapped in `ApiResponse` object

### CSV Format
- Spreadsheet-compatible
- UTF-8 with BOM (Excel-friendly)
- Includes report metadata header
- Tabular data with column headers
- Multiple sections for summaries

### PDF Format
- Planned feature (not yet implemented)
- Professional document format
- Suitable for formal reports
- Includes charts and formatting

---

## Security Features

### 1. **Authorization**
- All endpoints require `AdminOnly` policy
- Only administrators can generate compliance reports
- Prevents unauthorized access to sensitive audit data

### 2. **Audit Logging**
- All compliance report generation is logged
- Tracks who generated which reports
- Includes timestamps and correlation IDs

### 3. **Data Protection**
- Sensitive data is properly handled
- IP addresses and user information tracked
- Supports GDPR compliance requirements

### 4. **Query Timeout Protection**
- 60-second timeout for complex queries
- Prevents database overload
- Ensures system stability

---

## Common Use Cases

### 1. **GDPR Compliance**
**Scenario**: Customer requests all their data

**Solution**:
```bash
# Step 1: Generate data export
GET /api/compliance/gdpr/data-export?dataSubjectId=123&format=csv

# Step 2: Provide CSV file to customer
```

---

### 2. **Security Incident Investigation**
**Scenario**: Suspicious activity detected for a user

**Solution**:
```bash
# Step 1: Get user activity report
GET /api/compliance/user-activity?userId=456&startDate=2026-05-01&endDate=2026-05-06&format=json

# Step 2: Get security events
GET /api/compliance/iso27001/security-report?startDate=2026-05-01&endDate=2026-05-06&format=json

# Step 3: Analyze patterns and take action
```

---

### 3. **SOX Audit**
**Scenario**: Annual SOX compliance audit

**Solution**:
```bash
# Step 1: Financial access report
GET /api/compliance/sox/financial-access?startDate=2025-01-01&endDate=2025-12-31&format=pdf

# Step 2: Segregation of duties report
GET /api/compliance/sox/segregation-of-duties?format=pdf

# Step 3: Provide reports to auditors
```

---

### 4. **Data Change Investigation**
**Scenario**: Customer claims their data was changed incorrectly

**Solution**:
```bash
# Get complete modification history
GET /api/compliance/data-modification?entityType=SysUser&entityId=789&format=json

# Review who changed what and when
```

---

## Technical Implementation

### Architecture
- **Controller**: `ComplianceController.cs` - REST API endpoints
- **Service**: `ComplianceReporter.cs` - Business logic and report generation
- **Data Source**: Audit logs from `SYS_AUDIT_LOG` table
- **Dependencies**: 
  - `IAuditQueryService` - Query audit logs
  - `IUserRepository` - Get user information
  - `OracleDbContext` - Database access

### Performance Considerations
- Complex queries with 60-second timeout
- Pagination not currently implemented (loads all results)
- CSV export uses streaming for large datasets
- Database indexes on audit log tables recommended

---

## Configuration

No special configuration required. The Compliance API uses:
- Existing audit logging infrastructure
- Standard authentication/authorization
- Database connection from `appsettings.json`

---

## Limitations

1. **PDF Export**: Not yet implemented (returns empty array)
2. **Pagination**: Reports load all results (may be slow for large datasets)
3. **Real-time**: Reports are generated on-demand (not cached)
4. **Retention**: Limited by audit log retention policy

---

## Best Practices

### 1. **Regular Reporting**
- Generate compliance reports monthly
- Archive reports for audit trail
- Review suspicious patterns promptly

### 2. **Access Control**
- Limit admin access to compliance reports
- Log all report generation
- Review access logs regularly

### 3. **Data Retention**
- Keep audit logs for required retention period
- Archive old reports securely
- Document retention policies

### 4. **Performance**
- Use date ranges to limit report size
- Export to CSV for large datasets
- Schedule reports during off-peak hours

---

## Summary

The Compliance API is a powerful tool for:
- ✅ Meeting regulatory requirements (GDPR, SOX, ISO 27001)
- ✅ Generating audit reports
- ✅ Investigating security incidents
- ✅ Tracking data changes
- ✅ Monitoring user activity
- ✅ Supporting compliance audits

It leverages the comprehensive audit logging system to provide detailed, exportable reports in multiple formats, helping organizations maintain compliance and security.

---

**Status**: Fully implemented and operational  
**Access**: Admin-only  
**Base URL**: `/api/compliance`  
**Formats**: JSON, CSV (PDF planned)
