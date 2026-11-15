using ThinkOnErp.Domain.Entities.Hr;

namespace ThinkOnErp.Domain.Interfaces.Hr;

public interface IAttendanceTimeSettingsRepository
{
    Task<AttendanceTimeSettings?> GetCurrentAsync(CancellationToken ct = default);
    Task<AttendanceTimeSettings> InitializeAsync(string timeZoneId, string user, CancellationToken ct = default);
    Task<AttendanceTimeSettings> GetForEmployeeAsync(string employeeCode, CancellationToken ct = default);
}
