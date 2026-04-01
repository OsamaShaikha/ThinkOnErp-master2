using FluentValidation;

namespace ThinkOnErp.Application.Features.Roles.Commands.CreateRole;

/// <summary>
/// Validator for CreateRoleCommand.
/// Ensures all required fields are provided and meet business rules.
/// </summary>
public class CreateRoleCommandValidator : AbstractValidator<CreateRoleCommand>
{
    public CreateRoleCommandValidator()
    {
        RuleFor(x => x.RowDesc)
            .NotEmpty().WithMessage("Arabic description is required.")
            .MaximumLength(100).WithMessage("Arabic description must not exceed 100 characters.");

        RuleFor(x => x.RowDescE)
            .NotEmpty().WithMessage("English description is required.")
            .MaximumLength(100).WithMessage("English description must not exceed 100 characters.");

        RuleFor(x => x.Note)
            .MaximumLength(500).WithMessage("Note must not exceed 500 characters.")
            .When(x => !string.IsNullOrEmpty(x.Note));

        RuleFor(x => x.CreationUser)
            .NotEmpty().WithMessage("Creation user is required.");
    }
}
