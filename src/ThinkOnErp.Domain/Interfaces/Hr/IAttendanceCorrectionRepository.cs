using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using ThinkOnErp.Domain.Entities.Hr;

namespace ThinkOnErp.Domain.Interfaces.Hr;

public interface IAttendanceCorrectionRepository
{
    Task<IReadOnlyList<AttendanceCorrectionRequest>> GetRequestsAsync(
        string? employeeCode = null,
        DateTime? fromDate = null,
        DateTime? toDate = null,
        string? status = null,
        CancellationToken cancellationToken = default);

    Task<AttendanceCorrectionRequest?> GetByIdAsync(long id, CancellationToken cancellationToken = default);
    Task AddAsync(AttendanceCorrectionRequest request, CancellationToken cancellationToken = default);
    void Update(AttendanceCorrectionRequest request);

    // Raw Attendance Punches
    Task AddRawPunchAsync(RawAttendance rawPunch, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<RawAttendance>> GetUnprocessedRawPunchesAsync(DateTime date, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<RawAttendance>> GetRawPunchesByEmployeeAndDateAsync(string employeeCode, DateTime date, CancellationToken cancellationToken = default);

    // Calculated Attendance Day
    Task<AttendanceDay?> GetAttendanceDayAsync(string employeeCode, DateTime date, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<AttendanceDay>> GetAttendanceDaysAsync(string employeeCode, DateTime fromDate, DateTime toDate, CancellationToken cancellationToken = default);
    Task AddAttendanceDayAsync(AttendanceDay day, CancellationToken cancellationToken = default);
    void UpdateAttendanceDay(AttendanceDay day);

    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}
