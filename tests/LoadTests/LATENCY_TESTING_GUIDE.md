# Latency Testing Guide - Task 20.2

This guide covers the comprehensive latency testing implementation for validating **Task 20.2: Conduct latency testing (<10ms overhead for 99% of requests)**.

## Overview

The latency testing suite validates that the Full Traceability System adds no more than 10ms overhead to API requests for 99% of operations. This is a critical performance requirement that ensures the audit logging, request tracing, and performance monitoring components don't negatively impact user experience.

## Test Components

### 1. k6 Latency Overhead Test (`latency-overhead-test.js`)

**Purpose:** Advanced load testing with precise latency measurement using k6's high-performance testing engine.

**Features:**
- Multiple test scenarios (baseline, full system, high load)
- Custom metrics for traceability overhead measurement
- Weighted endpoint testing (realistic usage patterns)
- High-precision timing measurements
- Comprehensive threshold validation

**Usage:**
```bash
# Basic test
k6 run tests/LoadTests/latency-overhead-test.js

# With custom configuration
k6 run --env API_URL=http://localhost:5000 --env JWT_TOKEN="your-token" tests/LoadTests/latency-overhead-test.js

# Save results to file
k6 run --out json=results.json tests/LoadTests/latency-overhead-test.js
```

**Test Scenarios:**
1. **Baseline Measurement** (5 minutes): 100 req/min to establish baseline performance
2. **Full System Measurement** (5 minutes): 100 req/min with traceability enabled
3. **High Load Overhead** (12 minutes): Ramp up to 1000 req/min to test under stress

### 2. PowerShell Latency Script (`latency-measurement-script.ps1`)

**Purpose:** Cross-platform PowerShell script for detailed latency analysis with concurrent request execution.

**Features:**
- Parallel request execution using PowerShell jobs
- Detailed statistical analysis (percentiles, averages)
- Correlation ID tracking
- Audit overhead extraction from response headers
- Comprehensive error handling and reporting

**Usage:**
```powershell
# Basic test
.\tests\LoadTests\latency-measurement-script.ps1

# With custom parameters
.\tests\LoadTests\latency-measurement-script.ps1 -ApiUrl "http://localhost:5000" -Duration 120 -Concurrency 20

# With JWT token
.\tests\LoadTests\latency-measurement-script.ps1 -JwtToken "your-jwt-token" -OutputFile "results.json"
```

**Parameters:**
- `ApiUrl`: API base URL (default: http://localhost:5000)
- `JwtToken`: Authentication token (auto-authenticates if not provided)
- `Duration`: Test duration in seconds (default: 60)
- `Concurrency`: Concurrent requests (default: 10)
- `OutputFile`: Results file path (auto-generated if not specified)

### 3. Bash Latency Script (`latency-measurement-script.sh`)

**Purpose:** Unix/Linux compatible bash script for environments where PowerShell is not available.

**Features:**
- Pure bash implementation with curl for HTTP requests
- Parallel request execution using background processes
- Statistical calculations using bc for precision
- JSON output format for integration with other tools
- Cross-platform compatibility (Linux, macOS, WSL, Git Bash)

**Usage:**
```bash
# Make executable (Linux/macOS)
chmod +x tests/LoadTests/latency-measurement-script.sh

# Basic test
./tests/LoadTests/latency-measurement-script.sh

# With parameters
./tests/LoadTests/latency-measurement-script.sh http://localhost:5000 "jwt-token" 120 20
```

**Dependencies:**
- `curl`: HTTP client
- `jq`: JSON processor
- `bc`: Calculator for statistical computations

### 4. Master Test Runner (`run-latency-tests.ps1`)

**Purpose:** Orchestrates all latency tests and generates comprehensive reports.

**Features:**
- Runs all test types or specific test types
- Automatic dependency checking
- API availability validation
- Consolidated reporting
- Test result aggregation and validation

**Usage:**
```powershell
# Run all tests
.\tests\LoadTests\run-latency-tests.ps1

# Run specific test type
.\tests\LoadTests\run-latency-tests.ps1 -TestType "powershell"

# Skip k6 if not installed
.\tests\LoadTests\run-latency-tests.ps1 -SkipK6

# Custom configuration
.\tests\LoadTests\run-latency-tests.ps1 -ApiUrl "http://localhost:5000" -Duration 120
```

## Test Endpoints

The latency tests cover a representative mix of API endpoints with different complexity levels:

| Endpoint | Weight | Complexity | Description |
|----------|--------|------------|-------------|
| `/api/companies` | 25% | Medium | Database query with joins |
| `/api/users` | 25% | Medium | Database query with joins |
| `/api/roles` | 20% | Low | Simple lookup table |
| `/api/currencies` | 15% | Low | Simple lookup table |
| `/api/branches` | 10% | Medium | Database query with joins |
| `/api/auditlogs/legacy-view` | 5% | High | Complex audit log query |

This distribution simulates realistic usage patterns with mostly read operations and varying database complexity.

## Performance Thresholds

### Critical Requirements (Task 20.2)

1. **Traceability Overhead**: p99 < 10ms
   - The overhead introduced by audit logging, request tracing, and performance monitoring
   - Measured as the difference between baseline and full system performance

2. **Audit Overhead**: p99 < 5ms
   - Specific overhead from audit logging operations
   - Extracted from `X-Audit-Overhead-Ms` response header if available

3. **Response Time**: p99 < 500ms
   - Total API response time including all processing
   - Ensures overall system performance remains acceptable

4. **Error Rate**: < 1%
   - Percentage of failed requests
   - Validates system stability under load

### Target Performance Levels

- **Excellent**: p99 overhead < 5ms, error rate < 0.1%
- **Good**: p99 overhead 5-10ms, error rate 0.1-1%
- **Marginal**: p99 overhead 10-15ms, error rate 1-2%
- **Poor**: p99 overhead > 15ms, error rate > 2%

## Running the Tests

### Prerequisites

1. **ThinkOnErp API Running**
   ```bash
   cd src/ThinkOnErp.API
   dotnet run
   ```

2. **Dependencies Installed**
   - k6 (optional): https://k6.io/docs/getting-started/installation/
   - PowerShell Core (for cross-platform scripts)
   - curl, jq, bc (for bash script)

3. **Authentication**
   - Default superadmin credentials: `superadmin` / `SuperAdmin123!`
   - Or provide JWT token via parameter

### Quick Start

```powershell
# Run all tests (recommended)
.\tests\LoadTests\run-latency-tests.ps1

# Run only PowerShell test (if k6 not available)
.\tests\LoadTests\run-latency-tests.ps1 -TestType "powershell" -SkipK6
```

### Test Execution Flow

1. **API Availability Check**: Verifies the API is running and accessible
2. **Authentication**: Obtains JWT token if not provided
3. **System Warm-up**: Makes initial requests to initialize connection pools and JIT compilation
4. **Latency Measurement**: Executes test scenarios with precise timing
5. **Statistical Analysis**: Calculates percentiles and performance metrics
6. **Requirement Validation**: Checks against Task 20.2 thresholds
7. **Report Generation**: Creates detailed results and summary reports

## Interpreting Results

### Successful Test Output

```
✅ Task 20.2 PASSED: Latency requirements met
   System adds <10ms overhead for 99% of requests

Test Results Summary:
  k6: PASSED
  PowerShell: PASSED
  Bash: PASSED
```

### Key Metrics to Review

1. **Traceability Overhead (p99)**: Must be < 10ms
2. **Response Time Percentiles**: p50, p95, p99 should be reasonable
3. **Error Rate**: Should be < 1%
4. **Correlation ID Coverage**: All requests should have correlation IDs
5. **Audit Overhead**: Should be minimal and asynchronous

### Failed Test Analysis

If tests fail, investigate:

1. **High Overhead (p99 > 10ms)**
   - Check if audit logging is synchronous (should be async)
   - Monitor audit queue depth and processing rate
   - Verify database connection pool configuration
   - Check for slow database queries

2. **High Error Rate (> 1%)**
   - Review application logs for exceptions
   - Check database connection pool exhaustion
   - Monitor system resources (CPU, memory, disk I/O)
   - Verify network stability

3. **Slow Response Times**
   - Profile slow endpoints
   - Check database query performance
   - Review connection pool settings
   - Monitor system resource utilization

## Output Files

The tests generate several output files in the `./results` directory:

### JSON Results Files
- `k6-latency-results-{timestamp}.json`: k6 test results
- `powershell-latency-results-{timestamp}.json`: PowerShell test results
- `latency-results-{timestamp}.json`: Bash test results

### Summary Reports
- `latency-test-summary-{timestamp}.md`: Comprehensive test summary
- Individual test logs and detailed metrics

### Sample JSON Structure
```json
{
  "testConfiguration": {
    "apiUrl": "http://localhost:5000",
    "duration": 60,
    "concurrency": 10,
    "timestamp": "2024-01-15T14:30:22"
  },
  "endpointResults": {
    "companies": {
      "totalRequests": 120,
      "successfulRequests": 119,
      "failedRequests": 1,
      "errorRate": 0.83,
      "responseTime": {
        "min": 125,
        "max": 850,
        "average": 285.5,
        "p50": 275,
        "p95": 485,
        "p99": 625
      },
      "auditOverhead": {
        "count": 115,
        "average": 3.2,
        "p95": 6.8,
        "p99": 8.5
      },
      "correlationIds": 119,
      "status": "PASS"
    }
  },
  "summary": {
    "overallPass": true
  }
}
```

## Integration with CI/CD

### GitHub Actions Example

```yaml
name: Latency Testing

on:
  push:
    branches: [ main, develop ]
  pull_request:
    branches: [ main ]

jobs:
  latency-tests:
    runs-on: ubuntu-latest
    
    steps:
    - uses: actions/checkout@v3
    
    - name: Setup .NET
      uses: actions/setup-dotnet@v3
      with:
        dotnet-version: '8.0.x'
    
    - name: Install k6
      run: |
        sudo gpg -k
        sudo gpg --no-default-keyring --keyring /usr/share/keyrings/k6-archive-keyring.gpg --keyserver hkp://keyserver.ubuntu.com:80 --recv-keys C5AD17C747E3415A3642D57D77C6C491D6AC1D69
        echo "deb [signed-by=/usr/share/keyrings/k6-archive-keyring.gpg] https://dl.k6.io/deb stable main" | sudo tee /etc/apt/sources.list.d/k6.list
        sudo apt-get update
        sudo apt-get install k6
    
    - name: Start API
      run: |
        cd src/ThinkOnErp.API
        dotnet run &
        sleep 30  # Wait for API to start
    
    - name: Run Latency Tests
      run: |
        pwsh tests/LoadTests/run-latency-tests.ps1 -Duration 30
    
    - name: Upload Results
      uses: actions/upload-artifact@v3
      with:
        name: latency-test-results
        path: results/
```

## Troubleshooting

### Common Issues

1. **API Not Available**
   ```
   ERROR: API is not available at http://localhost:5000
   ```
   **Solution**: Ensure the ThinkOnErp API is running and accessible

2. **Authentication Failed**
   ```
   ERROR: Authentication failed with status 401
   ```
   **Solution**: Check superadmin credentials or provide valid JWT token

3. **k6 Not Found**
   ```
   WARN: k6 is not available. Install k6 or use -SkipK6 parameter
   ```
   **Solution**: Install k6 or use `-SkipK6` parameter to run other tests

4. **High Latency Results**
   ```
   ERROR: P99 overhead: 15.2ms (threshold: <10ms)
   ```
   **Solution**: 
   - Check audit queue processing rate
   - Verify asynchronous audit logging
   - Monitor database performance
   - Review system resources

5. **Bash Dependencies Missing**
   ```
   ERROR: Missing required dependencies: jq bc
   ```
   **Solution**: Install missing dependencies:
   ```bash
   # Ubuntu/Debian
   sudo apt-get install jq bc curl
   
   # macOS
   brew install jq bc
   
   # Windows (Git Bash)
   # jq and bc should be included with Git for Windows
   ```

### Performance Optimization

If latency tests fail, consider these optimizations:

1. **Audit Logger Configuration**
   ```json
   {
     "AuditLogging": {
       "BatchSize": 100,
       "BatchWindowMs": 50,
       "MaxQueueSize": 20000
     }
   }
   ```

2. **Database Connection Pool**
   ```json
   {
     "ConnectionStrings": {
       "DefaultConnection": "...;Max Pool Size=200;Min Pool Size=10;"
     }
   }
   ```

3. **Request Tracing Options**
   ```json
   {
     "RequestTracing": {
       "LogPayloads": false,
       "ExcludedPaths": ["/health", "/metrics", "/favicon.ico"]
     }
   }
   ```

## Continuous Monitoring

### Regular Testing Schedule

- **Daily**: Quick latency check (30-second tests)
- **Weekly**: Full latency test suite (5-minute tests)
- **Monthly**: Extended stress testing (30-minute tests)
- **Release**: Comprehensive validation before deployment

### Performance Baselines

Establish and maintain performance baselines:

1. **Record Initial Baseline**: After successful implementation
2. **Update Baselines**: After significant changes or optimizations
3. **Monitor Trends**: Track performance over time
4. **Alert Thresholds**: Set up alerts for performance degradation

### Monitoring Integration

Integrate with monitoring systems:

- **Application Performance Monitoring (APM)**: New Relic, Datadog, Application Insights
- **Custom Dashboards**: Grafana with performance metrics
- **Alerting**: PagerDuty, Slack notifications for threshold violations

## Conclusion

The latency testing suite provides comprehensive validation of Task 20.2 requirements, ensuring the Full Traceability System maintains excellent performance while providing complete audit coverage. Regular execution of these tests helps maintain system performance and quickly identify any performance regressions.

For questions or issues with the latency testing suite, refer to the troubleshooting section or review the detailed test outputs for specific guidance.