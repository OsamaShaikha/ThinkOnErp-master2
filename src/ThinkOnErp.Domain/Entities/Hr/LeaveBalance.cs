using System;

namespace ThinkOnErp.Domain.Entities.Hr;

public sealed class LeaveBalance
{
    public long Id { get; set; }
    public string EmployeeCode { get; set; } = string.Empty;
    public string LeaveTypeCode { get; set; } = string.Empty;
    public int Year { get; set; }
    public decimal AccruedDays { get; set; } = 0m;
    public decimal UsedDays { get; set; } = 0m;
    public decimal CarriedForwardDays { get; set; } = 0m;
    public decimal RemainingDays => (AccruedDays + CarriedForwardDays) - UsedDays;
    public string CreationUser { get; set; } = string.Empty;
    public DateTime CreationDate { get; set; } = DateTime.UtcNow;
    public string? UpdateUser { get; set; }
    public DateTime? UpdateDate { get; set; }

    public Employee? Employee { get; set; }
    public LeaveType? LeaveType { get; set; }
}
