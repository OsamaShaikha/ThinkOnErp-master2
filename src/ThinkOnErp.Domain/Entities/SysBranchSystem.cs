namespace ThinkOnErp.Domain.Entities;

public class SysBranchSystem
{
    public long Id { get; set; }
    public long BranchId { get; set; }
    public long SystemId { get; set; }
    public long? GrantedBy { get; set; }
    public DateTime? GrantedDate { get; set; }
    public DateTime? RevokedDate { get; set; }
    public string? Notes { get; set; }
    public string CreationUser { get; set; } = string.Empty;
    public DateTime? CreationDate { get; set; }
    public string? UpdateUser { get; set; }
    public DateTime? UpdateDate { get; set; }
    public SysBranch? Branch { get; set; }
    public SysSystem? System { get; set; }
    public SysSuperAdmin? GrantedBySuperAdmin { get; set; }
}
