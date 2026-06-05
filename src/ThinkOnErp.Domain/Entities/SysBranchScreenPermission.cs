namespace ThinkOnErp.Domain.Entities;

/// <summary>
/// Represents screen-level permissions granted to a branch (View/Insert/Update/Delete).
/// Maps to the SYS_BRANCH_SCREEN_PERMISSIONS table in Oracle database.
/// </summary>
public class SysBranchScreenPermission
{
    public Int64 Id { get; set; }
    public Int64 BranchId { get; set; }
    public Int64 ScreenId { get; set; }
    public bool CanView { get; set; }
    public bool CanInsert { get; set; }
    public bool CanUpdate { get; set; }
    public bool CanDelete { get; set; }
    public Int64? GrantedBy { get; set; }
    public DateTime? GrantedDate { get; set; }
    public string? Notes { get; set; }
    public string CreationUser { get; set; } = string.Empty;
    public DateTime? CreationDate { get; set; }
    public string? UpdateUser { get; set; }
    public DateTime? UpdateDate { get; set; }

    // Navigation properties
    public SysBranch? Branch { get; set; }
    public SysScreen? Screen { get; set; }
    public SysSuperAdmin? Granter { get; set; }
}
