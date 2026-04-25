using FluentValidation;

namespace ThinkOnErp.Application.Features.Companies.Commands.CreateCompany;

public class CreateCompanyCommandValidator : AbstractValidator<CreateCompanyCommand>
{
    public CreateCompanyCommandValidator()
    {
        RuleFor(x => x.CompanyNameAr).NotEmpty().MaximumLength(100);
        RuleFor(x => x.CompanyNameEn).NotEmpty().MaximumLength(100);
        RuleFor(x => x.CreationUser).NotEmpty();

        // New field validations
        RuleFor(x => x.LegalNameAr).MaximumLength(300).When(x => !string.IsNullOrEmpty(x.LegalNameAr));
        RuleFor(x => x.LegalNameEn).MaximumLength(300).When(x => !string.IsNullOrEmpty(x.LegalNameEn));
        
        RuleFor(x => x.CompanyCode)
            .MaximumLength(50)
            .When(x => !string.IsNullOrEmpty(x.CompanyCode));

        RuleFor(x => x.DefaultLang)
            .Must(lang => lang == "ar" || lang == "en")
            .WithMessage("Default language must be 'ar' or 'en'")
            .When(x => !string.IsNullOrEmpty(x.DefaultLang));

        RuleFor(x => x.TaxNumber).MaximumLength(50).When(x => !string.IsNullOrEmpty(x.TaxNumber));
    }
}
