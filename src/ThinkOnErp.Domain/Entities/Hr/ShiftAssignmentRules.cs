using System.Text.Json;

namespace ThinkOnErp.Domain.Entities.Hr;

public readonly record struct HrDateRange(DateTime Start, DateTime End);

public sealed class ShiftAssignmentVersionConflictException(string? message = null)
    : InvalidOperationException(message ?? "Shift assignment version changed. Read it again before editing.");

public static class ShiftAssignmentRules
{
    public static string Kind(EmployeeShiftAssignment assignment) => assignment.AssignmentKind switch
    {
        "BASE" => "BASE", "TEMPORARY" when assignment.EffectiveTo.HasValue => "TEMPORARY",
        null => assignment.EffectiveTo.HasValue ? "TEMPORARY" : "BASE",
        _ => throw new InvalidOperationException("Invalid shift assignment kind or temporary end date.")
    };

    public static HrDateRange Range(EmployeeShiftAssignment assignment) => new(assignment.EffectiveFrom.Date, assignment.EffectiveTo?.Date ?? DateTime.MaxValue.Date);

    public static bool Overlap(HrDateRange a, HrDateRange b) => a.Start <= b.End && b.Start <= a.End;

    public static IReadOnlyList<HrDateRange> Affected(EmployeeShiftAssignment old, EmployeeShiftAssignment proposed)
    {
        var a = Range(old); var b = Range(proposed);
        if (old.ShiftCode != proposed.ShiftCode || old.IsActive != proposed.IsActive || Kind(old) != Kind(proposed))
            return new[] { a, b };
        if (!Overlap(a, b)) return new[] { a, b };
        var start = a.Start > b.Start ? a.Start : b.Start;
        var end = a.End < b.End ? a.End : b.End;
        var result = new List<HrDateRange>();
        if (a.Start < start) result.Add(new(a.Start, start.AddDays(-1)));
        if (b.Start < start) result.Add(new(b.Start, start.AddDays(-1)));
        if (a.End > end) result.Add(new(end.AddDays(1), a.End));
        if (b.End > end) result.Add(new(end.AddDays(1), b.End));
        return result;
    }

    public static bool WorkingOverlap(HrDateRange a, IReadOnlyCollection<int> aDays, HrDateRange b, IReadOnlyCollection<int> bDays)
    {
        if (!Overlap(a, b)) return false;
        var start = a.Start > b.Start ? a.Start : b.Start;
        var end = a.End < b.End ? a.End : b.End;
        var lastOffset = Math.Min(6, (end - start).Days);
        for (var i = 0; i <= lastOffset; i++)
        {
            var day = ((int)start.DayOfWeek + i) % 7;
            if (aDays.Contains(day) && bDays.Contains(day)) return true;
        }
        return false;
    }

    public static string Version(EmployeeShiftAssignment assignment) => LeaveMovementKey.Hash(JsonSerializer.Serialize(new
    {
        assignment.Id, assignment.EmployeeCode, assignment.ShiftCode, assignment.AssignmentKind,
        From = assignment.EffectiveFrom.Ticks, To = assignment.EffectiveTo?.Ticks, assignment.IsActive,
        Created = assignment.CreationDate.Ticks, Updated = assignment.UpdateDate?.Ticks
    }));

    public static string ParseVersion(string? value)
    {
        var version = value?.Trim().Trim('"');
        if (version == null || version.Length != 64 || version.Any(c => !Uri.IsHexDigit(c))) throw new ArgumentException("A specific version from GET is required.");
        return version.ToUpperInvariant();
    }
}
