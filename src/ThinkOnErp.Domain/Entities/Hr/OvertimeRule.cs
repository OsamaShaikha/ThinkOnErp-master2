using System;

namespace ThinkOnErp.Domain.Entities.Hr;

public sealed class OvertimeRule
{
    public long Id { get; set; }
    public long CompanyId { get; set; }
    public string Code { get; set; } = string.Empty; // NORMAL, WEEKEND, HOLIDAY, NIGHT
    public string NameEn { get; set; } = string.Empty;
    public string NameAr { get; set; } = string.Empty;
    public string DayType { get; set; } = "NORMAL"; // NORMAL, WEEKEND, HOLIDAY, NIGHT
    public decimal Multiplier { get; set; } = 1.25m;
    public int MinimumMinutes { get; set; } = 30;
    public int MaximumMinutes { get; set; } = 480;
    public string HourlyDivisorFormula { get; set; } = "FIXED_240"; // FIXED_240 (30*8), ACTUAL_MONTHLY_HOURS, WORKING_DAYS_HOURS
    public bool RequiresApproval { get; set; } = true;
    public DateTime EffectiveFrom { get; set; }
    public DateTime? EffectiveTo { get; set; }
    public bool IsActive { get; set; } = true;
    public string CreationUser { get; set; } = string.Empty;
    public DateTime CreationDate { get; set; } = DateTime.UtcNow;
    public string? UpdateUser { get; set; }
    public DateTime? UpdateDate { get; set; }
}
