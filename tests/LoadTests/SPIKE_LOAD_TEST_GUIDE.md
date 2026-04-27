# Spike Load Test Guide - 50,000 Requests Per Minute

## Overview

This guide covers the spike load testing for the Full Traceability System, validating that the system can handle sudden traffic bursts up to 50,000 requests per minute and recover gracefully.

## Purpose

The spike load test validates:

1. **Spike Handling**: System can handle sudden 5x traffic increase (10k → 50k req/min)
2. **Queue Backpressure**: Audit queue mechanisms prevent memory exhaustion
3. **System Resilience**: No crashes or unresponsiveness during spike
4. **Recovery**: System returns to normal performance after spike
5. **Error Tolerance**: Error rate remains acceptable (<5%) during spike

## Test Scenario

### Test Phases

```
Phase 1: Baseline (5 minutes)
├─ Establish baseline at 10,000 req/min
└─ Measure normal system performance

Phase 2: Spike (3.5 minutes)
├─ Rapid increase to 50,000 req/min (30 seconds)
├─ Sustain spike for 3 minutes
└─ Measure system behavior under extreme load

Phase 3: Recovery (5 minutes)
├─ Quick drop back to 10,000 req/min (1 minute)
├─ Monitor system recovery
└─ Verify return to baseline performance

Phase 4: Ramp Down (2 minutes)
└─ Graceful shutdown
```

### Load Profile

```
Requests/Min
50,000 |           ┌─────────┐
       |          /           \
       |         /             \
10,000 |────────/               \────────
       |                                 \
     0 |                                  ─────
       └─────────────────────────────────────────> Time
       0   5   8.5  11.5  13.5  15.5  17 (minutes)
```

### Total Test Duration

- **Total**: ~17 minutes
- **Baseline**: 5 minutes
- **Spike**: 3.5 minutes (30s ramp + 3min sustained)
- **Recovery**: 5 minutes
- **Ramp Down**: 2 minutes

### Expected Request Volume

- **Baseline Phase**: ~50,000 requests
- **Spike Phase**: ~175,000 requests
- **Recovery Phase**: ~50,000 requests
- **Total**: ~275,000 requests

## Prerequisites

### 1. System Requirements

**Minimum Hardware**:
- CPU: 8+ cores
- RAM: 16+ GB
- Database: Oracle with sufficient connection pool (100+ connections)
- Network: Low latency, high bandwidth

**Recommended Hardware**:
- CPU: 16+ cores
- RAM: 32+ GB
- Database: Oracle with large connection pool (200+ connections)
- SSD storage for database and logs

### 2. Software Requirements

- k6 installed ([installation guide](https://k6.io/docs/getting-started/installation/))
- ThinkOnErp API running and accessible
- Oracle database with sufficient capacity
- Monitoring tools (optional but recommended)

### 3. Configuration Recommendations

**API Configuration** (`appsettings.json`):

```json
{
  "AuditLogging": {
    "Enabled": true,
    "BatchSize": 50,
    "BatchWindowMs": 100,
    "MaxQueueSize": 50000,  // Increased for spike handling
    "EnableBackpressure": true
  },
  "ConnectionStrings": {
    "OracleConnection": "...;Max Pool Size=200;Min Pool Size=20;..."
  }
}
```

**Database Configuration**:

```sql
-- Ensure sufficient processes
ALTER SYSTEM SET processes=300 SCOPE=SPFILE;

-- Ensure sufficient sessions
ALTER SYSTEM SET sessions=400 SCOPE=SPFILE;

-- Restart database for changes to take effect
```

## Running the Test

### Quick Start

**Windows (PowerShell)**:
```powershell
cd tests\LoadTests
.\run-spike-load-test.ps1
```

**Linux/macOS**:
```bash
cd tests/LoadTests
chmod +x run-spike-load-test.sh
./run-spike-load-test.sh
```

### Custom Configuration

**Specify API URL**:
```bash
# PowerShell
.\run-spike-load-test.ps1 -ApiUrl "http://your-api-url:port"

# Bash
./run-spike-load-test.sh http://your-api-url:port
```

**Provide JWT Token**:
```bash
# PowerShell
.\run-spike-load-test.ps1 -JwtToken "your-jwt-token"

# Bash
./run-spike-load-test.sh http://localhost:5000 "your-jwt-token"
```

**Save Results to File**:
```bash
# PowerShell
.\run-spike-load-test.ps1 -OutputFile "spike-results.json"

# Bash
./run-spike-load-test.sh http://localhost:5000 "" spike-results.json
```

### Direct k6 Execution

```bash
# Basic execution
k6 run spike-load-test-50k-rpm.js

# With custom API URL
k6 run --env API_URL=http://your-api-url spike-load-test-50k-rpm.js

# With output file
k6 run --out json=spike-results.json spike-load-test-50k-rpm.js
```

## Monitoring During Test

### 1. Real-Time Metrics

Watch the k6 output for real-time metrics:

```
✓ api_response_time_ms............: p(99)=45ms
✓ http_req_duration................: p(99)=1.2s
✓ error_rate.......................: 2.5%
✓ audit_queue_depth................: 8,500
```

### 2. Application Logs

Monitor application logs for warnings or errors:

```bash
# Tail logs in real-time
tail -f logs/log-*.txt

# Watch for specific patterns
tail -f logs/log-*.txt | grep -E "(ERROR|WARNING|Queue depth)"
```

### 3. Database Monitoring

Monitor database performance during the test:

```sql
-- Active sessions
SELECT COUNT(*) as active_sessions
FROM V$SESSION
WHERE STATUS = 'ACTIVE';

-- Connection pool usage
SELECT RESOURCE_NAME, CURRENT_UTILIZATION, MAX_UTILIZATION, LIMIT_VALUE
FROM V$RESOURCE_LIMIT
WHERE RESOURCE_NAME IN ('processes', 'sessions');

-- Audit log table growth
SELECT COUNT(*) as total_audit_logs,
       COUNT(CASE WHEN CREATION_DATE > SYSDATE - INTERVAL '1' HOUR THEN 1 END) as recent_logs
FROM SYS_AUDIT_LOG;

-- Slow queries during test
SELECT sql_text, elapsed_time/1000000 as elapsed_seconds, executions
FROM V$SQL
WHERE elapsed_time > 1000000
ORDER BY elapsed_time DESC
FETCH FIRST 10 ROWS ONLY;
```

### 4. System Resources

Monitor system resources:

```bash
# Linux/macOS
top
htop
iostat -x 1

# Windows
# Use Task Manager or Performance Monitor
```

### 5. Health Endpoint (if available)

```bash
# Check system health during test
watch -n 5 'curl -s http://localhost:5000/api/monitoring/health | jq'
```

## Success Criteria

### ✅ Primary Success Criteria

1. **System Stability**
   - ✓ System handles spike without crashing
   - ✓ No unresponsiveness or timeouts
   - ✓ All services remain operational

2. **Error Rate**
   - ✓ Overall error rate < 5%
   - ✓ Error rate during spike < 5%
   - ✓ Error rate after recovery < 1%

3. **Queue Backpressure**
   - ✓ Queue depth remains manageable (< 50,000)
   - ✓ No memory exhaustion
   - ✓ Backpressure mechanisms activate correctly

4. **Recovery**
   - ✓ System returns to baseline performance within 5 minutes
   - ✓ Response times normalize after spike
   - ✓ Error rate returns to < 1%

### 📊 Performance Metrics

**During Baseline (10,000 req/min)**:
- p99 response time: < 10ms
- p95 response time: < 8ms
- Error rate: < 1%

**During Spike (50,000 req/min)**:
- p99 response time: < 50ms (acceptable degradation)
- p95 response time: < 30ms
- Error rate: < 5% (spike tolerance)

**After Recovery (10,000 req/min)**:
- p99 response time: < 10ms (back to baseline)
- p95 response time: < 8ms
- Error rate: < 1%

## Interpreting Results

### Example Success Output

```
✓ api_response_time_ms............: p(99)=42ms   ✓ PASS (<50ms)
✓ http_req_duration................: p(99)=1.8s   ✓ PASS (<2s)
✓ error_rate.......................: 3.2%         ✓ PASS (<5%)
✓ error_rate_during_spike..........: 4.1%         ✓ PASS (<5%)
✓ error_rate_after_spike...........: 0.8%         ✓ PASS (<1%)
✓ response_time_after_spike........: p(95)=285ms  ✓ PASS (<300ms)
✓ audit_queue_depth................: max=12,500   ✓ PASS (<50,000)
```

### Phase-by-Phase Analysis

**1. Baseline Phase (0-5 minutes)**:
- Establishes normal performance metrics
- Should show consistent low latency
- Error rate should be minimal (<1%)

**2. Spike Phase (5-8.5 minutes)**:
- Response times will increase (expected)
- Error rate may increase but should stay <5%
- Queue depth will grow but should be managed
- System should remain responsive

**3. Recovery Phase (8.5-13.5 minutes)**:
- Response times should gradually return to baseline
- Error rate should decrease back to <1%
- Queue depth should drain
- System should stabilize

### Warning Signs

⚠️ **Investigate if you see**:

1. **High Error Rate**:
   - Error rate > 5% during spike
   - Error rate > 1% after recovery
   - Indicates system overload or configuration issues

2. **Unbounded Queue Growth**:
   - Queue depth > 50,000
   - Continuously growing queue
   - Indicates backpressure not working

3. **No Recovery**:
   - Response times don't return to baseline
   - Error rate remains elevated
   - Indicates system degradation or resource leak

4. **System Crashes**:
   - API becomes unresponsive
   - Database connection failures
   - Out of memory errors

## Troubleshooting

### Issue: High Error Rate During Spike

**Symptoms**:
- Error rate > 5% during spike phase
- Many 500 or 503 responses

**Possible Causes**:
1. Database connection pool exhausted
2. Insufficient system resources (CPU, memory)
3. Audit queue overflow
4. Network bandwidth limitations

**Solutions**:
```json
// Increase connection pool size
"ConnectionStrings": {
  "OracleConnection": "...;Max Pool Size=300;..."
}

// Increase queue size
"AuditLogging": {
  "MaxQueueSize": 100000
}
```

### Issue: Queue Depth Exceeds Limits

**Symptoms**:
- Queue depth > 50,000
- Memory usage growing continuously
- Out of memory errors

**Possible Causes**:
1. Batch processing too slow
2. Database write bottleneck
3. Insufficient database capacity

**Solutions**:
```json
// Optimize batch processing
"AuditLogging": {
  "BatchSize": 100,        // Larger batches
  "BatchWindowMs": 50,     // Faster processing
  "EnableBackpressure": true
}
```

### Issue: System Doesn't Recover

**Symptoms**:
- Response times remain elevated after spike
- Error rate doesn't return to baseline
- Queue depth doesn't drain

**Possible Causes**:
1. Resource leak (memory, connections)
2. Database performance degradation
3. Insufficient recovery time

**Solutions**:
1. Check for connection leaks
2. Monitor database performance
3. Increase recovery phase duration
4. Review application logs for errors

### Issue: Database Connection Failures

**Symptoms**:
- "ORA-12516: TNS:listener could not find available handler"
- "Connection pool exhausted"

**Solutions**:
```sql
-- Increase database processes
ALTER SYSTEM SET processes=400 SCOPE=SPFILE;
ALTER SYSTEM SET sessions=500 SCOPE=SPFILE;

-- Restart database
SHUTDOWN IMMEDIATE;
STARTUP;
```

## Best Practices

### Before Running the Test

1. **Backup Database**: Ensure recent backup exists
2. **Clear Old Data**: Archive or delete old audit logs
3. **Monitor Resources**: Set up monitoring dashboards
4. **Notify Team**: Inform team of load test schedule
5. **Test Environment**: Run in staging first

### During the Test

1. **Monitor Continuously**: Watch metrics in real-time
2. **Log Everything**: Capture all logs and metrics
3. **Don't Interrupt**: Let test complete fully
4. **Document Issues**: Note any anomalies immediately

### After the Test

1. **Analyze Results**: Review all metrics thoroughly
2. **Check Logs**: Look for errors or warnings
3. **Verify Recovery**: Ensure system is stable
4. **Document Findings**: Record results and observations
5. **Clean Up**: Archive test data if needed

## Results Documentation Template

```markdown
# Spike Load Test Results - [Date]

## Test Configuration
- API Version: [version]
- Database: Oracle [version]
- Test Duration: 17 minutes
- Spike Target: 50,000 req/min
- Spike Duration: 3 minutes

## Results Summary

### Overall Metrics
- Total Requests: [count]
- Success Rate: [percentage]
- Error Rate: [percentage]
- Peak Queue Depth: [count]

### Phase-by-Phase Results

**Baseline Phase (10,000 req/min)**:
- p99 response time: [ms]
- Error rate: [percentage]
- Status: ✓ PASS / ✗ FAIL

**Spike Phase (50,000 req/min)**:
- p99 response time: [ms]
- Error rate: [percentage]
- Peak queue depth: [count]
- Status: ✓ PASS / ✗ FAIL

**Recovery Phase (10,000 req/min)**:
- p99 response time: [ms]
- Error rate: [percentage]
- Recovery time: [minutes]
- Status: ✓ PASS / ✗ FAIL

## Success Criteria Validation

- [ ] System handled spike without crashing
- [ ] Error rate during spike < 5%
- [ ] Queue backpressure prevented memory exhaustion
- [ ] System recovered to baseline within 5 minutes

## Observations

[Any notable observations during the test]

## Issues Encountered

[Any issues or anomalies]

## Recommendations

[Recommendations for optimization or configuration changes]

## Conclusion

[Overall assessment of system resilience under spike load]
```

## Next Steps

After successful spike load testing:

1. ✅ Document results using template above
2. ✅ Update performance baseline metrics
3. ✅ Configure alerts for queue depth thresholds
4. ✅ Implement auto-scaling if needed
5. ✅ Schedule regular spike tests (monthly)
6. ✅ Share results with team and stakeholders

## References

- [k6 Documentation](https://k6.io/docs/)
- [k6 Spike Testing Guide](https://k6.io/docs/test-types/spike-testing/)
- [Full Traceability System Specification](../../.kiro/specs/full-traceability-system/requirements.md)
- [Load Testing README](README.md)
- [Sustained Load Test Guide](SUSTAINED_LOAD_TEST_GUIDE.md)
