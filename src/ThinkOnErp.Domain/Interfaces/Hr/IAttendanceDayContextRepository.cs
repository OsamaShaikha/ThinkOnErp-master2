using ThinkOnErp.Domain.Entities.Hr;

namespace ThinkOnErp.Domain.Interfaces.Hr;

public interface IAttendanceDayContextRepository
{
    Task<AttendanceDayContext> GetAsync(string employeeCode, DateTime workDate, DateTime? startUtc, DateTime? endUtc, CancellationToken ct = default);
    Task<bool> HasReviewDaysAsync(IReadOnlyList<string> employeeCodes, DateTime from, DateTime to, CancellationToken ct = default);
}
