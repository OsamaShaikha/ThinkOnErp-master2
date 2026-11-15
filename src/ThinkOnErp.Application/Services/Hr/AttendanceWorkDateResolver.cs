using ThinkOnErp.Domain.Entities.Hr;
using ThinkOnErp.Domain.Interfaces.Hr;

namespace ThinkOnErp.Application.Services.Hr;

public static class AttendanceWorkDateResolver
{
    public static async Task<TimedShiftDto> ResolveAsync(string employeeCode, DateTimeOffset instant, DateOnly localDate, string type,
        IShiftTimingService timing, IAttendanceCorrectionRepository attendance, CancellationToken ct = default)
    {
        if (type == "OUT")
        {
            List<AttendanceDay> open = [];
            foreach (DateOnly date in localDate == DateOnly.MinValue ? new[] { localDate } : new[] { localDate.AddDays(-1), localDate })
            {
                AttendanceDay? day = await attendance.GetAttendanceDayAsync(employeeCode, date.ToDateTime(TimeOnly.MinValue), ct);
                if (day?.FirstCheckIn != null && day.LastCheckOut == null)
                {
                    open.Add(day);
                }
            }
            if (open.Count > 1)
            {
                throw new InvalidOperationException("Multiple open attendance days require review before check-out.");
            }

            if (open.Count == 1)
            {
                return await timing.PreviewConfiguredAsync(employeeCode, DateOnly.FromDateTime(open[0].AttendanceDate), ct);
            }
        }
        return await timing.MatchConfiguredPunchAsync(employeeCode, instant, type, ct)
            ?? await timing.PreviewConfiguredAsync(employeeCode, localDate, ct);
    }
}
