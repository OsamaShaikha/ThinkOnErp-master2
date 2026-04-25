using FluentValidation;

namespace ThinkOnErp.Application.Features.Companies.Commands.CreateCompanyWithBranch;

/// <summary>
/// Validator for CreateCompanyWithBranchCommand.
/// Ensures all required fields are provided and validates business rules.
/// </summary>
public class CreateCompanyWithBranchCommandValidator : AbstractValidator<CreateCompanyWithBranchCommand>
{
    public CreateCompanyWithBranchCommandValidator()
    {
        // Company Name Validation
        RuleFor(x => x.CompanyNameEn)
            .NotEmpty()
            .WithMessage("Company English name is required")
            .MaximumLength(200)
            .WithMessage("Company English name cannot exceed 200 characters");

        RuleFor(x => x.CompanyNameAr)
            .MaximumLength(200)
            .WithMessage("Company Arabic name cannot exceed 200 characters")
            .When(x => !string.IsNullOrEmpty(x.CompanyNameAr));

        // Legal Name Validation
        RuleFor(x => x.LegalNameEn)
            .NotEmpty()
            .WithMessage("Legal English name is required")
            .MaximumLength(200)
            .WithMessage("Legal English name cannot exceed 200 characters");

        RuleFor(x => x.LegalNameAr)
            .MaximumLength(200)
            .WithMessage("Legal Arabic name cannot exceed 200 characters")
            .When(x => !string.IsNullOrEmpty(x.LegalNameAr));

        // Company Code Validation
        RuleFor(x => x.CompanyCode)
            .NotEmpty()
            .WithMessage("Company code is required")
            .MaximumLength(50)
            .WithMessage("Company code cannot exceed 50 characters")
            .Matches("^[A-Z0-9_-]+$")
            .WithMessage("Company code can only contain uppercase letters, numbers, underscores, and hyphens");

        // Language Validation
        RuleFor(x => x.DefaultLang)
            .Must(lang => lang == "ar" || lang == "en")
            .WithMessage("Default language must be 'ar' or 'en'")
            .When(x => !string.IsNullOrEmpty(x.DefaultLang));

        // Tax Number Validation
        RuleFor(x => x.TaxNumber)
            .MaximumLength(50)
            .WithMessage("Tax number cannot exceed 50 characters")
            .When(x => !string.IsNullOrEmpty(x.TaxNumber));

        // Branch Name Validation (Optional)
        RuleFor(x => x.BranchNameEn)
            .MaximumLength(200)
            .WithMessage("Branch English name cannot exceed 200 characters")
            .When(x => !string.IsNullOrEmpty(x.BranchNameEn));

        RuleFor(x => x.BranchNameAr)
            .MaximumLength(200)
            .WithMessage("Branch Arabic name cannot exceed 200 characters")
            .When(x => !string.IsNullOrEmpty(x.BranchNameAr));

        // Contact Information Validation (Optional)
        RuleFor(x => x.BranchPhone)
            .MaximumLength(20)
            .WithMessage("Branch phone cannot exceed 20 characters")
            .When(x => !string.IsNullOrEmpty(x.BranchPhone));

        RuleFor(x => x.BranchMobile)
            .MaximumLength(20)
            .WithMessage("Branch mobile cannot exceed 20 characters")
            .When(x => !string.IsNullOrEmpty(x.BranchMobile));

        RuleFor(x => x.BranchFax)
            .MaximumLength(20)
            .WithMessage("Branch fax cannot exceed 20 characters")
            .When(x => !string.IsNullOrEmpty(x.BranchFax));

        RuleFor(x => x.BranchEmail)
            .EmailAddress()
            .WithMessage("Branch email must be a valid email address")
            .MaximumLength(100)
            .WithMessage("Branch email cannot exceed 100 characters")
            .When(x => !string.IsNullOrEmpty(x.BranchEmail));

        // Creation User Validation
        RuleFor(x => x.CreationUser)
            .NotEmpty()
            .WithMessage("Creation user is required")
            .MaximumLength(50)
            .WithMessage("Creation user cannot exceed 50 characters");

        // ID Validation (must be positive if provided)
        RuleFor(x => x.BranchFiscalYearId)
            .GreaterThan(0)
            .WithMessage("Branch fiscal year ID must be greater than 0")
            .When(x => x.BranchFiscalYearId.HasValue);

        RuleFor(x => x.CountryId)
            .GreaterThan(0)
            .WithMessage("Country ID must be greater than 0")
            .When(x => x.CountryId.HasValue);

        RuleFor(x => x.CurrId)
            .GreaterThan(0)
            .WithMessage("Currency ID must be greater than 0")
            .When(x => x.CurrId.HasValue);

        RuleFor(x => x.BranchBaseCurrencyId)
            .GreaterThan(0)
            .WithMessage("Branch base currency ID must be greater than 0")
            .When(x => x.BranchBaseCurrencyId.HasValue);

        RuleFor(x => x.BranchRoundingRules)
            .InclusiveBetween(1, 6)
            .WithMessage("Branch rounding rules must be between 1 and 6")
            .When(x => x.BranchRoundingRules.HasValue);

        // Base64 Logo Validation
        RuleFor(x => x.CompanyLogoBase64)
            .Must(BeValidBase64)
            .WithMessage("Company logo must be a valid Base64 string")
            .Must(BeValidBase64Size)
            .WithMessage("Company logo size cannot exceed 5MB when decoded")
            .When(x => !string.IsNullOrEmpty(x.CompanyLogoBase64));

        RuleFor(x => x.BranchLogoBase64)
            .Must(BeValidBase64)
            .WithMessage("Branch logo must be a valid Base64 string")
            .Must(BeValidBase64Size)
            .WithMessage("Branch logo size cannot exceed 5MB when decoded")
            .When(x => !string.IsNullOrEmpty(x.BranchLogoBase64));
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