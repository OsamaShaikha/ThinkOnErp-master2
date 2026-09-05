namespace ThinkOnErp.Domain.Entities;

public class SysRole
{
    public Int64 Id { get; set; }
    public string RoleNameLocal { get; set; } = string.Empty;
    public string RoleNameEn { get; set; } = string.Empty;
    public string? Note { get; set; }
    public bool IsActive { get; set; }
    public string CreationUser { get; set; } = string.Empty;
    public DateTime? CreationDate { get; set; }
    public string? UpdateUser { get; set; }
    public DateTime? UpdateDate { get; set; }
}
