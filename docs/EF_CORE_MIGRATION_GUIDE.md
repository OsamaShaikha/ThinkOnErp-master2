# EF Core Migration Guide

## Table of Contents

1. [Overview](#overview)
2. [Pure EF Core Approach](#pure-ef-core-approach)
3. [Common LINQ Query Patterns](#common-linq-query-patterns)
4. [Entity Configurations and Relationships](#entity-configurations-and-relationships)
5. [Transaction Management](#transaction-management)
6. [Performance Optimization Techniques](#performance-optimization-techniques)
7. [LINQ Best Practices](#linq-best-practices)
8. [Feature Flag Usage for Gradual Migration](#feature-flag-usage-for-gradual-migration)
9. [Rollback Procedure](#rollback-procedure)
10. [CRUD Operation Examples](#crud-operation-examples)
11. [Troubleshooting Guide](#troubleshooting-guide)

---

## Overview

This guide provides comprehensive documentation for the ThinkOnErp ERP system's migration from ADO.NET to Entity Framework Core (EF Core). The migration uses a **pure EF Core approach** with LINQ queries, eliminating the need for stored procedures while maintaining backward compatibility with existing API contracts.

### Key Benefits

- **Type Safety**: Compile-time checking for queries and entity mappings
- **Productivity**: LINQ queries reduce boilerplate code for simple operations
- **Maintainability**: Centralized entity configurations and relationship definitions
- **Testability**: Easier unit testing with in-memory database provider
- **Change Tracking**: Automatic detection of entity modifications
- **Performance**: Optimized query generation and connection pooling

### Migration Scope

- **Total Repositories**: 23 repositories migrated to EF Core
- **Database**: Oracle Database (no schema changes required)
- **Architecture**: Clean Architecture maintained (Domain, Application, Infrastructure, API layers)
- **Approach**: Pure EF Core with LINQ queries (no stored procedures)
- **Deployment**: Gradual migration with feature flags and rollback capability

---

## Pure EF Core Approach

The migration uses **pure Entity Framework Core** without stored procedures. All database operations are performed using:

- **LINQ queries** for data retrieval (filtering, sorting, pagination, eager loading)
- **EF Core methods** for data modification (`Add()`, `Update()`, `Remove()`, `SaveChangesAsync()`)
- **EF Core transactions** for multi-operation atomicity (`BeginTransactionAsync()`)
- **Entity configurations** for table mappings and relationships

### Why Pure EF Core?

1. **Simplified Maintenance**: No need to maintain separate stored procedure code
2. **Better Refactoring**: Changes to entities automatically update queries
3. **Improved Testing**: Easier to test with in-memory database
4. **Cross-Database Compatibility**: Easier to support multiple database providers
5. **Modern Development**: Aligns with current .NET best practices

### Architecture Overview

```
┌─────────────────────────────────────────────────────────────┐
│                     Application Layer                        │
│  - Services, Use Cases, Application DTOs                     │
└─────────────────────────────────────────────────────────────┘
                            │
                            ▼
┌─────────────────────────────────────────────────────────────┐
│                      Domain Layer                            │
│  - Entities, Repository Interfaces, Domain Exceptions        │
└─────────────────────────────────────────────────────────────┘
                            │
                            ▼
┌─────────────────────────────────────────────────────────────┐
│                  Infrastructure Layer                        │
│  ┌───────────────────────────────────────────────────────┐  │
│  │           ThinkOnErpDbContext                         │  │
│  │  - DbSet<TEntity> properties                          │  │
│  │  - Entity configurations                              │  │
│  │  - Interceptors (Audit, Performance)                  │  │
│  └───────────────────────────────────────────────────────┘  │
│  ┌───────────────────────────────────────────────────────┐  │
│  │      EF Core Repository Implementations               │  │
│  │  - CompanyRepository, BranchRepository, etc.          │  │
│  │  - Pure LINQ queries                                  │  │
│  │  - EF Core change tracking                            │  │
│  └───────────────────────────────────────────────────────┘  │
└─────────────────────────────────────────────────────────────┘
                            │
                            ▼
┌─────────────────────────────────────────────────────────────┐
│                    Oracle Database                           │
│  - Tables (SYS_COMPANY, SYS_USERS, etc.)                    │
│  - Sequences (SEQ_SYS_*)                                    │
│  - Constraints and Indexes                                   │
└─────────────────────────────────────────────────────────────┘
```

---

## Common LINQ Query Patterns

### 1. Basic Filtering

```csharp
// Get all active companies
var companies = await _context.Companies
    .AsNoTracking()
    .Where(c => c.IsActive)
    .ToListAsync();

// Get company by ID
var company = await _context.Companies
    .AsNoTracking()
    .FirstOrDefaultAsync(c => c.RowId == companyId);

// Get companies by multiple criteria
var filteredCompanies = await _context.Companies
    .AsNoTracking()
    .Where(c => c.IsActive && c.CountryId == countryId)
    .ToListAsync();
```

### 2. Sorting

```csharp
// Sort by single property
var companies = await _context.Companies
    .AsNoTracking()
    .OrderBy(c => c.RowDesc)
    .ToListAsync();

// Sort by multiple properties
var companies = await _context.Companies
    .AsNoTracking()
    .OrderBy(c => c.CountryId)
    .ThenBy(c => c.RowDesc)
    .ToListAsync();

// Descending sort
var companies = await _context.Companies
    .AsNoTracking()
    .OrderByDescending(c => c.CreationDate)
    .ToListAsync();
```

### 3. Pagination

```csharp
// Skip and Take for pagination
var pageNumber = 1;
var pageSize = 20;

var companies = await _context.Companies
    .AsNoTracking()
    .OrderBy(c => c.RowDesc)
    .Skip((pageNumber - 1) * pageSize)
    .Take(pageSize)
    .ToListAsync();

// Get total count for pagination metadata
var totalCount = await _context.Companies
    .CountAsync();
```

### 4. Eager Loading (Include)

```csharp
// Load single navigation property
var companies = await _context.Companies
    .AsNoTracking()
    .Include(c => c.Currency)
    .ToListAsync();

// Load multiple navigation properties
var companies = await _context.Companies
    .AsNoTracking()
    .Include(c => c.Currency)
    .Include(c => c.DefaultBranch)
    .ToListAsync();

// Load nested navigation properties (ThenInclude)
var branches = await _context.Branches
    .AsNoTracking()
    .Include(b => b.Company)
        .ThenInclude(c => c.Currency)
    .ToListAsync();
```

### 5. Projection (Select)

```csharp
// Select specific properties for performance
var companyNames = await _context.Companies
    .AsNoTracking()
    .Select(c => new { c.RowId, c.RowDesc, c.RowDescE })
    .ToListAsync();

// Select with computed properties
var companyInfo = await _context.Companies
    .AsNoTracking()
    .Select(c => new
    {
        c.RowId,
        c.RowDesc,
        BranchCount = c.Branches.Count()
    })
    .ToListAsync();

// Get single column value
var logo = await _context.Companies
    .AsNoTracking()
    .Where(c => c.RowId == companyId)
    .Select(c => c.CompanyLogo)
    .FirstOrDefaultAsync();
```

### 6. Aggregation

```csharp
// Count
var companyCount = await _context.Companies
    .CountAsync();

var activeCompanyCount = await _context.Companies
    .CountAsync(c => c.IsActive);

// Any (existence check)
var hasCompanies = await _context.Companies
    .AnyAsync();

var hasActiveCompanies = await _context.Companies
    .AnyAsync(c => c.IsActive);

// Sum, Average, Min, Max
var totalUsers = await _context.Users
    .SumAsync(u => u.LoginCount);

var averageLoginCount = await _context.Users
    .AverageAsync(u => u.LoginCount);
```

### 7. Grouping

```csharp
// Group by single property
var companiesByCountry = await _context.Companies
    .AsNoTracking()
    .GroupBy(c => c.CountryId)
    .Select(g => new
    {
        CountryId = g.Key,
        Count = g.Count()
    })
    .ToListAsync();

// Group with multiple aggregations
var userStatsByBranch = await _context.Users
    .AsNoTracking()
    .GroupBy(u => u.BranchId)
    .Select(g => new
    {
        BranchId = g.Key,
        TotalUsers = g.Count(),
        ActiveUsers = g.Count(u => u.IsActive),
        TotalLogins = g.Sum(u => u.LoginCount)
    })
    .ToListAsync();
```

### 8. Joins

```csharp
// Inner join using navigation properties (preferred)
var usersWithBranches = await _context.Users
    .AsNoTracking()
    .Include(u => u.Branch)
    .ToListAsync();

// Explicit join using Join method
var userBranchInfo = await _context.Users
    .AsNoTracking()
    .Join(
        _context.Branches,
        user => user.BranchId,
        branch => branch.RowId,
        (user, branch) => new
        {
            UserName = user.UserName,
            BranchName = branch.RowDesc
        })
    .ToListAsync();

// Left join using GroupJoin and SelectMany
var companiesWithBranches = await _context.Companies
    .AsNoTracking()
    .GroupJoin(
        _context.Branches,
        company => company.RowId,
        branch => branch.ParRowId,
        (company, branches) => new { company, branches })
    .SelectMany(
        x => x.branches.DefaultIfEmpty(),
        (x, branch) => new
        {
            CompanyName = x.company.RowDesc,
            BranchName = branch != null ? branch.RowDesc : null
        })
    .ToListAsync();
```

### 9. String Operations

```csharp
// Contains (LIKE %value%)
var companies = await _context.Companies
    .AsNoTracking()
    .Where(c => c.RowDesc.Contains(searchTerm))
    .ToListAsync();

// StartsWith (LIKE value%)
var companies = await _context.Companies
    .AsNoTracking()
    .Where(c => c.CompanyCode.StartsWith(prefix))
    .ToListAsync();

// EndsWith (LIKE %value)
var companies = await _context.Companies
    .AsNoTracking()
    .Where(c => c.Email.EndsWith("@example.com"))
    .ToListAsync();

// Case-insensitive search
var companies = await _context.Companies
    .AsNoTracking()
    .Where(c => EF.Functions.Like(c.RowDesc.ToLower(), $"%{searchTerm.ToLower()}%"))
    .ToListAsync();
```

### 10. Date Operations

```csharp
// Filter by date range
var startDate = new DateTime(2024, 1, 1);
var endDate = new DateTime(2024, 12, 31);

var fiscalYears = await _context.FiscalYears
    .AsNoTracking()
    .Where(f => f.StartDate >= startDate && f.EndDate <= endDate)
    .ToListAsync();

// Filter by current date
var activeFiscalYears = await _context.FiscalYears
    .AsNoTracking()
    .Where(f => f.StartDate <= DateTime.Now && f.EndDate >= DateTime.Now)
    .ToListAsync();

// Extract date parts
var companiesByYear = await _context.Companies
    .AsNoTracking()
    .GroupBy(c => c.CreationDate.Value.Year)
    .Select(g => new
    {
        Year = g.Key,
        Count = g.Count()
    })
    .ToListAsync();
```


---

## Entity Configurations and Relationships

### Entity Configuration Pattern

All entity configurations implement `IEntityTypeConfiguration<TEntity>` and are located in `src/ThinkOnErp.Infrastructure/Data/Configurations/`.

### Example: Company Configuration

```csharp
public class CompanyConfiguration : IEntityTypeConfiguration<SysCompany>
{
    public void Configure(EntityTypeBuilder<SysCompany> builder)
    {
        // Table mapping
        builder.ToTable("SYS_COMPANY");
        
        // Primary key with Oracle sequence
        builder.HasKey(e => e.RowId);
        builder.Property(e => e.RowId)
            .HasColumnName("ROW_ID")
            .HasDefaultValueSql("SEQ_SYS_COMPANY.NEXTVAL")
            .ValueGeneratedOnAdd();
        
        // Required properties
        builder.Property(e => e.RowDesc)
            .HasColumnName("ROW_DESC")
            .HasMaxLength(200)
            .IsRequired();
        
        builder.Property(e => e.RowDescE)
            .HasColumnName("ROW_DESC_E")
            .HasMaxLength(200)
            .IsRequired();
        
        // Optional properties
        builder.Property(e => e.LegalName)
            .HasColumnName("LEGAL_NAME")
            .HasMaxLength(300);
        
        // BLOB property
        builder.Property(e => e.CompanyLogo)
            .HasColumnName("COMPANY_LOGO")
            .HasColumnType("BLOB");
        
        // Boolean conversion (Y/N to true/false)
        builder.Property(e => e.IsActive)
            .HasColumnName("IS_ACTIVE")
            .HasConversion(
                v => v ? "Y" : "N",
                v => v == "Y" || v == "1")
            .HasMaxLength(1)
            .IsRequired();
        
        // Foreign keys
        builder.HasOne(e => e.Currency)
            .WithMany()
            .HasForeignKey(e => e.CurrId)
            .HasConstraintName("FK_COMPANY_CURRENCY");
        
        builder.HasOne(e => e.DefaultBranch)
            .WithMany()
            .HasForeignKey(e => e.DefaultBranchId)
            .HasConstraintName("FK_COMPANY_DEFAULT_BRANCH");
        
        // Global query filter for soft delete
        builder.HasQueryFilter(e => e.IsActive);
        
        // Indexes
        builder.HasIndex(e => e.CompanyCode)
            .IsUnique()
            .HasDatabaseName("UK_COMPANY_CODE");
    }
}
```

### Relationship Types

#### One-to-Many Relationship

```csharp
// Company has many Branches
builder.HasOne(b => b.Company)
    .WithMany(c => c.Branches)
    .HasForeignKey(b => b.ParRowId)
    .HasConstraintName("FK_BRANCH_COMPANY");
```

#### Many-to-One Relationship

```csharp
// Branch belongs to one Company
builder.HasOne(b => b.Company)
    .WithMany()
    .HasForeignKey(b => b.ParRowId)
    .HasConstraintName("FK_BRANCH_COMPANY");
```

#### Many-to-Many Relationship

```csharp
// User has many Roles through UserRole join table
builder.HasMany(u => u.Roles)
    .WithMany(r => r.Users)
    .UsingEntity<SysUserRole>(
        j => j.HasOne(ur => ur.Role)
            .WithMany()
            .HasForeignKey(ur => ur.RoleId),
        j => j.HasOne(ur => ur.User)
            .WithMany()
            .HasForeignKey(ur => ur.UserId),
        j =>
        {
            j.ToTable("SYS_USER_ROLE");
            j.HasKey(ur => new { ur.UserId, ur.RoleId });
        });
```

#### Self-Referencing Relationship

```csharp
// Screen has parent Screen
builder.HasOne(s => s.ParentScreen)
    .WithMany(s => s.ChildScreens)
    .HasForeignKey(s => s.ParentScreenId)
    .HasConstraintName("FK_SCREEN_PARENT");
```

### Common Configuration Patterns

#### Oracle Sequence Configuration

```csharp
builder.Property(e => e.RowId)
    .HasColumnName("ROW_ID")
    .HasDefaultValueSql("SEQ_SYS_COMPANY.NEXTVAL")
    .ValueGeneratedOnAdd();
```

#### Boolean to Y/N Conversion

```csharp
builder.Property(e => e.IsActive)
    .HasColumnName("IS_ACTIVE")
    .HasConversion(
        v => v ? "Y" : "N",
        v => v == "Y" || v == "1")
    .HasMaxLength(1)
    .IsRequired();
```

#### BLOB Column Mapping

```csharp
builder.Property(e => e.CompanyLogo)
    .HasColumnName("COMPANY_LOGO")
    .HasColumnType("BLOB");
```

#### Soft Delete Global Query Filter

```csharp
// Automatically filters out soft-deleted records in all queries
builder.HasQueryFilter(e => e.IsActive);

// To include soft-deleted records, use IgnoreQueryFilters()
var allCompanies = await _context.Companies
    .IgnoreQueryFilters()
    .ToListAsync();
```

#### Composite Primary Key

```csharp
builder.HasKey(e => new { e.UserId, e.RoleId });
```

#### Unique Index

```csharp
builder.HasIndex(e => e.CompanyCode)
    .IsUnique()
    .HasDatabaseName("UK_COMPANY_CODE");
```

#### Composite Index

```csharp
builder.HasIndex(e => new { e.CompanyId, e.BranchId })
    .HasDatabaseName("IX_USER_COMPANY_BRANCH");
```

---

## Transaction Management

### Basic Transaction

```csharp
using var transaction = await _context.Database.BeginTransactionAsync();

try
{
    // Perform multiple operations
    _context.Companies.Add(company);
    await _context.SaveChangesAsync();
    
    _context.Branches.Add(branch);
    await _context.SaveChangesAsync();
    
    // Commit if all operations succeed
    await transaction.CommitAsync();
}
catch (Exception ex)
{
    // Rollback on error
    await transaction.RollbackAsync();
    _logger.LogError(ex, "Transaction failed");
    throw;
}
```

### Transaction with Multiple Entities

```csharp
public async Task<(long CompanyId, long BranchId, long FiscalYearId)> CreateWithBranchAsync(
    SysCompany company,
    SysBranch branch,
    SysFiscalYear fiscalYear)
{
    using var transaction = await _context.Database.BeginTransactionAsync();

    try
    {
        // Step 1: Create company
        _context.Companies.Add(company);
        await _context.SaveChangesAsync();
        
        // Step 2: Create branch with company ID
        branch.ParRowId = company.RowId;
        _context.Branches.Add(branch);
        await _context.SaveChangesAsync();
        
        // Step 3: Create fiscal year with branch ID
        fiscalYear.CompanyId = company.RowId;
        fiscalYear.BranchId = branch.RowId;
        _context.FiscalYears.Add(fiscalYear);
        await _context.SaveChangesAsync();
        
        // Step 4: Update company with default branch
        company.DefaultBranchId = branch.RowId;
        await _context.SaveChangesAsync();
        
        // Commit transaction
        await transaction.CommitAsync();
        
        return (company.RowId, branch.RowId, fiscalYear.RowId);
    }
    catch (Exception ex)
    {
        await transaction.RollbackAsync();
        _logger.LogError(ex, "Failed to create company with branch");
        throw;
    }
}
```

### Transaction Isolation Levels

```csharp
// Read Committed (default)
using var transaction = await _context.Database.BeginTransactionAsync(
    IsolationLevel.ReadCommitted);

// Serializable (highest isolation)
using var transaction = await _context.Database.BeginTransactionAsync(
    IsolationLevel.Serializable);

// Read Uncommitted (lowest isolation)
using var transaction = await _context.Database.BeginTransactionAsync(
    IsolationLevel.ReadUncommitted);
```

### Savepoints (Nested Transactions)

```csharp
using var transaction = await _context.Database.BeginTransactionAsync();

try
{
    // First operation
    _context.Companies.Add(company);
    await _context.SaveChangesAsync();
    
    // Create savepoint
    await transaction.CreateSavepointAsync("BeforeBranch");
    
    try
    {
        // Second operation
        _context.Branches.Add(branch);
        await _context.SaveChangesAsync();
    }
    catch (Exception)
    {
        // Rollback to savepoint (keeps company)
        await transaction.RollbackToSavepointAsync("BeforeBranch");
    }
    
    // Commit transaction
    await transaction.CommitAsync();
}
catch (Exception ex)
{
    await transaction.RollbackAsync();
    throw;
}
```

### Transaction Best Practices

1. **Keep transactions short**: Minimize the time between BeginTransaction and Commit
2. **Use try-catch-finally**: Always rollback on exceptions
3. **Avoid user interaction**: Don't wait for user input during a transaction
4. **Use appropriate isolation levels**: Balance consistency vs. performance
5. **Consider deadlocks**: Implement retry logic for transient failures

---

## Performance Optimization Techniques

### 1. AsNoTracking for Read-Only Queries

```csharp
// Without AsNoTracking (change tracking enabled)
var companies = await _context.Companies.ToListAsync();

// With AsNoTracking (better performance for read-only)
var companies = await _context.Companies
    .AsNoTracking()
    .ToListAsync();
```

**When to use**: All read-only queries where you don't need to update entities.

### 2. Compiled Queries

```csharp
// Define compiled query (in a static class)
public static class CompiledQueries
{
    public static readonly Func<ThinkOnErpDbContext, IAsyncEnumerable<SysCompany>> 
        GetAllCompanies = EF.CompileAsyncQuery(
            (ThinkOnErpDbContext context) => 
                context.Companies
                    .AsNoTracking()
                    .Include(c => c.Currency)
                    .Include(c => c.DefaultBranch)
                    .OrderBy(c => c.RowDesc));
    
    public static readonly Func<ThinkOnErpDbContext, long, Task<SysCompany?>> 
        GetCompanyById = EF.CompileAsyncQuery(
            (ThinkOnErpDbContext context, long id) => 
                context.Companies
                    .AsNoTracking()
                    .Include(c => c.Currency)
                    .Include(c => c.DefaultBranch)
                    .FirstOrDefault(c => c.RowId == id));
}

// Use compiled query
var companies = await CompiledQueries.GetAllCompanies(_context).ToListAsync();
var company = await CompiledQueries.GetCompanyById(_context, companyId);
```

**When to use**: Frequently executed queries with consistent structure.

### 3. Projection to Reduce Data Transfer

```csharp
// Bad: Loads all columns
var companies = await _context.Companies
    .AsNoTracking()
    .ToListAsync();

// Good: Loads only needed columns
var companyNames = await _context.Companies
    .AsNoTracking()
    .Select(c => new { c.RowId, c.RowDesc, c.RowDescE })
    .ToListAsync();
```

**When to use**: When you only need a subset of entity properties.

### 4. Avoid N+1 Query Problem

```csharp
// Bad: N+1 queries (1 for companies + N for branches)
var companies = await _context.Companies.ToListAsync();
foreach (var company in companies)
{
    var branches = await _context.Branches
        .Where(b => b.ParRowId == company.RowId)
        .ToListAsync();
}

// Good: Single query with Include
var companies = await _context.Companies
    .Include(c => c.Branches)
    .ToListAsync();
```

**When to use**: Always use Include for related data you know you'll need.

### 5. Split Queries for Cartesian Explosion

```csharp
// Without split query (cartesian explosion)
var companies = await _context.Companies
    .Include(c => c.Branches)
    .Include(c => c.Users)
    .ToListAsync();

// With split query (multiple queries)
var companies = await _context.Companies
    .AsSplitQuery()
    .Include(c => c.Branches)
    .Include(c => c.Users)
    .ToListAsync();
```

**When to use**: When including multiple collections that could cause cartesian explosion.

### 6. Batch Operations

```csharp
// Bad: Multiple SaveChanges calls
foreach (var company in companies)
{
    _context.Companies.Add(company);
    await _context.SaveChangesAsync();
}

// Good: Single SaveChanges call
_context.Companies.AddRange(companies);
await _context.SaveChangesAsync();
```

**When to use**: When inserting, updating, or deleting multiple entities.

### 7. Connection Pooling

```csharp
// In Program.cs or DependencyInjection.cs
services.AddDbContext<ThinkOnErpDbContext>(options =>
{
    options.UseOracle(
        connectionString,
        oracleOptions =>
        {
            // Enable connection pooling (enabled by default)
            oracleOptions.CommandTimeout(30);
            oracleOptions.UseOracleSQLCompatibility("11");
        });
});
```

**Configuration**: Connection pooling is enabled by default in Oracle.EntityFrameworkCore.

### 8. Query Filters for Multi-Tenancy

```csharp
// Configure global query filter in entity configuration
builder.HasQueryFilter(e => e.CompanyId == _currentCompanyId);

// All queries automatically filtered by company
var users = await _context.Users.ToListAsync();

// Bypass filter when needed
var allUsers = await _context.Users
    .IgnoreQueryFilters()
    .ToListAsync();
```

**When to use**: For multi-tenancy or soft delete scenarios.

### 9. Index Recommendations

```csharp
// Add indexes for frequently queried columns
builder.HasIndex(e => e.CompanyCode);
builder.HasIndex(e => e.Email);
builder.HasIndex(e => new { e.CompanyId, e.BranchId });
```

**Note**: Indexes must be created in the database. See `docs/EF_CORE_INDEX_RECOMMENDATIONS.md`.

### 10. Disable Change Tracking Globally

```csharp
// In DbContext constructor
public ThinkOnErpDbContext(DbContextOptions<ThinkOnErpDbContext> options)
    : base(options)
{
    // Disable change tracking by default for read-heavy applications
    ChangeTracker.QueryTrackingBehavior = QueryTrackingBehavior.NoTracking;
}

// Enable tracking for specific queries
var company = await _context.Companies
    .AsTracking()
    .FirstOrDefaultAsync(c => c.RowId == companyId);
```

**When to use**: For read-heavy applications where most queries are read-only.


---

## LINQ Best Practices

### 1. Use AsNoTracking for Read-Only Queries

```csharp
// Always use AsNoTracking for read-only queries
var companies = await _context.Companies
    .AsNoTracking()
    .ToListAsync();
```

**Benefit**: Reduces memory usage and improves performance by 30-50%.

### 2. Use Projection to Select Only Needed Data

```csharp
// Instead of loading entire entities
var companies = await _context.Companies
    .AsNoTracking()
    .Select(c => new CompanyDto
    {
        Id = c.RowId,
        Name = c.RowDesc,
        Code = c.CompanyCode
    })
    .ToListAsync();
```

**Benefit**: Reduces data transfer and memory usage.

### 3. Avoid Client-Side Evaluation

```csharp
// Bad: Client-side evaluation (downloads all data)
var companies = await _context.Companies
    .ToListAsync();
var filtered = companies.Where(c => c.RowDesc.Contains(searchTerm));

// Good: Server-side evaluation (filters in database)
var companies = await _context.Companies
    .Where(c => c.RowDesc.Contains(searchTerm))
    .ToListAsync();
```

**Benefit**: Reduces data transfer and improves performance.

### 4. Use Include for Related Data

```csharp
// Load related data in single query
var companies = await _context.Companies
    .Include(c => c.Currency)
    .Include(c => c.DefaultBranch)
    .ToListAsync();
```

**Benefit**: Avoids N+1 query problem.

### 5. Use AsSplitQuery for Multiple Collections

```csharp
// Avoid cartesian explosion with split queries
var companies = await _context.Companies
    .AsSplitQuery()
    .Include(c => c.Branches)
    .Include(c => c.Users)
    .ToListAsync();
```

**Benefit**: Prevents cartesian explosion in joins.

### 6. Use Compiled Queries for Frequently Executed Queries

```csharp
// Define once, use many times
public static readonly Func<ThinkOnErpDbContext, long, Task<SysCompany?>> 
    GetCompanyById = EF.CompileAsyncQuery(
        (ThinkOnErpDbContext context, long id) => 
            context.Companies.FirstOrDefault(c => c.RowId == id));
```

**Benefit**: Improves performance by caching query plan.

### 7. Use Any() Instead of Count() for Existence Checks

```csharp
// Bad: Counts all records
if (await _context.Companies.CountAsync() > 0)

// Good: Stops at first match
if (await _context.Companies.AnyAsync())
```

**Benefit**: Faster execution for existence checks.

### 8. Use FirstOrDefaultAsync Instead of SingleOrDefaultAsync

```csharp
// Use FirstOrDefaultAsync when you expect 0 or 1 result
var company = await _context.Companies
    .FirstOrDefaultAsync(c => c.RowId == companyId);

// Use SingleOrDefaultAsync only when you need to verify uniqueness
var company = await _context.Companies
    .SingleOrDefaultAsync(c => c.CompanyCode == code);
```

**Benefit**: FirstOrDefaultAsync is faster as it stops at first match.

### 9. Batch Operations with AddRange/UpdateRange/RemoveRange

```csharp
// Batch insert
_context.Companies.AddRange(companies);
await _context.SaveChangesAsync();

// Batch update
_context.Companies.UpdateRange(companies);
await _context.SaveChangesAsync();

// Batch delete
_context.Companies.RemoveRange(companies);
await _context.SaveChangesAsync();
```

**Benefit**: Reduces database round trips.

### 10. Use Pagination for Large Result Sets

```csharp
// Always paginate large result sets
var companies = await _context.Companies
    .OrderBy(c => c.RowDesc)
    .Skip((pageNumber - 1) * pageSize)
    .Take(pageSize)
    .ToListAsync();
```

**Benefit**: Prevents memory issues and improves response time.

### 11. Avoid Loading Unnecessary Navigation Properties

```csharp
// Bad: Loads all navigation properties
var company = await _context.Companies
    .Include(c => c.Branches)
        .ThenInclude(b => b.Users)
            .ThenInclude(u => u.Roles)
    .FirstOrDefaultAsync(c => c.RowId == companyId);

// Good: Load only what you need
var company = await _context.Companies
    .Include(c => c.Currency)
    .FirstOrDefaultAsync(c => c.RowId == companyId);
```

**Benefit**: Reduces data transfer and memory usage.

### 12. Use Explicit Loading for Conditional Related Data

```csharp
// Load entity first
var company = await _context.Companies
    .FirstOrDefaultAsync(c => c.RowId == companyId);

// Load related data conditionally
if (includeBranches)
{
    await _context.Entry(company)
        .Collection(c => c.Branches)
        .LoadAsync();
}
```

**Benefit**: Loads related data only when needed.

---

## Feature Flag Usage for Gradual Migration

The migration uses feature flags to enable gradual repository migration with rollback capability.

### Configuration

Add feature flags to `appsettings.json`:

```json
{
  "UseEfCore": {
    "CompanyRepository": true,
    "BranchRepository": true,
    "UserRepository": true,
    "RoleRepository": true,
    "FiscalYearRepository": true,
    "CurrencyRepository": true,
    "TicketStatusRepository": true,
    "TicketPriorityRepository": true,
    "TicketTypeRepository": true,
    "TicketRepository": true,
    "TicketCommentRepository": true,
    "TicketAttachmentRepository": true,
    "TicketConfigRepository": true,
    "TicketCategoryRepository": true,
    "RoleScreenPermissionRepository": true,
    "UserScreenPermissionRepository": true,
    "UserRoleRepository": true,
    "ScreenRepository": true,
    "SystemRepository": true,
    "CompanySystemRepository": true,
    "SuperAdminRepository": true,
    "AuditLogRepository": true,
    "SavedSearchRepository": true,
    "SearchAnalyticsRepository": true
  }
}
```

### Dependency Injection Configuration

In `src/ThinkOnErp.Infrastructure/DependencyInjection.cs`:

```csharp
public static IServiceCollection AddInfrastructure(
    this IServiceCollection services,
    IConfiguration configuration)
{
    // Register EF Core DbContext
    services.AddDbContext<ThinkOnErpDbContext>(options =>
    {
        options.UseOracle(
            configuration.GetConnectionString("OracleDb"),
            oracleOptions =>
            {
                oracleOptions.UseOracleSQLCompatibility("11");
                oracleOptions.CommandTimeout(30);
            })
            .EnableSensitiveDataLogging(isDevelopment)
            .EnableDetailedErrors(isDevelopment);
    });

    // Conditional repository registration based on feature flags
    RegisterRepository<ICompanyRepository, 
        EfCore.CompanyRepository, 
        AdoNet.CompanyRepository>(
            services, configuration, "CompanyRepository");

    RegisterRepository<IBranchRepository, 
        EfCore.BranchRepository, 
        AdoNet.BranchRepository>(
            services, configuration, "BranchRepository");

    // ... repeat for all 23 repositories

    return services;
}

private static void RegisterRepository<TInterface, TEfCoreImpl, TAdoNetImpl>(
    IServiceCollection services,
    IConfiguration configuration,
    string repositoryName)
    where TInterface : class
    where TEfCoreImpl : class, TInterface
    where TAdoNetImpl : class, TInterface
{
    var useEfCore = configuration.GetValue<bool>($"UseEfCore:{repositoryName}");
    
    if (useEfCore)
    {
        services.AddScoped<TInterface, TEfCoreImpl>();
    }
    else
    {
        services.AddScoped<TInterface, TAdoNetImpl>();
    }
}
```

### Migration Waves

**Wave 1: Pilot Repositories (Week 3-4)**
```json
{
  "UseEfCore": {
    "CurrencyRepository": true,
    "TicketStatusRepository": true,
    "TicketPriorityRepository": true
  }
}
```

**Wave 2: Core Repositories (Week 5-7)**
```json
{
  "UseEfCore": {
    "CompanyRepository": true,
    "BranchRepository": true,
    "UserRepository": true,
    "RoleRepository": true,
    "FiscalYearRepository": true
  }
}
```

**Wave 3: Permission Repositories (Week 8-9)**
```json
{
  "UseEfCore": {
    "RoleScreenPermissionRepository": true,
    "UserScreenPermissionRepository": true,
    "UserRoleRepository": true,
    "ScreenRepository": true,
    "SystemRepository": true,
    "CompanySystemRepository": true
  }
}
```

**Wave 4: Ticket Repositories (Week 10-12)**
```json
{
  "UseEfCore": {
    "TicketRepository": true,
    "TicketTypeRepository": true,
    "TicketCategoryRepository": true,
    "TicketConfigRepository": true,
    "TicketCommentRepository": true,
    "TicketAttachmentRepository": true
  }
}
```

**Wave 5: Admin & Audit Repositories (Week 13-14)**
```json
{
  "UseEfCore": {
    "SuperAdminRepository": true,
    "AuditLogRepository": true,
    "SavedSearchRepository": true,
    "SearchAnalyticsRepository": true
  }
}
```

### Monitoring During Migration

1. **Enable EF Core Logging**:
```json
{
  "Logging": {
    "LogLevel": {
      "Microsoft.EntityFrameworkCore.Database.Command": "Information"
    }
  }
}
```

2. **Monitor Performance Metrics**:
   - Query execution time
   - Memory usage
   - Connection pool usage
   - Error rates

3. **Compare with Baseline**:
   - Run comparison tests between ADO.NET and EF Core
   - Verify identical results
   - Measure performance differences

---

## Rollback Procedure

If issues occur during migration, follow this rollback procedure:

### Immediate Rollback (During Deployment)

1. **Disable Feature Flag**:
```json
{
  "UseEfCore": {
    "CompanyRepository": false
  }
}
```

2. **Restart Application**:
```bash
# For Docker deployment
docker-compose restart

# For IIS deployment
iisreset
```

3. **Verify Rollback**:
   - Check application logs
   - Test affected endpoints
   - Monitor error rates

### Partial Rollback (Single Repository)

```json
{
  "UseEfCore": {
    "CompanyRepository": false,  // Rollback this one
    "BranchRepository": true,    // Keep this one
    "UserRepository": true       // Keep this one
  }
}
```

### Full Rollback (All Repositories)

```json
{
  "UseEfCore": {
    "CompanyRepository": false,
    "BranchRepository": false,
    "UserRepository": false,
    // ... set all to false
  }
}
```

### Rollback Checklist

- [ ] Identify the problematic repository
- [ ] Set feature flag to `false` in `appsettings.json`
- [ ] Restart the application
- [ ] Verify the application is using ADO.NET implementation
- [ ] Check application logs for errors
- [ ] Test affected API endpoints
- [ ] Monitor error rates for 30 minutes
- [ ] Document the issue for investigation
- [ ] Create bug report with:
  - Repository name
  - Error messages
  - Query that failed
  - Performance metrics
  - Steps to reproduce

### Rollback Time Estimate

- **Configuration change**: 1 minute
- **Application restart**: 2-5 minutes
- **Verification**: 10-15 minutes
- **Total**: 15-20 minutes


---

## CRUD Operation Examples

### Create (Insert)

#### Simple Create

```csharp
public async Task<long> CreateAsync(SysCompany company)
{
    _logger.LogDebug("Creating new company: {RowDesc}", company.RowDesc);

    // Set default values
    company.CreationDate = DateTime.Now;
    company.IsActive = true;

    // Add entity to context
    _context.Companies.Add(company);

    // Save changes - EF Core retrieves generated ID from sequence
    await _context.SaveChangesAsync();

    _logger.LogInformation("Created company with ID: {RowId}", company.RowId);

    return company.RowId;
}
```

#### Batch Create

```csharp
public async Task<int> CreateBatchAsync(List<SysCompany> companies)
{
    _logger.LogDebug("Creating {Count} companies", companies.Count);

    // Set default values for all
    foreach (var company in companies)
    {
        company.CreationDate = DateTime.Now;
        company.IsActive = true;
    }

    // Add all entities at once
    _context.Companies.AddRange(companies);

    // Single SaveChanges for all
    var rowsAffected = await _context.SaveChangesAsync();

    _logger.LogInformation("Created {Count} companies", rowsAffected);

    return rowsAffected;
}
```

#### Create with Related Entities

```csharp
public async Task<(long CompanyId, long BranchId)> CreateWithBranchAsync(
    SysCompany company,
    SysBranch branch)
{
    using var transaction = await _context.Database.BeginTransactionAsync();

    try
    {
        // Create company
        company.CreationDate = DateTime.Now;
        company.IsActive = true;
        _context.Companies.Add(company);
        await _context.SaveChangesAsync();

        // Create branch with company ID
        branch.ParRowId = company.RowId;
        branch.CreationDate = DateTime.Now;
        branch.IsActive = true;
        _context.Branches.Add(branch);
        await _context.SaveChangesAsync();

        // Update company with default branch
        company.DefaultBranchId = branch.RowId;
        await _context.SaveChangesAsync();

        await transaction.CommitAsync();

        return (company.RowId, branch.RowId);
    }
    catch (Exception ex)
    {
        await transaction.RollbackAsync();
        _logger.LogError(ex, "Failed to create company with branch");
        throw;
    }
}
```

### Read (Select)

#### Get All

```csharp
public async Task<List<SysCompany>> GetAllAsync()
{
    return await _context.Companies
        .AsNoTracking()
        .Include(c => c.Currency)
        .Include(c => c.DefaultBranch)
        .OrderBy(c => c.RowDesc)
        .ToListAsync();
}
```

#### Get By ID

```csharp
public async Task<SysCompany?> GetByIdAsync(long rowId)
{
    return await _context.Companies
        .AsNoTracking()
        .Include(c => c.Currency)
        .Include(c => c.DefaultBranch)
        .FirstOrDefaultAsync(c => c.RowId == rowId);
}
```

#### Get with Filtering

```csharp
public async Task<List<SysCompany>> GetByCountryAsync(long countryId)
{
    return await _context.Companies
        .AsNoTracking()
        .Where(c => c.CountryId == countryId)
        .OrderBy(c => c.RowDesc)
        .ToListAsync();
}
```

#### Get with Pagination

```csharp
public async Task<(List<SysCompany> Companies, int TotalCount)> GetPagedAsync(
    int pageNumber,
    int pageSize,
    string? searchTerm = null)
{
    var query = _context.Companies.AsNoTracking();

    // Apply search filter if provided
    if (!string.IsNullOrWhiteSpace(searchTerm))
    {
        query = query.Where(c => 
            c.RowDesc.Contains(searchTerm) || 
            c.RowDescE.Contains(searchTerm) ||
            c.CompanyCode.Contains(searchTerm));
    }

    // Get total count
    var totalCount = await query.CountAsync();

    // Get paged results
    var companies = await query
        .OrderBy(c => c.RowDesc)
        .Skip((pageNumber - 1) * pageSize)
        .Take(pageSize)
        .ToListAsync();

    return (companies, totalCount);
}
```

#### Get with Projection

```csharp
public async Task<List<CompanyDto>> GetCompanyNamesAsync()
{
    return await _context.Companies
        .AsNoTracking()
        .Select(c => new CompanyDto
        {
            Id = c.RowId,
            Name = c.RowDesc,
            NameEnglish = c.RowDescE,
            Code = c.CompanyCode
        })
        .OrderBy(c => c.Name)
        .ToListAsync();
}
```

### Update

#### Simple Update

```csharp
public async Task<long> UpdateAsync(SysCompany company)
{
    _logger.LogDebug("Updating company with ID: {RowId}", company.RowId);

    // Set update date
    company.UpdateDate = DateTime.Now;

    // Update entity - EF Core tracks changes
    _context.Companies.Update(company);

    // Save changes
    var rowsAffected = await _context.SaveChangesAsync();

    _logger.LogInformation("Updated company with ID: {RowId}", company.RowId);

    return rowsAffected;
}
```

#### Partial Update (Specific Properties)

```csharp
public async Task<long> UpdateNameAsync(long rowId, string name, string nameEnglish)
{
    // Load entity with tracking
    var company = await _context.Companies
        .FirstOrDefaultAsync(c => c.RowId == rowId);

    if (company == null)
    {
        return 0;
    }

    // Update specific properties
    company.RowDesc = name;
    company.RowDescE = nameEnglish;
    company.UpdateDate = DateTime.Now;

    // Save changes - EF Core detects changes automatically
    var rowsAffected = await _context.SaveChangesAsync();

    return rowsAffected;
}
```

#### Batch Update

```csharp
public async Task<int> UpdateBatchAsync(List<SysCompany> companies)
{
    _logger.LogDebug("Updating {Count} companies", companies.Count);

    // Set update date for all
    foreach (var company in companies)
    {
        company.UpdateDate = DateTime.Now;
    }

    // Update all entities
    _context.Companies.UpdateRange(companies);

    // Single SaveChanges for all
    var rowsAffected = await _context.SaveChangesAsync();

    _logger.LogInformation("Updated {Count} companies", rowsAffected);

    return rowsAffected;
}
```

#### Update with Concurrency Check

```csharp
public async Task<long> UpdateWithConcurrencyCheckAsync(SysCompany company)
{
    try
    {
        company.UpdateDate = DateTime.Now;
        _context.Companies.Update(company);
        var rowsAffected = await _context.SaveChangesAsync();
        return rowsAffected;
    }
    catch (DbUpdateConcurrencyException ex)
    {
        _logger.LogWarning(ex, "Concurrency conflict updating company {RowId}", company.RowId);
        throw new InvalidOperationException(
            "The company was modified by another user. Please refresh and try again.", ex);
    }
}
```

### Delete

#### Soft Delete

```csharp
public async Task<long> DeleteAsync(long rowId)
{
    _logger.LogDebug("Soft deleting company with ID: {RowId}", rowId);

    // Load entity (need to ignore query filter to find it even if already deleted)
    var company = await _context.Companies
        .IgnoreQueryFilters()
        .FirstOrDefaultAsync(c => c.RowId == rowId);

    if (company == null)
    {
        _logger.LogWarning("Company with ID {RowId} not found", rowId);
        return 0;
    }

    // Perform soft delete
    company.IsActive = false;
    company.UpdateDate = DateTime.Now;

    var rowsAffected = await _context.SaveChangesAsync();

    _logger.LogInformation("Soft deleted company with ID: {RowId}", rowId);

    return rowsAffected;
}
```

#### Hard Delete

```csharp
public async Task<long> HardDeleteAsync(long rowId)
{
    _logger.LogDebug("Hard deleting company with ID: {RowId}", rowId);

    // Load entity
    var company = await _context.Companies
        .IgnoreQueryFilters()
        .FirstOrDefaultAsync(c => c.RowId == rowId);

    if (company == null)
    {
        _logger.LogWarning("Company with ID {RowId} not found", rowId);
        return 0;
    }

    // Remove entity
    _context.Companies.Remove(company);

    var rowsAffected = await _context.SaveChangesAsync();

    _logger.LogInformation("Hard deleted company with ID: {RowId}", rowId);

    return rowsAffected;
}
```

#### Batch Delete

```csharp
public async Task<int> DeleteBatchAsync(List<long> rowIds)
{
    _logger.LogDebug("Soft deleting {Count} companies", rowIds.Count);

    // Load entities
    var companies = await _context.Companies
        .Where(c => rowIds.Contains(c.RowId))
        .ToListAsync();

    // Perform soft delete on all
    foreach (var company in companies)
    {
        company.IsActive = false;
        company.UpdateDate = DateTime.Now;
    }

    var rowsAffected = await _context.SaveChangesAsync();

    _logger.LogInformation("Soft deleted {Count} companies", rowsAffected);

    return rowsAffected;
}
```

### Special Operations

#### Update BLOB (Logo)

```csharp
public async Task<long> UpdateLogoAsync(long rowId, byte[] logo, string userName)
{
    _logger.LogDebug("Updating logo for company with ID: {RowId}", rowId);

    // Load entity
    var company = await _context.Companies
        .FirstOrDefaultAsync(c => c.RowId == rowId);

    if (company == null)
    {
        return 0;
    }

    // Update logo
    company.CompanyLogo = logo;
    company.UpdateUser = userName;
    company.UpdateDate = DateTime.Now;

    var rowsAffected = await _context.SaveChangesAsync();

    _logger.LogInformation("Updated logo for company {RowId}, Size: {Size} bytes", 
        rowId, logo?.Length ?? 0);

    return rowsAffected;
}
```

#### Get BLOB (Logo)

```csharp
public async Task<byte[]?> GetLogoAsync(long rowId)
{
    // Use projection to load only logo column
    var logo = await _context.Companies
        .AsNoTracking()
        .Where(c => c.RowId == rowId)
        .Select(c => c.CompanyLogo)
        .FirstOrDefaultAsync();

    return logo;
}
```

#### Existence Check

```csharp
public async Task<bool> ExistsAsync(long rowId)
{
    return await _context.Companies
        .AnyAsync(c => c.RowId == rowId);
}

public async Task<bool> ExistsByCodeAsync(string companyCode)
{
    return await _context.Companies
        .AnyAsync(c => c.CompanyCode == companyCode);
}
```

#### Count

```csharp
public async Task<int> GetCountAsync()
{
    return await _context.Companies.CountAsync();
}

public async Task<int> GetActiveCountAsync()
{
    return await _context.Companies
        .CountAsync(c => c.IsActive);
}
```

---

## Troubleshooting Guide

### Common Issues and Solutions

#### 1. "Sequence contains no elements" Exception

**Problem**: Using `First()` or `Single()` on empty result set.

**Solution**: Use `FirstOrDefault()` or `SingleOrDefault()` instead.

```csharp
// Bad
var company = await _context.Companies.FirstAsync(c => c.RowId == id);

// Good
var company = await _context.Companies.FirstOrDefaultAsync(c => c.RowId == id);
if (company == null)
{
    throw new NotFoundException($"Company with ID {id} not found");
}
```

#### 2. "The instance of entity type cannot be tracked" Exception

**Problem**: Attempting to track multiple entities with the same key.

**Solution**: Use `AsNoTracking()` for read-only queries or detach existing entity.

```csharp
// Solution 1: Use AsNoTracking
var company = await _context.Companies
    .AsNoTracking()
    .FirstOrDefaultAsync(c => c.RowId == id);

// Solution 2: Detach existing entity
var existingEntry = _context.ChangeTracker.Entries<SysCompany>()
    .FirstOrDefault(e => e.Entity.RowId == company.RowId);
if (existingEntry != null)
{
    existingEntry.State = EntityState.Detached;
}
```

#### 3. N+1 Query Problem

**Problem**: Loading related entities in a loop causes multiple queries.

**Solution**: Use `Include()` to load related data in single query.

```csharp
// Bad: N+1 queries
var companies = await _context.Companies.ToListAsync();
foreach (var company in companies)
{
    var branches = await _context.Branches
        .Where(b => b.ParRowId == company.RowId)
        .ToListAsync();
}

// Good: Single query
var companies = await _context.Companies
    .Include(c => c.Branches)
    .ToListAsync();
```

#### 4. "A second operation started on this context" Exception

**Problem**: Attempting concurrent operations on same DbContext instance.

**Solution**: DbContext is not thread-safe. Use separate instances or await operations.

```csharp
// Bad: Concurrent operations
var task1 = _context.Companies.ToListAsync();
var task2 = _context.Branches.ToListAsync();
await Task.WhenAll(task1, task2);

// Good: Sequential operations
var companies = await _context.Companies.ToListAsync();
var branches = await _context.Branches.ToListAsync();

// Or use separate DbContext instances for parallel operations
```

#### 5. "Oracle.ManagedDataAccess.Client.OracleException: ORA-00001: unique constraint violated"

**Problem**: Attempting to insert duplicate value in unique column.

**Solution**: Check for existing record before insert or handle exception.

```csharp
try
{
    _context.Companies.Add(company);
    await _context.SaveChangesAsync();
}
catch (DbUpdateException ex) when (ex.InnerException is OracleException oracleEx 
    && oracleEx.Number == 1)
{
    throw new InvalidOperationException(
        $"Company code '{company.CompanyCode}' already exists.", ex);
}
```

#### 6. "The property 'RowId' cannot be modified" Exception

**Problem**: Attempting to modify primary key value.

**Solution**: Don't modify primary key after entity is tracked.

```csharp
// Bad
company.RowId = newId;

// Good: Create new entity with new ID
var newCompany = new SysCompany
{
    RowDesc = company.RowDesc,
    // ... copy other properties
};
_context.Companies.Add(newCompany);
```

#### 7. Slow Query Performance

**Problem**: Query takes too long to execute.

**Solutions**:

```csharp
// 1. Use AsNoTracking for read-only queries
var companies = await _context.Companies
    .AsNoTracking()
    .ToListAsync();

// 2. Use projection to select only needed columns
var companyNames = await _context.Companies
    .Select(c => new { c.RowId, c.RowDesc })
    .ToListAsync();

// 3. Add pagination
var companies = await _context.Companies
    .Skip((pageNumber - 1) * pageSize)
    .Take(pageSize)
    .ToListAsync();

// 4. Use compiled queries for frequently executed queries
var companies = await CompiledQueries.GetAllCompanies(_context).ToListAsync();

// 5. Add database indexes (see EF_CORE_INDEX_RECOMMENDATIONS.md)
```

#### 8. "DbUpdateConcurrencyException" Exception

**Problem**: Entity was modified by another user.

**Solution**: Implement concurrency handling strategy.

```csharp
try
{
    await _context.SaveChangesAsync();
}
catch (DbUpdateConcurrencyException ex)
{
    // Option 1: Client wins (overwrite database values)
    var entry = ex.Entries.Single();
    entry.OriginalValues.SetValues(entry.GetDatabaseValues());
    await _context.SaveChangesAsync();

    // Option 2: Database wins (reload from database)
    var entry = ex.Entries.Single();
    await entry.ReloadAsync();

    // Option 3: Merge (custom logic)
    // Implement custom merge logic based on business rules
}
```

#### 9. "Connection pool exhausted" Exception

**Problem**: Too many concurrent database connections.

**Solution**: Ensure DbContext is properly disposed and configure connection pool.

```csharp
// Use dependency injection (automatic disposal)
public class CompanyRepository : ICompanyRepository
{
    private readonly ThinkOnErpDbContext _context;
    
    public CompanyRepository(ThinkOnErpDbContext context)
    {
        _context = context;
    }
}

// Configure connection pool in Program.cs
services.AddDbContext<ThinkOnErpDbContext>(options =>
{
    options.UseOracle(connectionString, oracleOptions =>
    {
        oracleOptions.CommandTimeout(30);
        // Connection pooling is enabled by default
    });
});
```

#### 10. "Global query filter not applied" Issue

**Problem**: Soft-deleted records are returned in queries.

**Solution**: Ensure global query filter is configured and not bypassed.

```csharp
// Configure global query filter in entity configuration
builder.HasQueryFilter(e => e.IsActive);

// Query automatically filters soft-deleted records
var companies = await _context.Companies.ToListAsync();

// To include soft-deleted records, explicitly bypass filter
var allCompanies = await _context.Companies
    .IgnoreQueryFilters()
    .ToListAsync();
```

### Debugging Tips

#### Enable SQL Logging

```json
{
  "Logging": {
    "LogLevel": {
      "Microsoft.EntityFrameworkCore.Database.Command": "Information",
      "Microsoft.EntityFrameworkCore.Query": "Information"
    }
  }
}
```

#### Use EF Core Query Tags

```csharp
var companies = await _context.Companies
    .TagWith("GetAllCompanies - CompanyRepository")
    .ToListAsync();
```

#### Inspect Generated SQL

```csharp
var query = _context.Companies
    .Where(c => c.IsActive)
    .OrderBy(c => c.RowDesc);

var sql = query.ToQueryString();
_logger.LogDebug("Generated SQL: {Sql}", sql);
```

#### Check Change Tracker State

```csharp
var entries = _context.ChangeTracker.Entries<SysCompany>();
foreach (var entry in entries)
{
    _logger.LogDebug("Entity {Id} State: {State}", 
        entry.Entity.RowId, entry.State);
}
```

### Performance Monitoring

#### Query Execution Time

```csharp
var stopwatch = Stopwatch.StartNew();
var companies = await _context.Companies.ToListAsync();
stopwatch.Stop();

_logger.LogInformation("Query executed in {ElapsedMs}ms", stopwatch.ElapsedMilliseconds);
```

#### Connection Pool Metrics

Monitor connection pool usage through Oracle performance views:

```sql
SELECT * FROM V$SESSION WHERE PROGRAM LIKE '%ThinkOnErp%';
SELECT * FROM V$PROCESS WHERE PROGRAM LIKE '%ThinkOnErp%';
```

---

## Additional Resources

- **EF Core Documentation**: https://docs.microsoft.com/en-us/ef/core/
- **Oracle EF Core Provider**: https://www.oracle.com/database/technologies/appdev/dotnet/odp.html
- **Performance Optimization Guide**: `docs/EF_CORE_PERFORMANCE_OPTIMIZATION_GUIDE.md`
- **Index Recommendations**: `docs/EF_CORE_INDEX_RECOMMENDATIONS.md`
- **Pilot Migration Report**: `docs/EF_CORE_PILOT_MIGRATION.md`

---

## Summary

This guide covers the complete EF Core migration approach for ThinkOnErp:

✅ **Pure EF Core approach** with LINQ queries (no stored procedures)  
✅ **Common LINQ patterns** for filtering, sorting, pagination, and eager loading  
✅ **Entity configurations** for table mappings and relationships  
✅ **Transaction management** with `BeginTransactionAsync()`  
✅ **Performance optimization** techniques (AsNoTracking, compiled queries, projection)  
✅ **LINQ best practices** to avoid common pitfalls  
✅ **Feature flags** for gradual migration with rollback capability  
✅ **CRUD operation examples** for all common scenarios  
✅ **Troubleshooting guide** for common issues and solutions  

For questions or issues, contact the development team or refer to the additional resources listed above.
