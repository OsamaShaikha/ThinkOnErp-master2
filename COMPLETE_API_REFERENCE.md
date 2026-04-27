# ThinkOnERP - Complete API Reference

## Overview

ThinkOnERP provides a comprehensive REST API with **21 controllers** covering authentication, business operations, monitoring, compliance, and system management.

**Base URL**: `https://localhost:7136` or `http://localhost:5160`

**Swagger UI**: `https://localhost:7136/swagger`

---

## API Controllers Summary

| # | Controller | Base Route | Access | Purpose |
|---|------------|------------|--------|---------|
| 1 | **AuthController** | `/api/auth` | Public | Authentication & authorization |
| 2 | **UsersController** | `/api/users` | Authenticated | User management |
| 3 | **CompanyController** | `/api/companies` | Authenticated | Company management |
| 4 | **BranchController** | `/api/branches` | Authenticated | Branch management |
| 5 | **CurrencyController** | `/api/currencies` | Authenticated | Currency management |
| 6 | **RolesController** | `/api/roles` | Authenticated | Role management |
| 7 | **PermissionsController** | `/api/permissions` | Authenticated | Permission management |
| 8 | **FiscalYearController** | `/api/fiscalyears` | Authenticated | Fiscal year management |
| 9 | **SuperAdminController** | `/api/superadmins` | Admin Only | Super admin operations |
| 10 | **TicketsController** | `/api/tickets` | Authenticated | Support ticket management |
| 11 | **TicketTypesController** | `/api/ticket-types` | Authenticated | Ticket type management |
| 12 | **SavedSearchesController** | `/api/saved-searches` | Authenticated | Saved search management |
| 13 | **AuditLogsController** | `/api/auditlogs` | Admin Only | Legacy audit log viewing |
| 14 | **AuditTrailController** | `/api/audit-trail` | Admin Only | Advanced audit trail queries |
| 15 | **ComplianceController** | `/api/compliance` | Admin Only | Compliance reports (GDPR, SOX, ISO) |
| 16 | **MonitoringController** | `/api/monitoring` | Admin Only | System monitoring & metrics |
| 17 | **AlertsController** | `/api/alerts` | Admin Only | Alert management |
| 18 | **ConfigurationController** | `/api/configuration` | Admin Only | System configuration |
| 19 | **KeyManagementController** | `/api/keymanagement` | Admin Only | Encryption key management |
| 20 | **HealthController** | `/api/health` | Public | Health checks |
| 21 | **AuditHealthController** | `/api/audithealth` | Public | Audit system health |

---

## 1. Authentication API (`/api/auth`)

### Purpose
User authentication, token management, and session control.

### Endpoints

#### POST `/api/auth/login`
**Purpose**: Authenticate user and get JWT token

**Request**:
```json
{
  "username": "moe",
  "password": "Admin@123"
}
```

**Response**:
```json
{
  "success": true,
  "data": {
    "token": "eyJhbGciOiJIUzI1NiIs...",
    "refreshToken": "refresh_token_here",
    "expiresAt": "2026-05-07T00:00:00Z",
    "userId": 5,
    "userName": "moe",
    "userType": "SuperAdmin"
  }
}
```

#### POST `/api/auth/refresh-token`
**Purpose**: Refresh expired JWT token

#### POST `/api/auth/logout`
**Purpose**: Invalidate current session

#### POST `/api/auth/change-password`
**Purpose**: Change user password

---

## 2. Users API (`/api/users`)

### Purpose
User account management (CRUD operations).

### Endpoints

- `GET /api/users` - Get all users
- `GET /api/users/{id}` - Get user by ID
- `POST /api/users` - Create new user
- `PUT /api/users/{id}` - Update user
- `DELETE /api/users/{id}` - Delete user (soft delete)
- `POST /api/users/{id}/force-logout` - Force user logout

**Example - Create User**:
```json
POST /api/users
{
  "userName": "john.doe",
  "password": "SecurePass@123",
  "email": "john@example.com",
  "userType": "User",
  "companyId": 1,
  "branchId": 1,
  "roleId": 2
}
```

---

## 3. Company API (`/api/companies`)

### Purpose
Company management with logo support.

### Endpoints

- `GET /api/companies` - Get all companies
- `GET /api/companies/{id}` - Get company by ID
- `POST /api/companies` - Create company with default branch
- `PUT /api/companies/{id}` - Update company
- `DELETE /api/companies/{id}` - Delete company
- `POST /api/companies/{id}/logo` - Upload company logo (Base64)
- `GET /api/companies/{id}/logo` - Get company logo

**Example - Create Company**:
```json
POST /api/companies
{
  "rowDesc": "شركة الأمثلة",
  "rowDescE": "Example Company",
  "countryId": 1,
  "currencyId": 1,
  "fiscalYearId": 1,
  "companyLogo": "data:image/png;base64,iVBORw0KG...",
  "defaultBranch": {
    "rowDesc": "الفرع الرئيسي",
    "rowDescE": "Main Branch",
    "phone": "+966123456789",
    "email": "info@example.com"
  }
}
```

---

## 4. Branch API (`/api/branches`)

### Purpose
Branch management with logo support.

### Endpoints

- `GET /api/branches` - Get all branches
- `GET /api/branches/{id}` - Get branch by ID
- `POST /api/branches` - Create branch
- `PUT /api/branches/{id}` - Update branch
- `DELETE /api/branches/{id}` - Delete branch
- `POST /api/branches/{id}/logo` - Upload branch logo
- `GET /api/branches/{id}/logo` - Get branch logo

---

## 5. Currency API (`/api/currencies`)

### Purpose
Currency management for multi-currency support.

### Endpoints

- `GET /api/currencies` - Get all currencies
- `GET /api/currencies/{id}` - Get currency by ID
- `POST /api/currencies` - Create currency
- `PUT /api/currencies/{id}` - Update currency
- `DELETE /api/currencies/{id}` - Delete currency

---

## 6. Roles API (`/api/roles`)

### Purpose
Role-based access control management.

### Endpoints

- `GET /api/roles` - Get all roles
- `GET /api/roles/{id}` - Get role by ID
- `POST /api/roles` - Create role
- `PUT /api/roles/{id}` - Update role
- `DELETE /api/roles/{id}` - Delete role

---

## 7. Permissions API (`/api/permissions`)

### Purpose
Fine-grained permission management.

### Endpoints

- `GET /api/permissions` - Get all permissions
- `GET /api/permissions/user/{userId}` - Get user permissions
- `POST /api/permissions/assign` - Assign permission to role
- `DELETE /api/permissions/revoke` - Revoke permission from role

---

## 8. Fiscal Year API (`/api/fiscalyears`)

### Purpose
Fiscal year management for financial periods.

### Endpoints

- `GET /api/fiscalyears` - Get all fiscal years
- `GET /api/fiscalyears/{id}` - Get fiscal year by ID
- `POST /api/fiscalyears` - Create fiscal year
- `PUT /api/fiscalyears/{id}` - Update fiscal year
- `DELETE /api/fiscalyears/{id}` - Delete fiscal year

---

## 9. Super Admin API (`/api/superadmins`)

### Purpose
Super administrator account management.

### Endpoints

- `GET /api/superadmins` - Get all super admins
- `GET /api/superadmins/{id}` - Get super admin by ID
- `POST /api/superadmins` - Create super admin
- `PUT /api/superadmins/{id}` - Update super admin
- `DELETE /api/superadmins/{id}` - Delete super admin
- `POST /api/superadmins/{id}/change-password` - Change super admin password

---

## 10. Tickets API (`/api/tickets`)

### Purpose
Support ticket and request management system.

### Endpoints

- `GET /api/tickets` - Get all tickets (with filters)
- `GET /api/tickets/{id}` - Get ticket by ID
- `POST /api/tickets` - Create ticket
- `PUT /api/tickets/{id}` - Update ticket
- `DELETE /api/tickets/{id}` - Delete ticket
- `POST /api/tickets/{id}/assign` - Assign ticket to user
- `POST /api/tickets/{id}/status` - Update ticket status
- `POST /api/tickets/{id}/priority` - Update ticket priority
- `GET /api/tickets/sla/approaching` - Get tickets approaching SLA deadline
- `GET /api/tickets/sla/overdue` - Get overdue tickets

**Ticket Statuses**: Open, In Progress, Resolved, Closed, Cancelled

**Priorities**: Low, Medium, High, Critical

---

## 11. Ticket Types API (`/api/ticket-types`)

### Purpose
Manage ticket categories and types.

### Endpoints

- `GET /api/ticket-types` - Get all ticket types
- `GET /api/ticket-types/{id}` - Get ticket type by ID
- `POST /api/ticket-types` - Create ticket type
- `PUT /api/ticket-types/{id}` - Update ticket type
- `DELETE /api/ticket-types/{id}` - Delete ticket type

---

## 12. Saved Searches API (`/api/saved-searches`)

### Purpose
Save and manage custom search queries.

### Endpoints

- `GET /api/saved-searches` - Get user's saved searches
- `GET /api/saved-searches/{id}` - Get saved search by ID
- `POST /api/saved-searches` - Create saved search
- `PUT /api/saved-searches/{id}` - Update saved search
- `DELETE /api/saved-searches/{id}` - Delete saved search
- `POST /api/saved-searches/{id}/execute` - Execute saved search

---

## 13. Audit Logs API (`/api/auditlogs`)

### Purpose
Legacy audit log viewing (logs.png interface compatibility).

### Endpoints

#### GET `/api/auditlogs/legacy`
**Purpose**: Get audit logs in legacy format

**Parameters**:
- `companyId` - Filter by company
- `businessModule` - Filter by module
- `branchId` - Filter by branch
- `status` - Filter by status (Resolved/Unresolved/Critical)
- `startDate` - Start date
- `endDate` - End date
- `searchTerm` - Search text
- `pageNumber` - Page number
- `pageSize` - Items per page

**Response**:
```json
{
  "success": true,
  "data": {
    "items": [
      {
        "id": 35,
        "errorDescription": "UNHANDLED_EXCEPTION performed on Unknown by System",
        "module": "System",
        "company": "Unknown",
        "branch": "Unknown",
        "user": "System",
        "device": "Desktop Chrome 147",
        "dateTime": "2026-05-05T21:05:35",
        "status": "Unresolved",
        "errorCode": "UNKNOWN_SYS_677",
        "correlationId": "3eef2a78-21a9-4212-b31c-87da9100a0ad"
      }
    ],
    "totalCount": 21,
    "page": 1,
    "pageSize": 50
  }
}
```

---

## 14. Audit Trail API (`/api/audit-trail`)

### Purpose
Advanced audit trail queries and analysis.

### Endpoints

- `GET /api/audit-trail` - Query audit trail with filters
- `GET /api/audit-trail/{id}` - Get specific audit entry
- `GET /api/audit-trail/user/{userId}` - Get user's audit trail
- `GET /api/audit-trail/entity/{entityType}/{entityId}` - Get entity audit trail
- `GET /api/audit-trail/search` - Advanced search
- `POST /api/audit-trail/export` - Export audit trail

---

## 15. Compliance API (`/api/compliance`)

### Purpose
Generate compliance reports for GDPR, SOX, and ISO 27001.

### Endpoints

#### GDPR Reports
- `GET /api/compliance/gdpr/access-report` - GDPR data access report
- `GET /api/compliance/gdpr/data-export` - GDPR data export

#### SOX Reports
- `GET /api/compliance/sox/financial-access` - SOX financial access report
- `GET /api/compliance/sox/segregation-of-duties` - SOX segregation report

#### ISO 27001 Reports
- `GET /api/compliance/iso27001/security-report` - ISO 27001 security report

#### General Reports
- `GET /api/compliance/user-activity` - User activity report
- `GET /api/compliance/data-modification` - Data modification report

**Export Formats**: JSON, CSV, PDF

**Example**:
```bash
GET /api/compliance/gdpr/data-export?dataSubjectId=5&format=csv
```

---

## 16. Monitoring API (`/api/monitoring`)

### Purpose
System monitoring, performance metrics, and diagnostics.

### Endpoints

#### System Health
- `GET /api/monitoring/health` - System health metrics (Public)

#### Memory Management
- `GET /api/monitoring/memory` - Memory metrics
- `GET /api/monitoring/memory/pressure` - Memory pressure detection
- `GET /api/monitoring/memory/recommendations` - Optimization recommendations
- `POST /api/monitoring/memory/optimize` - Trigger memory optimization
- `POST /api/monitoring/memory/gc` - Force garbage collection

#### Performance
- `GET /api/monitoring/performance/endpoint` - Endpoint statistics
- `GET /api/monitoring/performance/slow-requests` - Slow requests
- `GET /api/monitoring/performance/slow-queries` - Slow database queries
- `GET /api/monitoring/performance/connection-pool` - Connection pool metrics

#### Security
- `GET /api/monitoring/security/threats` - Active security threats
- `GET /api/monitoring/security/daily-summary` - Daily security summary
- `GET /api/monitoring/security/check-failed-logins` - Check failed login patterns
- `GET /api/monitoring/security/failed-login-count` - Failed login count
- `POST /api/monitoring/security/check-sql-injection` - Check SQL injection
- `POST /api/monitoring/security/check-xss` - Check XSS patterns
- `GET /api/monitoring/security/check-anomalous-activity` - Check anomalous activity

#### Audit System
- `GET /api/monitoring/audit-queue-depth` - Audit queue depth
- `GET /api/monitoring/audit/metrics` - Audit system metrics
- `GET /api/monitoring/audit/fallback-status` - Fallback status
- `POST /api/monitoring/audit/replay-fallback` - Replay fallback events

#### Alerting
- `POST /api/monitoring/test-alert` - Test alert delivery

---

## 17. Alerts API (`/api/alerts`)

### Purpose
Alert rule management and alert history.

### Endpoints

- `GET /api/alerts/rules` - Get all alert rules
- `GET /api/alerts/rules/{id}` - Get alert rule by ID
- `POST /api/alerts/rules` - Create alert rule
- `PUT /api/alerts/rules/{id}` - Update alert rule
- `DELETE /api/alerts/rules/{id}` - Delete alert rule
- `GET /api/alerts/history` - Get alert history
- `POST /api/alerts/test` - Test alert rule

---

## 18. Configuration API (`/api/configuration`)

### Purpose
System configuration management.

### Endpoints

- `GET /api/configuration` - Get all configuration settings
- `GET /api/configuration/{key}` - Get configuration by key
- `PUT /api/configuration/{key}` - Update configuration
- `POST /api/configuration/reload` - Reload configuration

---

## 19. Key Management API (`/api/keymanagement`)

### Purpose
Encryption key management for audit log integrity.

### Endpoints

- `GET /api/keymanagement/current` - Get current key info
- `POST /api/keymanagement/rotate` - Rotate encryption key
- `GET /api/keymanagement/history` - Get key rotation history

---

## 20. Health API (`/api/health`)

### Purpose
Application health checks for load balancers.

### Endpoints

- `GET /api/health` - Basic health check
- `GET /api/health/ready` - Readiness check
- `GET /api/health/live` - Liveness check
- `GET /api/health/circuit-breakers` - Circuit breaker status

---

## 21. Audit Health API (`/api/audithealth`)

### Purpose
Audit logging system health checks.

### Endpoints

- `GET /api/audithealth` - Audit system health
- `GET /api/audithealth/queue` - Queue status
- `GET /api/audithealth/circuit-breaker` - Circuit breaker status

---

## Authentication

### JWT Token Authentication

All authenticated endpoints require a JWT token in the Authorization header:

```
Authorization: Bearer eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9...
```

### Getting a Token

```bash
POST /api/auth/login
Content-Type: application/json

{
  "username": "moe",
  "password": "Admin@123"
}
```

### Token Expiration

- Access tokens expire after a configured period (default: 1 hour)
- Use refresh token to get a new access token
- Refresh tokens expire after a longer period (default: 7 days)

---

## Authorization Levels

### Public Endpoints
- `/api/auth/login`
- `/api/health/*`
- `/api/audithealth/*`
- `/api/monitoring/health`

### Authenticated Endpoints
- All `/api/users/*`
- All `/api/companies/*`
- All `/api/branches/*`
- All `/api/tickets/*`
- etc.

### Admin-Only Endpoints
- All `/api/superadmins/*`
- All `/api/compliance/*`
- All `/api/monitoring/*` (except `/health`)
- All `/api/auditlogs/*`
- All `/api/audit-trail/*`
- All `/api/alerts/*`
- All `/api/configuration/*`
- All `/api/keymanagement/*`

---

## Response Format

### Success Response
```json
{
  "success": true,
  "statusCode": 200,
  "message": "Operation completed successfully",
  "data": { ... },
  "errors": null,
  "timestamp": "2026-05-06T00:00:00Z",
  "traceId": "abc123..."
}
```

### Error Response
```json
{
  "success": false,
  "statusCode": 400,
  "message": "Validation failed",
  "data": null,
  "errors": [
    "Username is required",
    "Password must be at least 8 characters"
  ],
  "timestamp": "2026-05-06T00:00:00Z",
  "traceId": "abc123..."
}
```

---

## Pagination

Endpoints that return lists support pagination:

**Parameters**:
- `pageNumber` - Page number (1-based, default: 1)
- `pageSize` - Items per page (default: 50, max: 100)

**Response**:
```json
{
  "items": [...],
  "totalCount": 150,
  "page": 1,
  "pageSize": 50,
  "totalPages": 3,
  "hasNextPage": true,
  "hasPreviousPage": false
}
```

---

## Filtering and Searching

Many endpoints support filtering:

**Example**:
```bash
GET /api/users?companyId=1&branchId=2&isActive=true&searchTerm=john
```

**Common Filter Parameters**:
- `companyId` - Filter by company
- `branchId` - Filter by branch
- `isActive` - Filter by active status
- `searchTerm` - Search in multiple fields
- `startDate` / `endDate` - Date range filter

---

## Rate Limiting

- **Default**: 100 requests per minute per IP
- **Admin endpoints**: 50 requests per minute
- **Authentication**: 10 requests per minute

**Rate Limit Headers**:
```
X-RateLimit-Limit: 100
X-RateLimit-Remaining: 95
X-RateLimit-Reset: 1620000000
```

---

## Error Codes

| Code | Description |
|------|-------------|
| 200 | Success |
| 201 | Created |
| 400 | Bad Request |
| 401 | Unauthorized |
| 403 | Forbidden |
| 404 | Not Found |
| 409 | Conflict |
| 422 | Validation Error |
| 429 | Too Many Requests |
| 500 | Internal Server Error |
| 503 | Service Unavailable |

---

## Testing Credentials

### Super Admin
- Username: `superadmin`
- Password: `Admin@123`

### Company Admin
- Username: `moe`
- Password: `Admin@123`

### Regular User
- Username: `user1`
- Password: `User@123`

---

## Swagger Documentation

Interactive API documentation is available at:

**URL**: `https://localhost:7136/swagger`

Features:
- Try out API endpoints
- View request/response schemas
- See authentication requirements
- Test with your own data

---

## Common Workflows

### 1. User Registration and Login
```bash
# 1. Login as admin
POST /api/auth/login
{"username": "moe", "password": "Admin@123"}

# 2. Create new user
POST /api/users
{
  "userName": "john.doe",
  "password": "SecurePass@123",
  "email": "john@example.com",
  "companyId": 1,
  "branchId": 1,
  "roleId": 2
}

# 3. New user logs in
POST /api/auth/login
{"username": "john.doe", "password": "SecurePass@123"}
```

### 2. Create Company with Branch
```bash
# 1. Login as super admin
POST /api/auth/login
{"username": "superadmin", "password": "Admin@123"}

# 2. Create company with default branch
POST /api/companies
{
  "rowDesc": "شركة جديدة",
  "rowDescE": "New Company",
  "countryId": 1,
  "currencyId": 1,
  "fiscalYearId": 1,
  "defaultBranch": {
    "rowDesc": "الفرع الرئيسي",
    "rowDescE": "Main Branch"
  }
}
```

### 3. Create and Track Support Ticket
```bash
# 1. Create ticket
POST /api/tickets
{
  "title": "System Issue",
  "description": "Cannot access reports",
  "priority": "High",
  "ticketTypeId": 1
}

# 2. Assign ticket
POST /api/tickets/{id}/assign
{"assignedToUserId": 5}

# 3. Update status
POST /api/tickets/{id}/status
{"status": "In Progress"}

# 4. Resolve ticket
POST /api/tickets/{id}/status
{"status": "Resolved", "resolutionNotes": "Fixed"}
```

### 4. Generate Compliance Report
```bash
# 1. Login as admin
POST /api/auth/login
{"username": "moe", "password": "Admin@123"}

# 2. Generate GDPR report
GET /api/compliance/gdpr/data-export?dataSubjectId=5&format=csv

# 3. Generate security report
GET /api/compliance/iso27001/security-report?startDate=2026-05-01&endDate=2026-05-06&format=pdf
```

---

## Best Practices

### 1. **Always Use HTTPS**
- Never send credentials over HTTP
- Use SSL/TLS certificates in production

### 2. **Handle Tokens Securely**
- Store tokens securely (not in localStorage)
- Refresh tokens before expiration
- Clear tokens on logout

### 3. **Implement Retry Logic**
- Retry failed requests with exponential backoff
- Handle rate limiting (429 errors)
- Check circuit breaker status

### 4. **Validate Input**
- Validate data before sending
- Handle validation errors gracefully
- Show user-friendly error messages

### 5. **Monitor API Usage**
- Track response times
- Monitor error rates
- Set up alerts for failures

---

## Support

For API support and questions:
- **Swagger UI**: `https://localhost:7136/swagger`
- **Documentation**: See individual controller documentation files
- **Health Check**: `GET /api/health`

---

**Version**: 1.0  
**Last Updated**: 2026-05-06  
**Total Endpoints**: 100+  
**Total Controllers**: 21
