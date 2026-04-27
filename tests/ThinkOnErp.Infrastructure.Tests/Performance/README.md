# Audit System Performance Tests

## Overview

This directory contains performance tests for the audit logging system, validating that both write and query operations meet the performance requirements defined in the Full Traceability System specification.

## Test Coverage

### Task 20.3: Audit Write Latency Testing

**Validates Requirement 13.7**: Audit writes complete within 50ms for 95% of operations

### Task 20.4: Audit Query Performance Testing

**Validates Requirement 11.5**: Query results returned within 2 seconds for date ranges up to 30 days

### Task 20.7: Memory Pressure and Queue Backpressure Testing

**Validates Requirement 13.4**: When queue exceeds capacity, apply backpressure to prevent memory exhaustion

The test suite includes:

1. **Single Write Latency Tests**
   - Measures latency for individual audit write operations
   - Tests data change events and authentication events
   - Validates P95 latency < 50ms

2. **Batch Write Performance Tests**
   - Tests various batch sizes (10, 50, 100, 200 events)
   - Validates that batching maintains latency requirements
   - Ensures efficient batch processing

3. **Concurrent Write Performance Tests**
   - Simulates concurrent audit writes from multiple threads
   - Tests system behavior under concurrent load
   - Validates P95 latency < 50ms under concurrency

4. **High-Volume Throughput Tests**
   - Validates Requirement 13.1: Support 10,000 requests per minute
   - Measures actual throughput under load
   - Ensures latency requirements are met at high volume

5. **Sustained Load Tests**
   - Tests performance over extended periods (30 seconds)
   - Validates no performance degradation over time
   - Ensures system stability under sustained load

## Memory Pressure and Queue Backpressure Tests (MemoryPressureBackpressurePerformanceTests.cs)

### Task 20.7: Memory Pressure Testing

**Validates Requirement 13.4**: When queue exceeds 10,000 entries, apply backpressure to prevent memory exhaustion

The test suite includes:

1. **Queue Backpressure Tests**
   - Tests behavior when queue reaches capacity
   - Validates backpressure mechanism activates correctly
   - Measures latency increase when backpressure is applied
   - Ensures queue drains after backpressure is released

2. **Memory Exhaustion Prevention Tests**
   - Tests system behavior when attempting to write 2x queue capacity
   - Validates memory growth remains bounded
   - Ensures backpressure prevents unbounded memory growth
   - Verifies all events are eventually processed

3. **Concurrent Memory Pressure Tests**
   - Simulates multiple threads writing under memory pressure
   - Tests system stability under concurrent backpressure
   - Validates thread-safe backpressure application
   - Ensures no events are lost under concurrent load

4. **Graceful Degradation Tests**
   - Tests behavior with slow database writes
   - Validates system remains responsive under database pressure
   - Ensures backpressure is applied appropriately
   - Verifies no crashes or data loss

5. **Queue Recovery Tests**
   - Tests system recovery after backpressure period
   - Validates performance returns to normal after queue drains
   - Ensures system can handle repeated pressure cycles
   - Verifies queue management is stable over time

## Audit Query Performance Tests (AuditQueryPerformanceTests.cs)

### Task 20.4: Query Performance Testing

**Validates Requirement 11.5**: Query results returned within 2 seconds for 30-day date ranges

The test suite includes:

1. **30-Day Date Range Query Tests**
   - Measures query performance for 30-day date ranges
   - Tests with various filter combinations
   - Tests with different pagination settings
   - Validates max query time < 2 seconds

2. **Various Date Range Tests**
   - Tests 1-day, 7-day, 14-day, and 30-day ranges
   - Validates performance scales appropriately with date range
   - Ensures all ranges meet performance requirements

3. **Entity and Actor Query Tests**
   - Tests entity history queries
   - Tests actor activity queries over 30-day ranges
   - Validates specialized query performance

4. **Correlation ID Query Tests**
   - Tests fast indexed queries by correlation ID
   - Validates sub-second response times for indexed queries

5. **Concurrent Query Tests**
   - Simulates multiple concurrent query operations
   - Tests system behavior under concurrent query load
   - Validates performance under realistic multi-user scenarios

## Running the Tests

### Prerequisites

- .NET 8.0 SDK or later
- xUnit test runner

### Configuration

Performance tests are **disabled by default** to avoid impacting regular test runs. To enable them:

#### Option 1: Environment Variables

```bash
# Linux/macOS
export THINKONERP_PERF_TEST_PerformanceTest__RunTests=true
export THINKONERP_PERF_TEST_PerformanceTest__Iterations=1000
export THINKONERP_PERF_TEST_PerformanceTest__ConcurrentOperations=100

# Windows PowerShell
$env:THINKONERP_PERF_TEST_PerformanceTest__RunTests="true"
$env:THINKONERP_PERF_TEST_PerformanceTest__Iterations="1000"
$env:THINKONERP_PERF_TEST_PerformanceTest__ConcurrentOperations="100"
```

#### Option 2: appsettings.json

Create or modify `appsettings.json` in the test project:

```json
{
  "PerformanceTest": {
    "RunTests": true,
    "Iterations": 1000,
    "ConcurrentOperations": 100
  }
}
```

### Running Tests

```bash
# Run all performance tests
dotnet test --filter "FullyQualifiedName~Performance"

# Run audit write latency tests only
dotnet test --filter "FullyQualifiedName~AuditWriteLatencyPerformanceTests"

# Run audit query performance tests only
dotnet test --filter "FullyQualifiedName~AuditQueryPerformanceTests"

# Run memory pressure and backpressure tests only
dotnet test --filter "FullyQualifiedName~MemoryPressureBackpressurePerformanceTests"

# Run specific test
dotnet test --filter "FullyQualifiedName~AuditLogger_SingleWrite_ShouldMeetLatencyRequirements"

# Run with verbose output
dotnet test --filter "FullyQualifiedName~Performance" --logger "console;verbosity=detailed"
```

## Configuration Options

| Setting | Default | Description |
|---------|---------|-------------|
| `PerformanceTest:RunTests` | `false` | Enable/disable performance tests |
| `PerformanceTest:Iterations` | `1000` | Number of iterations for single-threaded tests |
| `PerformanceTest:ConcurrentOperations` | `100` | Number of concurrent threads for concurrency tests |

## Performance Targets

Based on the Full Traceability System requirements:

### Audit Write Performance (Task 20.3)

| Metric | Target | Requirement |
|--------|--------|-------------|
| P95 Latency | < 50ms | Requirement 13.7 |
| Average Latency | < 25ms | Internal target |
| P99 Latency | < 100ms | Internal target |
| Throughput | ≥ 10,000 req/min | Requirement 13.1 |
| Performance Degradation | < 20% | Sustained load stability |

### Memory Pressure and Backpressure (Task 20.7)

| Metric | Target | Requirement |
|--------|--------|-------------|
| Queue Capacity Enforcement | Exact | Requirement 13.4 |
| Memory Growth | Bounded by queue size | Requirement 13.4 |
| Backpressure Activation | When queue full | Requirement 13.4 |
| Event Loss | 0 events | Data integrity |
| Recovery Time | < 5 seconds | System stability |
| Concurrent Stability | No crashes | Thread safety |

### Audit Query Performance (Task 20.4)

| Metric | Target | Requirement |
|--------|--------|-------------|
| Max Query Time (30-day) | < 2000ms | Requirement 11.5 |
| P95 Query Time | < 1500ms | Internal target |
| Average Query Time | < 1000ms | Internal target |
| Indexed Query (Correlation ID) | < 500ms | Internal target |
| Concurrent Query P95 | < 1500ms | Internal target |

## Test Results Interpretation

### Success Criteria

**Audit Write Performance:**
- **P95 Latency < 50ms**: 95% of audit write operations complete within 50ms
- **No Performance Degradation**: Performance remains stable over sustained load
- **Throughput Target Met**: System handles at least 10,000 requests per minute

**Memory Pressure and Backpressure:**
- **Queue Capacity Enforced**: Queue size is strictly enforced at configured limit
- **Memory Bounded**: Memory growth is bounded by queue capacity
- **Backpressure Applied**: Writes block when queue is full (BoundedChannelFullMode.Wait)
- **No Event Loss**: All events are eventually processed, none are dropped
- **System Stability**: No crashes or exceptions under memory pressure
- **Recovery**: System returns to normal performance after pressure is released

**Audit Query Performance:**
- **Max Query Time < 2 seconds**: All 30-day queries complete within 2 seconds
- **P95 Query Time < 1.5 seconds**: 95% of queries complete within 1.5 seconds
- **Concurrent Performance**: Performance maintained under concurrent query load

### Example Output

**Write Performance:**
```
Single Write Performance Results:
  Total Operations: 1000
  Average Latency: 12.34ms
  P50 Latency: 10.50ms
  P95 Latency: 23.45ms
  P99 Latency: 45.67ms
  Max Latency: 78ms
```

**Memory Pressure:**
```
Memory Pressure Results:
  Total Events Attempted: 20000
  Successful Writes: 20000
  Blocked Writes (backpressure): 8543
  Total Time: 15234ms
  Memory Before: 45.67 MB
  Memory After: 67.89 MB
  Memory Increase: 22.22 MB
  Final Queue Depth: 234
```

**Query Performance:**
```
30-Day Query Performance Results:
  Total Iterations: 10
  Average Query Time: 456.78ms
  P50 Query Time: 450.00ms
  P95 Query Time: 890.12ms
  P99 Query Time: 1234.56ms
  Max Query Time: 1456ms
```

## Implementation Details

### Audit Write Tests

#### Mock Repository

The write tests use a mock `IAuditRepository` that simulates database write latency (1-5ms) to provide realistic performance measurements without requiring an actual database connection.

#### Asynchronous Processing

The `AuditLogger` uses `System.Threading.Channels` for high-performance asynchronous processing:
- Events are queued immediately (non-blocking)
- Background task processes events in batches
- Batch size: 50 events or 100ms window (whichever comes first)

#### Circuit Breaker

Circuit breaker is **disabled** for performance tests to measure raw performance without resilience overhead.

### Audit Query Tests

#### Query Simulation

The query tests simulate database query latency to measure service layer overhead and validate performance targets. Key aspects:

- **Simulated Query Time**: 50-150ms to represent realistic Oracle database query times
- **Record Generation**: Generates test data sets with configurable sizes (default: 10,000 records)
- **Date Range Distribution**: Records are distributed evenly across the date range
- **Realistic Filters**: Tests include company ID, actor type, action type, and other common filters

#### Performance Measurement

Query performance is measured end-to-end including:
- Filter application and validation
- Role-based access control (RBAC) filtering
- Query construction and parameter binding
- Result mapping and pagination
- Response serialization overhead

#### Test Scenarios

1. **Basic 30-Day Queries**: Validates core requirement (Requirement 11.5)
2. **Filtered Queries**: Tests performance with multiple filter criteria
3. **Pagination Variations**: Tests different page sizes (10, 50, 100, 200)
4. **Date Range Variations**: Tests 1-day, 7-day, 14-day, and 30-day ranges
5. **Specialized Queries**: Entity history, actor activity, correlation ID lookups
6. **Concurrent Queries**: Multiple simultaneous queries from different users

#### Limitations

**Note**: These tests measure service layer performance with simulated database responses. For complete performance validation:

1. **Integration Testing**: Run tests against a real Oracle database with realistic data volumes
2. **Index Verification**: Ensure all required indexes are created (see Database/Scripts/13_Extend_SYS_AUDIT_LOG_For_Traceability.sql)
3. **Oracle Text**: Verify Oracle Text is configured for full-text search performance
4. **Connection Pooling**: Validate connection pool settings under load
5. **Query Plans**: Analyze Oracle execution plans for slow queries

For production performance validation, use the integration tests in `tests/ThinkOnErp.Infrastructure.Tests/Integration/` with a test database containing realistic data volumes.

## Troubleshooting

### Tests Are Skipped

If you see "Skipping performance test" messages, ensure you've enabled the tests via environment variables or configuration.

### High Latency Results

If latency results are higher than expected:

**For Write Tests:**
1. **Check System Load**: Ensure the test machine is not under heavy load
2. **Disable Other Services**: Stop unnecessary background services
3. **Increase Iterations**: More iterations provide more accurate percentile calculations
4. **Check Mock Configuration**: Verify mock repository latency simulation is appropriate

**For Query Tests:**
1. **Check Simulation Delay**: Verify the simulated database delay is realistic (50-150ms)
2. **Reduce Record Count**: Lower the simulated record count if memory is constrained
3. **Check System Resources**: Ensure adequate CPU and memory are available
4. **Disable Antivirus**: Temporarily disable antivirus for more consistent results

### Memory Issues

For high-volume tests, you may need to increase available memory:

```bash
# Increase test process memory limit
dotnet test --settings test.runsettings
```

### Database Connection Errors

The query performance tests use a mock database context. If you see connection errors:

1. **Check Configuration**: Verify the test connection string is properly configured
2. **Mock Setup**: Ensure mocks are properly configured for the test scenario
3. **Integration Tests**: For real database testing, use integration tests instead

## Related Documentation

- [Full Traceability System Design](../../../.kiro/specs/full-traceability-system/design.md)
- [Full Traceability System Requirements](../../../.kiro/specs/full-traceability-system/requirements.md)
- [Full Traceability System Tasks](../../../.kiro/specs/full-traceability-system/tasks.md)

## Contributing

When adding new performance tests:

1. Follow the existing test structure and naming conventions
2. Use the same configuration mechanism (environment variables)
3. Document performance targets and success criteria
4. Include detailed output for analysis
5. Ensure tests are disabled by default
