using FluentValidation;

namespace ThinkOnErp.Application.Features.SuperAdmins.Commands.ResetSuperAdminPassword;

/// <summary>
/// Validator for reset super admin password command
/// </summary>
public class ResetSuperAdminPasswordCommandValidator : AbstractValidator<ResetSuperAdminPasswordCommand>
{
    public ResetSuperAdminPasswordCommandValidator()
    {
        RuleFor(x => x.SuperAdminId)
            .GreaterThan(0)
            .WithMessage("Super admin ID must be greater than 0");

        RuleFor(x => x.UpdateUser)
            .NotEmpty()
            .WithMessage("Update user is required");
    }
}
