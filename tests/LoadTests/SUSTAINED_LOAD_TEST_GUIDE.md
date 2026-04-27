# Sustained Load Test Guide (1 Hour)

## Overview

This guide covers the execution and analysis of the **1-hour sustained load test** for the Full Traceability System. This test validates that the system maintains performance over extended periods at the target load of 10,000 requests per minute.

## Test Objectives

### Primary Goals

1. **Sustained Performance**: Validate that the system maintains 10,000 requests/minute for 1 hour
2. **Performance Stability**: Ensure no performance degradation over time (< 20% increase in latency)
3. **Memory Stability**: Verify no memory leaks during sustained operation
4. **Queue Management**: Confirm audit queue depth remains manageable (< 10,000 entries)
5. **Error Rate**: Maintain error rate below 1% throughout the test

### Requirements Validated

- **Requirement 13.1**: System SHALL support logging 10,000 requests per minute without degrading API response times
- **Non-Functional Requirement**: System SHALL maintain data integrity through transaction management
- **Reliability Requirement**: System SHALL have 99.9% availability

## Prerequisites

### 1. System Requirements

**Minimum Hardware:**
- CPU: 4+ cores
- RAM: 8GB+ available
- Disk: 10GB+ free space (for ~600,000 audit log entries)
- Network: Stable connection with low latency

**Software:**
- k6 installed (see [Installation Guide](README.md#prerequisites))
- ThinkOnErp API running and accessible
- Oracle database with sufficient capacity
- Valid JWT authentication token

### 2. Database Preparation

Before running the test, ensure your database is ready:

```sql
-- Check available tablespace
SELECT 
    tablespace_name,
    ROUND(SUM(bytes)/1024/1024/1024, 2) AS size_gb,
    ROUND(SUM(maxbytes)/1024/1024/1024, 2) AS max_size_gb
FROM dba_data_files
GROUP BY tablespace_name;

-- Verify audit log table exists and is accessible
SELECT COUNT(*) FROM SYS_AUDIT_LOG;

-- Check current audit log size
SELECT 
    ROUND(SUM(bytes)/1024/1024, 2) AS size_mb
FROM user_segments
WHERE segment_name = 'SYS_AUDIT_LOG';

-- Ensure sufficient space for ~600,000 new entries
-- Estimate: ~600,000 entries * ~2KB per entry = ~1.2GB
```

### 3. Application Configuration

Verify audit logging configuration in `appsettings.json`:

```json
{
  "AuditLogging": {
    "Enabled": true,
    "BatchSize": 50,
    "BatchWindowMs": 100,
    "MaxQueueSize": 10000,
    "SensitiveFields": ["password", "token", "refreshToken", "creditCard"],
    "MaskingPattern": "***MASKED***"
  }
}
```

### 4. Monitoring Setup

Set up monitoring before starting the test:

**Application Logs:**
```bash
# Tail application logs in a separate terminal
tail -f logs/log-*.txt
```

**Database Monitoring:**
```sql
-- Monitor active sessions (run periodically)
SELECT COUNT(*) FROM V$SESSION WHERE STATUS = 'ACTIVE';

-- Monitor connection pool
SELECT * FROM V$RESOURCE_LIMIT WHERE RESOURCE_NAME = 'processes';

-- Monitor audit log growth
SELECT COUNT(*) FROM SYS_AUDIT_LOG;
```

**System Resources:**
```bash
# Linux/macOS
htop
iostat -x 5

# Windows
# Use Task Manager or Performance Monitor
```

## Running the Test

### Basic Execution

Run the sustained load test with default settings:

```bash
cd tests/LoadTests
k6 run sustained-load-test-1hour.js
```

**Expected Duration:** ~75 minutes total
- Ramp up: 10 minutes
- Sustained load: 60 minutes
- Ramp down: 5 minutes

### Custom Configuration

**Specify API URL:**
```bash
k6 run --env API_URL=http://your-api-url:port sustained-load-test-1hour.js
```

**Provide JWT Token:**
```bash
k6 run --env JWT_TOKEN="your-jwt-token" sustained-load-test-1hour.js
```

**Save Results to File:**
```bash
k6 run --out json=sustained-results-$(date +%Y%m%d-%H%M%S).json sustained-load-test-1hour.js
```

**Run with Docker:**
```bash
docker run --rm -i \
  -e API_URL=http://host.docker.internal:5000 \
  grafana/k6:latest run - <sustained-load-test-1hour.js
```

### Recommended Execution

For comprehensive results with logging:

```bash
# Create results directory
mkdir -p results

# Run test with JSON output and console logging
k6 run \
  --out json=results/sustained-$(date +%Y%m%d-%H%M%S).json \
  sustained-load-test-1hour.js 2>&1 | tee results/sustained-$(date +%Y%m%d-%H%M%S).log
```

## Test Phases

### Phase 1: Ramp Up (10 minutes)

The test gradually increases load to reach the target:

| Time | Target Load | Purpose |
|------|-------------|---------|
| 0-2 min | 1,000 req/min | Initial warm-up |
| 2-5 min | 5,000 req/min | Intermediate load |
| 5-10 min | 10,000 req/min | Reach target load |

**What to Monitor:**
- Response times should remain stable
- Error rate should stay below 1%
- Queue depth should increase gradually
- Memory usage should stabilize

### Phase 2: Sustained Load (60 minutes)

The test maintains 10,000 requests/minute for 1 hour:

**Expected Behavior:**
- Consistent response times throughout
- Stable memory usage (no leaks)
- Queue depth remains below 10,000
- Error rate stays below 1%
- No performance degradation over time

**Critical Monitoring:**
- **First 10 minutes (10-20 min elapsed)**: Baseline performance metrics captured
- **Last 10 minutes (60-70 min elapsed)**: Final performance metrics captured
- **Comparison**: Last 10 min should not be >20% slower than first 10 min

### Phase 3: Ramp Down (5 minutes)

The test gradually reduces load to zero:

| Time | Target Load | Purpose |
|------|-------------|---------|
| 70-73 min | 1,000 req/min | Gradual reduction |
| 73-75 min | 0 req/min | Complete shutdown |

**What to Monitor:**
- Queue should drain completely
- All pending audit writes should complete
- No errors during shutdown

## Success Criteria

### ✅ Performance Metrics

| Metric | Threshold | Critical |
|--------|-----------|----------|
| API Response Time (p99) | < 10ms | YES |
| API Response Time (p95) | < 8ms | NO |
| HTTP Request Duration (p99) | < 500ms | YES |
| HTTP Request Duration (p95) | < 300ms | NO |
| Error Rate | < 1% | YES |
| HTTP Request Failures | < 1% | YES |

### ✅ Stability Metrics

| Metric | Threshold | Critical |
|--------|-----------|----------|
| Performance Degradation | < 20% | YES |
| Memory Growth | < 50% | YES |
| Queue Depth | < 10,000 | YES |
| Total Requests | ~600,000 | NO |

### ✅ Reliability Metrics

| Metric | Threshold | Critical |
|--------|-----------|----------|
| Availability | > 99.9% | YES |
| Data Loss | 0 | YES |
| Audit Write Failures | < 0.1% | YES |

## Interpreting Results

### Example Success Output

```
✓ api_response_time_ms............: p(99)=8.2ms   ✅ PASS (<10ms)
✓ http_req_duration................: p(99)=445ms   ✅ PASS (<500ms)
✓ error_rate.......................: 0.3%          ✅ PASS (<1%)
✓ http_req_failed..................: 0.2%          ✅ PASS (<1%)
✓ requests_per_minute..............: 10,000        ✅ PASS (target met)

Performance Degradation Analysis:
✓ response_time_first_10min (p95).: 6.5ms
✓ response_time_last_10min (p95)..: 7.2ms
✓ Degradation.....................: 10.8%         ✅ PASS (<20%)

System Health:
✓ audit_queue_depth (max).........: 8,500         ✅ PASS (<10,000)
✓ memory_usage_mb (start).........: 450MB
✓ memory_usage_mb (end)...........: 520MB
✓ Memory growth...................: 15.6%         ✅ PASS (<50%)
```

### Performance Degradation Analysis

Calculate performance degradation:

```
Degradation % = ((Last10Min_p95 - First10Min_p95) / First10Min_p95) * 100

Example:
First 10 min p95: 6.5ms
Last 10 min p95: 7.2ms
Degradation: ((7.2 - 6.5) / 6.5) * 100 = 10.8% ✅ PASS
```

**Interpretation:**
- **< 10%**: Excellent - No significant degradation
- **10-20%**: Good - Acceptable degradation
- **20-50%**: Warning - Investigate potential issues
- **> 50%**: Critical - System is degrading significantly

### Memory Leak Detection

Monitor memory growth over time:

```
Memory Growth % = ((End_Memory - Start_Memory) / Start_Memory) * 100

Example:
Start: 450MB
End: 520MB
Growth: ((520 - 450) / 450) * 100 = 15.6% ✅ PASS
```

**Interpretation:**
- **< 20%**: Normal - Expected growth for caching/buffers
- **20-50%**: Acceptable - Monitor for continued growth
- **50-100%**: Warning - Possible memory leak
- **> 100%**: Critical - Memory leak confirmed

## Troubleshooting

### Issue: High Error Rate (> 1%)

**Symptoms:**
- `error_rate` exceeds 1%
- `http_req_failed` exceeds 1%

**Possible Causes:**
1. Database connection pool exhaustion
2. Audit queue overflow
3. Database performance issues
4. Network timeouts

**Investigation Steps:**

```sql
-- Check database connections
SELECT COUNT(*) FROM V$SESSION WHERE STATUS = 'ACTIVE';

-- Check for blocking sessions
SELECT blocking_session, sid, serial#, wait_class, seconds_in_wait
FROM V$SESSION
WHERE blocking_session IS NOT NULL;

-- Check audit log table locks
SELECT * FROM V$LOCKED_OBJECT;
```

**Resolution:**
- Increase connection pool size in `appsettings.json`
- Increase `MaxQueueSize` in audit logging configuration
- Optimize database queries and indexes
- Scale database resources

### Issue: Performance Degradation (> 20%)

**Symptoms:**
- Response times increase significantly over time
- Last 10 minutes much slower than first 10 minutes

**Possible Causes:**
1. Memory leak in application
2. Database table fragmentation
3. Index degradation
4. Connection pool leaks

**Investigation Steps:**

```bash
# Check application memory usage
ps aux | grep ThinkOnErp

# Check for memory leaks in logs
grep -i "OutOfMemory\|memory" logs/log-*.txt
```

```sql
-- Check table fragmentation
SELECT table_name, num_rows, blocks, empty_blocks
FROM user_tables
WHERE table_name = 'SYS_AUDIT_LOG';

-- Rebuild indexes if needed
ALTER INDEX IDX_AUDIT_LOG_CORRELATION REBUILD ONLINE;
```

**Resolution:**
- Profile application for memory leaks
- Rebuild fragmented indexes
- Increase database buffer cache
- Consider table partitioning

### Issue: Queue Depth Exceeds 10,000

**Symptoms:**
- `audit_queue_depth` exceeds 10,000
- Backpressure warnings in logs

**Possible Causes:**
1. Database write performance too slow
2. Batch size too small
3. Batch window too large
4. Database connection issues

**Investigation Steps:**

```bash
# Check for backpressure warnings
grep -i "backpressure\|queue" logs/log-*.txt
```

```sql
-- Check slow audit log inserts
SELECT sql_text, elapsed_time, executions
FROM V$SQL
WHERE sql_text LIKE '%SYS_AUDIT_LOG%'
ORDER BY elapsed_time DESC;
```

**Resolution:**
- Increase `BatchSize` (try 100 or 200)
- Decrease `BatchWindowMs` (try 50ms)
- Optimize audit log table indexes
- Increase database write performance

### Issue: Memory Leak (> 50% growth)

**Symptoms:**
- Memory usage grows continuously
- Application becomes slower over time
- Eventually runs out of memory

**Investigation Steps:**

```bash
# Monitor memory usage during test
watch -n 10 'ps aux | grep ThinkOnErp'

# Check for memory dumps
ls -lh /tmp/coredump*
```

**Resolution:**
- Profile application with dotMemory or similar
- Check for unclosed database connections
- Review event handler subscriptions
- Check for large object retention in caches

## Post-Test Analysis

### 1. Database Verification

After the test completes, verify data integrity:

```sql
-- Count audit log entries created during test
SELECT COUNT(*) 
FROM SYS_AUDIT_LOG
WHERE CREATION_DATE >= SYSDATE - INTERVAL '2' HOUR;

-- Expected: ~600,000 entries

-- Verify no data loss
SELECT 
    EVENT_CATEGORY,
    COUNT(*) as entry_count
FROM SYS_AUDIT_LOG
WHERE CREATION_DATE >= SYSDATE - INTERVAL '2' HOUR
GROUP BY EVENT_CATEGORY;

-- Check for any errors in audit logs
SELECT COUNT(*)
FROM SYS_AUDIT_LOG
WHERE SEVERITY = 'Error'
AND CREATION_DATE >= SYSDATE - INTERVAL '2' HOUR;
```

### 2. Performance Analysis

Analyze the JSON results file:

```bash
# Extract key metrics
jq '.metrics | {
  http_req_duration: .http_req_duration,
  api_response_time_ms: .api_response_time_ms,
  error_rate: .error_rate,
  requests_per_minute: .requests_per_minute
}' results/sustained-*.json
```

### 3. Generate Report

Create a summary report:

```markdown
# Sustained Load Test Results - [Date]

## Test Configuration
- Target Load: 10,000 requests/minute
- Sustained Duration: 60 minutes
- Total Duration: 75 minutes
- API Version: [version]
- Database: Oracle [version]

## Results Summary

### Performance Metrics
- ✅ API Response Time (p99): [X]ms (target: <10ms)
- ✅ HTTP Request Duration (p99): [X]ms (target: <500ms)
- ✅ Error Rate: [X]% (target: <1%)
- ✅ Total Requests: [X] (expected: ~600,000)

### Stability Metrics
- ✅ Performance Degradation: [X]% (target: <20%)
- ✅ Memory Growth: [X]% (target: <50%)
- ✅ Max Queue Depth: [X] (target: <10,000)

## Observations
- [Any notable observations during the test]

## Issues Encountered
- [Any issues and how they were resolved]

## Recommendations
- [Any recommendations for optimization or configuration changes]

## Conclusion
[Pass/Fail] - The system [does/does not] meet the sustained load requirements.
```

## Best Practices

### Before Running

1. **Schedule Appropriately**: Run during off-peak hours if testing production
2. **Notify Team**: Inform team members about the test
3. **Backup Data**: Ensure recent database backup exists
4. **Monitor Resources**: Set up monitoring dashboards
5. **Document Baseline**: Record current system metrics

### During Test

1. **Don't Interrupt**: Let the test run to completion
2. **Monitor Continuously**: Watch for anomalies
3. **Log Observations**: Document any unusual behavior
4. **Take Snapshots**: Capture system state at intervals
5. **Be Ready to Stop**: Have a plan to abort if critical issues arise

### After Test

1. **Verify Data**: Check database integrity
2. **Analyze Results**: Review all metrics thoroughly
3. **Document Findings**: Create comprehensive report
4. **Share Results**: Distribute findings to team
5. **Plan Improvements**: Identify optimization opportunities

## Next Steps

After successful sustained load testing:

1. ✅ Document results in task completion summary
2. ✅ Update performance baseline metrics
3. ✅ Configure continuous performance monitoring
4. ✅ Set up alerts for performance degradation
5. ✅ Schedule regular sustained load tests (monthly)
6. ✅ Proceed to spike load testing (Task 20.6)
7. ✅ Proceed to memory pressure testing (Task 20.7)

## References

- [Load Testing README](README.md)
- [Full Traceability System Requirements](.kiro/specs/full-traceability-system/requirements.md)
- [Performance Requirements](.kiro/specs/full-traceability-system/design.md#performance-requirements)
- [k6 Documentation](https://k6.io/docs/)
- [k6 Thresholds](https://k6.io/docs/using-k6/thresholds/)
