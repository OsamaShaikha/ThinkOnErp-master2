# Implementation Plan: ThinkOnErp API

## Overview

This implementation plan creates a production-ready ASP.NET Core 8 Web API with Clean Architecture, CQRS pattern using MediatR, JWT authentication, Oracle database with stored procedures, and comprehensive property-based testing. The system manages 5 core entities (Role, Currency, Company, Branch, User) with full CRUD operations, soft delete pattern, and admin-only authorization for write operations.

## Tasks

- [x] 1. Set up Oracle database schema and stored procedures
  - [x] 1.1 Create Oracle sequences for all 5 entities
    - Create SEQ_SYS_ROLE, SEQ_SYS_CURRENCY, SEQ_SYS_COMPANY, SEQ_SYS_BRANCH, SEQ_SYS_USERS
    - _Requirements: 27.1, 27.2, 27.3, 27.4, 27.5_
  
  - [x] 1.2 Create stored procedures for SYS_ROLE table
    - SP_SYS_ROLE_SELECT_ALL, SP_SYS_ROLE_SELECT_BY_ID, SP_SYS_ROLE_INSERT, SP_SYS_ROLE_UPDATE, SP_SYS_ROLE_DELETE
    - Include SYS_REFCURSOR for SELECT operations, audit trail fields, soft delete logic
    - _Requirements: 17.1, 17.2, 17.3, 17.4, 17.5, 17.6, 17.7, 17.8, 28.1, 28.2, 28.3, 28.4, 28.5_
  
  - [x] 1.3 Create stored procedures for SYS_CURRENCY table
    - SP_SYS_CURRENCY_SELECT_ALL, SP_SYS_CURRENCY_SELECT_BY_ID, SP_SYS_CURRENCY_INSERT, SP_SYS_CURRENCY_UPDATE, SP_SYS_CURRENCY_DELETE
    - Include SYS_REFCURSOR for SELECT operations, audit trail fields, soft delete logic
    - _Requirements: 18.1, 18.2, 18.3, 18.4, 18.5, 18.6, 18.7, 18.8_
  
  - [x] 1.4 Create stored procedures for SYS_COMPANY table
    - SP_SYS_COMPANY_SELECT_ALL, SP_SYS_COMPANY_SELECT_BY_ID, SP_SYS_COMPANY_INSERT, SP_SYS_COMPANY_UPDATE, SP_SYS_COMPANY_DELETE
    - Include SYS_REFCURSOR for SELECT operations, audit trail fields, soft delete logic
    - _Requirements: 19.1, 19.2, 19.3, 19.4, 19.5, 19.6, 19.7, 19.8_
  
  - [x] 1.5 Create stored procedures for SYS_BRANCH table
    - SP_SYS_BRANCH_SELECT_ALL, SP_SYS_BRANCH_SELECT_BY_ID, SP_SYS_BRANCH_INSERT, SP_SYS_BRANCH_UPDATE, SP_SYS_BRANCH_DELETE
    - Include SYS_REFCURSOR for SELECT operations, audit trail fields, soft delete logic
    - _Requirements: 20.1, 20.2, 20.3, 20.4, 20.5, 20.6, 20.7, 20.8_
  
  - [x] 1.6 Create stored procedures for SYS_USERS table
    - SP_SYS_USERS_SELECT_ALL, SP_SYS_USERS_SELECT_BY_ID, SP_SYS_USERS_INSERT, SP_SYS_USERS_UPDATE, SP_SYS_USERS_DELETE, SP_SYS_USERS_LOGIN
    - Include SYS_REFCURSOR for SELECT operations, audit trail fields, soft delete logic, authentication logic
    - _Requirements: 21.1, 21.2, 21.3, 21.4, 21.5, 21.6, 21.7, 21.8, 21.9, 21.10, 21.11_


- [x] 2. Create Domain layer with entities and repository interfaces
  - [x] 2.1 Create SysRole entity with all properties
    - RowId, RowDesc, RowDescE, Note, IsActive, CreationUser, CreationDate, UpdateUser, UpdateDate
    - _Requirements: 1.3, 1.5_
  
  - [x] 2.2 Create SysCurrency entity with all properties
    - RowId, RowDesc, RowDescE, ShortDesc, ShortDescE, SingulerDesc, SingulerDescE, DualDesc, DualDescE, SumDesc, SumDescE, FracDesc, FracDescE, CurrRate, CurrRateDate, CreationUser, CreationDate, UpdateUser, UpdateDate
    - _Requirements: 1.3, 1.5_
  
  - [x] 2.3 Create SysCompany entity with all properties
    - RowId, RowDesc, RowDescE, CountryId, CurrId, IsActive, CreationUser, CreationDate, UpdateUser, UpdateDate
    - _Requirements: 1.3, 1.5_
  
  - [x] 2.4 Create SysBranch entity with all properties
    - RowId, ParRowId, RowDesc, RowDescE, Phone, Mobile, Fax, Email, IsHeadBranch, IsActive, CreationUser, CreationDate, UpdateUser, UpdateDate
    - _Requirements: 1.3, 1.5_
  
  - [x] 2.5 Create SysUser entity with all properties
    - RowId, RowDesc, RowDescE, UserName, Password, Phone, Phone2, Role, BranchId, Email, LastLoginDate, IsActive, IsAdmin, CreationUser, CreationDate, UpdateUser, UpdateDate
    - _Requirements: 1.3, 1.5_
  
  - [x] 2.6 Create IRoleRepository interface
    - GetAllAsync, GetByIdAsync, CreateAsync, UpdateAsync, DeleteAsync methods
    - _Requirements: 1.3, 1.5, 6.1, 6.2, 6.3, 6.4, 6.5_
  
  - [x] 2.7 Create ICurrencyRepository interface
    - GetAllAsync, GetByIdAsync, CreateAsync, UpdateAsync, DeleteAsync methods
    - _Requirements: 1.3, 1.5, 7.1, 7.2, 7.3, 7.4, 7.5_
  
  - [x] 2.8 Create ICompanyRepository interface
    - GetAllAsync, GetByIdAsync, CreateAsync, UpdateAsync, DeleteAsync methods
    - _Requirements: 1.3, 1.5, 8.1, 8.2, 8.3, 8.4, 8.5_
  
  - [x] 2.9 Create IBranchRepository interface
    - GetAllAsync, GetByIdAsync, CreateAsync, UpdateAsync, DeleteAsync methods
    - _Requirements: 1.3, 1.5, 9.1, 9.2, 9.3, 9.4, 9.5_
  
  - [x] 2.10 Create IUserRepository interface
    - GetAllAsync, GetByIdAsync, CreateAsync, UpdateAsync, DeleteAsync methods
    - _Requirements: 1.3, 1.5, 10.1, 10.2, 10.3, 10.4, 10.5_
  
  - [x] 2.11 Create IAuthRepository interface
    - AuthenticateAsync method for login validation
    - _Requirements: 1.3, 1.5, 2.1, 2.2_

- [x] 3. Create Application layer with DTOs, commands, queries, and validators
  - [x] 3.1 Create DTOs for all entities
    - RoleDto, CreateRoleDto, UpdateRoleDto
    - CurrencyDto, CreateCurrencyDto, UpdateCurrencyDto
    - CompanyDto, CreateCompanyDto, UpdateCompanyDto
    - BranchDto, CreateBranchDto, UpdateBranchDto
    - UserDto, CreateUserDto, UpdateUserDto, ChangePasswordDto
    - LoginDto, TokenDto
    - Include XML documentation for Swagger
    - _Requirements: 30.1, 30.2, 30.3, 30.5, 30.6_
  
  - [x] 3.2 Create ApiResponse wrapper class
    - Success, StatusCode, Message, Data, Errors, Timestamp, TraceId properties
    - Static factory methods Success and Fail
    - _Requirements: 11.1, 11.2, 11.3, 11.4, 11.5, 11.6, 11.7, 11.8, 11.9, 11.10_
  
  - [x] 3.3 Create Role commands with handlers and validators
    - CreateRoleCommand, CreateRoleCommandHandler, CreateRoleCommandValidator
    - UpdateRoleCommand, UpdateRoleCommandHandler, UpdateRoleCommandValidator
    - DeleteRoleCommand, DeleteRoleCommandHandler
    - _Requirements: 6.3, 6.4, 6.5, 29.1, 29.3, 29.4_
  
  - [x] 3.4 Create Role queries with handlers
    - GetAllRolesQuery, GetAllRolesQueryHandler
    - GetRoleByIdQuery, GetRoleByIdQueryHandler
    - _Requirements: 6.1, 6.2, 29.2, 29.3, 29.4_
  
  - [x] 3.5 Create Currency commands with handlers and validators
    - CreateCurrencyCommand, CreateCurrencyCommandHandler, CreateCurrencyCommandValidator
    - UpdateCurrencyCommand, UpdateCurrencyCommandHandler, UpdateCurrencyCommandValidator
    - DeleteCurrencyCommand, DeleteCurrencyCommandHandler
    - _Requirements: 7.3, 7.4, 7.5, 29.1, 29.3, 29.4_
  
  - [x] 3.6 Create Currency queries with handlers
    - GetAllCurrenciesQuery, GetAllCurrenciesQueryHandler
    - GetCurrencyByIdQuery, GetCurrencyByIdQueryHandler
    - _Requirements: 7.1, 7.2, 29.2, 29.3, 29.4_
  
  - [x] 3.7 Create Company commands with handlers and validators
    - CreateCompanyCommand, CreateCompanyCommandHandler, CreateCompanyCommandValidator
    - UpdateCompanyCommand, UpdateCompanyCommandHandler, UpdateCompanyCommandValidator
    - DeleteCompanyCommand, DeleteCompanyCommandHandler
    - _Requirements: 8.3, 8.4, 8.5, 29.1, 29.3, 29.4_
  
  - [x] 3.8 Create Company queries with handlers
    - GetAllCompaniesQuery, GetAllCompaniesQueryHandler
    - GetCompanyByIdQuery, GetCompanyByIdQueryHandler
    - _Requirements: 8.1, 8.2, 29.2, 29.3, 29.4_
  
  - [x] 3.9 Create Branch commands with handlers and validators
    - CreateBranchCommand, CreateBranchCommandHandler, CreateBranchCommandValidator
    - UpdateBranchCommand, UpdateBranchCommandHandler, UpdateBranchCommandValidator
    - DeleteBranchCommand, DeleteBranchCommandHandler
    - _Requirements: 9.3, 9.4, 9.5, 29.1, 29.3, 29.4_
  
  - [x] 3.10 Create Branch queries with handlers
    - GetAllBranchesQuery, GetAllBranchesQueryHandler
    - GetBranchByIdQuery, GetBranchByIdQueryHandler
    - _Requirements: 9.1, 9.2, 29.2, 29.3, 29.4_
  
  - [x] 3.11 Create User commands with handlers and validators
    - CreateUserCommand, CreateUserCommandHandler, CreateUserCommandValidator
    - UpdateUserCommand, UpdateUserCommandHandler, UpdateUserCommandValidator
    - DeleteUserCommand, DeleteUserCommandHandler
    - ChangePasswordCommand, ChangePasswordCommandHandler, ChangePasswordCommandValidator
    - _Requirements: 10.3, 10.4, 10.5, 10.6, 29.1, 29.3, 29.4_
  
  - [x] 3.12 Create User queries with handlers
    - GetAllUsersQuery, GetAllUsersQueryHandler
    - GetUserByIdQuery, GetUserByIdQueryHandler
    - _Requirements: 10.1, 10.2, 29.2, 29.3, 29.4_
  
  - [x] 3.13 Create Login command with handler and validator
    - LoginCommand, LoginCommandHandler, LoginCommandValidator
    - _Requirements: 2.1, 2.2, 29.1, 29.3, 29.4_
  
  - [x] 3.14 Create ValidationBehavior pipeline behavior
    - Intercept all MediatR requests, execute FluentValidation validators, collect errors, throw ValidationException
    - _Requirements: 12.1, 12.2, 12.3, 12.4, 12.5, 12.6, 12.7, 29.7, 29.8_
  
  - [x] 3.15 Create LoggingBehavior pipeline behavior
    - Log all MediatR requests and responses with execution time
    - _Requirements: 13.8, 29.7_
  
  - [x] 3.16 Create AddApplication extension method for dependency injection
    - Register MediatR, FluentValidation, ValidationBehavior, LoggingBehavior
    - _Requirements: 15.2, 15.6, 15.7, 15.8, 15.9_

- [x] 4. Create Infrastructure layer with repositories and services
  - [x] 4.1 Create OracleDbContext for connection management
    - Read connection string from configuration, provide connection creation method, implement IDisposable
    - _Requirements: 22.1, 22.2, 22.3, 22.4_
  
  - [x] 4.2 Create RoleRepository implementing IRoleRepository
    - Implement GetAllAsync calling SP_SYS_ROLE_SELECT_ALL with SYS_REFCURSOR
    - Implement GetByIdAsync calling SP_SYS_ROLE_SELECT_BY_ID with SYS_REFCURSOR
    - Implement CreateAsync calling SP_SYS_ROLE_INSERT with output parameter for new ID
    - Implement UpdateAsync calling SP_SYS_ROLE_UPDATE
    - Implement DeleteAsync calling SP_SYS_ROLE_DELETE
    - Use OracleCommand with CommandType.StoredProcedure, OracleParameter with explicit OracleDbType, proper disposal
    - _Requirements: 5.1, 5.2, 5.3, 5.4, 5.5, 5.6, 5.7, 5.8, 6.6, 6.7, 6.8, 6.9, 6.10, 22.5, 22.6, 22.7, 23.1, 23.2, 23.3, 23.4, 23.5, 23.6, 23.7, 23.8_
  
  - [x] 4.3 Create CurrencyRepository implementing ICurrencyRepository
    - Implement GetAllAsync calling SP_SYS_CURRENCY_SELECT_ALL with SYS_REFCURSOR
    - Implement GetByIdAsync calling SP_SYS_CURRENCY_SELECT_BY_ID with SYS_REFCURSOR
    - Implement CreateAsync calling SP_SYS_CURRENCY_INSERT with output parameter for new ID
    - Implement UpdateAsync calling SP_SYS_CURRENCY_UPDATE
    - Implement DeleteAsync calling SP_SYS_CURRENCY_DELETE
    - Use OracleCommand with CommandType.StoredProcedure, OracleParameter with explicit OracleDbType, proper disposal
    - _Requirements: 5.1, 5.2, 5.3, 5.4, 5.5, 5.6, 5.7, 5.8, 7.6, 7.7, 7.8, 7.9, 7.10, 22.5, 22.6, 22.7, 23.1, 23.2, 23.3, 23.4, 23.5, 23.6, 23.7, 23.8_
  
  - [x] 4.4 Create CompanyRepository implementing ICompanyRepository
    - Implement GetAllAsync calling SP_SYS_COMPANY_SELECT_ALL with SYS_REFCURSOR
    - Implement GetByIdAsync calling SP_SYS_COMPANY_SELECT_BY_ID with SYS_REFCURSOR
    - Implement CreateAsync calling SP_SYS_COMPANY_INSERT with output parameter for new ID
    - Implement UpdateAsync calling SP_SYS_COMPANY_UPDATE
    - Implement DeleteAsync calling SP_SYS_COMPANY_DELETE
    - Use OracleCommand with CommandType.StoredProcedure, OracleParameter with explicit OracleDbType, proper disposal
    - _Requirements: 5.1, 5.2, 5.3, 5.4, 5.5, 5.6, 5.7, 5.8, 8.6, 8.7, 8.8, 8.9, 8.10, 22.5, 22.6, 22.7, 23.1, 23.2, 23.3, 23.4, 23.5, 23.6, 23.7, 23.8_
  
  - [x] 4.5 Create BranchRepository implementing IBranchRepository
    - Implement GetAllAsync calling SP_SYS_BRANCH_SELECT_ALL with SYS_REFCURSOR
    - Implement GetByIdAsync calling SP_SYS_BRANCH_SELECT_BY_ID with SYS_REFCURSOR
    - Implement CreateAsync calling SP_SYS_BRANCH_INSERT with output parameter for new ID
    - Implement UpdateAsync calling SP_SYS_BRANCH_UPDATE
    - Implement DeleteAsync calling SP_SYS_BRANCH_DELETE
    - Use OracleCommand with CommandType.StoredProcedure, OracleParameter with explicit OracleDbType, proper disposal
    - _Requirements: 5.1, 5.2, 5.3, 5.4, 5.5, 5.6, 5.7, 5.8, 9.6, 9.7, 9.8, 9.9, 9.10, 22.5, 22.6, 22.7, 23.1, 23.2, 23.3, 23.4, 23.5, 23.6, 23.7, 23.8_
  
  - [x] 4.6 Create UserRepository implementing IUserRepository
    - Implement GetAllAsync calling SP_SYS_USERS_SELECT_ALL with SYS_REFCURSOR
    - Implement GetByIdAsync calling SP_SYS_USERS_SELECT_BY_ID with SYS_REFCURSOR
    - Implement CreateAsync calling SP_SYS_USERS_INSERT with output parameter for new ID
    - Implement UpdateAsync calling SP_SYS_USERS_UPDATE
    - Implement DeleteAsync calling SP_SYS_USERS_DELETE
    - Use OracleCommand with CommandType.StoredProcedure, OracleParameter with explicit OracleDbType, proper disposal
    - _Requirements: 5.1, 5.2, 5.3, 5.4, 5.5, 5.6, 5.7, 5.8, 10.7, 10.8, 10.9, 10.10, 10.11, 22.5, 22.6, 22.7, 23.1, 23.2, 23.3, 23.4, 23.5, 23.6, 23.7, 23.8_
  
  - [x] 4.7 Create AuthRepository implementing IAuthRepository
    - Implement AuthenticateAsync calling SP_SYS_USERS_LOGIN with username and password hash
    - Return user if credentials match and IS_ACTIVE is true, otherwise return null
    - _Requirements: 2.1, 2.2, 3.2, 3.3, 5.1, 5.2, 5.3, 5.4, 5.5, 5.6, 5.7, 5.8_
  
  - [x] 4.8 Create PasswordHashingService for SHA-256 hashing
    - Hash passwords using SHA-256, convert to hexadecimal string representation
    - _Requirements: 3.1, 3.2, 3.3, 3.4_
  
  - [x] 4.9 Create JwtTokenService for JWT token generation
    - Read JWT settings from configuration (SecretKey, Issuer, Audience, ExpiryInMinutes)
    - Generate tokens with claims: userId, userName, role, branchId, isAdmin
    - Sign tokens using HMAC-SHA256, set expiration time
    - _Requirements: 2.1, 2.3, 2.4, 2.5_
  
  - [x] 4.10 Create AddInfrastructure extension method for dependency injection
    - Register OracleDbContext, all repositories, PasswordHashingService, JwtTokenService as Scoped
    - _Requirements: 15.1, 15.3, 15.4, 15.5, 15.10_

- [x] 5. Create API layer with controllers, middleware, and configuration
  - [x] 5.1 Configure Serilog in Program.cs
    - Configure before building host, replace Microsoft logging
    - Console sink with colored output, file sink with daily rolling logs
    - Minimum level Information (production), Debug (development)
    - Enrich with LogContext, MachineName, ThreadId
    - _Requirements: 13.1, 13.2, 13.3, 13.4, 13.5, 13.6, 13.7, 13.10_
  
  - [x] 5.2 Create ExceptionHandlingMiddleware
    - Catch all unhandled exceptions, log with full details
    - Convert ValidationException to 400 with errors array
    - Convert all other exceptions to 500 with generic message
    - Never expose stack traces, set content type to application/json
    - _Requirements: 14.1, 14.2, 14.3, 14.4, 14.5, 14.6, 14.7_
  
  - [x] 5.3 Configure JWT authentication and authorization in Program.cs
    - Add JWT Bearer authentication with validation parameters
    - Configure AdminOnly authorization policy checking isAdmin claim
    - _Requirements: 2.6, 2.7, 4.1, 4.2, 4.3, 4.4, 4.5_
  
  - [x] 5.4 Configure Swagger with JWT Bearer support in Program.cs
    - Enable Swagger UI in Development environment only
    - Add JWT Bearer security definition with Authorize button
    - _Requirements: 16.1, 16.2, 16.3, 16.4, 16.5, 16.6, 16.7_
  
  - [x] 5.5 Register services and middleware in Program.cs
    - Call AddApplication and AddInfrastructure extension methods
    - Add exception handling middleware, authentication, authorization
    - _Requirements: 1.1, 1.2, 1.7, 1.8, 15.10_
  
  - [x] 5.6 Create AuthController with login endpoint
    - POST /api/auth/login without authorization
    - Return ApiResponse with TokenDto on success, 401 on failure
    - _Requirements: 24.1, 2.1, 2.2, 26.4_
  
  - [x] 5.7 Create RolesController with CRUD endpoints
    - GET /api/roles (authorized), GET /api/roles/{id} (authorized)
    - POST /api/roles (AdminOnly), PUT /api/roles/{id} (AdminOnly), DELETE /api/roles/{id} (AdminOnly)
    - Return ApiResponse wrapper for all endpoints
    - _Requirements: 24.2, 24.3, 24.4, 24.5, 24.6, 26.1, 26.2_
  
  - [x] 5.8 Create CurrencyController with CRUD endpoints
    - GET /api/currencies (authorized), GET /api/currencies/{id} (authorized)
    - POST /api/currencies (AdminOnly), PUT /api/currencies/{id} (AdminOnly), DELETE /api/currencies/{id} (AdminOnly)
    - Return ApiResponse wrapper for all endpoints
    - _Requirements: 24.7, 24.8, 24.9, 24.10, 24.11, 26.1, 26.2_
  
  - [x] 5.9 Create CompanyController with CRUD endpoints
    - GET /api/companies (authorized), GET /api/companies/{id} (authorized)
    - POST /api/companies (AdminOnly), PUT /api/companies/{id} (AdminOnly), DELETE /api/companies/{id} (AdminOnly)
    - Return ApiResponse wrapper for all endpoints
    - _Requirements: 24.12, 24.13, 24.14, 24.15, 24.16, 26.1, 26.2_
  
  - [x] 5.10 Create BranchController with CRUD endpoints
    - GET /api/branches (authorized), GET /api/branches/{id} (authorized)
    - POST /api/branches (AdminOnly), PUT /api/branches/{id} (AdminOnly), DELETE /api/branches/{id} (AdminOnly)
    - Return ApiResponse wrapper for all endpoints
    - _Requirements: 24.17, 24.18, 24.19, 24.20, 24.21, 26.1, 26.2_
  
  - [x] 5.11 Create UsersController with CRUD endpoints
    - GET /api/users (AdminOnly), GET /api/users/{id} (AdminOnly)
    - POST /api/users (AdminOnly), PUT /api/users/{id} (AdminOnly), DELETE /api/users/{id} (AdminOnly)
    - PUT /api/users/{id}/change-password (authorized)
    - Return ApiResponse wrapper for all endpoints
    - _Requirements: 24.22, 24.23, 24.24, 24.25, 24.26, 24.27, 26.1, 26.2_
  
  - [x] 5.12 Create appsettings.json with configuration template
    - ConnectionStrings:OracleDb, JwtSettings (SecretKey, Issuer, Audience, ExpiryInMinutes), Serilog:MinimumLevel
    - _Requirements: 25.1, 25.2, 25.3, 25.4, 25.5, 25.6, 25.7_

- [x] 6. Checkpoint - Ensure all tests pass
  - Ensure all tests pass, ask the user if questions arise.

- [x] 7. Create property-based tests with FsCheck
  - [x] 7.1 Write property test for JWT token structure completeness
    - **Property 1: JWT Token Structure Completeness**
    - **Validates: Requirements 2.1, 2.3, 2.4, 2.5**
    - For any valid user credentials, verify token contains all required claims (userId, userName, role, branchId, isAdmin), signed with configured secret, includes issuer and audience, expiration matches ExpiryInMinutes
    - Minimum 100 iterations
  
  - [x] 7.2 Write property test for authentication failure returns 401
    - **Property 2: Authentication Failure Returns 401**
    - **Validates: Requirements 2.2**
    - For any invalid credentials (non-existent username, incorrect password, inactive user), verify status code 401
    - Minimum 100 iterations
  
  - [x] 7.3 Write property test for valid token authentication
    - **Property 3: Valid Token Authentication**
    - **Validates: Requirements 2.6**
    - For any valid JWT token in request to protected endpoint, verify authentication succeeds
    - Minimum 100 iterations
  
  - [x] 7.4 Write property test for invalid token rejection
    - **Property 4: Invalid Token Rejection**
    - **Validates: Requirements 2.7**
    - For any invalid or expired JWT token, verify status code 401
    - Minimum 100 iterations
  
  - [x] 7.5 Write property test for password hashing on storage
    - **Property 5: Password Hashing on Storage**
    - **Validates: Requirements 3.1, 3.4**
    - For any user password, verify stored as SHA-256 hexadecimal hash, never plain text
    - Minimum 100 iterations
  
  - [x] 7.6 Write property test for password hashing on authentication
    - **Property 6: Password Hashing on Authentication**
    - **Validates: Requirements 3.2, 3.3**
    - For any login attempt, verify password hashed using SHA-256 before comparison
    - Minimum 100 iterations
  
  - [x] 7.7 Write property test for protected endpoint authorization
    - **Property 7: Protected Endpoint Authorization**
    - **Validates: Requirements 4.1, 4.4**
    - For any protected endpoint without valid JWT token, verify status code 401
    - Minimum 100 iterations
  
  - [x] 7.8 Write property test for admin-only endpoint authorization
    - **Property 8: Admin-Only Endpoint Authorization**
    - **Validates: Requirements 4.3, 4.5**
    - For any admin-only endpoint accessed by non-admin user, verify status code 403
    - Minimum 100 iterations
  
  - [x] 7.9 Write property test for GetAll returns only active records
    - **Property 9: GetAll Returns Only Active Records**
    - **Validates: Requirements 6.1, 7.1, 8.1, 9.1, 10.1**
    - For any entity type, verify GetAll returns only records where IS_ACTIVE is true
    - Minimum 100 iterations
  
  - [x] 7.10 Write property test for GetById returns match or null
    - **Property 10: GetById Returns Match or Null**
    - **Validates: Requirements 6.2, 7.2, 8.2, 9.2, 10.2**
    - For any entity type and ID, verify GetById returns matching record or null
    - Minimum 100 iterations
  
  - [x] 7.11 Write property test for Create returns valid ID
    - **Property 11: Create Returns Valid ID**
    - **Validates: Requirements 6.3, 7.3, 8.3, 9.3, 10.3**
    - For any entity type and valid data, verify Create returns positive decimal ID from Oracle sequence
    - Minimum 100 iterations
  
  - [x] 7.12 Write property test for Update succeeds for valid data
    - **Property 12: Update Succeeds for Valid Data**
    - **Validates: Requirements 6.4, 7.4, 8.4, 9.4, 10.4**
    - For any entity type, existing ID, and valid update data, verify Update succeeds and values persisted
    - Minimum 100 iterations
  
  - [x] 7.13 Write property test for Delete is soft delete
    - **Property 13: Delete is Soft Delete**
    - **Validates: Requirements 6.5, 7.5, 8.5, 9.5, 10.5**
    - For any entity type and existing ID, verify Delete sets IS_ACTIVE to false, record not in GetAll results
    - Minimum 100 iterations
  
  - [x] 7.14 Write property test for change password updates hash
    - **Property 14: Change Password Updates Hash**
    - **Validates: Requirements 10.6**
    - For any user and new valid password, verify password hashed using SHA-256 and stored hash updated
    - Minimum 100 iterations
  
  - [x] 7.15 Write property test for success response structure
    - **Property 15: Success Response Structure**
    - **Validates: Requirements 11.1, 11.3, 11.4, 11.5, 11.6, 11.7**
    - For any successful operation, verify ApiResponse has success=true, appropriate statusCode, message, data, ISO 8601 timestamp, traceId
    - Minimum 100 iterations
  
  - [x] 7.16 Write property test for error response structure
    - **Property 16: Error Response Structure**
    - **Validates: Requirements 11.2, 11.3, 11.4, 11.5, 11.6, 11.7**
    - For any failed operation, verify ApiResponse has success=false, appropriate statusCode, message, null data, ISO 8601 timestamp, traceId
    - Minimum 100 iterations
  
  - [x] 7.17 Write property test for validation error response
    - **Property 17: Validation Error Response**
    - **Validates: Requirements 11.8, 12.4, 12.5, 12.6**
    - For any request failing validation, verify response includes errors array with all validation messages, status code 400
    - Minimum 100 iterations
  
  - [x] 7.18 Write property test for validation executes for all requests
    - **Property 18: Validation Executes for All Requests**
    - **Validates: Requirements 12.3**
    - For any command or query with registered validators, verify validation behavior executes all validators before handler
    - Minimum 100 iterations
  
  - [x] 7.19 Write property test for unhandled exception returns 500
    - **Property 19: Unhandled Exception Returns 500**
    - **Validates: Requirements 14.1, 14.2, 14.3, 14.4, 14.5**
    - For any unhandled exception, verify middleware catches, logs at Error level, returns ApiResponse with status code 500, generic message, no stack trace
    - Minimum 100 iterations
  
  - [x] 7.20 Write property test for ValidationException returns 400
    - **Property 20: ValidationException Returns 400**
    - **Validates: Requirements 14.6**
    - For any ValidationException, verify middleware converts to ApiResponse with status code 400 and all validation errors
    - Minimum 100 iterations
  
  - [x] 7.21 Write property test for exception response is JSON
    - **Property 21: Exception Response is JSON**
    - **Validates: Requirements 14.7**
    - For any exception handled by middleware, verify response content type is application/json
    - Minimum 100 iterations
  
  - [x] 7.22 Write property test for all requests logged
    - **Property 22: All Requests Logged**
    - **Validates: Requirements 13.8**
    - For any MediatR request, verify logging behavior logs request type, parameters, response data, execution time
    - Minimum 100 iterations
  
  - [x] 7.23 Write property test for all exceptions logged
    - **Property 23: All Exceptions Logged**
    - **Validates: Requirements 13.9**
    - For any exception caught by middleware, verify logged at Error level with full details
    - Minimum 100 iterations
  
  - [x] 7.24 Write property test for database exception handling
    - **Property 24: Database Exception Handling**
    - **Validates: Requirements 22.7**
    - For any database operation throwing exception, verify repository logs and rethrows as domain exception
    - Minimum 100 iterations
  
  - [x] 7.25 Write property test for IS_ACTIVE mapping to boolean
    - **Property 25: IS_ACTIVE Mapping to Boolean**
    - **Validates: Requirements 23.4, 23.5**
    - For any Oracle IS_ACTIVE value 'Y' or '1', verify mapped to C# true; for 'N' or '0', verify mapped to C# false
    - Minimum 100 iterations
  
  - [x] 7.26 Write property test for success message format
    - **Property 26: Success Message Format**
    - **Validates: Requirements 26.1**
    - For any successful create operation, verify message follows format "{EntityName} created successfully"
    - Minimum 100 iterations
  
  - [x] 7.27 Write property test for not found message format
    - **Property 27: Not Found Message Format**
    - **Validates: Requirements 26.2**
    - For any GetById returning null, verify message follows format "No {entityName} found with the specified identifier"
    - Minimum 100 iterations
  
  - [x] 7.28 Write property test for authorization error message
    - **Property 28: Authorization Error Message**
    - **Validates: Requirements 26.3**
    - For any authorization failure (403), verify message is "Access denied. Administrator privileges are required"
    - Minimum 100 iterations
  
  - [x] 7.29 Write property test for authentication error message
    - **Property 29: Authentication Error Message**
    - **Validates: Requirements 26.4**
    - For any authentication failure (401 from login), verify message is "Invalid credentials. Please verify your username and password"
    - Minimum 100 iterations
  
  - [x] 7.30 Write property test for validation error message
    - **Property 30: Validation Error Message**
    - **Validates: Requirements 26.5**
    - For any validation failure, verify message is "One or more validation errors occurred"
    - Minimum 100 iterations
  
  - [x] 7.31 Write property test for server error message
    - **Property 31: Server Error Message**
    - **Validates: Requirements 26.6**
    - For any unhandled exception, verify message is "An unexpected error occurred. Please try again later"
    - Minimum 100 iterations
  
  - [x] 7.32 Write property test for no domain entities in API responses
    - **Property 32: No Domain Entities in API Responses**
    - **Validates: Requirements 30.3**
    - For any API endpoint response, verify data is DTO, never domain entity directly
    - Minimum 100 iterations

- [x] 8. Create unit tests for specific scenarios and edge cases
  - [x] 8.1 Write unit tests for authentication scenarios
    - Test login with valid credentials returns token
    - Test login with invalid username returns 401
    - Test login with invalid password returns 401
    - Test login with inactive user returns 401
    - Test token contains correct claims for admin user
    - Test token contains correct claims for non-admin user
  
  - [x] 8.2 Write unit tests for validation edge cases
    - Test CreateRole with empty RowDesc fails validation
    - Test CreateRole with RowDesc exceeding 100 characters fails validation
    - Test CreateRole with null Note succeeds
    - Test CreateCurrency with negative CurrRate fails validation
    - Test CreateUser with duplicate UserName fails
    - Test ChangePassword with password shorter than minimum length fails
  
  - [x] 8.3 Write unit tests for repository operations
    - Test GetById with existing ID returns correct role
    - Test GetById with non-existent ID returns null
    - Test GetAll with no records returns empty list
    - Test Create with valid data returns positive ID
    - Test Update with valid data persists changes
    - Test Delete with existing ID sets IS_ACTIVE to false
    - Test GetAll after Delete does not include deleted record
  
  - [x] 8.4 Write unit tests for password hashing
    - Test same password produces same hash
    - Test different passwords produce different hashes
    - Test hash is 64 characters (SHA-256 hex)
    - Test hash contains only hexadecimal characters
  
  - [x] 8.5 Write unit tests for ApiResponse wrapper
    - Test Success factory method creates response with success=true
    - Test Fail factory method creates response with success=false
    - Test Success includes data payload
    - Test Fail includes errors array
    - Test both include timestamp and traceId
  
  - [x] 8.6 Write unit tests for exception middleware
    - Test ValidationException converted to 400 with errors
    - Test generic Exception converted to 500 with generic message
    - Test response content type is application/json
    - Test stack trace not included in response
    - Test exception logged with full details
  
  - [x] 8.7 Write unit tests for authorization policies
    - Test AdminOnly policy allows admin user
    - Test AdminOnly policy denies non-admin user
    - Test protected endpoint denies unauthenticated request
    - Test protected endpoint allows authenticated request
  
  - [x] 8.8 Write unit tests for MediatR pipeline behaviors
    - Test LoggingBehavior logs request and response
    - Test ValidationBehavior executes before handler
    - Test ValidationBehavior collects all errors before throwing
    - Test pipeline executes in correct order (Logging → Validation → Handler)
  
  - [x] 8.9 Write unit tests for data type mapping
    - Test Oracle NUMBER mapped to C# decimal
    - Test Oracle VARCHAR2 mapped to C# string
    - Test Oracle DATE mapped to C# DateTime?
    - Test Oracle 'Y' mapped to C# true
    - Test Oracle 'N' mapped to C# false
    - Test Oracle '1' mapped to C# true
    - Test Oracle '0' mapped to C# false
  
  - [x] 8.10 Write integration tests for end-to-end flows
    - Test complete CRUD flow for Role entity
    - Test complete CRUD flow for Currency entity
    - Test complete CRUD flow for Company entity
    - Test complete CRUD flow for Branch entity
    - Test complete CRUD flow for User entity
    - Test login → create role → update role → delete role flow
    - Test unauthorized access returns 401
    - Test non-admin access to admin endpoint returns 403

- [x] 9. Final checkpoint - Ensure all tests pass
  - Ensure all tests pass, ask the user if questions arise.

- [x] 10. Create README.md documentation
  - [x] 10.1 Write README.md with project overview
    - Project description, architecture overview, technology stack
    - Prerequisites (ASP.NET Core 8, Oracle database, .NET SDK)
    - Setup instructions (database setup, configuration, running the API)
    - API endpoints documentation with examples
    - Authentication flow with JWT token usage
    - Testing instructions (unit tests, property tests)
    - Project structure explanation
    - Contributing guidelines

## Notes

- Tasks marked with `*` are optional and can be skipped for faster MVP
- Each task references specific requirements for traceability
- Checkpoints ensure incremental validation at major milestones
- Property tests validate universal correctness properties with minimum 100 iterations
- Unit tests validate specific examples, edge cases, and integration points
- Implementation follows Clean Architecture with strict dependency rules
- All database operations use Oracle stored procedures via ADO.NET
- All API responses wrapped in ApiResponse for consistency
- JWT Bearer authentication with role-based authorization throughout
- Comprehensive logging with Serilog for observability
