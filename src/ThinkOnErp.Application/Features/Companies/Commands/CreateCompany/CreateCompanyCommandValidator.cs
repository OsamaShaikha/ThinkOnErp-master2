using FluentValidation;

namespace ThinkOnErp.Application.Features.Companies.Commands.CreateCompany;

public class CreateCompanyCommandValidator : AbstractValidator<CreateCompanyCommand>
{
    public CreateCompanyCommandValidator()
    {
        RuleFor(x => x.CompanyNameAr).NotEmpty().MaximumLength(100);
        RuleFor(x => x.CompanyNameEn).NotEmpty().MaximumLength(100);
        RuleFor(x => x.CreationUser).NotEmpty();
    }
}
