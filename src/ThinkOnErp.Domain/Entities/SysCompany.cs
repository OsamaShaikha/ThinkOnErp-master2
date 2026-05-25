namespace ThinkOnErp.Domain.Entities;

public class SysCompany
{
    public Int64 Id { get; set; }
    public string CompanyNameAr { get; set; } = string.Empty;
    public string CompanyNameEn { get; set; } = string.Empty;
    public Int64? CountryId { get; set; }
    public Int64? CurrId { get; set; }
    public string? LegalName { get; set; }
    public string? LegalNameE { get; set; }
    public string? CompanyCode { get; set; }
    public Int64? DefaultBranchId { get; set; }
    public byte[]? CompanyLogo { get; set; }
    public bool HasLogo => CompanyLogo != null && CompanyLogo.Length > 0;
    public bool IsActive { get; set; }
    public string CreationUser { get; set; } = string.Empty;
    public DateTime? CreationDate { get; set; }
    public string? UpdateUser { get; set; }
    public DateTime? UpdateDate { get; set; }
    public SysCurrency? Currency { get; set; }
    public SysBranch? DefaultBranch { get; set; }
    public ICollection<SysBranch> Branches { get; set; } = new List<SysBranch>();
    public ICollection<SysCompanyScreenPermission> ScreenPermissions { get; set; } = new List<SysCompanyScreenPermission>();
    public ICollection<SysBranchSystem> BranchSystemAccess { get; set; } = new List<SysBranchSystem>();
    public ICollection<SysBranchScreenPermission> BranchScreenPermissions { get; set; } = new List<SysBranchScreenPermission>();
}
