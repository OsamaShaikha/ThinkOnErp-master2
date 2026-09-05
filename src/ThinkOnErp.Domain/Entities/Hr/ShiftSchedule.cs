using System;
using System.Collections.Generic;

namespace ThinkOnErp.Domain.Entities.Hr;

public sealed class ShiftSchedule
{
    public string ShiftCode { get; set; } = string.Empty;
    public string NameAr { get; set; } = string.Empty;
    public string NameEn { get; set; } = string.Empty;
    public TimeSpan StartTime { get; set; }
    public TimeSpan EndTime { get; set; }
    public int BreakMinutes { get; set; } = 60;
    public string WorkingDaysJson { get; set; } = "[\"Sunday\",\"Monday\",\"Tuesday\",\"Wednesday\",\"Thursday\"]";
    public bool IsActive { get; set; } = true;
    public string CreationUser { get; set; } = string.Empty;
    public DateTime CreationDate { get; set; } = DateTime.UtcNow;
    public string? UpdateUser { get; set; }
    public DateTime? UpdateDate { get; set; }

    public List<EmployeeShiftAssignment> ShiftAssignments { get; set; } = new();
}
