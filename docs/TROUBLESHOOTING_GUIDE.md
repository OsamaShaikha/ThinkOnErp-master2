# Troubleshooting Guide - Full Traceability System

## Overview

This guide provides systematic troubleshooting procedures for common issues with the ThinkOnErp Full Traceability System. Use this guide alongside the [Operational Runbooks](OPERATIONAL_RUNBOOKS.md) for detailed resolution steps.

## Quick Diagnostic Commands

```bash
# Check API health
curl http://localhost:5000/health

# View recent logs
docker logs thinkonerp-api --tail=100

# Check metrics
curl http://localhost:5000/metrics

# Check container status
docker ps | grep thinkonerp

# Check container resources
docker stats thinkonerp-api --no-stream

# Test database connectivity
docker exec -it thinkonerp-api nc -zv oracle-host 1521
```

## Troubleshooting Decision Tree

```
API Not Responding
├─ Container Running?
│  ├─ No → Check Docker: docker ps -a
│  └─ Yes → Check Health Endpoint
│     ├─ Fails → Check Logs for Errors
│     └─ Success → Check Network/Firewall
│
Database Issues
├─ Connection Timeout?
│  ├─ Yes → Check Oracle Listener
│  └─ No → Check for Locks/Blocking
│
Performance Issues
├─ High Latency?
│  ├─ Check Queue Depth
│  ├─ Check Database Performance
│  └─ Check System Resources
│
Audit Logging Issues
├─ Events Not Logged?
│  ├─ Check Circuit Breaker State
│  ├─ Check Queue Status
│  └─ Check Fallback Files
```

## Common Issues and Solutions

### 1. API Not Starting

**Symptoms:**
- Container exits immediately
- Health endpoint not accessible
- Logs show startup errors

**Diagnostic Steps:**
```bash
# Check container status
docker ps -a | grep thinkonerp-api

# View startup logs
docker logs thinkonerp-api

# Check for configuration errors
docker logs thinkonerp-api | grep -i "error\|exception\|fatal"
```

**Common Causes:**

#### Missing Environment Variables
```bash
# Check required variables
docker exec -it thinkonerp-api printenv | grep -E "ORACLE|JWT"

# Solution: Update .env file
nano .env
docker-compose restart thinkonerp-api
```

#### Invalid Oracle Connection String
```bash
# Test connection string format
# Should be: Data Source=(DESCRIPTION=(ADDRESS=(PROTOCOL=TCP)(HOST=host)(PORT=1521))(CONNECT_DATA=(SERVICE_NAME=XE)));User Id=user;Password=pass;

# Test connectivity
docker exec -it thinkonerp-api telnet oracle-host 1521
```

#### Port Already in Use
```bash
# Find process using port 5000
sudo lsof -i :5000

# Kill process or change port
sudo kill -9 <PID>
# OR
# Edit .env: API_PORT=5001
```

#### Missing Dependencies
```bash
# Rebuild image
docker-compose build --no-cache thinkonerp-api
docker-compose up -d
```

---

### 2. Database Connection Failures

**Symptoms:**
- Logs show "ORA-" errors
- Connection timeout errors
- "TNS: could not resolve" errors

**Diagnostic Steps:**
```bash
# Test database connectivity
docker exec -it thinkonerp-api nc -zv oracle-host 1521

# Check Oracle listener
# On Oracle server:
lsnrctl status

# Test with sqlplus
docker exec -it thinkonerp-api sqlplus system/password@//oracle-host:1521/XE
```

**Common Causes:**

#### Oracle Not Running
```bash
# Check Oracle container
docker ps | grep oracle

# Start Oracle
docker-compose up -d oracle-db

# Wait for initialization (first start takes 5-10 minutes)
docker logs oracle-db -f
```

#### Network Issues
```bash
# Check network connectivity
docker network ls
docker network inspect <network-name>

# Verify containers are on same network
docker inspect thinkonerp-api | grep NetworkMode
docker inspect oracle-db | grep NetworkMode
```

#### Firewall Blocking Connection
```bash
# Check firewall rules
sudo ufw status

# Allow Oracle port
sudo ufw allow from <api-container-ip> to any port 1521
```

#### Wrong Service Name/SID
```bash
# Check Oracle service name
docker exec -it oracle-db sqlplus / as sysdba
SQL> SELECT name FROM v$database;
SQL> SELECT instance_name FROM v$instance;

# Update connection string with correct SERVICE_NAME or SID
```

---

### 3. Audit Events Not Being Logged

**Symptoms:**
- No entries in SYS_AUDIT_LOG table
- Audit queries return empty results
- Missing audit trail for operations

**Diagnostic Steps:**
```bash
# Check audit logger status
docker logs thinkonerp-api | grep "Audit" | tail -50

# Check circuit breaker state
curl http://localhost:5000/metrics | grep circuit_breaker_state

# Check queue depth
curl http://localhost:5000/metrics | grep audit_queue_depth

# Check for fallback files
docker exec -it thinkonerp-api ls -lh /app/audit-fallback/
```

**Common Causes:**

#### Circuit Breaker Open
```bash
# Check state
curl http://localhost:5000/metrics | grep circuit_breaker

# Wait for automatic recovery (30 seconds)
# OR restart API
docker-compose restart thinkonerp-api

# Check fallback files for queued events
docker exec -it thinkonerp-api ls /app/audit-fallback/
```

#### Audit Logging Disabled
```bash
# Check configuration
docker exec -it thinkonerp-api cat appsettings.json | grep -A 5 "AuditLogging"

# Verify Enabled: true
# If false, update configuration and restart
```

#### Database Write Failures
```bash
# Check for database errors
docker logs thinkonerp-api | grep "Failed to write audit batch"

# Check table space
sqlplus system/password@//localhost:1521/XE
SQL> SELECT tablespace_name, SUM(bytes)/1024/1024 AS mb_free
     FROM dba_free_space
     GROUP BY tablespace_name;

# Add space if needed
SQL> ALTER TABLESPACE USERS ADD DATAFILE 
     '/u01/app/oracle/oradata/XE/users02.dbf' SIZE 1G AUTOEXTEND ON;
```

#### Queue Overflow
```bash
# Check queue depth
curl http://localhost:5000/metrics | grep audit_queue_depth

# If > MaxQueueSize, events are being dropped
# Solution: See Runbook 1 (Audit Queue Depth Exceeding Threshold)
```

---

### 4. Slow API Performance

**Symptoms:**
- API requests taking >1 second
- Users reporting slow response times
- Timeout errors

**Diagnostic Steps:**
```bash
# Check current latency
curl http://localhost:5000/metrics | grep http_server_request_duration

# Identify slow endpoints
docker logs thinkonerp-api | grep "Request completed" | grep -E "[1-9][0-9]{3}ms"

# Check system resources
docker stats thinkonerp-api

# Check database performance
sqlplus system/password@//localhost:1521/XE
SQL> SELECT sql_text, elapsed_time/1000000 AS seconds
     FROM v$sql
     WHERE elapsed_time > 5000000
     ORDER BY elapsed_time DESC
     FETCH FIRST 10 ROWS ONLY;
```

**Common Causes:**

#### Slow Database Queries
```bash
# Check for missing indexes
SQL> SELECT * FROM dba_indexes WHERE table_name = 'SYS_AUDIT_LOG';

# Add recommended indexes (see Runbook 7)
SQL> CREATE INDEX IDX_AUDIT_LOG_DATE_RANGE 
     ON SYS_AUDIT_LOG(CREATION_DATE, COMPANY_ID);

# Update statistics
SQL> EXEC DBMS_STATS.GATHER_TABLE_STATS('SYSTEM', 'SYS_AUDIT_LOG');
```

#### High CPU Usage
```bash
# Check CPU usage
docker stats thinkonerp-api

# If >80%, consider:
# 1. Scale horizontally
docker-compose up -d --scale thinkonerp-api=2

# 2. Increase container CPU limit
# Edit docker-compose.yml: cpus: '2'
```

#### Memory Pressure
```bash
# Check memory usage
docker stats thinkonerp-api

# If >80%, check for memory leaks
docker logs thinkonerp-api | grep "GC" | tail -20

# Increase memory limit
# Edit docker-compose.yml: memory: 4G
docker-compose up -d
```

#### Connection Pool Exhaustion
```bash
# Check pool utilization
docker logs thinkonerp-api | grep "Connection pool"

# Increase pool size
# Edit appsettings.json: Max Pool Size=200
docker-compose restart thinkonerp-api
```

---

### 5. Authentication Failures

**Symptoms:**
- Login requests return 401 Unauthorized
- Valid credentials rejected
- JWT token validation fails

**Diagnostic Steps:**
```bash
# Test login endpoint
curl -X POST http://localhost:5000/api/auth/login \
  -H "Content-Type: application/json" \
  -d '{"userName":"admin","password":"Admin@123"}'

# Check authentication logs
docker logs thinkonerp-api | grep -i "auth\|login"

# Verify JWT configuration
docker exec -it thinkonerp-api printenv | grep JWT
```

**Common Causes:**

#### Wrong Credentials
```bash
# Verify user exists
sqlplus system/password@//localhost:1521/XE
SQL> SELECT USER_NAME, IS_ACTIVE FROM SYS_USERS WHERE USER_NAME = 'admin';

# Reset password if needed
SQL> EXEC PKG_SYS_USERS.UPDATE_USER(
       p_row_id => <user_id>,
       p_password => 'NewPassword123',
       ...
     );
```

#### JWT Secret Key Mismatch
```bash
# Check JWT secret is set
docker exec -it thinkonerp-api printenv | grep JWT_SECRET_KEY

# Verify it's at least 32 characters
# Update if needed in .env file
```

#### Token Expired
```bash
# Check token expiry time
# Default is 60 minutes

# Verify token claims
# Use jwt.io to decode token and check exp claim
```

#### User Account Locked/Inactive
```bash
# Check user status
SQL> SELECT USER_NAME, IS_ACTIVE, FORCE_PASSWORD_CHANGE 
     FROM SYS_USERS 
     WHERE USER_NAME = 'admin';

# Activate user if needed
SQL> UPDATE SYS_USERS SET IS_ACTIVE = 1 WHERE USER_NAME = 'admin';
```

---

### 6. Missing Correlation IDs

**Symptoms:**
- Logs missing correlation IDs
- Cannot trace requests through system
- Correlation ID header not in responses

**Diagnostic Steps:**
```bash
# Check if middleware is registered
docker logs thinkonerp-api | grep "RequestTracingMiddleware"

# Test correlation ID in response
curl -v http://localhost:5000/api/roles | grep -i "x-correlation-id"

# Check logs for correlation IDs
docker logs thinkonerp-api | grep "CorrelationId"
```

**Common Causes:**

#### Middleware Not Registered
```bash
# Check Program.cs has middleware registered
# Should have: app.UseRequestTracing();

# Verify middleware order (should be early in pipeline)
docker logs thinkonerp-api --tail=100 | grep "Middleware"
```

#### Configuration Disabled
```bash
# Check configuration
docker exec -it thinkonerp-api cat appsettings.json | grep -A 5 "RequestTracing"

# Verify Enabled: true
```

#### AsyncLocal Context Lost
```bash
# Check for async/await issues in code
# Correlation context uses AsyncLocal which requires proper async flow

# Verify all async methods use await
# Check for Task.Run() which can lose context
```

---

### 7. Sensitive Data Exposure

**Symptoms:**
- Passwords visible in audit logs
- Credit card numbers in logs
- Tokens not masked

**Diagnostic Steps:**
```bash
# Check audit logs for sensitive data
sqlplus system/password@//localhost:1521/XE
SQL> SELECT OLD_VALUE, NEW_VALUE 
     FROM SYS_AUDIT_LOG 
     WHERE ROWNUM <= 10;

# Check for unmasked fields
SQL> SELECT * FROM SYS_AUDIT_LOG 
     WHERE OLD_VALUE LIKE '%password%' 
     OR NEW_VALUE LIKE '%password%';
```

**Common Causes:**

#### Masking Not Configured
```bash
# Check sensitive fields configuration
docker exec -it thinkonerp-api cat appsettings.json | grep -A 10 "SensitiveFields"

# Should include: password, token, refreshToken, creditCard, ssn
# Add missing fields and restart
```

#### Field Name Variations
```bash
# Masking uses pattern matching
# Ensure all variations are covered:
# - password, Password, PASSWORD
# - token, Token, accessToken, refreshToken
# - creditCard, cardNumber, ccNumber

# Update configuration with all variations
```

#### Masking Disabled
```bash
# Check if masking is enabled
docker exec -it thinkonerp-api cat appsettings.json | grep "EnableSensitiveDataMasking"

# Should be: true
```

---

### 8. Compliance Report Generation Failures

**Symptoms:**
- Report generation times out
- Empty reports generated
- Report export fails

**Diagnostic Steps:**
```bash
# Test report generation
curl -X POST http://localhost:5000/api/compliance/gdpr-report \
  -H "Authorization: Bearer $TOKEN" \
  -H "Content-Type: application/json" \
  -d '{"dataSubjectId":1,"startDate":"2024-01-01","endDate":"2024-01-31"}'

# Check compliance service logs
docker logs thinkonerp-api | grep "Compliance"

# Check for timeout errors
docker logs thinkonerp-api | grep -i "timeout.*report"
```

**Common Causes:**

#### Large Date Range
```bash
# Reduce date range to < 90 days
# Use pagination for large datasets

# Check query performance
SQL> SELECT COUNT(*) FROM SYS_AUDIT_LOG 
     WHERE CREATION_DATE BETWEEN TO_DATE('2024-01-01', 'YYYY-MM-DD') 
     AND TO_DATE('2024-12-31', 'YYYY-MM-DD');

# If > 1 million records, use smaller ranges
```

#### Missing Indexes
```bash
# Check indexes exist
SQL> SELECT index_name FROM dba_indexes 
     WHERE table_name = 'SYS_AUDIT_LOG';

# Add missing indexes (see Runbook 7)
```

#### Insufficient Memory
```bash
# Check memory usage during report generation
docker stats thinkonerp-api

# Increase memory if needed
# Edit docker-compose.yml: memory: 4G
```

#### PDF Generation Failure
```bash
# Check QuestPDF library is installed
docker exec -it thinkonerp-api dotnet list package | grep QuestPDF

# Check for PDF generation errors
docker logs thinkonerp-api | grep -i "pdf\|questpdf"
```

---

### 9. Redis Cache Connection Issues

**Symptoms:**
- Cache misses for all queries
- Logs show Redis connection errors
- Performance degradation

**Diagnostic Steps:**
```bash
# Check Redis connectivity
docker exec -it thinkonerp-api nc -zv redis-host 6379

# Test Redis connection
docker exec -it redis-cli ping

# Check cache configuration
docker exec -it thinkonerp-api cat appsettings.json | grep -A 5 "Redis"
```

**Common Causes:**

#### Redis Not Running
```bash
# Check Redis container
docker ps | grep redis

# Start Redis
docker-compose up -d redis

# Verify Redis is ready
docker logs redis
```

#### Wrong Connection String
```bash
# Check Redis connection string
docker exec -it thinkonerp-api printenv | grep REDIS

# Should be: localhost:6379 or redis:6379
# Update if incorrect
```

#### Redis Out of Memory
```bash
# Check Redis memory usage
docker exec -it redis redis-cli INFO memory

# Check maxmemory setting
docker exec -it redis redis-cli CONFIG GET maxmemory

# Increase if needed
docker exec -it redis redis-cli CONFIG SET maxmemory 512mb
```

#### Cache Disabled
```bash
# Check if caching is enabled
docker exec -it thinkonerp-api cat appsettings.json | grep "Caching"

# Verify Enabled: true
```

---

### 10. Alert Notifications Not Received

**Symptoms:**
- No email alerts received
- Webhook calls failing
- SMS not delivered

**Diagnostic Steps:**
```bash
# Check alert service logs
docker logs thinkonerp-api | grep "Alert"

# Test email notification
curl -X POST http://localhost:5000/api/admin/alerts/test-email \
  -H "Authorization: Bearer $ADMIN_TOKEN" \
  -d '{"recipient":"admin@example.com"}'

# Check notification channel errors
docker logs thinkonerp-api | grep -E "Email.*failed|Webhook.*failed|SMS.*failed"
```

**Common Causes:**

#### SMTP Configuration Issues
```bash
# Check SMTP settings
docker exec -it thinkonerp-api printenv | grep SMTP

# Test SMTP connectivity
docker exec -it thinkonerp-api telnet smtp.gmail.com 587

# Common issues:
# - Wrong SMTP host/port
# - Invalid credentials
# - SSL/TLS not enabled
# - App password not used (for Gmail)
```

#### Rate Limiting
```bash
# Check if rate limit exceeded
docker logs thinkonerp-api | grep "rate limit"

# Gmail: 500 emails/day
# SendGrid: Based on plan
# Adjust rate limits in configuration
```

#### Webhook Endpoint Down
```bash
# Test webhook URL
curl -X POST https://your-webhook-url.com/alerts \
  -H "Content-Type: application/json" \
  -d '{"test":"message"}'

# Check for timeout/connection errors
# Verify webhook endpoint is accessible
```

#### Invalid Phone Numbers (SMS)
```bash
# Check Twilio configuration
docker exec -it thinkonerp-api printenv | grep TWILIO

# Verify phone numbers are in E.164 format: +1234567890
# Verify numbers are verified in Twilio console
```

---

## Performance Troubleshooting

### High Memory Usage

```bash
# Check memory usage
docker stats thinkonerp-api

# Identify memory-intensive operations
docker logs thinkonerp-api | grep "Batch size\|Query returned"

# Check for memory leaks
docker logs thinkonerp-api | grep "GC" | tail -50

# Solutions:
# 1. Reduce batch sizes
# 2. Implement pagination limits
# 3. Enable response streaming
# 4. Increase container memory
```

### High CPU Usage

```bash
# Check CPU usage
docker stats thinkonerp-api

# Identify CPU-intensive operations
docker logs thinkonerp-api | grep "Request completed" | sort -t'=' -k2 -n

# Solutions:
# 1. Scale horizontally
# 2. Optimize hot paths
# 3. Add caching
# 4. Increase container CPU limit
```

### Disk Space Issues

```bash
# Check disk usage
df -h

# Check Docker disk usage
docker system df

# Check log file sizes
du -sh /var/lib/docker/containers/*/*-json.log

# Solutions:
# 1. Enable log rotation
# 2. Clean up old containers/images
# 3. Archive old audit data
# 4. Add more disk space
```

---

## Database Troubleshooting

### Table Space Full

```sql
-- Check table space usage
SELECT tablespace_name, 
       ROUND(SUM(bytes)/1024/1024, 2) AS used_mb,
       ROUND(MAX(maxbytes)/1024/1024, 2) AS max_mb
FROM dba_data_files
GROUP BY tablespace_name;

-- Add data file
ALTER TABLESPACE USERS ADD DATAFILE 
'/u01/app/oracle/oradata/XE/users02.dbf' SIZE 1G AUTOEXTEND ON;

-- Enable auto-extend
ALTER DATABASE DATAFILE '/u01/app/oracle/oradata/XE/users01.dbf' 
AUTOEXTEND ON NEXT 100M MAXSIZE UNLIMITED;
```

### Locked Tables

```sql
-- Find locked objects
SELECT l.session_id, l.oracle_username, l.os_user_name,
       o.object_name, l.locked_mode
FROM v$locked_object l
JOIN dba_objects o ON l.object_id = o.object_id;

-- Find blocking sessions
SELECT blocking_session, sid, serial#, wait_class
FROM v$session
WHERE blocking_session IS NOT NULL;

-- Kill blocking session
ALTER SYSTEM KILL SESSION 'sid,serial#' IMMEDIATE;
```

### Slow Queries

```sql
-- Find slow queries
SELECT sql_id, sql_text, 
       executions,
       ROUND(elapsed_time/1000000, 2) AS total_seconds,
       ROUND(elapsed_time/executions/1000, 2) AS avg_ms
FROM v$sql
WHERE executions > 0
ORDER BY elapsed_time/executions DESC
FETCH FIRST 10 ROWS ONLY;

-- Get execution plan
SELECT * FROM TABLE(DBMS_XPLAN.DISPLAY_CURSOR('<sql_id>', NULL, 'ALLSTATS LAST'));

-- Update statistics
EXEC DBMS_STATS.GATHER_TABLE_STATS('SYSTEM', 'SYS_AUDIT_LOG', CASCADE => TRUE);
```

---

## Log Analysis

### Finding Errors

```bash
# Recent errors
docker logs thinkonerp-api --since 1h | grep -i error

# Count errors by type
docker logs thinkonerp-api | grep -i error | \
  awk -F': ' '{print $NF}' | sort | uniq -c | sort -rn

# Errors with context (5 lines before/after)
docker logs thinkonerp-api | grep -B 5 -A 5 -i "error"
```

### Finding Slow Requests

```bash
# Requests taking >1 second
docker logs thinkonerp-api | grep "Request completed" | \
  grep -E "[1-9][0-9]{3}ms|[0-9]{5}ms"

# Group by endpoint
docker logs thinkonerp-api | grep "Request completed" | \
  awk '{print $5, $NF}' | sort | uniq -c | sort -rn
```

### Tracking Specific Request

```bash
# Find correlation ID
docker logs thinkonerp-api | grep "Request started" | tail -1

# Track request through logs
docker logs thinkonerp-api | grep "<correlation-id>"

# Get all events for correlation ID
sqlplus system/password@//localhost:1521/XE
SQL> SELECT * FROM SYS_AUDIT_LOG 
     WHERE CORRELATION_ID = '<correlation-id>'
     ORDER BY CREATION_DATE;
```

---

## Health Check Procedures

### Daily Health Check

```bash
#!/bin/bash
# daily-health-check.sh

echo "=== API Health ==="
curl -s http://localhost:5000/health | jq

echo -e "\n=== Container Status ==="
docker ps | grep thinkonerp

echo -e "\n=== Resource Usage ==="
docker stats thinkonerp-api --no-stream

echo -e "\n=== Recent Errors ==="
docker logs thinkonerp-api --since 24h | grep -i error | wc -l

echo -e "\n=== Queue Depth ==="
curl -s http://localhost:5000/metrics | grep audit_queue_depth

echo -e "\n=== Circuit Breaker State ==="
curl -s http://localhost:5000/metrics | grep circuit_breaker_state

echo -e "\n=== Database Connectivity ==="
docker exec -it thinkonerp-api nc -zv oracle-host 1521 && echo "OK" || echo "FAILED"
```

### Weekly Health Check

```bash
#!/bin/bash
# weekly-health-check.sh

echo "=== Disk Space ==="
df -h

echo -e "\n=== Docker Disk Usage ==="
docker system df

echo -e "\n=== Table Sizes ==="
sqlplus -s system/password@//localhost:1521/XE <<EOF
SELECT segment_name, ROUND(bytes/1024/1024, 2) AS size_mb
FROM dba_segments
WHERE segment_name IN ('SYS_AUDIT_LOG', 'SYS_AUDIT_LOG_ARCHIVE')
ORDER BY bytes DESC;
EOF

echo -e "\n=== Index Fragmentation ==="
sqlplus -s system/password@//localhost:1521/XE <<EOF
SELECT index_name, blevel, leaf_blocks
FROM dba_indexes
WHERE table_name = 'SYS_AUDIT_LOG';
EOF

echo -e "\n=== Performance Metrics ==="
curl -s http://localhost:5000/metrics | grep -E "http_server_request_duration|audit"
```

---

## Emergency Procedures

### Complete System Restart

```bash
# 1. Stop all services
docker-compose down

# 2. Check for orphaned containers
docker ps -a | grep thinkonerp

# 3. Clean up if needed
docker system prune -f

# 4. Start services
docker-compose up -d

# 5. Verify startup
docker logs thinkonerp-api -f

# 6. Test health
curl http://localhost:5000/health
```

### Database Recovery

```bash
# 1. Stop API
docker-compose stop thinkonerp-api

# 2. Backup database
docker exec oracle-db sh -c 'exp system/password@XE file=/tmp/backup.dmp full=y'
docker cp oracle-db:/tmp/backup.dmp ./backup-$(date +%Y%m%d).dmp

# 3. Restart Oracle
docker-compose restart oracle-db

# 4. Wait for Oracle to be ready
docker logs oracle-db -f

# 5. Restart API
docker-compose start thinkonerp-api
```

### Rollback Deployment

```bash
# 1. Stop current version
docker-compose down

# 2. Checkout previous version
git checkout <previous-commit>

# 3. Rebuild
docker-compose build --no-cache

# 4. Start services
docker-compose up -d

# 5. Verify
curl http://localhost:5000/health
```

---

## Getting Help

### Information to Collect

When reporting issues, collect:

1. **System Information**
```bash
docker --version
docker-compose --version
uname -a
```

2. **Container Logs**
```bash
docker logs thinkonerp-api --tail=500 > api-logs.txt
docker logs oracle-db --tail=500 > db-logs.txt
```

3. **Configuration**
```bash
docker-compose config > docker-config.txt
docker exec -it thinkonerp-api cat appsettings.json > appsettings.txt
```

4. **Metrics**
```bash
curl http://localhost:5000/metrics > metrics.txt
curl http://localhost:5000/health > health.txt
```

5. **Database State**
```sql
-- Save to file
spool db-state.txt
SELECT * FROM v$version;
SELECT * FROM v$instance;
SELECT tablespace_name, SUM(bytes)/1024/1024 AS mb 
FROM dba_data_files GROUP BY tablespace_name;
SELECT COUNT(*) FROM SYS_AUDIT_LOG;
spool off
```

### Support Contacts

- **Operations Team**: ops@thinkonerp.com
- **Database Team**: dba@thinkonerp.com
- **Development Team**: dev@thinkonerp.com
- **Security Team**: security@thinkonerp.com
- **On-Call**: +1-555-0100

### Related Documentation

- [Operational Runbooks](OPERATIONAL_RUNBOOKS.md) - Detailed resolution procedures
- [APM Configuration Guide](APM_CONFIGURATION_GUIDE.md) - Monitoring setup
- [Deployment Guide](../DEPLOYMENT.md) - Deployment procedures
- [Database Scripts](../Database/Scripts/) - Database maintenance scripts

---

## Document Maintenance

- **Last Updated**: 2024-01-01
- **Version**: 1.0
- **Owner**: Operations Team
- **Review Frequency**: Quarterly

