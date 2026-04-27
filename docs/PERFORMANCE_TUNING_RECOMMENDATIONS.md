# Performance Tuning Recommendations for Full Traceability System

## Document Overview

This document provides comprehensive performance tuning recommendations for the ThinkOnErp Full Traceability System. These recommendations are based on the system's architecture, expected load patterns (10,000+ requests per minute), and performance targets (<10ms overhead for 99% of requests).

**Target Audience**: System Administrators, DevOps Engineers, Database Administrators

**Last Updated**: 2026-05-03

---

## Table of Contents

1. [Oracle Database Configuration](#oracle-database-configuration)
2. [Connection Pooling Configuration](#connection-pooling-configuration)
3. [Audit Logging Configuration](#audit-logging-configuration)
4. [Redis Cache Configuration](#redis-cache-configuration)
5. [Memory Management](#memory-management)
6. [Background Service Configuration](#background-service-configuration)
7. [ASP.NET Core Configuration](#aspnet-core-configuration)
8. [Operating System Tuning](#operating-system-tuning)
9. [Monitoring and Alerting Thresholds](#monitoring-and-alerting-thresholds)
10. [Load Testing Recommendations](#load-testing-recommendations)

---

## 1. Oracle Database Configuration

### 1.1 Connection Pool Settings

**Recommended Configuration** (appsettings.json):

```json
{
  "ConnectionStrings": {
    "OracleDb": "User Id=THINKONERP;Password=your_password;Data Source=your_oracle_server:1521/ORCL;Min Pool Size=10;Max Pool Size=100;Connection Lifetime=300;Connection Timeout=30;Incr Pool Size=5;Decr Pool Size=2;Pooling=true;Validate Connection=true;Statement Cache Size=50;"
  }
}
```

**Parameter Explanations**:

| Parameter | Recommended Value | Rationale |
|-----------|------------------|-----------|
| `Min Pool Size` | 10 | Maintains minimum connections ready for immediate use |
| `Max Pool Size` | 100 | Supports 10,000 req/min with avg 600ms query time |
| `Connection Lifetime` | 300 seconds | Recycles connections every 5 minutes to prevent stale connections |
| `Connection Timeout` | 30 seconds | Fails fast if database is unavailable |
| `Incr Pool Size` | 5 | Grows pool gradually under load |
| `Decr Pool Size` | 2 | Shrinks pool slowly when load decreases |
| `Pooling` | true | **CRITICAL**: Must be enabled for performance |
| `Validate Connection` | true | Validates connections before use (prevents stale connection errors) |
| `Statement Cache Size` | 50 | Caches prepared statements for reuse |

**Calculation for Max Pool Size**:
```
Max Pool Size = (Peak Requests per Second × Average Query Time) / 1000
Example: (167 req/s × 600ms) / 1000 = 100 connections
```

### 1.2 Oracle Database Server Parameters

**Recommended Oracle init.ora / spfile parameters**:

```sql
-- Session and Process Limits
ALTER SYSTEM SET PROCESSES=500 SCOPE=SPFILE;
ALTER SYSTEM SET SESSIONS=550 SCOPE=SPFILE;

-- Memory Configuration (adjust based on available RAM)
ALTER SYSTEM SET SGA_TARGET=4G SCOPE=SPFILE;
ALTER SYSTEM SET PGA_AGGREGATE_TARGET=2G SCOPE=SPFILE;

-- Shared Pool (for statement caching)
ALTER SYSTEM SET SHARED_POOL_SIZE=1G SCOPE=SPFILE;

-- Buffer Cache (for frequently accessed data)
ALTER SYSTEM SET DB_CACHE_SIZE=2G SCOPE=SPFILE;

-- Redo Log Configuration
ALTER SYSTEM SET LOG_BUFFER=64M SCOPE=SPFILE;

-- Optimizer Settings
ALTER SYSTEM SET OPTIMIZER_MODE=ALL_ROWS SCOPE=BOTH;
ALTER SYSTEM SET OPTIMIZER_INDEX_COST_ADJ=100 SCOPE=BOTH;

-- Parallel Execution (for large queries)
ALTER SYSTEM SET PARALLEL_MAX_SERVERS=20 SCOPE=BOTH;
ALTER SYSTEM SET PARALLEL_MIN_SERVERS=4 SCOPE=BOTH;

-- Commit Performance
ALTER SYSTEM SET COMMIT_WRITE='BATCH,NOWAIT' SCOPE=BOTH;

-- Statistics Gathering
ALTER SYSTEM SET STATISTICS_LEVEL=TYPICAL SCOPE=BOTH;
```

**Note**: Restart Oracle database after changing SPFILE parameters.

### 1.3 Table Partitioning Strategy

**Partition SYS_AUDIT_LOG by date for improved query performance**:

```sql
-- Create partitioned table (requires migration from existing table)
CREATE TABLE SYS_AUDIT_LOG_PARTITIONED (
    -- All existing columns
    ROW_ID NUMBER(19) PRIMARY KEY,
    ACTOR_TYPE NVARCHAR2(50) NOT NULL,
    -- ... (all other columns)
    CREATION_DATE DATE NOT NULL
)
PARTITION BY RANGE (CREATION_DATE) INTERVAL (NUMTOYMINTERVAL(1, 'MONTH'))
(
    PARTITION p_initial VALUES LESS THAN (TO_DATE('2024-01-01', 'YYYY-MM-DD'))
);

-- Enable compression for older partitions
ALTER TABLE SYS_AUDIT_LOG_PARTITIONED MODIFY PARTITION p_initial COMPRESS FOR OLTP;
```

**Benefits**:
- Queries with date filters only scan relevant partitions
- Archival operations can drop entire partitions (instant)
- Older partitions can be compressed to save space

### 1.4 Index Optimization

**Verify all recommended indexes exist**:

```sql
-- Check existing indexes
SELECT INDEX_NAME, TABLE_NAME, COLUMN_NAME, COLUMN_POSITION
FROM USER_IND_COLUMNS
WHERE TABLE_NAME = 'SYS_AUDIT_LOG'
ORDER BY INDEX_NAME, COLUMN_POSITION;

-- Rebuild fragmented indexes (run monthly)
ALTER INDEX IDX_AUDIT_LOG_CORRELATION REBUILD ONLINE;
ALTER INDEX IDX_AUDIT_LOG_COMPANY_DATE REBUILD ONLINE;
ALTER INDEX IDX_AUDIT_LOG_ACTOR_DATE REBUILD ONLINE;
ALTER INDEX IDX_AUDIT_LOG_ENTITY_DATE REBUILD ONLINE;

-- Gather statistics (run weekly)
EXEC DBMS_STATS.GATHER_TABLE_STATS('THINKONERP', 'SYS_AUDIT_LOG', CASCADE => TRUE);
```

### 1.5 Oracle Text Configuration (for full-text search)

**Create Oracle Text index for audit log search**:

```sql
-- Create Oracle Text index on searchable columns
CREATE INDEX IDX_AUDIT_LOG_TEXT ON SYS_AUDIT_LOG(ENTITY_TYPE, ACTION, EXCEPTION_MESSAGE, BUSINESS_DESCRIPTION)
INDEXTYPE IS CTXSYS.CONTEXT
PARAMETERS ('SYNC (ON COMMIT)');

-- Optimize index (run daily)
EXEC CTX_DDL.OPTIMIZE_INDEX('IDX_AUDIT_LOG_TEXT', 'FULL');
```

---

## 2. Connection Pooling Configuration

### 2.1 Application-Level Connection Pool Monitoring

**Add connection pool monitoring to appsettings.json**:

```json
{
  "ConnectionPoolMonitoring": {
    "Enabled": true,
    "LogPoolStatsIntervalSeconds": 60,
    "AlertOnPoolExhaustionThreshold": 0.9
  }
}
```

### 2.2 Connection Pool Best Practices

1. **Always use `using` statements** to ensure connections are returned to pool:
   ```csharp
   using (var connection = _dbContext.CreateConnection())
   {
       // Use connection
   } // Connection automatically returned to pool
   ```

2. **Avoid long-running transactions** that hold connections:
   - Keep transactions under 5 seconds
   - Use batch operations instead of loops

3. **Monitor pool exhaustion** in production:
   - Alert when pool utilization exceeds 90%
   - Increase `Max Pool Size` if consistently high

---

## 3. Audit Logging Configuration

### 3.1 Optimal Audit Logging Settings

**Recommended Configuration** (appsettings.json):

```json
{
  "AuditLogging": {
    "Enabled": true,
    "BatchSize": 100,
    "BatchWindowMs": 50,
    "MaxQueueSize": 50000,
    "SensitiveFields": [
      "password",
      "token",
      "refreshToken",
      "creditCard",
      "ssn",
      "apiKey",
      "secret"
    ],
    "MaskingPattern": "***MASKED***",
    "CircuitBreakerFailureThreshold": 5,
    "CircuitBreakerTimeoutSeconds": 60,
    "RetryMaxAttempts": 3,
    "RetryDelayMs": 1000,
    "RetryBackoffMultiplier": 2.0,
    "FallbackToFileSystem": true,
    "FallbackDirectory": "/var/log/thinkonerp/audit-fallback"
  }
}
```

**Parameter Tuning Guidelines**:

| Parameter | Low Load (< 1000 req/min) | Medium Load (1000-5000 req/min) | High Load (> 5000 req/min) |
|-----------|---------------------------|----------------------------------|----------------------------|
| `BatchSize` | 50 | 100 | 200 |
| `BatchWindowMs` | 100 | 50 | 25 |
| `MaxQueueSize` | 10000 | 25000 | 50000 |

**Calculation for MaxQueueSize**:
```
MaxQueueSize = Peak Requests per Minute × 3 (3-minute buffer)
Example: 10,000 req/min × 3 = 30,000 events
```

### 3.2 Async Queue Configuration

**Channel configuration is set in DependencyInjection.cs**:

```csharp
// Current configuration (good for most scenarios)
var channelOptions = new BoundedChannelOptions(50000)
{
    FullMode = BoundedChannelFullMode.DropOldest, // Drop oldest if queue full
    SingleReader = true,  // Single background worker
    SingleWriter = false  // Multiple threads can write
};
```

**For extreme high load (> 20,000 req/min)**:
```csharp
var channelOptions = new BoundedChannelOptions(100000)
{
    FullMode = BoundedChannelFullMode.Wait, // Block writers if queue full
    SingleReader = false, // Multiple background workers
    SingleWriter = false
};
```

### 3.3 Batch Processing Optimization

**Monitor batch processing metrics**:
- Average batch size (should be close to configured `BatchSize`)
- Batch write latency (should be < 100ms for 95% of batches)
- Queue depth (should stay below 50% of `MaxQueueSize`)

**Adjust based on metrics**:
- If batch size is consistently low → Increase `BatchWindowMs`
- If batch write latency is high → Decrease `BatchSize`
- If queue depth is consistently high → Increase `MaxQueueSize` or add more database connections

---

## 4. Redis Cache Configuration

### 4.1 Redis Server Configuration

**Recommended redis.conf settings**:

```conf
# Memory Management
maxmemory 2gb
maxmemory-policy allkeys-lru

# Persistence (for audit caching, we can disable for performance)
save ""
appendonly no

# Network
tcp-backlog 511
timeout 300
tcp-keepalive 60

# Performance
maxclients 10000
```

### 4.2 Application Redis Configuration

**Recommended Configuration** (appsettings.json):

```json
{
  "SecurityMonitoring": {
    "Enabled": true,
    "UseRedisCache": true,
    "RedisConnectionString": "localhost:6379,abortConnect=false,connectTimeout=5000,syncTimeout=5000,defaultDatabase=0,ssl=false,allowAdmin=false"
  },
  "AuditQueryCaching": {
    "Enabled": true,
    "RedisConnectionString": "localhost:6379,abortConnect=false,connectTimeout=5000,syncTimeout=5000,defaultDatabase=1,ssl=false,allowAdmin=false",
    "DefaultCacheDurationMinutes": 15,
    "MaxCacheSizeBytes": 10485760,
    "EnableSlidingExpiration": true
  }
}
```

**Redis Connection String Parameters**:

| Parameter | Value | Rationale |
|-----------|-------|-----------|
| `abortConnect` | false | Don't fail startup if Redis unavailable |
| `connectTimeout` | 5000ms | Fail fast if Redis is slow |
| `syncTimeout` | 5000ms | Timeout for synchronous operations |
| `defaultDatabase` | 0 or 1 | Separate databases for different caches |
| `ssl` | false (true in prod) | Enable SSL in production |

### 4.3 Cache Invalidation Strategy

**Configure cache invalidation for audit queries**:

```json
{
  "AuditQueryCaching": {
    "InvalidateOnWrite": true,
    "InvalidationPatterns": [
      "audit:query:*",
      "audit:entity:*",
      "audit:correlation:*"
    ]
  }
}
```

---

## 5. Memory Management

### 5.1 Garbage Collection Configuration

**For .NET 8 applications, configure GC in appsettings.json**:

```json
{
  "System.GC.Server": true,
  "System.GC.Concurrent": true,
  "System.GC.RetainVM": true,
  "System.GC.HeapCount": 0
}
```

**Or via environment variables**:
```bash
export DOTNET_gcServer=1
export DOTNET_gcConcurrent=1
export DOTNET_GCHeapCount=0  # 0 = auto-detect based on CPU cores
```

### 5.2 Memory Monitoring Configuration

**Recommended Configuration** (appsettings.json):

```json
{
  "MemoryMonitoring": {
    "Enabled": true,
    "MonitoringIntervalSeconds": 30,
    "MemoryThresholdMB": 1024,
    "GCCollectionThreshold": 100,
    "AlertOnHighMemoryUsage": true,
    "HighMemoryThresholdPercent": 85
  }
}
```

### 5.3 Memory Limits (Docker/Kubernetes)

**Docker memory limits**:
```yaml
# docker-compose.yml
services:
  thinkonerp-api:
    image: thinkonerp-api:latest
    deploy:
      resources:
        limits:
          memory: 2G
        reservations:
          memory: 1G
```

**Kubernetes resource limits**:
```yaml
# kubernetes-deployment.yaml
resources:
  requests:
    memory: "1Gi"
    cpu: "500m"
  limits:
    memory: "2Gi"
    cpu: "2000m"
```

---

## 6. Background Service Configuration

### 6.1 Metrics Aggregation Service

**Recommended Configuration** (appsettings.json):

```json
{
  "MetricsAggregation": {
    "Enabled": true,
    "AggregationIntervalMinutes": 60,
    "RetentionDays": 90,
    "BatchSize": 1000
  }
}
```

**Tuning Guidelines**:
- **Low load**: Aggregate every 60 minutes
- **High load**: Aggregate every 30 minutes (reduces memory usage)

### 6.2 Archival Service

**Recommended Configuration** (appsettings.json):

```json
{
  "Archival": {
    "Enabled": true,
    "Schedule": "0 2 * * *",
    "BatchSize": 10000,
    "CompressionEnabled": true,
    "ExternalStorageEnabled": false,
    "ExternalStorageProvider": "S3",
    "S3Configuration": {
      "BucketName": "thinkonerp-audit-archive",
      "Region": "us-east-1",
      "AccessKeyId": "your-access-key",
      "SecretAccessKey": "your-secret-key"
    }
  }
}
```

**Tuning Guidelines**:
- Run archival during low-traffic periods (2 AM recommended)
- Increase `BatchSize` for faster archival (but higher memory usage)
- Enable compression to reduce storage costs (10:1 compression ratio typical)

### 6.3 Alert Processing Service

**Recommended Configuration** (appsettings.json):

```json
{
  "AlertProcessing": {
    "Enabled": true,
    "MaxConcurrentNotifications": 10,
    "NotificationTimeoutSeconds": 30,
    "RetryFailedNotifications": true,
    "MaxRetryAttempts": 3
  }
}
```

---

## 7. ASP.NET Core Configuration

### 7.1 Kestrel Web Server Configuration

**Recommended Configuration** (appsettings.json):

```json
{
  "Kestrel": {
    "Limits": {
      "MaxConcurrentConnections": 1000,
      "MaxConcurrentUpgradedConnections": 1000,
      "MaxRequestBodySize": 10485760,
      "KeepAliveTimeout": "00:02:00",
      "RequestHeadersTimeout": "00:00:30",
      "Http2": {
        "MaxStreamsPerConnection": 100,
        "InitialConnectionWindowSize": 131072,
        "InitialStreamWindowSize": 98304
      }
    },
    "AddServerHeader": false
  }
}
```

### 7.2 Request Tracing Configuration

**Recommended Configuration** (appsettings.json):

```json
{
  "RequestTracing": {
    "Enabled": true,
    "LogPayloads": true,
    "PayloadLoggingLevel": "Metadata",
    "MaxPayloadSize": 10240,
    "ExcludedPaths": [
      "/health",
      "/metrics",
      "/swagger"
    ],
    "CorrelationIdHeader": "X-Correlation-ID"
  }
}
```

**Payload Logging Levels**:
- `None`: No payload logging (fastest, least storage)
- `Metadata`: Log only size and content type (recommended for production)
- `Full`: Log complete payloads (use only for debugging)

### 7.3 Thread Pool Configuration

**Configure thread pool via environment variables**:

```bash
# Minimum threads (prevents slow startup under load)
export DOTNET_ThreadPool_MinThreads=50

# Maximum threads (prevents thread exhaustion)
export DOTNET_ThreadPool_MaxThreads=1000
```

---

## 8. Operating System Tuning

### 8.1 Linux Kernel Parameters

**Recommended /etc/sysctl.conf settings**:

```conf
# Network Performance
net.core.somaxconn = 4096
net.ipv4.tcp_max_syn_backlog = 8192
net.ipv4.tcp_tw_reuse = 1
net.ipv4.tcp_fin_timeout = 30

# File Descriptors
fs.file-max = 100000

# Memory Management
vm.swappiness = 10
vm.dirty_ratio = 15
vm.dirty_background_ratio = 5
```

**Apply changes**:
```bash
sudo sysctl -p
```

### 8.2 File Descriptor Limits

**Increase file descriptor limits** (/etc/security/limits.conf):

```conf
*  soft  nofile  65536
*  hard  nofile  65536
```

**Verify limits**:
```bash
ulimit -n  # Should show 65536
```

### 8.3 Disk I/O Optimization

**For SSD storage**:
```bash
# Set I/O scheduler to none or mq-deadline
echo none > /sys/block/sda/queue/scheduler
```

**For HDD storage**:
```bash
# Set I/O scheduler to deadline
echo deadline > /sys/block/sda/queue/scheduler
```

---

## 9. Monitoring and Alerting Thresholds

### 9.1 Performance Monitoring Thresholds

**Recommended Configuration** (appsettings.json):

```json
{
  "PerformanceMonitoring": {
    "Enabled": true,
    "SlowRequestThresholdMs": 1000,
    "SlowQueryThresholdMs": 500,
    "HighMemoryThresholdMB": 1536,
    "HighCpuThresholdPercent": 80,
    "MetricsRetentionDays": 90
  }
}
```

### 9.2 Alert Configuration

**Recommended Configuration** (appsettings.json):

```json
{
  "Alerts": {
    "Enabled": true,
    "RateLimitPerRulePerHour": 10,
    "EmailNotifications": {
      "Enabled": true,
      "SmtpServer": "smtp.gmail.com",
      "SmtpPort": 587,
      "UseSsl": true,
      "FromAddress": "alerts@thinkonerp.com",
      "ToAddresses": ["admin@thinkonerp.com", "devops@thinkonerp.com"]
    },
    "WebhookNotifications": {
      "Enabled": false,
      "WebhookUrl": "https://hooks.slack.com/services/YOUR/WEBHOOK/URL"
    },
    "SmsNotifications": {
      "Enabled": false,
      "TwilioAccountSid": "your-account-sid",
      "TwilioAuthToken": "your-auth-token",
      "TwilioPhoneNumber": "+1234567890",
      "ToPhoneNumbers": ["+1234567891"]
    }
  }
}
```

### 9.3 Critical Alert Rules

**Configure these alert rules for production**:

1. **Queue Depth Alert**: Alert when audit queue exceeds 80% capacity
2. **Connection Pool Alert**: Alert when connection pool utilization exceeds 90%
3. **Memory Alert**: Alert when memory usage exceeds 85%
4. **Slow Query Alert**: Alert when queries exceed 2 seconds
5. **Failed Login Alert**: Alert on 10+ failed logins from same IP in 5 minutes
6. **Database Failure Alert**: Alert immediately on database connection failures

---

## 10. Load Testing Recommendations

### 10.1 Load Testing Tools

**Recommended tools**:
- **k6**: Modern load testing tool with JavaScript scripting
- **Apache JMeter**: Traditional load testing with GUI
- **Gatling**: Scala-based load testing with detailed reports

### 10.2 Load Testing Scenarios

**Scenario 1: Baseline Performance Test**
- Duration: 10 minutes
- Virtual Users: 100
- Requests per Second: 167 (10,000 req/min)
- Expected: <10ms overhead, <50ms audit write latency

**Scenario 2: Spike Test**
- Duration: 5 minutes
- Virtual Users: Ramp from 100 to 500 in 1 minute
- Requests per Second: Peak 833 (50,000 req/min)
- Expected: System remains stable, queue depth increases but doesn't overflow

**Scenario 3: Sustained Load Test**
- Duration: 1 hour
- Virtual Users: 200
- Requests per Second: 333 (20,000 req/min)
- Expected: No memory leaks, stable performance throughout

### 10.3 Load Testing Metrics to Monitor

**Key metrics during load testing**:

1. **API Response Time**:
   - p50 < 50ms
   - p95 < 100ms
   - p99 < 200ms

2. **Audit Write Latency**:
   - p50 < 20ms
   - p95 < 50ms
   - p99 < 100ms

3. **Database Connection Pool**:
   - Utilization < 80%
   - Wait time < 10ms

4. **Memory Usage**:
   - Heap size stable (no leaks)
   - GC pause time < 50ms

5. **Queue Depth**:
   - Average < 50% of max
   - Peak < 80% of max

### 10.4 Sample k6 Load Test Script

```javascript
import http from 'k6/http';
import { check, sleep } from 'k6';

export let options = {
  stages: [
    { duration: '2m', target: 100 },  // Ramp up to 100 users
    { duration: '5m', target: 100 },  // Stay at 100 users
    { duration: '2m', target: 200 },  // Ramp up to 200 users
    { duration: '5m', target: 200 },  // Stay at 200 users
    { duration: '2m', target: 0 },    // Ramp down to 0 users
  ],
  thresholds: {
    http_req_duration: ['p(95)<200', 'p(99)<500'],
    http_req_failed: ['rate<0.01'],
  },
};

export default function () {
  // Login to get JWT token
  let loginRes = http.post('https://api.thinkonerp.com/api/auth/login', JSON.stringify({
    username: 'testuser',
    password: 'testpassword'
  }), {
    headers: { 'Content-Type': 'application/json' },
  });
  
  check(loginRes, {
    'login successful': (r) => r.status === 200,
  });
  
  let token = loginRes.json('token');
  
  // Make authenticated API calls
  let headers = {
    'Authorization': `Bearer ${token}`,
    'Content-Type': 'application/json',
  };
  
  // Test various endpoints
  http.get('https://api.thinkonerp.com/api/companies', { headers });
  http.get('https://api.thinkonerp.com/api/branches', { headers });
  http.get('https://api.thinkonerp.com/api/users', { headers });
  
  sleep(1);
}
```

---

## Summary of Key Recommendations

### Critical Performance Settings

1. **Oracle Connection Pool**: Min=10, Max=100, Statement Cache=50
2. **Audit Batch Size**: 100 events per batch
3. **Audit Batch Window**: 50ms
4. **Max Queue Size**: 50,000 events
5. **Redis Cache**: 2GB memory, LRU eviction
6. **Kestrel Max Connections**: 1000
7. **Thread Pool Min Threads**: 50

### Performance Targets

- **API Latency**: <10ms overhead for 99% of requests
- **Audit Write Latency**: <50ms for 95% of operations
- **Throughput**: 10,000+ requests per minute
- **Query Performance**: <2 seconds for 30-day date ranges
- **Memory Usage**: <2GB under normal load
- **Connection Pool Utilization**: <80% average

### Monitoring Checklist

- [ ] Configure connection pool monitoring
- [ ] Set up memory usage alerts (>85% threshold)
- [ ] Configure slow query alerts (>500ms threshold)
- [ ] Set up queue depth monitoring (>80% threshold)
- [ ] Configure failed login alerts (>5 attempts in 5 minutes)
- [ ] Set up database failure alerts
- [ ] Configure performance metrics dashboard

### Regular Maintenance Tasks

- **Daily**: Review slow query logs, check alert history
- **Weekly**: Gather Oracle statistics, review performance metrics
- **Monthly**: Rebuild fragmented indexes, review archival logs
- **Quarterly**: Conduct load testing, review capacity planning

---

## Additional Resources

- [Oracle Database Performance Tuning Guide](https://docs.oracle.com/en/database/oracle/oracle-database/19/tgdba/)
- [ASP.NET Core Performance Best Practices](https://learn.microsoft.com/en-us/aspnet/core/performance/performance-best-practices)
- [Redis Performance Optimization](https://redis.io/docs/management/optimization/)
- [k6 Load Testing Documentation](https://k6.io/docs/)

---

**Document Version**: 1.0  
**Last Reviewed**: 2026-05-03  
**Next Review Date**: 2026-08-03
