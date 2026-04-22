using FluentValidation;

namespace ThinkOnErp.Application.DTOs.SuperAdmin;

/// <summary>
/// Validator for SuperAdminChangePasswordDto
/// Validates the DTO before password hashing occurs
/// </summary>
public class SuperAdminChangePasswordDtoValidator : AbstractValidator<SuperAdminChangePasswordDto>
{
    public SuperAdminChangePasswordDtoValidator()
    {
        RuleFor(x => x.CurrentPassword)
            .NotEmpty()
            .WithMessage("Current password is required");

        RuleFor(x => x.NewPassword)
            .NotEmpty()
            .WithMessage("New password is required")
            .MinimumLength(8)
            .WithMessage("New password must be at least 8 characters long")
            .Matches(@"[A-Z]")
            .WithMessage("New password must contain at least one uppercase letter")
            .Matches(@"[a-z]")
            .WithMessage("New password must contain at least one lowercase letter")
            .Matches(@"[0-9]")
            .WithMessage("New password must contain at least one number")
            .Matches(@"[\W_]")
            .WithMessage("New password must contain at least one special character");

        RuleFor(x => x.ConfirmPassword)
            .Equal(x => x.NewPassword)
            .WithMessage("Confirm password must match new password");
    }
}
