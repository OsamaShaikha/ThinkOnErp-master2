# Current API endpoint catalog

Generated from the running application's Swagger documents. Re-run `scripts/generate-api-docs.ps1` while the API is running to refresh this catalog and the OpenAPI JSON files.

Generated: 2026-07-18 00:12:04 +03:00

## ThinkOnErp SuperAdmin API

133 operations across 103 paths. Full schemas and examples are in [superadmin.json](openapi/superadmin.json).

| Method | Path | Summary | Request content | Documented responses |
|---|---|---|---|---|
| POST | `/api/alerts/{id}/acknowledge` | Acknowledge an alert. Indicates that the alert has been reviewed by an administrator. Updates the alert status and records who acknowledged it and when. | application/json, text/json, application/*+json | 200, 400, 401, 403, 404 |
| POST | `/api/alerts/{id}/resolve` | Resolve an alert. Indicates that the alert has been addressed and closed. Updates the alert status to 'Resolved' and records resolution details. | application/json, text/json, application/*+json | 200, 400, 401, 403, 404 |
| GET | `/api/alerts/history` | Get alert history with pagination. Returns historical alerts that have been triggered, including acknowledgment and resolution status. | - | 200, 400, 401, 403 |
| GET | `/api/alerts/rules` | Get all configured alert rules with pagination. Returns alert rules with their conditions, thresholds, and notification settings. | - | 200, 400, 401, 403 |
| POST | `/api/alerts/rules` | Create a new alert rule. Defines when and how alerts should be triggered based on event type and severity. | application/json, text/json, application/*+json | 201, 400, 401, 403 |
| DELETE | `/api/alerts/rules/{id}` | Delete an alert rule. Removes the rule from the system and stops triggering alerts based on this rule. | - | 200, 401, 403, 404 |
| PUT | `/api/alerts/rules/{id}` | Update an existing alert rule. Allows modification of alert conditions, thresholds, notification channels, and recipients. | application/json, text/json, application/*+json | 200, 400, 401, 403, 404 |
| POST | `/api/alerts/test/email` | Test email notification channel. Sends a test email alert to verify email configuration. | application/json, text/json, application/*+json | 200, 400, 401, 403 |
| POST | `/api/alerts/test/sms` | Test SMS notification channel. Sends a test SMS alert to verify SMS configuration. | application/json, text/json, application/*+json | 200, 400, 401, 403 |
| POST | `/api/alerts/test/webhook` | Test webhook notification channel. Sends a test webhook alert to verify webhook configuration. | application/json, text/json, application/*+json | 200, 400, 401, 403 |
| GET | `/api/AuditHealth/metrics` | Get detailed metrics about the audit logging system. Requires admin authorization. | - | 200 |
| POST | `/api/AuditHealth/replay-fallback` | Replay fallback events from file system to database. This should be called manually after database becomes available again. Requires admin authorization. | - | 200 |
| GET | `/api/AuditHealth/status` | Get the health status of the audit logging system. Returns 200 OK if healthy, 503 Service Unavailable if unhealthy. This endpoint does NOT block API requests - it only reports status. | - | 200, 503 |
| GET | `/api/auditlogs/{id}` | Gets a single audit log entry with full detail. | - | 200, 401, 403, 404 |
| GET | `/api/auditlogs/{id}/status` | Gets current status of an audit log entry. Requires AdminOnly authorization. | - | 200, 400, 401, 403, 404 |
| GET | `/api/auditlogs/correlation/{correlationId}` | Gets all audit logs for a specific correlation ID. Returns all log entries associated with a single request, useful for debugging and request tracing. Requires AdminOnly authorization. | - | 200, 400, 401, 403 |
| GET | `/api/auditlogs/dashboard` | Gets dashboard counters for legacy view. Returns: Unresolved count, In Progress count, Resolved count, Critical Errors count Matches the top section of logs.png interface. Requires AdminOnly authorization. | - | 200, 401, 403 |
| GET | `/api/auditlogs/entity/{entityType}/{entityId}` | Gets the complete audit history for a specific entity. Returns all modifications (INSERT, UPDATE, DELETE) for the entity in chronological order. Useful for compliance audits, data lineage tracking, and investigating entity changes. Requires AdminOnly authorization. | - | 200, 400, 401, 403 |
| GET | `/api/auditlogs/export/csv` | Exports audit logs to CSV format based on filter criteria. Generates a CSV file with all audit log fields for offline analysis. Supports compliance reporting and data archival requirements. Requires AdminOnly authorization. | - | 200, 400, 401, 403 |
| GET | `/api/auditlogs/export/json` | Exports audit logs to JSON format based on filter criteria. Generates a JSON document with all audit log fields for programmatic processing. Supports API integrations and automated compliance reporting. Requires AdminOnly authorization. | - | 200, 400, 401, 403 |
| GET | `/api/auditlogs/legacy` | - | - | 200, 400, 401, 403 |
| PUT | `/api/auditlogs/legacy/{id}/status` | Updates status of audit log entry (for error resolution workflow). Updates status: Unresolved -> In Progress -> Resolved Requires AdminOnly authorization. This is the legacy-compatible endpoint matching the logs.png interface. | application/json, text/json, application/*+json | 200, 400, 401, 403, 404 |
| GET | `/api/auditlogs/legacy/export/csv` | Exports audit logs to CSV using legacy filters (matching the dashboard filter fields). | - | 200, 400 |
| GET | `/api/auditlogs/query` | Query audit logs with comprehensive filtering and pagination. Supports filtering by date range, actor, company, branch, entity type, action type, and more. Results are automatically filtered by user's company access. Returns results within 2 seconds for date ranges up to 30 days. Requires AdminOnly authorization. | - | 200, 400, 401, 403 |
| GET | `/api/auditlogs/replay/user/{userId}` | Gets user action replay for debugging and analysis. Returns a complete chronological sequence of all actions performed by a specific user within a time range. Includes request/response payloads, timing information, and timeline visualization data. Useful for reproducing bugs, understanding user workflows, and investigating user behavior. Requires AdminOnly authorization. | - | 200, 400, 401, 403 |
| GET | `/api/auditlogs/search` | Performs full-text search across all audit log fields. Searches through descriptions, error messages, entity types, actions, and metadata. Uses Oracle Text for efficient full-text search capabilities. Requires AdminOnly authorization. | - | 200, 400, 401, 403 |
| POST | `/api/auditlogs/transform` | Transforms a comprehensive audit log entry to legacy format. This endpoint is primarily for internal use and testing. Requires AdminOnly authorization. | application/json, text/json, application/*+json | 200, 400, 401, 403 |
| POST | `/api/audit-trail/export` | Exports audit trail data for compliance reporting. Requires AdminOnly authorization. Validates Requirement 17.7: Provide audit trail export functionality. | application/json, text/json, application/*+json | 200, 400, 401, 403 |
| POST | `/api/audit-trail/search` | Searches audit trail with advanced filtering. Requires AdminOnly authorization. Validates Requirement 17.11: Provide audit trail search and filtering capabilities. | application/json, text/json, application/*+json | 200, 400, 401, 403 |
| GET | `/api/audit-trail/statistics` | Retrieves audit trail statistics and summary information. Requires AdminOnly authorization. Validates Requirement 17.12: Ensure audit trail integrity through proper database constraints and validation. | - | 200, 401, 403 |
| GET | `/api/audit-trail/tickets/{ticketId}` | Retrieves audit trail for a specific ticket. Requires AdminOnly authorization. Validates Requirement 17.11: Provide audit trail search and filtering capabilities. | - | 200, 401, 403, 404 |
| POST | `/api/auth/superadmin/login` | - | application/json, text/json, application/*+json | 200, 401, 400 |
| POST | `/api/auth/superadmin/refresh` | - | application/json, text/json, application/*+json | 200, 401, 400 |
| GET | `/api/companies` | - | - | 200, 401 |
| POST | `/api/companies` | - | multipart/form-data | 201, 400, 401, 403 |
| DELETE | `/api/companies/{id}` | - | - | 200, 404, 401, 403 |
| GET | `/api/companies/{id}` | - | - | 200, 404, 401 |
| PUT | `/api/companies/{id}` | - | multipart/form-data | 200, 400, 404, 401, 403 |
| PUT | `/api/configuration/{key}` | Updates a specific configuration value by key. Validates the configuration key and value, then updates the setting. Configuration changes are logged for audit trail. Requires AdminOnly authorization. | application/json, text/json, application/*+json | 200, 400, 404, 401, 403 |
| GET | `/api/configuration/all` | Retrieves all ticket system configuration settings. Requires AdminOnly authorization. | - | 200, 401, 403 |
| GET | `/api/configuration/file-attachments` | Retrieves file attachment configuration settings. Includes maximum file size, attachment count limits, and allowed file types. Requires AdminOnly authorization. | - | 200, 401, 403 |
| GET | `/api/configuration/notifications` | Retrieves notification configuration settings. Includes notification enabled status and email templates. Requires AdminOnly authorization. | - | 200, 401, 403 |
| GET | `/api/configuration/sla-settings` | Retrieves SLA (Service Level Agreement) configuration settings. Includes priority-based target hours and escalation thresholds. Requires AdminOnly authorization. | - | 200, 401, 403 |
| PUT | `/api/configuration/sla-settings` | Updates SLA (Service Level Agreement) configuration settings in bulk. Updates all priority-based target hours and escalation threshold in a single operation. Configuration changes are validated and logged for audit trail. Requires AdminOnly authorization. | application/json, text/json, application/*+json | 200, 400, 401, 403 |
| GET | `/api/configuration/workflow` | Retrieves workflow configuration settings. Includes allowed status transitions and auto-close policies. Requires AdminOnly authorization. | - | 200, 401, 403 |
| GET | `/api/currencies` | Retrieves all active currencies from the system. Requires authentication. | - | 200, 401 |
| POST | `/api/currencies` | Creates a new currency in the system. Requires AdminOnly authorization. | application/json, text/json, application/*+json | 201, 400, 401, 403 |
| DELETE | `/api/currencies/{id}` | Deletes (soft delete) a currency from the system. Requires AdminOnly authorization. | - | 200, 404, 401, 403 |
| GET | `/api/currencies/{id}` | Retrieves a specific currency by its ID. Requires authentication. | - | 200, 404, 401 |
| PUT | `/api/currencies/{id}` | Updates an existing currency in the system. Requires AdminOnly authorization. | application/json, text/json, application/*+json | 200, 400, 404, 401, 403 |
| GET | `/api/documents` | - | - | 200 |
| DELETE | `/api/documents/{id}` | - | - | 200, 404 |
| GET | `/api/documents/{id}` | - | - | 200, 404 |
| PUT | `/api/documents/{id}` | - | application/json, text/json, application/*+json | 200, 404 |
| GET | `/api/documents/{id}/download` | - | - | 200, 404 |
| GET | `/api/documents/branch/{branchId}` | - | - | 200 |
| POST | `/api/documents/bulk-delete` | - | application/json, text/json, application/*+json | 200, 404 |
| GET | `/api/documents/company/{companyId}` | - | - | 200 |
| GET | `/api/documents/metadata` | - | - | 200 |
| GET | `/api/documents/my` | - | - | 200 |
| POST | `/api/documents/upload` | - | multipart/form-data | 201, 400 |
| POST | `/api/documents/upload-bulk` | - | multipart/form-data | 201, 400 |
| GET | `/api/features` | - | - | 200 |
| POST | `/api/features` | - | multipart/form-data | 201, 400 |
| DELETE | `/api/features/{id}` | - | - | 200, 404 |
| GET | `/api/features/{id}` | - | - | 200, 404 |
| PUT | `/api/features/{id}` | - | multipart/form-data | 200, 400, 404 |
| GET | `/api/health` | Basic health check endpoint. | - | 200 |
| GET | `/api/health/detailed` | Detailed health check with circuit breaker information. | - | 200 |
| GET | `/api/KeyManagement/encryption-key-version` | Gets the current encryption key version. | - | 200 |
| POST | `/api/KeyManagement/rotate-encryption-key` | Rotates the encryption key and returns the new version identifier. | - | 200, 500 |
| POST | `/api/KeyManagement/rotate-signing-key` | Rotates the signing key and returns the new version identifier. | - | 200, 500 |
| GET | `/api/KeyManagement/rotation-status` | Gets the current key rotation status and metadata. | - | 200 |
| GET | `/api/KeyManagement/signing-key-version` | Gets the current signing key version. | - | 200 |
| GET | `/api/KeyManagement/validate` | Validates that all required keys are configured and accessible. | - | 200 |
| GET | `/api/modules` | - | - | 200 |
| POST | `/api/modules` | - | application/json, text/json, application/*+json | 201, 400 |
| DELETE | `/api/modules/{id}` | - | - | 200, 404 |
| GET | `/api/modules/{id}` | - | - | 200, 404 |
| PUT | `/api/modules/{id}` | - | application/json, text/json, application/*+json | 200, 400, 404 |
| GET | `/api/Monitoring/audit/fallback-status` | Get audit logging fallback status. | - | 200 |
| GET | `/api/Monitoring/audit/metrics` | Get comprehensive audit logging system metrics. | - | 200 |
| POST | `/api/Monitoring/audit/replay-fallback` | Manually trigger replay of fallback audit events to database. | - | 200 |
| GET | `/api/Monitoring/audit-queue-depth` | Get the current audit queue depth. | - | 200 |
| GET | `/api/Monitoring/health` | Get comprehensive system health metrics including CPU, memory, and database connections. | - | 200 |
| GET | `/api/Monitoring/memory` | Get detailed memory usage metrics including heap sizes and GC statistics. | - | 200 |
| POST | `/api/Monitoring/memory/gc` | Force garbage collection for a specific generation. | - | 200 |
| POST | `/api/Monitoring/memory/optimize` | Trigger memory optimization strategies including garbage collection and heap compaction. | - | 200 |
| GET | `/api/Monitoring/memory/pressure` | Detect current memory pressure level and get recommendations. | - | 200 |
| GET | `/api/Monitoring/memory/recommendations` | Get memory optimization recommendations based on current usage patterns. | - | 200 |
| GET | `/api/Monitoring/performance/connection-pool` | Get detailed Oracle connection pool metrics. | - | 200 |
| GET | `/api/Monitoring/performance/endpoint` | Get performance statistics for a specific endpoint over a time period. | - | 200 |
| GET | `/api/Monitoring/performance/slow-queries` | Get slow database queries that exceeded the specified execution time threshold. | - | 200 |
| GET | `/api/Monitoring/performance/slow-requests` | Get slow requests that exceeded the specified execution time threshold. | - | 200 |
| GET | `/api/Monitoring/security/check-anomalous-activity` | Detect anomalous activity for a specific user. | - | 200, 404 |
| GET | `/api/Monitoring/security/check-failed-logins` | Check for failed login patterns from a specific IP address. | - | 200, 404 |
| POST | `/api/Monitoring/security/check-sql-injection` | Detect SQL injection patterns in input text. | application/json, text/json, application/*+json | 200, 404 |
| POST | `/api/Monitoring/security/check-xss` | Detect XSS (Cross-Site Scripting) patterns in input text. | application/json, text/json, application/*+json | 200, 404 |
| GET | `/api/Monitoring/security/daily-summary` | Generate a daily security summary report for a specific date. | - | 200 |
| GET | `/api/Monitoring/security/failed-login-count` | Get the count of failed login attempts for a specific user. | - | 200 |
| GET | `/api/Monitoring/security/threats` | Get all active security threats that have not been resolved. | - | 200 |
| POST | `/api/Monitoring/test-alert` | Test alert delivery by sending a test alert through configured channels. | - | 200 |
| GET | `/api/screens` | - | - | 200 |
| POST | `/api/screens` | - | multipart/form-data | 201, 400 |
| DELETE | `/api/screens/{id}` | - | - | 200, 404 |
| GET | `/api/screens/{id}` | - | - | 200, 404 |
| PUT | `/api/screens/{id}` | - | multipart/form-data | 200, 400, 404 |
| GET | `/api/screens/{screenId}/features` | - | - | 200 |
| POST | `/api/screens/{screenId}/features` | - | application/json, text/json, application/*+json | 200, 400 |
| DELETE | `/api/screens/{screenId}/features/{featureId}` | - | - | 200, 404 |
| GET | `/api/screens/bysystem/{systemId}` | - | - | 200 |
| GET | `/api/superadmins` | Retrieves all active super admin accounts | - | 200 |
| POST | `/api/superadmins` | Creates a new super admin account | application/json, text/json, application/*+json | 201, 400 |
| DELETE | `/api/superadmins/{id}` | Deletes a super admin account (soft delete) | - | 200, 404, 403 |
| GET | `/api/superadmins/{id}` | Retrieves a specific super admin by ID | - | 200, 404 |
| PUT | `/api/superadmins/{id}` | Updates an existing super admin account | application/json, text/json, application/*+json | 200, 404, 403 |
| PUT | `/api/superadmins/{id}/change-password` | Changes the password for a specific super admin | application/json, text/json, application/*+json | 200, 400, 404 |
| POST | `/api/superadmins/{id}/reset-password` | Resets the password for a specific super admin (admin-initiated) Generates a secure temporary password | - | 200, 404 |
| GET | `/api/superadmins/dashboard` | - | - | 200, 401, 403, 500 |
| POST | `/api/superadmins/provision-dev-schema` | Provisions or re-syncs the developer template schema. | - | 200 |
| POST | `/api/superadmins/sync-tenant-schemas` | Syncs all existing tenant schemas against the developer template. | - | 200 |
| GET | `/api/syscodes` | - | - | 200 |
| POST | `/api/syscodes` | - | application/json, text/json, application/*+json | 201, 400 |
| GET | `/api/syscodes/groups` | - | - | 200 |
| GET | `/api/syscodes/groups/{codeMgr}` | - | - | 200 |
| DELETE | `/api/syscodes/groups/{codeMgr}/items/{codeMnr}/langs/{codeLang}` | - | - | 200, 404 |
| GET | `/api/syscodes/groups/{codeMgr}/items/{codeMnr}/langs/{codeLang}` | - | - | 200, 404 |
| PUT | `/api/syscodes/groups/{codeMgr}/items/{codeMnr}/langs/{codeLang}` | - | application/json, text/json, application/*+json | 200, 404 |
| GET | `/api/syssettings` | - | - | 200 |
| POST | `/api/syssettings` | - | application/json, text/json, application/*+json | 201, 400 |
| DELETE | `/api/syssettings/{settingCode}` | - | - | 200, 404 |
| GET | `/api/syssettings/{settingCode}` | - | - | 200, 404 |
| PUT | `/api/syssettings/{settingCode}` | - | application/json, text/json, application/*+json | 200, 404 |

## ThinkOnErp Company API

92 operations across 64 paths. Full schemas and examples are in [company.json](openapi/company.json).

| Method | Path | Summary | Request content | Documented responses |
|---|---|---|---|---|
| POST | `/api/Auth/login` | Authenticates a user and generates a JWT token. This endpoint does not require authorization. | application/json, text/json, application/*+json | 200, 401, 400 |
| POST | `/api/Auth/refresh` | Refreshes an expired access token using a valid refresh token. This endpoint does not require authorization. | application/json, text/json, application/*+json | 200, 401, 400 |
| GET | `/api/branches` | - | - | 200, 401 |
| POST | `/api/branches` | - | multipart/form-data | 201, 400, 401, 403 |
| GET | `/api/branches/{branchId}/access/features/revoked` | - | - | 200, 404 |
| GET | `/api/branches/{branchId}/access/screens` | - | - | 200, 404 |
| GET | `/api/branches/{branchId}/access/screens/{screenId}/features` | - | - | 200, 404 |
| DELETE | `/api/branches/{branchId}/access/screens/{screenId}/features/{featureId}/revoke` | - | - | 200, 404 |
| POST | `/api/branches/{branchId}/access/screens/{screenId}/features/{featureId}/revoke` | - | - | 200, 404 |
| DELETE | `/api/branches/{branchId}/access/screens/{screenId}/revoke` | - | - | 200, 404 |
| POST | `/api/branches/{branchId}/access/screens/{screenId}/revoke` | - | - | 200, 404 |
| GET | `/api/branches/{branchId}/access/screens/revoked` | - | - | 200, 404 |
| GET | `/api/branches/{branchId}/access/state` | - | - | 200, 404 |
| GET | `/api/branches/{branchId}/access/systems` | - | - | 200, 404 |
| PUT | `/api/branches/{branchId}/access/systems` | - | application/json, text/json, application/*+json | 200, 404, 400 |
| GET | `/api/branches/{branchId}/permissions/roles/{roleId}` | - | - | 200 |
| PUT | `/api/branches/{branchId}/permissions/roles/{roleId}` | - | application/json, text/json, application/*+json | 200 |
| DELETE | `/api/branches/{branchId}/permissions/roles/{roleId}/screens/{screenId}/features/{featureId}` | - | - | 200 |
| GET | `/api/branches/{branchId}/permissions/users/{userId}` | - | - | 200 |
| PUT | `/api/branches/{branchId}/permissions/users/{userId}` | - | application/json, text/json, application/*+json | 200 |
| DELETE | `/api/branches/{branchId}/permissions/users/{userId}/screens/{screenId}/features/{featureId}` | - | - | 200 |
| DELETE | `/api/branches/{id}` | - | - | 200, 404, 401, 403 |
| GET | `/api/branches/{id}` | - | - | 200, 404, 401 |
| PUT | `/api/branches/{id}` | - | multipart/form-data | 200, 400, 404, 401, 403 |
| GET | `/api/branches/company/{companyId}` | - | - | 200, 401 |
| GET | `/api/compliance/data-modification` | Generate data modification report for a specific entity. Returns a complete audit trail of all modifications (INSERT, UPDATE, DELETE) for the entity. Useful for data lineage tracking, compliance audits, and debugging. | - | 200, 400, 401, 403, 404 |
| GET | `/api/compliance/gdpr/access-report` | Generate GDPR data access report for a specific data subject. Returns a comprehensive report of all access to the data subject's personal data. Supports GDPR Article 15 (Right of Access) compliance requirements. | - | 200, 400, 401, 403, 404 |
| GET | `/api/compliance/gdpr/data-export` | Generate GDPR data export report for a specific data subject. Returns a complete export of all personal data stored in the system for the data subject. Supports GDPR Article 20 (Right to Data Portability) compliance requirements. | - | 200, 400, 401, 403, 404 |
| GET | `/api/compliance/iso27001/security-report` | Generate ISO 27001 security event report. Returns a comprehensive report of security events for ISO 27001 compliance. Supports ISO 27001 Annex A.12.4 (Logging and Monitoring) compliance requirements. | - | 200, 400, 401, 403 |
| GET | `/api/compliance/sox/financial-access` | Generate SOX financial data access report. Returns a comprehensive report of all financial data access events for SOX compliance. Supports SOX Section 404 (Internal Controls) compliance requirements. | - | 200, 400, 401, 403 |
| GET | `/api/compliance/sox/segregation-of-duties` | Generate SOX segregation of duties report. Returns a report identifying potential segregation of duties violations. Supports SOX Section 404 (Internal Controls) compliance requirements. | - | 200, 400, 401, 403 |
| GET | `/api/compliance/user-activity` | Generate user activity report for a specific user. Returns a chronological report of all user actions within the specified date range. Useful for user behavior analysis, compliance audits, and security investigations. | - | 200, 400, 401, 403, 404 |
| GET | `/api/documents` | - | - | 200 |
| DELETE | `/api/documents/{id}` | - | - | 200, 404 |
| GET | `/api/documents/{id}` | - | - | 200, 404 |
| PUT | `/api/documents/{id}` | - | application/json, text/json, application/*+json | 200, 404 |
| GET | `/api/documents/{id}/download` | - | - | 200, 404 |
| GET | `/api/documents/branch/{branchId}` | - | - | 200 |
| POST | `/api/documents/bulk-delete` | - | application/json, text/json, application/*+json | 200, 404 |
| GET | `/api/documents/company/{companyId}` | - | - | 200 |
| GET | `/api/documents/metadata` | - | - | 200 |
| GET | `/api/documents/my` | - | - | 200 |
| POST | `/api/documents/upload` | - | multipart/form-data | 201, 400 |
| POST | `/api/documents/upload-bulk` | - | multipart/form-data | 201, 400 |
| GET | `/api/fiscalyears` | Retrieves all active fiscal years from the system. Requires authentication. | - | 200, 401 |
| POST | `/api/fiscalyears` | Creates a new fiscal year in the system. Requires AdminOnly authorization. | application/json, text/json, application/*+json | 201, 400, 401, 403 |
| DELETE | `/api/fiscalyears/{id}` | Deletes (soft delete) a fiscal year from the system. Requires AdminOnly authorization. | - | 200, 404, 401, 403 |
| GET | `/api/fiscalyears/{id}` | Retrieves a specific fiscal year by its ID. Requires authentication. | - | 200, 404, 401 |
| PUT | `/api/fiscalyears/{id}` | Updates an existing fiscal year in the system. Requires AdminOnly authorization. | application/json, text/json, application/*+json | 200, 400, 404, 401, 403 |
| POST | `/api/fiscalyears/{id}/close` | Closes a fiscal year, preventing further modifications. Requires AdminOnly authorization. | application/json, text/json, application/*+json | 200, 404, 401, 403 |
| GET | `/api/fiscalyears/company/{companyId}` | Retrieves all fiscal years for a specific company. Requires authentication. | - | 200, 401 |
| GET | `/api/permissions/check` | - | - | 200 |
| GET | `/api/permissions/my-screens` | - | - | 200 |
| GET | `/api/Roles` | Retrieves all active roles from the system. Requires authentication. | - | 200, 401 |
| POST | `/api/Roles` | Creates a new role in the system. Requires AdminOnly authorization. | application/json, text/json, application/*+json | 201, 400, 401, 403 |
| DELETE | `/api/Roles/{id}` | Deletes (soft delete) a role from the system. Requires AdminOnly authorization. | - | 200, 404, 401, 403 |
| GET | `/api/Roles/{id}` | Retrieves a specific role by its ID. Requires authentication. | - | 200, 404, 401 |
| PUT | `/api/Roles/{id}` | Updates an existing role in the system. Requires AdminOnly authorization. | application/json, text/json, application/*+json | 200, 400, 404, 401, 403 |
| GET | `/api/saved-searches` | Retrieves all saved searches accessible to the current user (private + public). Requires authentication. | - | 200, 401 |
| POST | `/api/saved-searches` | Creates a new saved search for the current user. Requires authentication. | application/json, text/json, application/*+json | 201, 400, 401 |
| GET | `/api/tickets` | Retrieves tickets with filtering, sorting, and pagination. Requires authentication. | - | 200, 401 |
| POST | `/api/tickets` | Creates a new ticket with optional file attachments. Requires authentication. | application/json, text/json, application/*+json | 201, 400, 401 |
| DELETE | `/api/tickets/{id}` | Deletes (soft delete) a ticket from the system. Requires AdminOnly authorization. | - | 200, 404, 401, 403 |
| GET | `/api/tickets/{id}` | Retrieves a specific ticket by its ID with full details. Requires authentication and authorization to view the ticket. | - | 200, 404, 401, 403 |
| PUT | `/api/tickets/{id}` | Updates an existing ticket. Requires authentication and authorization to update the ticket. | application/json, text/json, application/*+json | 200, 400, 404, 401, 403 |
| PUT | `/api/tickets/{id}/assign` | Assigns a ticket to a support staff member. Requires AdminOnly authorization. | application/json, text/json, application/*+json | 200, 400, 404, 401, 403 |
| GET | `/api/tickets/{id}/attachments` | Retrieves all attachments for a specific ticket. Requires authentication and authorization to view the ticket. | - | 200, 404, 401, 403 |
| POST | `/api/tickets/{id}/attachments` | Uploads a file attachment to a ticket. Requires authentication and authorization to attach files to the ticket. | application/json, text/json, application/*+json | 201, 400, 404, 401, 403 |
| GET | `/api/tickets/{id}/attachments/{attachmentId}` | Downloads a specific attachment file from a ticket. Requires authentication and authorization to view the ticket. Returns the file with proper content-type headers. | - | 200, 404, 401, 403 |
| GET | `/api/tickets/{id}/comments` | Retrieves all comments for a specific ticket. Requires authentication and authorization to view the ticket. | - | 200, 404, 401, 403 |
| POST | `/api/tickets/{id}/comments` | Adds a comment to a ticket. Requires authentication and authorization to comment on the ticket. | application/json, text/json, application/*+json | 201, 400, 404, 401, 403 |
| PUT | `/api/tickets/{id}/status` | Updates the status of a ticket with workflow validation. Requires authentication and authorization to update the ticket. | application/json, text/json, application/*+json | 200, 400, 404, 401, 403 |
| GET | `/api/tickets/reports/sla-compliance` | Retrieves SLA compliance report with priority and type breakdown. Requires AdminOnly authorization. Supports export to PDF and Excel formats via format parameter. | - | 200, 400, 401, 403 |
| GET | `/api/tickets/reports/volume` | Retrieves ticket volume report with time-based filtering and grouping. Requires AdminOnly authorization. Supports export to PDF and Excel formats via format parameter. | - | 200, 400, 401, 403 |
| GET | `/api/tickets/reports/workload` | Retrieves workload report showing ticket distribution per assignee. Requires AdminOnly authorization. Supports export to PDF and Excel formats via format parameter. | - | 200, 400, 401, 403 |
| POST | `/api/tickets/search/save` | Creates a new saved search. This is a convenience endpoint that redirects to /api/saved-searches. Requires authentication. | - | 307, 401 |
| GET | `/api/tickets/search/saved` | Retrieves saved searches for the current user. This is a convenience endpoint that redirects to /api/saved-searches. Requires authentication. | - | 307, 401 |
| GET | `/api/ticket-types` | Retrieves all active ticket types from the system. Requires authentication. | - | 200, 401 |
| POST | `/api/ticket-types` | Creates a new ticket type in the system. Requires AdminOnly authorization. | application/json, text/json, application/*+json | 201, 400, 401, 403 |
| DELETE | `/api/ticket-types/{id}` | Deletes (soft delete) a ticket type from the system. Requires AdminOnly authorization. Checks for dependencies before deletion. | - | 200, 400, 404, 401, 403 |
| GET | `/api/ticket-types/{id}` | Retrieves a specific ticket type by its ID. Requires authentication. | - | 200, 404, 401 |
| PUT | `/api/ticket-types/{id}` | Updates an existing ticket type in the system. Requires AdminOnly authorization. | application/json, text/json, application/*+json | 200, 400, 404, 401, 403 |
| GET | `/api/users` | Retrieves all active users from the system. Requires AdminOnly authorization. | - | 200, 401, 403 |
| POST | `/api/users` | Creates a new user in the system. Requires AdminOnly authorization. | application/json, text/json, application/*+json | 201, 400, 401, 403 |
| DELETE | `/api/users/{id}` | Deletes (soft delete) a user from the system. Requires AdminOnly authorization. | - | 200, 404, 401, 403 |
| GET | `/api/users/{id}` | Retrieves a specific user by their ID. Requires AdminOnly authorization. | - | 200, 404, 401, 403 |
| PUT | `/api/users/{id}` | Updates an existing user in the system. Requires AdminOnly authorization. | application/json, text/json, application/*+json | 200, 400, 404, 401, 403 |
| PUT | `/api/users/{id}/change-password` | Changes the password for a specific user. Requires authentication (not AdminOnly - users can change their own password). | application/json, text/json, application/*+json | 200, 400, 401 |
| POST | `/api/users/{id}/force-logout` | Forces logout of a user by invalidating all their tokens. Requires AdminOnly authorization. | - | 200, 404, 401, 403 |
| POST | `/api/users/{id}/reset-password` | Resets the password for a specific user (admin-initiated) Generates a secure temporary password Requires AdminOnly authorization. | - | 200, 404, 401, 403 |
| GET | `/api/users/branch/{branchId}` | Retrieves all active users for a specific branch. Requires AdminOnly authorization. | - | 200, 401, 403 |
| GET | `/api/users/company/{companyId}` | Retrieves all active users for a specific company (through branches). Requires AdminOnly authorization. | - | 200, 401, 403 |

