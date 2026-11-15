using ThinkOnErp.Domain.Entities.Hr;

namespace ThinkOnErp.Domain.Interfaces.Hr;

public interface IShiftAssignmentRepository
{
    Task EnsureEmployeeAsync(string employeeCode, CancellationToken ct = default);
    Task<(IReadOnlyList<EmployeeShiftAssignment> Items, int Total)> ListAsync(string employeeCode, DateTime? from, DateTime? to, bool activeOnly, int page, int size, CancellationToken ct = default);
    Task<EmployeeShiftAssignment?> GetAsync(string employeeCode, long id, CancellationToken ct = default);
    Task<IReadOnlyDictionary<string, ShiftSchedule>> DefinitionsAsync(IReadOnlyList<string> codes, CancellationToken ct = default);
    Task<EmployeeShiftAssignment> CreateAsync(EmployeeShiftAssignment proposed, string shiftVersion, CancellationToken ct = default);
    Task<EmployeeShiftAssignment> UpdateAsync(string employeeCode, long id, EmployeeShiftAssignment proposed, string assignmentVersion, string shiftVersion, string user, CancellationToken ct = default);
    Task DeleteAsync(string employeeCode, long id, string assignmentVersion, CancellationToken ct = default);
}
