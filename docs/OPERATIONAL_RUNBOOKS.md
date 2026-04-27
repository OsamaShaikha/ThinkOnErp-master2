# Operational Runbooks - Full Traceability System

## Overview

This document provides step-by-step procedures for diagnosing and resolving common operational issues with the ThinkOnErp Full Traceability System. Each runbook includes symptoms, diagnosis steps, resolution procedures, and prevention strategies.

## Table of Contents

1. [Audit Queue Depth Exceeding Threshold](#runbook-1-audit-queue-depth-exceeding-threshold)
2. [Audit Write Failures](#runbook-2-audit-write-failures)
3. [High API Latency](#runbook-3-high-api-latency)
4. [Database Connection Pool Exhaustion](#runbook-4-database-connection-pool-exhaustion)
5. [Circuit Breaker Open State](#runbook-5-circuit-breaker-open-state)
6. [Failed Login Attack Detection](#runbook-6-failed-login-attack-detection)
7. [Slow Query Performance](#runbook-7-slow-query-performance)
8. [Memory Pressure and OOM](#runbook-8-memory-pressure-and-oom)
9. [Archival Service Failures](#runbook-9-archival-service-failures)
10. [Alert Notification Failures](#runbook-10-alert-notification-failures)
11. [Redis Cache Connectivity Issues](#runbook-11-redis-cache-connectivity-issues)
12. [Background Service Health Issues](#runbook-12-background-service-health-issues)

---

## Runbook 1: Audit Queue Depth Exceeding Threshold

### Symptoms
- Alert: "Audit queue depth > 5000 events"
- API response times increasing
- Memory usage climbing
- Logs showing: "Audit queue backpressure applied"

### Impact
- **Severity**: High
- **User Impact**: Potential API slowdown, audit data loss if queue overflows
- **Business Impact**: Compliance risk if audit events are dropped

### Diagnosis Steps

1. **Check current queue depth**:
```bash
# View metrics endpoint
curl http://localhost:5000/metrics | grep audit_queue_depth

# Check application logs
docker logs thinkonerp-api | grep "Queue depth"
```

2. **Identify the bottleneck**:
```bash
# Check database write performance
docker logs thinkonerp-api | grep "Batch write completed" | tail -20

# Look for slow batch writes (>500ms)
docker logs thinkonerp-api | grep "Batch write took" | grep -E "[5-9][0-9]{2}ms|[0-9]{4}ms"
```

3. **Check database health**:
```sql
-- Connect to Oracle
sqlplus system/password@//localhost:1521/XE

-- Check for locks on SYS_AUDIT_LOG
SELECT * FROM v$locked_object WHERE object_id = (
  SELECT object_id FROM dba_objects WHERE object_name = 'SYS_AUDIT_LOG'
);

-- Check table space
SELECT tablespace_name, SUM(bytes)/1024/1024 AS mb_free
FROM dba_free_space
GROUP BY tablespace_name;
```

4. **Review system resources**:
```bash
# Check container resources
docker stats thinkonerp-api

# Check disk I/O
iostat -x 1 5
```

### Resolution Steps

#### Immediate Actions (< 5 minutes)

1. **Increase batch processing frequency** (temporary):
```bash
# Edit appsettings.json or set environment variable
docker exec -it thinkonerp-api bash
export AuditLogging__BatchWindowMs=50  # Reduce from 100ms to 50ms
```

2. **Restart the API** (if queue is critically full):
```bash
docker-compose restart thinkonerp-api
```

3. **Monitor queue recovery**:
```bash
# Watch queue depth decrease
watch -n 5 'curl -s http://localhost:5000/metrics | grep audit_queue_depth'
```

#### Short-term Actions (< 30 minutes)

1. **Optimize database writes**:
```sql
-- Rebuild indexes if fragmented
ALTER INDEX IDX_AUDIT_LOG_CORRELATION REBUILD ONLINE;
ALTER INDEX IDX_AUDIT_LOG_COMPANY_DATE REBUILD ONLINE;

-- Update statistics
EXEC DBMS_STATS.GATHER_TABLE_STATS('SYSTEM', 'SYS_AUDIT_LOG');
```

2. **Increase batch size** (if database can handle it):
```json
// Update appsettings.Production.json
{
  "AuditLogging": {
    "BatchSize": 100,  // Increase from 50
    "BatchWindowMs": 100
  }
}
```

3. **Scale horizontally** (if using load balancer):
```bash
# Add another API instance
docker-compose up -d --scale thinkonerp-api=2
```

#### Long-term Actions (< 24 hours)

1. **Implement table partitioning**:
```sql
-- Partition SYS_AUDIT_LOG by month
ALTER TABLE SYS_AUDIT_LOG MODIFY
PARTITION BY RANGE (CREATION_DATE)
INTERVAL (NUMTOYMINTERVAL(1, 'MONTH'))
(
  PARTITION p_initial VALUES LESS THAN (TO_DATE('2024-01-01', 'YYYY-MM-DD'))
);
```

2. **Increase database resources**:
   - Add more CPU/memory to database server
   - Use faster storage (SSD/NVMe)
   - Optimize Oracle SGA/PGA settings

3. **Review audit logging configuration**:
```json
{
  "AuditLogging": {
    "MaxQueueSize": 15000,  // Increase from 10000
    "BatchSize": 100,
    "BatchWindowMs": 100
  }
}
```

### Prevention

1. **Set up monitoring alerts**:
   - Alert when queue depth > 3000 (warning)
   - Alert when queue depth > 5000 (critical)
   - Alert when batch write time > 200ms

2. **Regular maintenance**:
   - Weekly index rebuilds
   - Monthly statistics updates
   - Quarterly table space reviews

3. **Capacity planning**:
   - Monitor growth trends
   - Plan for 3x peak load capacity
   - Test with load testing tools

### Verification

```bash
# Verify queue is draining
curl http://localhost:5000/metrics | grep audit_queue_depth

# Check batch write times are normal (<100ms)
docker logs thinkonerp-api | grep "Batch write completed" | tail -10

# Verify no errors
docker logs thinkonerp-api | grep -i error | tail -20
```

### Escalation

- **Level 1**: Operations team (queue depth 3000-5000)
- **Level 2**: Database team (queue depth > 5000, database issues)
- **Level 3**: Development team (code optimization needed)

---

## Runbook 2: Audit Write Failures

### Symptoms
- Alert: "Audit write failure rate > 5%"
- Logs showing: "Failed to write audit batch"
- Circuit breaker opening
- Audit data gaps in queries

### Impact
- **Severity**: Critical
- **User Impact**: None (audit logging is non-blocking)
- **Business Impact**: Compliance risk, audit trail gaps

### Diagnosis Steps

1. **Check error logs**:
```bash
# View recent audit write errors
docker logs thinkonerp-api | grep "Failed to write audit batch" | tail -50

# Check for specific error types
docker logs thinkonerp-api | grep -E "ORA-|timeout|connection"
```

2. **Identify error patterns**:
```bash
# Count error types
docker logs thinkonerp-api | grep "Failed to write audit batch" | \
  awk -F': ' '{print $NF}' | sort | uniq -c | sort -rn
```

3. **Check circuit breaker state**:
```bash
curl http://localhost:5000/metrics | grep circuit_breaker_state
```

4. **Test database connectivity**:
```bash
# From API container
docker exec -it thinkonerp-api bash
nc -zv oracle-host 1521

# Test with sqlplus
sqlplus system/password@//oracle-host:1521/XE
```

### Resolution Steps

#### Database Connection Errors

1. **Verify connection string**:
```bash
# Check environment variable
docker exec -it thinkonerp-api printenv | grep ORACLE_CONNECTION_STRING
```

2. **Test connection from container**:
```bash
docker exec -it thinkonerp-api bash
telnet oracle-host 1521
```

3. **Check Oracle listener**:
```bash
# On Oracle server
lsnrctl status
```

4. **Restart Oracle listener** (if needed):
```bash
lsnrctl stop
lsnrctl start
```

#### Timeout Errors

1. **Increase command timeout**:
```json
// appsettings.json
{
  "ConnectionStrings": {
    "OracleConnection": "...;Connection Timeout=60;"  // Increase from 30
  }
}
```

2. **Check for long-running queries**:
```sql
SELECT sql_text, elapsed_time/1000000 AS elapsed_seconds
FROM v$sql
WHERE elapsed_time > 5000000  -- > 5 seconds
ORDER BY elapsed_time DESC;
```

3. **Kill blocking sessions**:
```sql
-- Find blocking sessions
SELECT blocking_session, sid, serial#, wait_class, seconds_in_wait
FROM v$session
WHERE blocking_session IS NOT NULL;

-- Kill session (if necessary)
ALTER SYSTEM KILL SESSION 'sid,serial#' IMMEDIATE;
```

#### Table Space Full

1. **Check table space usage**:
```sql
SELECT tablespace_name, 
       ROUND(SUM(bytes)/1024/1024, 2) AS used_mb,
       ROUND(MAX(bytes)/1024/1024, 2) AS max_mb
FROM dba_data_files
GROUP BY tablespace_name;
```

2. **Add data file**:
```sql
ALTER TABLESPACE USERS ADD DATAFILE 
'/u01/app/oracle/oradata/XE/users02.dbf' SIZE 1G AUTOEXTEND ON;
```

3. **Enable auto-extend**:
```sql
ALTER DATABASE DATAFILE '/u01/app/oracle/oradata/XE/users01.dbf' 
AUTOEXTEND ON NEXT 100M MAXSIZE UNLIMITED;
```

#### Circuit Breaker Open

1. **Check circuit breaker metrics**:
```bash
curl http://localhost:5000/metrics | grep circuit_breaker
```

2. **Wait for half-open state** (automatic after 30 seconds)

3. **Manually reset** (if needed):
```bash
# Restart API to reset circuit breaker
docker-compose restart thinkonerp-api
```

### Prevention

1. **Database monitoring**:
   - Monitor table space usage (alert at 80%)
   - Monitor connection pool utilization
   - Set up Oracle Enterprise Manager alerts

2. **Connection pool tuning**:
```json
{
  "ConnectionStrings": {
    "OracleConnection": "...;Min Pool Size=10;Max Pool Size=100;Connection Lifetime=300;"
  }
}
```

3. **Implement fallback logging**:
   - File system fallback already implemented
   - Verify fallback directory has space
   - Set up fallback file monitoring

### Verification

```bash
# Verify writes are succeeding
docker logs thinkonerp-api | grep "Batch write completed" | tail -10

# Check circuit breaker is closed
curl http://localhost:5000/metrics | grep circuit_breaker_state

# Verify no recent errors
docker logs thinkonerp-api --since 5m | grep -i error
```

### Escalation

- **Level 1**: Operations team (connection issues)
- **Level 2**: Database team (database errors, performance)
- **Level 3**: Development team (circuit breaker tuning)

---

## Runbook 3: High API Latency

### Symptoms
- Alert: "p99 latency > 1000ms"
- Users reporting slow API responses
- Metrics showing increased response times
- Logs showing slow requests

### Impact
- **Severity**: High
- **User Impact**: Poor user experience, timeouts
- **Business Impact**: Reduced productivity, potential revenue loss

### Diagnosis Steps

1. **Check current latency metrics**:
```bash
# View p50, p95, p99 latencies
curl http://localhost:5000/metrics | grep http_server_request_duration

# Check slow requests
docker logs thinkonerp-api | grep "Request completed" | grep -E "[1-9][0-9]{3}ms"
```

2. **Identify slow endpoints**:
```bash
# Group by endpoint
docker logs thinkonerp-api | grep "Request completed" | \
  awk '{print $5, $NF}' | sort | uniq -c | sort -rn | head -20
```

3. **Check for slow database queries**:
```bash
docker logs thinkonerp-api | grep "Query execution time" | grep -E "[5-9][0-9]{2}ms|[0-9]{4}ms"
```

4. **Review system resources**:
```bash
# CPU and memory
docker stats thinkonerp-api

# Database connections
docker logs thinkonerp-api | grep "Connection pool"
```

### Resolution Steps

#### Slow Database Queries

1. **Identify slow queries**:
```sql
-- Top 10 slowest queries
SELECT sql_text, 
       executions,
       ROUND(elapsed_time/1000000, 2) AS elapsed_seconds,
       ROUND(elapsed_time/executions/1000, 2) AS avg_ms
FROM v$sql
WHERE executions > 0
ORDER BY elapsed_time/executions DESC
FETCH FIRST 10 ROWS ONLY;
```

2. **Add missing indexes**:
```sql
-- Check for missing indexes
SELECT * FROM dba_indexes WHERE table_name = 'SYS_AUDIT_LOG';

-- Add index if needed
CREATE INDEX IDX_AUDIT_LOG_CUSTOM ON SYS_AUDIT_LOG(column_name);
```

3. **Update statistics**:
```sql
EXEC DBMS_STATS.GATHER_TABLE_STATS('SYSTEM', 'SYS_AUDIT_LOG', CASCADE => TRUE);
```

#### High CPU Usage

1. **Identify CPU-intensive operations**:
```bash
# Check container CPU
docker stats thinkonerp-api --no-stream

# Profile application (if profiling enabled)
dotnet-trace collect --process-id $(docker exec thinkonerp-api pidof dotnet)
```

2. **Scale horizontally**:
```bash
# Add more API instances
docker-compose up -d --scale thinkonerp-api=3
```

3. **Optimize hot paths**:
   - Review audit logging batch processing
   - Check sensitive data masking performance
   - Optimize JSON serialization

#### Memory Pressure

1. **Check memory usage**:
```bash
docker stats thinkonerp-api --no-stream
```

2. **Increase container memory**:
```yaml
# docker-compose.yml
services:
  thinkonerp-api:
    deploy:
      resources:
        limits:
          memory: 4G  # Increase from 2G
```

3. **Force garbage collection** (temporary):
```bash
# Restart API
docker-compose restart thinkonerp-api
```

#### Connection Pool Exhaustion

1. **Check pool metrics**:
```bash
docker logs thinkonerp-api | grep "Connection pool" | tail -20
```

2. **Increase pool size**:
```json
{
  "ConnectionStrings": {
    "OracleConnection": "...;Max Pool Size=200;"  // Increase from 100
  }
}
```

3. **Check for connection leaks**:
```sql
SELECT username, COUNT(*) AS connection_count
FROM v$session
WHERE username IS NOT NULL
GROUP BY username
ORDER BY connection_count DESC;
```

### Prevention

1. **Performance monitoring**:
   - Set up APM dashboards (see APM_CONFIGURATION_GUIDE.md)
   - Monitor p95/p99 latencies
   - Track slow query trends

2. **Regular optimization**:
   - Weekly index maintenance
   - Monthly query performance review
   - Quarterly load testing

3. **Caching strategy**:
   - Enable Redis caching for audit queries
   - Cache compliance reports
   - Implement response caching for read-heavy endpoints

### Verification

```bash
# Check latency improved
curl http://localhost:5000/metrics | grep http_server_request_duration

# Verify no slow requests
docker logs thinkonerp-api --since 5m | grep "Request completed" | grep -E "[1-9][0-9]{3}ms"

# Test endpoint response time
time curl http://localhost:5000/api/auditlogs?pageSize=10
```

### Escalation

- **Level 1**: Operations team (resource issues)
- **Level 2**: Database team (query optimization)
- **Level 3**: Development team (code optimization)

---

## Runbook 4: Database Connection Pool Exhaustion

### Symptoms
- Alert: "Connection pool utilization > 90%"
- Logs showing: "Timeout expired. The timeout period elapsed prior to obtaining a connection"
- API requests timing out
- Increased error rate

### Impact
- **Severity**: Critical
- **User Impact**: API unavailable, request failures
- **Business Impact**: Service outage

### Diagnosis Steps

1. **Check connection pool metrics**:
```bash
docker logs thinkonerp-api | grep "Connection pool" | tail -50
```

2. **Check active database sessions**:
```sql
SELECT username, program, COUNT(*) AS session_count
FROM v$session
WHERE username = 'SYSTEM'  -- Your app user
GROUP BY username, program
ORDER BY session_count DESC;
```

3. **Identify long-running queries**:
```sql
SELECT sid, serial#, username, status, 
       ROUND(last_call_et/60, 2) AS minutes_running,
       sql_id
FROM v$session
WHERE username = 'SYSTEM'
  AND status = 'ACTIVE'
  AND last_call_et > 300  -- > 5 minutes
ORDER BY last_call_et DESC;
```

4. **Check for connection leaks**:
```bash
# Look for unclosed connections in logs
docker logs thinkonerp-api | grep -i "connection.*not.*closed"
```

### Resolution Steps

#### Immediate Actions

1. **Kill long-running sessions**:
```sql
-- Identify sessions to kill
SELECT 'ALTER SYSTEM KILL SESSION ''' || sid || ',' || serial# || ''' IMMEDIATE;' AS kill_command
FROM v$session
WHERE username = 'SYSTEM'
  AND status = 'ACTIVE'
  AND last_call_et > 600;  -- > 10 minutes

-- Execute the kill commands
ALTER SYSTEM KILL SESSION 'sid,serial#' IMMEDIATE;
```

2. **Restart API** (if critical):
```bash
docker-compose restart thinkonerp-api
```

3. **Increase pool size temporarily**:
```bash
# Set environment variable
docker exec -it thinkonerp-api bash
export ConnectionStrings__OracleConnection="...;Max Pool Size=200;"
```

#### Short-term Actions

1. **Update connection string**:
```json
{
  "ConnectionStrings": {
    "OracleConnection": "Data Source=...;Max Pool Size=200;Min Pool Size=20;Connection Lifetime=300;Incr Pool Size=5;Decr Pool Size=2;"
  }
}
```

2. **Enable connection pooling metrics**:
```csharp
// Add to Program.cs
builder.Services.AddHealthChecks()
    .AddOracle(connectionString, name: "oracle", tags: new[] { "db", "oracle" });
```

3. **Review code for connection leaks**:
```bash
# Search for potential leaks
grep -r "new OracleConnection" src/
grep -r "CreateConnection" src/
```

#### Long-term Actions

1. **Implement connection pooling best practices**:
   - Always use `using` statements
   - Implement connection retry logic
   - Set appropriate timeouts

2. **Add connection pool monitoring**:
```csharp
// Custom health check
public class ConnectionPoolHealthCheck : IHealthCheck
{
    public Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context)
    {
        var poolSize = GetCurrentPoolSize();
        var maxPoolSize = GetMaxPoolSize();
        var utilization = (double)poolSize / maxPoolSize;
        
        if (utilization > 0.9)
            return Task.FromResult(HealthCheckResult.Unhealthy($"Pool utilization: {utilization:P}"));
        if (utilization > 0.7)
            return Task.FromResult(HealthCheckResult.Degraded($"Pool utilization: {utilization:P}"));
        
        return Task.FromResult(HealthCheckResult.Healthy($"Pool utilization: {utilization:P}"));
    }
}
```

3. **Implement circuit breaker for database calls**:
   - Already implemented in AuditLogger
   - Extend to other database operations

### Prevention

1. **Code review checklist**:
   - All database connections use `using` statements
   - No connections stored in class fields
   - Async methods properly awaited

2. **Monitoring and alerts**:
   - Alert when pool utilization > 70%
   - Track connection lifetime metrics
   - Monitor for connection leaks

3. **Load testing**:
   - Test with expected peak load
   - Verify connection pool sizing
   - Test connection recovery

### Verification

```bash
# Check pool utilization is normal
docker logs thinkonerp-api | grep "Connection pool" | tail -10

# Verify active sessions are reasonable
sqlplus system/password@//localhost:1521/XE
SELECT COUNT(*) FROM v$session WHERE username = 'SYSTEM';

# Test API is responding
curl http://localhost:5000/health
```

### Escalation

- **Level 1**: Operations team (restart, temporary fixes)
- **Level 2**: Database team (session management, Oracle tuning)
- **Level 3**: Development team (connection leak fixes)

---

## Runbook 5: Circuit Breaker Open State

### Symptoms
- Alert: "Circuit breaker opened for audit logging"
- Logs showing: "Circuit breaker is open, rejecting request"
- Audit events being written to fallback file system
- Metrics showing circuit breaker state = OPEN

### Impact
- **Severity**: High
- **User Impact**: None (audit logging is non-blocking)
- **Business Impact**: Audit data temporarily stored in files, compliance risk

### Diagnosis Steps

1. **Check circuit breaker state**:
```bash
curl http://localhost:5000/metrics | grep circuit_breaker_state
```

2. **Review recent failures**:
```bash
docker logs thinkonerp-api | grep "Circuit breaker" | tail -50
```

3. **Check underlying cause**:
```bash
# Look for database errors
docker logs thinkonerp-api | grep -E "ORA-|database.*error" | tail -20

# Check for timeouts
docker logs thinkonerp-api | grep -i timeout | tail -20
```

4. **Verify fallback is working**:
```bash
# Check fallback directory
docker exec -it thinkonerp-api ls -lh /app/audit-fallback/

# View fallback files
docker exec -it thinkonerp-api tail /app/audit-fallback/audit-*.json
```

### Resolution Steps

#### Automatic Recovery

1. **Wait for half-open state** (30 seconds by default):
```bash
# Monitor state transition
watch -n 5 'curl -s http://localhost:5000/metrics | grep circuit_breaker_state'
```

2. **Circuit breaker will test connection**:
   - If successful: transitions to CLOSED
   - If failed: returns to OPEN for another 30 seconds

#### Manual Intervention

1. **Fix underlying database issue** (see Runbook 2)

2. **Restart API to reset circuit breaker**:
```bash
docker-compose restart thinkonerp-api
```

3. **Replay fallback events**:
```bash
# Check fallback files
docker exec -it thinkonerp-api ls /app/audit-fallback/

# Replay events (if replay mechanism implemented)
docker exec -it thinkonerp-api dotnet ThinkOnErp.API.dll --replay-audit-fallback
```

#### Configuration Tuning

1. **Adjust circuit breaker thresholds**:
```json
{
  "AuditLogging": {
    "CircuitBreaker": {
      "FailureThreshold": 10,  // Increase from 5
      "SuccessThreshold": 3,
      "Timeout": 60  // Increase from 30 seconds
    }
  }
}
```

2. **Increase retry attempts**:
```json
{
  "AuditLogging": {
    "RetryPolicy": {
      "MaxRetries": 5,  // Increase from 3
      "DelayMs": 1000
    }
  }
}
```

### Prevention

1. **Database reliability**:
   - Implement database high availability
   - Regular database maintenance
   - Monitor database health proactively

2. **Circuit breaker tuning**:
   - Adjust thresholds based on observed patterns
   - Test circuit breaker behavior under load
   - Document expected recovery times

3. **Fallback monitoring**:
   - Alert when fallback is activated
   - Monitor fallback file size
   - Implement automatic replay mechanism

### Verification

```bash
# Verify circuit breaker is closed
curl http://localhost:5000/metrics | grep circuit_breaker_state

# Check audit writes are succeeding
docker logs thinkonerp-api | grep "Batch write completed" | tail -10

# Verify no fallback files being created
docker exec -it thinkonerp-api ls -lt /app/audit-fallback/ | head -5
```

### Escalation

- **Level 1**: Operations team (monitoring, basic troubleshooting)
- **Level 2**: Database team (database issues)
- **Level 3**: Development team (circuit breaker tuning, replay mechanism)

---


## Runbook 6: Failed Login Attack Detection

### Symptoms
- Alert: "Failed login attempts > 10 per minute from IP"
- Security monitor flagging suspicious IPs
- Logs showing repeated failed login attempts
- Potential brute force attack

### Impact
- **Severity**: High
- **User Impact**: Legitimate users may be blocked if using same IP
- **Business Impact**: Security breach risk, potential account compromise

### Diagnosis Steps

1. **Check security threats**:
```bash
# View recent security threats
docker logs thinkonerp-api | grep "Security threat detected" | tail -50
```

2. **Identify attacking IPs**:
```sql
SELECT IP_ADDRESS, COUNT(*) AS failed_attempts,
       MIN(CREATION_DATE) AS first_attempt,
       MAX(CREATION_DATE) AS last_attempt
FROM SYS_AUDIT_LOG
WHERE ACTION = 'LOGIN_FAILED'
  AND CREATION_DATE > SYSDATE - 1/24  -- Last hour
GROUP BY IP_ADDRESS
HAVING COUNT(*) > 10
ORDER BY failed_attempts DESC;
```

3. **Check affected accounts**:
```sql
SELECT ENTITY_ID AS user_id, IP_ADDRESS, COUNT(*) AS attempts
FROM SYS_AUDIT_LOG
WHERE ACTION = 'LOGIN_FAILED'
  AND CREATION_DATE > SYSDATE - 1/24
GROUP BY ENTITY_ID, IP_ADDRESS
ORDER BY attempts DESC;
```

4. **Review attack pattern**:
```bash
# Check for distributed attack (multiple IPs)
docker logs thinkonerp-api | grep "LOGIN_FAILED" | \
  awk '{print $5}' | sort | uniq -c | sort -rn
```

### Resolution Steps

#### Immediate Actions

1. **Block attacking IP** (if firewall available):
```bash
# Using UFW
sudo ufw deny from <attacking-ip>

# Using iptables
sudo iptables -A INPUT -s <attacking-ip> -j DROP
```

2. **Lock compromised accounts** (if any):
```sql
UPDATE SYS_USERS
SET IS_ACTIVE = 0
WHERE ROW_ID IN (
  SELECT DISTINCT ENTITY_ID
  FROM SYS_AUDIT_LOG
  WHERE ACTION = 'LOGIN_FAILED'
    AND IP_ADDRESS = '<attacking-ip>'
    AND CREATION_DATE > SYSDATE - 1/24
);
```

3. **Enable rate limiting** (if not already enabled):
```json
{
  "SecurityMonitoring": {
    "FailedLoginThreshold": 5,
    "FailedLoginWindowMinutes": 5,
    "BlockDurationMinutes": 30
  }
}
```

#### Short-term Actions

1. **Notify affected users**:
```sql
-- Get list of users with failed attempts
SELECT u.EMAIL, u.USER_NAME, COUNT(*) AS failed_attempts
FROM SYS_USERS u
JOIN SYS_AUDIT_LOG a ON u.ROW_ID = a.ENTITY_ID
WHERE a.ACTION = 'LOGIN_FAILED'
  AND a.CREATION_DATE > SYSDATE - 1/24
GROUP BY u.EMAIL, u.USER_NAME
HAVING COUNT(*) > 3;
```

2. **Force password reset** (if accounts compromised):
```sql
UPDATE SYS_USERS
SET FORCE_PASSWORD_CHANGE = 1
WHERE ROW_ID IN (<compromised-user-ids>);
```

3. **Review security logs**:
```bash
# Export security report
curl -X POST http://localhost:5000/api/compliance/security-report \
  -H "Authorization: Bearer $ADMIN_TOKEN" \
  -H "Content-Type: application/json" \
  -d '{
    "startDate": "2024-01-01T00:00:00Z",
    "endDate": "2024-01-02T00:00:00Z"
  }' > security-report.pdf
```

#### Long-term Actions

1. **Implement CAPTCHA** for login:
   - Add reCAPTCHA to login form
   - Require CAPTCHA after 3 failed attempts

2. **Enable multi-factor authentication**:
   - Implement 2FA for all users
   - Require 2FA for admin accounts

3. **Implement IP reputation checking**:
   - Integrate with IP reputation services
   - Block known malicious IPs automatically

4. **Add account lockout policy**:
```json
{
  "Authentication": {
    "AccountLockout": {
      "Enabled": true,
      "MaxFailedAttempts": 5,
      "LockoutDurationMinutes": 30,
      "ResetCounterMinutes": 15
    }
  }
}
```

### Prevention

1. **Security monitoring**:
   - Monitor failed login patterns
   - Set up alerts for suspicious activity
   - Review security reports weekly

2. **User education**:
   - Train users on password security
   - Encourage use of password managers
   - Implement password complexity requirements

3. **Infrastructure protection**:
   - Use WAF (Web Application Firewall)
   - Implement rate limiting at load balancer
   - Use DDoS protection services

### Verification

```bash
# Verify IP is blocked
curl -X POST http://localhost:5000/api/auth/login \
  --interface <attacking-ip> \
  -H "Content-Type: application/json" \
  -d '{"userName":"test","password":"test"}'

# Check no recent failed attempts
docker logs thinkonerp-api --since 5m | grep "LOGIN_FAILED"

# Verify security monitoring is active
curl http://localhost:5000/metrics | grep security_threats_detected
```

### Escalation

- **Level 1**: Operations team (IP blocking, monitoring)
- **Level 2**: Security team (incident response, forensics)
- **Level 3**: Development team (security enhancements)

---

## Runbook 7: Slow Query Performance

### Symptoms
- Alert: "Slow query detected (>500ms)"
- Users reporting slow data loading
- Database CPU usage high
- Audit query timeouts

### Impact
- **Severity**: Medium
- **User Impact**: Slow response times, poor user experience
- **Business Impact**: Reduced productivity

### Diagnosis Steps

1. **Identify slow queries**:
```bash
# Check application logs
docker logs thinkonerp-api | grep "Query execution time" | grep -E "[5-9][0-9]{2}ms|[0-9]{4}ms"
```

2. **Check database performance**:
```sql
-- Top 10 slowest queries
SELECT sql_id, sql_text, 
       executions,
       ROUND(elapsed_time/1000000, 2) AS total_seconds,
       ROUND(elapsed_time/executions/1000, 2) AS avg_ms,
       ROUND(cpu_time/1000000, 2) AS cpu_seconds,
       disk_reads,
       buffer_gets
FROM v$sql
WHERE executions > 0
ORDER BY elapsed_time/executions DESC
FETCH FIRST 10 ROWS ONLY;
```

3. **Check for missing indexes**:
```sql
-- Find full table scans
SELECT sql_id, sql_text, executions, disk_reads
FROM v$sql
WHERE sql_text LIKE '%SYS_AUDIT_LOG%'
  AND disk_reads > 10000
ORDER BY disk_reads DESC;
```

4. **Review execution plans**:
```sql
-- Get execution plan for slow query
SELECT * FROM TABLE(DBMS_XPLAN.DISPLAY_CURSOR('<sql_id>', NULL, 'ALLSTATS LAST'));
```

### Resolution Steps

#### Missing Indexes

1. **Identify missing indexes**:
```sql
-- Check existing indexes
SELECT index_name, column_name, column_position
FROM dba_ind_columns
WHERE table_name = 'SYS_AUDIT_LOG'
ORDER BY index_name, column_position;
```

2. **Add recommended indexes**:
```sql
-- For date range queries
CREATE INDEX IDX_AUDIT_LOG_DATE_RANGE ON SYS_AUDIT_LOG(CREATION_DATE, COMPANY_ID);

-- For entity queries
CREATE INDEX IDX_AUDIT_LOG_ENTITY ON SYS_AUDIT_LOG(ENTITY_TYPE, ENTITY_ID, CREATION_DATE);

-- For actor queries
CREATE INDEX IDX_AUDIT_LOG_ACTOR ON SYS_AUDIT_LOG(ACTOR_ID, CREATION_DATE);
```

3. **Rebuild fragmented indexes**:
```sql
-- Check index fragmentation
SELECT index_name, blevel, leaf_blocks, distinct_keys
FROM dba_indexes
WHERE table_name = 'SYS_AUDIT_LOG';

-- Rebuild if needed
ALTER INDEX IDX_AUDIT_LOG_CORRELATION REBUILD ONLINE;
```

#### Query Optimization

1. **Rewrite inefficient queries**:
```sql
-- Bad: Using OR conditions
SELECT * FROM SYS_AUDIT_LOG
WHERE ENTITY_TYPE = 'User' OR ENTITY_TYPE = 'Company';

-- Good: Using IN clause
SELECT * FROM SYS_AUDIT_LOG
WHERE ENTITY_TYPE IN ('User', 'Company');
```

2. **Add query hints** (if needed):
```sql
-- Force index usage
SELECT /*+ INDEX(SYS_AUDIT_LOG IDX_AUDIT_LOG_DATE_RANGE) */
  *
FROM SYS_AUDIT_LOG
WHERE CREATION_DATE BETWEEN :start_date AND :end_date;
```

3. **Implement pagination**:
```csharp
// Always use pagination for large result sets
var query = context.AuditLogs
    .Where(a => a.CreationDate >= startDate)
    .OrderByDescending(a => a.CreationDate)
    .Skip((pageNumber - 1) * pageSize)
    .Take(pageSize);
```

#### Statistics Update

1. **Gather table statistics**:
```sql
-- Gather statistics for SYS_AUDIT_LOG
EXEC DBMS_STATS.GATHER_TABLE_STATS(
  ownname => 'SYSTEM',
  tabname => 'SYS_AUDIT_LOG',
  estimate_percent => DBMS_STATS.AUTO_SAMPLE_SIZE,
  method_opt => 'FOR ALL COLUMNS SIZE AUTO',
  cascade => TRUE
);
```

2. **Schedule automatic statistics gathering**:
```sql
-- Enable automatic statistics gathering
BEGIN
  DBMS_SCHEDULER.SET_ATTRIBUTE(
    name => 'MAINTENANCE_WINDOW_GROUP',
    attribute => 'ENABLED',
    value => TRUE
  );
END;
```

#### Caching Implementation

1. **Enable Redis caching**:
```json
{
  "Caching": {
    "Enabled": true,
    "Redis": {
      "ConnectionString": "localhost:6379",
      "DefaultExpirationMinutes": 15
    }
  }
}
```

2. **Cache frequently accessed queries**:
```csharp
// Cache audit query results
var cacheKey = $"audit:query:{filter.GetHashCode()}";
var cachedResult = await cache.GetAsync<PagedResult<AuditLogEntry>>(cacheKey);

if (cachedResult == null)
{
    cachedResult = await auditQueryService.QueryAsync(filter, pagination);
    await cache.SetAsync(cacheKey, cachedResult, TimeSpan.FromMinutes(15));
}
```

### Prevention

1. **Query performance monitoring**:
   - Monitor slow query trends
   - Set up alerts for queries >500ms
   - Review execution plans regularly

2. **Index maintenance**:
   - Weekly index rebuild for fragmented indexes
   - Monthly index usage analysis
   - Remove unused indexes

3. **Database tuning**:
   - Optimize Oracle SGA/PGA
   - Configure appropriate buffer cache
   - Enable result cache for frequently accessed data

### Verification

```bash
# Test query performance
time curl "http://localhost:5000/api/auditlogs?startDate=2024-01-01&endDate=2024-01-31&pageSize=50"

# Check no slow queries in recent logs
docker logs thinkonerp-api --since 5m | grep "Query execution time" | grep -E "[5-9][0-9]{2}ms"

# Verify indexes are being used
sqlplus system/password@//localhost:1521/XE
SELECT * FROM v$sql_plan WHERE object_name LIKE 'IDX_AUDIT%';
```

### Escalation

- **Level 1**: Operations team (monitoring, basic troubleshooting)
- **Level 2**: Database team (query optimization, index tuning)
- **Level 3**: Development team (query rewriting, caching implementation)

---

## Runbook 8: Memory Pressure and OOM

### Symptoms
- Alert: "Memory usage > 80%"
- Container being killed by OOM killer
- Logs showing: "OutOfMemoryException"
- Frequent garbage collections
- API becoming unresponsive

### Impact
- **Severity**: Critical
- **User Impact**: API unavailable, request failures
- **Business Impact**: Service outage

### Diagnosis Steps

1. **Check current memory usage**:
```bash
# Container memory
docker stats thinkonerp-api --no-stream

# Detailed memory info
docker exec -it thinkonerp-api cat /proc/meminfo
```

2. **Check for memory leaks**:
```bash
# View GC statistics
docker logs thinkonerp-api | grep "GC" | tail -50

# Check heap size
docker exec -it thinkonerp-api dotnet-counters monitor --process-id 1
```

3. **Identify memory-intensive operations**:
```bash
# Large audit batches
docker logs thinkonerp-api | grep "Batch size" | tail -20

# Large query results
docker logs thinkonerp-api | grep "Query returned" | grep -E "[0-9]{4,} rows"
```

4. **Check for resource leaks**:
```bash
# Unclosed connections
docker logs thinkonerp-api | grep -i "connection.*leak"

# Undisposed objects
docker logs thinkonerp-api | grep -i "dispose"
```

### Resolution Steps

#### Immediate Actions

1. **Restart container**:
```bash
docker-compose restart thinkonerp-api
```

2. **Increase memory limit** (temporary):
```bash
# Update docker-compose.yml
docker-compose down
# Edit docker-compose.yml: memory: 4G
docker-compose up -d
```

3. **Force garbage collection** (if accessible):
```bash
# Trigger GC via diagnostic endpoint (if implemented)
curl -X POST http://localhost:5000/api/diagnostics/gc
```

#### Short-term Actions

1. **Reduce batch sizes**:
```json
{
  "AuditLogging": {
    "BatchSize": 25,  // Reduce from 50
    "MaxQueueSize": 5000  // Reduce from 10000
  }
}
```

2. **Implement pagination limits**:
```json
{
  "Pagination": {
    "MaxPageSize": 100,  // Reduce from 1000
    "DefaultPageSize": 20
  }
}
```

3. **Enable response streaming**:
```csharp
// Stream large responses instead of buffering
public async Task<IActionResult> ExportAuditLogs([FromQuery] AuditQueryFilter filter)
{
    var stream = auditQueryService.StreamResultsAsync(filter);
    return File(stream, "application/json", "audit-logs.json");
}
```

#### Long-term Actions

1. **Profile memory usage**:
```bash
# Install dotnet-dump
docker exec -it thinkonerp-api dotnet tool install --global dotnet-dump

# Capture memory dump
docker exec -it thinkonerp-api dotnet-dump collect --process-id 1

# Analyze dump
dotnet-dump analyze dump_20240101_120000.dmp
```

2. **Fix memory leaks**:
   - Review event handler subscriptions
   - Check for static collections growing unbounded
   - Verify all IDisposable objects are disposed
   - Use weak references for caches

3. **Optimize memory usage**:
```csharp
// Use object pooling for frequently allocated objects
services.AddSingleton<ObjectPoolProvider, DefaultObjectPoolProvider>();
services.AddSingleton(serviceProvider =>
{
    var provider = serviceProvider.GetRequiredService<ObjectPoolProvider>();
    return provider.Create(new DefaultPooledObjectPolicy<StringBuilder>());
});

// Use ArrayPool for large arrays
var buffer = ArrayPool<byte>.Shared.Rent(size);
try
{
    // Use buffer
}
finally
{
    ArrayPool<byte>.Shared.Return(buffer);
}
```

4. **Implement memory limits**:
```json
{
  "MemoryManagement": {
    "MaxCacheSize": 100,  // MB
    "MaxQueueSize": 5000,
    "EnableMemoryPressureMonitoring": true,
    "MemoryPressureThreshold": 0.8
  }
}
```

### Prevention

1. **Memory monitoring**:
   - Monitor memory usage trends
   - Set up alerts at 70% and 80%
   - Track GC frequency and duration

2. **Load testing**:
   - Test with realistic data volumes
   - Verify memory usage under sustained load
   - Test memory recovery after spikes

3. **Code review**:
   - Review for memory leaks
   - Ensure proper disposal patterns
   - Use memory profilers during development

### Verification

```bash
# Check memory usage is normal
docker stats thinkonerp-api --no-stream

# Verify no OOM errors
docker logs thinkonerp-api --since 5m | grep -i "OutOfMemory"

# Check GC is not excessive
docker logs thinkonerp-api --since 5m | grep "GC" | wc -l

# Test API is responsive
curl http://localhost:5000/health
```

### Escalation

- **Level 1**: Operations team (restart, resource allocation)
- **Level 2**: Platform team (container optimization)
- **Level 3**: Development team (memory leak fixes, optimization)

---

## Runbook 9: Archival Service Failures

### Symptoms
- Alert: "Archival service failed"
- Logs showing: "Failed to archive audit data"
- SYS_AUDIT_LOG table growing beyond expected size
- Disk space warnings

### Impact
- **Severity**: Medium
- **User Impact**: None (archival is background process)
- **Business Impact**: Storage costs, potential performance degradation

### Diagnosis Steps

1. **Check archival service status**:
```bash
# View archival logs
docker logs thinkonerp-api | grep "Archival" | tail -50
```

2. **Check table sizes**:
```sql
SELECT segment_name, ROUND(bytes/1024/1024, 2) AS size_mb
FROM dba_segments
WHERE segment_name IN ('SYS_AUDIT_LOG', 'SYS_AUDIT_LOG_ARCHIVE')
ORDER BY bytes DESC;
```

3. **Check retention policies**:
```sql
SELECT * FROM SYS_RETENTION_POLICIES;
```

4. **Check for archival errors**:
```bash
docker logs thinkonerp-api | grep -i "archival.*error" | tail -20
```

### Resolution Steps

#### Table Space Issues

1. **Check table space**:
```sql
SELECT tablespace_name, 
       ROUND(SUM(bytes)/1024/1024, 2) AS used_mb,
       ROUND(MAX(maxbytes)/1024/1024, 2) AS max_mb,
       ROUND(SUM(bytes)/MAX(maxbytes)*100, 2) AS pct_used
FROM dba_data_files
GROUP BY tablespace_name;
```

2. **Add space if needed**:
```sql
ALTER TABLESPACE USERS ADD DATAFILE 
'/u01/app/oracle/oradata/XE/users03.dbf' SIZE 1G AUTOEXTEND ON;
```

#### Long-Running Archival

1. **Check for long transactions**:
```sql
SELECT sid, serial#, username, 
       ROUND(used_ublk*8192/1024/1024, 2) AS undo_mb,
       start_time
FROM v$transaction t
JOIN v$session s ON t.ses_addr = s.saddr
WHERE username = 'SYSTEM';
```

2. **Reduce batch size**:
```json
{
  "Archival": {
    "BatchSize": 1000,  // Reduce from 5000
    "MaxExecutionMinutes": 30
  }
}
```

3. **Kill stuck archival transaction** (if necessary):
```sql
ALTER SYSTEM KILL SESSION 'sid,serial#' IMMEDIATE;
```

#### External Storage Issues

1. **Test S3/Azure connectivity**:
```bash
# Test S3 connection
docker exec -it thinkonerp-api aws s3 ls s3://your-bucket/

# Test Azure connection
docker exec -it thinkonerp-api az storage blob list --account-name youraccountname
```

2. **Check credentials**:
```bash
# Verify environment variables
docker exec -it thinkonerp-api printenv | grep -E "AWS|AZURE"
```

3. **Retry failed uploads**:
```bash
# Trigger manual archival
curl -X POST http://localhost:5000/api/admin/archival/retry \
  -H "Authorization: Bearer $ADMIN_TOKEN"
```

#### Checksum Verification Failures

1. **Check for data corruption**:
```sql
SELECT archive_batch_id, COUNT(*) AS records, 
       SUM(CASE WHEN checksum IS NULL THEN 1 ELSE 0 END) AS missing_checksums
FROM SYS_AUDIT_LOG_ARCHIVE
GROUP BY archive_batch_id
HAVING SUM(CASE WHEN checksum IS NULL THEN 1 ELSE 0 END) > 0;
```

2. **Recalculate checksums**:
```sql
UPDATE SYS_AUDIT_LOG_ARCHIVE
SET checksum = DBMS_CRYPTO.HASH(
  UTL_RAW.CAST_TO_RAW(OLD_VALUE || NEW_VALUE || TO_CHAR(CREATION_DATE, 'YYYY-MM-DD HH24:MI:SS')),
  DBMS_CRYPTO.HASH_SH256
)
WHERE checksum IS NULL;
```

### Prevention

1. **Archival monitoring**:
   - Monitor archival job completion
   - Track archived data volume
   - Alert on archival failures

2. **Capacity planning**:
   - Monitor table growth rates
   - Plan storage capacity 6 months ahead
   - Implement automatic table space management

3. **Regular testing**:
   - Test archival process monthly
   - Verify data retrieval from archive
   - Test external storage connectivity

### Verification

```bash
# Check archival completed successfully
docker logs thinkonerp-api | grep "Archival completed" | tail -5

# Verify data was archived
sqlplus system/password@//localhost:1521/XE
SELECT COUNT(*) FROM SYS_AUDIT_LOG_ARCHIVE WHERE ARCHIVED_DATE > SYSDATE - 1;

# Check table sizes reduced
SELECT segment_name, ROUND(bytes/1024/1024, 2) AS size_mb
FROM dba_segments
WHERE segment_name = 'SYS_AUDIT_LOG';
```

### Escalation

- **Level 1**: Operations team (monitoring, basic troubleshooting)
- **Level 2**: Database team (table space management, performance)
- **Level 3**: Development team (archival logic fixes)

---

## Runbook 10: Alert Notification Failures

### Symptoms
- Alerts not being received
- Logs showing: "Failed to send alert notification"
- Email/SMS/webhook delivery failures
- Alert history shows failed deliveries

### Impact
- **Severity**: High
- **User Impact**: Missing critical alerts
- **Business Impact**: Delayed incident response

### Diagnosis Steps

1. **Check alert service status**:
```bash
docker logs thinkonerp-api | grep "Alert" | tail -50
```

2. **Check notification channel errors**:
```bash
# Email errors
docker logs thinkonerp-api | grep "Email.*failed" | tail -20

# Webhook errors
docker logs thinkonerp-api | grep "Webhook.*failed" | tail -20

# SMS errors
docker logs thinkonerp-api | grep "SMS.*failed" | tail -20
```

3. **Test notification channels**:
```bash
# Test email
curl -X POST http://localhost:5000/api/admin/alerts/test-email \
  -H "Authorization: Bearer $ADMIN_TOKEN" \
  -H "Content-Type: application/json" \
  -d '{"recipient":"admin@example.com"}'
```

### Resolution Steps

#### Email Delivery Issues

1. **Check SMTP configuration**:
```bash
# Verify SMTP settings
docker exec -it thinkonerp-api printenv | grep SMTP
```

2. **Test SMTP connectivity**:
```bash
# Test SMTP connection
docker exec -it thinkonerp-api telnet smtp.gmail.com 587
```

3. **Update SMTP settings**:
```json
{
  "Email": {
    "SmtpHost": "smtp.gmail.com",
    "SmtpPort": 587,
    "UseSsl": true,
    "Username": "your-email@gmail.com",
    "Password": "your-app-password",
    "FromAddress": "noreply@thinkonerp.com",
    "FromName": "ThinkOnErp Alerts"
  }
}
```

4. **Check email rate limits**:
   - Gmail: 500 emails/day
   - SendGrid: Based on plan
   - AWS SES: Based on sending limits

#### Webhook Delivery Issues

1. **Test webhook endpoint**:
```bash
# Test webhook URL
curl -X POST https://your-webhook-url.com/alerts \
  -H "Content-Type: application/json" \
  -d '{"test":"message"}'
```

2. **Check webhook timeout**:
```json
{
  "Alerts": {
    "Webhook": {
      "TimeoutSeconds": 30,  // Increase if needed
      "RetryAttempts": 3
    }
  }
}
```

3. **Verify webhook authentication**:
```bash
# Check webhook headers
docker logs thinkonerp-api | grep "Webhook request" | tail -5
```

#### SMS Delivery Issues

1. **Check Twilio credentials**:
```bash
docker exec -it thinkonerp-api printenv | grep TWILIO
```

2. **Test Twilio API**:
```bash
curl -X POST https://api.twilio.com/2010-04-01/Accounts/$TWILIO_ACCOUNT_SID/Messages.json \
  --data-urlencode "To=+1234567890" \
  --data-urlencode "From=+1234567890" \
  --data-urlencode "Body=Test message" \
  -u $TWILIO_ACCOUNT_SID:$TWILIO_AUTH_TOKEN
```

3. **Check SMS rate limits**:
   - Twilio: Based on account type
   - Verify phone numbers are verified

#### Rate Limiting Issues

1. **Check alert rate limits**:
```bash
docker logs thinkonerp-api | grep "Alert rate limit" | tail -20
```

2. **Adjust rate limits**:
```json
{
  "Alerts": {
    "RateLimiting": {
      "MaxAlertsPerHour": 20,  // Increase if needed
      "MaxAlertsPerRule": 10
    }
  }
}
```

### Prevention

1. **Notification monitoring**:
   - Monitor delivery success rates
   - Set up alerts for alert failures (meta-alerts)
   - Track notification latency

2. **Redundancy**:
   - Configure multiple notification channels
   - Implement fallback channels
   - Use multiple SMTP providers

3. **Testing**:
   - Test notifications weekly
   - Verify all channels are working
   - Update contact information regularly

### Verification

```bash
# Send test alert
curl -X POST http://localhost:5000/api/admin/alerts/test \
  -H "Authorization: Bearer $ADMIN_TOKEN"

# Check alert was delivered
docker logs thinkonerp-api | grep "Alert sent successfully" | tail -5

# Verify no recent failures
docker logs thinkonerp-api --since 5m | grep "Alert.*failed"
```

### Escalation

- **Level 1**: Operations team (configuration, testing)
- **Level 2**: Infrastructure team (SMTP, network issues)
- **Level 3**: Development team (notification logic fixes)

---

## Runbook 11: Redis Cache Connectivity Issues

### Symptoms
- Alert: "Redis connection failed"
- Logs showing: "StackExchange.Redis.RedisConnectionException"
- Audit query performance degraded (cache misses)
- Security monitoring not tracking failed logins
- Increased database load

### Impact
- **Severity**: Medium
- **User Impact**: Slower response times, degraded performance
- **Business Impact**: Increased database load, higher infrastructure costs

### Diagnosis Steps

1. **Check Redis connection status**:
```bash
# Test Redis connectivity
docker exec -it thinkonerp-api bash
redis-cli -h redis-host -p 6379 ping

# Check Redis container status
docker ps | grep redis
docker logs redis-container
```

2. **Check application logs**:
```bash
# View Redis connection errors
docker logs thinkonerp-api | grep -i "redis" | grep -i "error" | tail -50

# Check cache hit/miss rates
docker logs thinkonerp-api | grep "Cache" | tail -20
```

3. **Check Redis server health**:
```bash
# Connect to Redis
redis-cli -h redis-host -p 6379

# Check server info
INFO server
INFO stats
INFO memory

# Check connected clients
CLIENT LIST

# Check slow log
SLOWLOG GET 10
```

4. **Check network connectivity**:
```bash
# Test network connection
docker exec -it thinkonerp-api ping redis-host
docker exec -it thinkonerp-api telnet redis-host 6379

# Check DNS resolution
docker exec -it thinkonerp-api nslookup redis-host
```

### Resolution Steps

#### Redis Server Down

1. **Check Redis container status**:
```bash
# Check if Redis is running
docker ps -a | grep redis

# View Redis logs
docker logs redis-container --tail 100
```

2. **Restart Redis container**:
```bash
# Restart Redis
docker-compose restart redis

# Or start if stopped
docker-compose up -d redis
```

3. **Verify Redis is accepting connections**:
```bash
# Test connection
redis-cli -h localhost -p 6379 ping
# Expected output: PONG
```

#### Connection String Issues

1. **Verify Redis connection string**:
```bash
# Check environment variable
docker exec -it thinkonerp-api printenv | grep REDIS

# Check appsettings.json
docker exec -it thinkonerp-api cat /app/appsettings.json | grep -A 5 "Redis"
```

2. **Update connection string**:
```json
{
  "Caching": {
    "Redis": {
      "ConnectionString": "redis-host:6379,password=your-password,ssl=false,abortConnect=false,connectTimeout=5000,syncTimeout=5000",
      "InstanceName": "ThinkOnErp:"
    }
  }
}
```

3. **Test updated connection**:
```bash
# Restart API to pick up new settings
docker-compose restart thinkonerp-api

# Verify connection
docker logs thinkonerp-api | grep "Redis connected"
```

#### Redis Memory Issues

1. **Check Redis memory usage**:
```bash
redis-cli -h redis-host -p 6379
INFO memory

# Check maxmemory setting
CONFIG GET maxmemory
```

2. **Check eviction policy**:
```bash
# View current policy
CONFIG GET maxmemory-policy

# Set appropriate policy (if needed)
CONFIG SET maxmemory-policy allkeys-lru
```

3. **Increase Redis memory** (if needed):
```yaml
# docker-compose.yml
services:
  redis:
    image: redis:7-alpine
    command: redis-server --maxmemory 2gb --maxmemory-policy allkeys-lru
```

4. **Clear cache if necessary**:
```bash
# Flush all keys (use with caution)
redis-cli -h redis-host -p 6379 FLUSHALL

# Or flush specific database
redis-cli -h redis-host -p 6379 FLUSHDB
```

#### Connection Pool Exhaustion

1. **Check connection pool settings**:
```json
{
  "Caching": {
    "Redis": {
      "ConnectionString": "redis-host:6379,connectTimeout=5000,syncTimeout=5000,connectRetry=3,abortConnect=false"
    }
  }
}
```

2. **Monitor connection count**:
```bash
# Check connected clients
redis-cli -h redis-host -p 6379 CLIENT LIST | wc -l

# Check max clients
redis-cli -h redis-host -p 6379 CONFIG GET maxclients
```

3. **Increase max clients** (if needed):
```bash
redis-cli -h redis-host -p 6379 CONFIG SET maxclients 10000
```

#### Network Issues

1. **Check firewall rules**:
```bash
# Check if port 6379 is open
sudo ufw status | grep 6379

# Allow Redis port if needed
sudo ufw allow 6379/tcp
```

2. **Check Docker network**:
```bash
# Inspect Docker network
docker network inspect thinkonerp_network

# Verify containers are on same network
docker inspect thinkonerp-api | grep -A 10 Networks
docker inspect redis-container | grep -A 10 Networks
```

3. **Test connectivity between containers**:
```bash
# From API container to Redis
docker exec -it thinkonerp-api nc -zv redis-host 6379
```

#### Authentication Issues

1. **Verify Redis password**:
```bash
# Check if Redis requires password
redis-cli -h redis-host -p 6379 INFO
# If you get "NOAUTH Authentication required", password is needed

# Test with password
redis-cli -h redis-host -p 6379 -a your-password ping
```

2. **Update connection string with password**:
```json
{
  "Caching": {
    "Redis": {
      "ConnectionString": "redis-host:6379,password=your-password"
    }
  }
}
```

### Prevention

1. **Redis monitoring**:
   - Monitor Redis memory usage (alert at 80%)
   - Track connection count
   - Monitor cache hit/miss rates
   - Set up Redis health checks

2. **High availability**:
   - Implement Redis Sentinel for automatic failover
   - Use Redis Cluster for horizontal scaling
   - Configure Redis persistence (RDB + AOF)

3. **Connection resilience**:
```json
{
  "Caching": {
    "Redis": {
      "ConnectionString": "redis-host:6379,abortConnect=false,connectRetry=3,connectTimeout=5000,syncTimeout=5000",
      "EnableCircuitBreaker": true,
      "CircuitBreakerThreshold": 5,
      "CircuitBreakerTimeout": 30
    }
  }
}
```

4. **Regular maintenance**:
   - Monitor Redis slow log
   - Review memory usage trends
   - Test failover procedures
   - Update Redis version regularly

### Verification

```bash
# Verify Redis is responding
redis-cli -h redis-host -p 6379 ping
# Expected: PONG

# Check API can connect to Redis
docker logs thinkonerp-api | grep "Redis" | tail -10

# Test cache functionality
curl http://localhost:5000/api/auditlogs?pageSize=10
# Check logs for cache hit
docker logs thinkonerp-api | grep "Cache hit" | tail -5

# Verify no connection errors
docker logs thinkonerp-api --since 5m | grep -i "redis.*error"
```

### Escalation

- **Level 1**: Operations team (restart, basic troubleshooting)
- **Level 2**: Infrastructure team (network, Redis configuration)
- **Level 3**: Development team (connection pool tuning, circuit breaker)

---

## Runbook 12: Background Service Health Issues

### Symptoms
- Alert: "Background service not running"
- Logs showing: "Background service failed to start"
- Archival not running on schedule
- Performance metrics not being aggregated
- Scheduled reports not being generated

### Impact
- **Severity**: Medium to High (depending on service)
- **User Impact**: Varies by service (delayed reports, missing metrics)
- **Business Impact**: Data accumulation, compliance risk, operational visibility loss

### Diagnosis Steps

1. **Check background service status**:
```bash
# View background service logs
docker logs thinkonerp-api | grep "BackgroundService" | tail -50

# Check specific services
docker logs thinkonerp-api | grep -E "ArchivalService|MetricsAggregationService|ReportGenerationService" | tail -50
```

2. **Identify which service is failing**:
```bash
# Check service startup
docker logs thinkonerp-api | grep "Starting background service"

# Check for exceptions
docker logs thinkonerp-api | grep "BackgroundService.*exception" | tail -20
```

3. **Check service health endpoint**:
```bash
# Check overall health
curl http://localhost:5000/health

# Check detailed health (if available)
curl http://localhost:5000/health/detailed
```

4. **Review service execution history**:
```sql
-- Check archival service execution
SELECT * FROM SYS_AUDIT_LOG_ARCHIVE
WHERE ARCHIVED_DATE > SYSDATE - 7
ORDER BY ARCHIVED_DATE DESC;

-- Check metrics aggregation
SELECT * FROM SYS_PERFORMANCE_METRICS
WHERE AGGREGATION_DATE > SYSDATE - 7
ORDER BY AGGREGATION_DATE DESC;
```

### Resolution Steps

#### Service Startup Failures

1. **Check for configuration errors**:
```bash
# View startup logs
docker logs thinkonerp-api | grep -A 10 "Starting background service"

# Check for missing configuration
docker logs thinkonerp-api | grep -i "configuration.*missing"
```

2. **Verify service configuration**:
```json
{
  "BackgroundServices": {
    "ArchivalService": {
      "Enabled": true,
      "Schedule": "0 2 * * *",  // 2 AM daily
      "BatchSize": 5000
    },
    "MetricsAggregationService": {
      "Enabled": true,
      "IntervalMinutes": 60
    },
    "ReportGenerationService": {
      "Enabled": true,
      "Schedule": "0 8 * * 1"  // 8 AM every Monday
    }
  }
}
```

3. **Restart API to reinitialize services**:
```bash
docker-compose restart thinkonerp-api

# Monitor startup
docker logs thinkonerp-api -f | grep "BackgroundService"
```

#### Service Execution Failures

1. **Check for database connectivity issues**:
```bash
# View database errors
docker logs thinkonerp-api | grep "BackgroundService.*database" | tail -20

# Test database connection
docker exec -it thinkonerp-api bash
sqlplus system/password@//oracle-host:1521/XE
```

2. **Check for timeout issues**:
```bash
# Look for timeout errors
docker logs thinkonerp-api | grep "BackgroundService.*timeout" | tail -20
```

3. **Increase service timeout**:
```json
{
  "BackgroundServices": {
    "ArchivalService": {
      "MaxExecutionMinutes": 60,  // Increase from 30
      "CommandTimeoutSeconds": 300
    }
  }
}
```

#### Archival Service Issues

1. **Check archival service status**:
```bash
# View archival logs
docker logs thinkonerp-api | grep "ArchivalService" | tail -50

# Check last successful archival
docker logs thinkonerp-api | grep "Archival completed successfully" | tail -5
```

2. **Manually trigger archival** (if needed):
```bash
# Trigger archival via API
curl -X POST http://localhost:5000/api/admin/archival/run \
  -H "Authorization: Bearer $ADMIN_TOKEN"
```

3. **Check for table space issues** (see Runbook 9)

#### Metrics Aggregation Service Issues

1. **Check metrics service status**:
```bash
# View metrics aggregation logs
docker logs thinkonerp-api | grep "MetricsAggregationService" | tail-50

# Check last aggregation
docker logs thinkonerp-api | grep "Metrics aggregation completed" | tail -5
```

2. **Verify metrics are being collected**:
```sql
SELECT COUNT(*) AS recent_metrics
FROM SYS_PERFORMANCE_METRICS
WHERE AGGREGATION_DATE > SYSDATE - 1/24;  -- Last hour
```

3. **Manually trigger aggregation**:
```bash
curl -X POST http://localhost:5000/api/admin/metrics/aggregate \
  -H "Authorization: Bearer $ADMIN_TOKEN"
```

#### Report Generation Service Issues

1. **Check report service status**:
```bash
# View report generation logs
docker logs thinkonerp-api | grep "ReportGenerationService" | tail -50

# Check scheduled reports
docker logs thinkonerp-api | grep "Generating scheduled report" | tail -10
```

2. **Check for email delivery issues** (see Runbook 10)

3. **Manually generate report**:
```bash
curl -X POST http://localhost:5000/api/compliance/reports/generate \
  -H "Authorization: Bearer $ADMIN_TOKEN" \
  -H "Content-Type: application/json" \
  -d '{
    "reportType": "GDPR",
    "startDate": "2024-01-01",
    "endDate": "2024-01-31"
  }'
```

#### Service Deadlock or Hang

1. **Check for long-running operations**:
```bash
# Check if service is stuck
docker logs thinkonerp-api | grep "BackgroundService.*started" | tail -10
docker logs thinkonerp-api | grep "BackgroundService.*completed" | tail -10
```

2. **Check database locks**:
```sql
-- Check for blocking sessions
SELECT blocking_session, sid, serial#, wait_class, seconds_in_wait
FROM v$session
WHERE blocking_session IS NOT NULL;
```

3. **Restart API if service is hung**:
```bash
# Graceful restart
docker-compose restart thinkonerp-api

# Force restart if needed
docker-compose kill thinkonerp-api
docker-compose up -d thinkonerp-api
```

#### Scheduling Issues

1. **Verify cron expressions**:
```bash
# Test cron expression (use online tool or library)
# "0 2 * * *" = 2 AM daily
# "0 8 * * 1" = 8 AM every Monday
# "*/30 * * * *" = Every 30 minutes
```

2. **Check system time**:
```bash
# Check container time
docker exec -it thinkonerp-api date

# Check timezone
docker exec -it thinkonerp-api cat /etc/timezone
```

3. **Update schedule if needed**:
```json
{
  "BackgroundServices": {
    "ArchivalService": {
      "Schedule": "0 2 * * *",  // Cron expression
      "Timezone": "UTC"
    }
  }
}
```

### Prevention

1. **Service monitoring**:
   - Monitor service execution frequency
   - Track service execution duration
   - Alert on service failures
   - Monitor service health endpoints

2. **Health checks**:
```csharp
// Implement health checks for background services
public class BackgroundServiceHealthCheck : IHealthCheck
{
    private readonly IBackgroundServiceMonitor _monitor;
    
    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context)
    {
        var services = await _monitor.GetServiceStatusAsync();
        
        var failedServices = services.Where(s => !s.IsHealthy).ToList();
        
        if (failedServices.Any())
        {
            return HealthCheckResult.Unhealthy(
                $"Background services unhealthy: {string.Join(", ", failedServices.Select(s => s.Name))}");
        }
        
        return HealthCheckResult.Healthy("All background services healthy");
    }
}
```

3. **Graceful error handling**:
   - Implement retry logic with exponential backoff
   - Log detailed error information
   - Send alerts on repeated failures
   - Implement circuit breakers for external dependencies

4. **Regular testing**:
   - Test service execution manually
   - Verify scheduled tasks run on time
   - Test service recovery after failures
   - Review service logs weekly

### Verification

```bash
# Check all background services are running
docker logs thinkonerp-api | grep "BackgroundService.*started" | tail -10

# Verify recent executions
docker logs thinkonerp-api | grep "BackgroundService.*completed" | tail -10

# Check health endpoint
curl http://localhost:5000/health

# Verify no errors in last hour
docker logs thinkonerp-api --since 1h | grep "BackgroundService.*error"

# Check specific service execution
docker logs thinkonerp-api | grep "ArchivalService.*completed successfully" | tail -5
```

### Escalation

- **Level 1**: Operations team (restart, monitoring)
- **Level 2**: Database team (if database-related issues)
- **Level 3**: Development team (service logic fixes, scheduling issues)

---

## Quick Reference Guide

### Common Commands

```bash
# View logs
docker logs thinkonerp-api -f

# Check metrics
curl http://localhost:5000/metrics

# Health check
curl http://localhost:5000/health

# Restart service
docker-compose restart thinkonerp-api

# Check container stats
docker stats thinkonerp-api

# Execute SQL
sqlplus system/password@//localhost:1521/XE
```

### Alert Severity Levels

| Level | Response Time | Escalation |
|-------|---------------|------------|
| Critical | < 15 minutes | Immediate |
| High | < 1 hour | Within 2 hours |
| Medium | < 4 hours | Next business day |
| Low | < 24 hours | As time permits |

### Contact Information

- **Operations Team**: ops@thinkonerp.com
- **Database Team**: dba@thinkonerp.com
- **Development Team**: dev@thinkonerp.com
- **Security Team**: security@thinkonerp.com
- **On-Call**: +1-555-0100

### Useful Links

- [APM Configuration Guide](APM_CONFIGURATION_GUIDE.md)
- [Deployment Guide](../DEPLOYMENT.md)
- [Troubleshooting Guide](TROUBLESHOOTING_GUIDE.md)
- [Database Scripts](../Database/Scripts/)

---

## Document Maintenance

- **Last Updated**: 2024-01-02
- **Version**: 1.1
- **Owner**: Operations Team
- **Review Frequency**: Quarterly

### Change Log

| Date | Version | Changes | Author |
|------|---------|---------|--------|
| 2024-01-01 | 1.0 | Initial creation with 10 runbooks | Operations Team |
| 2024-01-02 | 1.1 | Added Runbook 11 (Redis Cache Connectivity Issues) and Runbook 12 (Background Service Health Issues) | Operations Team |

