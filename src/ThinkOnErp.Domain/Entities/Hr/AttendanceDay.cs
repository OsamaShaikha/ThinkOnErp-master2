using System;

namespace ThinkOnErp.Domain.Entities.Hr;

public sealed class AttendanceDay
{
    public long Id { get; set; }
    public string EmployeeCode { get; set; } = string.Empty;
    public DateTime AttendanceDate { get; set; }
    public string? ShiftCode { get; set; }
    public long? WorkCalendarId { get; set; }
    public DateTime? FirstCheckIn { get; set; }
    public DateTime? LastCheckOut { get; set; }
    public decimal ScheduledHours { get; set; }
    public decimal ActualWorkedHours { get; set; }
    public int LateArrivalMinutes { get; set; }
    public int EarlyLeaveMinutes { get; set; }
    public decimal OvertimeHours { get; set; }
    public bool HasMissingPunch { get; set; }
    public string Status { get; set; } = "PRESENT"; // PRESENT, ABSENT, LATE, EARLY_LEAVE, MISSING_PUNCH, ON_LEAVE, HOLIDAY, WEEKEND
    public string? LeaveTypeCode { get; set; }
    public string? Notes { get; set; }
    public string CreationUser { get; set; } = string.Empty;
    public DateTime CreationDate { get; set; } = DateTime.UtcNow;
    public string? UpdateUser { get; set; }
    public DateTime? UpdateDate { get; set; }

    public Employee? Employee { get; set; }
    public ShiftSchedule? ShiftSchedule { get; set; }
    public WorkCalendar? WorkCalendar { get; set; }

}
