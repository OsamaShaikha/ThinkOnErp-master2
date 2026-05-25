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

        RuleFor(x => x.LegalNameAr).MaximumLength(300).When(x => !string.IsNullOrEmpty(x.LegalNameAr));
        RuleFor(x => x.LegalNameEn).MaximumLength(300).When(x => !string.IsNullOrEmpty(x.LegalNameEn));
        
        RuleFor(x => x.CompanyCode)
            .MaximumLength(50)
            .When(x => !string.IsNullOrEmpty(x.CompanyCode));

        RuleFor(x => x.CompanyLogo)
            .Must(l => l == null || l.Length <= 5 * 1024 * 1024)
            .WithMessage("Company logo size cannot exceed 5MB")
            .When(x => x.CompanyLogo != null);
    }
}
