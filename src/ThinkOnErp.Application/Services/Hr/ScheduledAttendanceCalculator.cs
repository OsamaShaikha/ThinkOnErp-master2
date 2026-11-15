using ThinkOnErp.Domain.Entities.Hr;

namespace ThinkOnErp.Application.Services.Hr;

public static class ScheduledAttendanceCalculator
{
    // Stored punch timestamps are UTC; Oracle DATE/TIMESTAMP materializes them
    // without a Kind. Never interpret them using the execution host's zone.
    public static AttendanceDay Calculate(string employeeCode, DateOnly workDate, DateTime? checkIn, DateTime? checkOut,
        TimedShiftDto schedule, AttendancePolicy policy, int excusedLateMinutes = 0, int excusedEarlyMinutes = 0, decimal? multiSessionNetMinutes = null)
    {
        if (schedule.Shift.EmployeeCode != employeeCode || schedule.Shift.WorkDate != workDate) throw new InvalidOperationException("Attendance and scheduled employee/date must match.");
        if (checkIn?.Kind == DateTimeKind.Local || checkOut?.Kind == DateTimeKind.Local) throw new ArgumentException("Punch timestamps must be UTC.");
        var first = checkIn.HasValue ? DateTime.SpecifyKind(checkIn.Value, DateTimeKind.Utc) : (DateTime?)null;
        var last = checkOut.HasValue ? DateTime.SpecifyKind(checkOut.Value, DateTimeKind.Utc) : (DateTime?)null;
        if (first.HasValue && last.HasValue && last < first) throw new ArgumentException("Check-out cannot precede check-in; supply the actual next-day UTC timestamp.");
        if (policy.GracePeriodMinutes < 0 || policy.EarlyLeaveToleranceMinutes < 0 || policy.MinMinutesForOvertime < 0) throw new InvalidOperationException("Attendance tolerances cannot be negative.");
        var working = schedule.Shift.Status == "WORKING";
        if (working && (schedule.StartUtc == null || schedule.EndUtc == null || schedule.ElapsedMinutes == null)) throw new InvalidOperationException("Working shift must have UTC boundaries.");
        var scheduledMinutes = working ? Math.Max(0m, schedule.ElapsedMinutes!.Value - schedule.Shift.BreakMinutes) : 0m;
        var grossMinutes = first.HasValue && last.HasValue ? (decimal)(last.Value - first.Value).Ticks / TimeSpan.TicksPerMinute : 0m;
        var netMinutes = multiSessionNetMinutes.HasValue
            ? multiSessionNetMinutes.Value
            : (first.HasValue && last.HasValue ? Math.Max(0m, grossMinutes - (working ? schedule.Shift.BreakMinutes : 0)) : 0m);
        var missing = first.HasValue != last.HasValue;
        var day = new AttendanceDay { EmployeeCode = employeeCode, AttendanceDate = workDate.ToDateTime(TimeOnly.MinValue), ShiftCode = schedule.Shift.ShiftCode,
            TimeZoneId = schedule.TimeZoneId, ShiftStartsAtUtc = schedule.StartUtc?.UtcDateTime, ShiftEndsAtUtc = schedule.EndUtc?.UtcDateTime,
            ShiftBreakMinutes = schedule.Shift.BreakMinutes, ShiftSource = schedule.Shift.Source,
            ScheduledHours = Math.Round(scheduledMinutes / 60m, 2, MidpointRounding.AwayFromZero), FirstCheckIn = first, LastCheckOut = last,
            ActualWorkedHours = Math.Round(netMinutes / 60m, 2, MidpointRounding.AwayFromZero), HasMissingPunch = missing,
            Status = missing ? "INCOMPLETE" : first.HasValue ? "PRESENT" : working ? "ABSENT" : schedule.Shift.Status };
        if (!working) return day; // A recorded rest-day punch is evidence, not scheduled work/overtime pay.
        if (first.HasValue)
        {
            var rawLate = Math.Max(0, (int)(first.Value - schedule.StartUtc!.Value.UtcDateTime).TotalMinutes);
            var late = Math.Max(0, rawLate - Math.Max(0, excusedLateMinutes));
            day.LateArrivalMinutes = late > policy.GracePeriodMinutes ? policy.AutoDeductLateArrival ? late - policy.GracePeriodMinutes : late : 0;
        }
        if (last.HasValue)
        {
            var rawEarly = Math.Max(0, (int)(schedule.EndUtc!.Value.UtcDateTime - last.Value).TotalMinutes);
            var early = Math.Max(0, rawEarly - Math.Max(0, excusedEarlyMinutes));
            day.EarlyLeaveMinutes = early > policy.EarlyLeaveToleranceMinutes ? early : 0;
        }
        var extra = Math.Max(0m, netMinutes - scheduledMinutes);
        if (extra >= policy.MinMinutesForOvertime) day.OvertimeHours = Math.Round(extra / 60m, 2, MidpointRounding.AwayFromZero);
        return day;
    }

    public static void CopyMetrics(AttendanceDay source, AttendanceDay target)
    {
        if (target.TimeZoneId != null && (target.TimeZoneId != source.TimeZoneId || target.ShiftCode != source.ShiftCode ||
            target.ShiftStartsAtUtc != source.ShiftStartsAtUtc || target.ShiftEndsAtUtc != source.ShiftEndsAtUtc ||
            target.ShiftBreakMinutes != source.ShiftBreakMinutes || target.ShiftSource != source.ShiftSource || target.ScheduledHours != source.ScheduledHours))
            throw new InvalidOperationException("Attendance shift/time snapshot differs from the current roster. Review it before recalculation.");
        target.TimeZoneId = source.TimeZoneId; target.ShiftStartsAtUtc = source.ShiftStartsAtUtc; target.ShiftEndsAtUtc = source.ShiftEndsAtUtc;
        target.ShiftBreakMinutes = source.ShiftBreakMinutes; target.ShiftSource = source.ShiftSource;
        target.ShiftCode = source.ShiftCode; target.ScheduledHours = source.ScheduledHours;
        target.ActualWorkedHours = source.ActualWorkedHours; target.LateArrivalMinutes = source.LateArrivalMinutes;
        target.EarlyLeaveMinutes = source.EarlyLeaveMinutes; target.OvertimeHours = source.OvertimeHours;
        target.LeaveTypeCode = source.LeaveTypeCode;
        target.HasMissingPunch = source.HasMissingPunch; target.Status = source.Status;
    }
}
