namespace ThinkOnErp.Domain.Entities;

public class SysBranch
{
    public Int64 Id { get; set; }
    public Int64? CompanyId { get; set; }
    public string BranchNameAr { get; set; } = string.Empty;
    public string BranchNameEn { get; set; } = string.Empty;
    public string? Phone { get; set; }
    public string? Mobile { get; set; }
    public string? Fax { get; set; }
    public string? Email { get; set; }
    public string? TaxNumber { get; set; }
    public bool IsHeadBranch { get; set; }
    public int? DefaultLang { get; set; }
    public Int64? BaseCurrencyId { get; set; }
    public int? RoundingRules { get; set; }
    public string? BranchLogoPath { get; set; }
    public bool IsActive { get; set; }
    public string CreationUser { get; set; } = string.Empty;
    public DateTime? CreationDate { get; set; }
    public string? UpdateUser { get; set; }
    public DateTime? UpdateDate { get; set; }
    public SysCurrency? BaseCurrency { get; set; }
    public SysCompany? Company { get; set; }
    public ICollection<SysBranchSystem> SystemAccess { get; set; } = new List<SysBranchSystem>();
    public ICollection<SysBranchScreenPermission> ScreenPermissions { get; set; } = new List<SysBranchScreenPermission>();
}
