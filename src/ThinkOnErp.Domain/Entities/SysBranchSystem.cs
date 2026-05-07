namespace ThinkOnErp.Domain.Entities;

/// <summary>
/// Represents system access control per branch (allow/block systems for branches).
/// Maps to the SYS_BRANCH_SYSTEMS table in Oracle database.
/// </summary>
public class SysBranchSystem
{
    public Int64 Id { get; set; }
    public Int64 BranchId { get; set; }
    public Int64 SystemId { get; set; }
    public bool IsAllowed { get; set; }
    public Int64? GrantedBy { get; set; }
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
    public SysSuperAdmin? Granter { get; set; }
}
