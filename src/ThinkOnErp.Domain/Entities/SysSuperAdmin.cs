namespace ThinkOnErp.Domain.Entities;

public class SysSuperAdmin
{
    public Int64 Id { get; set; }
    public string NameAr { get; set; } = string.Empty;
    public string NameEn { get; set; } = string.Empty;
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
