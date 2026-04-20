# Password Hashing Pattern - Clean Architecture

## Overview

This document explains the correct password hashing pattern that maintains clean architecture principles in the ThinkOnErp project.

---

## The Problem

**WRONG** ❌: Application layer referencing Infrastructure layer

```csharp
// Application Layer - CreateUserCommandHandler.cs
using ThinkOnErp.Infrastructure.Services; // ❌ WRONG!

public class CreateUserCommandHandler
{
    private readonly PasswordHashingService _passwordHashingService; // ❌ WRONG!
    
    public async Task<Int64> Handle(...)
    {
        var hash = _passwordHashingService.HashPassword(password); // ❌ WRONG!
    }
}
```

This violates clean architecture because:
- Application layer should only depend on Domain layer
- Infrastructure layer depends on Domain layer
- Application cannot reference Infrastructure

---

## The Solution

**CORRECT** ✅: Hash password in API Controller layer

### Layer Responsibilities

```
┌─────────────────────────────────────────────────┐
│ API Layer (Controllers)                         │
│ - Receives plain password from user             │
│ - Hashes password using PasswordHashingService  │
│ - Passes hashed password to Application layer   │
└─────────────────────────────────────────────────┘
                    ↓ (hashed password)
┌─────────────────────────────────────────────────┐
│ Application Layer (Command Handlers)            │
│ - Receives already-hashed password              │
│ - Performs business logic                       │
│ - Passes hashed password to Infrastructure      │
└─────────────────────────────────────────────────┘
                    ↓ (hashed password)
┌─────────────────────────────────────────────────┐
│ Infrastructure Layer (Repositories)             │
│ - Receives already-hashed password              │
│ - Stores in database via stored procedure       │
└─────────────────────────────────────────────────┘
                    ↓ (hashed password)
┌─────────────────────────────────────────────────┐
│ Database (Oracle)                               │
│ - Stores hashed password                        │
└─────────────────────────────────────────────────┘
```

---

## Implementation Pattern

### 1. API Controller (Hash Password)

```csharp
// src/ThinkOnErp.API/Controllers/SuperAdminController.cs
using ThinkOnErp.Infrastructure.Services; // ✅ OK in API layer

public class SuperAdminController : ControllerBase
{
    private readonly IMediator _mediator;
    private readonly PasswordHashingService _passwordHashingService; // ✅ OK here

    public SuperAdminController(
        IMediator mediator,
        PasswordHashingService passwordHashingService)
    {
        _mediator = mediator;
        _passwordHashingService = passwordHashingService;
    }

    [HttpPost]
    public async Task<ActionResult> CreateSuperAdmin([FromBody] CreateSuperAdminDto dto)
    {
        // Hash the password using SHA-256
        var passwordHash = _passwordHashingService.HashPassword(dto.Password); // ✅ Hash here

        var command = new CreateSuperAdminCommand
        {
            UserName = dto.UserName,
            Password = passwordHash, // ✅ Pass hashed password
            // ... other properties
        };

        var result = await _mediator.Send(command);
        return Ok(result);
    }
}
```

### 2. Application Command Handler (Receive Hashed Password)

```csharp
// src/ThinkOnErp.Application/Features/SuperAdmins/Commands/CreateSuperAdmin/CreateSuperAdminCommandHandler.cs
using ThinkOnErp.Domain.Interfaces; // ✅ Only Domain references

public class CreateSuperAdminCommandHandler : IRequestHandler<CreateSuperAdminCommand, Int64>
{
    private readonly ISuperAdminRepository _repository; // ✅ Interface from Domain

    public CreateSuperAdminCommandHandler(ISuperAdminRepository repository)
    {
        _repository = repository;
    }

    public async Task<Int64> Handle(CreateSuperAdminCommand request, CancellationToken cancellationToken)
    {
        var superAdmin = new SysSuperAdmin
        {
            UserName = request.UserName,
            Password = request.Password, // ✅ Already hashed from API layer
            // ... other properties
        };

        return await _repository.CreateAsync(superAdmin);
    }
}
```

### 3. Infrastructure Repository (Store Hashed Password)

```csharp
// src/ThinkOnErp.Infrastructure/Repositories/SuperAdminRepository.cs
public class SuperAdminRepository : ISuperAdminRepository
{
    public async Task<Int64> CreateAsync(SysSuperAdmin superAdmin)
    {
        // ... setup connection and command
        
        command.Parameters.Add(new OracleParameter
        {
            ParameterName = "P_PASSWORD",
            OracleDbType = OracleDbType.Varchar2,
            Value = superAdmin.Password // ✅ Already hashed, just store it
        });

        // ... execute stored procedure
    }
}
```

---

## Password Verification Pattern (Login)

### 1. API Controller (Hash Input Password)

```csharp
// src/ThinkOnErp.API/Controllers/AuthController.cs
[HttpPost("login")]
public async Task<ActionResult> Login([FromBody] LoginDto dto)
{
    // Hash the input password
    var passwordHash = _passwordHashingService.HashPassword(dto.Password); // ✅ Hash here

    // Authenticate with hashed password
    var user = await _authRepository.AuthenticateAsync(dto.UserName, passwordHash);

    if (user == null)
        return Unauthorized();

    // Generate token...
}
```

### 2. Infrastructure Repository (Compare Hashed Passwords)

```csharp
// src/ThinkOnErp.Infrastructure/Repositories/AuthRepository.cs
public async Task<SysUser?> AuthenticateAsync(string userName, string passwordHash)
{
    // Call stored procedure with hashed password
    command.Parameters.Add(new OracleParameter
    {
        ParameterName = "P_PASSWORD",
        Value = passwordHash // ✅ Already hashed
    });

    // Stored procedure compares hashed passwords
    // Returns user if match, null otherwise
}
```

---

## Change Password Pattern

### 1. API Controller (Hash Both Passwords)

```csharp
[HttpPost("{id}/change-password")]
public async Task<ActionResult> ChangePassword(Int64 id, [FromBody] ChangePasswordDto dto)
{
    // Get current user to verify current password
    var user = await _mediator.Send(new GetSuperAdminByIdQuery { SuperAdminId = id });
    
    // Hash current password and verify
    var currentPasswordHash = _passwordHashingService.HashPassword(dto.CurrentPassword);
    if (user.Password != currentPasswordHash)
    {
        return BadRequest("Current password is incorrect");
    }

    // Hash new password
    var newPasswordHash = _passwordHashingService.HashPassword(dto.NewPassword);

    var command = new ChangeSuperAdminPasswordCommand
    {
        SuperAdminId = id,
        NewPassword = newPasswordHash // ✅ Pass hashed password
    };

    await _mediator.Send(command);
    return Ok();
}
```

---

## Key Points

### ✅ DO
- Hash passwords in **API Controller layer**
- Pass **hashed passwords** to Application layer
- Store **hashed passwords** in database
- Use `PasswordHashingService` only in API and Infrastructure layers

### ❌ DON'T
- Reference Infrastructure layer from Application layer
- Hash passwords in Command Handlers
- Pass plain passwords to repositories
- Store plain passwords in database

---

## Why This Pattern?

### Clean Architecture Compliance
```
Domain ← Application ← Infrastructure
                    ↑
                  API
```

- **Domain**: Core entities and interfaces (no dependencies)
- **Application**: Business logic (depends only on Domain)
- **Infrastructure**: Implementation details (depends on Domain)
- **API**: Entry point (depends on Application and Infrastructure)

### Benefits
1. ✅ Maintains clean architecture
2. ✅ Separation of concerns
3. ✅ Testable (can mock PasswordHashingService in API tests)
4. ✅ Consistent with existing codebase (AuthController pattern)
5. ✅ Infrastructure services only used where appropriate

---

## Examples in Codebase

### Existing Implementation (AuthController)
- ✅ `src/ThinkOnErp.API/Controllers/AuthController.cs` - Hashes password in controller
- ✅ `src/ThinkOnErp.Application/Features/Auth/Commands/Login/LoginCommandHandler.cs` - No hashing
- ✅ `src/ThinkOnErp.Infrastructure/Repositories/AuthRepository.cs` - Receives hashed password

### New Implementation (SuperAdminController)
- ✅ `src/ThinkOnErp.API/Controllers/SuperAdminController.cs` - Hashes password in controller
- ✅ `src/ThinkOnErp.Application/Features/SuperAdmins/Commands/CreateSuperAdmin/CreateSuperAdminCommandHandler.cs` - No hashing
- ✅ `src/ThinkOnErp.Infrastructure/Repositories/SuperAdminRepository.cs` - Receives hashed password

---

## Summary

**Password hashing happens in the API Controller layer, not in the Application layer.**

This maintains clean architecture while ensuring passwords are properly hashed before storage.

---

**Reference**: See `SUPERADMIN_BUILD_SUCCESS.md` for the complete implementation that follows this pattern.
