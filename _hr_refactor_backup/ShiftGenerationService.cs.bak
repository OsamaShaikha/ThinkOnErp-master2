using ThinkOnErp.Domain.Entities.Hr;
using ThinkOnErp.Domain.Interfaces.Hr;

namespace ThinkOnErp.Application.Services.Hr;

public record GenerateShiftDaysDto(DateOnly? From, DateOnly? To);
public record ShiftGenerationResult(int Created, int Updated, int Unchanged, int PreservedManual, int Unscheduled);

public interface IShiftGenerationService
{
    Task<ShiftGenerationResult> GenerateAsync(string employeeCode, GenerateShiftDaysDto dto, string user, CancellationToken ct = default);
}

public sealed class ShiftGenerationService(IShiftGenerationRepository repository) : IShiftGenerationService
{
    public Task<ShiftGenerationResult> GenerateAsync(string employeeCode, GenerateShiftDaysDto dto, string user, CancellationToken ct = default)
    {
        if (dto.From == null || dto.To == null || dto.To < dto.From || dto.To.Value.DayNumber - dto.From.Value.DayNumber >= 366)
            throw new ArgumentException("Generation needs an ordered date range of at most 366 days.");
        return repository.ExecuteAsync(employeeCode, dto.From.Value.ToDateTime(TimeOnly.MinValue), dto.To.Value.ToDateTime(TimeOnly.MinValue),
            (days, assignments, definitions) => Plan(employeeCode, dto.From.Value, dto.To.Value, days, assignments, definitions), user, ct);
    }

    public static (ShiftGenerationResult Result, IReadOnlyList<EmployeeShiftDay> Writes) Plan(string employeeCode, DateOnly from, DateOnly to,
        IReadOnlyList<EmployeeShiftDay> days, IReadOnlyList<EmployeeShiftAssignment> assignments, IReadOnlyDictionary<string, ShiftSchedule> definitions)
    {
        var existing = days.ToDictionary(d => DateOnly.FromDateTime(d.WorkDate));
        var writes = new List<EmployeeShiftDay>();
        int created = 0, updated = 0, unchanged = 0, manual = 0, unscheduled = 0;
        for (var number = from.DayNumber; number <= to.DayNumber; number++)
        {
            var date = DateOnly.FromDayNumber(number); existing.TryGetValue(date, out var old);
            if (old?.Source == "MANUAL") { manual++; continue; } // Also preserve inactive manual reservations.
            if (old != null && old.Source != "GENERATED") throw new InvalidOperationException("Invalid daily shift source.");
            var selection = AssignmentShiftSelection.Resolve(assignments, definitions, date.ToDateTime(TimeOnly.MinValue));
            if (selection.Status == "UNSCHEDULED")
            {
                unscheduled++;
                if (old == null || !old.IsActive) continue;
                writes.Add(new() { Id = old.Id, EmployeeCode = employeeCode, WorkDate = old.WorkDate, ShiftCode = old.ShiftCode,
                    IsRestDay = old.IsRestDay, IsActive = false, Source = "GENERATED" }); updated++; continue;
            }
            var rest = selection.Status == "REST_DAY"; var code = rest ? null : selection.ShiftCode;
            if (old != null && old.IsActive && old.IsRestDay == rest && old.ShiftCode == code) { unchanged++; continue; }
            writes.Add(new() { Id = old?.Id ?? 0, EmployeeCode = employeeCode, WorkDate = date.ToDateTime(TimeOnly.MinValue), ShiftCode = code,
                IsRestDay = rest, IsActive = true, Source = "GENERATED" });
            if (old == null) created++; else updated++;
        }
        return (new(created, updated, unchanged, manual, unscheduled), writes);
    }
}
