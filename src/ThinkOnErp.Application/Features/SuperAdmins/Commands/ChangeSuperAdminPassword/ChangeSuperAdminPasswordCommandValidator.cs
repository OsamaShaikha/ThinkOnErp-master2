using FluentValidation;

namespace ThinkOnErp.Application.Features.SuperAdmins.Commands.ChangeSuperAdminPassword;

/// <summary>
/// Validator for change super admin password command
/// </summary>
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

        // Password validation removed - it's validated in the DTO before hashing
        RuleFor(x => x.NewPassword)
            .NotEmpty()
            .WithMessage("New password is required");

        RuleFor(x => x.ConfirmPassword)
            .NotEmpty()
            .WithMessage("Confirm password is required");

        RuleFor(x => x.UpdateUser)
            .NotEmpty()
            .WithMessage("Update user is required");
    }
}
