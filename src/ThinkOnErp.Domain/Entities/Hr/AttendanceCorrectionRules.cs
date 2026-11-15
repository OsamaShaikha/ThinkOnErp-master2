using System.Globalization;
using System.Text.Json;

namespace ThinkOnErp.Domain.Entities.Hr;

public sealed class AttendanceCorrectionStaleException() : InvalidOperationException("Attendance changed since this correction was submitted. Review and submit a new request.");

public static class AttendanceCorrectionRules
{
    public static void ValidateInput(AttendanceCorrectionRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.EmployeeCode) || request.EmployeeCode.Length > 100 || string.IsNullOrWhiteSpace(request.Reason) || request.Reason.Trim().Length > 1000)
        {
            throw new ArgumentException("Employee and a correction reason up to 1000 characters are required.");
        }

        if (request.AttendanceDate != request.AttendanceDate.Date || (!request.RequestedCheckIn.HasValue && !request.RequestedCheckOut.HasValue))
        {
            throw new ArgumentException("A local work date and at least one requested punch are required.");
        }

        if (request.RequestedCheckIn?.Kind is not (null or DateTimeKind.Utc) || request.RequestedCheckOut?.Kind is not (null or DateTimeKind.Utc))
        {
            throw new ArgumentException("Requested punch timestamps must be UTC with a Z suffix.");
        }

        if (request.RequestedCheckIn.HasValue && request.RequestedCheckOut.HasValue && request.RequestedCheckOut < request.RequestedCheckIn)
        {
            throw new ArgumentException("Requested checkout cannot precede check-in; supply its actual next-day timestamp.");
        }
    }

    public static void CaptureOriginal(AttendanceCorrectionRequest request, AttendanceDay? day)
    {
        if (day != null && day.TimeZoneId == null)
        {
            throw new InvalidOperationException("Legacy attendance needs a reviewed time/shift snapshot before correction.");
        }

        DateTime? first = request.RequestedCheckIn ?? day?.FirstCheckIn; DateTime? last = request.RequestedCheckOut ?? day?.LastCheckOut;
        if (first.HasValue && last.HasValue && last < first)
        {
            throw new ArgumentException("The merged correction has checkout before check-in.");
        }

        if (first == day?.FirstCheckIn && last == day?.LastCheckOut)
        {
            throw new ArgumentException("The request must change or complete a punch.");
        }

        request.OldCheckIn = day?.FirstCheckIn; request.OldCheckOut = day?.LastCheckOut; request.OriginalDayVersion = Version(day);
    }

    public static string Version(AttendanceDay? day)
    {
        if (day == null)
        {
            return LeaveMovementKey.Hash("NO_ATTENDANCE_DAY");
        }

        static string Number(decimal value)
        {
            return value.ToString("G29", CultureInfo.InvariantCulture);
        }

        static string? Optional(decimal? value)
        {
            return value.HasValue ? Number(value.Value) : null;
        }

        return LeaveMovementKey.Hash(JsonSerializer.Serialize(new
        {
            day.Id,
            day.EmployeeCode,
            day.WorkCalendarId,
            Date = day.AttendanceDate.Ticks,
            In = day.FirstCheckIn?.Ticks,
            Out = day.LastCheckOut?.Ticks,
            day.ShiftCode,
            day.TimeZoneId,
            Start = day.ShiftStartsAtUtc?.Ticks,
            End = day.ShiftEndsAtUtc?.Ticks,
            day.ShiftBreakMinutes,
            day.ShiftSource,
            Scheduled = Number(day.ScheduledHours),
            Actual = Number(day.ActualWorkedHours),
            Overtime = Number(day.OvertimeHours),
            day.LateArrivalMinutes,
            day.EarlyLeaveMinutes,
            day.Status,
            day.HasMissingPunch,
            day.LeaveTypeCode,
            day.Notes,
            day.CheckInBranchId,
            day.CheckOutBranchId,
            day.HasGeofenceViolation,
            day.IsCheckInWithinGeofence,
            day.IsCheckOutWithinGeofence,
            InLat = Optional(day.CheckInLatitude),
            InLon = Optional(day.CheckInLongitude),
            OutLat = Optional(day.CheckOutLatitude),
            OutLon = Optional(day.CheckOutLongitude),
            InDistance = Optional(day.CheckInDistanceMeters),
            OutDistance = Optional(day.CheckOutDistanceMeters),
            day.CreationUser,
            Created = day.CreationDate.Ticks,
            day.UpdateUser,
            Updated = day.UpdateDate?.Ticks
        }));
    }
}
