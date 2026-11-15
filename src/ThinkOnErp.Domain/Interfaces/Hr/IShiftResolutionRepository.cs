using ThinkOnErp.Domain.Entities.Hr;

namespace ThinkOnErp.Domain.Interfaces.Hr;

public interface IShiftResolutionRepository
{
    Task<IReadOnlyList<EmployeeShiftDay>> GetDailyShiftsAsync(string employeeCode, DateOnly workDate, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<EmployeeShiftAssignment>> GetAssignmentsAsync(string employeeCode, DateOnly workDate, CancellationToken cancellationToken = default);
    Task<ShiftSchedule?> GetShiftAsync(string shiftCode, CancellationToken cancellationToken = default);
}
