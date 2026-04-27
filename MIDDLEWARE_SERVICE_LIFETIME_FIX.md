# Middleware Service Lifetime Fix

## Problem

The application was failing to start with the following error:

```
System.InvalidOperationException: Cannot resolve scoped service 'ThinkOnErp.Domain.Interfaces.IExceptionCategorizationService' from root provider.
```

## Root Cause

**Service Lifetime Mismatch in Middleware:**

- `IExceptionCategorizationService` is registered as a **Scoped** service
- Middleware classes (`ExceptionHandlingMiddleware` and `RequestTracingMiddleware`) were injecting it directly in their constructors
- Middleware is instantiated once at application startup (like a Singleton)
- **ASP.NET Core does not allow Singleton-lifetime components to depend on Scoped services**

This is a fundamental ASP.NET Core design principle: middleware is created once and reused for all requests, so it cannot have scoped dependencies that are meant to be created per-request.

## Solution

**Use `IServiceScopeFactory` to resolve scoped services within middleware:**

Instead of injecting scoped services directly in middleware constructors, we:
1. Inject `IServiceScopeFactory` in the constructor
2. Create a scope in the `InvokeAsync` method (which runs per-request)
3. Resolve the scoped service from that scope
4. Use the service within the scope
5. Dispose the scope when done

## Changes Made

### 1. ExceptionHandlingMiddleware.cs

**Before:**
```csharp
private readonly IExceptionCategorizationService _exceptionCategorization;

public ExceptionHandlingMiddleware(
    RequestDelegate next,
    ILogger<ExceptionHandlingMiddleware> logger,
    IAuditLogger auditLogger,
    IExceptionCategorizationService exceptionCategorization)
{
    _exceptionCategorization = exceptionCategorization;
}
```

**After:**
```csharp
private readonly IServiceScopeFactory _serviceScopeFactory;

public ExceptionHandlingMiddleware(
    RequestDelegate next,
    ILogger<ExceptionHandlingMiddleware> logger,
    IAuditLogger auditLogger,
    IServiceScopeFactory serviceScopeFactory)
{
    _serviceScopeFactory = serviceScopeFactory;
}
```

**Usage in method:**
```csharp
// Resolve scoped service to determine severity
string severity;
using (var scope = _serviceScopeFactory.CreateScope())
{
    var exceptionCategorization = scope.ServiceProvider.GetRequiredService<IExceptionCategorizationService>();
    severity = exceptionCategorization.DetermineSeverity(exception);
}
```

### 2. RequestTracingMiddleware.cs

**Before:**
```csharp
private readonly IExceptionCategorizationService _exceptionCategorization;

public RequestTracingMiddleware(
    RequestDelegate next,
    IAuditLogger auditLogger,
    IPerformanceMonitor performanceMonitor,
    IServiceScopeFactory serviceScopeFactory,
    IExceptionCategorizationService exceptionCategorization,
    ILogger<RequestTracingMiddleware> logger,
    IOptions<RequestTracingOptions> options)
{
    _exceptionCategorization = exceptionCategorization;
}
```

**After:**
```csharp
// Removed _exceptionCategorization field

public RequestTracingMiddleware(
    RequestDelegate next,
    IAuditLogger auditLogger,
    IPerformanceMonitor performanceMonitor,
    IServiceScopeFactory serviceScopeFactory,
    ILogger<RequestTracingMiddleware> logger,
    IOptions<RequestTracingOptions> options)
{
    // Already had IServiceScopeFactory, just removed IExceptionCategorizationService
}
```

**Usage in method:**
```csharp
// Resolve scoped service to determine severity
string severity;
using (var scope = _serviceScopeFactory.CreateScope())
{
    var exceptionCategorization = scope.ServiceProvider.GetRequiredService<IExceptionCategorizationService>();
    severity = exceptionCategorization.DetermineSeverity(exception);
}
```

## Why This Works

1. **IServiceScopeFactory is Singleton-safe**: It can be injected into middleware because it's designed to create scopes on-demand
2. **Scope creation is per-request**: The scope is created in `InvokeAsync`, which runs for each HTTP request
3. **Proper lifetime management**: The `using` statement ensures the scope is disposed after use
4. **Scoped services work correctly**: Services resolved from the scope have the correct per-request lifetime

## Service Lifetime Reference

| Service | Lifetime | Reason |
|---------|----------|--------|
| `IExceptionCategorizationService` | Scoped | May depend on per-request context |
| `IServiceScopeFactory` | Singleton | Factory pattern - safe to inject anywhere |
| Middleware | Singleton | Created once at startup |

## Verification

Build succeeded with no errors:
```
Build succeeded with 69 warning(s) in 15.3s
```

The warnings are unrelated to this fix (mostly XML documentation and nullable reference warnings).

## Related Files

- `src/ThinkOnErp.API/Middleware/ExceptionHandlingMiddleware.cs`
- `src/ThinkOnErp.API/Middleware/RequestTracingMiddleware.cs`
- `src/ThinkOnErp.Domain/Interfaces/IExceptionCategorizationService.cs`
- `src/ThinkOnErp.Infrastructure/Services/ExceptionCategorizationService.cs`

## Best Practices for Middleware

**✅ DO:**
- Inject Singleton services directly in middleware constructors
- Inject `IServiceScopeFactory` to resolve Scoped services
- Create scopes in `InvokeAsync` method
- Dispose scopes properly using `using` statements

**❌ DON'T:**
- Inject Scoped services directly in middleware constructors
- Store Scoped services in middleware fields
- Create scopes in middleware constructors
- Forget to dispose scopes

## Next Steps

The application should now start successfully. The middleware will properly resolve scoped services on a per-request basis while maintaining correct service lifetimes.
