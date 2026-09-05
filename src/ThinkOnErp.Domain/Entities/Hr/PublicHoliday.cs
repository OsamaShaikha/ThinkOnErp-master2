using System;

namespace ThinkOnErp.Domain.Entities.Hr;

public sealed class PublicHoliday
{
    public long Id { get; set; }
    public long CompanyId { get; set; }
    public long? BranchId { get; set; }
    public string NameEn { get; set; } = string.Empty;
    public string NameAr { get; set; } = string.Empty;
    public DateTime HolidayDate { get; set; }
    public bool IsPaid { get; set; } = true;
    public bool IsRecurring { get; set; }
    public string? Description { get; set; }
    public bool IsActive { get; set; } = true;
    public string CreationUser { get; set; } = string.Empty;
    public DateTime CreationDate { get; set; } = DateTime.UtcNow;
    public string? UpdateUser { get; set; }
    public DateTime? UpdateDate { get; set; }
}
