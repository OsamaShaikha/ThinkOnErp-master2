namespace ThinkOnErp.Domain.Entities;

public class SysRoleScreenPermission
{
    public long Id { get; set; }
    public long BranchId { get; set; }
    public long RoleId { get; set; }
    public long ScreenId { get; set; }
    public long FeatureId { get; set; }
    public bool IsGranted { get; set; }
    public string CreationUser { get; set; } = string.Empty;
    public DateTime? CreationDate { get; set; }
    public string? UpdateUser { get; set; }
    public DateTime? UpdateDate { get; set; }
    public SysBranch? Branch { get; set; }
    public SysRole? Role { get; set; }
    public SysScreen? Screen { get; set; }
    public SysFeature? Feature { get; set; }
}
