using System;

namespace ThinkOnErp.Domain.Entities.Hr;

public sealed class EmployeeShiftAssignment
{
    public long Id { get; set; }
    public string EmployeeCode { get; set; } = string.Empty;
    public string ShiftCode { get; set; } = string.Empty;
    public DateTime EffectiveFrom { get; set; }
    public DateTime? EffectiveTo { get; set; }
    public bool IsActive { get; set; } = true;
    public string CreationUser { get; set; } = string.Empty;
    public DateTime CreationDate { get; set; } = DateTime.UtcNow;
    public string? UpdateUser { get; set; }
    public DateTime? UpdateDate { get; set; }

    public Employee? Employee { get; set; }
    public ShiftSchedule? ShiftSchedule { get; set; }
}
