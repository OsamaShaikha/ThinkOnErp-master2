# ThinkOnErp API

A production-ready ASP.NET Core 8 Web API implementing an Enterprise Resource Planning system using Clean Architecture principles, CQRS pattern with MediatR, JWT Bearer authentication, and Oracle database with stored procedures.

## Overview

ThinkOnErp API provides secure, role-based access to manage organizational data including roles, currencies, companies, branches, and users through a RESTful API. The system implements soft delete patterns, comprehensive validation, structured logging, and property-based testing for correctness guarantees.

## Architecture

### Clean Architecture Layers

The system follows Clean Architecture with strict dependency rules:

- **API Layer** (Presentation): Controllers, middleware, Program.cs configuration
- **Application Layer** (CQRS): MediatR commands/queries, DTOs, validators, pipeline behaviors
- **Domain Layer** (Core): Entities and repository interfaces with zero external dependencies
- **Infrastructure Layer** (Data): ADO.NET repositories, Oracle database access, JWT token service

### Technology Stack

- **Framework**: ASP.NET Core 8
- **Database**: Oracle with stored procedures
- **Authentication**: JWT Bearer tokens with SHA-256 password hashing
- **CQRS**: MediatR for command/query separation
- **Validation**: FluentValidation with pipeline integration
- **Logging**: Serilog with console and file sinks
- **Data Access**: ADO.NET with Oracle.ManagedDataAccess.Core
- **Testing**: xUnit, FsCheck (property-based testing), Moq

## Prerequisites

- .NET 8 SDK or later
- Oracle Database 11g or later
- Oracle Client libraries (Oracle.ManagedDataAccess.Core handles this automatically)
- Visual Studio 2022 / VS Code / Rider (optional)

## Setup Instructions

### 1. Database Setup

Execute the SQL scripts in the `Database/Scripts` directory in order:

```bash
# Connect to your Oracle database and run:
sqlplus THINKONERP/oracle123@localhost:1521/XEPDB1

# Execute scripts in order:
@Database/Scripts/01_Create_Sequences.sql
@Database/Scripts/02_Create_SYS_ROLE_Procedures.sql
@Database/Scripts/03_Create_SYS_CURRENCY_Procedures.sql
@Database/Scripts/04_Create_SYS_BRANCH_Procedures.sql
@Database/Scripts/05_Create_SYS_USERS_Procedures.sql
@Database/Scripts/06_Insert_Test_Data.sql
@Database/Scripts/07_Add_RefreshToken_To_Users.sql
```

### 2. Configuration

Update `appsettings.json` with your Oracle connection string and JWT settings:

```json
{
  "ConnectionStrings": {
    "OracleDb": "Data Source=localhost:1521/XEPDB1;User Id=THINKONERP;Password=oracle123;"
  },
  "JwtSettings": {
    "SecretKey": "YourSecretKeyHere-MustBe32CharactersOrMore",
    "Issuer": "ThinkOnErpAPI",
    "Audience": "ThinkOnErpClient",
    "ExpiryInMinutes": 60,
    "RefreshTokenExpiryInDays": 7
  },
  "Serilog": {
    "MinimumLevel": {
      "Default": "Information"
    }
  }
}
```

### 3. Running the API

```bash
# Restore dependencies
dotnet restore

# Build the solution
dotnet build

# Run the API
dotnet run --project src/ThinkOnErp.API

# The API will be available at:
# - HTTP: http://localhost:5000
# - HTTPS: https://localhost:5001
# - Swagger UI: https://localhost:5001/swagger (Development only)
```

## API Endpoints

### Authentication

- `POST /api/auth/login` - Authenticate and receive JWT token (no authorization required)
- `POST /api/auth/refresh` - Refresh access token using refresh token (no authorization required)

For detailed refresh token documentation, see [docs/REFRESH_TOKEN_API.md](docs/REFRESH_TOKEN_API.md).

### Roles (Admin-only for CUD operations)

- `GET /api/roles` - Get all active roles (requires authentication)
- `GET /api/roles/{id}` - Get role by ID (requires authentication)
- `POST /api/roles` - Create new role (requires admin)
- `PUT /api/roles/{id}` - Update role (requires admin)
- `DELETE /api/roles/{id}` - Soft delete role (requires admin)

### Currencies (Admin-only for CUD operations)

- `GET /api/currencies` - Get all active currencies
- `GET /api/currencies/{id}` - Get currency by ID
- `POST /api/currencies` - Create new currency (requires admin)
- `PUT /api/currencies/{id}` - Update currency (requires admin)
- `DELETE /api/currencies/{id}` - Soft delete currency (requires admin)

### Companies (Admin-only for CUD operations)

- `GET /api/companies` - Get all active companies
- `GET /api/companies/{id}` - Get company by ID
- `POST /api/companies` - Create new company (requires admin)
- `PUT /api/companies/{id}` - Update company (requires admin)
- `DELETE /api/companies/{id}` - Soft delete company (requires admin)

### Branches (Admin-only for CUD operations)

- `GET /api/branches` - Get all active branches
- `GET /api/branches/{id}` - Get branch by ID
- `POST /api/branches` - Create new branch (requires admin)
- `PUT /api/branches/{id}` - Update branch (requires admin)
- `DELETE /api/branches/{id}` - Soft delete branch (requires admin)

### Users (All admin-only except change password)

- `GET /api/users` - Get all active users (requires admin)
- `GET /api/users/{id}` - Get user by ID (requires admin)
- `POST /api/users` - Create new user (requires admin)
- `PUT /api/users/{id}` - Update user (requires admin)
- `DELETE /api/users/{id}` - Soft delete user (requires admin)
- `PUT /api/users/{id}/change-password` - Change user password (requires authentication)

## Authentication Flow

### 1. Login

```bash
POST /api/auth/login
Content-Type: application/json

{
  "userName": "admin",
  "password": "admin123"
}
```

Response:
```json
{
  "success": true,
  "statusCode": 200,
  "message": "Login successful",
  "data": {
    "accessToken": "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9...",
    "expiresAt": "2024-01-01T12:00:00Z",
    "tokenType": "Bearer"
  },
  "timestamp": "2024-01-01T11:00:00Z",
  "traceId": "abc123..."
}
```

### 2. Using the Token

Include the token in the Authorization header for subsequent requests:

```bash
GET /api/roles
Authorization: Bearer eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9...
```

### 3. Swagger UI Authentication

1. Navigate to https://localhost:5001/swagger
2. Click the "Authorize" button
3. Enter: `Bearer {your-token-here}`
4. Click "Authorize"
5. All subsequent requests will include the token

## Testing

### Running Unit Tests

```bash
# Run all tests
dotnet test

# Run tests with detailed output
dotnet test --verbosity normal

# Run tests for specific project
dotnet test tests/ThinkOnErp.API.Tests
dotnet test tests/ThinkOnErp.Infrastructure.Tests
```

### Property-Based Tests

The project includes comprehensive property-based tests using FsCheck that validate universal correctness properties:

- JWT token structure and claims
- Authentication and authorization flows
- Password hashing with SHA-256
- CRUD operations (Create, Read, Update, Delete)
- Soft delete behavior
- API response structure
- Validation error handling
- Exception middleware behavior
- Data type mapping (Oracle ↔ C#)
- Message format consistency

Property tests run 100 iterations with randomly generated inputs to ensure correctness across all valid scenarios.

### Unit Tests

Unit tests cover specific scenarios and edge cases:

- Authentication scenarios (valid/invalid credentials, inactive users)
- Validation edge cases (empty fields, length limits, negative values)
- Repository operations (CRUD, null handling)
- Password hashing (consistency, format, special characters)
- ApiResponse wrapper (success/failure, timestamps, trace IDs)
- Exception middleware (ValidationException, generic exceptions, JSON format)
- Authorization policies (admin-only, protected endpoints)
- MediatR pipeline behaviors (logging, validation order)
- Data type mapping (Oracle types to C# types)
- End-to-end integration flows

## Project Structure

```
ThinkOnErp/
├── src/
│   ├── ThinkOnErp.API/              # API Layer
│   │   ├── Controllers/             # REST API controllers
│   │   ├── Middleware/              # Exception handling middleware
│   │   └── Program.cs               # Application entry point
│   ├── ThinkOnErp.Application/      # Application Layer
│   │   ├── Common/                  # ApiResponse wrapper
│   │   ├── DTOs/                    # Data transfer objects
│   │   ├── Features/                # CQRS commands and queries
│   │   └── Behaviors/               # MediatR pipeline behaviors
│   ├── ThinkOnErp.Domain/           # Domain Layer
│   │   ├── Entities/                # Domain entities
│   │   └── Interfaces/              # Repository interfaces
│   └── ThinkOnErp.Infrastructure/   # Infrastructure Layer
│       ├── Data/                    # OracleDbContext
│       ├── Repositories/            # ADO.NET repository implementations
│       └── Services/                # JWT token and password hashing services
├── tests/
│   ├── ThinkOnErp.API.Tests/        # API layer tests
│   └── ThinkOnErp.Infrastructure.Tests/  # Infrastructure layer tests
├── Database/
│   └── Scripts/                     # Oracle SQL scripts
└── README.md
```

## Key Features

### Security

- JWT Bearer authentication with configurable expiration
- SHA-256 password hashing
- Role-based authorization (Admin vs. Regular users)
- Protected endpoints with [Authorize] attribute
- AdminOnly policy for administrative operations

### Data Management

- Soft delete pattern (IS_ACTIVE flag)
- Audit trail fields (CreationUser, CreationDate, UpdateUser, UpdateDate)
- Oracle sequences for ID generation
- Stored procedures for all database operations
- SYS_REFCURSOR for result sets

### API Design

- Unified ApiResponse wrapper for all endpoints
- Consistent error handling with trace IDs
- Validation error details in errors array
- ISO 8601 timestamps
- Professional error messages

### Validation

- FluentValidation for request validation
- Automatic validation in MediatR pipeline
- Validation errors collected before throwing
- 400 Bad Request for validation failures

### Logging

- Serilog with structured logging
- Console sink with colored output
- File sink with daily rolling logs
- Request/response logging in MediatR pipeline
- Exception logging with full details
- Enriched with LogContext, MachineName, ThreadId

### Testing

- Property-based testing with FsCheck (100+ iterations per property)
- Unit tests for specific scenarios
- Integration tests for end-to-end flows
- Test coverage for all correctness properties
- Mocked dependencies for isolated testing

## Contributing

1. Follow Clean Architecture principles
2. Maintain strict dependency rules (Domain → Application → Infrastructure → API)
3. Write property-based tests for universal properties
4. Write unit tests for specific scenarios
5. Use FluentValidation for all input validation
6. Follow RESTful API conventions
7. Include XML documentation comments
8. Use Serilog for all logging
9. Never expose domain entities directly through API (use DTOs)
10. Always use stored procedures for database operations

## License

This project is proprietary software. All rights reserved.

## Support

For issues, questions, or contributions, please contact the development team.
