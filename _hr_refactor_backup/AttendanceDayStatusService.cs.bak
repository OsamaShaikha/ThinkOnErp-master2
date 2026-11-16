using ThinkOnErp.Domain.Entities.Hr;
using ThinkOnErp.Domain.Interfaces.Hr;

namespace ThinkOnErp.Application.Services.Hr;

public interface IAttendanceDayStatusService
{
    Task<AttendanceDayClassificationDto> PreviewAsync(string employeeCode, DateOnly workDate, CancellationToken ct = default);
    Task<AttendanceDayClassificationDto> SaveAsync(string employeeCode, DateOnly workDate, string user, CancellationToken ct = default);
}

public sealed class AttendanceDayStatusService(IShiftTimingService timing, IAttendanceDayClassifier classifier,
    IAttendanceCorrectionRepository attendance, IAttendanceTimeSettingsRepository settings, IPolicyRepository policies, IHrUnitOfWork unitOfWork) : IAttendanceDayStatusService
{
    public async Task<AttendanceDayClassificationDto> PreviewAsync(string employeeCode, DateOnly workDate, CancellationToken ct = default)
    {
        var setting = await settings.GetForEmployeeAsync(employeeCode, ct);
        var schedule = await timing.PreviewConfiguredAsync(employeeCode, workDate, ct);
        var date = workDate.ToDateTime(TimeOnly.MinValue);
        var old = await attendance.GetAttendanceDayAsync(employeeCode, date, ct);
        if (old != null && old.TimeZoneId == null) throw new InvalidOperationException("Legacy attendance needs a reviewed time/shift snapshot before classification.");
        var policy = await policies.GetActiveAttendancePolicyAsync(setting.CompanyId, date, ct) ?? new AttendancePolicy();
        var calculated = ScheduledAttendanceCalculator.Calculate(employeeCode, workDate, old?.FirstCheckIn, old?.LastCheckOut, schedule, policy);
        calculated.Id = old?.Id ?? 0;
        return await classifier.ClassifyAsync(calculated, schedule, ct);
    }

    public Task<AttendanceDayClassificationDto> SaveAsync(string employeeCode, DateOnly workDate, string user, CancellationToken ct = default)
        => unitOfWork.ExecuteForEmployeeDatesAsync(employeeCode, workDate.ToDateTime(TimeOnly.MinValue), workDate.ToDateTime(TimeOnly.MinValue), async () =>
        {
            var setting = await settings.GetForEmployeeAsync(employeeCode, ct);
            var today = DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(DateTimeOffset.UtcNow, ShiftTimeRules.Zone(setting.TimeZoneId)).DateTime);
            if (workDate > today) throw new InvalidOperationException("Future attendance dates cannot be finalized.");
            await attendance.EnsureEmployeeWorkDateAsync(employeeCode, workDate.ToDateTime(TimeOnly.MinValue), ct);
            var result = await PreviewAsync(employeeCode, workDate, ct);
            if (result.Day.Status == "PENDING") throw new InvalidOperationException("The scheduled work period has not ended; absence cannot be finalized yet.");
            var old = await attendance.GetAttendanceDayAsync(employeeCode, workDate.ToDateTime(TimeOnly.MinValue), ct);
            if (old == null)
            {
                result.Day.CreationUser = user; result.Day.CreationDate = DateTime.UtcNow;
                await attendance.AddAttendanceDayAsync(result.Day, ct);
            }
            else
            {
                ScheduledAttendanceCalculator.CopyMetrics(result.Day, old);
                old.UpdateUser = user; old.UpdateDate = DateTime.UtcNow;
                await attendance.UpdateAttendanceDayAsync(old, ct);
                result = result with { Day = old };
            }
            await attendance.SaveChangesAsync(ct); return result;
        }, ct);
}
