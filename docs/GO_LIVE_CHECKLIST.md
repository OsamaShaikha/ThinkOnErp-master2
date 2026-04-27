# Go-Live Checklist and Rollback Plan

## Pre-Deployment Checklist

### Infrastructure
- [ ] Oracle Database accessible from production environment
- [ ] Database migration scripts executed and verified
- [ ] Redis instance configured (if caching enabled)
- [ ] SMTP server configured (if email alerts enabled)
- [ ] External storage configured (S3/Azure for archival, if enabled)
- [ ] SSL/TLS certificates installed

### Database
- [ ] All migration scripts executed in order (Database/Scripts/)
- [ ] Indexes created and verified
- [ ] Table partitioning configured for SYS_AUDIT_LOG
- [ ] Seed data loaded (roles, default admin user)
- [ ] Stored procedures compiled without errors
- [ ] Rollback scripts tested (Database/Rollback/)

### Configuration
- [ ] `appsettings.Production.json` reviewed and customized
- [ ] JWT secret key set via environment variable (not in config file)
- [ ] Database connection string configured
- [ ] Default admin password changed from seed data defaults
- [ ] SuperAdmin password changed
- [ ] Audit logging enabled
- [ ] Security monitoring enabled
- [ ] All sensitive fields listed in masking configuration
- [ ] Alert notification channels configured and tested
- [ ] Key management storage configured

### Security
- [ ] HTTPS enforced
- [ ] CORS origins restricted to known domains
- [ ] Swagger UI disabled or protected in production
- [ ] Default credentials removed/changed
- [ ] Audit data encryption enabled
- [ ] Audit log integrity signing enabled
- [ ] Rate limiting configured

### Testing
- [ ] All unit tests pass (`dotnet test`)
- [ ] API endpoints accessible via Swagger
- [ ] Authentication flow working (login → access token → protected endpoint)
- [ ] Audit logs being captured
- [ ] Alert notifications being delivered
- [ ] Compliance reports generating correctly
- [ ] Performance acceptable (<10ms overhead, <50ms audit write)

### Monitoring
- [ ] Health check endpoint responding (`/health`)
- [ ] Prometheus metrics scraping configured
- [ ] Grafana dashboards imported
- [ ] Alert rules created for critical metrics
- [ ] Log aggregation configured (Serilog file/console output)
- [ ] Connection pool monitoring active

---

## Deployment Steps

1. **Backup** existing database and configuration
2. **Execute** database migration scripts
3. **Deploy** application (Docker or direct)
4. **Verify** health check: `GET /health`
5. **Verify** authentication: `POST /api/auth/login`
6. **Verify** audit logging: check `GET /api/auditlogs`
7. **Verify** monitoring: check `GET /api/monitoring/health`
8. **Verify** alerts: trigger test alert
9. **Announce** deployment completion

---

## Rollback Plan

### Trigger Conditions
- Application fails to start
- Health check fails after 5 minutes
- Critical errors in logs
- Audit logging not functioning
- Authentication broken

### Rollback Steps

1. **Stop** the new deployment
2. **Execute** database rollback scripts (`Database/Rollback/`)
3. **Restore** previous application version
4. **Restore** previous `appsettings.json`
5. **Verify** health check on rolled-back version
6. **Verify** core functionality (login, CRUD operations)
7. **Document** the failure reason
8. **Communicate** rollback to stakeholders

### Rollback Scripts Location
```
Database/Rollback/
├── 01_Rollback_Audit_Tables.sql
├── 02_Rollback_Indexes.sql
├── ...
└── 11_Rollback_Complete.sql
```

---

## Post-Deployment Validation

- [ ] Monitor error rate for first 30 minutes
- [ ] Verify audit log entries appearing for real traffic
- [ ] Check security monitoring for false positives
- [ ] Verify scheduled tasks running (archival, metrics aggregation)
- [ ] Confirm alert notifications reaching recipients
- [ ] Review application logs for warnings or errors
