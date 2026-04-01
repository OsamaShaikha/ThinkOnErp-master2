# Kiro AI Prompt — ASP.NET Core API Project

> **Usage:** Copy this entire prompt and paste it directly into Kiro AI to scaffold a complete, production-ready ASP.NET Core 8 Web API.

---

## 🏗️ Architecture & Patterns

You are an expert ASP.NET Core architect. Generate a complete, production-ready **ASP.NET Core 8 Web API** project from scratch based on the following specifications. Follow every instruction precisely.

Use **Clean Architecture** with four layers:

1. **API (Presentation)** — Controllers, middleware, configuration
2. **Application** — CQRS with MediatR, commands, queries, DTOs, validators
3. **Domain** — Entities, interfaces, enums (zero external dependencies)
4. **Infrastructure** — ADO.NET data access, Oracle stored procedure calls, repositories

Use the following NuGet packages:

| Package | Purpose |
|---|---|
| `MediatR` (latest) | CQRS mediator |
| `FluentValidation.AspNetCore` | Command/query validation |
| `Serilog.AspNetCore` | Structured logging |
| `Serilog.Sinks.Console` | Console log sink |
| `Serilog.Sinks.File` | File log sink |
| `Microsoft.AspNetCore.Authentication.JwtBearer` | JWT auth |
| `Oracle.ManagedDataAccess.Core` | Oracle database driver |
| `Microsoft.Extensions.DependencyInjection` | Built-in DI |

---

## 🗄️ Database Schema (Oracle)

The Oracle database has these tables — **already created, do NOT generate DDL**:

### `SYS_ROLE`
```
ROW_ID, ROW_DESC, ROW_DESC_E, NOTE, IS_ACTIVE,
CREATION_USER, CREATION_DATE, UPDATE_USER, UPDATE_DATE
```

### `SYS_CURRENCY`
```
ROW_ID, ROW_DESC, ROW_DESC_E, SHORT_DESC, SHOR_DESC_E,
SINGULER_DESC, SINGULER_DESC_E, DUAL_DESC, DUAL_DESC_E,
SUM_DESC, SUM_DESC_E, FRAC_DESC, FRAC_DESC_E,
CURR_RATE, CURR_RATE_DATE,
CREATION_USER, CREATION_DATE, UPDATE_USER, UPDATE_DATE
```

### `SYS_COMPANY`
```
ROW_ID, ROW_DESC, ROW_DESC_E, COUNTRY_ID,
CURR_ID (FK → SYS_CURRENCY), IS_ACTIVE,
CREATION_USER, CREATION_DATE, UPDATE_USER, UPDATE_DATE
```

### `SYS_BRANCH`
```
ROW_ID, PAR_ROW_ID (FK → SYS_COMPANY),
ROW_DESC, ROW_DESC_E, PHONE, MOBILE, FAX, EMAIL,
IS_HEAD_BRANCH, IS_ACTIVE,
CREATION_USER, CREATION_DATE, UPDATE_USER, UPDATE_DATE
```

### `SYS_USERS`
```
ROW_ID, ROW_DESC, ROW_DESC_E, USER_NAME (UNIQUE), PASSWORD,
PHONE, PHONE2, ROLE (FK → SYS_ROLE), BRANCH_ID (FK → SYS_BRANCH),
EMAIL, LAST_LOGIN_DATE, IS_ACTIVE, IS_ADMIN,
CREATION_USER, CREATION_DATE, UPDATE_USER, UPDATE_DATE
```

---

## 📦 Stored Procedures

Generate Oracle PL/SQL stored procedures for every CRUD operation on every table.

**Naming convention:** `SP_<TABLE>_<ACTION>`

For each table generate:

| Procedure | Description |
|---|---|
| `SP_<TABLE>_SELECT_ALL` | Returns all active rows via `SYS_REFCURSOR` |
| `SP_<TABLE>_SELECT_BY_ID` | Returns single row by `ROW_ID` via `SYS_REFCURSOR` |
| `SP_<TABLE>_INSERT` | Inserts new row; auto `ROW_ID` via sequence; sets `CREATION_USER` and `CREATION_DATE` |
| `SP_<TABLE>_UPDATE` | Updates existing row by `ROW_ID`; sets `UPDATE_USER` and `UPDATE_DATE` |
| `SP_<TABLE>_DELETE` | Soft delete — sets `IS_ACTIVE = '0'` (no physical delete) |

### Special Authentication Procedure

```sql
SP_SYS_USERS_LOGIN
  IN  P_USER_NAME     VARCHAR2
  IN  P_PASSWORD      VARCHAR2   -- SHA-256 hashed
  OUT P_RESULT_CURSOR SYS_REFCURSOR
```

Returns user record if credentials match and `IS_ACTIVE = '1'`, else returns empty cursor.

### Stored Procedure Rules

- All parameters prefixed with `P_` (e.g., `P_ROW_ID`, `P_ROW_DESC`)
- Handle exceptions: `EXCEPTION WHEN OTHERS THEN RAISE`
- Use Oracle sequences for `ROW_ID` generation

Create these sequences:

```sql
SEQ_SYS_ROLE
SEQ_SYS_CURRENCY
SEQ_SYS_COMPANY
SEQ_SYS_BRANCH
SEQ_SYS_USERS
```

---

## 📁 Project Structure

Generate this exact folder structure:

```
Solution/
├── src/
│   ├── API/
│   │   ├── Controllers/
│   │   │   ├── AuthController.cs
│   │   │   ├── RolesController.cs
│   │   │   ├── CurrencyController.cs
│   │   │   ├── CompanyController.cs
│   │   │   ├── BranchController.cs
│   │   │   └── UsersController.cs
│   │   ├── Middleware/
│   │   │   └── ExceptionHandlingMiddleware.cs
│   │   ├── Program.cs
│   │   └── appsettings.json
│   │
│   ├── Application/
│   │   ├── Common/
│   │   │   ├── ApiResponse.cs
│   │   │   ├── PaginatedResponse.cs
│   │   │   └── Behaviors/
│   │   │       ├── ValidationBehavior.cs
│   │   │       └── LoggingBehavior.cs
│   │   └── Features/
│   │       ├── Roles/
│   │       │   ├── Commands/
│   │       │   │   ├── CreateRole/   (Command + Handler + Validator)
│   │       │   │   ├── UpdateRole/   (Command + Handler + Validator)
│   │       │   │   └── DeleteRole/   (Command + Handler)
│   │       │   ├── Queries/
│   │       │   │   ├── GetAllRoles/  (Query + Handler)
│   │       │   │   └── GetRoleById/ (Query + Handler)
│   │       │   └── DTOs/RoleDto.cs
│   │       ├── Currency/   (same structure as Roles)
│   │       ├── Company/    (same structure as Roles)
│   │       ├── Branch/     (same structure as Roles)
│   │       ├── Users/      (same structure as Roles + ChangePassword command)
│   │       └── Auth/
│   │           ├── Commands/Login/ (Command + Handler + Validator)
│   │           └── DTOs/           (LoginDto + TokenDto)
│   │
│   ├── Domain/
│   │   ├── Entities/
│   │   │   ├── SysRole.cs
│   │   │   ├── SysCurrency.cs
│   │   │   ├── SysCompany.cs
│   │   │   ├── SysBranch.cs
│   │   │   └── SysUser.cs
│   │   └── Interfaces/
│   │       ├── IRoleRepository.cs
│   │       ├── ICurrencyRepository.cs
│   │       ├── ICompanyRepository.cs
│   │       ├── IBranchRepository.cs
│   │       ├── IUserRepository.cs
│   │       └── IAuthRepository.cs
│   │
│   └── Infrastructure/
│       ├── Data/
│       │   └── OracleDbContext.cs
│       ├── Repositories/
│       │   ├── RoleRepository.cs
│       │   ├── CurrencyRepository.cs
│       │   ├── CompanyRepository.cs
│       │   ├── BranchRepository.cs
│       │   ├── UserRepository.cs
│       │   └── AuthRepository.cs
│       └── DependencyInjection.cs
```

---

## 🔐 JWT Bearer Authentication

- **Login endpoint:** `POST /api/auth/login` with `{ userName, password }`
- On success: return JWT access token with claims: `userId`, `userName`, `role`, `branchId`, `isAdmin`
- Token settings in `appsettings.json`: `Issuer`, `Audience`, `SecretKey`, `ExpiryInMinutes`
- Protect all endpoints with `[Authorize]` except `POST /api/auth/login`
- Admin-only endpoints use `[Authorize(Policy = "AdminOnly")]`
- Generate a `JwtTokenService` in the Infrastructure layer
- Passwords must be stored and compared as **SHA-256 hex hash**

---

## 📡 API Response Format

**ALL responses from every endpoint must use this unified `ApiResponse<T>` wrapper. No raw returns ever.**

### ✅ Success Response

```json
{
  "success": true,
  "statusCode": 200,
  "message": "Roles retrieved successfully.",
  "data": { },
  "timestamp": "2025-01-01T12:00:00Z",
  "traceId": "guid-here"
}
```

### ❌ Error Response

```json
{
  "success": false,
  "statusCode": 400,
  "message": "Validation failed.",
  "errors": [
    "Field X is required.",
    "Field Y must be positive."
  ],
  "timestamp": "2025-01-01T12:00:00Z",
  "traceId": "guid-here"
}
```

### 🔍 Not Found Response

```json
{
  "success": false,
  "statusCode": 404,
  "message": "Role with ID 5 was not found.",
  "data": null,
  "timestamp": "2025-01-01T12:00:00Z",
  "traceId": "guid-here"
}
```

### Implementation Requirements

Create `ApiResponse<T>` as a generic class with static factory methods:

```csharp
ApiResponse<T>.Success(data, message, statusCode)
ApiResponse<T>.Fail(message, errors, statusCode)
```

Every controller action must return `ActionResult<ApiResponse<T>>` and use the factory methods.

---

## 📋 Serilog Configuration

- Configure Serilog in `Program.cs` **before** `builder.Build()`
- **Sinks:** Console (colored output) + File (`logs/log-.txt`, daily rolling)
- Minimum level: `Information` in production, `Debug` in development
- Enrich with: `FromLogContext`, `WithMachineName`, `WithThreadId`
- Log every MediatR request/response via `LoggingBehavior` pipeline behavior
- Log unhandled exceptions in `ExceptionHandlingMiddleware` at `Error` level
- **Replace Microsoft default logging entirely with Serilog**

Log format:
```
[{Timestamp:HH:mm:ss} {Level:u3}] {SourceContext} | {Message}{NewLine}{Exception}
```

---

## 🔌 ADO.NET + Oracle Rules

- Use `Oracle.ManagedDataAccess.Core` **only** — no Entity Framework, no Dapper
- Connection string stored in `appsettings.json` under `ConnectionStrings:OracleDb`
- `OracleDbContext` wraps connection creation and disposal (implements `IDisposable`)

### Every repository method must

1. Open a new `OracleConnection` using the connection string
2. Create an `OracleCommand` with `CommandType.StoredProcedure`
3. Add `OracleParameter`s with explicit `OracleDbType` matching Oracle column types
4. For **SELECT**: use `OracleDataReader` to map to entity
5. For **INSERT/UPDATE/DELETE**: use `ExecuteNonQuery`, return affected rows or new ID
6. Wrap everything in `try/catch` — on exception log with Serilog and rethrow as custom domain exception
7. Use `using` statements for all disposable Oracle objects
8. For `SYS_REFCURSOR` out parameters use `OracleDbType.RefCursor`

### Required Repository Pattern (follow exactly)

```csharp
public async Task<SysRole?> GetByIdAsync(decimal rowId)
{
    using var connection = new OracleConnection(_connectionString);
    await connection.OpenAsync();

    using var command = new OracleCommand("SP_SYS_ROLE_SELECT_BY_ID", connection)
    {
        CommandType = CommandType.StoredProcedure
    };

    command.Parameters.Add("P_ROW_ID",        OracleDbType.Decimal).Value     = rowId;
    command.Parameters.Add("P_RESULT_CURSOR",  OracleDbType.RefCursor).Direction = ParameterDirection.Output;

    using var reader = await command.ExecuteReaderAsync();

    if (await reader.ReadAsync())
    {
        return MapToEntity(reader);
    }

    return null;
}
```

---

## 🧩 Dependency Injection

### `Infrastructure/DependencyInjection.cs`

Create extension method `services.AddInfrastructure(configuration)` that:
- Registers all repositories as **Scoped** (`IRoleRepository → RoleRepository`, etc.)
- Registers `OracleDbContext` as Scoped
- Registers `JwtTokenService` as Scoped

### `Application/DependencyInjection.cs`

Create extension method `services.AddApplication()` that:
- Registers MediatR scanning Application assembly
- Registers FluentValidation scanning Application assembly
- Registers `ValidationBehavior` and `LoggingBehavior` as pipeline behaviors

### `Program.cs`

```csharp
builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);
```

---

## 🌐 Controllers Specification

### `AuthController` — no `[Authorize]`

| Method | Route | Action |
|---|---|---|
| `POST` | `/api/auth/login` | `LoginCommand` |

### `RolesController` — `[Authorize]`

| Method | Route | Action | Policy |
|---|---|---|---|
| `GET` | `/api/roles` | `GetAllRolesQuery` | — |
| `GET` | `/api/roles/{id}` | `GetRoleByIdQuery` | — |
| `POST` | `/api/roles` | `CreateRoleCommand` | AdminOnly |
| `PUT` | `/api/roles/{id}` | `UpdateRoleCommand` | AdminOnly |
| `DELETE` | `/api/roles/{id}` | `DeleteRoleCommand` | AdminOnly |

### `CurrencyController` — `[Authorize]`

| Method | Route | Policy |
|---|---|---|
| `GET` | `/api/currencies` | — |
| `GET` | `/api/currencies/{id}` | — |
| `POST` | `/api/currencies` | AdminOnly |
| `PUT` | `/api/currencies/{id}` | AdminOnly |
| `DELETE` | `/api/currencies/{id}` | AdminOnly |

### `CompanyController` — `[Authorize]`

| Method | Route | Policy |
|---|---|---|
| `GET` | `/api/companies` | — |
| `GET` | `/api/companies/{id}` | — |
| `POST` | `/api/companies` | AdminOnly |
| `PUT` | `/api/companies/{id}` | AdminOnly |
| `DELETE` | `/api/companies/{id}` | AdminOnly |

### `BranchController` — `[Authorize]`

| Method | Route | Policy |
|---|---|---|
| `GET` | `/api/branches` | — |
| `GET` | `/api/branches/{id}` | — |
| `POST` | `/api/branches` | AdminOnly |
| `PUT` | `/api/branches/{id}` | AdminOnly |
| `DELETE` | `/api/branches/{id}` | AdminOnly |

### `UsersController` — `[Authorize]`

| Method | Route | Policy |
|---|---|---|
| `GET` | `/api/users` | AdminOnly |
| `GET` | `/api/users/{id}` | AdminOnly |
| `POST` | `/api/users` | AdminOnly |
| `PUT` | `/api/users/{id}` | AdminOnly |
| `DELETE` | `/api/users/{id}` | AdminOnly |
| `PUT` | `/api/users/{id}/change-password` | — |

---

## ⚙️ `appsettings.json` Template

Generate this exact `appsettings.json`:

```json
{
  "ConnectionStrings": {
    "OracleDb": "User Id=YOUR_USER;Password=YOUR_PASS;Data Source=YOUR_HOST:1521/YOUR_SERVICE"
  },
  "JwtSettings": {
    "SecretKey": "YourSuperSecretKeyHereMustBe32Chars!",
    "Issuer": "YourAppName",
    "Audience": "YourAppUsers",
    "ExpiryInMinutes": 60
  },
  "Serilog": {
    "MinimumLevel": {
      "Default": "Information",
      "Override": {
        "Microsoft": "Warning",
        "System": "Warning"
      }
    }
  }
}
```

---

## ✅ Additional Requirements

### 1 — Global Exception Handling

`ExceptionHandlingMiddleware` must catch all unhandled exceptions and return `ApiResponse<object>.Fail(...)` with `statusCode: 500`. Never expose stack traces to the client.

### 2 — Validation Pipeline Behavior

`ValidationBehavior` (MediatR pipeline) must intercept commands/queries, run FluentValidation, and throw `ValidationException` with all error messages collected. The middleware converts this to a `400 ApiResponse`.

### 3 — Swagger / OpenAPI

Add Swagger with JWT Bearer support in `Program.cs` (Development only):
- Add security definition: Bearer token
- Show all routes with their request/response schemas

### 4 — DateTime Mapping

All `DateTime` fields from Oracle map to C# `DateTime?` (nullable).

### 5 — `IS_ACTIVE` Mapping

Map `'Y'` / `'1'` → `true` and `'0'` / `'N'` → `false` in all entity mappers.

### 6 — `ROW_ID` Type

All `ROW_ID` columns use `decimal` in C# (Oracle `NUMBER` maps to `decimal`).

### 7 — Password Hashing

Passwords in `SYS_USERS` are stored as **SHA-256 hex strings**. Hash all incoming passwords before comparing or inserting.

### 8 — XML Docs for Swagger

Every DTO must have XML summary comments for Swagger schema generation.

### 9 — Professional API Messages

All user-facing success and error messages must be professional English. Examples:

| Scenario | Message |
|---|---|
| Record created | `"Role created successfully."` |
| Record not found | `"No role found with the specified identifier."` |
| Unauthorized | `"Access denied. Administrator privileges are required."` |
| Bad credentials | `"Invalid credentials. Please verify your username and password."` |
| Validation fail | `"One or more validation errors occurred."` |
| Server error | `"An unexpected error occurred. Please try again later."` |

### 10 — `README.md`

Generate a `README.md` with:
- Project overview
- Architecture diagram (text-based)
- Setup and run steps
- Connection string configuration guide
- Sample API calls with expected JSON responses

---

## 🚀 Generation Order

Generate the complete solution **file by file** in this order:

1. Oracle sequences and stored procedures (all tables)
2. Domain layer (entities → interfaces)
3. Application layer (DTOs → commands → queries → behaviors)
4. Infrastructure layer (OracleDbContext → repositories → DependencyInjection)
5. API layer (Program.cs → middleware → controllers → appsettings.json)
6. README.md
