# EF Core Configuration Quick Reference

## Configuration Files

| File | Purpose | When to Use |
|------|---------|-------------|
| `appsettings.json` | Base configuration | Default values for all environments |
| `appsettings.Development.json` | Development overrides | Local development with verbose logging |
| `appsettings.Production.json` | Production overrides | Production deployment with optimized settings |

## Feature Flags (Repository Migration)

**Location**: `UseEfCore` section

Enable repositories gradually:
```json
"UseEfCore": {
  "CompanyRepository": false,    // Set to true to use EF Core
  "BranchRepository": false,
  "UserRepository": false,
  // ... 20 more repositories
}
```

**Rollback**: Set flag to `false` and restart application

## Key Configuration Sections

### 1. Logging (`EfCore.Logging`)

| Setting | Development | Production | Purpose |
|---------|-------------|------------|---------|
| `LogLevel` | Debug | Warning | Minimum log level |
| `EnableSensitiveDataLogging` | true | **false** | ⚠️ Logs parameter values |
| `EnableDetailedErrors` | true | **false** | ⚠️ Detailed error info |
| `LogSqlQueries` | true | false | Log all SQL queries |
| `SlowQueryThresholdMs` | 250 | 1000 | Slow query threshold |

### 2. Connection Pool (`EfCore.ConnectionPool`)

| Setting | Development | Production | Purpose |
|---------|-------------|------------|---------|
| `MinPoolSize` | 2 | 10 | Minimum connections |
| `MaxPoolSize` | 20 | 200 | Maximum connections |
| `ConnectionTimeout` | 30 | 15 | Timeout (seconds) |
| `StatementCacheSize` | 20 | 100 | Cached statements |

### 3. Query Behavior (`EfCore.QueryBehavior`)

| Setting | Development | Production | Purpose |
|---------|-------------|------------|---------|
| `DefaultTrackingBehavior` | NoTracking | NoTracking | Change tracking |
| `UseCompiledQueries` | false | true | Query compilation |
| `MaxRetryCount` | 2 | 3 | Retry attempts |
| `CommandTimeout` | 60 | 30 | Query timeout (seconds) |

### 4. Performance (`EfCore.Performance`)

| Setting | Development | Production | Purpose |
|---------|-------------|------------|---------|
| `BatchSize` | 50 | 200 | Batch operation size |
| `PrecompileCommonQueries` | false | true | Precompile queries |
| `EnableQueryCaching` | false | true | Cache query results |
| `EnableLazyLoading` | false | false | ⚠️ Lazy loading (N+1) |

### 5. Interceptors (`EfCore.Interceptors`)

| Setting | Development | Production | Purpose |
|---------|-------------|------------|---------|
| `EnableAuditInterceptor` | true | true | Audit logging |
| `EnablePerformanceInterceptor` | true | true | Performance monitoring |
| `EnableConnectionInterceptor` | true | false | Connection logging |
| `EnableTransactionInterceptor` | true | false | Transaction logging |
| `EnableCommandInterceptor` | true | false | Command logging |

### 6. Health Checks (`HealthChecks.EfCoreDbContext`)

| Setting | Development | Production | Purpose |
|---------|-------------|------------|---------|
| `Enabled` | true | true | Enable health checks |
| `TimeoutSeconds` | 30 | 10 | Health check timeout |
| `CheckConnectionPool` | true | true | Check pool health |
| `FailureThreshold` | 5 | 3 | Failures before unhealthy |

## Common Tasks

### Enable a Repository

1. Update `appsettings.json`:
   ```json
   "UseEfCore": {
     "CompanyRepository": true
   }
   ```

2. Restart application

3. Monitor health checks:
   ```
   GET /health?tags=db
   ```

4. Check logs for errors

### Rollback a Repository

1. Update `appsettings.json`:
   ```json
   "UseEfCore": {
     "CompanyRepository": false
   }
   ```

2. Restart application

3. Verify rollback successful

### Optimize Slow Queries

1. Check slow query logs:
   ```
   [WARNING] Query execution time: 2500ms (threshold: 1000ms)
   ```

2. Enable query splitting:
   ```json
   "QuerySplittingBehavior": "SplitQuery"
   ```

3. Use projection:
   ```csharp
   .Select(c => new { c.RowId, c.RowDesc })
   ```

4. Use AsNoTracking:
   ```csharp
   .AsNoTracking()
   ```

### Fix Connection Pool Exhaustion

1. Check pool utilization:
   ```
   GET /health?tags=db
   ```

2. Increase MaxPoolSize (temporary):
   ```json
   "MaxPoolSize": 300
   ```

3. Find and fix connection leaks:
   ```csharp
   using (var context = new ThinkOnErpDbContext(options))
   {
       // Use context
   }
   ```

4. Optimize slow queries

## Security Checklist

### ⚠️ NEVER Enable in Production

- [ ] `EnableSensitiveDataLogging: true`
- [ ] `EnableDetailedErrors: true`
- [ ] `LogParameterValues: true`

### ✅ Always Enable in Production

- [ ] `EnableSensitiveDataLogging: false`
- [ ] `EnableDetailedErrors: false`
- [ ] `LogParameterValues: false`
- [ ] Connection string in environment variables
- [ ] Health checks enabled
- [ ] Monitoring and alerts configured

## Performance Checklist

### Development

- [ ] Small connection pool (2-20)
- [ ] Verbose logging enabled
- [ ] All interceptors enabled
- [ ] Compiled queries disabled
- [ ] Query caching disabled

### Production

- [ ] Large connection pool (10-200)
- [ ] Minimal logging (warnings only)
- [ ] Only critical interceptors enabled
- [ ] Compiled queries enabled
- [ ] Query caching enabled
- [ ] Slow query threshold: 1000ms
- [ ] Health checks configured
- [ ] Monitoring enabled

## Migration Strategy

### Phase 1: Pilot (Week 1-2)
- [ ] Enable CurrencyRepository
- [ ] Enable TicketStatusRepository
- [ ] Enable TicketPriorityRepository
- [ ] Monitor for 48 hours

### Phase 2: Core (Week 3-4)
- [ ] Enable CompanyRepository
- [ ] Enable BranchRepository
- [ ] Enable UserRepository
- [ ] Enable RoleRepository
- [ ] Enable FiscalYearRepository
- [ ] Monitor for 48 hours

### Phase 3: Permission (Week 5-6)
- [ ] Enable permission repositories (6 total)
- [ ] Monitor for 48 hours

### Phase 4: Ticket (Week 7-8)
- [ ] Enable ticket repositories (6 total)
- [ ] Monitor for 48 hours

### Phase 5: Admin (Week 9-10)
- [ ] Enable admin repositories (4 total)
- [ ] Monitor for 2 weeks
- [ ] Remove ADO.NET implementations

## Monitoring

### Key Metrics

| Metric | Warning | Critical | Action |
|--------|---------|----------|--------|
| Pool Utilization | 80% | 95% | Increase MaxPoolSize |
| Query Time | 1000ms | 5000ms | Optimize query |
| Error Rate | 1% | 5% | Investigate errors |
| Memory Usage | 1GB | 2GB | Use AsNoTracking |

### Health Check Endpoints

- All checks: `GET /health`
- Database only: `GET /health?tags=db`
- EF Core only: `GET /health?tags=efcore`

### Log Locations

- Console: Real-time logs
- File: `Logs/log-{Date}.txt` (Development)
- File: `/var/log/thinkonerp/log-{Date}.txt` (Production)

## Troubleshooting

### Connection Timeout
1. Increase MaxPoolSize
2. Check for connection leaks
3. Optimize slow queries

### Slow Queries
1. Enable query splitting
2. Use projection
3. Use AsNoTracking
4. Add indexes

### High Memory
1. Use AsNoTracking
2. Use pagination
3. Use projection
4. Dispose DbContext

### Feature Flag Not Working
1. Restart application
2. Verify repository name
3. Check DI registration

## Support

- **Full Documentation**: `docs/EF_CORE_CONFIGURATION_GUIDE.md`
- **Migration Guide**: `docs/EF_CORE_MIGRATION_GUIDE.md`
- **Requirements**: `.kiro/specs/ef-core-migration/requirements.md`
- **Design**: `.kiro/specs/ef-core-migration/design.md`
- **Tasks**: `.kiro/specs/ef-core-migration/tasks.md`

---

**Quick Reference Version 1.0**
