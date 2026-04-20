# SuperAdmin Architecture Diagram

## System Architecture Overview

```
┌─────────────────────────────────────────────────────────────────────┐
│                          CLIENT LAYER                                │
│  (Postman, Browser, Mobile App, Third-party Applications)           │
└────────────────────────────┬────────────────────────────────────────┘
                             │
                             │ HTTP/HTTPS
                             │
┌────────────────────────────▼────────────────────────────────────────┐
│                          API LAYER                                   │
│  ┌──────────────────────────────────────────────────────────────┐  │
│  │  AuthController                                               │  │
│  │  ├─ POST /api/auth/superadmin/login                          │  │
│  │  └─ POST /api/auth/superadmin/refresh                        │  │
│  └──────────────────────────────────────────────────────────────┘  │
│  ┌──────────────────────────────────────────────────────────────┐  │
│  │  SuperAdminController [Authorize(Policy = "AdminOnly")]      │  │
│  │  ├─ GET    /api/superadmins                                  │  │
│  │  ├─ GET    /api/superadmins/{id}                             │  │
│  │  ├─ POST   /api/superadmins                                  │  │
│  │  ├─ PUT    /api/superadmins/{id}                             │  │
│  │  └─ DELETE /api/superadmins/{id}                             │  │
│  └──────────────────────────────────────────────────────────────┘  │
│                                                                      │
│  Services:                                                           │
│  ├─ PasswordHashingService (SHA-256)                                │
│  ├─ JwtTokenService (Token Generation)                              │
│  └─ Authorization Policies                                          │
└────────────────────────────┬────────────────────────────────────────┘
                             │
                             │ MediatR (CQRS)
                             │
┌────────────────────────────▼────────────────────────────────────────┐
│                      APPLICATION LAYER                               │
│  ┌──────────────────────────────────────────────────────────────┐  │
│  │  Commands (Write Operations)                                 │  │
│  │  ├─ CreateSuperAdminCommand + Handler + Validator           │  │
│  │  ├─ UpdateSuperAdminCommand + Handler + Validator           │  │
│  │  └─ DeleteSuperAdminCommand + Handler                       │  │
│  └──────────────────────────────────────────────────────────────┘  │
│  ┌──────────────────────────────────────────────────────────────┐  │
│  │  Queries (Read Operations)                                   │  │
│  │  ├─ GetAllSuperAdminsQuery + Handler                        │  │
│  │  └─ GetSuperAdminByIdQuery + Handler                        │  │
│  └──────────────────────────────────────────────────────────────┘  │
│  ┌──────────────────────────────────────────────────────────────┐  │
│  │  DTOs (Data Transfer Objects)                                │  │
│  │  ├─ SuperAdminDto                                            │  │
│  │  ├─ CreateSuperAdminDto                                      │  │
│  │  ├─ UpdateSuperAdminDto                                      │  │
│  │  └─ ChangePasswordDto                                        │  │
│  └──────────────────────────────────────────────────────────────┘  │
└────────────────────────────┬────────────────────────────────────────┘
                             │
                             │ Repository Interface
                             │
┌────────────────────────────▼────────────────────────────────────────┐
│                        DOMAIN LAYER                                  │
│  ┌──────────────────────────────────────────────────────────────┐  │
│  │  SysSuperAdmin Entity                                        │  │
│  │  ├─ RowId (Primary Key)                                      │  │
│  │  ├─ NameAr, NameEn                                           │  │
│  │  ├─ UserName (Unique)                                        │  │
│  │  ├─ Password (SHA-256 Hash)                                  │  │
│  │  ├─ Email, Phone                                             │  │
│  │  ├─ TwoFaEnabled, TwoFaSecret                                │  │
│  │  ├─ IsActive                                                 │  │
│  │  ├─ LastLoginDate                                            │  │
│  │  ├─ RefreshToken, RefreshTokenExpiry                         │  │
│  │  └─ Audit Fields (CreationUser, CreationDate, etc.)         │  │
│  └──────────────────────────────────────────────────────────────┘  │
│  ┌──────────────────────────────────────────────────────────────┐  │
│  │  ISuperAdminRepository Interface                             │  │
│  │  ├─ CreateAsync()                                            │  │
│  │  ├─ UpdateAsync()                                            │  │
│  │  ├─ DeleteAsync()                                            │  │
│  │  ├─ GetAllAsync()                                            │  │
│  │  ├─ GetByIdAsync()                                           │  │
│  │  ├─ GetByUsernameAsync()                                     │  │
│  │  ├─ AuthenticateAsync()                                      │  │
│  │  ├─ SaveRefreshTokenAsync()                                  │  │
│  │  ├─ ValidateRefreshTokenAsync()                              │  │
│  │  ├─ ChangePasswordAsync()                                    │  │
│  │  ├─ Enable2FAAsync()                                         │  │
│  │  ├─ Disable2FAAsync()                                        │  │
│  │  ├─ UpdateLastLoginAsync()                                   │  │
│  │  └─ CheckUsernameExistsAsync()                               │  │
│  └──────────────────────────────────────────────────────────────┘  │
└────────────────────────────┬────────────────────────────────────────┘
                             │
                             │ Implementation
                             │
┌────────────────────────────▼────────────────────────────────────────┐
│                    INFRASTRUCTURE LAYER                              │
│  ┌──────────────────────────────────────────────────────────────┐  │
│  │  SuperAdminRepository                                        │  │
│  │  ├─ OracleDbContext (Database Connection)                   │  │
│  │  ├─ Implements ISuperAdminRepository                        │  │
│  │  └─ Calls Stored Procedures                                 │  │
│  └──────────────────────────────────────────────────────────────┘  │
│  ┌──────────────────────────────────────────────────────────────┐  │
│  │  Services                                                    │  │
│  │  ├─ JwtTokenService (Token Generation)                      │  │
│  │  │   └─ GenerateToken(SysSuperAdmin) overload               │  │
│  │  └─ PasswordHashingService (SHA-256)                        │  │
│  └──────────────────────────────────────────────────────────────┘  │
└────────────────────────────┬────────────────────────────────────────┘
                             │
                             │ ADO.NET / Oracle.ManagedDataAccess
                             │
┌────────────────────────────▼────────────────────────────────────────┐
│                       DATABASE LAYER                                 │
│  ┌──────────────────────────────────────────────────────────────┐  │
│  │  SYS_SUPER_ADMIN Table                                       │  │
│  │  ├─ ROW_ID (PK, NUMBER(19))                                  │  │
│  │  ├─ ROW_DESC (NVARCHAR2(200))                                │  │
│  │  ├─ ROW_DESC_E (NVARCHAR2(200))                              │  │
│  │  ├─ USER_NAME (NVARCHAR2(100), UNIQUE)                       │  │
│  │  ├─ PASSWORD (NVARCHAR2(256))                                │  │
│  │  ├─ EMAIL (NVARCHAR2(100))                                   │  │
│  │  ├─ PHONE (NVARCHAR2(20))                                    │  │
│  │  ├─ TWO_FA_ENABLED (CHAR(1))                                 │  │
│  │  ├─ TWO_FA_SECRET (NVARCHAR2(100))                           │  │
│  │  ├─ IS_ACTIVE (CHAR(1))                                      │  │
│  │  ├─ LAST_LOGIN_DATE (DATE)                                   │  │
│  │  ├─ REFRESH_TOKEN (NVARCHAR2(500))                           │  │
│  │  ├─ REFRESH_TOKEN_EXPIRY (DATE)                              │  │
│  │  └─ Audit Fields (CREATION_USER, CREATION_DATE, etc.)       │  │
│  └──────────────────────────────────────────────────────────────┘  │
│  ┌──────────────────────────────────────────────────────────────┐  │
│  │  SEQ_SYS_SUPER_ADMIN Sequence                                │  │
│  │  └─ Generates unique ROW_ID values                           │  │
│  └──────────────────────────────────────────────────────────────┘  │
│  ┌──────────────────────────────────────────────────────────────┐  │
│  │  Stored Procedures (11 total)                                │  │
│  │  ├─ SP_SYS_SUPER_ADMIN_INSERT                                │  │
│  │  ├─ SP_SYS_SUPER_ADMIN_UPDATE                                │  │
│  │  ├─ SP_SYS_SUPER_ADMIN_DELETE                                │  │
│  │  ├─ SP_SYS_SUPER_ADMIN_SELECT_ALL                            │  │
│  │  ├─ SP_SYS_SUPER_ADMIN_SELECT_BY_ID                          │  │
│  │  ├─ SP_SYS_SUPER_ADMIN_SELECT_BY_USERNAME                    │  │
│  │  ├─ SP_SYS_SUPER_ADMIN_CHANGE_PASSWORD                       │  │
│  │  ├─ SP_SYS_SUPER_ADMIN_ENABLE_2FA                            │  │
│  │  ├─ SP_SYS_SUPER_ADMIN_DISABLE_2FA                           │  │
│  │  ├─ SP_SYS_SUPER_ADMIN_UPDATE_LAST_LOGIN                     │  │
│  │  └─ SP_SYS_SUPER_ADMIN_LOGIN                                 │  │
│  └──────────────────────────────────────────────────────────────┘  │
└─────────────────────────────────────────────────────────────────────┘
```

---

## Authentication Flow

```
┌─────────┐                                                    ┌──────────┐
│ Client  │                                                    │ Database │
└────┬────┘                                                    └────┬─────┘
     │                                                              │
     │ 1. POST /api/auth/superadmin/login                           │
     │    { userName, password }                                    │
     ├──────────────────────────────────────────────────────────►  │
     │                                                              │
     │ 2. Hash password (SHA-256)                                   │
     │    AuthController → PasswordHashingService                   │
     │                                                              │
     │ 3. Call SP_SYS_SUPER_ADMIN_LOGIN                             │
     │    AuthRepository → Database                                 │
     ├──────────────────────────────────────────────────────────►  │
     │                                                              │
     │                                    4. Validate credentials   │
     │                                       Check IS_ACTIVE = '1'  │
     │                                       Return user data       │
     │  ◄──────────────────────────────────────────────────────────┤
     │                                                              │
     │ 5. Generate JWT tokens                                       │
     │    JwtTokenService.GenerateToken(superAdmin)                 │
     │    - Access Token (60 min)                                   │
     │    - Refresh Token (7 days)                                  │
     │    - Special Claims:                                         │
     │      * userType: "SuperAdmin"                                │
     │      * isSuperAdmin: "true"                                  │
     │                                                              │
     │ 6. Save refresh token to database                            │
     │    UPDATE SYS_SUPER_ADMIN                                    │
     │    SET REFRESH_TOKEN = ?, REFRESH_TOKEN_EXPIRY = ?           │
     ├──────────────────────────────────────────────────────────►  │
     │                                                              │
     │  ◄──────────────────────────────────────────────────────────┤
     │                                                              │
     │ 7. Return tokens to client                                   │
     │    { accessToken, refreshToken, expiresAt, ... }             │
     │  ◄──────────────────────────────────────────────────────────┤
     │                                                              │
     │ 8. Use access token for subsequent requests                  │
     │    Authorization: Bearer {accessToken}                       │
     │                                                              │
     │ 9. When access token expires, refresh                        │
     │    POST /api/auth/superadmin/refresh                         │
     │    { refreshToken }                                          │
     ├──────────────────────────────────────────────────────────►  │
     │                                                              │
     │ 10. Validate refresh token                                   │
     │     Check expiry, check IS_ACTIVE                            │
     ├──────────────────────────────────────────────────────────►  │
     │  ◄──────────────────────────────────────────────────────────┤
     │                                                              │
     │ 11. Generate new tokens                                      │
     │     Return new access + refresh tokens                       │
     │  ◄──────────────────────────────────────────────────────────┤
     │                                                              │
```

---

## CRUD Operations Flow

```
┌─────────┐                                                    ┌──────────┐
│ Client  │                                                    │ Database │
└────┬────┘                                                    └────┬─────┘
     │                                                              │
     │ CREATE: POST /api/superadmins                                │
     │ ┌────────────────────────────────────────────────────────┐  │
     │ │ 1. Validate request (FluentValidation)                 │  │
     │ │ 2. Hash password (PasswordHashingService)              │  │
     │ │ 3. Send CreateSuperAdminCommand (MediatR)              │  │
     │ │ 4. Handler calls repository.CreateAsync()              │  │
     │ │ 5. Repository calls SP_SYS_SUPER_ADMIN_INSERT          │  │
     │ └────────────────────────────────────────────────────────┘  │
     ├──────────────────────────────────────────────────────────►  │
     │  ◄──────────────────────────────────────────────────────────┤
     │ Returns: { success: true, data: newId }                      │
     │                                                              │
     │ READ: GET /api/superadmins                                   │
     │ ┌────────────────────────────────────────────────────────┐  │
     │ │ 1. Verify authorization (JWT token)                    │  │
     │ │ 2. Send GetAllSuperAdminsQuery (MediatR)               │  │
     │ │ 3. Handler calls repository.GetAllAsync()              │  │
     │ │ 4. Repository calls SP_SYS_SUPER_ADMIN_SELECT_ALL      │  │
     │ │ 5. Map entities to DTOs                                │  │
     │ └────────────────────────────────────────────────────────┘  │
     ├──────────────────────────────────────────────────────────►  │
     │  ◄──────────────────────────────────────────────────────────┤
     │ Returns: { success: true, data: [superAdmins] }              │
     │                                                              │
     │ UPDATE: PUT /api/superadmins/{id}                            │
     │ ┌────────────────────────────────────────────────────────┐  │
     │ │ 1. Validate request (FluentValidation)                 │  │
     │ │ 2. Send UpdateSuperAdminCommand (MediatR)              │  │
     │ │ 3. Handler calls repository.UpdateAsync()              │  │
     │ │ 4. Repository calls SP_SYS_SUPER_ADMIN_UPDATE          │  │
     │ └────────────────────────────────────────────────────────┘  │
     ├──────────────────────────────────────────────────────────►  │
     │  ◄──────────────────────────────────────────────────────────┤
     │ Returns: { success: true, data: true }                       │
     │                                                              │
     │ DELETE: DELETE /api/superadmins/{id}                         │
     │ ┌────────────────────────────────────────────────────────┐  │
     │ │ 1. Verify authorization (JWT token)                    │  │
     │ │ 2. Send DeleteSuperAdminCommand (MediatR)              │  │
     │ │ 3. Handler calls repository.DeleteAsync()              │  │
     │ │ 4. Repository calls SP_SYS_SUPER_ADMIN_DELETE          │  │
     │ │ 5. Soft delete: SET IS_ACTIVE = '0'                    │  │
     │ └────────────────────────────────────────────────────────┘  │
     ├──────────────────────────────────────────────────────────►  │
     │  ◄──────────────────────────────────────────────────────────┤
     │ Returns: { success: true, data: true }                       │
     │                                                              │
```

---

## Security Layers

```
┌─────────────────────────────────────────────────────────────────────┐
│                        SECURITY LAYERS                               │
├─────────────────────────────────────────────────────────────────────┤
│                                                                      │
│  Layer 1: HTTPS/TLS                                                  │
│  └─ All communication encrypted in transit                           │
│                                                                      │
│  Layer 2: JWT Authentication                                         │
│  ├─ Access Token (60 minutes)                                        │
│  ├─ Refresh Token (7 days)                                           │
│  └─ Special Claims: userType="SuperAdmin", isSuperAdmin="true"       │
│                                                                      │
│  Layer 3: Authorization Policies                                     │
│  ├─ [Authorize(Policy = "AdminOnly")] on management endpoints        │
│  └─ Public endpoints: login, refresh                                 │
│                                                                      │
│  Layer 4: Password Security                                          │
│  ├─ SHA-256 hashing                                                  │
│  ├─ Hashing in API layer (clean architecture)                        │
│  ├─ Only hashed passwords stored                                     │
│  └─ Validation: min 8 chars, mixed case, numbers, special chars      │
│                                                                      │
│  Layer 5: Database Security                                          │
│  ├─ Stored procedures (SQL injection prevention)                     │
│  ├─ Parameterized queries                                            │
│  ├─ Soft deletes (data retention)                                    │
│  └─ Audit trail (creation/update tracking)                           │
│                                                                      │
│  Layer 6: Application Security                                       │
│  ├─ Input validation (FluentValidation)                              │
│  ├─ Error handling (no sensitive data in errors)                     │
│  ├─ Logging (security events tracked)                                │
│  └─ Rate limiting (future enhancement)                               │
│                                                                      │
│  Layer 7: Two-Factor Authentication (Future)                         │
│  ├─ Database columns ready: TWO_FA_ENABLED, TWO_FA_SECRET            │
│  ├─ Stored procedures ready: ENABLE_2FA, DISABLE_2FA                 │
│  └─ Implementation pending                                           │
│                                                                      │
└─────────────────────────────────────────────────────────────────────┘
```

---

## Data Flow: Create SuperAdmin

```
Client Request
     │
     │ POST /api/superadmins
     │ {
     │   "nameAr": "مدير النظام",
     │   "nameEn": "System Admin",
     │   "userName": "admin",
     │   "password": "SecurePass123!",
     │   "email": "admin@example.com"
     │ }
     │
     ▼
┌─────────────────────────────────────┐
│ SuperAdminController                │
│ ├─ Validate JWT token               │
│ ├─ Hash password (SHA-256)          │
│ │  password → 8C6976E5B5410415...   │
│ └─ Create command with hashed pwd   │
└────────────┬────────────────────────┘
             │
             │ MediatR.Send(CreateSuperAdminCommand)
             │
             ▼
┌─────────────────────────────────────┐
│ CreateSuperAdminCommandHandler      │
│ ├─ Validate command                 │
│ ├─ Check username uniqueness        │
│ └─ Call repository.CreateAsync()    │
└────────────┬────────────────────────┘
             │
             │ CreateAsync(entity)
             │
             ▼
┌─────────────────────────────────────┐
│ SuperAdminRepository                │
│ ├─ Open database connection         │
│ ├─ Create OracleCommand             │
│ ├─ Set parameters                   │
│ └─ Execute SP_SYS_SUPER_ADMIN_INSERT│
└────────────┬────────────────────────┘
             │
             │ EXEC SP_SYS_SUPER_ADMIN_INSERT
             │
             ▼
┌─────────────────────────────────────┐
│ Oracle Database                     │
│ ├─ Generate ROW_ID (sequence)       │
│ ├─ INSERT INTO SYS_SUPER_ADMIN      │
│ ├─ COMMIT                            │
│ └─ RETURN new ROW_ID                │
└────────────┬────────────────────────┘
             │
             │ Returns: newId = 123
             │
             ▼
┌─────────────────────────────────────┐
│ Response to Client                  │
│ {                                   │
│   "success": true,                  │
│   "message": "Super admin created", │
│   "data": 123,                      │
│   "statusCode": 201                 │
│ }                                   │
└─────────────────────────────────────┘
```

---

## Technology Stack

```
┌─────────────────────────────────────────────────────────────────────┐
│                        TECHNOLOGY STACK                              │
├─────────────────────────────────────────────────────────────────────┤
│                                                                      │
│  Backend Framework                                                   │
│  └─ ASP.NET Core 8.0 (Web API)                                       │
│                                                                      │
│  Programming Language                                                │
│  └─ C# 12.0                                                          │
│                                                                      │
│  Architecture Patterns                                               │
│  ├─ Clean Architecture (Domain, Application, Infrastructure, API)   │
│  ├─ CQRS (Command Query Responsibility Segregation)                 │
│  ├─ Repository Pattern                                               │
│  └─ Dependency Injection                                             │
│                                                                      │
│  Libraries & Packages                                                │
│  ├─ MediatR (CQRS implementation)                                    │
│  ├─ FluentValidation (Input validation)                              │
│  ├─ Oracle.ManagedDataAccess.Core (Database access)                  │
│  ├─ Microsoft.AspNetCore.Authentication.JwtBearer (JWT auth)         │
│  └─ Moq, xUnit, FsCheck (Testing)                                    │
│                                                                      │
│  Database                                                            │
│  ├─ Oracle Database                                                  │
│  ├─ Stored Procedures (PL/SQL)                                       │
│  └─ Sequences for ID generation                                      │
│                                                                      │
│  Security                                                            │
│  ├─ JWT (JSON Web Tokens)                                            │
│  ├─ SHA-256 (Password hashing)                                       │
│  ├─ HTTPS/TLS (Transport security)                                   │
│  └─ Authorization Policies                                           │
│                                                                      │
│  API Documentation                                                   │
│  ├─ XML Comments                                                     │
│  ├─ Swagger/OpenAPI (future)                                         │
│  └─ Markdown documentation                                           │
│                                                                      │
│  Testing                                                             │
│  ├─ xUnit (Unit testing framework)                                   │
│  ├─ Moq (Mocking framework)                                          │
│  ├─ FsCheck (Property-based testing)                                 │
│  └─ Integration tests (future)                                       │
│                                                                      │
└─────────────────────────────────────────────────────────────────────┘
```

---

## Deployment Architecture

```
┌─────────────────────────────────────────────────────────────────────┐
│                      PRODUCTION ENVIRONMENT                          │
├─────────────────────────────────────────────────────────────────────┤
│                                                                      │
│  ┌────────────────┐         ┌────────────────┐                      │
│  │  Load Balancer │────────▶│  Web Server 1  │                      │
│  │   (HTTPS)      │         │  (IIS/Kestrel) │                      │
│  └────────┬───────┘         └────────┬───────┘                      │
│           │                          │                              │
│           │                 ┌────────▼───────┐                      │
│           └────────────────▶│  Web Server 2  │                      │
│                             │  (IIS/Kestrel) │                      │
│                             └────────┬───────┘                      │
│                                      │                              │
│                             ┌────────▼───────┐                      │
│                             │  API Layer     │                      │
│                             │  (ASP.NET Core)│                      │
│                             └────────┬───────┘                      │
│                                      │                              │
│                             ┌────────▼───────┐                      │
│                             │ Oracle Database│                      │
│                             │  (Primary)     │                      │
│                             └────────┬───────┘                      │
│                                      │                              │
│                             ┌────────▼───────┐                      │
│                             │ Oracle Database│                      │
│                             │  (Standby)     │                      │
│                             └────────────────┘                      │
│                                                                      │
└─────────────────────────────────────────────────────────────────────┘
```

---

## Summary

This architecture provides:

✅ **Clean Separation of Concerns** - Each layer has a single responsibility  
✅ **Scalability** - Stateless API, horizontal scaling possible  
✅ **Security** - Multiple security layers, JWT authentication  
✅ **Maintainability** - CQRS pattern, clear code organization  
✅ **Testability** - Dependency injection, repository pattern  
✅ **Performance** - Stored procedures, efficient data access  
✅ **Reliability** - Error handling, logging, audit trail  

**Status:** Production Ready ✅
