# User Guide: Compliance Report Generation

## Overview

ThinkOnErp provides automated compliance reporting for GDPR, SOX, and ISO 27001. All reports are generated via the `/api/compliance` endpoints and can be exported as JSON, CSV, or PDF.

---

## Authentication

All compliance endpoints require **admin** privileges:

```
Authorization: Bearer {admin-jwt-token}
```

---

## GDPR Reports

### Access Report (Article 15 — Right of Access)

Shows all data accessed by or about a specific user:

```http
GET /api/compliance/gdpr/access-report?userId=42&startDate=2024-01-01&endDate=2024-12-31&format=pdf
```

**Response includes:**
- Total data access events
- Entities accessed (type, count)
- Timeline of access events
- Source IP addresses
- Accessing user details

### Data Export Report (Article 20 — Data Portability)

Exports all personal data for a subject access request:

```http
GET /api/compliance/gdpr/data-export?userId=42&format=json
```

---

## SOX Reports

### Financial Access Report

Tracks all access to financial data and modules:

```http
GET /api/compliance/sox/financial-access?startDate=2024-01-01&endDate=2024-03-31&format=pdf
```

**Response includes:**
- Users who accessed financial modules
- Financial data changes with before/after values
- Configuration changes affecting financial settings

### Segregation of Duties Report

Identifies potential duty conflicts:

```http
GET /api/compliance/sox/segregation-of-duties?format=pdf
```

**Detects conflicts such as:**
- Users with both data entry and approval roles
- Users with financial access and audit access
- Admin users also performing financial operations

---

## ISO 27001 Reports

### Security Event Report

Comprehensive security event tracking:

```http
GET /api/compliance/iso27001/security-report?startDate=2024-01-01&endDate=2024-12-31&format=pdf
```

**Response includes:**
- Authentication events summary
- Security threats detected
- Failed login patterns
- Access control violations
- Data modification summary

---

## User Activity Report

Track all actions by a specific user:

```http
GET /api/compliance/user-activity?userId=42&startDate=2024-01-01&endDate=2024-01-31&format=csv
```

## Data Modification Report

Track all data changes in a period:

```http
GET /api/compliance/data-modifications?startDate=2024-01-01&endDate=2024-01-31&entityType=SysCompany&format=json
```

---

## Scheduled Reports

Reports can be auto-generated on a schedule. See `appsettings.json` → `ScheduledReporting` section. The `ScheduledReportGenerationService` background service handles execution.

---

## Export Formats

| Format | Content-Type | Best For |
|---|---|---|
| JSON | `application/json` | API integration, automation |
| CSV | `text/csv` | Spreadsheet analysis |
| PDF | `application/pdf` | Formal submission to auditors |
