using MediatR;

namespace ThinkOnErp.Application.Features.Companies.Commands.CreateCompanyWithBranch;

/// <summary>
/// Command to create a new company with an automatic default branch.
/// This command creates both a company and its head office branch in a single transaction.
/// </summary>
public class CreateCompanyWithBranchCommand : IRequest<CreateCompanyWithBranchResult>
{
    // Company Information
    /// <summary>
    /// Arabic name/description of the company
    /// </summary>
    public string CompanyNameAr { get; set; } = string.Empty;

    /// <summary>
    /// English name/description of the company (required)
    /// </summary>
    public string CompanyNameEn { get; set; } = string.Empty;

    /// <summary>
    /// Legal name of the company in Arabic
    /// </summary>
    public string? LegalNameAr { get; set; }

    /// <summary>
    /// Legal name of the company in English (required)
    /// </summary>
    public string LegalNameEn { get; set; } = string.Empty;

    /// <summary>
    /// Unique company code (required)
    /// </summary>
    public string CompanyCode { get; set; } = string.Empty;

    /// <summary>
    /// Default language for the company (ar/en)
    /// </summary>
    public string DefaultLang { get; set; } = "ar";

    /// <summary>
    /// Tax registration number
    /// </summary>
    public string? TaxNumber { get; set; }

    /// <summary>
    /// Country ID
    /// </summary>
    public Int64? CountryId { get; set; }

    /// <summary>
    /// Currency ID (legacy field)
    /// </summary>
    public Int64? CurrId { get; set; }

    // Branch Information (Optional - defaults will be generated)
    /// <summary>
    /// Arabic name for the default branch (optional - will be auto-generated if not provided)
    /// </summary>
    public string? BranchNameAr { get; set; }

    /// <summary>
    /// English name for the default branch (optional - will be auto-generated if not provided)
    /// </summary>
    public string? BranchNameEn { get; set; }

    /// <summary>
    /// Phone number for the default branch
    /// </summary>
    public string? BranchPhone { get; set; }

    /// <summary>
    /// Mobile number for the default branch
    /// </summary>
    public string? BranchMobile { get; set; }

    /// <summary>
    /// Fax number for the default branch
    /// </summary>
    public string? BranchFax { get; set; }

    /// <summary>
    /// Email address for the default branch
    /// </summary>
    public string? BranchEmail { get; set; }

    /// <summary>
    /// Logo for the default branch (optional)
    /// </summary>
    public byte[]? BranchLogo { get; set; }

    /// <summary>
    /// Company logo as Base64 string (optional)
    /// </summary>
    public string? CompanyLogoBase64 { get; set; }

    /// <summary>
    /// Branch logo as Base64 string (optional)
    /// </summary>
    public string? BranchLogoBase64 { get; set; }

    /// <summary>
    /// Base currency ID for the branch (optional)
    /// </summary>
    public Int64? BranchBaseCurrencyId { get; set; }

    /// <summary>
    /// Rounding rules for branch calculations (1=HALF_UP, 2=HALF_DOWN, 3=UP, 4=DOWN, 5=CEILING, 6=FLOOR)
    /// </summary>
    public int? BranchRoundingRules { get; set; }

    /// <summary>
    /// Fiscal year ID for the branch (optional)
    /// </summary>
    public Int64? BranchFiscalYearId { get; set; }

    // Audit Information
    /// <summary>
    /// Username of the user creating the company and branch
    /// </summary>
    public string CreationUser { get; set; } = string.Empty;
}

/// <summary>
/// Result object returned when creating a company with branch
/// </summary>
public class CreateCompanyWithBranchResult
{
    /// <summary>
    /// ID of the newly created company
    /// </summary>
    public Int64 CompanyId { get; set; }

    /// <summary>
    /// ID of the newly created default branch
    /// </summary>
    public Int64 BranchId { get; set; }

    /// <summary>
    /// ID of the newly created default fiscal year
    /// </summary>
    public Int64 FiscalYearId { get; set; }

    /// <summary>
    /// Company code of the created company
    /// </summary>
    public string CompanyCode { get; set; } = string.Empty;

    /// <summary>
    /// English name of the created company
    /// </summary>
    public string CompanyName { get; set; } = string.Empty;

    /// <summary>
    /// English name of the created branch
    /// </summary>
    public string BranchName { get; set; } = string.Empty;
}