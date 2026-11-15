namespace ThinkOnErp.Domain.Entities.Hr;

public record AssignmentShiftSelection(string Status, string Source, string? ShiftCode, long? AssignmentId)
{
    public static AssignmentShiftSelection Resolve(IEnumerable<EmployeeShiftAssignment> assignments,
        IReadOnlyDictionary<string, ShiftSchedule> definitions, DateTime date)
    {
        EmployeeShiftAssignment[] rows = assignments.Where(a => a.IsActive && a.EffectiveFrom.Date <= date.Date && (!a.EffectiveTo.HasValue || a.EffectiveTo.Value.Date >= date.Date)).ToArray();
        foreach (EmployeeShiftAssignment? row in rows)
        {
            _ = ShiftAssignmentRules.Kind(row);
        }

        EmployeeShiftAssignment[] temporary = rows.Where(a => ShiftAssignmentRules.Kind(a) == "TEMPORARY").ToArray();
        if (temporary.Length > 1)
        {
            throw new InvalidOperationException("Conflicting temporary shifts for this employee/date.");
        }

        string source = temporary.Length > 0 ? "TEMPORARY" : "BASE";
        EmployeeShiftAssignment[] candidates = temporary.Length > 0 ? temporary : rows.Where(a => ShiftAssignmentRules.Kind(a) == "BASE").ToArray();
        if (candidates.Length == 0)
        {
            return new("UNSCHEDULED", "NONE", null, null);
        }

        ShiftSchedule Definition(EmployeeShiftAssignment row)
        {
            if (!definitions.TryGetValue(row.ShiftCode, out ShiftSchedule? definition) || !definition.IsActive)
            {
                throw new InvalidOperationException("Selected shift is missing or inactive.");
            }

            _ = ShiftDefinitionRules.GrossMinutes(definition); return definition;
        }
        EmployeeShiftAssignment[] working = candidates.Where(a => ShiftDefinitionRules.WorkingDays(Definition(a)).Contains((int)date.DayOfWeek)).ToArray();
        return working.Length > 1
            ? throw new InvalidOperationException("Conflicting base shifts for this employee/date.")
            : working.Length == 0
            ? new("REST_DAY", source, candidates.Length == 1 ? candidates[0].ShiftCode : null, candidates.Length == 1 ? candidates[0].Id : null)
            : new("WORKING", source, working[0].ShiftCode, working[0].Id);
    }
}
