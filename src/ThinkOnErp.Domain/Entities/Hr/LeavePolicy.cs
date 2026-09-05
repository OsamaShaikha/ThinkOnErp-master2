using System;

namespace ThinkOnErp.Domain.Entities.Hr;

public sealed class LeavePolicy
{
    public long Id { get; set; }
    public string LeaveTypeCode { get; set; } = string.Empty;
    public string PolicyName { get; set; } = string.Empty;
    public string ApplicableTo { get; set; } = "ALL"; // ALL, FULL_TIME, PROBATION, CONFIRMED
    public string AccrualMethod { get; set; } = "SERVICE_TIERED"; // ANNUAL_LUMP_SUM, MONTHLY_ACCRUAL, SERVICE_TIERED
    public decimal AccrualRate { get; set; } = 1.1667m; // e.g. 14 / 12 per month
    public int MinServiceMonths { get; set; } = 0;
    public int Tier1YearsThreshold { get; set; } = 5;
    public decimal Tier1Days { get; set; } = 14m;
    public decimal Tier2Days { get; set; } = 21m;
    public bool IsActive { get; set; } = true;
    public string CreationUser { get; set; } = string.Empty;
    public DateTime CreationDate { get; set; } = DateTime.UtcNow;
    public string? UpdateUser { get; set; }
    public DateTime? UpdateDate { get; set; }

    public LeaveType? LeaveType { get; set; }
}
