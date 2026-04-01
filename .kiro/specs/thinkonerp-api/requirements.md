# Requirements Document

## Introduction

ThinkOnErp API is a production-ready ASP.NET Core 8 Web API implementing an Enterprise Resource Planning system with Clean Architecture. The system provides secure, role-based access to manage organizational data including roles, currencies, companies, branches, and users through a RESTful API backed by Oracle database with stored procedures.

## Glossary

- **API_Layer**: The presentation layer containing controllers, middleware, and configuration
- **Application_Layer**: The CQRS layer containing MediatR commands, queries, DTOs, and validators
- **Domain_Layer**: The core business layer containing entities and interfaces with zero external dependencies
- **Infrastructure_Layer**: The data access layer implementing ADO.NET with Oracle stored procedures
- **Auth_Service**: The authentication service responsible for JWT token generation and validation
- **Repository**: A data access component that executes Oracle stored procedures
- **MediatR_Pipeline**: The request processing pipeline that handles commands and queries
- **Validation_Behavior**: A pipeline behavior that validates requests using FluentValidation
- **Logging_Behavior**: A pipeline behavior that logs all MediatR requests and responses
- **Exception_Middleware**: Global middleware that catches and formats all unhandled exceptions
- **ApiResponse**: A unified response wrapper for all API endpoints
- **Stored_Procedure**: An Oracle PL/SQL procedure for database operations
- **JWT_Token**: JSON Web Token used for authentication and authorization
- **Admin_User**: A user with IS_ADMIN flag set to true
- **Active_Record**: A database record with IS_ACTIVE flag set to true
- **Soft_Delete**: Setting IS_ACTIVE to false instead of physical deletion

## Requirements

### Requirement 1: Clean Architecture Structure

**User Story:** As a developer, I want the system organized in Clean Architecture layers, so that the codebase is maintainable and testable

#### Acceptance Criteria

1. THE API_Layer SHALL contain controllers, middleware, and Program.cs configuration
2. THE Application_Layer SHALL contain CQRS commands, queries, DTOs, validators, and pipeline behaviors
3. THE Domain_Layer SHALL contain entities and interfaces with zero external package dependencies
4. THE Infrastructure_Layer SHALL contain ADO.NET repositories and Oracle database access implementations
5. THE Domain_Layer SHALL NOT reference any other layer
6. THE Application_Layer SHALL reference only the Domain_Layer
7. THE Infrastructure_Layer SHALL reference the Domain_Layer and Application_Layer
8. THE API_Layer SHALL reference all other layers for dependency injection configuration

### Requirement 2: JWT Bearer Authentication

**User Story:** As a system administrator, I want secure JWT-based authentication, so that only authorized users can access the API

#### Acceptance Criteria

1. WHEN a user submits valid credentials to the login endpoint, THE Auth_Service SHALL generate a JWT_Token containing userId, userName, role, branchId, and isAdmin claims
2. WHEN a user submits invalid credentials, THE Auth_Service SHALL return an error response with status code 401
3. WHEN a JWT_Token is generated, THE Auth_Service SHALL sign it using the SecretKey from configuration
4. WHEN a JWT_Token is generated, THE Auth_Service SHALL set expiration time according to ExpiryInMinutes from configuration
5. THE Auth_Service SHALL include Issuer and Audience claims matching the configuration values
6. WHEN a request includes a valid JWT_Token, THE API_Layer SHALL authenticate the user and populate the security context
7. WHEN a request includes an invalid or expired JWT_Token, THE API_Layer SHALL return status code 401

### Requirement 3: Password Security

**User Story:** As a security officer, I want passwords hashed using SHA-256, so that credentials are protected

#### Acceptance Criteria

1. WHEN a user password is stored, THE Infrastructure_Layer SHALL hash it using SHA-256 and store the hexadecimal string representation
2. WHEN a user attempts to login, THE Auth_Service SHALL hash the provided password using SHA-256 before comparison
3. WHEN comparing passwords, THE Auth_Service SHALL compare the SHA-256 hexadecimal hash values
4. THE Infrastructure_Layer SHALL NOT store passwords in plain text

### Requirement 4: Role-Based Authorization

**User Story:** As a system administrator, I want role-based access control, so that users can only perform authorized operations

#### Acceptance Criteria

1. THE API_Layer SHALL protect all endpoints with the Authorize attribute except the login endpoint
2. WHERE an endpoint requires administrator privileges, THE API_Layer SHALL apply the AdminOnly authorization policy
3. WHEN a non-admin user attempts to access an admin-only endpoint, THE API_Layer SHALL return status code 403
4. WHEN an unauthenticated user attempts to access a protected endpoint, THE API_Layer SHALL return status code 401
5. THE AdminOnly policy SHALL verify the isAdmin claim in the JWT_Token equals true

### Requirement 5: Oracle Stored Procedure Data Access

**User Story:** As a database administrator, I want all database operations through stored procedures, so that data access is controlled and auditable

#### Acceptance Criteria

1. THE Infrastructure_Layer SHALL use ADO.NET with Oracle.ManagedDataAccess.Core for all database operations
2. THE Infrastructure_Layer SHALL NOT use Entity Framework or Dapper
3. FOR ALL database operations, THE Repository SHALL call the corresponding Oracle stored procedure
4. WHEN executing a stored procedure, THE Repository SHALL use OracleCommand with CommandType.StoredProcedure
5. WHEN executing a stored procedure, THE Repository SHALL add OracleParameter objects with explicit OracleDbType values
6. FOR SELECT operations, THE Repository SHALL use OracleDataReader to map results to entities
7. FOR INSERT, UPDATE, and DELETE operations, THE Repository SHALL use ExecuteNonQuery
8. THE Repository SHALL use SYS_REFCURSOR output parameters with OracleDbType.RefCursor for result sets

### Requirement 6: CRUD Operations for Roles

**User Story:** As an administrator, I want to manage system roles, so that I can control user permissions

#### Acceptance Criteria

1. WHEN retrieving all roles, THE Application_Layer SHALL execute GetAllRolesQuery returning all Active_Record entries
2. WHEN retrieving a role by ID, THE Application_Layer SHALL execute GetRoleByIdQuery returning the matching role or null
3. WHEN creating a role, THE Application_Layer SHALL execute CreateRoleCommand with validation and return the new role ID
4. WHEN updating a role, THE Application_Layer SHALL execute UpdateRoleCommand with validation and return success status
5. WHEN deleting a role, THE Application_Layer SHALL execute DeleteRoleCommand performing a Soft_Delete
6. THE Repository SHALL call SP_SYS_ROLE_SELECT_ALL for retrieving all roles
7. THE Repository SHALL call SP_SYS_ROLE_SELECT_BY_ID for retrieving a role by ID
8. THE Repository SHALL call SP_SYS_ROLE_INSERT for creating a role
9. THE Repository SHALL call SP_SYS_ROLE_UPDATE for updating a role
10. THE Repository SHALL call SP_SYS_ROLE_DELETE for deleting a role

### Requirement 7: CRUD Operations for Currency

**User Story:** As an administrator, I want to manage currencies, so that the system supports multi-currency operations

#### Acceptance Criteria

1. WHEN retrieving all currencies, THE Application_Layer SHALL execute GetAllCurrenciesQuery returning all Active_Record entries
2. WHEN retrieving a currency by ID, THE Application_Layer SHALL execute GetCurrencyByIdQuery returning the matching currency or null
3. WHEN creating a currency, THE Application_Layer SHALL execute CreateCurrencyCommand with validation and return the new currency ID
4. WHEN updating a currency, THE Application_Layer SHALL execute UpdateCurrencyCommand with validation and return success status
5. WHEN deleting a currency, THE Application_Layer SHALL execute DeleteCurrencyCommand performing a Soft_Delete
6. THE Repository SHALL call SP_SYS_CURRENCY_SELECT_ALL for retrieving all currencies
7. THE Repository SHALL call SP_SYS_CURRENCY_SELECT_BY_ID for retrieving a currency by ID
8. THE Repository SHALL call SP_SYS_CURRENCY_INSERT for creating a currency
9. THE Repository SHALL call SP_SYS_CURRENCY_UPDATE for updating a currency
10. THE Repository SHALL call SP_SYS_CURRENCY_DELETE for deleting a currency

### Requirement 8: CRUD Operations for Company

**User Story:** As an administrator, I want to manage companies, so that the system supports multi-company operations

#### Acceptance Criteria

1. WHEN retrieving all companies, THE Application_Layer SHALL execute GetAllCompaniesQuery returning all Active_Record entries
2. WHEN retrieving a company by ID, THE Application_Layer SHALL execute GetCompanyByIdQuery returning the matching company or null
3. WHEN creating a company, THE Application_Layer SHALL execute CreateCompanyCommand with validation and return the new company ID
4. WHEN updating a company, THE Application_Layer SHALL execute UpdateCompanyCommand with validation and return success status
5. WHEN deleting a company, THE Application_Layer SHALL execute DeleteCompanyCommand performing a Soft_Delete
6. THE Repository SHALL call SP_SYS_COMPANY_SELECT_ALL for retrieving all companies
7. THE Repository SHALL call SP_SYS_COMPANY_SELECT_BY_ID for retrieving a company by ID
8. THE Repository SHALL call SP_SYS_COMPANY_INSERT for creating a company
9. THE Repository SHALL call SP_SYS_COMPANY_UPDATE for updating a company
10. THE Repository SHALL call SP_SYS_COMPANY_DELETE for deleting a company

### Requirement 9: CRUD Operations for Branch

**User Story:** As an administrator, I want to manage branches, so that the system supports multi-branch operations

#### Acceptance Criteria

1. WHEN retrieving all branches, THE Application_Layer SHALL execute GetAllBranchesQuery returning all Active_Record entries
2. WHEN retrieving a branch by ID, THE Application_Layer SHALL execute GetBranchByIdQuery returning the matching branch or null
3. WHEN creating a branch, THE Application_Layer SHALL execute CreateBranchCommand with validation and return the new branch ID
4. WHEN updating a branch, THE Application_Layer SHALL execute UpdateBranchCommand with validation and return success status
5. WHEN deleting a branch, THE Application_Layer SHALL execute DeleteBranchCommand performing a Soft_Delete
6. THE Repository SHALL call SP_SYS_BRANCH_SELECT_ALL for retrieving all branches
7. THE Repository SHALL call SP_SYS_BRANCH_SELECT_BY_ID for retrieving a branch by ID
8. THE Repository SHALL call SP_SYS_BRANCH_INSERT for creating a branch
9. THE Repository SHALL call SP_SYS_BRANCH_UPDATE for updating a branch
10. THE Repository SHALL call SP_SYS_BRANCH_DELETE for deleting a branch

### Requirement 10: CRUD Operations for Users

**User Story:** As an administrator, I want to manage users, so that I can control system access

#### Acceptance Criteria

1. WHEN retrieving all users, THE Application_Layer SHALL execute GetAllUsersQuery returning all Active_Record entries
2. WHEN retrieving a user by ID, THE Application_Layer SHALL execute GetUserByIdQuery returning the matching user or null
3. WHEN creating a user, THE Application_Layer SHALL execute CreateUserCommand with validation and return the new user ID
4. WHEN updating a user, THE Application_Layer SHALL execute UpdateUserCommand with validation and return success status
5. WHEN deleting a user, THE Application_Layer SHALL execute DeleteUserCommand performing a Soft_Delete
6. WHEN a user changes their password, THE Application_Layer SHALL execute ChangePasswordCommand with validation
7. THE Repository SHALL call SP_SYS_USERS_SELECT_ALL for retrieving all users
8. THE Repository SHALL call SP_SYS_USERS_SELECT_BY_ID for retrieving a user by ID
9. THE Repository SHALL call SP_SYS_USERS_INSERT for creating a user
10. THE Repository SHALL call SP_SYS_USERS_UPDATE for updating a user
11. THE Repository SHALL call SP_SYS_USERS_DELETE for deleting a user

### Requirement 11: Unified API Response Format

**User Story:** As a frontend developer, I want consistent response format from all endpoints, so that I can handle responses uniformly

#### Acceptance Criteria

1. THE API_Layer SHALL wrap all successful responses in ApiResponse with success set to true
2. THE API_Layer SHALL wrap all error responses in ApiResponse with success set to false
3. THE ApiResponse SHALL include statusCode matching the HTTP status code
4. THE ApiResponse SHALL include a descriptive message field
5. THE ApiResponse SHALL include a data field containing the response payload or null
6. THE ApiResponse SHALL include a timestamp field with ISO 8601 format
7. THE ApiResponse SHALL include a traceId field for request correlation
8. WHEN validation fails, THE ApiResponse SHALL include an errors array with all validation messages
9. THE API_Layer SHALL return ActionResult with ApiResponse wrapper for all controller actions
10. THE ApiResponse SHALL provide static factory methods Success and Fail for creating responses

### Requirement 12: Request Validation

**User Story:** As a developer, I want automatic request validation, so that invalid data is rejected before processing

#### Acceptance Criteria

1. THE Application_Layer SHALL define FluentValidation validators for all commands and queries with input parameters
2. THE Validation_Behavior SHALL intercept all MediatR requests in the pipeline
3. WHEN a request has validation rules, THE Validation_Behavior SHALL execute all validators
4. WHEN validation fails, THE Validation_Behavior SHALL throw ValidationException with all error messages
5. WHEN ValidationException is thrown, THE Exception_Middleware SHALL convert it to ApiResponse with status code 400
6. THE Validation_Behavior SHALL collect all validation errors before throwing the exception
7. THE Validation_Behavior SHALL execute before the request handler

### Requirement 13: Structured Logging with Serilog

**User Story:** As a system operator, I want structured logging, so that I can monitor and troubleshoot the system

#### Acceptance Criteria

1. THE API_Layer SHALL configure Serilog before building the application host
2. THE API_Layer SHALL replace Microsoft default logging with Serilog
3. THE API_Layer SHALL configure console sink with colored output
4. THE API_Layer SHALL configure file sink with daily rolling logs in the logs directory
5. THE API_Layer SHALL set minimum log level to Information for production
6. THE API_Layer SHALL set minimum log level to Debug for development
7. THE API_Layer SHALL enrich logs with LogContext, MachineName, and ThreadId
8. THE Logging_Behavior SHALL log all MediatR requests and responses
9. THE Exception_Middleware SHALL log all unhandled exceptions at Error level
10. THE API_Layer SHALL use log format with timestamp, level, source context, message, and exception

### Requirement 14: Global Exception Handling

**User Story:** As a security officer, I want centralized exception handling, so that error details are not exposed to clients

#### Acceptance Criteria

1. THE Exception_Middleware SHALL catch all unhandled exceptions from the request pipeline
2. WHEN an unhandled exception occurs, THE Exception_Middleware SHALL log it with full details
3. WHEN an unhandled exception occurs, THE Exception_Middleware SHALL return ApiResponse with status code 500
4. THE Exception_Middleware SHALL NOT include stack traces in the response
5. THE Exception_Middleware SHALL include a generic error message for unhandled exceptions
6. WHEN ValidationException occurs, THE Exception_Middleware SHALL return status code 400 with validation errors
7. THE Exception_Middleware SHALL set the response content type to application/json

### Requirement 15: Dependency Injection Configuration

**User Story:** As a developer, I want centralized dependency injection, so that services are properly registered and scoped

#### Acceptance Criteria

1. THE Infrastructure_Layer SHALL provide AddInfrastructure extension method for service registration
2. THE Application_Layer SHALL provide AddApplication extension method for service registration
3. THE AddInfrastructure method SHALL register all repositories as Scoped services
4. THE AddInfrastructure method SHALL register OracleDbContext as Scoped service
5. THE AddInfrastructure method SHALL register Auth_Service as Scoped service
6. THE AddApplication method SHALL register MediatR scanning the Application assembly
7. THE AddApplication method SHALL register FluentValidation scanning the Application assembly
8. THE AddApplication method SHALL register Validation_Behavior as a pipeline behavior
9. THE AddApplication method SHALL register Logging_Behavior as a pipeline behavior
10. THE API_Layer SHALL call AddApplication and AddInfrastructure in Program.cs

### Requirement 16: Swagger Documentation

**User Story:** As an API consumer, I want interactive API documentation, so that I can explore and test endpoints

#### Acceptance Criteria

1. WHERE the environment is Development, THE API_Layer SHALL enable Swagger UI
2. THE API_Layer SHALL configure Swagger with JWT Bearer security definition
3. THE API_Layer SHALL display all controller routes in Swagger UI
4. THE API_Layer SHALL generate request and response schemas from DTOs
5. THE API_Layer SHALL include XML documentation comments in Swagger schemas
6. THE Swagger UI SHALL provide an Authorize button for entering JWT tokens
7. WHEN a JWT_Token is entered, THE Swagger UI SHALL include it in the Authorization header for test requests

### Requirement 17: Oracle Stored Procedures for Roles

**User Story:** As a database administrator, I want stored procedures for role operations, so that data access is standardized

#### Acceptance Criteria

1. THE Stored_Procedure SP_SYS_ROLE_SELECT_ALL SHALL return all roles with IS_ACTIVE equals true via SYS_REFCURSOR
2. THE Stored_Procedure SP_SYS_ROLE_SELECT_BY_ID SHALL accept P_ROW_ID parameter and return matching role via SYS_REFCURSOR
3. THE Stored_Procedure SP_SYS_ROLE_INSERT SHALL accept all role fields and generate ROW_ID using SEQ_SYS_ROLE sequence
4. THE Stored_Procedure SP_SYS_ROLE_INSERT SHALL set CREATION_USER and CREATION_DATE automatically
5. THE Stored_Procedure SP_SYS_ROLE_UPDATE SHALL accept P_ROW_ID and updated fields
6. THE Stored_Procedure SP_SYS_ROLE_UPDATE SHALL set UPDATE_USER and UPDATE_DATE automatically
7. THE Stored_Procedure SP_SYS_ROLE_DELETE SHALL accept P_ROW_ID and set IS_ACTIVE to false
8. THE Stored_Procedure SHALL handle exceptions and raise errors appropriately

### Requirement 18: Oracle Stored Procedures for Currency

**User Story:** As a database administrator, I want stored procedures for currency operations, so that data access is standardized

#### Acceptance Criteria

1. THE Stored_Procedure SP_SYS_CURRENCY_SELECT_ALL SHALL return all currencies with IS_ACTIVE equals true via SYS_REFCURSOR
2. THE Stored_Procedure SP_SYS_CURRENCY_SELECT_BY_ID SHALL accept P_ROW_ID parameter and return matching currency via SYS_REFCURSOR
3. THE Stored_Procedure SP_SYS_CURRENCY_INSERT SHALL accept all currency fields and generate ROW_ID using SEQ_SYS_CURRENCY sequence
4. THE Stored_Procedure SP_SYS_CURRENCY_INSERT SHALL set CREATION_USER and CREATION_DATE automatically
5. THE Stored_Procedure SP_SYS_CURRENCY_UPDATE SHALL accept P_ROW_ID and updated fields
6. THE Stored_Procedure SP_SYS_CURRENCY_UPDATE SHALL set UPDATE_USER and UPDATE_DATE automatically
7. THE Stored_Procedure SP_SYS_CURRENCY_DELETE SHALL accept P_ROW_ID and set IS_ACTIVE to false
8. THE Stored_Procedure SHALL handle exceptions and raise errors appropriately

### Requirement 19: Oracle Stored Procedures for Company

**User Story:** As a database administrator, I want stored procedures for company operations, so that data access is standardized

#### Acceptance Criteria

1. THE Stored_Procedure SP_SYS_COMPANY_SELECT_ALL SHALL return all companies with IS_ACTIVE equals true via SYS_REFCURSOR
2. THE Stored_Procedure SP_SYS_COMPANY_SELECT_BY_ID SHALL accept P_ROW_ID parameter and return matching company via SYS_REFCURSOR
3. THE Stored_Procedure SP_SYS_COMPANY_INSERT SHALL accept all company fields and generate ROW_ID using SEQ_SYS_COMPANY sequence
4. THE Stored_Procedure SP_SYS_COMPANY_INSERT SHALL set CREATION_USER and CREATION_DATE automatically
5. THE Stored_Procedure SP_SYS_COMPANY_UPDATE SHALL accept P_ROW_ID and updated fields
6. THE Stored_Procedure SP_SYS_COMPANY_UPDATE SHALL set UPDATE_USER and UPDATE_DATE automatically
7. THE Stored_Procedure SP_SYS_COMPANY_DELETE SHALL accept P_ROW_ID and set IS_ACTIVE to false
8. THE Stored_Procedure SHALL handle exceptions and raise errors appropriately

### Requirement 20: Oracle Stored Procedures for Branch

**User Story:** As a database administrator, I want stored procedures for branch operations, so that data access is standardized

#### Acceptance Criteria

1. THE Stored_Procedure SP_SYS_BRANCH_SELECT_ALL SHALL return all branches with IS_ACTIVE equals true via SYS_REFCURSOR
2. THE Stored_Procedure SP_SYS_BRANCH_SELECT_BY_ID SHALL accept P_ROW_ID parameter and return matching branch via SYS_REFCURSOR
3. THE Stored_Procedure SP_SYS_BRANCH_INSERT SHALL accept all branch fields and generate ROW_ID using SEQ_SYS_BRANCH sequence
4. THE Stored_Procedure SP_SYS_BRANCH_INSERT SHALL set CREATION_USER and CREATION_DATE automatically
5. THE Stored_Procedure SP_SYS_BRANCH_UPDATE SHALL accept P_ROW_ID and updated fields
6. THE Stored_Procedure SP_SYS_BRANCH_UPDATE SHALL set UPDATE_USER and UPDATE_DATE automatically
7. THE Stored_Procedure SP_SYS_BRANCH_DELETE SHALL accept P_ROW_ID and set IS_ACTIVE to false
8. THE Stored_Procedure SHALL handle exceptions and raise errors appropriately

### Requirement 21: Oracle Stored Procedures for Users

**User Story:** As a database administrator, I want stored procedures for user operations, so that data access is standardized

#### Acceptance Criteria

1. THE Stored_Procedure SP_SYS_USERS_SELECT_ALL SHALL return all users with IS_ACTIVE equals true via SYS_REFCURSOR
2. THE Stored_Procedure SP_SYS_USERS_SELECT_BY_ID SHALL accept P_ROW_ID parameter and return matching user via SYS_REFCURSOR
3. THE Stored_Procedure SP_SYS_USERS_INSERT SHALL accept all user fields and generate ROW_ID using SEQ_SYS_USERS sequence
4. THE Stored_Procedure SP_SYS_USERS_INSERT SHALL set CREATION_USER and CREATION_DATE automatically
5. THE Stored_Procedure SP_SYS_USERS_UPDATE SHALL accept P_ROW_ID and updated fields
6. THE Stored_Procedure SP_SYS_USERS_UPDATE SHALL set UPDATE_USER and UPDATE_DATE automatically
7. THE Stored_Procedure SP_SYS_USERS_DELETE SHALL accept P_ROW_ID and set IS_ACTIVE to false
8. THE Stored_Procedure SP_SYS_USERS_LOGIN SHALL accept P_USER_NAME and P_PASSWORD parameters
9. THE Stored_Procedure SP_SYS_USERS_LOGIN SHALL return user record via SYS_REFCURSOR when credentials match and IS_ACTIVE equals true
10. THE Stored_Procedure SP_SYS_USERS_LOGIN SHALL return empty cursor when credentials do not match or user is inactive
11. THE Stored_Procedure SHALL handle exceptions and raise errors appropriately

### Requirement 22: Database Connection Management

**User Story:** As a developer, I want proper connection management, so that database resources are not leaked

#### Acceptance Criteria

1. THE Infrastructure_Layer SHALL read the Oracle connection string from ConnectionStrings:OracleDb configuration
2. THE Repository SHALL create a new OracleConnection for each database operation
3. THE Repository SHALL open the connection before executing commands
4. THE Repository SHALL dispose the connection after operation completion using using statements
5. THE Repository SHALL dispose OracleCommand objects using using statements
6. THE Repository SHALL dispose OracleDataReader objects using using statements
7. WHEN a database exception occurs, THE Repository SHALL log the exception and rethrow as a domain exception

### Requirement 23: Data Type Mapping

**User Story:** As a developer, I want consistent data type mapping between Oracle and C#, so that data integrity is maintained

#### Acceptance Criteria

1. THE Repository SHALL map Oracle NUMBER columns to C# decimal type for ROW_ID fields
2. THE Repository SHALL map Oracle VARCHAR2 columns to C# string type
3. THE Repository SHALL map Oracle DATE columns to C# nullable DateTime type
4. THE Repository SHALL map Oracle IS_ACTIVE values of Y or 1 to C# true
5. THE Repository SHALL map Oracle IS_ACTIVE values of N or 0 to C# false
6. THE Repository SHALL use OracleDbType.Decimal for decimal parameters
7. THE Repository SHALL use OracleDbType.Varchar2 for string parameters
8. THE Repository SHALL use OracleDbType.Date for DateTime parameters

### Requirement 24: API Endpoint Routes

**User Story:** As an API consumer, I want RESTful endpoint routes, so that the API follows standard conventions

#### Acceptance Criteria

1. THE API_Layer SHALL expose POST /api/auth/login for authentication without authorization
2. THE API_Layer SHALL expose GET /api/roles for retrieving all roles with authorization
3. THE API_Layer SHALL expose GET /api/roles/{id} for retrieving a role by ID with authorization
4. THE API_Layer SHALL expose POST /api/roles for creating a role with AdminOnly policy
5. THE API_Layer SHALL expose PUT /api/roles/{id} for updating a role with AdminOnly policy
6. THE API_Layer SHALL expose DELETE /api/roles/{id} for deleting a role with AdminOnly policy
7. THE API_Layer SHALL expose GET /api/currencies for retrieving all currencies with authorization
8. THE API_Layer SHALL expose GET /api/currencies/{id} for retrieving a currency by ID with authorization
9. THE API_Layer SHALL expose POST /api/currencies for creating a currency with AdminOnly policy
10. THE API_Layer SHALL expose PUT /api/currencies/{id} for updating a currency with AdminOnly policy
11. THE API_Layer SHALL expose DELETE /api/currencies/{id} for deleting a currency with AdminOnly policy
12. THE API_Layer SHALL expose GET /api/companies for retrieving all companies with authorization
13. THE API_Layer SHALL expose GET /api/companies/{id} for retrieving a company by ID with authorization
14. THE API_Layer SHALL expose POST /api/companies for creating a company with AdminOnly policy
15. THE API_Layer SHALL expose PUT /api/companies/{id} for updating a company with AdminOnly policy
16. THE API_Layer SHALL expose DELETE /api/companies/{id} for deleting a company with AdminOnly policy
17. THE API_Layer SHALL expose GET /api/branches for retrieving all branches with authorization
18. THE API_Layer SHALL expose GET /api/branches/{id} for retrieving a branch by ID with authorization
19. THE API_Layer SHALL expose POST /api/branches for creating a branch with AdminOnly policy
20. THE API_Layer SHALL expose PUT /api/branches/{id} for updating a branch with AdminOnly policy
21. THE API_Layer SHALL expose DELETE /api/branches/{id} for deleting a branch with AdminOnly policy
22. THE API_Layer SHALL expose GET /api/users for retrieving all users with AdminOnly policy
23. THE API_Layer SHALL expose GET /api/users/{id} for retrieving a user by ID with AdminOnly policy
24. THE API_Layer SHALL expose POST /api/users for creating a user with AdminOnly policy
25. THE API_Layer SHALL expose PUT /api/users/{id} for updating a user with AdminOnly policy
26. THE API_Layer SHALL expose DELETE /api/users/{id} for deleting a user with AdminOnly policy
27. THE API_Layer SHALL expose PUT /api/users/{id}/change-password for changing user password with authorization

### Requirement 25: Configuration Management

**User Story:** As a system operator, I want externalized configuration, so that settings can be changed without recompilation

#### Acceptance Criteria

1. THE API_Layer SHALL read Oracle connection string from appsettings.json under ConnectionStrings:OracleDb
2. THE API_Layer SHALL read JWT SecretKey from appsettings.json under JwtSettings:SecretKey
3. THE API_Layer SHALL read JWT Issuer from appsettings.json under JwtSettings:Issuer
4. THE API_Layer SHALL read JWT Audience from appsettings.json under JwtSettings:Audience
5. THE API_Layer SHALL read JWT ExpiryInMinutes from appsettings.json under JwtSettings:ExpiryInMinutes
6. THE API_Layer SHALL read Serilog minimum level from appsettings.json under Serilog:MinimumLevel
7. THE API_Layer SHALL provide appsettings.json template with placeholder values for all required settings

### Requirement 26: Professional Error Messages

**User Story:** As an API consumer, I want clear error messages, so that I can understand and resolve issues

#### Acceptance Criteria

1. WHEN a record is created successfully, THE API_Layer SHALL return message with format "{EntityName} created successfully"
2. WHEN a record is not found, THE API_Layer SHALL return message with format "No {entityName} found with the specified identifier"
3. WHEN authorization fails, THE API_Layer SHALL return message "Access denied. Administrator privileges are required"
4. WHEN authentication fails, THE API_Layer SHALL return message "Invalid credentials. Please verify your username and password"
5. WHEN validation fails, THE API_Layer SHALL return message "One or more validation errors occurred"
6. WHEN an unhandled exception occurs, THE API_Layer SHALL return message "An unexpected error occurred. Please try again later"
7. THE API_Layer SHALL use professional English for all user-facing messages

### Requirement 27: Oracle Database Sequences

**User Story:** As a database administrator, I want sequences for primary key generation, so that IDs are unique and automatically assigned

#### Acceptance Criteria

1. THE Stored_Procedure SHALL use sequence SEQ_SYS_ROLE for generating SYS_ROLE.ROW_ID values
2. THE Stored_Procedure SHALL use sequence SEQ_SYS_CURRENCY for generating SYS_CURRENCY.ROW_ID values
3. THE Stored_Procedure SHALL use sequence SEQ_SYS_COMPANY for generating SYS_COMPANY.ROW_ID values
4. THE Stored_Procedure SHALL use sequence SEQ_SYS_BRANCH for generating SYS_BRANCH.ROW_ID values
5. THE Stored_Procedure SHALL use sequence SEQ_SYS_USERS for generating SYS_USERS.ROW_ID values
6. THE Stored_Procedure SHALL call NEXTVAL on the sequence during INSERT operations

### Requirement 28: Audit Trail Fields

**User Story:** As an auditor, I want automatic audit trail tracking, so that I can see who created and modified records

#### Acceptance Criteria

1. WHEN a record is inserted, THE Stored_Procedure SHALL set CREATION_USER to the current user identifier
2. WHEN a record is inserted, THE Stored_Procedure SHALL set CREATION_DATE to the current timestamp
3. WHEN a record is updated, THE Stored_Procedure SHALL set UPDATE_USER to the current user identifier
4. WHEN a record is updated, THE Stored_Procedure SHALL set UPDATE_DATE to the current timestamp
5. THE Stored_Procedure SHALL NOT modify CREATION_USER or CREATION_DATE during updates

### Requirement 29: MediatR CQRS Pattern

**User Story:** As a developer, I want CQRS with MediatR, so that commands and queries are separated and testable

#### Acceptance Criteria

1. THE Application_Layer SHALL define Command classes for all write operations
2. THE Application_Layer SHALL define Query classes for all read operations
3. THE Application_Layer SHALL define Handler classes implementing IRequestHandler for each command and query
4. THE Application_Layer SHALL send all commands and queries through MediatR
5. THE API_Layer SHALL inject IMediator into controllers
6. THE API_Layer SHALL call mediator.Send for all controller actions
7. THE MediatR_Pipeline SHALL execute Logging_Behavior before Validation_Behavior
8. THE MediatR_Pipeline SHALL execute Validation_Behavior before the request handler

### Requirement 30: DTO Mapping

**User Story:** As a developer, I want DTOs for data transfer, so that internal entities are not exposed to clients

#### Acceptance Criteria

1. THE Application_Layer SHALL define DTO classes for all entities
2. THE Application_Layer SHALL use DTOs in all command and query responses
3. THE Application_Layer SHALL NOT expose domain entities directly through the API
4. THE Handler SHALL map domain entities to DTOs before returning results
5. THE DTO SHALL include XML documentation comments for Swagger schema generation
6. THE DTO SHALL include only the fields required for the specific operation
