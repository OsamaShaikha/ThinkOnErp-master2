using ThinkOnErp.Domain.Entities.Hr;

namespace ThinkOnErp.Domain.Interfaces.Hr;

public interface IShiftDayRepository
{
    Task EnsureEmployeeAsync(string employeeCode, CancellationToken ct = default);
    Task<(IReadOnlyList<EmployeeShiftDay> Items, int Total)> ListAsync(string employeeCode, DateTime? from, DateTime? to, bool activeOnly, int page, int size, CancellationToken ct = default);
    Task<EmployeeShiftDay?> GetAsync(string employeeCode, long id, CancellationToken ct = default);
    Task<IReadOnlyDictionary<string, ShiftSchedule>> DefinitionsAsync(IReadOnlyList<string> codes, CancellationToken ct = default);
    Task<EmployeeShiftDay> CreateAsync(EmployeeShiftDay proposed, string? shiftVersion, CancellationToken ct = default);
    Task<EmployeeShiftDay> UpdateAsync(string employeeCode, long id, EmployeeShiftDay proposed, string version, string? shiftVersion, string user, CancellationToken ct = default);
    Task DeleteAsync(string employeeCode, long id, string version, CancellationToken ct = default);
}
