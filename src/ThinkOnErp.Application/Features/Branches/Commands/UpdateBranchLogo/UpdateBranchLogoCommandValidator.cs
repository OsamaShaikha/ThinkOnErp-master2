using FluentValidation;

namespace ThinkOnErp.Application.Features.Branches.Commands.UpdateBranchLogo;

/// <summary>
/// Validator for UpdateBranchLogoCommand.
/// Ensures branch ID is valid and logo size is within limits.
/// </summary>
public class UpdateBranchLogoCommandValidator : AbstractValidator<UpdateBranchLogoCommand>
{
    public UpdateBranchLogoCommandValidator()
    {
        // Branch ID Validation
        RuleFor(x => x.BranchId)
            .GreaterThan(0)
            .WithMessage("Branch ID must be greater than 0");

        // Logo Size Validation (5MB limit)
        RuleFor(x => x.Logo)
            .Must(logo => logo.Length <= 5 * 1024 * 1024)
            .WithMessage("Logo file size cannot exceed 5MB")
            .When(x => x.Logo.Length > 0);

        // Update User Validation
        RuleFor(x => x.UpdateUser)
            .NotEmpty()
            .WithMessage("Update user is required")
            .MaximumLength(50)
            .WithMessage("Update user cannot exceed 50 characters");
    }
}