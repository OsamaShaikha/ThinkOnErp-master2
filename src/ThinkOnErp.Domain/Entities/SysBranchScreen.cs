namespace ThinkOnErp.Domain.Entities;

public class SysBranchScreen
{
    public long Id { get; set; }
    public long BranchId { get; set; }
    public long ScreenId { get; set; }
    public long? RevokedBy { get; set; }
    public DateTime? RevokedDate { get; set; }
    public string? Notes { get; set; }
    public string CreationUser { get; set; } = string.Empty;
    public DateTime? CreationDate { get; set; }
    public string? UpdateUser { get; set; }
    public DateTime? UpdateDate { get; set; }
    public SysBranch? Branch { get; set; }
    public SysScreen? Screen { get; set; }
    public SysSuperAdmin? RevokedBySuperAdmin { get; set; }
}
