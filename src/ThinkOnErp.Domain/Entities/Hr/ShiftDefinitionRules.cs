using System.Text;
using System.Text.Json;
using System.Security.Cryptography;

namespace ThinkOnErp.Domain.Entities.Hr;

public sealed class ShiftVersionConflictException() : InvalidOperationException("Shift version changed. Read it again before editing.");

public static class ShiftDefinitionRules
{
    public static int[] WorkingDays(ShiftSchedule shift)
    {
        int[] days;
        try { days = JsonSerializer.Deserialize<int[]>(shift.WorkingDaysJson) ?? throw new JsonException(); }
        catch (Exception ex) when (ex is JsonException or ArgumentNullException)
        { throw new InvalidOperationException("Shift working days must be a JSON array of day numbers 0..6.", ex); }
        if (days.Any(d => d < 0 || d > 6) || days.Distinct().Count() != days.Length)
            throw new InvalidOperationException("Shift working days must contain distinct day numbers 0..6.");
        return days;
    }

    public static decimal GrossMinutes(ShiftSchedule shift)
    {
        if (shift.StartTime < TimeSpan.Zero || shift.StartTime >= TimeSpan.FromDays(1) || shift.EndTime < TimeSpan.Zero ||
            shift.EndTime >= TimeSpan.FromDays(1) || shift.StartTime == shift.EndTime)
            throw new InvalidOperationException("Shift start/end times must be distinct clock times within one day.");
        var duration = shift.EndTime - shift.StartTime;
        if (duration < TimeSpan.Zero) duration += TimeSpan.FromDays(1);
        var minutes = (decimal)duration.Ticks / TimeSpan.TicksPerMinute;
        if (shift.BreakMinutes < 0 || shift.BreakMinutes >= minutes)
            throw new InvalidOperationException("Shift break must be nonnegative and shorter than the shift.");
        return minutes;
    }

    public static string Version(ShiftSchedule shift) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(new
    {
        shift.ShiftCode, shift.NameLocal, shift.NameEn, Start = shift.StartTime.Ticks, End = shift.EndTime.Ticks,
        shift.BreakMinutes, shift.WorkingDaysJson, shift.IsActive, Created = shift.CreationDate.Ticks, Updated = shift.UpdateDate?.Ticks
    }))));

    public static bool SameCalculation(ShiftSchedule a, ShiftSchedule b)
    {
        if (a.StartTime != b.StartTime || a.EndTime != b.EndTime || a.BreakMinutes != b.BreakMinutes || a.IsActive != b.IsActive) return false;
        try { return WorkingDays(a).OrderBy(d => d).SequenceEqual(WorkingDays(b).OrderBy(d => d)); }
        catch (InvalidOperationException) { return a.WorkingDaysJson == b.WorkingDaysJson; }
    }
}
