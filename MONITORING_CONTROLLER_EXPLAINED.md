# MonitoringController - Complete Explanation

## Overview

The **MonitoringController** is a comprehensive system monitoring and observability API that provides real-time insights into your ThinkOnERP application's health, performance, security, and resource usage. It's designed for administrators and DevOps teams to monitor, diagnose, and optimize the system.

---

## Purpose

The Monitoring API serves several critical operational needs:

1. **System Health Monitoring**: Track CPU, memory, database connections, and overall system health
2. **Performance Analysis**: Identify slow requests, slow queries, and performance bottlenecks
3. **Security Monitoring**: Detect threats, failed logins, SQL injection, XSS attempts
4. **Resource Management**: Monitor and optimize memory usage, garbage collection
5. **Audit System Health**: Track audit logging queue, circuit breaker state, fallback status
6. **Alerting**: Test and trigger alerts for critical system events

---

## Key Features

### 🔒 **Admin-Only Access**
- All endpoints require admin privileges (`AdminOnly` policy)
- Exception: `/health` endpoint is public (`AllowAnonymous`) for load balancers

### 📊 **Real-Time Metrics**
- Live system health data
- Current resource utilization
- Active security threats
- Performance statistics

### 🔧 **Operational Controls**
- Force garbage collection
- Optimize memory
- Replay fallback audit events
- Test alert delivery

### 📈 **Historical Analysis**
- Slow request tracking
- Slow query logging
- Security threat history
- Daily security summaries

---

## API Endpoints

### Base URL
```
/api/monitoring
```

---

## 1. System Health Endpoints

### 1.1 Get System Health

**Endpoint**: `GET /api/monitoring/health`

**Access**: Public (AllowAnonymous) - for load balancer health checks

**Purpose**: Comprehensive system health check

**What It Returns**:
- CPU utilization percentage
- Memory usage and GC statistics
- Database connection pool status
- Request rate (requests per second)
- Error rate percentage
- Audit queue depth
- Individual health checks for each subsystem
- Overall health status (Healthy/Degraded/Unhealthy)

**Example Response**:
```json
{
  "status": "Healthy",
  "cpuUsagePercent": 45.2,
  "memoryUsageMB": 512,
  "totalMemoryMB": 2048,
  "requestsPerSecond": 125.5,
  "errorRatePercent": 0.5,
  "auditQueueDepth": 150,
  "databaseConnectionsActive": 5,
  "databaseConnectionsIdle": 15,
  "healthChecks": {
    "database": "Healthy",
    "auditLogging": "Healthy",
    "memory": "Healthy"
  },
  "timestamp": "2026-05-06T00:00:00Z"
}
```

**Use Cases**:
- Load balancer health checks
- Monitoring dashboard
- Automated health monitoring
- Alerting systems

---

## 2. Memory Monitoring Endpoints

### 2.1 Get Memory Metrics

**Endpoint**: `GET /api/monitoring/memory`

**Purpose**: Detailed memory usage analysis

**What It Returns**:
- Total allocated memory
- Available memory
- Generation heap sizes (Gen0, Gen1, Gen2, LOH)
- GC collection counts per generation
- Memory allocation rate
- Memory pressure indicators
- Optimization recommendations

**Example Response**:
```json
{
  "totalAllocatedMB": 512,
  "totalAvailableMB": 1536,
  "gen0HeapSizeMB": 8,
  "gen1HeapSizeMB": 16,
  "gen2HeapSizeMB": 256,
  "largeObjectHeapMB": 128,
  "gen0Collections": 1250,
  "gen1Collections": 85,
  "gen2Collections": 12,
  "allocationRateMBPerSec": 2.5,
  "memoryPressure": "Low",
  "recommendations": [
    "Consider object pooling for frequently allocated objects",
    "Review large object allocations (>85KB)"
  ]
}
```

**Use Cases**:
- Memory leak detection
- Performance optimization
- Capacity planning
- Troubleshooting OOM errors

---

### 2.2 Get Memory Pressure

**Endpoint**: `GET /api/monitoring/memory/pressure`

**Purpose**: Detect current memory pressure level

**What It Returns**:
- Pressure severity (None, Low, Moderate, High, Critical)
- Pressure level percentage (0-100)
- Description of current situation
- Actionable recommendations
- Whether immediate action is required

**Example Response**:
```json
{
  "severity": "Moderate",
  "pressurePercent": 65,
  "description": "Memory usage is elevated but manageable",
  "recommendations": [
    "Monitor memory trends",
    "Consider garbage collection during low-traffic period",
    "Review recent memory allocation patterns"
  ],
  "requiresImmediateAction": false
}
```

---

### 2.3 Get Memory Optimization Recommendations

**Endpoint**: `GET /api/monitoring/memory/recommendations`

**Purpose**: Get specific optimization suggestions

**What It Returns**:
- List of actionable recommendations based on current memory patterns

**Example Response**:
```json
[
  "Force Gen2 garbage collection to reclaim memory",
  "Implement object pooling for frequently allocated types",
  "Review large object allocations (>85KB) to avoid LOH fragmentation",
  "Consider increasing heap size if memory pressure persists"
]
```

---

### 2.4 Optimize Memory

**Endpoint**: `POST /api/monitoring/memory/optimize`

**Purpose**: Trigger memory optimization strategies

**⚠️ WARNING**: Can temporarily impact performance

**What It Does**:
- Forces full garbage collection (Gen2) with heap compaction
- Compacts the Large Object Heap (LOH)
- Trims the working set (Windows only)

**When to Use**:
- During low-traffic periods
- When memory pressure is high
- Before scheduled maintenance
- After bulk operations

**Example Request**:
```bash
POST /api/monitoring/memory/optimize
```

**Example Response**:
```json
{
  "message": "Memory optimization completed successfully"
}
```

---

### 2.5 Force Garbage Collection

**Endpoint**: `POST /api/monitoring/memory/gc`

**Purpose**: Manually trigger garbage collection

**⚠️ WARNING**: Can impact performance - use sparingly

**Parameters**:
- `generation` (optional): GC generation (0, 1, or 2) - default: 2
- `blocking` (optional): Wait for GC to complete - default: true
- `compacting` (optional): Compact the heap - default: true

**Generation Levels**:
- **0**: Collects short-lived objects (fastest)
- **1**: Collects Gen0 and Gen1 objects
- **2**: Full collection including all generations and LOH (slowest but most thorough)

**Example Request**:
```bash
POST /api/monitoring/memory/gc?generation=2&blocking=true&compacting=true
```

**Example Response**:
```json
{
  "message": "Garbage collection (Gen2) completed successfully",
  "generation": 2,
  "blocking": true,
  "compacting": true
}
```

---

## 3. Performance Monitoring Endpoints

### 3.1 Get Endpoint Statistics

**Endpoint**: `GET /api/monitoring/performance/endpoint`

**Purpose**: Performance statistics for a specific API endpoint

**Parameters**:
- `endpoint` (required): Endpoint path (e.g., "/api/users")
- `periodMinutes` (optional): Time period in minutes - default: 60

**What It Returns**:
- Total request count
- Average response time
- Min/max response times
- Success rate
- Error rate
- Requests per second
- 95th percentile response time

**Example Request**:
```bash
GET /api/monitoring/performance/endpoint?endpoint=/api/users&periodMinutes=60
```

**Example Response**:
```json
{
  "endpoint": "/api/users",
  "periodMinutes": 60,
  "totalRequests": 1250,
  "averageResponseTimeMs": 125,
  "minResponseTimeMs": 45,
  "maxResponseTimeMs": 850,
  "successRate": 99.2,
  "errorRate": 0.8,
  "requestsPerSecond": 20.8,
  "p95ResponseTimeMs": 250
}
```

---

### 3.2 Get Slow Requests

**Endpoint**: `GET /api/monitoring/performance/slow-requests`

**Purpose**: Find requests that exceeded execution time threshold

**Parameters**:
- `thresholdMs` (optional): Execution time threshold in ms - default: 1000
- `pageNumber` (optional): Page number (1-based) - default: 1
- `pageSize` (optional): Items per page (1-100) - default: 50

**What It Returns**:
- Paginated list of slow requests
- Endpoint path
- Execution time
- Timestamp
- Correlation ID
- Status code
- User information

**Example Request**:
```bash
GET /api/monitoring/performance/slow-requests?thresholdMs=1000&pageNumber=1&pageSize=50
```

**Example Response**:
```json
{
  "items": [
    {
      "endpoint": "/api/companies",
      "executionTimeMs": 11905,
      "timestamp": "2026-05-06T00:20:11Z",
      "correlationId": "ccc3845b-17f0-4706-84e1-34aabdb56a42",
      "statusCode": 500,
      "userId": 5,
      "userName": "moe"
    }
  ],
  "totalCount": 15,
  "page": 1,
  "pageSize": 50,
  "totalPages": 1
}
```

**Use Cases**:
- Performance troubleshooting
- Identifying bottlenecks
- Optimization opportunities
- SLA monitoring

---

### 3.3 Get Slow Queries

**Endpoint**: `GET /api/monitoring/performance/slow-queries`

**Purpose**: Find database queries that exceeded execution time threshold

**Parameters**:
- `thresholdMs` (optional): Execution time threshold in ms - default: 500
- `pageNumber` (optional): Page number (1-based) - default: 1
- `pageSize` (optional): Items per page (1-100) - default: 50

**What It Returns**:
- Paginated list of slow queries
- Query text or stored procedure name
- Execution time
- Timestamp
- Correlation ID
- Parameters used

**Example Request**:
```bash
GET /api/monitoring/performance/slow-queries?thresholdMs=500&pageNumber=1&pageSize=50
```

**Use Cases**:
- Database performance tuning
- Index optimization
- Query optimization
- Capacity planning

---

### 3.4 Get Connection Pool Metrics

**Endpoint**: `GET /api/monitoring/performance/connection-pool`

**Purpose**: Oracle connection pool health and utilization

**What It Returns**:
- Active connection count
- Idle connection count
- Pool size configuration (min/max)
- Connection timeout settings
- Pool utilization percentage
- Health status
- Recommendations

**Example Response**:
```json
{
  "activeConnections": 5,
  "idleConnections": 15,
  "minPoolSize": 10,
  "maxPoolSize": 100,
  "connectionTimeout": 30,
  "utilizationPercent": 20,
  "status": "Healthy",
  "recommendations": []
}
```

---

## 4. Security Monitoring Endpoints

### 4.1 Get Active Security Threats

**Endpoint**: `GET /api/monitoring/security/threats`

**Purpose**: List all unresolved security threats

**Parameters**:
- `pageNumber` (optional): Page number (1-based) - default: 1
- `pageSize` (optional): Items per page (1-100) - default: 50

**What It Returns**:
- Paginated list of active threats
- Threat type (FailedLogin, SQLInjection, XSS, AnomalousActivity)
- Severity (Low, Medium, High, Critical)
- Source IP address
- Detection timestamp
- Description
- Recommended actions

**Threat Types Detected**:
- **Failed Login Patterns**: Multiple failed login attempts from same IP
- **SQL Injection**: Malicious SQL patterns in input
- **XSS Attempts**: Cross-site scripting patterns
- **Unauthorized Access**: Permission violations
- **Anomalous Activity**: Unusual user behavior patterns

**Example Response**:
```json
{
  "items": [
    {
      "threatId": "THR-2026-001",
      "threatType": "FailedLogin",
      "severity": "High",
      "sourceIp": "192.168.1.100",
      "detectedAt": "2026-05-06T00:15:00Z",
      "description": "5 failed login attempts in 2 minutes",
      "affectedUser": "admin",
      "isResolved": false,
      "recommendedActions": [
        "Block IP address temporarily",
        "Review authentication logs",
        "Check for credential stuffing attack"
      ]
    }
  ],
  "totalCount": 3,
  "page": 1,
  "pageSize": 50
}
```

---

### 4.2 Get Daily Security Summary

**Endpoint**: `GET /api/monitoring/security/daily-summary`

**Purpose**: Comprehensive security summary for a specific date

**Parameters**:
- `date` (optional): Date to generate summary for - default: today

**What It Returns**:
- Total threat count by type and severity
- Top threat sources (IP addresses)
- Resolution statistics
- Trend analysis vs previous day

**Example Request**:
```bash
GET /api/monitoring/security/daily-summary?date=2026-05-06
```

**Example Response**:
```json
{
  "date": "2026-05-06",
  "totalThreats": 15,
  "threatsByType": {
    "FailedLogin": 8,
    "SQLInjection": 3,
    "XSS": 2,
    "AnomalousActivity": 2
  },
  "threatsBySeverity": {
    "Critical": 1,
    "High": 4,
    "Medium": 6,
    "Low": 4
  },
  "topSources": [
    {"ip": "192.168.1.100", "count": 5},
    {"ip": "10.0.0.50", "count": 3}
  ],
  "resolvedCount": 10,
  "unresolvedCount": 5,
  "trendVsPreviousDay": "+20%"
}
```

---

### 4.3 Check Failed Login Pattern

**Endpoint**: `GET /api/monitoring/security/check-failed-logins`

**Purpose**: Check if an IP has exceeded failed login threshold

**Parameters**:
- `ipAddress` (required): IP address to check

**What It Returns**:
- SecurityThreat if pattern detected
- 404 if no pattern found

**Example Request**:
```bash
GET /api/monitoring/security/check-failed-logins?ipAddress=192.168.1.100
```

---

### 4.4 Get Failed Login Count

**Endpoint**: `GET /api/monitoring/security/failed-login-count`

**Purpose**: Count failed login attempts for a specific user

**Parameters**:
- `username` (required): Username to check

**What It Returns**:
- Failed login count within time window (typically 5 minutes)
- Threshold value
- Status (Normal/Warning/Blocked)

**Example Response**:
```json
{
  "username": "admin",
  "failedLoginCount": 3,
  "threshold": 5,
  "status": "Warning"
}
```

---

### 4.5 Check SQL Injection

**Endpoint**: `POST /api/monitoring/security/check-sql-injection`

**Purpose**: Scan input for SQL injection patterns

**Request Body**: String to scan

**What It Detects**:
- UNION statements
- SELECT/INSERT/UPDATE/DELETE keywords
- Comment sequences (-- and /* */)
- String concatenation attempts

**Example Request**:
```bash
POST /api/monitoring/security/check-sql-injection
Content-Type: application/json

"SELECT * FROM users WHERE id = 1 OR 1=1--"
```

---

### 4.6 Check XSS

**Endpoint**: `POST /api/monitoring/security/check-xss`

**Purpose**: Scan input for XSS patterns

**Request Body**: String to scan

**What It Detects**:
- Script tags
- Event handlers (onclick, onerror, etc.)
- JavaScript protocol URLs
- Data URIs with scripts

**Example Request**:
```bash
POST /api/monitoring/security/check-xss
Content-Type: application/json

"<script>alert('XSS')</script>"
```

---

### 4.7 Check Anomalous Activity

**Endpoint**: `GET /api/monitoring/security/check-anomalous-activity`

**Purpose**: Detect unusual user behavior patterns

**Parameters**:
- `userId` (required): User ID to check

**What It Detects**:
- Unusually high API request volumes
- Requests at unusual times
- Rapid succession of different operations
- Geographic anomalies

**Example Request**:
```bash
GET /api/monitoring/security/check-anomalous-activity?userId=123
```

---

## 5. Audit System Monitoring Endpoints

### 5.1 Get Audit Queue Depth

**Endpoint**: `GET /api/monitoring/audit-queue-depth`

**Purpose**: Monitor audit logging queue status

**What It Returns**:
- Current queue depth
- Maximum queue size
- Utilization percentage
- Status (Healthy/Warning/Critical)

**Example Response**:
```json
{
  "queueDepth": 150,
  "maxQueueSize": 10000,
  "utilizationPercent": 1.5,
  "status": "Healthy"
}
```

**Status Levels**:
- **Healthy**: < 70% utilization
- **Warning**: 70-90% utilization
- **Critical**: ≥ 90% utilization

---

### 5.2 Get Audit Metrics

**Endpoint**: `GET /api/monitoring/audit/metrics`

**Purpose**: Comprehensive audit logging system metrics

**What It Returns**:
- Queue depth and capacity
- Circuit breaker state
- Success/failure rates
- Pending fallback files
- Processing status
- Resilience metrics

**Example Response**:
```json
{
  "queueDepth": 150,
  "queueCapacity": 10000,
  "queueUtilizationPercent": 1.5,
  "isHealthy": true,
  "pendingFallbackFiles": 0,
  "resilience": {
    "totalRequests": 10000,
    "successfulRequests": 9950,
    "failedRequests": 50,
    "circuitBreakerRejections": 0,
    "retriedRequests": 25,
    "circuitState": "Closed",
    "successRate": 99.5,
    "failureRate": 0.5,
    "rejectionRate": 0
  },
  "status": "Healthy",
  "timestamp": "2026-05-06T00:00:00Z"
}
```

**Circuit Breaker States**:
- **Closed**: Normal operation
- **Open**: Database failures detected, using fallback
- **HalfOpen**: Testing if database recovered

---

### 5.3 Get Audit Fallback Status

**Endpoint**: `GET /api/monitoring/audit/fallback-status`

**Purpose**: Check fallback file storage status

**What It Returns**:
- Whether fallback is currently active
- Number of pending fallback files
- Circuit breaker state
- Fallback directory path
- Status description

**Example Response**:
```json
{
  "fallbackActive": false,
  "pendingFileCount": 0,
  "circuitState": "Closed",
  "fallbackDirectory": "AuditFallback",
  "status": "Inactive",
  "timestamp": "2026-05-06T00:00:00Z"
}
```

---

### 5.4 Replay Fallback Events

**Endpoint**: `POST /api/monitoring/audit/replay-fallback`

**Purpose**: Manually replay fallback events to database

**When to Use**:
- After database connectivity is restored
- When circuit breaker is closed
- To clear pending fallback files

**What It Does**:
- Reads all pending fallback files
- Replays events to database
- Deletes successfully replayed files
- Returns count of replayed events

**Example Request**:
```bash
POST /api/monitoring/audit/replay-fallback
```

**Example Response**:
```json
{
  "message": "Fallback replay completed",
  "replayedCount": 150,
  "timestamp": "2026-05-06T00:00:00Z"
}
```

---

## 6. Alerting Endpoints

### 6.1 Test Alert

**Endpoint**: `POST /api/monitoring/test-alert`

**Purpose**: Test alert delivery through configured channels

**Parameters**:
- `alertType` (optional): Type of alert - default: "Test"
- `severity` (optional): Severity level - default: "Low"
- `channels` (optional): Comma-separated channels - default: "Email"

**Supported Channels**:
- Email
- Webhook
- SMS

**Example Request**:
```bash
POST /api/monitoring/test-alert?alertType=Test&severity=Low&channels=Email,Webhook
```

**Example Response**:
```json
{
  "message": "Test alert sent successfully",
  "alertType": "Test",
  "severity": "Low",
  "channels": "Email,Webhook",
  "timestamp": "2026-05-06T00:00:00Z"
}
```

---

## Automatic Alerting

The Monitoring API automatically triggers alerts for critical conditions:

### Alert Types

1. **AuditQueueOverflow** (High)
   - Triggered when queue ≥ 90% full
   - Indicates system under heavy load

2. **AuditQueueFull** (Critical)
   - Triggered when queue = 100% full
   - Backpressure being applied

3. **AuditHealthCheckFailed** (Critical)
   - Audit logging system not responding
   - May not be capturing events

4. **AuditFallbackActivated** (High)
   - Database unavailable
   - Using file system fallback

5. **AuditCircuitBreakerOpen** (Critical)
   - Repeated database failures
   - Circuit breaker protecting system

---

## Common Use Cases

### 1. **Performance Troubleshooting**

**Scenario**: API response times are slow

**Solution**:
```bash
# Step 1: Check slow requests
GET /api/monitoring/performance/slow-requests?thresholdMs=1000

# Step 2: Check slow queries
GET /api/monitoring/performance/slow-queries?thresholdMs=500

# Step 3: Check connection pool
GET /api/monitoring/performance/connection-pool

# Step 4: Get endpoint statistics
GET /api/monitoring/performance/endpoint?endpoint=/api/users
```

---

### 2. **Memory Issues**

**Scenario**: Application using too much memory

**Solution**:
```bash
# Step 1: Check memory metrics
GET /api/monitoring/memory

# Step 2: Check memory pressure
GET /api/monitoring/memory/pressure

# Step 3: Get recommendations
GET /api/monitoring/memory/recommendations

# Step 4: Optimize memory (during low traffic)
POST /api/monitoring/memory/optimize
```

---

### 3. **Security Incident**

**Scenario**: Suspicious activity detected

**Solution**:
```bash
# Step 1: Get active threats
GET /api/monitoring/security/threats

# Step 2: Check specific IP
GET /api/monitoring/security/check-failed-logins?ipAddress=192.168.1.100

# Step 3: Get daily summary
GET /api/monitoring/security/daily-summary

# Step 4: Check user activity
GET /api/monitoring/security/check-anomalous-activity?userId=123
```

---

### 4. **Audit System Issues**

**Scenario**: Audit logs not being written

**Solution**:
```bash
# Step 1: Check audit metrics
GET /api/monitoring/audit/metrics

# Step 2: Check queue depth
GET /api/monitoring/audit-queue-depth

# Step 3: Check fallback status
GET /api/monitoring/audit/fallback-status

# Step 4: Replay fallback if needed
POST /api/monitoring/audit/replay-fallback
```

---

## Best Practices

### 1. **Regular Monitoring**
- Check system health every 5 minutes
- Monitor slow requests daily
- Review security threats hourly
- Track memory trends weekly

### 2. **Alerting**
- Configure alerts for critical thresholds
- Test alert delivery regularly
- Document alert response procedures
- Review alert history monthly

### 3. **Performance**
- Baseline normal performance metrics
- Set realistic thresholds
- Investigate anomalies promptly
- Optimize based on data

### 4. **Security**
- Review security threats daily
- Block malicious IPs promptly
- Analyze attack patterns
- Update security rules based on threats

### 5. **Capacity Planning**
- Track resource trends over time
- Plan for peak loads
- Monitor growth patterns
- Scale proactively

---

## Integration with Monitoring Tools

The Monitoring API can be integrated with:

- **Prometheus**: Scrape `/health` endpoint
- **Grafana**: Visualize metrics dashboards
- **Datadog**: Custom metrics integration
- **New Relic**: APM integration
- **PagerDuty**: Alert routing
- **Slack**: Alert notifications

---

## Technical Implementation

### Architecture
- **Controller**: `MonitoringController.cs` - REST API endpoints
- **Services**:
  - `IPerformanceMonitor` - Performance metrics
  - `IMemoryMonitor` - Memory management
  - `ISecurityMonitor` - Security threat detection
  - `IAuditLogger` - Audit system metrics
  - `IAlertManager` - Alert delivery

### Performance Considerations
- Metrics collection is lightweight
- Most endpoints return cached data
- Database queries are optimized
- Pagination for large result sets

---

## Summary

The Monitoring API is a comprehensive observability solution providing:

- ✅ **System Health**: Real-time health metrics and status
- ✅ **Performance**: Slow request/query tracking and optimization
- ✅ **Memory**: Usage monitoring and optimization controls
- ✅ **Security**: Threat detection and analysis
- ✅ **Audit System**: Queue monitoring and fallback management
- ✅ **Alerting**: Automated alerts for critical conditions

It's essential for maintaining a healthy, secure, and performant ThinkOnERP system.

---

**Status**: Fully implemented and operational  
**Access**: Admin-only (except `/health`)  
**Base URL**: `/api/monitoring`  
**Purpose**: System observability and operational excellence
