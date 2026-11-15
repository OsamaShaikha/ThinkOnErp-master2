using System.Text.Json;

namespace ThinkOnErp.Domain.Entities.Hr;

public sealed class ShiftDayVersionConflictException() : InvalidOperationException("Daily roster version changed. Read it again before editing.");

public static class ShiftDayRules
{
    public static void Validate(EmployeeShiftDay day)
    {
        if (day.WorkDate != day.WorkDate.Date || day.Source != "MANUAL" ||
            day.IsRestDay && day.ShiftCode != null || !day.IsRestDay && (string.IsNullOrWhiteSpace(day.ShiftCode) || day.ShiftCode.Length > 100))
            throw new ArgumentException("A manual daily entry needs a local calendar date and either explicit rest or one shift.");
    }

    public static string Version(EmployeeShiftDay day) => LeaveMovementKey.Hash(JsonSerializer.Serialize(new
    {
        day.Id, day.EmployeeCode, Date = day.WorkDate.Ticks, day.ShiftCode, day.IsRestDay,
        day.IsActive, day.Source, Created = day.CreationDate.Ticks, Updated = day.UpdateDate?.Ticks
    }));
}
