namespace ThinkOnErp.Application.DTOs.Company;

public class UpdateCompanyDto
{
    public string CompanyNameLocal { get; set; } = string.Empty;
    public string CompanyNameEn { get; set; } = string.Empty;
    public Int64? CountryId { get; set; }
    public Int64? CurrId { get; set; }
    public string? LegalNameLocal { get; set; }
    public string? LegalNameEn { get; set; }
    public string? CompanyCode { get; set; }

    /// <summary>
    /// Foreign key to SYS_BRANCH table - references the default/head branch for this company (optional)
    /// </summary>
    public Int64? DefaultBranchId { get; set; }
}
