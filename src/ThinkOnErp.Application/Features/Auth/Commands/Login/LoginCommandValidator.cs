using FluentValidation;

namespace ThinkOnErp.Application.Features.Auth.Commands.Login;

/// <summary>
/// Validator for LoginCommand.
/// Ensures username and password are provided.
/// </summary>
public class LoginCommandValidator : AbstractValidator<LoginCommand>
{
    public LoginCommandValidator()
    {
        RuleFor(x => x.UserName)
            .NotEmpty().WithMessage("Username is required.");

        RuleFor(x => x.Password)
            .NotEmpty().WithMessage("Password is required.");
    }
}
