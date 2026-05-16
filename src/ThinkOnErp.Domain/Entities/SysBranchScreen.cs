namespace ThinkOnErp.Domain.Entities;

/// <summary>
/// Represents screen access control at branch level
/// </summary>
public class SysBranchScreen
{
    public long RowId { get; set; }
    public long BranchId { get; set; }
    public long ScreenId { get; set; }
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
    public SysScreen? Screen { get; set; }
    public SysSuperAdmin? GrantedByAdmin { get; set; }
}
