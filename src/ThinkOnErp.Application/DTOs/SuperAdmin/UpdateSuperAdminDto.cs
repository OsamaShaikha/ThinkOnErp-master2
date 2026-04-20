namespace ThinkOnErp.Application.DTOs.SuperAdmin;

/// <summary>
/// Data transfer object for updating an existing super admin
/// </summary>
public class UpdateSuperAdminDto
{
    public string NameAr { get; set; } = string.Empty;
    public string NameEn { get; set; } = string.Empty;
    public string? Email { get; set; }
    public string? Phone { get; set; }
}
