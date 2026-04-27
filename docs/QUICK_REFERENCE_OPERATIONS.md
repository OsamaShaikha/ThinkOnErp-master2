# Quick Reference: Daily Operations

## Health Checks

| Check | Endpoint | Frequency |
|---|---|---|
| System health | `GET /health` | Every 1 min |
| Audit system health | `GET /api/monitoring/health` | Every 5 min |
| Performance metrics | `GET /api/monitoring/performance` | Every 5 min |
| Memory usage | `GET /api/monitoring/memory` | Every 5 min |
| Connection pool | `GET /api/monitoring/connection-pool` | Every 5 min |
| Security summary | `GET /api/monitoring/security/summary` | Every 15 min |
| Slow queries | `GET /api/monitoring/slow-queries` | Every 15 min |

---

## Common Operations

### Query Recent Audit Logs
```
GET /api/auditlogs?page=1&pageSize=20
```

### Search Audit Logs
```
GET /api/auditlogs/search?query=error&page=1&pageSize=20
```

### Trace a Request
```
GET /api/auditlogs/correlation/{correlation-id}
```

### View Entity History
```
GET /api/auditlogs/entity-history?entityType=SysUser&entityId=42
```

### Check Error Dashboard
```
GET /api/auditlogs/legacy-dashboard
```

### Export Audit Data
```
GET /api/auditlogs/export?startDate=2024-01-01&endDate=2024-01-31&format=csv
```

---

## Alert Management

| Action | Endpoint |
|---|---|
| View active alerts | `GET /api/alerts?status=active` |
| Acknowledge alert | `PUT /api/alerts/{id}/acknowledge` |
| Resolve alert | `PUT /api/alerts/{id}/resolve` |
| View alert history | `GET /api/alerts/history` |
| List alert rules | `GET /api/alerts/rules` |

---

## Compliance Reports

| Report | Endpoint |
|---|---|
| GDPR Access | `GET /api/compliance/gdpr/access-report?userId=X` |
| GDPR Export | `GET /api/compliance/gdpr/data-export?userId=X` |
| SOX Financial | `GET /api/compliance/sox/financial-access` |
| SOX Segregation | `GET /api/compliance/sox/segregation-of-duties` |
| ISO 27001 Security | `GET /api/compliance/iso27001/security-report` |

Add `&format=pdf` for PDF output, `&format=csv` for CSV.

---

## Key Thresholds

| Metric | Warning | Critical |
|---|---|---|
| API latency (p99) | >5000ms | >10000ms |
| Error rate | >5% | >10% |
| Connection pool | >80% | >95% |
| Memory usage | >512MB | >1024MB |
| Audit queue depth | >5000 | >8000 |
| Failed logins | >5/5min | >10/5min |

---

## Emergency Procedures

**Circuit Breaker Open (DB failure):**
1. Check database connectivity
2. Review `GET /api/monitoring/audit/health`
3. Events are being saved to local filesystem fallback
4. Once DB recovers, events auto-replay

**Security Threat Detected:**
1. Check `GET /api/monitoring/security/summary`
2. Review threat details in alert
3. Block IP if needed at network level
4. Document incident and response

**Memory Pressure:**
1. Check `GET /api/monitoring/memory`
2. Reduce `AuditLogging:MaxQueueSize` if queue is large
3. Force garbage collection if needed
4. Consider scaling horizontally
