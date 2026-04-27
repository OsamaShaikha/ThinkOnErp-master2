# Load Testing for Full Traceability System

This directory contains load testing scripts to validate the performance requirements of the Full Traceability System.

## Requirements Being Validated

From the Full Traceability System specification (Requirement 13):

1. **System SHALL support logging 10,000 requests per minute** without degrading API response times
2. **System SHALL add no more than 10ms latency** to API requests for 99% of operations
3. **Audit Logger SHALL use asynchronous writes** to avoid blocking API request processing

## Prerequisites

### 1. Install k6

k6 is a modern load testing tool built for developers.

**macOS:**
```bash
brew install k6
```

**Windows (using Chocolatey):**
```bash
choco install k6
```

**Windows (using Scoop):**
```bash
scoop install k6
```

**Linux:**
```bash
sudo gpg -k
sudo gpg --no-default-keyring --keyring /usr/share/keyrings/k6-archive-keyring.gpg --keyserver hkp://keyserver.ubuntu.com:80 --recv-keys C5AD17C747E3415A3642D57D77C6C491D6AC1D69
echo "deb [signed-by=/usr/share/keyrings/k6-archive-keyring.gpg] https://dl.k6.io/deb stable main" | sudo tee /etc/apt/sources.list.d/k6.list
sudo apt-get update
sudo apt-get install k6
```

**Docker:**
```bash
docker pull grafana/k6:latest
```

For more installation options, visit: https://k6.io/docs/getting-started/installation/

### 2. Start the ThinkOnErp API

Ensure the API is running before executing load tests:

```bash
# From the project root
cd src/ThinkOnErp.API
dotnet run
```

The API should be accessible at `http://localhost:5000` (or your configured URL).

### 3. Obtain a JWT Token

You need a valid JWT token for authentication. You can either:

**Option A: Use the default superadmin credentials**
The load test script will automatically authenticate using:
- Username: `superadmin`
- Password: `SuperAdmin123!`

**Option B: Provide your own JWT token**
```bash
# Login via API to get a token
curl -X POST http://localhost:5000/api/auth/login \
  -H "Content-Type: application/json" \
  -d '{"userName":"superadmin","password":"SuperAdmin123!"}'

# Copy the token from the response
export JWT_TOKEN="your-token-here"
```

## Available Load Tests

This directory contains multiple load test scenarios:

### 1. Standard Load Test (23 minutes)

**File**: `load-test-10k-rpm.js`

Validates that the system can handle 10,000 requests per minute with a 10-minute sustained period.

- Ramp up: 10 minutes
- Sustained: 10 minutes at 10,000 req/min
- Ramp down: 3 minutes
- **Total duration**: ~23 minutes

### 2. Sustained Load Test (75 minutes)

**File**: `sustained-load-test-1hour.js`

Validates that the system maintains performance over extended periods (1 hour) without degradation.

- Ramp up: 10 minutes
- Sustained: **60 minutes** at 10,000 req/min
- Ramp down: 5 minutes
- **Total duration**: ~75 minutes
- **Expected requests**: ~600,000

**Key Features**:
- Performance degradation detection (first 10 min vs last 10 min)
- Memory leak detection
- Queue depth monitoring
- Comprehensive stability validation

**See**: [SUSTAINED_LOAD_TEST_GUIDE.md](SUSTAINED_LOAD_TEST_GUIDE.md) for detailed instructions.

### 3. Spike Load Test (17 minutes) ⭐ NEW

**File**: `spike-load-test-50k-rpm.js`

Validates that the system can handle sudden traffic bursts up to 50,000 requests per minute and recover gracefully.

- Baseline: 5 minutes at 10,000 req/min
- Spike: **3.5 minutes at 50,000 req/min** (5x increase)
- Recovery: 5 minutes at 10,000 req/min
- Ramp down: 2 minutes
- **Total duration**: ~17 minutes
- **Expected requests**: ~275,000

**Key Features**:
- Sudden 5x traffic increase validation
- Queue backpressure mechanism testing
- System recovery time measurement
- Phase-based metrics tracking (baseline, spike, recovery)
- Memory exhaustion prevention validation

**See**: [SPIKE_LOAD_TEST_GUIDE.md](SPIKE_LOAD_TEST_GUIDE.md) for detailed instructions.

## Running the Load Tests

### Quick Start with Helper Scripts

**Sustained Load Test (Recommended for comprehensive validation):**

```bash
# Windows (PowerShell)
.\run-sustained-load-test.ps1

# Linux/macOS
./run-sustained-load-test.sh
```

**Spike Load Test (Validates burst handling and recovery):**

```bash
# Windows (PowerShell)
.\run-spike-load-test.ps1

# Linux/macOS
./run-spike-load-test.sh
```

**Standard Load Test:**

```bash
k6 run load-test-10k-rpm.js
```

### Basic Test Run

Run the standard load test with default settings (10,000 requests per minute target):

```bash
k6 run load-test-10k-rpm.js
```

Run the sustained load test (1 hour at target load):

```bash
k6 run sustained-load-test-1hour.js
```

Run the spike load test (burst to 50,000 req/min):

```bash
k6 run spike-load-test-50k-rpm.js
```

### Custom Configuration

**Specify API URL:**
```bash
k6 run --env API_URL=http://your-api-url:port load-test-10k-rpm.js
```

**Provide JWT Token:**
```bash
k6 run --env JWT_TOKEN="your-jwt-token" load-test-10k-rpm.js
```

**Adjust Virtual Users and Duration:**
```bash
# Run with 100 VUs for 5 minutes
k6 run --vus 100 --duration 5m load-test-10k-rpm.js
```

**Run with Docker:**
```bash
docker run --rm -i grafana/k6:latest run - <load-test-10k-rpm.js
```

### Output to File

Save results to a JSON file for later analysis:

```bash
k6 run --out json=results.json load-test-10k-rpm.js
```

## Test Scenarios

The load test includes the following scenario:

### Sustained Load Test (23 minutes total)

1. **Ramp Up (10 minutes)**
   - 0-2 min: Ramp from 100 to 1,000 requests/minute
   - 2-5 min: Ramp from 1,000 to 5,000 requests/minute
   - 5-10 min: Ramp from 5,000 to 10,000 requests/minute

2. **Sustained Load (10 minutes)**
   - Maintain 10,000 requests/minute

3. **Ramp Down (3 minutes)**
   - 10-12 min: Ramp down to 1,000 requests/minute
   - 12-13 min: Ramp down to 0

## API Operations Tested

The load test simulates realistic usage patterns with a mix of operations:

| Operation | Weight | Description |
|-----------|--------|-------------|
| GET /api/companies | 30% | Retrieve company list |
| GET /api/users | 25% | Retrieve user list |
| GET /api/roles | 15% | Retrieve role list |
| GET /api/currencies | 10% | Retrieve currency list |
| GET /api/branches | 10% | Retrieve branch list |
| POST /api/companies | 5% | Create new company (write operation) |
| GET /api/auditlogs | 5% | Query audit logs |

This distribution simulates a realistic workload with mostly read operations and some write operations that trigger audit logging.

## Performance Thresholds

The test validates the following performance requirements:

### API Response Time (Audit Overhead)
- **p99 < 10ms** ✅ CRITICAL - 99% of operations must add less than 10ms latency
- **p95 < 8ms** ✅ 95% of operations should add less than 8ms latency
- **p50 < 5ms** ✅ 50% of operations should add less than 5ms latency

### HTTP Request Duration (Total)
- **p99 < 500ms** ✅ 99% of requests should complete within 500ms
- **p95 < 300ms** ✅ 95% of requests should complete within 300ms
- **avg < 200ms** ✅ Average response time should be under 200ms

### Error Rate
- **< 1%** ✅ Less than 1% of requests should fail

## Interpreting Results

After the test completes, k6 will display a summary report. Key metrics to review:

### ✅ Success Criteria

```
✓ api_response_time_ms............: p(99)=8.5ms   (PASS if <10ms)
✓ http_req_duration................: p(99)=450ms   (PASS if <500ms)
✓ error_rate.......................: 0.5%          (PASS if <1%)
✓ requests_per_minute..............: 10,000        (PASS if ≥10,000)
```

### 📊 Additional Metrics

- **http_reqs**: Total number of HTTP requests made
- **http_req_failed**: Percentage of failed requests
- **http_req_duration**: Request duration statistics (min, avg, max, p90, p95, p99)
- **vus**: Number of virtual users active
- **vus_max**: Maximum number of virtual users reached

### ⚠️ Warning Signs

If you see any of these, investigate further:

- **p99 > 10ms for api_response_time_ms**: Audit logging is adding too much overhead
- **error_rate > 1%**: System is experiencing failures under load
- **http_req_duration p99 > 500ms**: Overall API performance is degrading
- **http_req_failed > 1%**: High failure rate indicates system instability

## Monitoring During Load Tests

While the load test is running, monitor the following:

### 1. Application Logs

```bash
tail -f logs/log-*.txt
```

Look for:
- Audit logging errors
- Database connection pool exhaustion
- Queue backpressure warnings
- Exception spikes

### 2. Database Performance

Connect to Oracle and monitor:

```sql
-- Active sessions
SELECT COUNT(*) FROM V$SESSION WHERE STATUS = 'ACTIVE';

-- Connection pool usage
SELECT * FROM V$RESOURCE_LIMIT WHERE RESOURCE_NAME = 'processes';

-- Slow queries
SELECT sql_text, elapsed_time, executions
FROM V$SQL
WHERE elapsed_time > 500000
ORDER BY elapsed_time DESC;

-- Audit log table size
SELECT COUNT(*) FROM SYS_AUDIT_LOG;
```

### 3. System Resources

Monitor CPU, memory, and disk I/O:

```bash
# Linux/macOS
top
htop
iostat -x 1

# Windows
Task Manager or Performance Monitor
```

### 4. Audit Queue Depth

If you have monitoring endpoints enabled:

```bash
curl http://localhost:5000/api/monitoring/health
```

Look for:
- Audit queue depth (should stay below 10,000)
- Batch processing rate
- Database write latency

## Troubleshooting

### Test Fails to Start

**Error: "Failed to authenticate"**
- Ensure the API is running at the specified URL
- Verify the superadmin credentials are correct
- Check that JWT authentication is properly configured

**Error: "Connection refused"**
- Verify the API URL is correct
- Ensure the API is running and accessible
- Check firewall settings

### Performance Thresholds Not Met

**p99 > 10ms for audit overhead**
- Check if audit logging is synchronous (should be async)
- Verify batch processing is enabled
- Check database connection pool size
- Review audit logger queue depth

**High error rate**
- Check application logs for exceptions
- Monitor database connection pool exhaustion
- Verify database is not overloaded
- Check for timeout errors

**Slow HTTP request duration**
- Profile slow endpoints
- Check database query performance
- Review connection pool configuration
- Monitor system resources (CPU, memory, disk)

### Database Issues

**Connection pool exhaustion**
- Increase `Max Pool Size` in connection string
- Reduce `Min Pool Size` if too high
- Check for connection leaks

**Slow queries**
- Review query execution plans
- Ensure indexes are in place
- Consider partitioning for large tables

## Advanced Testing

### Spike Testing

Test how the system handles sudden traffic spikes:

**Use the dedicated spike load test**:
```bash
k6 run spike-load-test-50k-rpm.js
```

This test validates:
- Sudden burst to 50,000 req/min (5x normal load)
- Queue backpressure mechanisms
- System recovery after spike
- Error rate management during extreme load

**See**: [SPIKE_LOAD_TEST_GUIDE.md](SPIKE_LOAD_TEST_GUIDE.md) for detailed instructions.

**Custom spike test** (modify stages in any test script):
```javascript
// Modify the stages in load-test-10k-rpm.js
stages: [
    { duration: '1m', target: 1000 },
    { duration: '30s', target: 20000 },  // Sudden spike
    { duration: '2m', target: 20000 },   // Sustain spike
    { duration: '1m', target: 1000 },    // Return to normal
]
```

### Stress Testing

Find the breaking point of the system:

```javascript
stages: [
    { duration: '2m', target: 10000 },
    { duration: '5m', target: 20000 },
    { duration: '5m', target: 30000 },
    { duration: '5m', target: 40000 },
    // Continue until system fails
]
```

### Soak Testing

Test system stability over extended periods:

```javascript
stages: [
    { duration: '5m', target: 10000 },
    { duration: '4h', target: 10000 },  // 4 hours sustained
    { duration: '5m', target: 0 },
]
```

## Results Documentation

After completing load tests, document the results:

### 1. Create a Results Summary

```markdown
# Load Test Results - [Date]

## Test Configuration
- Target: 10,000 requests/minute
- Duration: 23 minutes
- API Version: [version]
- Database: Oracle [version]

## Results
- ✅ p99 audit overhead: 8.5ms (target: <10ms)
- ✅ p99 request duration: 450ms (target: <500ms)
- ✅ Error rate: 0.5% (target: <1%)
- ✅ Throughput: 10,000 req/min achieved

## Observations
- [Any notable observations]

## Recommendations
- [Any recommendations for optimization]
```

### 2. Save Raw Results

```bash
# Save JSON output
k6 run --out json=results-$(date +%Y%m%d-%H%M%S).json load-test-10k-rpm.js

# Save summary to file
k6 run load-test-10k-rpm.js > results-$(date +%Y%m%d-%H%M%S).txt 2>&1
```

### 3. Generate Reports

Use k6 Cloud or Grafana for visual reports:

```bash
# Upload to k6 Cloud (requires account)
k6 run --out cloud load-test-10k-rpm.js

# Export to InfluxDB + Grafana
k6 run --out influxdb=http://localhost:8086/k6 load-test-10k-rpm.js
```

## Next Steps

After successful load testing:

1. ✅ Document results in task completion summary
2. ✅ Update performance baseline metrics
3. ✅ Configure continuous performance monitoring
4. ✅ Set up alerts for performance degradation
5. ✅ Schedule regular load tests (weekly/monthly)

## Batch Parameter Validation

In addition to the comprehensive k6 load tests, we provide scripts to validate the batch processing parameters specifically:

### PowerShell Script (Windows)

```powershell
cd tests\LoadTests
.\validate-batch-parameters.ps1
```

**With custom parameters:**
```powershell
.\validate-batch-parameters.ps1 -ApiUrl "http://localhost:5000" -Duration 120 -OutputFile "results.json"
```

### Bash Script (Linux/macOS)

```bash
cd tests/LoadTests
chmod +x validate-batch-parameters.sh
./validate-batch-parameters.sh
```

**With custom parameters:**
```bash
./validate-batch-parameters.sh http://localhost:5000 "your-jwt-token" 120
```

### What These Scripts Test

The batch parameter validation scripts test the system at four different load levels:

1. **Low Load**: 100 requests/minute
2. **Medium Load**: 1,000 requests/minute
3. **High Load**: 5,000 requests/minute
4. **Target Load**: 10,000 requests/minute

For each load level, the scripts measure:
- Total requests and success rate
- Response time percentiles (P50, P95, P99)
- Error rate
- Performance degradation across load levels

### Expected Results

With the current batch parameters (BatchSize=50, BatchWindowMs=100):

```
✓ Throughput: System handled 10,000 req/min successfully (>99%)
✓ Error Rate: <1%
✓ Performance Degradation: <50% increase from low to high load
```

## References

- [k6 Documentation](https://k6.io/docs/)
- [k6 Best Practices](https://k6.io/docs/testing-guides/test-types/)
- [Full Traceability System Specification](.kiro/specs/full-traceability-system/requirements.md)
- [Performance Requirements](.kiro/specs/full-traceability-system/design.md#performance-requirements)
- [Batch Processing Optimization](../../TASK_11_8_BATCH_PROCESSING_OPTIMIZATION.md)
