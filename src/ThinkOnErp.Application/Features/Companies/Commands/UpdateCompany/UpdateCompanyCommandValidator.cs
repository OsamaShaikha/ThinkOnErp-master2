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

        RuleFor(x => x.SystemLanguage)
            .Must(lang => lang == "ar" || lang == "en")
            .WithMessage("System language must be 'ar' or 'en'")
            .When(x => !string.IsNullOrEmpty(x.SystemLanguage));

        RuleFor(x => x.TaxNumber).MaximumLength(50).When(x => !string.IsNullOrEmpty(x.TaxNumber));

        RuleFor(x => x.RoundingRules)
            .Must(rule => new[] { "HALF_UP", "HALF_DOWN", "UP", "DOWN", "CEILING", "FLOOR" }.Contains(rule))
            .WithMessage("Rounding rules must be one of: HALF_UP, HALF_DOWN, UP, DOWN, CEILING, FLOOR")
            .When(x => !string.IsNullOrEmpty(x.RoundingRules));

        RuleFor(x => x.FiscalYearId).GreaterThan(0).When(x => x.FiscalYearId.HasValue);
        RuleFor(x => x.BaseCurrencyId).GreaterThan(0).When(x => x.BaseCurrencyId.HasValue);
    }
}
