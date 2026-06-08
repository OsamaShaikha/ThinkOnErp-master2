using FluentValidation;

namespace ThinkOnErp.Application.Features.Auth.Commands.Login;

public class CompanyLoginCommandValidator : AbstractValidator<CompanyLoginCommand>
{
    public CompanyLoginCommandValidator()
    {
        RuleFor(x => x.UserName)
            .NotEmpty().WithMessage("Username is required.");

        RuleFor(x => x.Password)
            .NotEmpty().WithMessage("Password is required.");

        RuleFor(x => x.CompanyCode)
            .NotEmpty().WithMessage("Company code is required.");
    }
}
