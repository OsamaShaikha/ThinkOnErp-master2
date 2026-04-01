using FluentValidation;

namespace ThinkOnErp.Application.Features.Roles.Commands.UpdateRole;

/// <summary>
/// Validator for UpdateRoleCommand.
/// Ensures all required fields are provided and meet business rules.
/// </summary>
public class UpdateRoleCommandValidator : AbstractValidator<UpdateRoleCommand>
{
    public UpdateRoleCommandValidator()
    {
        RuleFor(x => x.RowId)
            .GreaterThan(0).WithMessage("Role ID must be greater than 0.");

        RuleFor(x => x.RowDesc)
            .NotEmpty().WithMessage("Arabic description is required.")
            .MaximumLength(100).WithMessage("Arabic description must not exceed 100 characters.");

        RuleFor(x => x.RowDescE)
            .NotEmpty().WithMessage("English description is required.")
            .MaximumLength(100).WithMessage("English description must not exceed 100 characters.");

        RuleFor(x => x.Note)
            .MaximumLength(500).WithMessage("Note must not exceed 500 characters.")
            .When(x => !string.IsNullOrEmpty(x.Note));

        RuleFor(x => x.UpdateUser)
            .NotEmpty().WithMessage("Update user is required.");
    }
}
