using ThinkOnErp.Application.DTOs.Accounting.Tax;
using ThinkOnErp.Domain.Entities.Accounting;

namespace ThinkOnErp.Application.Services.Accounting.Tax.Declarations;

/// <summary>
/// Defines the contract for country-specific and jurisdiction-specific Tax Return / Declaration providers.
/// </summary>
public interface ITaxDeclarationProvider
{
    /// <summary>
    /// Unique template identifier (e.g. GCC_ZATCA_16, JO_SALES_TAX, EG_VAT_FORM_10, UK_EU_VAT_9, GLOBAL_GENERIC).
    /// </summary>
    string TemplateCode { get; }

    /// <summary>
    /// ISO 3166-1 alpha-2 country code (e.g. SA, JO, EG, GB, GLOBAL).
    /// </summary>
    string CountryCode { get; }

    /// <summary>
    /// Returns the descriptive metadata, sections, and box definitions for this template.
    /// </summary>
    TaxDeclarationTemplateDto GetTemplateMetadata();

    /// <summary>
    /// Computes and builds the itemized tax declaration from raw transactions and rates.
    /// </summary>
    VatDeclarationDto BuildDeclaration(
        VatDeclarationFilterDto filter,
        IReadOnlyList<TaxTransaction> transactions,
        IReadOnlyList<TaxRate> availableRates,
        string branchNameAr,
        string branchNameEn);
}
