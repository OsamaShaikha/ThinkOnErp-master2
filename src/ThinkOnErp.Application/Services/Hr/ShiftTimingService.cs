using ThinkOnErp.Application.DTOs.Hr;
using ThinkOnErp.Domain.Entities.Hr;
using ThinkOnErp.Domain.Interfaces.Hr;

namespace ThinkOnErp.Application.Services.Hr;

public record TimedShiftDto(EffectiveShiftDto Shift, string TimeZoneId, DateTimeOffset? StartUtc, DateTimeOffset? EndUtc, decimal? ElapsedMinutes);

public interface IShiftTimingService
{
    Task<TimedShiftDto> PreviewConfiguredAsync(string employeeCode, DateOnly workDate, CancellationToken ct = default);
    Task<TimedShiftDto?> MatchConfiguredPunchAsync(string employeeCode, DateTimeOffset instant, string punchType, CancellationToken ct = default);
    Task<TimedShiftDto> PreviewAsync(string employeeCode, DateOnly workDate, string timeZoneId, CancellationToken ct = default);
    Task<TimedShiftDto?> MatchPunchAsync(string employeeCode, DateTimeOffset instant, string punchType, string timeZoneId, CancellationToken ct = default);
}

public sealed class ShiftTimingService(IEffectiveShiftService shifts, IAttendanceTimeSettingsRepository? settings = null) : IShiftTimingService
{
    public async Task<TimedShiftDto> PreviewConfiguredAsync(string employeeCode, DateOnly workDate, CancellationToken ct = default)
    {
        var setting = await (settings ?? throw new InvalidOperationException("Time settings repository is required.")).GetForEmployeeAsync(employeeCode, ct);
        return await PreviewAsync(employeeCode, workDate, setting.TimeZoneId, ct);
    }

    public async Task<TimedShiftDto?> MatchConfiguredPunchAsync(string employeeCode, DateTimeOffset instant, string punchType, CancellationToken ct = default)
    {
        var setting = await (settings ?? throw new InvalidOperationException("Time settings repository is required.")).GetForEmployeeAsync(employeeCode, ct);
        return await MatchPunchAsync(employeeCode, instant, punchType, setting.TimeZoneId, ct);
    }

    public async Task<TimedShiftDto> PreviewAsync(string employeeCode, DateOnly workDate, string timeZoneId, CancellationToken ct = default)
    {
        var zone = ShiftTimeRules.Zone(timeZoneId);
        var shift = await shifts.ResolveAsync(employeeCode, workDate, ct);
        if (shift.Status != "WORKING") return new(shift, zone.Id, null, null, null);
        if (shift.StartsAtLocal == null || shift.EndsAtLocal == null) throw new InvalidOperationException("Working shift has no boundaries.");
        var window = ShiftTimeRules.ToUtc(DateTime.SpecifyKind(shift.StartsAtLocal.Value, DateTimeKind.Unspecified), DateTime.SpecifyKind(shift.EndsAtLocal.Value, DateTimeKind.Unspecified), zone);
        return new(shift, zone.Id, window.StartUtc, window.EndUtc, window.ElapsedMinutes);
    }

    public async Task<TimedShiftDto?> MatchPunchAsync(string employeeCode, DateTimeOffset instant, string punchType, string timeZoneId, CancellationToken ct = default)
    {
        if (punchType is not ("IN" or "OUT")) throw new ArgumentException("Punch type must be IN or OUT.");
        var zone = ShiftTimeRules.Zone(timeZoneId);
        var date = DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(instant, zone).DateTime);
        TimedShiftDto? matched = null;
        // Shifts are shorter than 24 local hours and anchored to their start date.
        foreach (var workDate in date == DateOnly.MinValue ? new[] { date } : new[] { date.AddDays(-1), date })
        {
            var candidate = await PreviewAsync(employeeCode, workDate, timeZoneId, ct);
            if (candidate.StartUtc == null || candidate.EndUtc == null) continue;
            var window = new ShiftUtcWindow(candidate.StartUtc.Value, candidate.EndUtc.Value, candidate.ElapsedMinutes!.Value);
            if (!ShiftTimeRules.Contains(window, instant, punchType)) continue;
            if (matched != null) throw new InvalidOperationException("Punch matches overlapping shifts on two work dates. Review the daily roster.");
            matched = candidate;
        }
        return matched;
    }
}
