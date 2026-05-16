namespace ThinkOnErp.Domain.Entities;

/// <summary>
/// Represents system/module access control at branch level
/// </summary>
public class SysBranchSystem
{
    public long RowId { get; set; }
    public long BranchId { get; set; }
    public long SystemId { get; set; }
    public bool IsAllowed { get; set; }
    public long? GrantedBy { get; set; }
    public DateTime? GrantedDate { get; set; }
    public DateTime? RevokedDate { get; set; }
    public string? Notes { get; set; }
    public string CreationUser { get; set; } = string.Empty;
    public DateTime? CreationDate { get; set; }
    public string? UpdateUser { get; set; }
    public DateTime? UpdateDate { get; set; }

    // Navigation properties
    public SysBranch? Branch { get; set; }
    public SysSystem? System { get; set; }
    public SysSuperAdmin? GrantedByAdmin { get; set; }
}
