# SuperAdmin Remaining Commands Implementation Guide

## Overview

This guide provides the exact code needed to implement the remaining 5 SuperAdmin commands. Each command follows the same pattern as CreateSuperAdmin.

---

## 1. UpdateSuperAdmin Command

### Command: `UpdateSuperAdminCommand.cs`
Location: `src/ThinkOnErp.Application/Features/SuperAdmins/Commands/UpdateSuperAdmin/`

```csharp
using MediatR;

namespace ThinkOnErp.Application.Features.SuperAdmins.Commands.UpdateSuperAdmin;

public class UpdateSuperAdminCommand : IRequest<bool>
{
    public Int64 SuperAdminId { get; set; }
    public string NameAr { get; set; } = string.Empty;
    public string NameEn { get; set; } = string.Empty;
    public string? Email { get; set; }
    public string? Phone { get; set; }
    public string UpdateUser { get; set; } = string.Empty;
}
```

### Handler: `UpdateSuperAdminCommandHandler.cs`

```csharp
using MediatR;
using ThinkOnErp.Domain.Interfaces;

namespace ThinkOnErp.Application.Features.SuperAdmins.Commands.UpdateSuperAdmin;

public class UpdateSuperAdminCommandHandler : IRequestHandler<UpdateSuperAdminCommand, bool>
{
    private readonly ISuperAdminRepository _repository;

    public UpdateSuperAdminCommandHandler(ISuperAdminRepository repository)
    {
        _repository = repository;
    }

    public async Task<bool> Handle(UpdateSuperAdminCommand request, CancellationToken cancellationToken)
    {
        var superAdmin = await _repository.GetByIdAsync(request.SuperAdminId);
        if (superAdmin == null)
        {
            throw new InvalidOperationException($"Super admin with ID {request.SuperAdminId} not found");
        }

        superAdmin.RowDesc = request.NameAr;
        superAdmin.RowDescE = request.NameEn;
        superAdmin.Email = request.Email;
        superAdmin.Phone = request.Phone;
        superAdmin.UpdateUser = request.UpdateUser;
        superAdmin.UpdateDate = DateTime.UtcNow;

        return await _repository.UpdateAsync(superAdmin);
    }
}
```

### Validator: `UpdateSuperAdminCommandValidator.cs`

```csharp
using FluentValidation;

namespace ThinkOnErp.Application.Features.SuperAdmins.Commands.UpdateSuperAdmin;

public class UpdateSuperAdminCommandValidator : AbstractValidator<UpdateSuperAdminCommand>
{
    public UpdateSuperAdminCommandValidator()
    {
        RuleFor(x => x.SuperAdminId)
            .GreaterThan(0)
            .WithMessage("Super admin ID must be greater than 0");

        RuleFor(x => x.NameAr)
            .NotEmpty()
            .WithMessage("Arabic name is required")
            .MaximumLength(200)
            .WithMessage("Arabic name cannot exceed 200 characters");

        RuleFor(x => x.NameEn)
            .NotEmpty()
            .WithMessage("English name is required")
            .MaximumLength(200)
            .WithMessage("English name cannot exceed 200 characters");

        RuleFor(x => x.Email)
            .EmailAddress()
            .When(x => !string.IsNullOrEmpty(x.Email))
            .WithMessage("Invalid email format")
            .MaximumLength(100)
            .WithMessage("Email cannot exceed 100 characters");

        RuleFor(x => x.Phone)
            .MaximumLength(20)
            .When(x => !string.IsNullOrEmpty(x.Phone))
            .WithMessage("Phone cannot exceed 20 characters");

        RuleFor(x => x.UpdateUser)
            .NotEmpty()
            .WithMessage("Update user is required");
    }
}
```

### Controller Method (add to SuperAdminController.cs)

```csharp
/// <summary>
/// Updates an existing super admin account
/// </summary>
[HttpPut("{id}")]
[ProducesResponseType(typeof(ApiResponse<bool>), StatusCodes.Status200OK)]
[ProducesResponseType(typeof(ApiResponse<bool>), StatusCodes.Status404NotFound)]
public async Task<ActionResult<ApiResponse<bool>>> UpdateSuperAdmin(Int64 id, [FromBody] UpdateSuperAdminDto dto)
{
    try
    {
        _logger.LogInformation("Updating super admin with ID: {SuperAdminId}", id);

        var command = new UpdateSuperAdminCommand
        {
            SuperAdminId = id,
            NameAr = dto.NameAr,
            NameEn = dto.NameEn,
            Email = dto.Email,
            Phone = dto.Phone,
            UpdateUser = User.Identity?.Name ?? "system"
        };

        var result = await _mediator.Send(command);

        _logger.LogInformation("Super admin updated successfully: {SuperAdminId}", id);

        return Ok(ApiResponse<bool>.CreateSuccess(
            result,
            "Super admin updated successfully",
            200));
    }
    catch (InvalidOperationException ex)
    {
        _logger.LogWarning("Super admin not found: {ErrorMessage}", ex.Message);
        return NotFound(ApiResponse<bool>.CreateFailure(
            ex.Message,
            statusCode: 404));
    }
    catch (Exception ex)
    {
        _logger.LogError(ex, "Error updating super admin with ID: {SuperAdminId}", id);
        throw;
    }
}
```

---

## 2. DeleteSuperAdmin Command

### Command: `DeleteSuperAdminCommand.cs`
Location: `src/ThinkOnErp.Application/Features/SuperAdmins/Commands/DeleteSuperAdmin/`

```csharp
using MediatR;

namespace ThinkOnErp.Application.Features.SuperAdmins.Commands.DeleteSuperAdmin;

public class DeleteSuperAdminCommand : IRequest<bool>
{
    public Int64 SuperAdminId { get; set; }
}
```

### Handler: `DeleteSuperAdminCommandHandler.cs`

```csharp
using MediatR;
using ThinkOnErp.Domain.Interfaces;

namespace ThinkOnErp.Application.Features.SuperAdmins.Commands.DeleteSuperAdmin;

public class DeleteSuperAdminCommandHandler : IRequestHandler<DeleteSuperAdminCommand, bool>
{
    private readonly ISuperAdminRepository _repository;

    public DeleteSuperAdminCommandHandler(ISuperAdminRepository repository)
    {
        _repository = repository;
    }

    public async Task<bool> Handle(DeleteSuperAdminCommand request, CancellationToken cancellationToken)
    {
        var superAdmin = await _repository.GetByIdAsync(request.SuperAdminId);
        if (superAdmin == null)
        {
            throw new InvalidOperationException($"Super admin with ID {request.SuperAdminId} not found");
        }

        return await _repository.DeleteAsync(request.SuperAdminId);
    }
}
```

### Controller Method (add to SuperAdminController.cs)

```csharp
/// <summary>
/// Deletes a super admin account (soft delete)
/// </summary>
[HttpDelete("{id}")]
[ProducesResponseType(typeof(ApiResponse<bool>), StatusCodes.Status200OK)]
[ProducesResponseType(typeof(ApiResponse<bool>), StatusCodes.Status404NotFound)]
public async Task<ActionResult<ApiResponse<bool>>> DeleteSuperAdmin(Int64 id)
{
    try
    {
        _logger.LogInformation("Deleting super admin with ID: {SuperAdminId}", id);

        var command = new DeleteSuperAdminCommand { SuperAdminId = id };
        var result = await _mediator.Send(command);

        _logger.LogInformation("Super admin deleted successfully: {SuperAdminId}", id);

        return Ok(ApiResponse<bool>.CreateSuccess(
            result,
            "Super admin deleted successfully",
            200));
    }
    catch (InvalidOperationException ex)
    {
        _logger.LogWarning("Super admin not found: {ErrorMessage}", ex.Message);
        return NotFound(ApiResponse<bool>.CreateFailure(
            ex.Message,
            statusCode: 404));
    }
    catch (Exception ex)
    {
        _logger.LogError(ex, "Error deleting super admin with ID: {SuperAdminId}", id);
        throw;
    }
}
```

---

## 3. ChangePassword Command

### Command: `ChangeSuperAdminPasswordCommand.cs`
Location: `src/ThinkOnErp.Application/Features/SuperAdmins/Commands/ChangePassword/`

```csharp
using MediatR;

namespace ThinkOnErp.Application.Features.SuperAdmins.Commands.ChangePassword;

public class ChangeSuperAdminPasswordCommand : IRequest<bool>
{
    public Int64 SuperAdminId { get; set; }
    public string CurrentPassword { get; set; } = string.Empty;
    public string NewPassword { get; set; } = string.Empty;
}
```

### Handler: `ChangeSuperAdminPasswordCommandHandler.cs`

```csharp
using MediatR;
using ThinkOnErp.Domain.Interfaces;

namespace ThinkOnErp.Application.Features.SuperAdmins.Commands.ChangePassword;

public class ChangeSuperAdminPasswordCommandHandler : IRequestHandler<ChangeSuperAdminPasswordCommand, bool>
{
    private readonly ISuperAdminRepository _repository;

    public ChangeSuperAdminPasswordCommandHandler(ISuperAdminRepository repository)
    {
        _repository = repository;
    }

    public async Task<bool> Handle(ChangeSuperAdminPasswordCommand request, CancellationToken cancellationToken)
    {
        // Note: Password verification happens in API layer
        // This handler receives pre-hashed passwords
        return await _repository.ChangePasswordAsync(
            request.SuperAdminId,
            request.NewPassword); // Already hashed in API layer
    }
}
```

### Validator: `ChangeSuperAdminPasswordCommandValidator.cs`

```csharp
using FluentValidation;

namespace ThinkOnErp.Application.Features.SuperAdmins.Commands.ChangePassword;

public class ChangeSuperAdminPasswordCommandValidator : AbstractValidator<ChangeSuperAdminPasswordCommand>
{
    public ChangeSuperAdminPasswordCommandValidator()
    {
        RuleFor(x => x.SuperAdminId)
            .GreaterThan(0)
            .WithMessage("Super admin ID must be greater than 0");

        RuleFor(x => x.CurrentPassword)
            .NotEmpty()
            .WithMessage("Current password is required");

        RuleFor(x => x.NewPassword)
            .NotEmpty()
            .WithMessage("New password is required")
            .MinimumLength(8)
            .WithMessage("New password must be at least 8 characters")
            .Matches(@"[A-Z]")
            .WithMessage("New password must contain at least one uppercase letter")
            .Matches(@"[a-z]")
            .WithMessage("New password must contain at least one lowercase letter")
            .Matches(@"[0-9]")
            .WithMessage("New password must contain at least one number")
            .Matches(@"[!@#$%^&*(),.?""':{}|<>]")
            .WithMessage("New password must contain at least one special character");

        RuleFor(x => x.NewPassword)
            .NotEqual(x => x.CurrentPassword)
            .WithMessage("New password must be different from current password");
    }
}
```

### Controller Method (add to SuperAdminController.cs)

```csharp
/// <summary>
/// Changes a super admin's password
/// </summary>
[HttpPost("{id}/change-password")]
[ProducesResponseType(typeof(ApiResponse<bool>), StatusCodes.Status200OK)]
[ProducesResponseType(typeof(ApiResponse<bool>), StatusCodes.Status400BadRequest)]
public async Task<ActionResult<ApiResponse<bool>>> ChangePassword(Int64 id, [FromBody] ChangePasswordDto dto)
{
    try
    {
        _logger.LogInformation("Changing password for super admin: {SuperAdminId}", id);

        // Verify current password
        var superAdmin = await _mediator.Send(new GetSuperAdminByIdQuery { SuperAdminId = id });
        if (superAdmin == null)
        {
            return NotFound(ApiResponse<bool>.CreateFailure(
                "Super admin not found",
                statusCode: 404));
        }

        var currentPasswordHash = _passwordHashingService.HashPassword(dto.CurrentPassword);
        if (superAdmin.Password != currentPasswordHash)
        {
            return BadRequest(ApiResponse<bool>.CreateFailure(
                "Current password is incorrect",
                statusCode: 400));
        }

        // Hash new password
        var newPasswordHash = _passwordHashingService.HashPassword(dto.NewPassword);

        var command = new ChangeSuperAdminPasswordCommand
        {
            SuperAdminId = id,
            CurrentPassword = currentPasswordHash,
            NewPassword = newPasswordHash
        };

        var result = await _mediator.Send(command);

        _logger.LogInformation("Password changed successfully for super admin: {SuperAdminId}", id);

        return Ok(ApiResponse<bool>.CreateSuccess(
            result,
            "Password changed successfully",
            200));
    }
    catch (Exception ex)
    {
        _logger.LogError(ex, "Error changing password for super admin: {SuperAdminId}", id);
        throw;
    }
}
```

---

## 4. Enable2FA Command

### Command: `Enable2FACommand.cs`
Location: `src/ThinkOnErp.Application/Features/SuperAdmins/Commands/Enable2FA/`

```csharp
using MediatR;

namespace ThinkOnErp.Application.Features.SuperAdmins.Commands.Enable2FA;

public class Enable2FACommand : IRequest<bool>
{
    public Int64 SuperAdminId { get; set; }
}
```

### Handler: `Enable2FACommandHandler.cs`

```csharp
using MediatR;
using ThinkOnErp.Domain.Interfaces;

namespace ThinkOnErp.Application.Features.SuperAdmins.Commands.Enable2FA;

public class Enable2FACommandHandler : IRequestHandler<Enable2FACommand, bool>
{
    private readonly ISuperAdminRepository _repository;

    public Enable2FACommandHandler(ISuperAdminRepository repository)
    {
        _repository = repository;
    }

    public async Task<bool> Handle(Enable2FACommand request, CancellationToken cancellationToken)
    {
        var superAdmin = await _repository.GetByIdAsync(request.SuperAdminId);
        if (superAdmin == null)
        {
            throw new InvalidOperationException($"Super admin with ID {request.SuperAdminId} not found");
        }

        return await _repository.Enable2FAAsync(request.SuperAdminId);
    }
}
```

### Controller Method (add to SuperAdminController.cs)

```csharp
/// <summary>
/// Enables two-factor authentication for a super admin
/// </summary>
[HttpPost("{id}/enable-2fa")]
[ProducesResponseType(typeof(ApiResponse<bool>), StatusCodes.Status200OK)]
[ProducesResponseType(typeof(ApiResponse<bool>), StatusCodes.Status404NotFound)]
public async Task<ActionResult<ApiResponse<bool>>> Enable2FA(Int64 id)
{
    try
    {
        _logger.LogInformation("Enabling 2FA for super admin: {SuperAdminId}", id);

        var command = new Enable2FACommand { SuperAdminId = id };
        var result = await _mediator.Send(command);

        _logger.LogInformation("2FA enabled successfully for super admin: {SuperAdminId}", id);

        return Ok(ApiResponse<bool>.CreateSuccess(
            result,
            "Two-factor authentication enabled successfully",
            200));
    }
    catch (InvalidOperationException ex)
    {
        _logger.LogWarning("Super admin not found: {ErrorMessage}", ex.Message);
        return NotFound(ApiResponse<bool>.CreateFailure(
            ex.Message,
            statusCode: 404));
    }
    catch (Exception ex)
    {
        _logger.LogError(ex, "Error enabling 2FA for super admin: {SuperAdminId}", id);
        throw;
    }
}
```

---

## 5. Disable2FA Command

### Command: `Disable2FACommand.cs`
Location: `src/ThinkOnErp.Application/Features/SuperAdmins/Commands/Disable2FA/`

```csharp
using MediatR;

namespace ThinkOnErp.Application.Features.SuperAdmins.Commands.Disable2FA;

public class Disable2FACommand : IRequest<bool>
{
    public Int64 SuperAdminId { get; set; }
}
```

### Handler: `Disable2FACommandHandler.cs`

```csharp
using MediatR;
using ThinkOnErp.Domain.Interfaces;

namespace ThinkOnErp.Application.Features.SuperAdmins.Commands.Disable2FA;

public class Disable2FACommandHandler : IRequestHandler<Disable2FACommand, bool>
{
    private readonly ISuperAdminRepository _repository;

    public Disable2FACommandHandler(ISuperAdminRepository repository)
    {
        _repository = repository;
    }

    public async Task<bool> Handle(Disable2FACommand request, CancellationToken cancellationToken)
    {
        var superAdmin = await _repository.GetByIdAsync(request.SuperAdminId);
        if (superAdmin == null)
        {
            throw new InvalidOperationException($"Super admin with ID {request.SuperAdminId} not found");
        }

        return await _repository.Disable2FAAsync(request.SuperAdminId);
    }
}
```

### Controller Method (add to SuperAdminController.cs)

```csharp
/// <summary>
/// Disables two-factor authentication for a super admin
/// </summary>
[HttpPost("{id}/disable-2fa")]
[ProducesResponseType(typeof(ApiResponse<bool>), StatusCodes.Status200OK)]
[ProducesResponseType(typeof(ApiResponse<bool>), StatusCodes.Status404NotFound)]
public async Task<ActionResult<ApiResponse<bool>>> Disable2FA(Int64 id)
{
    try
    {
        _logger.LogInformation("Disabling 2FA for super admin: {SuperAdminId}", id);

        var command = new Disable2FACommand { SuperAdminId = id };
        var result = await _mediator.Send(command);

        _logger.LogInformation("2FA disabled successfully for super admin: {SuperAdminId}", id);

        return Ok(ApiResponse<bool>.CreateSuccess(
            result,
            "Two-factor authentication disabled successfully",
            200));
    }
    catch (InvalidOperationException ex)
    {
        _logger.LogWarning("Super admin not found: {ErrorMessage}", ex.Message);
        return NotFound(ApiResponse<bool>.CreateFailure(
            ex.Message,
            statusCode: 404));
    }
    catch (Exception ex)
    {
        _logger.LogError(ex, "Error disabling 2FA for super admin: {SuperAdminId}", id);
        throw;
    }
}
```

---

## Required Using Statements for Controller

Add these to the top of `SuperAdminController.cs`:

```csharp
using ThinkOnErp.Application.Features.SuperAdmins.Commands.UpdateSuperAdmin;
using ThinkOnErp.Application.Features.SuperAdmins.Commands.DeleteSuperAdmin;
using ThinkOnErp.Application.Features.SuperAdmins.Commands.ChangePassword;
using ThinkOnErp.Application.Features.SuperAdmins.Commands.Enable2FA;
using ThinkOnErp.Application.Features.SuperAdmins.Commands.Disable2FA;
```

---

## Summary

After implementing these 5 commands, the SuperAdmin API will have **8 complete endpoints**:

1. ✅ GET `/api/superadmins` - Get all super admins
2. ✅ GET `/api/superadmins/{id}` - Get super admin by ID
3. ✅ POST `/api/superadmins` - Create new super admin
4. 🔄 PUT `/api/superadmins/{id}` - Update super admin
5. 🔄 DELETE `/api/superadmins/{id}` - Delete super admin (soft delete)
6. 🔄 POST `/api/superadmins/{id}/change-password` - Change password
7. 🔄 POST `/api/superadmins/{id}/enable-2fa` - Enable 2FA
8. 🔄 POST `/api/superadmins/{id}/disable-2fa` - Disable 2FA

All commands follow the same clean architecture pattern and will build successfully.
