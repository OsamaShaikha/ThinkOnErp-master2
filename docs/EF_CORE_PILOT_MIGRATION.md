# EF Core Pilot Migration - Feature Flags Documentation

## Overview

This document describes the feature flag configuration for the EF Core pilot migration. Three repositories have been migrated to EF Core as part of Phase 2 (Pilot Repository Migration):

1. **CurrencyRepository**
2. **TicketStatusRepository**
3. **TicketPriorityRepository**

The migration uses feature flags to allow gradual rollout and easy rollback if issues are encountered.

## Feature Flag Configuration

### Location

Feature flags are configured in `appsettings.json` and `appsettings.Development.json` under the `UseEfCore` section:

```json
"UseEfCore": {
  "_comment": "Feature flags for EF Core migration. Set to true to use EF Core implementation, false for legacy ADO.NET.",
  "CurrencyRepository": false,
  "TicketStatusRepository": false,
  "TicketPriorityRepository": false
}
```

### Default Behavior

By default, all feature flags are set to `false`, meaning the application will use the legacy ADO.NET implementations. This ensures backward compatibility and zero risk until you're ready to enable EF Core.

## Enabling EF Core Implementations

### Development Environment

To test EF Core implementations in development:

1. Open `appsettings.Development.json`
2. Set the desired repository flag to `true`:

```json
"UseEfCore": {
  "CurrencyRepository": true,
  "TicketStatusRepository": true,
  "TicketPriorityRepository": true
}
```

3. Restart the application
4. Test all CRUD operations for the enabled repositories
5. Monitor logs for any errors or performance issues

### Production Environment

For production deployment, follow a gradual rollout strategy:

#### Step 1: Enable One Repository at a Time

Start with the simplest repository (CurrencyRepository):

```json
"UseEfCore": {
  "CurrencyRepository": true,
  "TicketStatusRepository": false,
  "TicketPriorityRepository": false
}
```

#### Step 2: Monitor for 24-48 Hours

- Check application logs for errors
- Monitor performance metrics
- Verify API responses are identical to ADO.NET implementation
- Check database connection pool usage

#### Step 3: Enable Additional Repositories

If no issues are found, enable the next repository:

```json
"UseEfCore": {
  "CurrencyRepository": true,
  "TicketStatusRepository": true,
  "TicketPriorityRepository": false
}
```

Repeat monitoring for 24-48 hours.

#### Step 4: Full Pilot Migration

Once all three repositories are stable:

```json
"UseEfCore": {
  "CurrencyRepository": true,
  "TicketStatusRepository": true,
  "TicketPriorityRepository": true
}
```

## Rollback Procedure

If issues are encountered with any EF Core implementation:

### Immediate Rollback

1. Open `appsettings.json` (or use environment variables/Azure App Configuration)
2. Set the problematic repository flag to `false`:

```json
"UseEfCore": {
  "CurrencyRepository": false,  // Rollback to ADO.NET
  "TicketStatusRepository": true,
  "TicketPriorityRepository": true
}
```

3. Restart the application (or wait for configuration reload if using Azure App Configuration)
4. Verify the application is using the ADO.NET implementation
5. Investigate the issue in a non-production environment

### No Code Deployment Required

The beauty of feature flags is that rollback requires **no code deployment**. Simply change the configuration and restart the application.

## Implementation Details

### Dependency Injection Configuration

The feature flags are implemented in `DependencyInjection.cs`:

```csharp
// CurrencyRepository - Feature flag: UseEfCore:CurrencyRepository
if (configuration.GetValue<bool>("UseEfCore:CurrencyRepository", false))
{
    services.AddScoped<ICurrencyRepository, ThinkOnErp.Infrastructure.Repositories.EfCore.CurrencyRepository>();
}
else
{
    services.AddScoped<ICurrencyRepository, CurrencyRepository>();
}
```

### Repository Locations

- **Legacy ADO.NET Implementations**: `src/ThinkOnErp.Infrastructure/Repositories/`
  - `CurrencyRepository.cs`
  - `TicketStatusRepository.cs`
  - `TicketPriorityRepository.cs`

- **EF Core Implementations**: `src/ThinkOnErp.Infrastructure/Repositories/EfCore/`
  - `CurrencyRepository.cs`
  - `TicketStatusRepository.cs`
  - `TicketPriorityRepository.cs`

### Interface Compatibility

Both implementations implement the same interfaces from the Domain layer:
- `ICurrencyRepository`
- `ITicketStatusRepository`
- `ITicketPriorityRepository`

This ensures that the Application layer and API layer are completely unaware of which implementation is being used.

## Testing Strategy

### Unit Tests

Unit tests exist for both implementations:
- Legacy tests: `tests/ThinkOnErp.Infrastructure.Tests/Repositories/`
- EF Core tests: `tests/ThinkOnErp.Infrastructure.Tests/Repositories/EfCore/`

### Integration Tests

Integration tests verify that both implementations produce identical results:
- `tests/ThinkOnErp.Infrastructure.Tests/IntegrationTests/`

### API Tests

End-to-end API tests ensure that API contracts remain unchanged:
- `tests/ThinkOnErp.API.Tests/`

## Monitoring and Observability

### Key Metrics to Monitor

1. **Response Times**: Compare EF Core vs ADO.NET query execution times
2. **Error Rates**: Monitor for any increase in exceptions
3. **Database Connection Pool**: Check for connection pool exhaustion
4. **Memory Usage**: EF Core change tracking may increase memory usage
5. **Query Performance**: Use SQL profiling to compare generated queries

### Logging

EF Core query logging is enabled in development:

```json
"Serilog": {
  "MinimumLevel": {
    "Override": {
      "Microsoft.EntityFrameworkCore": "Information"
    }
  }
}
```

This logs all SQL queries generated by EF Core for debugging purposes.

## Known Differences

### EF Core vs ADO.NET

1. **ID Generation**: EF Core automatically retrieves generated IDs from Oracle sequences
2. **Change Tracking**: EF Core tracks entity changes automatically
3. **Query Translation**: EF Core translates LINQ queries to SQL
4. **Connection Management**: EF Core manages connections through DbContext
5. **Transaction Handling**: EF Core provides built-in transaction support

### Performance Considerations

- **Read Operations**: EF Core uses `AsNoTracking()` for read-only queries to match ADO.NET performance
- **Write Operations**: EF Core may be slightly slower due to change tracking overhead
- **Bulk Operations**: For bulk inserts/updates, consider using stored procedures or EF Core bulk extensions

## Troubleshooting

### Issue: EF Core queries are slower than ADO.NET

**Solution**: 
- Check if `AsNoTracking()` is used for read-only queries
- Review generated SQL queries in logs
- Consider using compiled queries for frequently executed queries
- Verify database indexes are in place

### Issue: Connection pool exhaustion

**Solution**:
- Ensure DbContext is properly disposed (using `using` statements or DI scoped lifetime)
- Check for long-running queries that hold connections
- Increase connection pool size in connection string if needed

### Issue: Unexpected null values

**Solution**:
- Verify entity configurations map nullable properties correctly
- Check for differences in how ADO.NET and EF Core handle `DBNull.Value`
- Review entity property types (nullable vs non-nullable)

### Issue: Foreign key constraint violations

**Solution**:
- Verify entity relationships are configured correctly in entity configurations
- Check that foreign key values are set before saving
- Review cascade delete behavior

## Next Steps

After successful pilot migration:

1. **Phase 3**: Migrate core entity repositories (Company, Branch, User, Role, FiscalYear)
2. **Phase 4**: Migrate permission repositories
3. **Phase 5**: Migrate ticket management repositories
4. **Phase 6**: Migrate admin and audit repositories
5. **Phase 7**: Performance optimization and full deployment

## Support

For issues or questions:
- Review logs in `Logs/` directory
- Check EF Core documentation: https://docs.microsoft.com/en-us/ef/core/
- Consult the design document: `.kiro/specs/ef-core-migration/design.md`
- Review requirements: `.kiro/specs/ef-core-migration/requirements.md`

## Conclusion

The feature flag approach provides a safe, gradual migration path from ADO.NET to EF Core. By enabling one repository at a time and monitoring closely, you can ensure a smooth transition with minimal risk.

Remember: **Rollback is always just a configuration change away!**
