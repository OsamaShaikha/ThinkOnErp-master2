using ThinkOnErp.Domain.Entities.Hr;

namespace ThinkOnErp.Domain.Interfaces.Hr;

public interface IShiftGenerationRepository
{
    Task<T> ExecuteAsync<T>(string employeeCode, DateTime from, DateTime to,
        Func<IReadOnlyList<EmployeeShiftDay>, IReadOnlyList<EmployeeShiftAssignment>, IReadOnlyDictionary<string, ShiftSchedule>, (T Result, IReadOnlyList<EmployeeShiftDay> Writes)> plan,
        string user, CancellationToken ct = default);
}
