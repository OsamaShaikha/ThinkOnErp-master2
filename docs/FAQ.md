# Frequently Asked Questions (FAQ)

## General

### Q: What is the Full Traceability System?
A comprehensive audit logging, compliance reporting, performance monitoring, and security threat detection system built into ThinkOnErp. It automatically tracks all data changes, authentication events, permission modifications, and system exceptions.

### Q: Does audit logging affect API performance?
The system adds <10ms latency to 99% of API requests. Audit events are queued asynchronously and written in batches, so the API response is not blocked.

### Q: What happens if the database is down?
The system uses a circuit breaker pattern. When the database is unavailable, audit events are written to the local filesystem as a fallback. Once the database recovers, a background service automatically replays the saved events.

---

## Audit Logging

### Q: How do I find all changes made by a specific user?
```http
GET /api/auditlogs?actorId={userId}&page=1&pageSize=50
```

### Q: How do I see the full history of a record?
```http
GET /api/auditlogs/entity-history?entityType=SysUser&entityId=42
```

### Q: How do I trace a complete API request?
Every request is assigned a Correlation ID (returned in response header `X-Correlation-ID`). Use it:
```http
GET /api/auditlogs/correlation/{correlationId}
```

### Q: What data is masked in audit logs?
By default: `password`, `token`, `refreshToken`, `creditCard`, `ssn`, `socialSecurityNumber`. Configure via `AuditLogging:SensitiveFields`.

### Q: How long is audit data retained?
Configurable per event category via the `SYS_RETENTION_POLICIES` table. Default is 365 days. Archival runs daily via background service.

---

## Security

### Q: How are failed logins handled?
After 5 failed attempts within 5 minutes, the account is temporarily blocked. All failed attempts are logged in `SYS_FAILED_LOGINS` and trigger security alerts.

### Q: How do I detect SQL injection attempts?
The `SecurityMonitor` automatically detects SQL injection patterns in request payloads and generates alerts. Check: `GET /api/monitoring/security/summary`

### Q: Are audit logs tamper-proof?
Each audit entry is cryptographically signed with HMAC-SHA256 and linked via hash chain. The integrity service can verify the complete chain on demand.

---

## Compliance

### Q: Which compliance standards are supported?
GDPR (Articles 15, 20), SOX (financial access, segregation of duties), and ISO 27001 (security events, access control).

### Q: Can reports be generated automatically?
Yes. Configure the `ScheduledReportGenerationService` in `appsettings.json` → `ScheduledReporting` with cron expressions.

### Q: What export formats are available?
JSON, CSV, and PDF (generated with QuestPDF library).

---

## Monitoring

### Q: How do I check system health?
```http
GET /health                        # Basic health check
GET /api/monitoring/health         # Detailed audit system health
GET /api/monitoring/performance    # Performance metrics
GET /api/monitoring/memory         # Memory usage
```

### Q: How do I view slow queries?
```http
GET /api/monitoring/slow-queries?threshold=2000
```

### Q: Where are Grafana dashboards?
Grafana dashboards are in `monitoring/dashboards/`. Use `docker-compose -f monitoring/docker-compose.monitoring.yml up` to start the monitoring stack.

---

## Alerts

### Q: How do I set up email alerts?
Configure SMTP settings in `appsettings.json` → `Alerting:Email`, then create alert rules via `POST /api/alerts/rules`.

### Q: Why am I not receiving alerts?
Check: (1) Alerting is enabled, (2) notification channel is configured, (3) rate limiting hasn't suppressed it, (4) alert rule is enabled. Monitor via `GET /api/monitoring/alerts/health`.

---

## Troubleshooting

### Q: Audit events are not appearing
Check: `AuditLogging:Enabled = true`, circuit breaker is not open (`GET /api/monitoring/audit/health`), database connection is valid.

### Q: Performance is degraded
Reduce `MaxPayloadSize`, increase `BatchSize`, check connection pool utilization, review slow query metrics.

### Q: Application won't start
Configuration validation runs at startup. Check console output for validation errors. Common issues: missing JWT secret key, invalid connection string, malformed configuration values.
