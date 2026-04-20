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
