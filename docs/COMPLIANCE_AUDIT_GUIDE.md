# Compliance Audit Guide — GDPR, SOX, ISO 27001

## Overview

ThinkOnErp provides built-in compliance reporting through the `ComplianceReporter` service and the `/api/compliance` API endpoints. This guide covers how to conduct compliance audits using the system's capabilities.

## Prerequisites

- Admin-level JWT token (required for all compliance endpoints)
- Audit logging must be enabled (`AuditLogging:Enabled = true`)
- Reports require audit data to exist for the requested date range

---

## GDPR Compliance

### What Is Tracked

| Requirement | System Feature |
|---|---|
| Data access tracking | All read operations on personal data are logged |
| Data modification tracking | Before/after values captured for all changes |
| Right of access (Art. 15) | GDPR Access Report provides complete history |
| Right to data portability (Art. 20) | GDPR Data Export Report |
| Consent tracking | Authentication events log user consent |
| Data breach notification | Security monitoring with real-time alerts |

### Generate GDPR Access Report

```http
GET /api/compliance/gdpr/access-report?userId={userId}&startDate=2024-01-01&endDate=2024-12-31
Authorization: Bearer {admin-token}
```

This report shows all data accessed by or about a specific user, including:
- Which entities were accessed
- When the access occurred
- What data fields were read
- Source IP addresses

### Generate GDPR Data Export Report

```http
GET /api/compliance/gdpr/data-export?userId={userId}&format=json
Authorization: Bearer {admin-token}
```

Supported formats: `json`, `csv`, `pdf`

### GDPR Audit Checklist

1. ✅ Verify all personal data access is being logged — check `SYS_AUDIT_LOG` for `ENTITY_TYPE` matching user-related tables
2. ✅ Verify data masking is active — check `AuditLogging:SensitiveFields` includes `password`, `ssn`, `creditCard`
3. ✅ Generate access report for sample users — confirm completeness
4. ✅ Verify data retention policies — check `SYS_RETENTION_POLICIES` table
5. ✅ Test data export functionality — generate export for a test user
6. ✅ Verify consent records — check authentication audit events for login consent

---

## SOX Compliance

### What Is Tracked

| Requirement | System Feature |
|---|---|
| Financial data access controls | Role-based access with audit trail |
| Segregation of duties | SOX Segregation Report |
| Change management | All configuration changes logged |
| Access reviews | User activity reports |
| Data integrity | Cryptographic hash chains on audit entries |

### Generate SOX Financial Access Report

```http
GET /api/compliance/sox/financial-access?startDate=2024-01-01&endDate=2024-03-31
Authorization: Bearer {admin-token}
```

This report covers:
- All access to financial modules (Accounting, Finance, POS)
- User permission changes affecting financial data
- Configuration changes to financial settings

### Generate SOX Segregation of Duties Report

```http
GET /api/compliance/sox/segregation-of-duties
Authorization: Bearer {admin-token}
```

Identifies potential conflicts where users have both:
- Data entry and approval permissions
- Financial access and audit access
- Admin and financial operator roles

### SOX Audit Checklist

1. ✅ Generate financial access report for the audit period
2. ✅ Review segregation of duties report for conflicts
3. ✅ Verify all financial data modifications have before/after values
4. ✅ Confirm audit log integrity — use `AuditLogIntegrityService` verification
5. ✅ Review user permission change history
6. ✅ Verify encryption of sensitive financial data at rest

---

## ISO 27001 Compliance

### What Is Tracked

| Control | System Feature |
|---|---|
| A.9 Access Control | JWT authentication, RBAC, multi-tenant isolation |
| A.12 Operations Security | Performance monitoring, slow query detection |
| A.12.4 Logging & Monitoring | Full audit trail with correlation IDs |
| A.16 Incident Management | Security threat detection, alert system |
| A.18 Compliance | Automated compliance reporting |

### Generate ISO 27001 Security Report

```http
GET /api/compliance/iso27001/security-report?startDate=2024-01-01&endDate=2024-12-31
Authorization: Bearer {admin-token}
```

### ISO 27001 Audit Checklist

1. ✅ Review security event summary — check `/api/monitoring/security/summary`
2. ✅ Verify failed login tracking — check `SYS_FAILED_LOGINS` table
3. ✅ Review security threat alerts — check `/api/alerts/history`
4. ✅ Verify audit log tamper detection — cryptographic signatures active
5. ✅ Review access control policies — verify RBAC configuration
6. ✅ Confirm incident response — verify alert notification channels are active
7. ✅ Review data retention policies — verify compliance with A.18
8. ✅ Verify encryption at rest — check `AuditEncryption` configuration

---

## Export Formats

All compliance reports support multiple export formats:

| Format | Use Case | Endpoint Parameter |
|---|---|---|
| JSON | API integration, automated processing | `?format=json` |
| CSV | Spreadsheet analysis, data import | `?format=csv` |
| PDF | Formal audit submission, printing | `?format=pdf` (uses QuestPDF) |

## Scheduled Report Generation

Configure automatic report generation in `appsettings.json`:

```json
{
  "ScheduledReporting": {
    "Enabled": true,
    "Reports": [
      {
        "Type": "GDPR_ACCESS",
        "Schedule": "0 0 1 * *",
        "Format": "pdf"
      },
      {
        "Type": "SOX_FINANCIAL",
        "Schedule": "0 0 1 */3 *",
        "Format": "pdf"
      },
      {
        "Type": "ISO27001_SECURITY",
        "Schedule": "0 0 1 * *",
        "Format": "pdf"
      }
    ]
  }
}
```

## Audit Log Integrity Verification

To verify audit logs have not been tampered with:

```http
GET /api/auditlogs/integrity/verify?startDate=2024-01-01&endDate=2024-12-31
Authorization: Bearer {admin-token}
```

This validates:
- SHA-256 hash chain continuity
- Cryptographic signature validity
- No gaps in sequence numbers
- No modified entries detected
