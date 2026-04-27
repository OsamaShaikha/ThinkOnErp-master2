# Sample Load Test Results

This document shows example output from successful load testing runs.

## k6 Load Test Results (Successful)

```
          /\      |‾‾| /‾‾/   /‾‾/   
     /\  /  \     |  |/  /   /  /    
    /  \/    \    |     (   /   ‾‾\  
   /          \   |  |\  \ |  (‾)  | 
  / __________ \  |__| \__\ \_____/ .io

  execution: local
    script: load-test-10k-rpm.js
    output: -

  scenarios: (100.00%) 1 scenario, 200 max VUs, 23m30s max duration (incl. graceful stop):
           * sustained_load: Up to 10000.00 iterations/min for 23m0s (maxVUs: 50-200, gracefulStop: 30s)


running (23m03.5s), 000/200 VUs, 230000 complete and 0 interrupted iterations
sustained_load ✓ [======================================] 000/200 VUs  23m0s  10000 iters/min

     ✓ GET /api/companies status is 200
     ✓ GET /api/companies has correlation ID
     ✓ GET /api/users status is 200
     ✓ GET /api/users has correlation ID
     ✓ GET /api/roles status is 200
     ✓ GET /api/roles has correlation ID
     ✓ GET /api/currencies status is 200
     ✓ GET /api/currencies has correlation ID
     ✓ GET /api/branches status is 200
     ✓ GET /api/branches has correlation ID
     ✓ POST /api/companies status is 200 or 201
     ✓ POST /api/companies has correlation ID
     ✓ GET /api/auditlogs status is 200
     ✓ GET /api/auditlogs has correlation ID

     checks.........................: 100.00% ✓ 3220000     ✗ 0      
     data_received..................: 2.3 GB  1.7 MB/s
     data_sent......................: 115 MB  83 kB/s
     http_req_blocked...............: avg=1.2µs    min=0s       med=1µs      max=15.2ms   p(90)=2µs      p(95)=2µs     
     http_req_connecting............: avg=0s       min=0s       med=0s       max=8.5ms    p(90)=0s       p(95)=0s      
   ✓ http_req_duration..............: avg=185.5ms  min=12.3ms   med=165ms    max=892ms    p(90)=285ms    p(95)=325ms   
       { expected_response:true }...: avg=185.5ms  min=12.3ms   med=165ms    max=892ms    p(90)=285ms    p(95)=325ms   
   ✓ http_req_failed................: 0.50%   ✓ 1150        ✗ 228850 
     http_req_receiving.............: avg=125µs    min=15µs     med=95µs     max=45.2ms   p(90)=185µs    p(95)=225µs   
     http_req_sending...............: avg=45µs     min=5µs      med=35µs     max=12.5ms   p(90)=75µs     p(95)=95µs    
     http_req_tls_handshaking.......: avg=0s       min=0s       med=0s       max=0s       p(90)=0s       p(95)=0s      
     http_req_waiting...............: avg=185.3ms  min=12.1ms   med=164.8ms  max=891.5ms  p(90)=284.8ms  p(95)=324.8ms 
     http_reqs......................: 230000  167.2/s
     iteration_duration.............: avg=1.18s    min=12.5ms   med=1.16s    max=2.95s    p(90)=1.28s    p(95)=1.35s   
     iterations.....................: 230000  167.2/s
     vus............................: 1       min=1         max=200  
     vus_max........................: 200     min=200       max=200  

   ✓ api_response_time_ms...........: avg=6.2ms    min=0.5ms    med=5.8ms    max=12.5ms   p(90)=8.2ms    p(95)=8.8ms   
       p(99)=9.2ms    ✓ PASS (threshold: p(99)<10ms)
   ✓ audit_overhead.................: avg=5.8ms    min=0.3ms    med=5.5ms    max=11.8ms   p(90)=7.8ms    p(95)=8.5ms   
   ✓ error_rate.....................: 0.50%   ✓ 1150        ✗ 228850 
   ✓ requests_per_minute............: 10,020  (avg over test duration)


=== Load Test Complete ===
Check the summary report above for detailed metrics.
Key metrics to review:
  - api_response_time_ms (p99 should be <10ms) ✓ PASS: 9.2ms
  - http_req_duration (p99 should be <500ms) ✓ PASS: 450ms
  - error_rate (should be <1%) ✓ PASS: 0.5%
  - requests_per_minute (should reach 10,000) ✓ PASS: 10,020
==========================

All performance thresholds met! ✓
```

### Key Metrics Explained

| Metric | Value | Threshold | Status |
|--------|-------|-----------|--------|
| **Throughput** | 10,020 req/min | ≥ 10,000 | ✅ PASS |
| **Audit Overhead (p99)** | 9.2ms | < 10ms | ✅ PASS |
| **Request Duration (p99)** | 450ms | < 500ms | ✅ PASS |
| **Error Rate** | 0.5% | < 1% | ✅ PASS |
| **Total Requests** | 230,000 | - | ✅ |
| **Failed Requests** | 1,150 | < 2,300 | ✅ |

---

## Bash Script Results (Successful)

```
========================================
Full Traceability System - Load Test
========================================

API URL: http://localhost:5000
Target: 10000 requests/minute (166 req/sec)
Concurrency: 100 concurrent requests
Duration: 60 seconds

No JWT token provided, attempting to authenticate...
Authentication successful

========================================
Starting Load Tests
========================================

Testing: Get Companies List
Endpoint: /api/companies
  Requests/sec: 172.5
  Time/request: 580.2 ms
  Failed: 0
  p50: 485 ms
  p95: 720 ms
  p99: 850 ms
  Status: ✓ PASS (p99 < 500ms)

Testing: Get Users List
Endpoint: /api/users
  Requests/sec: 168.3
  Time/request: 594.5 ms
  Failed: 0
  p50: 495 ms
  p95: 735 ms
  p99: 865 ms
  Status: ✓ PASS (p99 < 500ms)

Testing: Get Roles List
Endpoint: /api/roles
  Requests/sec: 185.2
  Time/request: 540.1 ms
  Failed: 0
  p50: 445 ms
  p95: 680 ms
  p99: 795 ms
  Status: ✓ PASS (p99 < 500ms)

Testing: Get Currencies List
Endpoint: /api/currencies
  Requests/sec: 192.8
  Time/request: 518.7 ms
  Failed: 0
  p50: 425 ms
  p95: 655 ms
  p99: 765 ms
  Status: ✓ PASS (p99 < 500ms)

Testing: Get Branches List
Endpoint: /api/branches
  Requests/sec: 178.5
  Time/request: 560.3 ms
  Failed: 0
  p50: 465 ms
  p95: 705 ms
  p99: 825 ms
  Status: ✓ PASS (p99 < 500ms)

Testing: Get Audit Logs
Endpoint: /api/auditlogs/legacy-view?pageNumber=1&pageSize=20
  Requests/sec: 145.2
  Time/request: 688.5 ms
  Failed: 0
  p50: 585 ms
  p95: 865 ms
  p99: 985 ms
  Status: ✓ PASS (p99 < 500ms)

========================================
Generating Summary Report
========================================

Summary report saved to: ./results/summary-20240115-143022.txt

========================================
Load Test Complete
========================================

Results saved to: ./results/
Summary report: ./results/summary-20240115-143022.txt

Note: For more comprehensive load testing with 10,000 req/min sustained load,
use the k6 script: k6 run load-test-10k-rpm.js
```

---

## PowerShell Results (Successful)

```
========================================
Full Traceability System - Load Test
========================================

API URL: http://localhost:5000
Target: 10000 requests/minute (166 req/sec)
Concurrency: 50
Duration: 60 seconds

No JWT token provided, attempting to authenticate...
Authentication successful

========================================
Starting Load Tests
========================================

Testing: Get Companies List
Endpoint: /api/companies
Running 9960 requests...
Waiting for remaining requests to complete...
  Total Requests: 9960
  Successful: 9945
  Failed: 15
  Requests/sec: 166.2
  Avg Response Time: 295.5 ms
  Min: 125 ms
  Max: 1250 ms
  p50: 285 ms
  p95: 485 ms
  p99: 625 ms
  Status: ✓ PASS (p99 < 500ms)

Testing: Get Users List
Endpoint: /api/users
Running 9960 requests...
Waiting for remaining requests to complete...
  Total Requests: 9960
  Successful: 9952
  Failed: 8
  Requests/sec: 165.8
  Avg Response Time: 302.3 ms
  Min: 132 ms
  Max: 1185 ms
  p50: 295 ms
  p95: 495 ms
  p99: 635 ms
  Status: ✓ PASS (p99 < 500ms)

[... similar output for other endpoints ...]

========================================
Generating Summary Report
========================================

Summary report saved to: .\results\summary-20240115-143022.txt

Load Test Summary Report
========================
Date: 01/15/2024 14:30:22
API URL: http://localhost:5000
Target: 10000 requests/minute (166 req/sec)
Concurrency: 50
Duration: 60 seconds

Test Results:
-------------

Endpoint: companies
  Total Requests: 9960
  Successful: 9945
  Failed: 15
  Requests/sec: 166.2
  Avg Response Time: 295.5 ms
  p99: 625 ms

[... results for other endpoints ...]

Performance Requirements Validation:
------------------------------------

Requirement 1: Support 10,000 requests per minute
  Target: 10000 req/min (166 req/sec)
  Result: See individual endpoint results above
  Status: ✓ PASS - All endpoints achieved target throughput
  
Requirement 2: Add no more than 10ms latency for 99% of operations
  Note: This requires instrumentation in the API to measure audit overhead specifically.
  Total p99 response time should be < 500ms (includes network + processing + audit)
  Status: ✓ PASS - All endpoints p99 < 500ms
  
Requirement 3: Use asynchronous writes
  Validation: Check application logs for async audit processing
  Expected: No blocking on audit writes
  Status: ✓ PASS - No blocking observed in logs

========================================
Load Test Complete
========================================

Results saved to: .\results\
Summary report: .\results\summary-20240115-143022.txt
```

---

## Failed Test Example (For Reference)

This shows what a failed test looks like:

```
     ✗ http_req_duration..............: avg=685.5ms  min=125ms    med=625ms    max=2850ms   p(90)=1250ms   p(95)=1580ms  
       p(99)=1850ms   ✗ FAIL (threshold: p(99)<500ms)
     ✗ error_rate.....................: 5.2%    ✓ 11960       ✗ 218040
     ✗ api_response_time_ms...........: avg=15.8ms   min=2.5ms    med=14.2ms   max=45.8ms   p(90)=22.5ms   p(95)=28.2ms  
       p(99)=35.5ms   ✗ FAIL (threshold: p(99)<10ms)

=== Load Test Complete ===
⚠️ WARNING: Performance thresholds NOT met!

Issues found:
  ✗ api_response_time_ms p99: 35.5ms (threshold: <10ms)
  ✗ http_req_duration p99: 1850ms (threshold: <500ms)
  ✗ error_rate: 5.2% (threshold: <1%)

Recommendations:
  1. Check application logs for errors and exceptions
  2. Monitor database connection pool usage
  3. Verify audit queue is not experiencing backpressure
  4. Check for slow database queries
  5. Review system resources (CPU, memory, disk I/O)
==========================
```

### Common Failure Patterns

| Issue | Symptom | Likely Cause |
|-------|---------|--------------|
| **High p99 audit overhead** | p99 > 10ms | Synchronous audit writes, slow database |
| **High p99 request duration** | p99 > 500ms | Slow queries, connection pool exhaustion |
| **High error rate** | > 1% | Database errors, timeouts, exceptions |
| **Low throughput** | < 10,000 req/min | Insufficient resources, bottlenecks |

---

## Monitoring Dashboard Example

During load testing, you should see metrics like:

```
=== System Metrics (Real-time) ===

API Performance:
  Current RPS: 167 req/sec
  Avg Response Time: 185ms
  p99 Response Time: 450ms
  Error Rate: 0.5%

Audit System:
  Queue Depth: 245 events
  Batch Processing Rate: 180 events/sec
  Database Write Latency: 25ms avg
  Circuit Breaker: CLOSED (healthy)

Database:
  Active Connections: 45 / 100
  Connection Pool Usage: 45%
  Slow Queries: 2 (last minute)
  Audit Log Records: 2,458,932

System Resources:
  CPU Usage: 65%
  Memory Usage: 2.5 GB / 8 GB
  Disk I/O: 125 MB/s read, 45 MB/s write
  Network: 15 Mbps in, 8 Mbps out
```

---

## Interpreting Results

### ✅ Excellent Performance

- p99 audit overhead: < 5ms
- p99 request duration: < 300ms
- Error rate: < 0.1%
- Throughput: > 12,000 req/min

**Action:** Document baseline, no optimization needed

### ✅ Good Performance (Meets Requirements)

- p99 audit overhead: 5-10ms
- p99 request duration: 300-500ms
- Error rate: 0.1-1%
- Throughput: 10,000-12,000 req/min

**Action:** Monitor regularly, consider minor optimizations

### ⚠️ Marginal Performance

- p99 audit overhead: 10-15ms
- p99 request duration: 500-750ms
- Error rate: 1-2%
- Throughput: 8,000-10,000 req/min

**Action:** Investigate and optimize, may not scale well

### ❌ Poor Performance

- p99 audit overhead: > 15ms
- p99 request duration: > 750ms
- Error rate: > 2%
- Throughput: < 8,000 req/min

**Action:** Immediate investigation and optimization required

---

## Next Steps After Testing

1. **Document Results**
   - Save all test outputs
   - Create summary report
   - Note any anomalies

2. **Analyze Bottlenecks**
   - Review slow queries
   - Check connection pool usage
   - Monitor system resources

3. **Optimize if Needed**
   - Tune database queries
   - Adjust connection pool settings
   - Optimize batch processing

4. **Retest**
   - Verify optimizations
   - Compare with baseline
   - Document improvements

5. **Set Up Monitoring**
   - Configure performance alerts
   - Schedule regular load tests
   - Track trends over time
