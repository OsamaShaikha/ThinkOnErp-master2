# Audit Trail APIs - Complete Implementation Guide

## Overview

The Audit Trail APIs provide comprehensive logging, tracking, and reporting capabilities for all ticket-related activities in the ThinkOnErp system. These APIs ensure compliance with security requirements and provide detailed audit trails for regulatory purposes.

## 🔐 Security & Authorization

**All audit trail endpoints require AdminOnly authorization** - only users with administrative privileges can access audit data.

## 📋 API Endpoints

### 1. Get Ticket Audit Trail
**Endpoint:** `GET /api/audit-trail/tickets/{ticketId}`

**Purpose:** Retrieve complete audit history for a specific ticket

**Query Parameters:**
- `FromDate` (optional): Start date filter
- `ToDate` (optional): End date filter  
- `ActionFilter` (optional): Filter by action type (INSERT, UPDATE, DELETE, etc.)
- `UserIdFilter` (optional): Filter by specific user ID

**Response:** List of audit events with full details including:
- Timestamp of each action
- User who performed the action
- Action type (CREATE, UPDATE, DELETE, STATUS_CHANGE, etc.)
- Before/after values for changes
- IP address and user agent information

### 2. Advanced Audit Trail Search
**Endpoint:** `POST /api/audit-trail/search`

**Purpose:** Search audit trail with comprehensive filtering and pagination

**Request Body (AuditTrailSearchDto):**
```json
{
  "EntityType": "Ticket",           // Filter by entity type
  "EntityId": 12345,               // Filter by specific entity ID
  "UserId": 67890,                 // Filter by user ID
  "CompanyId": 1,                  // Filter by company
  "BranchId": 2,                   // Filter by branch
  "Action": "UPDATE",              // Filter by action type
  "FromDate": "2024-01-01",        // Date range start
  "ToDate": "2024-12-31",          // Date range end
  "Severity": "Warning",           // Filter by severity (Info, Warning, Error)
  "EventCategory": "DataChange",   // Filter by category
  "Page": 1,                       // Page number
  "PageSize": 50                   // Records per page (max 100)
}
```

**Response:** Paginated results with:
- Audit events array
- Total count
- Pagination metadata (current page, total pages, has next/previous)

### 3. Export Audit Trail
**Endpoint:** `POST /api/audit-trail/export`

**Purpose:** Export audit data for compliance reporting

**Request Body (AuditTrailExportDto):**
```json
{
  "EntityType": "Ticket",          // Optional entity filter
  "FromDate": "2024-01-01",        // Required start date
  "ToDate": "2024-12-31",          // Required end date
  "CompanyId": 1,                  // Optional company filter
  "Format": "CSV"                  // Export format: CSV or JSON
}
```

**Response:** File download with audit data in specified format

**Validation Rules:**
- Date range cannot exceed 365 days
- FromDate must be earlier than ToDate
- Format must be either CSV or JSON

### 4. Audit Trail Statistics
**Endpoint:** `GET /api/audit-trail/statistics`

**Purpose:** Get statistical overview of audit trail data

**Query Parameters:**
- `fromDate` (optional): Statistics start date
- `toDate` (optional): Statistics end date

**Response:** Comprehensive statistics including:
- Total events count
- Events breakdown by entity type (Ticket, Comment, Attachment, etc.)
- Events breakdown by action (INSERT, UPDATE, DELETE, etc.)
- Events breakdown by severity (Info, Warning, Error)

## 🎯 What Gets Audited

The audit trail system automatically logs:

### Ticket Operations
- **Creation**: Full ticket data, creator information
- **Modification**: Before/after values, changed fields
- **Deletion**: Complete ticket data before deletion
- **Status Changes**: Previous and new status with timestamps
- **Assignment Changes**: Previous and new assignee information

### Comments & Attachments
- **Comment Addition**: Comment preview, internal/external flag
- **File Upload**: File name, size, MIME type, uploader
- **File Download**: File access tracking for security

### Access & Search
- **Ticket Access**: Who viewed which tickets when
- **Search Activities**: Search terms, filters, result counts
- **Authorization Failures**: Failed access attempts with reasons

### Administrative Actions
- **Configuration Changes**: System settings modifications
- **User Management**: User creation, role changes
- **Permission Changes**: Access control modifications

## 📊 Audit Data Structure

Each audit event contains:

```json
{
  "CorrelationId": "req-12345",           // Request tracking ID
  "ActorType": "USER",                    // Who performed action
  "ActorId": 67890,                       // User ID
  "CompanyId": 1,                         // Company context
  "BranchId": 2,                          // Branch context
  "Action": "UPDATE",                     // What was done
  "EntityType": "Ticket",                 // What was affected
  "EntityId": 12345,                      // Specific entity ID
  "IpAddress": "192.168.1.100",          // Client IP
  "UserAgent": "Mozilla/5.0...",         // Client browser
  "Severity": "Info",                     // Event severity
  "EventCategory": "DataChange",          // Event category
  "Metadata": "{...}",                    // Detailed JSON data
  "Timestamp": "2024-04-27T10:30:00Z"    // When it happened
}
```

## 🔍 Search & Filter Capabilities

### Entity Types
- `Ticket` - Ticket operations
- `TicketComment` - Comment activities
- `TicketAttachment` - File operations
- `TicketType` - Ticket type changes
- `Configuration` - System configuration

### Actions
- `INSERT` - Creation operations
- `UPDATE` - Modification operations
- `DELETE` - Deletion operations
- `VIEW` - Access/viewing operations
- `SEARCH` - Search operations
- `STATUS_CHANGE` - Status modifications
- `ASSIGNMENT_CHANGE` - Assignment modifications
- `COMMENT_ADDED` - Comment additions
- `ATTACHMENT_UPLOADED` - File uploads
- `ATTACHMENT_DOWNLOADED` - File downloads
- `AUTHORIZATION_FAILURE` - Access denials

### Severity Levels
- `Info` - Normal operations
- `Warning` - Important events (deletions, failures)
- `Error` - System errors

### Event Categories
- `DataChange` - Data modifications
- `Request` - Access and search requests
- `Permission` - Authorization events
- `Configuration` - System configuration changes

## 📈 Export Formats

### CSV Export
- Headers with column names
- Comma-separated values
- Proper escaping for special characters
- UTF-8 encoding

### JSON Export
- Structured JSON array
- Pretty-printed format
- Complete data preservation
- UTF-8 encoding

## 🛡️ Compliance Features

### Regulatory Compliance
- **Immutable Audit Log**: Records cannot be modified after creation
- **Complete Traceability**: Every action is tracked with full context
- **Data Integrity**: Proper database constraints and validation
- **Retention Policies**: Configurable data retention periods

### Security Monitoring
- **Failed Access Tracking**: All authorization failures logged
- **IP Address Logging**: Client identification for security
- **User Agent Tracking**: Browser/application identification
- **Correlation IDs**: Request tracing across system components

### Performance Considerations
- **Pagination**: Large result sets handled efficiently
- **Indexed Searches**: Optimized database queries
- **Export Limits**: Maximum 365-day range for exports
- **Page Size Limits**: Maximum 100 records per page

## 🔧 Integration Points

### Automatic Logging
The audit trail service is automatically called by:
- Ticket controllers for all CRUD operations
- Comment controllers for comment management
- Attachment controllers for file operations
- Authentication middleware for access tracking

### Manual Logging
Developers can manually log events using:
```csharp
await _auditTrailService.LogAdministrativeActionAsync(
    action: "CONFIG_CHANGE",
    entityType: "SystemSettings",
    entityId: settingId,
    changeDetails: JsonSerializer.Serialize(changes),
    userId: currentUserId,
    userName: currentUserName,
    companyId: userCompanyId,
    branchId: userBranchId,
    correlationId: requestId
);
```

## 📋 Use Cases

### Compliance Reporting
- Generate audit reports for regulatory requirements
- Export data for external compliance systems
- Track data access for privacy regulations (GDPR, etc.)

### Security Investigation
- Investigate suspicious activities
- Track unauthorized access attempts
- Monitor data access patterns

### Operational Monitoring
- Monitor system usage patterns
- Track user activities for training
- Identify process improvement opportunities

### Troubleshooting
- Trace ticket lifecycle for issue resolution
- Identify when and who made specific changes
- Correlate user actions with system behavior

The Audit Trail APIs provide a comprehensive foundation for compliance, security monitoring, and operational transparency in the ThinkOnErp ticket management system.