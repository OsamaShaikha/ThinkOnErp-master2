using System;

namespace ThinkOnErp.Domain.Entities.Hr;

public sealed class AttendancePolicy
{
    public long Id { get; set; }
    public long CompanyId { get; set; }
    public string Code { get; set; } = string.Empty;
    public string NameEn { get; set; } = string.Empty;
    public string NameAr { get; set; } = string.Empty;
    public int GracePeriodMinutes { get; set; } = 15;
    public int EarlyLeaveToleranceMinutes { get; set; } = 0;
    public int MinimumMinutesForOvertime { get; set; } = 30;
    public bool AutoDeductLateArrival { get; set; } = true;
    public string MissingPunchHandling { get; set; } = "FLAG_FOR_CORRECTION"; // FLAG_FOR_CORRECTION, AUTO_ESTIMATE, DEDUCT_FULL_DAY
    public bool IsDefault { get; set; } = true;
    public DateTime EffectiveFrom { get; set; }
    public DateTime? EffectiveTo { get; set; }
    public bool IsActive { get; set; } = true;
    public string CreationUser { get; set; } = string.Empty;
    public DateTime CreationDate { get; set; } = DateTime.UtcNow;
    public string? UpdateUser { get; set; }
    public DateTime? UpdateDate { get; set; }
}
