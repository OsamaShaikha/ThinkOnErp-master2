# Requirements Document

## Introduction

This document specifies the requirements for migrating the ThinkOnErp ERP system from ADO.NET to Entity Framework Core (EF Core). The system is an ASP.NET Core 8.0 application using Clean Architecture with an Oracle database backend. Currently, all 23 repositories use ADO.NET with OracleConnection/OracleCommand to execute stored procedures (SP_SYS_*). The migration will replace ADO.NET with EF Core while maintaining existing stored procedure calls, preserving Clean Architecture principles, and ensuring backward compatibility with existing API contracts.

## Glossary

- **EF_Core_DbContext**: The Entity Framework Core DbContext class that manages database connections and entity tracking
- **Repository**: A class implementing the repository pattern that provides data access methods for a specific entity
- **Stored_Procedure**: A precompiled SQL routine stored in the Oracle database (prefixed with SP_SYS_*)
- **Entity**: A domain model class representing a database table
- **Entity_Configuration**: A class implementing IEntityTypeConfiguration that defines entity mappings and relationships
- **Migration_Strategy**: The approach used to transition from ADO.NET to EF Core (gradual or big bang)
- **OracleDbContext**: The current ADO.NET connection factory class that creates OracleConnection instances
- **Clean_Architecture**: An architectural pattern separating concerns into Domain, Application, Infrastructure, and API layers
- **API_Contract**: The public interface of API endpoints including request/response models and HTTP methods
- **Connection_String**: The Oracle database connection configuration string
- **FromSqlRaw**: EF Core method for executing raw SQL queries and stored procedures that return entities
- **ExecuteSqlRaw**: EF Core method for executing raw SQL commands and stored procedures that don't return entities
- **DbSet**: EF Core collection property representing a table in the database
- **Transaction**: A unit of work that ensures data consistency across multiple database operations
- **Audit_Interceptor**: A component that intercepts database commands for audit logging purposes
- **Repository_Interface**: The contract (ICompanyRepository, IUserRepository, etc.) defined in the Domain layer
- **Zero_Downtime_Deployment**: A deployment strategy that allows the application to remain available during updates

## Requirements

### Requirement 1: EF Core Configuration and Setup

**User Story:** As a developer, I want to configure Entity Framework Core for Oracle database, so that the application can use EF Core for data access.

#### Acceptance Criteria

1. THE EF_Core_DbContext SHALL inherit from Microsoft.EntityFrameworkCore.DbContext
2. THE EF_Core_DbContext SHALL use the same Oracle connection string as the current OracleDbContext
3. THE EF_Core_DbContext SHALL configure Oracle.EntityFrameworkCore as the database provider
4. THE EF_Core_DbContext SHALL include DbSet properties for all 23 entity types (Company, Branch, User, Role, Permission, Currency, FiscalYear, SuperAdmin, Audit, Screen, System, Ticket, TicketType, TicketStatus, TicketPriority, TicketComment, TicketAttachment, TicketConfig, Alert, SavedSearch, SearchAnalytics, SlowQuery, Auth)
5. THE EF_Core_DbContext SHALL override OnModelCreating to apply entity configurations
6. THE EF_Core_DbContext SHALL support dependency injection through constructor parameters
7. THE EF_Core_DbContext SHALL maintain compatibility with the existing AuditCommandInterceptor

### Requirement 2: Entity Configuration and Mapping

**User Story:** As a developer, I want to define entity configurations for all database tables, so that EF Core can correctly map entities to Oracle tables.

#### Acceptance Criteria

1. FOR ALL entities, THE Entity_Configuration SHALL implement IEntityTypeConfiguration<TEntity>
2. THE Entity_Configuration SHALL map entity properties to Oracle column names using HasColumnName
3. THE Entity_Configuration SHALL specify primary keys using HasKey
4. THE Entity_Configuration SHALL define foreign key relationships using HasOne/HasMany/WithMany
5. THE Entity_Configuration SHALL configure required and optional properties using IsRequired
6. THE Entity_Configuration SHALL specify maximum lengths for string properties using HasMaxLength
7. THE Entity_Configuration SHALL map Oracle NUMBER types to C# long or decimal types
8. THE Entity_Configuration SHALL map Oracle VARCHAR2 types to C# string types
9. THE Entity_Configuration SHALL map Oracle DATE types to C# DateTime types
10. THE Entity_Configuration SHALL map Oracle BLOB types to C# byte[] types
11. THE Entity_Configuration SHALL configure Oracle sequences for primary key generation using HasDefaultValueSql
12. THE Entity_Configuration SHALL preserve existing table names without modification

### Requirement 3: Stored Procedure Integration

**User Story:** As a developer, I want to execute existing Oracle stored procedures through EF Core, so that I can maintain compatibility with the current database layer.

#### Acceptance Criteria

1. WHEN a stored procedure returns entities, THE Repository SHALL use FromSqlRaw to execute the procedure and map results to entities
2. WHEN a stored procedure performs insert/update/delete operations, THE Repository SHALL use ExecuteSqlRaw to execute the procedure
3. WHEN a stored procedure has input parameters, THE Repository SHALL pass parameters using OracleParameter objects
4. WHEN a stored procedure has output parameters, THE Repository SHALL retrieve output values after execution
5. WHEN a stored procedure returns a REF CURSOR, THE Repository SHALL map the cursor results to entity collections
6. THE Repository SHALL handle OracleException types and convert them to domain-specific exceptions
7. THE Repository SHALL preserve all existing stored procedure calls (SP_SYS_COMPANY_*, SP_SYS_USERS_*, SP_SYS_BRANCH_*, etc.)
8. FOR ALL stored procedures with P_RESULT_CURSOR output parameters, THE Repository SHALL correctly map cursor data to entities

### Requirement 4: Repository Implementation Migration

**User Story:** As a developer, I want to migrate all 23 repositories from ADO.NET to EF Core, so that the application uses a consistent data access approach.

#### Acceptance Criteria

1. THE Repository SHALL accept EF_Core_DbContext through constructor dependency injection instead of OracleDbContext
2. THE Repository SHALL implement the same Repository_Interface as the current ADO.NET implementation
3. THE Repository SHALL maintain all existing public methods with identical signatures
4. THE Repository SHALL use EF Core methods (FromSqlRaw, ExecuteSqlRaw, SaveChangesAsync) instead of OracleConnection/OracleCommand
5. THE Repository SHALL preserve all existing error handling and exception mapping logic
6. THE Repository SHALL maintain the same return types for all methods
7. FOR ALL 23 repositories (CompanyRepository, BranchRepository, UserRepository, RoleRepository, PermissionRepository, CurrencyRepository, FiscalYearRepository, SuperAdminRepository, AuthRepository, AuditRepository, ScreenRepository, SystemRepository, TicketRepository, TicketTypeRepository, TicketStatusRepository, TicketPriorityRepository, TicketCommentRepository, TicketAttachmentRepository, TicketConfigRepository, AlertRepository, SavedSearchRepository, SearchAnalyticsRepository, SlowQueryRepository), THE migration SHALL be completed
8. THE Repository SHALL use async/await patterns consistently with existing implementations

### Requirement 5: LINQ Query Support

**User Story:** As a developer, I want to use LINQ queries for simple data access operations, so that I can write more maintainable and type-safe code.

#### Acceptance Criteria

1. WHEN a query involves simple filtering by ID or status, THE Repository SHALL use LINQ queries instead of stored procedures
2. WHEN a query involves complex business logic, THE Repository SHALL continue using stored procedures
3. THE Repository SHALL use IQueryable<T> for composable queries
4. THE Repository SHALL use AsNoTracking for read-only queries to improve performance
5. THE Repository SHALL use Include/ThenInclude for eager loading related entities when needed
6. THE Repository SHALL apply Where, OrderBy, Select, and other LINQ operators as appropriate
7. THE Repository SHALL ensure LINQ queries generate efficient SQL that performs comparably to stored procedures

### Requirement 6: Transaction Management

**User Story:** As a developer, I want to manage database transactions through EF Core, so that I can ensure data consistency across multiple operations.

#### Acceptance Criteria

1. WHEN multiple database operations must succeed or fail together, THE Repository SHALL use Database.BeginTransactionAsync
2. THE Repository SHALL commit transactions using CommitAsync after successful operations
3. THE Repository SHALL rollback transactions using RollbackAsync when errors occur
4. THE Repository SHALL dispose transactions properly using using statements or try-finally blocks
5. THE EF_Core_DbContext SHALL support nested transactions through savepoints
6. THE Repository SHALL maintain existing transactional behavior from stored procedures
7. WHEN a stored procedure manages its own transaction, THE Repository SHALL not create an additional transaction wrapper

### Requirement 7: Connection String Compatibility

**User Story:** As a developer, I want to use the same Oracle connection string for EF Core, so that I don't need to modify configuration files.

#### Acceptance Criteria

1. THE EF_Core_DbContext SHALL read the connection string from IConfiguration using the key "OracleDb"
2. THE connection string format SHALL remain unchanged from the current ADO.NET implementation
3. THE EF_Core_DbContext SHALL support all Oracle connection string parameters used by the current system
4. WHEN the connection string is missing, THE EF_Core_DbContext SHALL throw InvalidOperationException with a descriptive message
5. THE EF_Core_DbContext SHALL validate the connection string format during initialization

### Requirement 8: Migration Strategy and Rollback Plan

**User Story:** As a project manager, I want a clear migration strategy with rollback capability, so that I can minimize risk during the transition.

#### Acceptance Criteria

1. THE Migration_Strategy SHALL support gradual migration where repositories are migrated one at a time
2. THE Migration_Strategy SHALL support big bang migration where all repositories are migrated simultaneously
3. THE system SHALL allow both OracleDbContext and EF_Core_DbContext to coexist during gradual migration
4. THE system SHALL provide a rollback mechanism to revert to ADO.NET implementations if issues occur
5. THE Migration_Strategy SHALL include a testing phase where both implementations run in parallel for validation
6. THE Migration_Strategy SHALL document the order of repository migration based on dependencies
7. THE Migration_Strategy SHALL identify high-risk repositories that require additional testing

### Requirement 9: Performance Optimization

**User Story:** As a developer, I want EF Core to perform comparably to ADO.NET, so that the migration doesn't degrade application performance.

#### Acceptance Criteria

1. THE EF_Core_DbContext SHALL enable connection pooling with appropriate pool size configuration
2. THE EF_Core_DbContext SHALL use compiled queries for frequently executed queries
3. THE Repository SHALL use AsNoTracking for read-only queries to reduce memory overhead
4. THE Repository SHALL use projection (Select) to retrieve only required columns when appropriate
5. THE EF_Core_DbContext SHALL configure query splitting strategy to avoid cartesian explosion in joins
6. THE Repository SHALL batch multiple insert/update operations when possible
7. THE system SHALL measure and compare query execution times between ADO.NET and EF Core implementations
8. WHEN EF Core performance is significantly worse than ADO.NET, THE Repository SHALL optimize the query or continue using stored procedures

### Requirement 10: Audit Logging Integration

**User Story:** As a developer, I want to maintain audit logging functionality with EF Core, so that all database operations continue to be tracked.

#### Acceptance Criteria

1. THE EF_Core_DbContext SHALL integrate with the existing AuditCommandInterceptor
2. THE EF_Core_DbContext SHALL support SaveChangesInterceptor for tracking entity changes
3. THE EF_Core_DbContext SHALL capture insert, update, and delete operations for audit logging
4. THE EF_Core_DbContext SHALL preserve correlation context and user information in audit logs
5. THE EF_Core_DbContext SHALL maintain compatibility with the existing AuditableOracleConnection wrapper
6. THE audit logging SHALL capture both LINQ queries and stored procedure executions
7. THE audit logging SHALL include execution time, affected rows, and parameter values

### Requirement 11: API Contract Preservation

**User Story:** As an API consumer, I want all existing API endpoints to work without changes, so that I don't need to update client applications.

#### Acceptance Criteria

1. THE API_Contract SHALL remain unchanged for all endpoints after migration
2. THE API response models SHALL maintain the same structure and property names
3. THE API request models SHALL accept the same parameters and validation rules
4. THE API error responses SHALL return the same HTTP status codes and error messages
5. THE API pagination, filtering, and sorting behavior SHALL remain consistent
6. THE API authentication and authorization SHALL work identically after migration
7. FOR ALL 23 entity types, THE API endpoints SHALL produce identical responses before and after migration

### Requirement 12: Testing Strategy

**User Story:** As a QA engineer, I want comprehensive tests for the EF Core migration, so that I can verify correctness and prevent regressions.

#### Acceptance Criteria

1. THE testing strategy SHALL include unit tests for each repository method
2. THE testing strategy SHALL include integration tests that verify database operations against a real Oracle database
3. THE testing strategy SHALL include comparison tests that verify EF Core produces the same results as ADO.NET
4. THE testing strategy SHALL include performance tests that measure query execution times
5. THE testing strategy SHALL include end-to-end API tests that verify complete request/response cycles
6. THE testing strategy SHALL achieve at least 80% code coverage for repository implementations
7. THE testing strategy SHALL include tests for error handling and exception scenarios
8. THE testing strategy SHALL verify stored procedure parameter mapping and output parameter retrieval
9. THE testing strategy SHALL test transaction rollback scenarios
10. THE testing strategy SHALL validate that all 23 repositories pass their test suites

### Requirement 13: Dependency Injection Configuration

**User Story:** As a developer, I want to configure EF Core through dependency injection, so that the application follows ASP.NET Core best practices.

#### Acceptance Criteria

1. THE DependencyInjection configuration SHALL register EF_Core_DbContext using AddDbContext
2. THE DependencyInjection configuration SHALL configure the DbContext lifetime as Scoped
3. THE DependencyInjection configuration SHALL register all 23 repositories with their interfaces
4. THE DependencyInjection configuration SHALL support switching between ADO.NET and EF Core implementations through configuration
5. THE DependencyInjection configuration SHALL register Oracle-specific services (OracleConnection pooling, etc.)
6. THE DependencyInjection configuration SHALL maintain compatibility with existing service registrations
7. THE DependencyInjection configuration SHALL allow both OracleDbContext and EF_Core_DbContext to be registered simultaneously during gradual migration

### Requirement 14: Error Handling and Exception Mapping

**User Story:** As a developer, I want consistent error handling across all repositories, so that exceptions are properly categorized and logged.

#### Acceptance Criteria

1. WHEN an Oracle constraint violation occurs, THE Repository SHALL throw a domain-specific exception with a meaningful message
2. WHEN a stored procedure raises an application error (ORA-20000 series), THE Repository SHALL extract and throw the custom error message
3. WHEN a connection failure occurs, THE Repository SHALL throw a DatabaseConnectionException
4. WHEN a timeout occurs, THE Repository SHALL throw a DatabaseTimeoutException
5. THE Repository SHALL preserve the original exception as InnerException for debugging
6. THE Repository SHALL log all database exceptions with appropriate severity levels
7. THE Repository SHALL handle DbUpdateException and map it to domain exceptions
8. THE Repository SHALL handle DbUpdateConcurrencyException for optimistic concurrency conflicts

### Requirement 15: Entity Tracking and Change Detection

**User Story:** As a developer, I want EF Core to track entity changes efficiently, so that updates are automatically detected and persisted.

#### Acceptance Criteria

1. THE EF_Core_DbContext SHALL enable change tracking for entities retrieved through LINQ queries
2. THE EF_Core_DbContext SHALL disable change tracking for read-only queries using AsNoTracking
3. THE Repository SHALL use Update method to mark entities as modified when using stored procedures
4. THE Repository SHALL use Attach method to attach detached entities before updating
5. THE EF_Core_DbContext SHALL detect changes to navigation properties and related entities
6. THE EF_Core_DbContext SHALL configure change tracking behavior through ChangeTracker.QueryTrackingBehavior
7. THE Repository SHALL call SaveChangesAsync to persist tracked changes to the database

### Requirement 16: Concurrency Control

**User Story:** As a developer, I want to handle concurrent updates to entities, so that data integrity is maintained in multi-user scenarios.

#### Acceptance Criteria

1. WHEN entities have timestamp or version columns, THE Entity_Configuration SHALL configure them as concurrency tokens using IsConcurrencyToken
2. WHEN a concurrency conflict occurs, THE Repository SHALL throw DbUpdateConcurrencyException
3. THE Repository SHALL provide retry logic for transient concurrency conflicts
4. THE Repository SHALL allow the application layer to decide conflict resolution strategy (client wins, server wins, merge)
5. THE Entity_Configuration SHALL map Oracle ROWVERSION or UPDATE_DATE columns as concurrency tokens where applicable

### Requirement 17: Lazy Loading and Eager Loading

**User Story:** As a developer, I want to control when related entities are loaded, so that I can optimize query performance.

#### Acceptance Criteria

1. THE EF_Core_DbContext SHALL disable lazy loading by default to prevent N+1 query problems
2. WHEN related entities are needed, THE Repository SHALL use Include to eagerly load them
3. WHEN multiple levels of relationships are needed, THE Repository SHALL use ThenInclude for nested eager loading
4. THE Repository SHALL use explicit loading with Load method when conditional loading is required
5. THE Entity_Configuration SHALL configure navigation properties for all foreign key relationships
6. THE Repository SHALL avoid loading unnecessary related entities to minimize data transfer

### Requirement 18: Database Schema Compatibility

**User Story:** As a database administrator, I want the migration to work with the existing database schema, so that no schema changes are required.

#### Acceptance Criteria

1. THE Entity_Configuration SHALL map to existing Oracle table names without modification
2. THE Entity_Configuration SHALL map to existing Oracle column names without modification
3. THE Entity_Configuration SHALL respect existing primary key and foreign key constraints
4. THE Entity_Configuration SHALL work with existing Oracle sequences for ID generation
5. THE Entity_Configuration SHALL handle existing Oracle data types correctly
6. THE migration SHALL NOT require any ALTER TABLE statements
7. THE migration SHALL NOT require any stored procedure modifications
8. THE Entity_Configuration SHALL handle Oracle-specific features (BLOB, CLOB, REF CURSOR) correctly

### Requirement 19: Deployment and Zero Downtime

**User Story:** As a DevOps engineer, I want to deploy the EF Core migration without downtime, so that users are not impacted.

#### Acceptance Criteria

1. THE deployment strategy SHALL support blue-green deployment where old and new versions run simultaneously
2. THE deployment strategy SHALL allow gradual traffic shifting from ADO.NET to EF Core implementations
3. THE deployment strategy SHALL include health checks that verify EF Core connectivity before routing traffic
4. THE deployment strategy SHALL support rollback to ADO.NET implementation within 5 minutes if issues occur
5. THE deployment strategy SHALL include database connection pool warm-up to prevent cold start delays
6. THE deployment strategy SHALL monitor error rates and automatically rollback if error threshold is exceeded
7. THE deployment strategy SHALL maintain existing connection pooling configuration during transition

### Requirement 20: Documentation and Knowledge Transfer

**User Story:** As a developer, I want comprehensive documentation for the EF Core implementation, so that I can maintain and extend the system.

#### Acceptance Criteria

1. THE documentation SHALL include a migration guide explaining the differences between ADO.NET and EF Core implementations
2. THE documentation SHALL provide examples of common repository patterns (CRUD, stored procedures, LINQ queries)
3. THE documentation SHALL document all entity configurations and relationships
4. THE documentation SHALL explain the transaction management approach
5. THE documentation SHALL include troubleshooting guides for common issues
6. THE documentation SHALL document performance optimization techniques
7. THE documentation SHALL provide a decision tree for when to use stored procedures vs LINQ queries
8. THE documentation SHALL include code comments explaining complex mappings and configurations

### Requirement 21: Monitoring and Observability

**User Story:** As a system administrator, I want to monitor EF Core performance and health, so that I can identify and resolve issues quickly.

#### Acceptance Criteria

1. THE EF_Core_DbContext SHALL log all SQL queries with execution times
2. THE EF_Core_DbContext SHALL expose metrics for connection pool usage
3. THE EF_Core_DbContext SHALL track query performance and identify slow queries
4. THE EF_Core_DbContext SHALL integrate with existing logging infrastructure (Serilog, Application Insights)
5. THE EF_Core_DbContext SHALL provide health check endpoints that verify database connectivity
6. THE monitoring SHALL alert when query execution times exceed thresholds
7. THE monitoring SHALL track the number of queries per request to identify N+1 problems
8. THE monitoring SHALL compare EF Core metrics with baseline ADO.NET metrics

### Requirement 22: Stored Procedure Output Parameter Handling

**User Story:** As a developer, I want to retrieve output parameters from stored procedures, so that I can get generated IDs and row counts.

#### Acceptance Criteria

1. WHEN a stored procedure has output parameters, THE Repository SHALL declare OracleParameter objects with Direction.Output
2. THE Repository SHALL execute the stored procedure using ExecuteSqlRaw with output parameters
3. THE Repository SHALL retrieve output parameter values after execution using parameter.Value
4. THE Repository SHALL convert OracleDecimal output values to C# long or decimal types
5. THE Repository SHALL handle null output parameter values appropriately
6. FOR ALL stored procedures with P_NEW_ID output parameters, THE Repository SHALL return the generated ID
7. FOR ALL stored procedures with P_ROWS_AFFECTED output parameters, THE Repository SHALL return the affected row count

### Requirement 23: Blob and Large Object Handling

**User Story:** As a developer, I want to handle BLOB columns efficiently, so that logo images and attachments are stored and retrieved correctly.

#### Acceptance Criteria

1. THE Entity_Configuration SHALL map BLOB columns to byte[] properties
2. THE Repository SHALL handle null BLOB values correctly
3. THE Repository SHALL support storing BLOB data through stored procedures using OracleParameter with OracleDbType.Blob
4. THE Repository SHALL support retrieving BLOB data through stored procedures and mapping to byte[] properties
5. THE Repository SHALL handle large BLOB values (>1MB) without memory issues
6. THE Repository SHALL use streaming for very large BLOB values when appropriate
7. FOR ALL entities with logo or attachment properties (Company, Branch, TicketAttachment), THE BLOB handling SHALL work correctly

### Requirement 24: Null Handling and Optional Properties

**User Story:** As a developer, I want nullable properties to be handled correctly, so that optional database columns work as expected.

#### Acceptance Criteria

1. THE Entity_Configuration SHALL mark optional properties as nullable using nullable reference types (string?, long?)
2. THE Repository SHALL convert DBNull.Value to null when mapping from stored procedure results
3. THE Repository SHALL convert null to DBNull.Value when passing parameters to stored procedures
4. THE Entity_Configuration SHALL use IsRequired(false) for optional properties
5. THE Repository SHALL handle null values in LINQ queries using null-conditional operators
6. THE Repository SHALL preserve existing null handling behavior from ADO.NET implementations

### Requirement 25: Sequence and Identity Generation

**User Story:** As a developer, I want primary keys to be generated automatically using Oracle sequences, so that I don't need to manually assign IDs.

#### Acceptance Criteria

1. THE Entity_Configuration SHALL configure primary key generation using HasDefaultValueSql with Oracle sequence syntax
2. THE Entity_Configuration SHALL map to existing Oracle sequences (SEQ_SYS_COMPANY, SEQ_SYS_USERS, etc.)
3. WHEN inserting entities through LINQ, THE EF_Core_DbContext SHALL retrieve generated IDs from sequences
4. WHEN inserting entities through stored procedures, THE Repository SHALL retrieve generated IDs from output parameters
5. THE Entity_Configuration SHALL use ValueGeneratedOnAdd for primary key properties
6. THE sequence generation SHALL work consistently across all 23 entity types

### Requirement 26: Soft Delete Support

**User Story:** As a developer, I want to support soft deletes where IS_ACTIVE is set to false, so that data is preserved for audit purposes.

#### Acceptance Criteria

1. THE Entity_Configuration SHALL include IS_ACTIVE property for all entities that support soft delete
2. THE Repository SHALL implement soft delete by calling stored procedures that set IS_ACTIVE to '0' or 'N'
3. THE Repository SHALL filter out soft-deleted records in queries by default using global query filters
4. THE EF_Core_DbContext SHALL configure global query filters using HasQueryFilter for entities with IS_ACTIVE
5. THE Repository SHALL provide methods to include soft-deleted records when needed using IgnoreQueryFilters
6. THE soft delete behavior SHALL match existing ADO.NET implementations

### Requirement 27: Multi-Tenancy Support

**User Story:** As a developer, I want to maintain multi-tenancy support where data is isolated by company and branch, so that tenant data remains secure.

#### Acceptance Criteria

1. THE Repository SHALL filter queries by company ID and branch ID based on user context
2. THE EF_Core_DbContext SHALL integrate with existing MultiTenantAccessService
3. THE Repository SHALL prevent cross-tenant data access through query filters
4. THE Entity_Configuration SHALL define foreign key relationships for company and branch associations
5. THE Repository SHALL validate tenant context before executing insert/update/delete operations
6. THE multi-tenancy behavior SHALL match existing ADO.NET implementations

### Requirement 28: Refresh Token and Authentication Support

**User Story:** As a developer, I want to support refresh token storage and retrieval, so that authentication continues to work correctly.

#### Acceptance Criteria

1. THE Entity_Configuration SHALL map REFRESH_TOKEN and REFRESH_TOKEN_EXPIRY columns for User entity
2. THE Repository SHALL support storing refresh tokens through stored procedures
3. THE Repository SHALL support retrieving users by refresh token for token refresh operations
4. THE Repository SHALL support clearing refresh tokens during logout operations
5. THE Repository SHALL support force logout functionality that clears refresh tokens
6. THE authentication behavior SHALL match existing ADO.NET implementations

### Requirement 29: Fiscal Year and Branch Relationship

**User Story:** As a developer, I want to maintain the relationship between companies, branches, and fiscal years, so that financial data is correctly organized.

#### Acceptance Criteria

1. THE Entity_Configuration SHALL define navigation properties for Company-Branch relationship
2. THE Entity_Configuration SHALL define navigation properties for Branch-FiscalYear relationship
3. THE Repository SHALL support creating companies with default branches and fiscal years in a single transaction
4. THE Repository SHALL support setting default branches for companies
5. THE Repository SHALL enforce referential integrity for company-branch-fiscal year relationships
6. THE relationship behavior SHALL match existing ADO.NET implementations including SP_SYS_COMPANY_INSERT_WITH_BRANCH

### Requirement 30: Ticket System Relationships

**User Story:** As a developer, I want to maintain all ticket system relationships, so that the support ticket functionality works correctly.

#### Acceptance Criteria

1. THE Entity_Configuration SHALL define navigation properties for Ticket-TicketType relationship
2. THE Entity_Configuration SHALL define navigation properties for Ticket-TicketStatus relationship
3. THE Entity_Configuration SHALL define navigation properties for Ticket-TicketPriority relationship
4. THE Entity_Configuration SHALL define navigation properties for Ticket-TicketComment relationship (one-to-many)
5. THE Entity_Configuration SHALL define navigation properties for Ticket-TicketAttachment relationship (one-to-many)
6. THE Entity_Configuration SHALL define navigation properties for Ticket-Company relationship
7. THE Entity_Configuration SHALL define navigation properties for Ticket-Branch relationship
8. THE Repository SHALL support all ticket-related stored procedures
9. THE ticket system behavior SHALL match existing ADO.NET implementations
