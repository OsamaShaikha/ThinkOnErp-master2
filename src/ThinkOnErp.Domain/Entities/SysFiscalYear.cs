namespace ThinkOnErp.Domain.Entities;

public class SysFiscalYear
{
    public Int64 Id { get; set; }
    public Int64 CompanyId { get; set; }
    public Int64 BranchId { get; set; }
    public string FiscalYearCode { get; set; } = string.Empty;
    public string? FiscalYearNameAr { get; set; }
    public string? FiscalYearNameEn { get; set; }
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    public bool IsClosed { get; set; }
    public bool IsActive { get; set; }
    public string CreationUser { get; set; } = string.Empty;
    public DateTime? CreationDate { get; set; }
    public string? UpdateUser { get; set; }
    public DateTime? UpdateDate { get; set; }
    public SysCompany? Company { get; set; }
    public SysBranch? Branch { get; set; }
}
