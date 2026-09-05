namespace ThinkOnErp.Application.DTOs.Company;

public class CreateCompanyDto
{
    public string CompanyNameLocal { get; set; } = string.Empty;
    public string CompanyNameEn { get; set; } = string.Empty;
    public Int64? CountryId { get; set; }
    public Int64? CurrId { get; set; }
    public string? LegalNameLocal { get; set; }
    public string? LegalNameEn { get; set; }
    public string? CompanyCode { get; set; }
    public string? TaxNumber { get; set; }
    public string? BranchNameLocal { get; set; }
    public string? BranchNameEn { get; set; }
    public string? BranchPhone { get; set; }
    public string? BranchMobile { get; set; }
    public string? BranchFax { get; set; }
    public string? BranchEmail { get; set; }

    /// <summary>
    /// Default language for the branch (ar/en) (optional, defaults to 'ar')
    /// </summary>
    public int? BranchDefaultLang { get; set; }

    /// <summary>
    /// Base currency ID for the branch (optional)
    /// </summary>
    public Int64? BranchBaseCurrencyId { get; set; }
    public int? BranchRoundingRules { get; set; }
    public List<long>? Systems { get; set; }
}
