using ThinkOnErp.Application.DTOs.Hr;
using ThinkOnErp.Domain.Entities.Hr;
using ThinkOnErp.Domain.Interfaces.Hr;

namespace ThinkOnErp.Application.Services.Hr;

public interface IEffectiveShiftService
{
    Task<EffectiveShiftDto> ResolveAsync(string employeeCode, DateOnly localWorkDate, CancellationToken cancellationToken = default);
}

public sealed class EffectiveShiftService(IEmployeeRepository employees, IShiftResolutionRepository shifts) : IEffectiveShiftService
{
    public async Task<EffectiveShiftDto> ResolveAsync(string employeeCode, DateOnly localWorkDate, CancellationToken cancellationToken = default)
    {
        if (await employees.GetEmployeeByCodeAsync(employeeCode, cancellationToken) == null)
            throw new KeyNotFoundException("Employee not found in the current HR scope.");
        var date = localWorkDate.ToDateTime(TimeOnly.MinValue);
        var days = (await shifts.GetDailyShiftsAsync(employeeCode, localWorkDate, cancellationToken))
            .Where(d => d.EmployeeCode == employeeCode && d.IsActive && d.WorkDate.Date == date).ToArray();
        if (days.Length > 1) throw new InvalidOperationException("Conflicting daily shifts for this employee/date.");
        string? code;
        string source;
        long? assignmentId = null;
        long? dailyId = null;
        ShiftSchedule? selectedShift = null;
        if (days.Length == 1)
        {
            var day = days[0];
            if (day.Source is not ("MANUAL" or "GENERATED")) throw new InvalidOperationException("Invalid daily shift source.");
            source = "DAILY_" + day.Source;
            dailyId = day.Id;
            if (day.IsRestDay)
            {
                if (day.ShiftCode != null) throw new InvalidOperationException("A daily rest entry cannot also assign a shift.");
                return Empty("REST_DAY", source, null);
            }
            code = day.ShiftCode;
        }
        else
        {
            var assignments = (await shifts.GetAssignmentsAsync(employeeCode, localWorkDate, cancellationToken))
                .Where(a => a.EmployeeCode == employeeCode && a.IsActive && a.EffectiveFrom.Date <= date &&
                    (!a.EffectiveTo.HasValue || a.EffectiveTo.Value.Date >= date)).ToArray();
            var definitions = new Dictionary<string, ShiftSchedule>(StringComparer.Ordinal);
            foreach (var shiftCode in assignments.Select(a => a.ShiftCode).Distinct())
            {
                var definition = await shifts.GetShiftAsync(shiftCode, cancellationToken);
                if (definition != null) definitions[shiftCode] = definition;
            }
            var selection = AssignmentShiftSelection.Resolve(assignments, definitions, date);
            assignmentId = selection.AssignmentId;
            source = selection.Source;
            if (selection.Status != "WORKING") return Empty(selection.Status, source, selection.ShiftCode);
            code = selection.ShiftCode;
            selectedShift = definitions[code!];
        }
        if (string.IsNullOrWhiteSpace(code)) throw new InvalidOperationException("Selected shift has no code.");
        var shift = selectedShift ?? await GetUsableShiftAsync(code, cancellationToken);
        var grossMinutes = ShiftDefinitionRules.GrossMinutes(shift);
        var workingDays = ReadWorkingDays(shift);
        var start = date.Add(shift.StartTime);
        var end = date.Add(shift.EndTime);
        if (end < start) end = end.AddDays(1); // Night work belongs to its start date.
        // Direct daily assignments express exceptions to recurring weekday patterns.
        if (dailyId == null && !workingDays.Contains((int)localWorkDate.DayOfWeek))
            return Empty("REST_DAY", source, code);
        var minutes = grossMinutes - shift.BreakMinutes;
        return new(employeeCode, localWorkDate, "WORKING", source, code, assignmentId, dailyId, start, end,
            shift.BreakMinutes, Math.Round(minutes, 4, MidpointRounding.AwayFromZero), Math.Round(minutes / 60m, 4, MidpointRounding.AwayFromZero));

        EffectiveShiftDto Empty(string status, string origin, string? shiftCode)
            => new(employeeCode, localWorkDate, status, origin, shiftCode, assignmentId, dailyId, null, null, 0, 0, 0);
    }

    private async Task<ShiftSchedule> GetUsableShiftAsync(string code, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(code)) throw new InvalidOperationException("Selected shift has no code.");
        var definition = await shifts.GetShiftAsync(code, cancellationToken);
        if (definition == null || !definition.IsActive || definition.ShiftCode != code)
            throw new InvalidOperationException("Selected shift is missing or inactive.");
        return definition;
    }

    private static int[] ReadWorkingDays(ShiftSchedule shift)
        => ShiftDefinitionRules.WorkingDays(shift);
}
