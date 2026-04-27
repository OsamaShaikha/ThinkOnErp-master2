# Performance Tuning Guide

## Overview

This guide provides detailed instructions for tuning ThinkOnErp's performance parameters across audit logging, database connections, query execution, and monitoring systems.

---

## 1. Audit Logging Performance

### Batch Processing Tuning

The audit logger uses `System.Threading.Channels` with configurable batching:

| Parameter | Default | High-Throughput | Low-Latency | Memory-Constrained |
|---|---|---|---|---|
| `BatchSize` | 50 | 100 | 10 | 25 |
| `BatchWindowMs` | 100 | 200 | 50 | 100 |
| `MaxQueueSize` | 10000 | 50000 | 1000 | 1000 |
| `DatabaseTimeoutSeconds` | 30 | 60 | 15 | 30 |

```json
{
  "AuditLogging": {
    "BatchSize": 100,
    "BatchWindowMs": 200,
    "MaxQueueSize": 50000,
    "DatabaseTimeoutSeconds": 60
  }
}
```

**Guidelines:**
- Increase `BatchSize` to reduce database round trips (50x fewer calls at BatchSize=50)
- Increase `BatchWindowMs` for higher throughput at the cost of latency
- `MaxQueueSize` controls backpressure — set higher for bursty workloads
- Monitor queue depth via `/api/monitoring/audit/health`

### Circuit Breaker Tuning

```json
{
  "AuditLogging": {
    "EnableCircuitBreaker": true,
    "CircuitBreakerFailureThreshold": 5,
    "CircuitBreakerTimeoutSeconds": 60
  }
}
```

- Lower `FailureThreshold` for faster failure detection
- Increase `TimeoutSeconds` if database recovery is slow

---

## 2. Oracle Connection Pooling

### Recommended Settings

```json
{
  "ConnectionStrings": {
    "OracleDb": "Data Source=...;Min Pool Size=10;Max Pool Size=100;Connection Timeout=30;Incr Pool Size=5;Decr Pool Size=2;Connection Lifetime=300;"
  }
}
```

| Parameter | Recommendation | Notes |
|---|---|---|
| `Min Pool Size` | 10-20 | Pre-warm connections for faster first requests |
| `Max Pool Size` | 50-200 | Based on concurrent user count |
| `Connection Timeout` | 30s | Time to wait for available connection |
| `Incr Pool Size` | 5 | Connections added when pool grows |
| `Decr Pool Size` | 2 | Connections removed when idle |
| `Connection Lifetime` | 300s | Forces connection refresh to prevent stale connections |

### Monitoring

Check pool health via: `GET /api/monitoring/connection-pool`

Key metrics to watch:
- Pool utilization percentage (alert at >80%)
- Time waiting for connection (alert at >5s)
- Connection creation rate (high rate = pool too small)

---

## 3. Query Performance

### Audit Query Tuning

```json
{
  "AuditQueryCaching": {
    "Enabled": true,
    "CacheDurationMinutes": 5,
    "MaxCachedQueries": 1000,
    "QueryTimeoutSeconds": 30
  }
}
```

**Best Practices:**
- Use date range filters — queries without date filters scan entire table
- Enable Redis caching for frequently accessed queries
- Use covering indexes for common filter combinations
- Monitor slow queries via `GET /api/monitoring/slow-queries`

### Index Recommendations

The system creates these indexes automatically:
- `IDX_AUDIT_LOG_CORRELATION` — correlation ID lookups
- `IDX_AUDIT_LOG_BRANCH` — branch-based filtering
- `IDX_AUDIT_LOG_CATEGORY` — event category filtering
- Composite indexes for company+date, actor+date, entity+date

### Table Partitioning

SYS_AUDIT_LOG uses date-based partitioning:
- Monthly partitions for recent data (last 12 months)
- Quarterly partitions for older data
- Partition pruning automatically eliminates irrelevant partitions

---

## 4. Performance Monitoring Tuning

```json
{
  "PerformanceMonitoring": {
    "Enabled": true,
    "SlowRequestThresholdMs": 5000,
    "SlowQueryThresholdMs": 2000,
    "MetricsWindowMinutes": 60,
    "AggregationIntervalMinutes": 60,
    "MaxMetricsInMemory": 10000
  }
}
```

| Parameter | Low Traffic | High Traffic |
|---|---|---|
| `SlowRequestThresholdMs` | 5000 | 2000 |
| `MetricsWindowMinutes` | 60 | 15 |
| `MaxMetricsInMemory` | 10000 | 50000 |

---

## 5. Archival Tuning

```json
{
  "Archival": {
    "Enabled": true,
    "BatchSize": 1000,
    "RunIntervalHours": 24,
    "CompressionLevel": "Optimal",
    "MaxConcurrentOperations": 4
  }
}
```

- Increase `BatchSize` for faster archival (uses more memory)
- Set `CompressionLevel` to `Fastest` if CPU is constrained
- Schedule during off-peak hours via cron expression

---

## 6. Memory Optimization

### Monitor Current Usage

```http
GET /api/monitoring/memory
Authorization: Bearer {admin-token}
```

### Key Configuration

```json
{
  "MemoryMonitoring": {
    "WarningThresholdMB": 512,
    "CriticalThresholdMB": 1024,
    "CollectionIntervalSeconds": 60
  }
}
```

### Tips
- Reduce `MaxQueueSize` if memory pressure is high
- Lower `MaxMetricsInMemory` for performance monitoring
- Configure archival to run more frequently to reduce table sizes
- Use `AuditLogging:MaxPayloadSize` to limit captured request/response sizes

---

## 7. Load Testing Benchmarks

The system has been validated with these targets:

| Metric | Target | Achieved |
|---|---|---|
| Request throughput | 10,000 req/min | ✅ |
| Audit write latency (p95) | <50ms | ✅ |
| API latency overhead | <10ms for 99% | ✅ |
| Query response (30-day) | <2 seconds | ✅ |
| Spike load | 50,000 req/min burst | ✅ |

Run load tests: `dotnet test tests/LoadTests`
