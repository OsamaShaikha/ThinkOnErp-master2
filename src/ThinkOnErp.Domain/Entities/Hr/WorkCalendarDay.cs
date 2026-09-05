using System;

namespace ThinkOnErp.Domain.Entities.Hr;

public sealed class WorkCalendarDay
{
    public long Id { get; set; }
    public long WorkCalendarId { get; set; }
    public DayOfWeek DayOfWeek { get; set; }
    public bool IsWorkingDay { get; set; }
    public string? DefaultShiftCode { get; set; }
    public decimal StandardWorkingHours { get; set; } = 8.0m;

    public WorkCalendar? WorkCalendar { get; set; }
    public ShiftSchedule? DefaultShift { get; set; }
}

