namespace ThinkOnErp.Domain.Entities;

/// <summary>
/// Represents a Super Admin account with full platform access
/// </summary>
public class SysSuperAdmin
{
    public long RowId { get; set; }
    public string RowDesc { get; set; } = string.Empty;
    public string RowDescE { get; set; } = string.Empty;
    public string UserName { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public string? Email { get; set; }
    public string? Phone { get; set; }
    public string? TwoFaSecret { get; set; }
    public bool TwoFaEnabled { get; set; }
    public bool IsActive { get; set; }
    public DateTime? LastLoginDate { get; set; }
    public string CreationUser { get; set; } = string.Empty;
    public DateTime? CreationDate { get; set; }
    public string? UpdateUser { get; set; }
    public DateTime? UpdateDate { get; set; }
}
