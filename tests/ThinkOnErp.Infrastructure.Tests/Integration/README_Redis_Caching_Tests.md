# Redis Caching Integration Tests

This directory contains comprehensive integration tests for Redis caching functionality in the Full Traceability System. These tests validate the Redis integration for both AuditQueryService caching and SecurityMonitor failed login tracking.

## Test Coverage

### 1. RedisCachingIntegrationTests.cs
**Primary Focus**: Core Redis caching functionality and integration

**Test Categories**:
- **AuditQueryService Caching Tests**
  - Cache hit/miss scenarios
  - Different filter combinations creating separate cache entries
  - Search result caching
  - Cache expiration and refresh behavior

- **SecurityMonitor Redis Integration Tests**
  - Redis-based sliding window tracking for failed login attempts
  - Failed login pattern detection using Redis
  - User-specific failed login counting
  - Sliding window filtering (only counting recent attempts)

- **Redis Connection Handling and Error Scenarios**
  - Graceful fallback to database when Redis is unavailable
  - Error handling for Redis connection failures

- **Concurrent Access Scenarios**
  - Multiple concurrent requests handling cache correctly
  - Race condition handling in SecurityMonitor
  - Thread safety validation

- **Cache Invalidation Tests**
  - Manual cache clearance forcing refresh
  - Automatic cleanup of expired entries

**Validates Requirements**: 8.5, 8.6, 6.3

### 2. RedisCacheInvalidationIntegrationTests.cs
**Primary Focus**: Cache expiration, invalidation, and cleanup behavior

**Test Categories**:
- **Cache Expiration Tests**
  - Automatic expiration after TTL
  - Sliding window expiration for SecurityMonitor
  - Filtering of expired timestamps

- **Manual Cache Invalidation Tests**
  - Clearing specific cache patterns
  - Selective invalidation of cache entries

- **Cache Memory Management Tests**
  - Large dataset handling
  - Concurrent large operations
  - Memory efficiency validation

- **Redis Connection Resilience Tests**
  - Graceful degradation when Redis becomes unavailable
  - Timeout handling
  - Connection recovery scenarios

**Validates Requirements**: 8.5, 6.3, Performance Requirements

### 3. RedisCachePerformanceIntegrationTests.cs
**Primary Focus**: Performance validation and load testing

**Test Categories**:
- **Cache Performance Tests**
  - High-volume query performance
  - Concurrent request handling efficiency
  - SecurityMonitor performance under load
  - Race condition handling in concurrent scenarios

- **Memory Usage Tests**
  - Large dataset memory management
  - High-frequency operation memory stability
  - Memory leak detection

- **Cache Hit Ratio Tests**
  - Hit ratio optimization
  - Repeated query efficiency

**Performance Requirements Validated**:
- Cache hits under 50ms
- Concurrent operations under 5000ms for 100 requests
- Average tracking time under 10ms
- Average detection time under 20ms
- Cache hit ratio >= 66%

**Validates Requirements**: Performance Requirements, 8.5, 6.3

### 4. RedisCacheConfigurationIntegrationTests.cs
**Primary Focus**: Configuration validation and setup scenarios

**Test Categories**:
- **Configuration Tests**
  - Caching enabled/disabled behavior
  - Custom cache duration configuration
  - Redis enabled/disabled for SecurityMonitor
  - Warning logging for misconfiguration

- **Connection String Tests**
  - Valid connection string handling
  - Invalid connection string graceful handling

- **Configuration Validation Tests**
  - AuditQueryCachingOptions validation
  - SecurityMonitoringOptions validation

- **Fallback Behavior Tests**
  - Operation continuation after Redis connection loss
  - Database fallback scenarios

**Validates Requirements**: 8.5, 8.6, 6.3, Configuration Management

## Key Components Tested

### AuditQueryService Redis Integration
- **Cache Key Generation**: SHA256-based deterministic cache keys
- **Cache-Aside Pattern**: Try cache first, fall back to database
- **TTL Management**: Configurable cache duration (default 5 minutes)
- **Graceful Degradation**: Continues operation when Redis is unavailable
- **Query Result Caching**: Both QueryAsync and SearchAsync methods
- **Performance**: Cache hits significantly faster than database calls

### SecurityMonitor Redis Integration
- **Sliding Window Tracking**: Failed login attempts with time-based filtering
- **IP-based Tracking**: `failed_logins:{ipAddress}` keys
- **User-based Tracking**: `failed_logins_user:{username}` keys
- **TTL Management**: 2x window size for automatic cleanup
- **Concurrent Safety**: Thread-safe operations for multiple simultaneous attempts
- **Threshold Detection**: Configurable thresholds with severity levels

## Configuration Options Tested

### AuditQueryCachingOptions
```json
{
  "AuditQueryCaching": {
    "Enabled": true,
    "CacheDurationMinutes": 5,
    "RedisConnectionString": "localhost:6379",
    "ParallelQueriesEnabled": true,
    "ParallelQueryThresholdDays": 30,
    "ParallelQueryChunkSizeDays": 7,
    "MaxParallelQueries": 4
  }
}
```

### SecurityMonitoringOptions
```json
{
  "SecurityMonitoring": {
    "UseRedisCache": true,
    "RedisConnectionString": "localhost:6379",
    "FailedLoginThreshold": 5,
    "FailedLoginWindowMinutes": 5
  }
}
```

## Test Prerequisites

### Redis Server
- Redis server running on `localhost:6379`
- No authentication required for tests
- Database flushing permissions for cleanup

### Test Environment
- .NET 8.0 or later
- StackExchange.Redis package
- Microsoft.Extensions.Caching.StackExchangeRedis package
- xUnit test framework

## Running the Tests

### Individual Test Classes
```bash
dotnet test --filter "ClassName=RedisCachingIntegrationTests"
dotnet test --filter "ClassName=RedisCacheInvalidationIntegrationTests"
dotnet test --filter "ClassName=RedisCachePerformanceIntegrationTests"
dotnet test --filter "ClassName=RedisCacheConfigurationIntegrationTests"
```

### All Redis Integration Tests
```bash
dotnet test --filter "FullyQualifiedName~Integration" --filter "Name~Redis"
```

### Performance Tests Only
```bash
dotnet test --filter "ClassName=RedisCachePerformanceIntegrationTests"
```

## Test Data Cleanup

All test classes implement `IDisposable` and automatically:
- Flush Redis database after each test
- Dispose Redis connections
- Clean up service providers
- Remove temporary data

## Performance Benchmarks

The tests validate these performance requirements:

| Metric | Requirement | Test Validation |
|--------|-------------|-----------------|
| Cache Hit Latency | < 50ms | ✓ Validated |
| Concurrent Requests (100) | < 5000ms | ✓ Validated |
| Failed Login Tracking | < 10ms avg | ✓ Validated |
| Threat Detection | < 20ms avg | ✓ Validated |
| Cache Hit Ratio | ≥ 66% | ✓ Validated |
| Memory Efficiency | Stable under load | ✓ Validated |

## Error Scenarios Tested

1. **Redis Connection Failures**
   - Server unavailable
   - Connection timeout
   - Network interruption

2. **Configuration Issues**
   - Invalid connection strings
   - Missing configuration sections
   - Conflicting settings

3. **Memory Pressure**
   - Large dataset caching
   - High-frequency operations
   - Concurrent access patterns

4. **Cache Corruption**
   - Invalid cache data
   - Serialization failures
   - Key conflicts

## Integration with Full Traceability System

These tests validate the Redis caching components that support:

- **Audit Query Performance**: Fast retrieval of audit logs for compliance reporting
- **Security Monitoring**: Real-time failed login pattern detection
- **System Scalability**: Reduced database load through intelligent caching
- **High Availability**: Graceful degradation when cache is unavailable

The Redis caching integration is a critical component for meeting the system's performance requirements while maintaining data consistency and security.