namespace ThinkOnErp.Application.DTOs.Pos;

/// <summary>
/// بيانات المستخدم المهيأة لنقاط البيع لاختيار مدخل الطلب أو الكاشير (POS User Lookup)
/// </summary>
public sealed class PosUserDto
{
    public long Id { get; set; }
    public string UserName { get; set; } = string.Empty;
    public string FullNameLocal { get; set; } = string.Empty;
    public string FullNameEn { get; set; } = string.Empty;
    public string DisplayName => !string.IsNullOrWhiteSpace(FullNameLocal) ? FullNameLocal : FullNameEn;
    public string? Phone { get; set; }
    public string? Email { get; set; }
    public long? BranchId { get; set; }
    public long? RoleId { get; set; }
    public bool IsActive { get; set; } = true;
}
