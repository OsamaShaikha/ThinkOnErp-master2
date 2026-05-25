namespace ThinkOnErp.Application.DTOs.Branch;

public class CreateBranchDto
{
    public Int64? CompanyId { get; set; }
    public string BranchNameAr { get; set; } = string.Empty;
    public string BranchNameEn { get; set; } = string.Empty;
    public string? Phone { get; set; }
    public string? Mobile { get; set; }
    public string? Fax { get; set; }
    public string? Email { get; set; }
    public string? TaxNumber { get; set; }
    public bool IsHeadBranch { get; set; }
    public int? DefaultLang { get; set; } = 1;
    public Int64? BaseCurrencyId { get; set; }
    public int? RoundingRules { get; set; }
    public List<long>? Systems { get; set; }
}
