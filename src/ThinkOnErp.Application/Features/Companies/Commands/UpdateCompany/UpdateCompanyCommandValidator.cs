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

        RuleFor(x => x.TaxNumber).MaximumLength(50).When(x => !string.IsNullOrEmpty(x.TaxNumber));

        RuleFor(x => x.FiscalYearId).GreaterThan(0).When(x => x.FiscalYearId.HasValue);

        // Base64 Logo Validation
        RuleFor(x => x.CompanyLogoBase64)
            .Must(BeValidBase64)
            .WithMessage("Company logo must be a valid Base64 string")
            .Must(BeValidBase64Size)
            .WithMessage("Company logo size cannot exceed 5MB when decoded")
            .When(x => !string.IsNullOrEmpty(x.CompanyLogoBase64));
    }

    /// <summary>
    /// Validates if a string is a valid Base64 format
    /// </summary>
    private static bool BeValidBase64(string? base64String)
    {
        if (string.IsNullOrEmpty(base64String))
            return true;

        try
        {
            // Remove data URL prefix if present (e.g., "data:image/jpeg;base64,")
            var base64Data = base64String;
            if (base64String.Contains(','))
            {
                base64Data = base64String.Split(',')[1];
            }

            Convert.FromBase64String(base64Data);
            return true;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Validates if Base64 decoded size is within 5MB limit
    /// </summary>
    private static bool BeValidBase64Size(string? base64String)
    {
        if (string.IsNullOrEmpty(base64String))
            return true;

        try
        {
            // Remove data URL prefix if present
            var base64Data = base64String;
            if (base64String.Contains(','))
            {
                base64Data = base64String.Split(',')[1];
            }

            var bytes = Convert.FromBase64String(base64Data);
            const int maxSize = 5 * 1024 * 1024; // 5MB
            return bytes.Length <= maxSize;
        }
        catch
        {
            return false;
        }
    }
}
