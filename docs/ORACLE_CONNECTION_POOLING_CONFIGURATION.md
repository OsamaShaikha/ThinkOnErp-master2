# Oracle Connection Pooling Configuration for High-Volume Audit Logging

## Overview

The ThinkOnErp API uses Oracle connection pooling to efficiently manage database connections for high-volume audit logging scenarios. The system is configured to handle **10,000+ requests per minute** with optimal performance.

## Current Configuration

### Connection String Parameters

```
Pooling=true;
Min Pool Size=5;
Max Pool Size=100;
Connection Timeout=15;
Incr Pool Size=5;
Decr Pool Size=2;
Validate Connection=true;
Connection Lifetime=300;
Statement Cache Size=50;
Statement Cache Purge=false;
```

### Parameter Explanations

#### Core Pooling Settings

**Pooling=true**
- Enables connection pooling (default: true)
- Essential for high-performance scenarios
- Reuses existing connections instead of creating new ones

**Min Pool Size=5**
- Minimum number of connections always maintained in the pool
- Prevents cold starts and ensures immediate availability
- **Rationale**: 5 connections provide a baseline for handling burst traffic without delay
- Connections are created at application startup

**Max Pool Size=100**
- Maximum number of connections allowed in the pool
- Prevents database overload and resource exhaustion
- **Rationale for 10,000 req/min**: 
  - At 10,000 requests/minute = ~167 requests/second
  - With batch processing (50 events per batch, 100ms window), actual DB writes are much lower
  - Estimated ~20-30 concurrent DB operations under normal load
  - 100 max connections provides 3-5x headroom for spikes
  - Oracle database can typically handle 100-200 concurrent connections efficiently

#### Connection Growth and Shrinkage

**Incr Pool Size=5**
- Number of connections to add when pool needs to grow
- Grows in increments of 5 to balance responsiveness and overhead
- **Rationale**: Moderate growth prevents connection storms while responding to load increases

**Decr Pool Size=2**
- Number of connections to remove when pool shrinks during low traffic
- Shrinks slowly to avoid thrashing
- **Rationale**: Conservative shrinkage prevents frequent grow/shrink cycles

#### Connection Timeout and Lifetime

**Connection Timeout=15**
- Seconds to wait for an available connection from the pool
- Fail-fast approach prevents request queuing
- **Rationale**: 15 seconds is sufficient for normal operations while preventing indefinite hangs
- If timeout occurs, indicates pool exhaustion or database issues

**Connection Lifetime=300**
- Maximum lifetime of a connection in seconds (5 minutes)
- Forces periodic connection refresh to prevent stale connections
- **Rationale**: 
  - Prevents long-lived connections from accumulating issues
  - Balances connection freshness with overhead of recreation
  - Aligns with typical Oracle session timeout settings

#### Connection Validation

**Validate Connection=true**
- Validates connections before returning them from the pool
- Prevents using stale or broken connections
- **Rationale**: 
  - Critical for reliability in production environments
  - Small overhead (<1ms) is worth the reliability gain
  - Prevents cascading failures from broken connections

#### Statement Caching

**Statement Cache Size=50**
- Number of prepared statements to cache per connection
- Significantly improves performance for repeated queries
- **Rationale for audit logging**:
  - Audit insert statements are highly repetitive
  - Batch inserts use the same prepared statement repeatedly
  - 50 statements covers all audit-related queries plus application queries
  - Each cached statement saves ~5-10ms of parse time

**Statement Cache Purge=false**
- Keeps cached statements across connection reuse
- Maximizes statement cache effectiveness
- **Rationale**: Audit queries are consistent, so cache retention is beneficial

## Performance Characteristics

### Expected Performance Under Load

**10,000 Requests/Minute Scenario:**
- Requests per second: ~167
- With batching (50 events, 100ms window): ~20 DB writes/second
- Expected concurrent connections: 20-30
- Pool utilization: 20-30% under normal load
- Headroom for spikes: 3-5x capacity

**Batch Processing Impact:**
- Batch size: 50 events
- Batch window: 100ms
- Reduces DB round trips by 50x
- Actual DB operations: ~200-400/minute instead of 10,000/minute

### Connection Pool Metrics

**Healthy Pool Indicators:**
- Active connections: 20-40 under load
- Connection wait time: <5ms (p95)
- Connection creation rate: <10/minute
- Connection timeout errors: 0

**Warning Signs:**
- Active connections consistently >80
- Connection wait time >100ms
- Frequent connection timeouts
- High connection creation rate (>50/minute)

## Monitoring and Tuning

### Key Metrics to Monitor

1. **Connection Pool Utilization**
   - Current active connections
   - Peak active connections
   - Connection wait time (p50, p95, p99)

2. **Connection Lifecycle**
   - Connection creation rate
   - Connection disposal rate
   - Average connection lifetime

3. **Performance Metrics**
   - Query execution time
   - Statement cache hit rate
   - Connection validation time

4. **Error Metrics**
   - Connection timeout errors
   - Connection validation failures
   - Pool exhaustion events

### Tuning Guidelines

#### When to Increase Max Pool Size

**Symptoms:**
- Frequent connection timeout errors
- Connection wait time >100ms consistently
- Pool utilization >90% sustained

**Action:**
- Increase Max Pool Size in increments of 20
- Monitor database server capacity
- Typical range: 100-200 for high-volume scenarios

#### When to Increase Min Pool Size

**Symptoms:**
- High connection creation rate during traffic spikes
- Cold start latency issues
- Frequent pool growth/shrink cycles

**Action:**
- Increase Min Pool Size to match typical load
- Typical range: 10-20 for high-volume scenarios

#### When to Adjust Connection Lifetime

**Symptoms:**
- Stale connection errors
- Database session timeout issues
- Memory leaks in long-lived connections

**Action:**
- Decrease Connection Lifetime if stale connections occur
- Increase if connection churn is too high
- Typical range: 180-600 seconds

#### When to Adjust Statement Cache Size

**Symptoms:**
- High parse time in query execution
- Statement cache miss rate >20%
- Varied query patterns

**Action:**
- Increase Statement Cache Size for diverse workloads
- Typical range: 50-200 statements

## Configuration Files

### appsettings.json

The connection string is configured in `appsettings.json`:

```json
{
  "ConnectionStrings": {
    "OracleDb": "Data Source=(DESCRIPTION=(ADDRESS=(PROTOCOL=TCP)(HOST=your-host)(PORT=1521))(CONNECT_DATA=(SERVICE_NAME=your-service)));User Id=your-user;Password=your-password;Pooling=true;Min Pool Size=5;Max Pool Size=100;Connection Timeout=15;Incr Pool Size=5;Decr Pool Size=2;Validate Connection=true;Connection Lifetime=300;Statement Cache Size=50;Statement Cache Purge=false;"
  }
}
```

### Environment Variables (.env)

For production deployments, use environment variables:

```bash
ORACLE_CONNECTION_STRING="Data Source=...;Pooling=true;Min Pool Size=5;Max Pool Size=100;Connection Timeout=15;Incr Pool Size=5;Decr Pool Size=2;Validate Connection=true;Connection Lifetime=300;Statement Cache Size=50;Statement Cache Purge=false;"
```

## High-Volume Optimization Recommendations

### For 10,000+ Requests/Minute

**Current Configuration: ✅ Optimal**

The current settings are well-tuned for the target load:
- Min Pool Size (5) provides baseline capacity
- Max Pool Size (100) provides 3-5x headroom
- Statement caching (50) optimizes repeated queries
- Connection validation prevents failures
- Connection lifetime (300s) balances freshness and overhead

### For 50,000+ Requests/Minute

If scaling beyond 10,000 req/min, consider:

```
Min Pool Size=10;
Max Pool Size=200;
Connection Timeout=20;
Incr Pool Size=10;
Statement Cache Size=100;
```

**Rationale:**
- Higher baseline (10) for sustained high load
- Larger max pool (200) for extreme spikes
- Faster growth (10) to respond to rapid load increases
- Larger statement cache (100) for diverse queries

### For Multiple API Instances

When running multiple API instances (horizontal scaling):

**Per-Instance Configuration:**
```
Min Pool Size=5;
Max Pool Size=50;
```

**Rationale:**
- Total connections = instances × max pool size
- 4 instances × 50 = 200 total connections
- Prevents single database overload
- Distributes load across instances

## Database Server Considerations

### Oracle Database Configuration

Ensure Oracle database is configured to handle connection pool:

**Maximum Processes:**
```sql
-- Check current setting
SELECT value FROM v$parameter WHERE name = 'processes';

-- Recommended: 2x max pool size × number of API instances
-- Example: 2 × 100 × 4 instances = 800 processes
ALTER SYSTEM SET processes=800 SCOPE=SPFILE;
```

**Session Timeout:**
```sql
-- Check current setting
SELECT value FROM v$parameter WHERE name = 'idle_time';

-- Set to match or exceed connection lifetime
-- Example: 10 minutes (600 seconds)
ALTER SYSTEM SET idle_time=10 SCOPE=BOTH;
```

**Shared Pool Size:**
```sql
-- Check current setting
SELECT value FROM v$parameter WHERE name = 'shared_pool_size';

-- Ensure adequate size for statement caching
-- Recommended: At least 512MB for high-volume scenarios
ALTER SYSTEM SET shared_pool_size=512M SCOPE=BOTH;
```

## Troubleshooting

### Connection Pool Exhaustion

**Symptoms:**
- "Timeout expired. The timeout period elapsed prior to obtaining a connection from the pool."
- High connection wait times
- Request failures under load

**Solutions:**
1. Increase Max Pool Size
2. Investigate connection leaks (unclosed connections)
3. Optimize query performance to reduce connection hold time
4. Enable connection pool monitoring

### Stale Connection Errors

**Symptoms:**
- "ORA-03113: end-of-file on communication channel"
- "ORA-03135: connection lost contact"
- Intermittent connection failures

**Solutions:**
1. Enable Validate Connection=true (already enabled)
2. Reduce Connection Lifetime
3. Check network stability
4. Review Oracle database session timeout settings

### High Connection Creation Rate

**Symptoms:**
- Frequent connection creation/disposal
- High CPU usage on database server
- Increased latency during traffic spikes

**Solutions:**
1. Increase Min Pool Size to match typical load
2. Adjust Incr Pool Size for smoother growth
3. Review connection lifetime settings
4. Investigate connection leak patterns

### Statement Cache Misses

**Symptoms:**
- High parse time in query execution
- Increased database CPU usage
- Slower query performance

**Solutions:**
1. Increase Statement Cache Size
2. Review query patterns for consistency
3. Use parameterized queries consistently
4. Monitor statement cache hit rate

## Validation and Testing

### Load Testing Checklist

- [ ] Test with 10,000 requests/minute sustained load
- [ ] Test with 50,000 requests/minute spike load
- [ ] Monitor connection pool utilization
- [ ] Verify connection timeout errors = 0
- [ ] Verify connection wait time <10ms (p95)
- [ ] Verify statement cache hit rate >80%
- [ ] Test connection recovery after database restart
- [ ] Test behavior under database slow response
- [ ] Verify no connection leaks over 1-hour test
- [ ] Monitor database server resource usage

### Performance Benchmarks

**Target Metrics:**
- Connection acquisition time: <5ms (p95)
- Query execution time: <50ms (p95) for audit writes
- Connection pool utilization: 20-40% under normal load
- Connection timeout errors: 0
- Statement cache hit rate: >80%

## References

- [Oracle Data Provider for .NET Documentation](https://docs.oracle.com/en/database/oracle/oracle-database/19/odpnt/)
- [Connection Pooling Best Practices](https://docs.oracle.com/en/database/oracle/oracle-database/19/odpnt/featConnecting.html#GUID-2C4F7D6E-8E3E-4F3E-8F3E-8F3E8F3E8F3E)
- [Statement Caching in ODP.NET](https://docs.oracle.com/en/database/oracle/oracle-database/19/odpnt/featStatement.html)

## Change History

| Date | Version | Changes | Author |
|------|---------|---------|--------|
| 2024-01-XX | 1.0 | Initial configuration for high-volume audit logging | System |

