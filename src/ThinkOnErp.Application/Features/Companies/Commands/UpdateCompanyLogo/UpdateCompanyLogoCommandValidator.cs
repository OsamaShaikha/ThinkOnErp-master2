using FluentValidation;

namespace ThinkOnErp.Application.Features.Companies.Commands.UpdateCompanyLogo;

public class UpdateCompanyLogoCommandValidator : AbstractValidator<UpdateCompanyLogoCommand>
{
    public UpdateCompanyLogoCommandValidator()
    {
        RuleFor(x => x.CompanyId)
            .GreaterThan(0)
            .WithMessage("Company ID is required");

        RuleFor(x => x.Logo)
            .NotEmpty()
            .WithMessage("Logo data is required")
            .Must(logo => logo.Length <= 5 * 1024 * 1024) // 5MB limit
            .WithMessage("Logo file size cannot exceed 5MB");

        RuleFor(x => x.UpdateUser)
            .NotEmpty()
            .WithMessage("Update user is required");
    }
}