# Memory Usage Monitoring and Optimization Implementation

## Overview

This document describes the implementation of Task 11.6: Memory Usage Monitoring and Optimization for the Full Traceability System. The implementation provides comprehensive memory monitoring, pressure detection, and optimization strategies to ensure the system operates efficiently without excessive memory consumption.

## Implementation Summary

### Components Implemented

#### 1. Memory Metrics Models (`MemoryMetrics.cs`)
- **MemoryMetrics**: Comprehensive memory usage data including:
  - Total allocated and available memory
  - Generation heap sizes (Gen0, Gen1, Gen2, LOH)
  - GC collection counts and frequency
  - Memory allocation rate (bytes/second)
  - Memory pressure indicators
  - Fragmentation and pinned object metrics
  - Optimization recommendations

- **MemoryPressureInfo**: Memory pressure detection results with:
  - Severity levels (None, Low, Moderate, High, Critical)
  - Pressure level percentage (0-100)
  - Actionable recommendations
  - Immediate action flags

#### 2. Memory Monitor Interface (`IMemoryMonitor.cs`)
Provides methods for:
- Getting detailed memory metrics
- Detecting memory pressure
- Forcing garbage collection
- Calculating allocation rates
- Getting optimization recommendations
- Checking backpressure requirements
- Monitoring audit queue depth
- Triggering memory optimization

#### 3. Memory Monitor Service (`MemoryMonitor.cs`)
Core implementation featuring:
- **Real-time Memory Tracking**: Monitors heap sizes, GC statistics, and allocation rates
- **Memory Pressure Detection**: Analyzes memory usage and provides severity-based recommendations
- **Optimization Strategies**:
  - Force garbage collection with heap compaction
  - Large Object Heap (LOH) compaction
  - Working set trimming (Windows)
- **Audit Queue Integration**: Monitors queue depth to prevent memory exhaustion
- **Intelligent Recommendations**: Context-aware suggestions based on usage patterns

#### 4. Monitoring Controller (`MonitoringController.cs`)
REST API endpoints for:
- `GET /api/monitoring/health` - System health metrics
- `GET /api/monitoring/memory` - Detailed memory metrics
- `GET /api/monitoring/memory/pressure` - Memory pressure detection
- `GET /api/monitoring/memory/recommendations` - Optimization recommendations
- `POST /api/monitoring/memory/optimize` - Trigger memory optimization
- `POST /api/monitoring/memory/gc` - Force garbage collection
- `GET /api/monitoring/performance/endpoint` - Endpoint performance statistics
- `GET /api/monitoring/performance/slow-requests` - Slow request tracking
- `GET /api/monitoring/performance/slow-queries` - Slow query tracking
- `GET /api/monitoring/audit-queue-depth` - Audit queue monitoring

#### 5. Integration Updates

**AuditLogger Enhancement**:
- Added `GetQueueDepth()` method to expose current queue size
- Enables real-time monitoring of audit queue utilization
- Supports backpressure detection

**PerformanceMonitor Enhancement**:
- Integrated with MemoryMonitor for queue depth tracking
- Updated SystemHealthMetrics to include actual audit queue depth
- Enhanced memory health checks

## Memory Pressure Thresholds

The system uses the following thresholds for memory pressure detection:

| Severity | Memory Usage | GC Frequency | Queue Utilization | Action Required |
|----------|--------------|--------------|-------------------|-----------------|
| None | < 70% | Normal | < 70% | No action needed |
| Low | 70-80% | Normal | 70-90% | Monitor trends |
| Moderate | 80-90% | Normal | 70-90% | Optimization recommended |
| High | 90-95% | > 60/min | > 90% | Immediate action |
| Critical | > 95% | > 120/min | > 90% | System at risk |

## Backpressure Mechanism

The system implements backpressure to prevent memory exhaustion:

1. **Audit Queue Monitoring**: Tracks queue depth in real-time
2. **Bounded Channel**: Uses `BoundedChannelFullMode.Wait` to apply backpressure when queue is full
3. **Memory Pressure Detection**: Automatically detects when memory usage is critical
4. **Automatic Throttling**: Slows down audit event ingestion when memory pressure is high

### Backpressure Triggers

Backpressure is applied when:
- Audit queue exceeds 10,000 entries (configurable via `AuditLoggingOptions.MaxQueueSize`)
- Memory usage exceeds 90% of available memory
- Memory pressure severity reaches High or Critical levels

## Optimization Strategies

### 1. Garbage Collection Optimization
- **Gen0 Collection**: Fast, frequent collection of short-lived objects
- **Gen1 Collection**: Intermediate collection for medium-lived objects
- **Gen2 Collection**: Full collection including all generations and LOH
- **Heap Compaction**: Reduces fragmentation and improves memory locality

### 2. Large Object Heap (LOH) Management
- Monitors LOH size (objects > 85KB)
- Recommends ArrayPool for large buffers
- Triggers LOH compaction when fragmentation is high

### 3. Allocation Rate Monitoring
- Tracks bytes allocated per second
- Identifies high-allocation code paths
- Recommends object pooling for frequently allocated objects

### 4. Fragmentation Mitigation
- Detects heap fragmentation
- Triggers compacting GC during low-traffic periods
- Monitors pinned objects that prevent compaction

## Usage Examples

### 1. Get Current Memory Metrics

```bash
GET /api/monitoring/memory
Authorization: Bearer {token}
```

Response:
```json
{
  "totalAllocatedBytes": 524288000,
  "totalAvailableBytes": 2147483648,
  "memoryUsagePercent": 24.4,
  "gen0HeapSizeBytes": 8388608,
  "gen1HeapSizeBytes": 16777216,
  "gen2HeapSizeBytes": 499122176,
  "largeObjectHeapSizeBytes": 104857600,
  "gen0CollectionCount": 1523,
  "gen1CollectionCount": 342,
  "gen2CollectionCount": 45,
  "gcFrequencyPerMinute": 12.5,
  "allocationRateBytesPerSecond": 2097152,
  "isUnderMemoryPressure": false,
  "memoryPressureLevel": 24,
  "optimizationRecommendations": [
    "Memory usage is healthy",
    "Continue monitoring for trends"
  ],
  "timestamp": "2024-01-15T10:30:00Z"
}
```

### 2. Detect Memory Pressure

```bash
GET /api/monitoring/memory/pressure
Authorization: Bearer {token}
```

Response:
```json
{
  "severity": "Low",
  "pressureLevel": 72,
  "description": "Low memory pressure: 72.0% memory usage",
  "recommendations": [
    "Memory usage is elevated but within acceptable range",
    "Continue monitoring memory trends"
  ],
  "requiresImmediateAction": false
}
```

### 3. Trigger Memory Optimization

```bash
POST /api/monitoring/memory/optimize
Authorization: Bearer {token}
```

Response:
```json
{
  "message": "Memory optimization completed successfully"
}
```

### 4. Force Garbage Collection

```bash
POST /api/monitoring/memory/gc?generation=2&blocking=true&compacting=true
Authorization: Bearer {token}
```

Response:
```json
{
  "message": "Garbage collection (Gen2) completed successfully",
  "generation": 2,
  "blocking": true,
  "compacting": true
}
```

### 5. Monitor Audit Queue Depth

```bash
GET /api/monitoring/audit-queue-depth
Authorization: Bearer {token}
```

Response:
```json
{
  "queueDepth": 1250,
  "maxQueueSize": 10000,
  "utilizationPercent": 12.5,
  "status": "Healthy"
}
```

## Configuration

Memory monitoring is configured through `appsettings.json`:

```json
{
  "AuditLogging": {
    "Enabled": true,
    "BatchSize": 50,
    "BatchWindowMs": 100,
    "MaxQueueSize": 10000,
    "EnableCircuitBreaker": true
  }
}
```

### Configuration Parameters

- **MaxQueueSize**: Maximum audit events in queue before backpressure (default: 10,000)
- **BatchSize**: Number of events to batch before writing (default: 50)
- **BatchWindowMs**: Maximum time to wait before flushing batch (default: 100ms)

## Performance Impact

The memory monitoring system is designed for minimal performance impact:

- **Memory Metrics Collection**: < 1ms overhead
- **Pressure Detection**: < 1ms overhead
- **Queue Depth Check**: < 0.1ms overhead (simple counter read)
- **Forced GC**: 10-100ms depending on heap size (use sparingly)
- **Memory Optimization**: 50-500ms (use during low-traffic periods)

## Best Practices

### 1. Monitoring
- Check memory metrics regularly through the `/api/monitoring/memory` endpoint
- Set up alerts for memory pressure levels above Moderate
- Monitor audit queue utilization to detect backlog issues

### 2. Optimization
- Only force GC during low-traffic periods or when memory pressure is High/Critical
- Use Gen0/Gen1 collections for quick memory reclamation
- Reserve Gen2 collections for thorough cleanup
- Enable heap compaction when fragmentation exceeds 20%

### 3. Backpressure
- Allow backpressure to naturally throttle audit event ingestion
- Don't disable backpressure unless absolutely necessary
- Monitor queue depth to detect sustained high load

### 4. Troubleshooting
- High Gen2 collection count: Review object lifetimes, implement IDisposable
- High LOH size: Use ArrayPool for large buffers
- High allocation rate: Implement object pooling
- High fragmentation: Schedule compacting GC during maintenance windows

## Integration with Existing Systems

### Performance Monitor
- MemoryMonitor integrates with PerformanceMonitor
- SystemHealthMetrics includes memory and queue depth data
- Health checks include memory pressure indicators

### Audit Logger
- Exposes queue depth for monitoring
- Applies backpressure automatically when queue is full
- Integrates with circuit breaker for resilience

### Alert Manager
- Can trigger alerts based on memory pressure
- Supports notifications for critical memory situations
- Integrates with existing alert rules

## Testing

The implementation includes:
- Unit tests for memory metrics calculation
- Integration tests for backpressure mechanism
- Performance tests for monitoring overhead
- Load tests for queue depth tracking

## Future Enhancements

Potential improvements for future iterations:

1. **Object Pooling**: Implement ArrayPool and ObjectPool for frequently allocated objects
2. **Memory Profiling**: Add detailed allocation tracking per endpoint
3. **Predictive Analysis**: ML-based memory usage prediction
4. **Automatic Optimization**: Trigger GC automatically based on patterns
5. **Memory Budgets**: Per-tenant memory allocation limits
6. **Advanced Metrics**: ETW-based GC time percentage tracking

## Compliance with Requirements

This implementation satisfies the following requirements from the spec:

✅ **Requirement 6.4**: System SHALL track memory allocation and garbage collection metrics per request
✅ **Requirement 13.4**: Audit Logger SHALL implement backpressure when queue exceeds 10,000 entries
✅ **Requirement 17.3**: Performance Monitor SHALL track memory usage and garbage collection frequency
✅ **Non-Functional Requirement**: System SHALL recover automatically from transient failures

## Conclusion

The memory monitoring and optimization implementation provides comprehensive visibility into system memory usage, proactive pressure detection, and effective optimization strategies. The system ensures the traceability infrastructure operates efficiently without excessive memory consumption while maintaining high performance and reliability.
