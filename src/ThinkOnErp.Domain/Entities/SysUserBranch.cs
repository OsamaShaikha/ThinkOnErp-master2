namespace ThinkOnErp.Domain.Entities;

public class SysUserBranch
{
    public long Id { get; set; }
    public long UserId { get; set; }
    public long BranchId { get; set; }
    public bool IsPrimary { get; set; }
    public string AssignedBy { get; set; } = string.Empty;
    public DateTime AssignedAt { get; set; } = DateTime.UtcNow;

    public SysUser? User { get; set; }
    public SysBranch? Branch { get; set; }
}
