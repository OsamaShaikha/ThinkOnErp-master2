using FluentValidation;

namespace ThinkOnErp.Application.Features.Auth.Commands.RefreshToken;

/// <summary>
/// Validator for RefreshTokenCommand.
/// Ensures refresh token is provided and meets basic requirements.
/// </summary>
public class RefreshTokenCommandValidator : AbstractValidator<RefreshTokenCommand>
{
    public RefreshTokenCommandValidator()
    {
        RuleFor(x => x.RefreshToken)
            .NotEmpty()
            .WithMessage("Refresh token is required")
            .MinimumLength(10)
            .WithMessage("Invalid refresh token format");
    }
}
