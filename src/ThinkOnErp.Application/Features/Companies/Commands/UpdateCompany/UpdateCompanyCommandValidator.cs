using FluentValidation;

namespace ThinkOnErp.Application.Features.Companies.Commands.UpdateCompany;

public class UpdateCompanyCommandValidator : AbstractValidator<UpdateCompanyCommand>
{
    public UpdateCompanyCommandValidator()
    {
        RuleFor(x => x.CompanyId).GreaterThan(0);
        RuleFor(x => x.CompanyNameAr).NotEmpty().MaximumLength(100);
        RuleFor(x => x.CompanyNameEn).NotEmpty().MaximumLength(100);
        RuleFor(x => x.UpdateUser).NotEmpty();
    }
}
