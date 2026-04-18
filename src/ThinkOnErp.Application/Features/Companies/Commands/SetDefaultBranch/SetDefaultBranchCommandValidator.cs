using FluentValidation;

namespace ThinkOnErp.Application.Features.Companies.Commands.SetDefaultBranch;

/// <summary>
/// Validator for SetDefaultBranchCommand.
/// Ensures that the command contains valid company ID, branch ID, and user information.
/// </summary>
public class SetDefaultBranchCommandValidator : AbstractValidator<SetDefaultBranchCommand>
{
    /// <summary>
    /// Initializes a new instance of the SetDefaultBranchCommandValidator class.
    /// Defines validation rules for setting default branch.
    /// </summary>
    public SetDefaultBranchCommandValidator()
    {
        RuleFor(x => x.CompanyId)
            .GreaterThan(0)
            .WithMessage("Company ID must be greater than 0");

        RuleFor(x => x.BranchId)
            .GreaterThan(0)
            .WithMessage("Branch ID must be greater than 0");

        RuleFor(x => x.UpdateUser)
            .NotEmpty()
            .WithMessage("Update user is required")
            .MaximumLength(100)
            .WithMessage("Update user cannot exceed 100 characters");
    }
}