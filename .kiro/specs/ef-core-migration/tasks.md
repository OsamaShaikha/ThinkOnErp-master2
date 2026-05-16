# Implementation Plan: EF Core Migration

## Overview

This implementation plan covers the migration of ThinkOnErp from ADO.NET to Entity Framework Core (EF Core). The migration will be executed in 7 phases over 16 weeks, covering all 23 repositories. The approach uses pure EF Core with LINQ queries, eliminating the need for stored procedures while maintaining backward compatibility with existing API contracts.

## Tasks

## Phase 1: Foundation Setup (Week 1-2)

- [x] 1.1 Install EF Core NuGet Packages
  - Add Oracle.EntityFrameworkCore package (version 8.x)
  - Add Microsoft.EntityFrameworkCore.Design package
  - Add Microsoft.EntityFrameworkCore.Tools package
  - Verify package compatibility with .NET 8.0
  - _Requirements: REQ-1, REQ-7_

- [x] 1.2 Create ThinkOnErpDbContext
  - Create `ThinkOnErpDbContext.cs` in `src/ThinkOnErp.Infrastructure/Data/`
  - Add DbSet properties for all 23 entities (Companies, Branches, Users, Roles, etc.)
  - Implement constructor accepting `DbContextOptions<ThinkOnErpDbContext>`
  - Override `OnModelCreating` method to apply entity configurations
  - Configure change tracking behavior (QueryTrackingBehavior.NoTracking for read-only by default)
  - _Requirements: REQ-1, REQ-7, REQ-15_

- [x] 1.3 Create Entity Configuration Classes (Part 1: Core Entities)
  - Create `Configurations` folder in `src/ThinkOnErp.Infrastructure/Data/`
  - Create `CompanyConfiguration.cs` implementing `IEntityTypeConfiguration<SysCompany>`
  - Create `BranchConfiguration.cs` implementing `IEntityTypeConfiguration<SysBranch>`
  - Create `UserConfiguration.cs` implementing `IEntityTypeConfiguration<SysUser>`
  - Create `RoleConfiguration.cs` implementing `IEntityTypeConfiguration<SysRole>`
  - Create `CurrencyConfiguration.cs` implementing `IEntityTypeConfiguration<SysCurrency>`
  - Create `FiscalYearConfiguration.cs` implementing `IEntityTypeConfiguration<SysFiscalYear>`
  - Map all properties to Oracle column names using `HasColumnName`
  - Configure primary keys with Oracle sequences using `HasDefaultValueSql`
  - Define foreign key relationships using `HasOne/HasMany/WithMany`
  - Configure IS_ACTIVE to bool conversion ('Y'/'N' to true/false)
  - Apply global query filter for soft delete on IS_ACTIVE
  - _Requirements: REQ-2, REQ-18, REQ-25_

- [x] 1.4 Create Entity Configuration Classes (Part 2: Permission Entities)
  - Create `RoleScreenPermissionConfiguration.cs`
  - Create `UserScreenPermissionConfiguration.cs`
  - Create `UserRoleConfiguration.cs`
  - Create `ScreenConfiguration.cs`
  - Create `SystemConfiguration.cs`
  - Create `CompanySystemConfiguration.cs`
  - Map all properties and relationships
  - Configure composite primary keys where applicable
  - _Requirements: REQ-2, REQ-18_

- [x] 1.5 Create Entity Configuration Classes (Part 3: Ticket Entities)
  - Create `TicketConfiguration.cs` (SysRequestTicket)
  - Create `TicketTypeConfiguration.cs`
  - Create `TicketStatusConfiguration.cs`
  - Create `TicketPriorityConfiguration.cs`
  - Create `TicketCommentConfiguration.cs`
  - Create `TicketAttachmentConfiguration.cs` (with BLOB mapping)
  - Create `TicketConfigConfiguration.cs`
  - Create `TicketCategoryConfiguration.cs`
  - Map BLOB columns for attachments using `HasColumnType("BLOB")`
  - _Requirements: REQ-2, REQ-18, REQ-23_

- [x] 1.6 Create Entity Configuration Classes (Part 4: Admin & Audit Entities)
  - Create `SuperAdminConfiguration.cs`
  - Create `AuditLogConfiguration.cs`
  - Create `SavedSearchConfiguration.cs`
  - Create `SearchAnalyticsConfiguration.cs`
  - Map all properties and relationships
  - _Requirements: REQ-2, REQ-10, REQ-18_

- [x] 1.7 Create EF Core Extension Methods
  - Create `EfCoreExtensions.cs` in `src/ThinkOnErp.Infrastructure/Data/`
  - Implement `ToListAsyncSafe<T>` method with error handling
  - Implement `FirstOrDefaultAsyncSafe<T>` method with error handling
  - Implement `AnyAsyncSafe` method with error handling
  - Add logging extensions for query execution
  - Add pagination extension methods (Skip/Take helpers)
  - _Requirements: REQ-5, REQ-15_

- [x] 1.8 Create EF Core Audit Interceptor
  - Create `EfCoreAuditInterceptor.cs` in `src/ThinkOnErp.Infrastructure/Interceptors/`
  - Implement `SaveChangesInterceptor` base class
  - Override `SavingChangesAsync` method
  - Capture entity changes (Added, Modified, Deleted states)
  - Extract old and new values from ChangeTracker
  - Integrate with existing `IAuditRepository`
  - Capture user context from `IHttpContextAccessor`
  - _Requirements: REQ-10_

- [x] 1.9 Create Repository Exception Handler
  - Create `RepositoryExceptionHandler.cs` in `src/ThinkOnErp.Infrastructure/Exceptions/`
  - Map `OracleException` to domain-specific exceptions
  - Handle constraint violations (ORA-00001 unique constraint)
  - Handle application errors (ORA-20000 series)
  - Handle connection failures
  - Handle timeout exceptions
  - Map `DbUpdateException` to domain exceptions
  - Map `DbUpdateConcurrencyException` for optimistic concurrency
  - _Requirements: REQ-14_

- [x] 1.10 Update Dependency Injection Configuration
  - Update `DependencyInjection.cs` in `src/ThinkOnErp.Infrastructure/`
  - Add `AddDbContext<ThinkOnErpDbContext>` registration
  - Configure Oracle provider with connection string from IConfiguration
  - Set DbContext lifetime to Scoped
  - Configure Oracle-specific options (UseOracleSQLCompatibility, CommandTimeout)
  - Enable sensitive data logging in development
  - Register `EfCoreAuditInterceptor` as interceptor
  - Add feature flag support for gradual migration (UseEfCore:RepositoryName)
  - Maintain existing `OracleDbContext` registration for coexistence
  - _Requirements: REQ-1, REQ-7, REQ-13_

## Phase 2: Pilot Repository Migration (Week 3-4)

- [x] 2.1 Migrate CurrencyRepository to EF Core
  - Create `EfCore` subfolder in `src/ThinkOnErp.Infrastructure/Repositories/`
  - Create `CurrencyRepository.cs` in EfCore folder
  - Inject `ThinkOnErpDbContext` instead of `OracleDbContext`
  - Implement `GetAllAsync` using LINQ with `AsNoTracking()` and `OrderBy`
  - Implement `GetByIdAsync` using LINQ with `FirstOrDefaultAsync`
  - Implement `CreateAsync` using `_context.Currencies.Add()` and `SaveChangesAsync()`
  - Implement `UpdateAsync` using `_context.Currencies.Update()` and `SaveChangesAsync()`
  - Implement `DeleteAsync` using soft delete (set IsActive = false) and `SaveChangesAsync()`
  - Let EF Core handle ID generation from Oracle sequences automatically
  - Add exception handling and mapping using `RepositoryExceptionHandler`
  - Preserve all existing method signatures and return types
  - _Requirements: REQ-4, REQ-5, REQ-15_

- [x] 2.2 Migrate TicketStatusRepository to EF Core
  - Create `TicketStatusRepository.cs` in EfCore folder
  - Inject `ThinkOnErpDbContext`
  - Implement `GetAllAsync` using LINQ with `AsNoTracking()` and `OrderBy`
  - Implement `GetByIdAsync` using LINQ with `FirstOrDefaultAsync`
  - Implement `CreateAsync` using `Add()` and `SaveChangesAsync()`
  - Implement `UpdateAsync` using `Update()` and `SaveChangesAsync()`
  - Implement `DeleteAsync` using soft delete and `SaveChangesAsync()`
  - Add exception handling using `RepositoryExceptionHandler`
  - Preserve method signatures
  - _Requirements: REQ-4, REQ-5, REQ-15_

- [x] 2.3 Migrate TicketPriorityRepository to EF Core
  - Create `TicketPriorityRepository.cs` in EfCore folder
  - Inject `ThinkOnErpDbContext`
  - Implement all methods using pure LINQ queries
  - Implement CRUD operations with `Add()`, `Update()`, soft delete, `SaveChangesAsync()`
  - Add exception handling using `RepositoryExceptionHandler`
  - Preserve method signatures
  - _Requirements: REQ-4, REQ-5, REQ-15_

- [-] 2.4 Create Unit Tests for Pilot Repositories (OPTIONAL)
  - Create test project if not exists: `ThinkOnErp.Infrastructure.Tests`
  - Add EF Core InMemory provider for testing
  - Create `CurrencyRepositoryTests.cs`
  - Create `TicketStatusRepositoryTests.cs`
  - Create `TicketPriorityRepositoryTests.cs`
  - Test all CRUD operations
  - Test exception scenarios
  - Test null handling
  - Achieve 80%+ code coverage
  - _Requirements: REQ-12_

- [x] 2.5 Create Integration Tests for Pilot Repositories (OPTIONAL)
  - Create `IntegrationTests` folder in test project
  - Configure test Oracle connection string
  - Create `CurrencyRepositoryIntegrationTests.cs`
  - Create `TicketStatusRepositoryIntegrationTests.cs`
  - Create `TicketPriorityRepositoryIntegrationTests.cs`
  - Test stored procedure execution
  - Test output parameter retrieval
  - Test transaction rollback
  - Verify data persistence
  - _Requirements: REQ-12_

- [x] 2.6 Update DI Configuration for Pilot Repositories
  - Update `DependencyInjection.cs`
  - Add conditional registration for `ICurrencyRepository`
  - Add conditional registration for `ITicketStatusRepository`
  - Add conditional registration for `ITicketPriorityRepository`
  - Read feature flags from `IConfiguration` (UseEfCore:CurrencyRepository, etc.)
  - Default to ADO.NET implementation if flag not set
  - Document feature flag usage in appsettings.json
  - _Requirements: REQ-13_

## Phase 3: Core Entity Repositories (Week 5-7)

- [x] 3.1 Migrate CompanyRepository to EF Core
  - Create `CompanyRepository.cs` in EfCore folder
  - Implement `GetAllAsync` using LINQ with `Include(c => c.Currency)` and `Include(c => c.DefaultBranch)`
  - Implement `GetByIdAsync` using LINQ with eager loading
  - Implement `CreateAsync` using `_context.Companies.Add()` and `SaveChangesAsync()`
  - Let EF Core retrieve generated ID from Oracle sequence automatically
  - Implement `UpdateAsync` using `_context.Companies.Update()` and `SaveChangesAsync()`
  - Implement `DeleteAsync` using soft delete (set IsActive = false)
  - Implement `UpdateLogoAsync` by loading entity, updating CompanyLogo property, and saving
  - Implement `GetLogoAsync` using LINQ projection to select only logo column
  - Implement `CreateWithBranchAsync` using EF Core transaction with multiple Add operations
  - Implement `SetDefaultBranchAsync` by loading entity, updating DefaultBranchId, and saving
  - Handle unique constraint violations for company code
  - Add comprehensive exception handling using `RepositoryExceptionHandler`
  - _Requirements: REQ-4, REQ-5, REQ-23, REQ-29_

- [x] 3.2 Migrate BranchRepository to EF Core
  - Create `BranchRepository.cs` in EfCore folder
  - Implement `GetAllAsync` using LINQ with `Include(b => b.Company)`
  - Implement `GetByIdAsync` using LINQ with eager loading
  - Implement `GetByCompanyIdAsync` using LINQ with `Where(b => b.CompanyId == companyId)`
  - Implement `CreateAsync` using `Add()` and `SaveChangesAsync()`
  - Implement `UpdateAsync` using `Update()` and `SaveChangesAsync()`
  - Implement `DeleteAsync` using soft delete
  - Handle BLOB for branch logo (UpdateLogoAsync, GetLogoAsync)
  - Add exception handling using `RepositoryExceptionHandler`
  - _Requirements: REQ-4, REQ-5, REQ-23, REQ-29_

- [x] 3.3 Migrate UserRepository to EF Core
  - Create `UserRepository.cs` in EfCore folder
  - Implement `GetAllAsync` using LINQ with `Include(u => u.Company)` and `Include(u => u.Branch)`
  - Implement `GetByIdAsync` using LINQ with eager loading
  - Implement `GetByUsernameAsync` using LINQ with `FirstOrDefaultAsync(u => u.UserName == username)`
  - Implement `GetByRefreshTokenAsync` for authentication using LINQ
  - Implement `CreateAsync` with password hash using `Add()` and `SaveChangesAsync()`
  - Implement `UpdateAsync` using `Update()` and `SaveChangesAsync()`
  - Implement `UpdateRefreshTokenAsync` by loading entity, updating token properties, and saving
  - Implement `ClearRefreshTokenAsync` for logout by setting token to null
  - Implement `ForceLogoutAsync` to clear tokens for specific user
  - Filter by company and branch for multi-tenancy using `Where()` clauses
  - Add exception handling using `RepositoryExceptionHandler`
  - _Requirements: REQ-4, REQ-5, REQ-27, REQ-28_

- [x] 3.4 Migrate RoleRepository to EF Core
  - Create `RoleRepository.cs` in EfCore folder
  - Implement `GetAllAsync` using LINQ with `Include(r => r.RoleScreenPermissions).ThenInclude(p => p.Screen)`
  - Implement `GetByIdAsync` with eager loading of permissions
  - Implement `GetByCompanyIdAsync` using LINQ with `Where(r => r.CompanyId == companyId)`
  - Implement `CreateAsync` using `Add()` and `SaveChangesAsync()`
  - Implement `UpdateAsync` using `Update()` and `SaveChangesAsync()`
  - Implement `DeleteAsync` using soft delete
  - Add exception handling using `RepositoryExceptionHandler`
  - _Requirements: REQ-4, REQ-5_

- [x] 3.5 Migrate FiscalYearRepository to EF Core
  - Create `FiscalYearRepository.cs` in EfCore folder
  - Implement `GetAllAsync` using LINQ with `Include(f => f.Branch)`
  - Implement `GetByIdAsync` using LINQ with eager loading
  - Implement `GetByBranchIdAsync` using LINQ with `Where(f => f.BranchId == branchId)`
  - Implement `GetActiveByBranchIdAsync` with filtering `Where(f => f.BranchId == branchId && f.IsActive)`
  - Implement `CreateAsync` using `Add()` and `SaveChangesAsync()`
  - Implement `UpdateAsync` using `Update()` and `SaveChangesAsync()`
  - Implement `DeleteAsync` using soft delete
  - Add exception handling using `RepositoryExceptionHandler`
  - _Requirements: REQ-4, REQ-5, REQ-29_

- [x] 3.6 Create Unit and Integration Tests for Core Repositories (OPTIONAL)
  - Create unit tests for CompanyRepository
  - Create unit tests for BranchRepository
  - Create unit tests for UserRepository
  - Create unit tests for RoleRepository
  - Create unit tests for FiscalYearRepository
  - Create integration tests for all five repositories
  - Test BLOB handling
  - Test multi-tenancy filtering
  - Test authentication scenarios
  - Achieve 80%+ code coverage
  - _Requirements: REQ-12_

- [x] 3.7 Update DI Configuration for Core Repositories
  - Update `DependencyInjection.cs`
  - Add conditional registration for all five core repositories
  - Add feature flags to appsettings.json
  - Document migration status
  - _Requirements: REQ-13_

## Phase 4: Permission Repositories (Week 8-9)

- [x] 4.1 Migrate Permission Repositories to EF Core
  - Create `RoleScreenPermissionRepository.cs` in EfCore folder
  - Create `UserScreenPermissionRepository.cs` in EfCore folder
  - Create `UserRoleRepository.cs` in EfCore folder
  - Create `ScreenRepository.cs` in EfCore folder
  - Create `SystemRepository.cs` in EfCore folder
  - Create `CompanySystemRepository.cs` in EfCore folder
  - Implement all methods using pure LINQ queries
  - Handle composite primary keys using `HasKey(e => new { e.Property1, e.Property2 })`
  - Handle many-to-many relationships with `Include()` and `ThenInclude()`
  - Implement CRUD operations with `Add()`, `Update()`, `Remove()`, `SaveChangesAsync()`
  - Add exception handling using `RepositoryExceptionHandler`
  - _Requirements: REQ-4, REQ-5_

- [x] 4.2 Create Tests for Permission Repositories (OPTIONAL)
  - Create unit tests for all six repositories
  - Create integration tests for all six repositories
  - Test composite key operations
  - Test relationship queries
  - Achieve 80%+ code coverage
  - _Requirements: REQ-12_

- [x] 4.3 Update DI Configuration for Permission Repositories
  - Update `DependencyInjection.cs`
  - Add conditional registration for all six repositories
  - Add feature flags
  - _Requirements: REQ-13_

## Phase 5: Ticket Management Repositories (Week 10-12)

- [x] 5.1 Migrate Ticket Core Repositories to EF Core
  - Create `TicketRepository.cs` in EfCore folder (SysRequestTicket)
  - Create `TicketTypeRepository.cs` in EfCore folder
  - Create `TicketCategoryRepository.cs` in EfCore folder
  - Create `TicketConfigRepository.cs` in EfCore folder
  - Implement complex queries with multiple `Include()` statements for related entities
  - Implement `GetByCompanyIdAsync` and `GetByBranchIdAsync` for multi-tenancy filtering
  - Implement `GetByStatusAsync`, `GetByPriorityAsync`, `GetByTypeAsync` using LINQ Where clauses
  - Implement CRUD operations with `Add()`, `Update()`, soft delete, `SaveChangesAsync()`
  - Add exception handling using `RepositoryExceptionHandler`
  - _Requirements: REQ-4, REQ-5, REQ-27_

- [x] 5.2 Migrate Ticket Detail Repositories to EF Core
  - Create `TicketCommentRepository.cs` in EfCore folder
  - Create `TicketAttachmentRepository.cs` in EfCore folder
  - Handle BLOB for attachments (store and retrieve byte arrays)
  - Implement `GetByTicketIdAsync` using LINQ with `Where(c => c.TicketId == ticketId)`
  - Implement `CreateAsync` for comments and attachments using `Add()` and `SaveChangesAsync()`
  - Implement `UpdateAsync` using `Update()` and `SaveChangesAsync()`
  - Implement `DeleteAsync` using soft delete or hard delete with `Remove()`
  - Add exception handling using `RepositoryExceptionHandler`
  - _Requirements: REQ-4, REQ-5, REQ-23_

- [x] 5.3 Create Tests for Ticket Repositories (OPTIONAL)
  - Create unit tests for all six ticket repositories
  - Create integration tests for all six repositories
  - Test BLOB handling for attachments
  - Test multi-tenancy filtering
  - Test complex queries with joins
  - Achieve 80%+ code coverage
  - _Requirements: REQ-12_

- [x] 5.4 Update DI Configuration for Ticket Repositories
  - Update `DependencyInjection.cs`
  - Add conditional registration for all six repositories
  - Add feature flags
  - _Requirements: REQ-13_

## Phase 6: Admin & Audit Repositories (Week 13-14)

- [x] 6.1 Migrate Admin Repositories to EF Core
  - Create `SuperAdminRepository.cs` in EfCore folder
  - Implement `GetAllAsync` using LINQ with `AsNoTracking()`
  - Implement `GetByIdAsync` using LINQ
  - Implement `GetByUsernameAsync` using LINQ with `FirstOrDefaultAsync`
  - Implement CRUD operations with `Add()`, `Update()`, soft delete, `SaveChangesAsync()`
  - Add exception handling using `RepositoryExceptionHandler`
  - _Requirements: REQ-4, REQ-5_

- [x] 6.2 Migrate Audit and Analytics Repositories to EF Core
  - Create `AuditLogRepository.cs` in EfCore folder
  - Create `SavedSearchRepository.cs` in EfCore folder
  - Create `SearchAnalyticsRepository.cs` in EfCore folder
  - Implement bulk insert for audit logs using `AddRangeAsync()` and `SaveChangesAsync()`
  - Implement complex analytics queries using LINQ with `GroupBy()`, `Select()`, `OrderBy()`
  - Implement date range filtering using `Where(a => a.Timestamp >= startDate && a.Timestamp <= endDate)`
  - Implement pagination using `Skip()` and `Take()`
  - Implement CRUD operations with `Add()`, `Update()`, `SaveChangesAsync()`
  - Add exception handling using `RepositoryExceptionHandler`
  - _Requirements: REQ-4, REQ-5, REQ-10_

- [x] 6.3 Create Tests for Admin & Audit Repositories (OPTIONAL)
  - Create unit tests for all four repositories
  - Create integration tests for all four repositories
  - Test bulk operations
  - Test analytics queries
  - Achieve 80%+ code coverage
  - _Requirements: REQ-12_

- [x] 6.4 Update DI Configuration for Admin & Audit Repositories
  - Update `DependencyInjection.cs`
  - Add conditional registration for all four repositories
  - Add feature flags
  - _Requirements: REQ-13_

## Phase 7: Testing, Performance, and Deployment (Week 15-16)

- [x] 7.1 Create Comparison Tests (OPTIONAL)
  - Create `ComparisonTests` folder in test project
  - For each repository, create comparison test that runs same query with both implementations
  - Compare results for equality (same data, same order)
  - Verify all 23 repositories produce identical results
  - Document any differences found
  - _Requirements: REQ-12_

- [x] 7.2 Create Performance Tests (OPTIONAL)
  - Add BenchmarkDotNet package to test project
  - Create `PerformanceBenchmarks` folder
  - Create benchmarks for common operations (GetAll, GetById, Create, Update, Delete)
  - Run benchmarks for all 23 repositories
  - Document performance metrics (queries per second, average latency)
  - Identify and optimize slow queries using EF Core query optimization techniques
  - Use `AsNoTracking()` for read-only queries
  - Use compiled queries for frequently executed queries
  - Optimize `Include()` statements to avoid over-fetching
  - Configure query splitting to avoid cartesian explosion
  - _Requirements: REQ-9, REQ-12_

- [x] 7.3 Create End-to-End API Tests (OPTIONAL)
  - Create `E2ETests` folder in test project
  - Test all API endpoints with EF Core repositories
  - Verify response models match ADO.NET responses
  - Verify HTTP status codes
  - Verify error messages
  - Verify pagination, filtering, sorting
  - Test authentication and authorization
  - _Requirements: REQ-11, REQ-12_

- [x] 7.4 Performance Optimization
  - Enable connection pooling with appropriate pool size
  - Use compiled queries for frequently executed queries
  - Optimize Include/ThenInclude usage
  - Configure query splitting strategy
  - Batch insert/update operations where possible
  - Add indexes if needed (document for DBA)
  - Re-run performance tests to verify improvements
  - _Requirements: REQ-9_

- [x] 7.5 Create Migration Documentation
  - Create `docs/EF_CORE_MIGRATION_GUIDE.md`
  - Document the pure EF Core approach (no stored procedures)
  - Provide examples of common LINQ query patterns (filtering, sorting, pagination, eager loading)
  - Document entity configurations and relationships
  - Explain EF Core transaction management with `BeginTransactionAsync()`
  - Create troubleshooting guide for common EF Core issues
  - Document performance optimization techniques (AsNoTracking, compiled queries, query splitting)
  - Document LINQ best practices (avoid N+1 queries, use projection, batch operations)
  - Document feature flag usage for gradual migration
  - Document rollback procedure (disable feature flags)
  - Include code examples for CRUD operations
  - _Requirements: REQ-20_

- [x] 7.6 Create Monitoring and Health Checks
  - Configure EF Core logging to log all SQL queries
  - Add query execution time logging
  - Create health check endpoint for DbContext connectivity
  - Integrate with existing logging infrastructure (Serilog)
  - Add metrics for connection pool usage
  - Add alerts for slow queries (>1 second)
  - Add alerts for high error rates
  - Create dashboard for EF Core metrics
  - _Requirements: REQ-21_

- [x] 7.7 Update Configuration Files
  - Add EF Core logging configuration
  - Add connection pool configuration
  - Add feature flags for all 23 repositories
  - Add health check configuration
  - Document all configuration options
  - Create appsettings.Development.json with development settings
  - Create appsettings.Production.json with production settings
  - _Requirements: REQ-7, REQ-13, REQ-19_

- [x] 7.8 Create Deployment Plan
  - Document blue-green deployment strategy
  - Document gradual traffic shifting approach
  - Create deployment checklist
  - Create rollback procedure (disable feature flags)
  - Document health check verification steps
  - Document monitoring during deployment
  - Create automated rollback triggers (error rate threshold)
  - Document connection pool warm-up procedure
  - Estimate deployment time (zero downtime)
  - _Requirements: REQ-19_

- [ ] 7.9 Execute Pilot Deployment
  - Enable feature flags for pilot repositories in production
  - Verify health checks pass
  - Monitor error rates for 24 hours
  - Monitor performance metrics
  - Compare metrics with ADO.NET baseline
  - Verify no API contract changes
  - Document any issues encountered
  - Rollback if error rate exceeds 1%
  - _Requirements: REQ-19_

- [ ] 7.10 Execute Full Deployment
  - Deploy core repositories (Company, Branch, User, Role, FiscalYear) - Wave 1
  - Monitor for 48 hours
  - Deploy permission repositories - Wave 2
  - Monitor for 48 hours
  - Deploy ticket repositories - Wave 3
  - Monitor for 48 hours
  - Deploy admin and audit repositories - Wave 4
  - Monitor for 48 hours
  - Verify all 23 repositories are using EF Core
  - Remove ADO.NET implementations after 2 weeks of stable operation
  - Update documentation with final deployment status
  - _Requirements: REQ-19_

## Notes

**Total Tasks**: 50 tasks across 7 phases  
**Estimated Duration**: 16 weeks  
**Total Repositories**: 23 repositories to migrate  
**Approach**: Pure EF Core with LINQ queries (no stored procedures, no ADO.NET)  
**Testing Strategy**: Unit tests, integration tests, comparison tests (optional), performance tests, E2E tests  
**Deployment Strategy**: Gradual migration with feature flags and rollback capability  
**Success Criteria**: 
- All 23 repositories migrated to pure EF Core
- All CRUD operations using EF Core methods (Add, Update, Remove, SaveChangesAsync)
- All queries using LINQ (Where, Include, Select, OrderBy, etc.)
- All tests passing with 80%+ code coverage
- Optimized performance with EF Core best practices
- Zero API contract changes
- Zero downtime deployment
- Successful production operation for 2 weeks

## Task Dependency Graph

```json
{
  "waves": [
    { "id": 0, "tasks": ["1.1", "1.2"] },
    { "id": 1, "tasks": ["1.3", "1.4", "1.5", "1.6"] },
    { "id": 2, "tasks": ["1.7", "1.8", "1.9"] },
    { "id": 3, "tasks": ["1.10"] },
    { "id": 4, "tasks": ["2.1", "2.2", "2.3"] },
    { "id": 5, "tasks": ["2.4", "2.5"] },
    { "id": 6, "tasks": ["2.6"] },
    { "id": 7, "tasks": ["3.1", "3.2", "3.3", "3.4", "3.5"] },
    { "id": 8, "tasks": ["3.6"] },
    { "id": 9, "tasks": ["3.7"] },
    { "id": 10, "tasks": ["4.1"] },
    { "id": 11, "tasks": ["4.2"] },
    { "id": 12, "tasks": ["4.3"] },
    { "id": 13, "tasks": ["5.1", "5.2"] },
    { "id": 14, "tasks": ["5.3"] },
    { "id": 15, "tasks": ["5.4"] },
    { "id": 16, "tasks": ["6.1", "6.2"] },
    { "id": 17, "tasks": ["6.3"] },
    { "id": 18, "tasks": ["6.4"] },
    { "id": 19, "tasks": ["7.1", "7.2", "7.3"] },
    { "id": 20, "tasks": ["7.4"] },
    { "id": 21, "tasks": ["7.5", "7.6", "7.7"] },
    { "id": 22, "tasks": ["7.8"] },
    { "id": 23, "tasks": ["7.9"] },
    { "id": 24, "tasks": ["7.10"] }
  ]
}
```
