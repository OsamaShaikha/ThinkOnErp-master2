# Audit Logging Graceful Degradation Guide

## Overview

The ThinkOnErp audit logging system is designed to **never break the application**, even when audit logging itself fails. This document explains how graceful degradation works, how to monitor audit logging health, and how to recover from failures.

## Key Principle

**Audit logging failures MUST NOT cause API requests to fail.**

The system is designed so that:
- API requests always succeed, even if audit logging is completely unavailable
- Audit events are queued and retried automatically
- Fallback mechanisms preserve audit data when the database is unavailable
- Operators have full visibility into audit logging health

## Architecture

### Layers of Protection

1. **Fire-and-Forget Pattern**
   - Middleware and behaviors use async fire-and-forget (`_ = Task`) for audit logging
   - Audit logging never blocks API request processing
   - Exceptions in audit logging are caught and logged but never propagated

2. **Asynchronous Queue with Backpressure**
   - Audit events are queued in memory using `System.Threading.Channels`
   - Background task processes events in batches
   - Backpressure prevents memory exhaustion when queue is full
   - Default queue size: 10,000 events

3. **Circuit Breaker Pattern**
   - Protects against cascading failures when database is slow or unavailable
   - Opens circuit after consecutive failures
   - Automatically closes circuit when database recovers
   - Configurable thresholds and timeouts

4. **Retry Policy**
   - Automatically retries transient database failures
   - Exponential backoff between retries
   - Configurable retry count and delays
   - Detects Oracle-specific transient errors

5. **File System Fallback**
   - Writes audit events to disk when database is unavailable
   - Structured JSON format for easy replay
   - Automatic replay when database recovers
   - File rotation to prevent disk space exhaustion

## Monitoring Audit Logging Health

### Health Check Endpoint

**GET /api/audithealth/status**

Returns the current health status of the audit logging system.

**Response (Healthy):**
```json
{
  "isHealthy": true,
  "status": "Healthy",
  "timestamp": "2024-01-15T10:30:00Z",
  "message": "Audit logging system is operating normally",
  "metrics": {
    "totalRequests": 15000,
    "successfulRequests": 14950,
    "failedRequests": 50,
    "circuitBreakerRejections": 0,
    "retriedRequests": 100,
    "circuitState": "Closed",
    "successRate": 99.67,
    "failureRate": 0.33,
    "rejectionRate": 0.0,
    "queueDepth": 25,
    "pendingFallbackFiles": 0
  }
}
```

**Response (Degraded):**
```json
{
  "isHealthy": false,
  "status": "Degraded",
  "timestamp": "2024-01-15T10:30:00Z",
  "message": "Audit logging system is degraded but API requests continue to operate normally",
  "metrics": {
    "totalRequests": 15000,
    "successfulRequests": 12000,
    "failedRequests": 3000,
    "circuitBreakerRejections": 500,
    "retriedRequests": 2500,
    "circuitState": "Open",
    "successRate": 80.0,
    "failureRate": 20.0,
    "rejectionRate": 3.33,
    "queueDepth": 8500,
    "pendingFallbackFiles": 150
  }
}
```

### Metrics Endpoint

**GET /api/audithealth/metrics** (Requires Admin Authorization)

Returns detailed metrics about the audit logging system.

### Key Metrics to Monitor

| Metric | Description | Healthy Range | Action Required |
|--------|-------------|---------------|-----------------|
| `successRate` | Percentage of successful audit writes | > 95% | Investigate if < 95% |
| `circuitState` | Circuit breaker state | Closed | Investigate if Open |
| `queueDepth` | Number of pending events | < 5000 | Investigate if > 8000 |
| `pendingFallbackFiles` | Events in file fallback | 0 | Replay when database recovers |
| `failureRate` | Percentage of failed writes | < 5% | Investigate if > 5% |

## Failure Scenarios and Behavior

### Scenario 1: Database Connection Failure

**What Happens:**
1. Audit logger attempts to write to database
2. Connection fails (transient error detected)
3. Retry policy attempts 3 retries with exponential backoff
4. If all retries fail, circuit breaker opens
5. Events are written to file system fallback
6. API requests continue to succeed normally

**Operator Actions:**
1. Check database connectivity: `SELECT 1 FROM DUAL`
2. Monitor fallback files: `GET /api/audithealth/metrics`
3. When database recovers, replay fallback: `POST /api/audithealth/replay-fallback`

**Recovery:**
- Circuit breaker automatically closes after successful health check
- Fallback events are replayed to database
- Normal operation resumes

### Scenario 2: Database Slow Performance

**What Happens:**
1. Audit writes take longer than expected
2. Queue depth increases as events accumulate
3. Backpressure may slow down event queuing (but not API requests)
4. Circuit breaker may open if timeouts occur
5. Events are written to file system fallback if circuit opens

**Operator Actions:**
1. Check database performance metrics
2. Monitor queue depth: `GET /api/audithealth/metrics`
3. Investigate slow queries in SYS_AUDIT_LOG table
4. Consider database tuning or scaling

**Recovery:**
- Improve database performance
- Queue will drain automatically
- Circuit breaker closes when performance improves

### Scenario 3: Queue Full (Backpressure)

**What Happens:**
1. Queue reaches maximum size (10,000 events)
2. Backpressure is applied - new events wait to be queued
3. API requests continue but may experience slight delay (< 10ms)
4. Background processor works to drain queue

**Operator Actions:**
1. Check queue depth: `GET /api/audithealth/metrics`
2. Investigate why queue is not draining (database issues?)
3. Consider increasing queue size in configuration
4. Monitor for database connectivity or performance issues

**Recovery:**
- Resolve underlying database issue
- Queue drains automatically
- Normal operation resumes

### Scenario 4: File System Fallback Full

**What Happens:**
1. File system fallback reaches size limit (100 MB default)
2. Oldest files are moved to archive directory
3. New events continue to be written to fallback
4. Archived files are preserved for manual recovery

**Operator Actions:**
1. Check pending fallback files: `GET /api/audithealth/metrics`
2. Replay fallback events: `POST /api/audithealth/replay-fallback`
3. Check archived files in `logs/audit-fallback/archive/`
4. Consider increasing fallback size limit in configuration

**Recovery:**
- Replay fallback events to database
- Archive directory can be cleaned up after verification

### Scenario 5: Complete Audit Logging Failure

**What Happens:**
1. All resilience mechanisms fail (database down, file system full, etc.)
2. Audit events are logged to standard application log
3. API requests continue to succeed normally
4. Audit data may be lost for this period

**Operator Actions:**
1. Check application logs for audit logging errors
2. Investigate root cause (database, disk space, etc.)
3. Resolve underlying issue
4. Accept that audit data may be lost for failure period
5. Document the outage for compliance purposes

**Recovery:**
- Resolve underlying issue
- System automatically resumes normal operation
- No manual intervention required for audit logging

## Configuration

### Audit Logging Options

```json
{
  "AuditLogging": {
    "Enabled": true,
    "BatchSize": 50,
    "BatchWindowMs": 100,
    "MaxQueueSize": 10000,
    "EnableCircuitBreaker": true,
    "SensitiveFields": ["password", "token", "refreshToken", "creditCard", "ssn"]
  }
}
```

### Resilient Audit Logger Options

```json
{
  "ResilientAuditLogger": {
    "EnableCircuitBreaker": true,
    "EnableRetryPolicy": true,
    "FallbackStrategy": "LogToFile",
    "FallbackFilePath": "logs/audit-fallback.log"
  }
}
```

### Circuit Breaker Options

```json
{
  "CircuitBreaker": {
    "FailureThreshold": 5,
    "SuccessThreshold": 2,
    "Timeout": 60000,
    "HalfOpenRetryDelay": 30000
  }
}
```

### Retry Policy Options

```json
{
  "RetryPolicy": {
    "MaxRetries": 3,
    "InitialDelay": 100,
    "MaxDelay": 5000,
    "BackoffMultiplier": 2.0
  }
}
```

### File System Fallback Options

```json
{
  "FileSystemAuditFallback": {
    "FallbackPath": "logs/audit-fallback",
    "MaxTotalSizeBytes": 104857600,
    "MaxReplayAttempts": 3
  }
}
```

## Operational Procedures

### Daily Health Check

1. Check audit logging health: `GET /api/audithealth/status`
2. Verify `isHealthy: true` and `circuitState: Closed`
3. Check `queueDepth` is reasonable (< 1000)
4. Verify `pendingFallbackFiles: 0`

### Weekly Review

1. Review audit logging metrics: `GET /api/audithealth/metrics`
2. Check success rate is > 99%
3. Review application logs for audit logging warnings
4. Verify no archived fallback files need replay

### Incident Response

**Alert: Audit Logging Degraded**

1. Check health status: `GET /api/audithealth/status`
2. Review metrics to identify issue:
   - High `queueDepth` → Database performance issue
   - `circuitState: Open` → Database connectivity issue
   - High `pendingFallbackFiles` → Database unavailable
3. Investigate root cause (database, network, disk space)
4. Resolve underlying issue
5. Replay fallback events if needed: `POST /api/audithealth/replay-fallback`
6. Verify recovery: `GET /api/audithealth/status`

**Alert: Queue Full**

1. Check queue depth: `GET /api/audithealth/metrics`
2. Investigate database connectivity and performance
3. Check for long-running transactions blocking SYS_AUDIT_LOG
4. Consider temporarily increasing queue size
5. Monitor until queue drains

**Alert: Fallback Files Accumulating**

1. Check pending fallback files: `GET /api/audithealth/metrics`
2. Verify database is available
3. Replay fallback events: `POST /api/audithealth/replay-fallback`
4. Monitor replay progress
5. Verify fallback files are cleared

## Testing Graceful Degradation

### Test 1: Database Unavailable

1. Stop Oracle database
2. Make API requests (should succeed)
3. Check health status (should show degraded)
4. Check fallback files are created
5. Start Oracle database
6. Replay fallback events
7. Verify all events are in database

### Test 2: Database Slow

1. Simulate slow database (add artificial delay)
2. Make API requests (should succeed)
3. Check queue depth increases
4. Remove delay
5. Verify queue drains

### Test 3: Queue Full

1. Configure small queue size (100 events)
2. Generate high volume of API requests
3. Verify backpressure is applied
4. Verify API requests still succeed
5. Verify queue drains when load decreases

### Test 4: Circuit Breaker Opens

1. Simulate database failures
2. Verify circuit breaker opens after threshold
3. Verify fallback mechanism activates
4. Restore database
5. Verify circuit breaker closes automatically

## Troubleshooting

### Problem: Health Check Returns Degraded

**Symptoms:**
- `GET /api/audithealth/status` returns `isHealthy: false`
- `status: "Degraded"`

**Diagnosis:**
1. Check `circuitState` in metrics
2. Check `queueDepth` in metrics
3. Check `pendingFallbackFiles` in metrics
4. Review application logs for errors

**Solutions:**
- If `circuitState: Open` → Check database connectivity
- If `queueDepth` high → Check database performance
- If `pendingFallbackFiles` > 0 → Replay fallback events

### Problem: Audit Events Not Appearing in Database

**Symptoms:**
- API requests succeed
- No audit events in SYS_AUDIT_LOG table

**Diagnosis:**
1. Check health status: `GET /api/audithealth/status`
2. Check if audit logging is enabled in configuration
3. Check application logs for errors
4. Check if events are in fallback files

**Solutions:**
- If audit logging disabled → Enable in configuration
- If circuit breaker open → Resolve database issue
- If events in fallback → Replay fallback events
- If no errors → Check database permissions

### Problem: High Queue Depth

**Symptoms:**
- `queueDepth` > 5000 in metrics
- Queue not draining

**Diagnosis:**
1. Check database connectivity
2. Check database performance
3. Check for blocking transactions
4. Check circuit breaker state

**Solutions:**
- Resolve database connectivity issues
- Optimize database performance
- Kill blocking transactions
- Wait for circuit breaker to close

### Problem: Fallback Files Not Replaying

**Symptoms:**
- `pendingFallbackFiles` > 0 after replay
- Replay endpoint returns errors

**Diagnosis:**
1. Check application logs for replay errors
2. Check database connectivity
3. Check file permissions
4. Check for corrupted fallback files

**Solutions:**
- Resolve database connectivity
- Fix file permissions
- Move corrupted files to error directory
- Retry replay operation

## Performance Impact

### Normal Operation

- **Latency Added:** < 1ms (fire-and-forget pattern)
- **Memory Usage:** ~10 MB (queue + processing)
- **CPU Usage:** < 1% (background processing)
- **Disk I/O:** Minimal (batch writes)

### Degraded Operation

- **Latency Added:** < 10ms (backpressure when queue full)
- **Memory Usage:** ~50 MB (full queue + fallback)
- **CPU Usage:** < 5% (retry attempts + fallback)
- **Disk I/O:** Moderate (fallback file writes)

### Recovery Operation

- **Latency Added:** None (replay is background operation)
- **Memory Usage:** ~20 MB (replay processing)
- **CPU Usage:** < 10% (replay processing)
- **Disk I/O:** High (reading fallback files)

## Compliance Considerations

### Audit Data Loss

In extreme failure scenarios (database down + file system full + application crash), audit data may be lost. This is acceptable because:

1. **Application Availability is Priority:** The system prioritizes keeping the application running over perfect audit logging
2. **Rare Occurrence:** Multiple simultaneous failures are extremely rare
3. **Documented Behavior:** This behavior is documented and understood
4. **Mitigation:** Multiple layers of protection minimize risk

### Compliance Reporting

When audit data loss occurs:

1. Document the incident (time, duration, root cause)
2. Estimate the number of affected events
3. Review application logs for partial audit data
4. Include incident in compliance reports
5. Implement corrective actions to prevent recurrence

## Summary

The audit logging system is designed to be **resilient and non-intrusive**:

- ✅ API requests always succeed, even when audit logging fails
- ✅ Multiple layers of protection prevent data loss
- ✅ Automatic recovery when issues are resolved
- ✅ Full visibility into system health
- ✅ Clear operational procedures for incident response

**Remember:** Audit logging failures should never cause API requests to fail. The system is designed to degrade gracefully and recover automatically.
