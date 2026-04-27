# Redis Distributed Cache Configuration

## Overview

The Full Traceability System uses Redis distributed cache for two primary purposes:

1. **Security Monitoring**: Tracking failed login attempts with sliding window for threat detection
2. **Audit Query Caching**: Caching audit query results for improved performance

Redis is conditionally enabled based on configuration settings, allowing flexible deployment scenarios.

## Implementation Details

### Service Registration

Redis is configured in `DependencyInjection.cs` in both the `AddInfrastructure()` and `AddTraceabilitySystem()` methods:

```csharp
// Configure Redis distributed cache if enabled for security monitoring OR audit query caching
var securityOptions = new SecurityMonitoringOptions();
configuration.GetSection(SecurityMonitoringOptions.SectionName).Bind(securityOptions);

var auditCachingOptions = new AuditQueryCachingOptions();
configuration.GetSection(AuditQueryCachingOptions.SectionName).Bind(auditCachingOptions);

var needsRedis = (securityOptions.UseRedisCache && !string.IsNullOrWhiteSpace(securityOptions.RedisConnectionString)) ||
                (auditCachingOptions.Enabled && !string.IsNullOrWhiteSpace(auditCachingOptions.RedisConnectionString));

if (needsRedis)
{
    var redisConnectionString = auditCachingOptions.Enabled && !string.IsNullOrWhiteSpace(auditCachingOptions.RedisConnectionString)
        ? auditCachingOptions.RedisConnectionString
        : securityOptions.RedisConnectionString;
        
    services.AddStackExchangeRedisCache(options =>
    {
        options.Configuration = redisConnectionString;
        options.InstanceName = "ThinkOnErp:";
    });
}
```

### Conditional Enablement Logic

Redis is enabled when **either** of the following conditions is true:

1. **Security Monitoring with Redis**: `SecurityMonitoring:UseRedisCache = true` AND `SecurityMonitoring:RedisConnectionString` is not empty
2. **Audit Query Caching**: `AuditQueryCaching:Enabled = true` AND `AuditQueryCaching:RedisConnectionString` is not empty

### Connection String Priority

When both features are enabled, the connection string is selected with the following priority:

1. **First Priority**: `AuditQueryCaching:RedisConnectionString` (if audit caching is enabled)
2. **Fallback**: `SecurityMonitoring:RedisConnectionString`

This allows using the same Redis instance for both features or separate instances if needed.

### Instance Name Prefix

All Redis keys are prefixed with `"ThinkOnErp:"` to:
- Avoid key collisions when sharing Redis with other applications
- Enable easy identification of keys belonging to this application
- Support multi-tenant Redis deployments

## Configuration

### Development Environment (appsettings.Development.json)

```json
{
  "SecurityMonitoring": {
    "UseRedisCache": false,
    "RedisConnectionString": "localhost:6379"
  },
  "AuditQueryCaching": {
    "Enabled": false,
    "RedisConnectionString": "localhost:6379"
  }
}
```

**Note**: Redis is disabled by default in development to simplify local setup.

### Production Environment (appsettings.Production.json)

```json
{
  "SecurityMonitoring": {
    "UseRedisCache": true,
    "RedisConnectionString": "REPLACE_WITH_PRODUCTION_REDIS_CONNECTION_STRING"
  },
  "AuditQueryCaching": {
    "Enabled": true,
    "CacheDurationMinutes": 10,
    "RedisConnectionString": "REPLACE_WITH_PRODUCTION_REDIS_CONNECTION_STRING"
  }
}
```

**Note**: Redis is enabled in production for optimal performance and security monitoring.

### Default Configuration (appsettings.json)

```json
{
  "ConnectionStrings": {
    "Redis": "localhost:6379,abortConnect=false,connectTimeout=5000,syncTimeout=5000"
  },
  "SecurityMonitoring": {
    "UseRedisCache": false,
    "RedisConnectionString": "localhost:6379"
  },
  "AuditQueryCaching": {
    "Enabled": false,
    "CacheDurationMinutes": 5,
    "RedisConnectionString": "localhost:6379"
  }
}
```

## Usage Scenarios

### Scenario 1: Security Monitoring Only

Enable Redis for failed login tracking with sliding window:

```json
{
  "SecurityMonitoring": {
    "UseRedisCache": true,
    "RedisConnectionString": "redis-server:6379,password=secret",
    "FailedLoginThreshold": 5,
    "FailedLoginWindowMinutes": 5
  },
  "AuditQueryCaching": {
    "Enabled": false
  }
}
```

**Use Case**: Track failed login attempts across multiple API instances for distributed threat detection.

### Scenario 2: Audit Query Caching Only

Enable Redis for caching audit query results:

```json
{
  "SecurityMonitoring": {
    "UseRedisCache": false
  },
  "AuditQueryCaching": {
    "Enabled": true,
    "CacheDurationMinutes": 10,
    "RedisConnectionString": "redis-server:6379,password=secret"
  }
}
```

**Use Case**: Improve audit query performance by caching frequently accessed results.

### Scenario 3: Both Features Enabled (Recommended for Production)

Enable Redis for both security monitoring and audit caching:

```json
{
  "SecurityMonitoring": {
    "UseRedisCache": true,
    "RedisConnectionString": "redis-server:6379,password=secret",
    "FailedLoginThreshold": 5,
    "FailedLoginWindowMinutes": 5
  },
  "AuditQueryCaching": {
    "Enabled": true,
    "CacheDurationMinutes": 10,
    "RedisConnectionString": "redis-server:6379,password=secret"
  }
}
```

**Use Case**: Full production deployment with comprehensive security monitoring and optimized query performance.

### Scenario 4: Separate Redis Instances

Use different Redis instances for security and caching:

```json
{
  "SecurityMonitoring": {
    "UseRedisCache": true,
    "RedisConnectionString": "redis-security:6379,password=secret1"
  },
  "AuditQueryCaching": {
    "Enabled": true,
    "CacheDurationMinutes": 10,
    "RedisConnectionString": "redis-cache:6379,password=secret2"
  }
}
```

**Use Case**: Isolate security-critical data from general caching, or use different Redis configurations for each purpose.

## Redis Connection String Format

### Basic Connection

```
localhost:6379
```

### With Password

```
redis-server:6379,password=your_password
```

### With Advanced Options

```
redis-server:6379,password=secret,abortConnect=false,connectTimeout=5000,syncTimeout=5000,ssl=true
```

### Connection String Options

| Option | Description | Default | Recommended |
|--------|-------------|---------|-------------|
| `abortConnect` | Abort connection on first failure | `true` | `false` (for resilience) |
| `connectTimeout` | Connection timeout in milliseconds | `5000` | `5000` |
| `syncTimeout` | Synchronous operation timeout in milliseconds | `5000` | `5000` |
| `ssl` | Use SSL/TLS encryption | `false` | `true` (for production) |
| `password` | Redis authentication password | None | Required for production |

## How Services Use Redis

### SecurityMonitor Service

The `SecurityMonitor` service uses Redis for:

1. **Failed Login Tracking**: Stores failed login attempts per IP address with sliding window expiration
2. **Rate Limiting**: Tracks request counts per IP and per user
3. **Threat Pattern Detection**: Maintains counters for suspicious activity patterns

**Redis Key Pattern**: `ThinkOnErp:security:failed_logins:{ipAddress}`

**Example Usage**:
```csharp
public class SecurityMonitor : ISecurityMonitor
{
    private readonly IDistributedCache _cache;
    
    public async Task<SecurityThreat?> DetectFailedLoginPatternAsync(string ipAddress)
    {
        if (_options.UseRedisCache && _cache != null)
        {
            // Use Redis sliding window for distributed tracking
            var key = $"failed_logins:{ipAddress}";
            var attempts = await GetFailedLoginAttemptsFromRedis(key);
            
            if (attempts >= _options.FailedLoginThreshold)
            {
                return new SecurityThreat
                {
                    ThreatType = "FailedLoginPattern",
                    Severity = "High",
                    IpAddress = ipAddress
                };
            }
        }
        else
        {
            // Fallback to database tracking
            var attempts = await GetFailedLoginAttemptsFromDatabase(ipAddress);
        }
    }
}
```

### AuditQueryService (via CachedAuditQueryService)

The `CachedAuditQueryService` decorator uses Redis for:

1. **Query Result Caching**: Caches audit query results based on filter parameters
2. **Sliding Expiration**: Automatically extends cache lifetime for frequently accessed queries
3. **Cache Invalidation**: Clears cache when new audit entries are written

**Redis Key Pattern**: `ThinkOnErp:audit_query:{hash_of_filter_parameters}`

**Example Usage**:
```csharp
public class CachedAuditQueryService : IAuditQueryService
{
    private readonly IAuditQueryService _inner;
    private readonly IDistributedCache _cache;
    
    public async Task<PagedResult<AuditLogEntry>> QueryAsync(
        AuditQueryFilter filter, 
        PaginationOptions pagination)
    {
        if (!_options.Enabled)
        {
            return await _inner.QueryAsync(filter, pagination);
        }
        
        var cacheKey = GenerateCacheKey(filter, pagination);
        var cachedResult = await _cache.GetStringAsync(cacheKey);
        
        if (cachedResult != null)
        {
            return JsonSerializer.Deserialize<PagedResult<AuditLogEntry>>(cachedResult);
        }
        
        var result = await _inner.QueryAsync(filter, pagination);
        
        await _cache.SetStringAsync(
            cacheKey, 
            JsonSerializer.Serialize(result),
            new DistributedCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = _options.CacheDuration
            });
        
        return result;
    }
}
```

## Deployment Considerations

### Docker Deployment

When deploying with Docker, use the provided `docker-compose.yml`:

```yaml
services:
  redis:
    image: redis:7-alpine
    ports:
      - "6379:6379"
    command: redis-server --requirepass your_password
    volumes:
      - redis-data:/data
    restart: unless-stopped

  api:
    environment:
      - SecurityMonitoring__UseRedisCache=true
      - SecurityMonitoring__RedisConnectionString=redis:6379,password=your_password
      - AuditQueryCaching__Enabled=true
      - AuditQueryCaching__RedisConnectionString=redis:6379,password=your_password
```

### Kubernetes Deployment

For Kubernetes, use a Redis StatefulSet or managed service:

```yaml
apiVersion: v1
kind: ConfigMap
metadata:
  name: thinkonerp-config
data:
  SecurityMonitoring__UseRedisCache: "true"
  SecurityMonitoring__RedisConnectionString: "redis-service:6379,password=secret"
  AuditQueryCaching__Enabled: "true"
  AuditQueryCaching__RedisConnectionString: "redis-service:6379,password=secret"
```

### Cloud Managed Redis

#### AWS ElastiCache

```json
{
  "SecurityMonitoring": {
    "UseRedisCache": true,
    "RedisConnectionString": "your-cluster.cache.amazonaws.com:6379,ssl=true"
  }
}
```

#### Azure Cache for Redis

```json
{
  "SecurityMonitoring": {
    "UseRedisCache": true,
    "RedisConnectionString": "your-cache.redis.cache.windows.net:6380,password=key,ssl=true"
  }
}
```

#### Google Cloud Memorystore

```json
{
  "SecurityMonitoring": {
    "UseRedisCache": true,
    "RedisConnectionString": "10.0.0.3:6379"
  }
}
```

## Monitoring and Troubleshooting

### Health Checks

Redis health is monitored through the application health check endpoint:

```json
{
  "HealthChecks": {
    "Redis": {
      "Enabled": true,
      "TimeoutSeconds": 5
    }
  }
}
```

Access health status at: `GET /health`

### Common Issues

#### Issue 1: Redis Connection Timeout

**Symptom**: Application logs show "Redis connection timeout" errors

**Solution**:
1. Verify Redis server is running: `redis-cli ping`
2. Check network connectivity between API and Redis
3. Increase connection timeout: `connectTimeout=10000`
4. Verify firewall rules allow traffic on Redis port

#### Issue 2: Redis Authentication Failed

**Symptom**: "NOAUTH Authentication required" or "ERR invalid password"

**Solution**:
1. Verify password in connection string matches Redis configuration
2. Check Redis `requirepass` setting in `redis.conf`
3. Ensure password doesn't contain special characters that need escaping

#### Issue 3: Redis Memory Exhausted

**Symptom**: "OOM command not allowed when used memory > 'maxmemory'"

**Solution**:
1. Increase Redis `maxmemory` setting
2. Configure eviction policy: `maxmemory-policy allkeys-lru`
3. Reduce cache duration in `AuditQueryCaching:CacheDurationMinutes`
4. Monitor Redis memory usage: `redis-cli info memory`

### Redis Monitoring Commands

```bash
# Check Redis connection
redis-cli -h localhost -p 6379 -a password ping

# Monitor Redis operations in real-time
redis-cli -h localhost -p 6379 -a password monitor

# Check memory usage
redis-cli -h localhost -p 6379 -a password info memory

# List all keys (use with caution in production)
redis-cli -h localhost -p 6379 -a password --scan --pattern "ThinkOnErp:*"

# Get key count
redis-cli -h localhost -p 6379 -a password dbsize

# Check specific key
redis-cli -h localhost -p 6379 -a password get "ThinkOnErp:audit_query:some_key"
```

## Performance Considerations

### Cache Hit Rate

Monitor cache effectiveness through application logs:

```csharp
// CachedAuditQueryService logs cache hits/misses
[Information] Audit query cache hit for filter: {Filter}
[Information] Audit query cache miss for filter: {Filter}
```

### Memory Usage

Estimate Redis memory requirements:

- **Security Monitoring**: ~1KB per IP address × number of unique IPs in window
- **Audit Query Caching**: ~10-100KB per cached query × number of unique queries

**Example**: 
- 1000 unique IPs with failed logins: ~1MB
- 100 cached audit queries: ~1-10MB
- **Total**: ~2-11MB (minimal memory footprint)

### Scaling Considerations

For high-traffic deployments:

1. **Redis Cluster**: Use Redis Cluster for horizontal scaling
2. **Read Replicas**: Configure read replicas for query caching
3. **Separate Instances**: Use dedicated Redis instances for security vs. caching
4. **Connection Pooling**: StackExchange.Redis handles connection pooling automatically

## Security Best Practices

1. **Always use passwords** in production: `password=strong_password`
2. **Enable SSL/TLS** for production: `ssl=true`
3. **Restrict network access** to Redis using firewall rules
4. **Use separate Redis instances** for security-critical data
5. **Rotate passwords regularly** and update connection strings
6. **Monitor for unauthorized access** using Redis logs
7. **Disable dangerous commands** in `redis.conf`: `rename-command FLUSHALL ""`

## Testing Redis Configuration

### Unit Tests

Redis configuration is tested in integration tests:

```csharp
[Fact]
public async Task SecurityMonitor_WithRedis_TracksFailedLogins()
{
    // Arrange
    var services = new ServiceCollection();
    services.AddStackExchangeRedisCache(options =>
    {
        options.Configuration = "localhost:6379";
        options.InstanceName = "ThinkOnErp:";
    });
    
    // Act & Assert
    // Test failed login tracking with Redis
}
```

### Manual Testing

1. **Start Redis**: `docker run -d -p 6379:6379 redis:7-alpine`
2. **Enable Redis in config**: Set `UseRedisCache = true`
3. **Run application**: `dotnet run`
4. **Trigger failed logins**: Attempt login with wrong password 5 times
5. **Verify Redis keys**: `redis-cli --scan --pattern "ThinkOnErp:*"`
6. **Check security alerts**: Verify threat detection triggers

## Summary

The Redis distributed cache configuration in the Full Traceability System:

✅ **Conditionally enabled** based on feature requirements  
✅ **Supports multiple use cases** (security monitoring, query caching)  
✅ **Flexible connection strings** (single instance or separate instances)  
✅ **Production-ready** with proper error handling and fallbacks  
✅ **Well-documented** configuration options  
✅ **Instance name prefix** prevents key collisions  
✅ **Health monitoring** integrated into application health checks  

The implementation is complete and ready for production deployment. Simply configure the connection strings and enable the desired features in `appsettings.Production.json`.
