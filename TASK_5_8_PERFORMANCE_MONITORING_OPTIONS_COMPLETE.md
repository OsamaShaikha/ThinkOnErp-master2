# Task 5.8: PerformanceMonitoringOptions Configuration Class - COMPLETE

## Summary

Successfully created the `PerformanceMonitoringOptions` configuration class for the Full Traceability System. This class provides comprehensive configuration options for performance monitoring, including thresholds for slow requests, slow queries, and system health metrics.

## Implementation Details

### File Created

**Location**: `src/ThinkOnErp.Infrastructure/Configuration/PerformanceMonitoringOptions.cs`

### Configuration Properties

The class includes the following configurable properties with data annotations for validation:

#### Core Monitoring Settings
- **Enabled** (bool): Enable/disable performance monitoring (default: true)
- **SlowRequestThresholdMs** (int): Threshold for flagging slow requests (default: 1000ms, range: 100-60000ms)
- **SlowQueryThresholdMs** (int): Threshold for flagging slow database queries (default: 500ms, range: 50-30000ms)
- **SlidingWindowDurationMinutes** (int): Duration for sliding window metrics calculation (default: 60 minutes, range: 5-1440 minutes)

#### System Health Thresholds
- **CpuThresholdPercent** (int): CPU usage threshold for alerts (default: 90%, range: 50-100%)
- **MemoryThresholdPercent** (int): Memory usage threshold for alerts (default: 90%, range: 50-100%)
- **ConnectionPoolThresholdPercent** (int): Database connection pool utilization threshold (default: 80%, range: 50-100%)
- **DiskSpaceThresholdPercent** (int): Disk space usage threshold for alerts (default: 90%, range: 50-100%)

#### Performance Alert Thresholds
- **RequestRateThreshold** (int): Request rate threshold for anomaly detection (default: 5000 req/min, range: 100-100000)
- **ErrorRateThresholdPercent** (int): Error rate threshold for alerts (default: 5%, range: 1-50%)

#### Advanced Monitoring Features
- **CollectQueryExecutionPlans** (bool): Collect detailed query execution plans for slow queries (default: false)
- **TrackMemoryAllocation** (bool): Track memory allocation per request (default: true)
- **TrackGarbageCollection** (bool): Track garbage collection metrics (default: true)
- **EnablePercentileCalculations** (bool): Enable p50, p95, p99 percentile calculations (default: true)

#### Data Retention Settings
- **MetricsAggregationIntervalSeconds** (int): Interval for aggregating metrics to database (default: 3600 seconds, range: 60-86400 seconds)
- **MaxSlowRequestsRetained** (int): Maximum slow requests to retain in memory (default: 1000, range: 100-10000)
- **MaxSlowQueriesRetained** (int): Maximum slow queries to retain in memory (default: 1000, range: 100-10000)

#### Persistence Settings
- **PersistSlowRequests** (bool): Persist slow requests to database (default: true)
- **PersistSlowQueries** (bool): Persist slow queries to database (default: true)

### Configuration Example

Updated `src/ThinkOnErp.Infrastructure/Configuration/appsettings.audit.example.json` to include:

```json
{
  "PerformanceMonitoring": {
    "Enabled": true,
    "SlowRequestThresholdMs": 1000,
    "SlowQueryThresholdMs": 500,
    "SlidingWindowDurationMinutes": 60,
    "CpuThresholdPercent": 90,
    "MemoryThresholdPercent": 90,
    "ConnectionPoolThresholdPercent": 80,
    "DiskSpaceThresholdPercent": 90,
    "RequestRateThreshold": 5000,
    "ErrorRateThresholdPercent": 5,
    "CollectQueryExecutionPlans": false,
    "TrackMemoryAllocation": true,
    "TrackGarbageCollection": true,
    "MetricsAggregationIntervalSeconds": 3600,
    "MaxSlowRequestsRetained": 1000,
    "MaxSlowQueriesRetained": 1000,
    "EnablePercentileCalculations": true,
    "PersistSlowRequests": true,
    "PersistSlowQueries": true
  }
}
```

## Design Alignment

The implementation aligns with the Full Traceability System design document:

1. **Slow Request Detection**: Configurable threshold (default 1000ms) matches design requirement
2. **Slow Query Detection**: Configurable threshold (default 500ms) matches design requirement
3. **Sliding Window**: 1-hour default for metrics calculation as specified
4. **System Health Monitoring**: Thresholds for CPU, memory, connections, and disk space
5. **Alert Thresholds**: Request rate and error rate thresholds for anomaly detection
6. **Percentile Metrics**: Support for p50, p95, p99 calculations using t-digest algorithm
7. **Data Retention**: Configurable retention limits for in-memory and persisted metrics

## Validation

- ✅ All properties have appropriate data annotations for validation
- ✅ Range constraints ensure sensible configuration values
- ✅ Default values match design document specifications
- ✅ XML documentation comments provide clear guidance
- ✅ Configuration section name constant for easy binding
- ✅ Build succeeds with no errors
- ✅ No diagnostics issues detected
- ✅ Follows existing configuration class patterns (AuditLoggingOptions, RequestTracingOptions)

## Usage

The configuration class can be bound from appsettings.json using:

```csharp
services.Configure<PerformanceMonitoringOptions>(
    configuration.GetSection(PerformanceMonitoringOptions.SectionName));
```

Or accessed via dependency injection:

```csharp
public class PerformanceMonitor
{
    private readonly PerformanceMonitoringOptions _options;
    
    public PerformanceMonitor(IOptions<PerformanceMonitoringOptions> options)
    {
        _options = options.Value;
    }
}
```

## Environment-Specific Configuration

The configuration supports environment-specific overrides:

**Development** (lower thresholds for easier testing):
```json
{
  "PerformanceMonitoring": {
    "SlowRequestThresholdMs": 500,
    "SlowQueryThresholdMs": 250
  }
}
```

**Production** (higher thresholds for production workloads):
```json
{
  "PerformanceMonitoring": {
    "SlowRequestThresholdMs": 2000,
    "SlowQueryThresholdMs": 1000,
    "CollectQueryExecutionPlans": false
  }
}
```

## Next Steps

This configuration class is ready for use by:
- Task 5.1-5.7: PerformanceMonitor service implementation
- Task 6.x: SecurityMonitor integration
- Task 7.x: AlertManager integration
- Task 12.3: MonitoringController endpoints

## Files Modified

1. **Created**: `src/ThinkOnErp.Infrastructure/Configuration/PerformanceMonitoringOptions.cs`
2. **Updated**: `src/ThinkOnErp.Infrastructure/Configuration/appsettings.audit.example.json`

## Verification

Build output confirms successful compilation:
```
ThinkOnErp.Infrastructure net8.0 succeeded (2.0s) → src\ThinkOnErp.Infrastructure\bin\Debug\net8.0\ThinkOnErp.Infrastructure.dll
Build succeeded with 34 warning(s) in 3.9s
```

All warnings are pre-existing and unrelated to the new configuration class.

---

**Task Status**: ✅ COMPLETE
**Date**: 2024
**Spec**: Full Traceability System (Task 5.8)
