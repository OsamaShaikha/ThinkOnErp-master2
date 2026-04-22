using FluentValidation;

namespace ThinkOnErp.Application.Features.Users.Commands.ResetUserPassword;

/// <summary>
/// Validator for ResetUserPasswordCommand
/// Ensures the command has valid data before processing
/// </summary>
public class ResetUserPasswordCommandValidator : AbstractValidator<ResetUserPasswordCommand>
{
    public ResetUserPasswordCommandValidator()
    {
        RuleFor(x => x.UserId)
            .GreaterThan(0)
            .WithMessage("User ID must be greater than 0");

        RuleFor(x => x.UpdateUser)
            .NotEmpty()
            .WithMessage("Update user is required");
    }
}
