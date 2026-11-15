using ThinkOnErp.Domain.Entities.Hr;
using ThinkOnErp.Domain.Interfaces.Hr;

namespace ThinkOnErp.Application.Services.Hr;

public record AttendanceDayClassificationDto(AttendanceDay Day, bool IsHoliday, bool HasApprovedLeave, bool HasOnlineEvidence, bool NeedsReview, string? ReviewReason);

public interface IAttendanceDayClassifier
{
    Task<AttendanceDayClassificationDto> ClassifyAsync(AttendanceDay day, TimedShiftDto schedule, CancellationToken ct = default);
}

public sealed class AttendanceDayClassifier(IAttendanceDayContextRepository repository) : IAttendanceDayClassifier
{
    public async Task<AttendanceDayClassificationDto> ClassifyAsync(AttendanceDay day, TimedShiftDto schedule, CancellationToken ct = default)
    {
        var context = await repository.GetAsync(day.EmployeeCode, day.AttendanceDate, schedule.StartUtc?.UtcDateTime, schedule.EndUtc?.UtcDateTime, ct);
        var decision = AttendanceDayStatusRules.Decide(schedule.Shift.Status, day.FirstCheckIn.HasValue, day.LastCheckOut.HasValue,
            schedule.EndUtc, DateTimeOffset.UtcNow, context);
        day.Status = decision.Status; day.LeaveTypeCode = decision.LeaveTypeCode;
        // Keep review flags separate from attendance evidence and monetary deductions.
        return new(day, context.IsHoliday, context.ApprovedLeaves.Count > 0, context.HasOnlineEvidence, decision.NeedsReview, decision.ReviewReason);
    }
}
