using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using ThinkOnErp.Domain.Entities.Hr;

namespace ThinkOnErp.Domain.Interfaces.Hr;

public interface IAttendanceCorrectionRepository
{
    Task<IReadOnlyList<AttendanceCorrectionRequest>> GetCorrectionRequestsAsync(string? employeeCode, string? status, CancellationToken cancellationToken = default);
    Task<AttendanceCorrectionRequest?> GetCorrectionRequestByIdAsync(long id, CancellationToken cancellationToken = default);
    Task AddCorrectionRequestAsync(AttendanceCorrectionRequest request, CancellationToken cancellationToken = default);
    Task UpdateCorrectionRequestAsync(AttendanceCorrectionRequest request, CancellationToken cancellationToken = default);

    Task<AttendanceDay?> GetAttendanceDayAsync(string employeeCode, DateTime date, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<AttendanceDay>> GetAttendanceDaysAsync(string employeeCode, DateTime fromDate, DateTime toDate, CancellationToken cancellationToken = default);
    Task AddAttendanceDayAsync(AttendanceDay day, CancellationToken cancellationToken = default);
    Task UpdateAttendanceDayAsync(AttendanceDay day, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<RawAttendance>> GetUnprocessedRawPunchesAsync(int batchSize = 500, CancellationToken cancellationToken = default);
    Task AddRawAttendanceAsync(RawAttendance raw, CancellationToken cancellationToken = default);

    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}
