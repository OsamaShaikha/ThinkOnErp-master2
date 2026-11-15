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
    Task<AttendanceCorrectionRequest?> GetCorrectionRequestSnapshotAsync(long id, CancellationToken cancellationToken = default);
    Task<AttendanceCorrectionRequest> RejectCorrectionAsync(long id, string reason, string user, CancellationToken cancellationToken = default);
    Task<AttendanceCorrectionRequest> SubmitCorrectionAsync(AttendanceCorrectionRequest request, CancellationToken cancellationToken = default);
    Task AddCorrectionRequestAsync(AttendanceCorrectionRequest request, CancellationToken cancellationToken = default);
    Task UpdateCorrectionRequestAsync(AttendanceCorrectionRequest request, CancellationToken cancellationToken = default);

    Task EnsureEmployeeWorkDateAsync(string employeeCode, DateTime workDate, CancellationToken cancellationToken = default);
    Task<AttendanceDay?> GetAttendanceDayAsync(string employeeCode, DateTime date, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<AttendanceDay>> GetAttendanceDaysAsync(string employeeCode, DateTime fromDate, DateTime toDate, CancellationToken cancellationToken = default);
    Task AddAttendanceDayAsync(AttendanceDay day, CancellationToken cancellationToken = default);
    Task UpdateAttendanceDayAsync(AttendanceDay day, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<RawAttendance>> GetUnprocessedRawPunchesAsync(int batchSize = 500, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<RawAttendance>> GetAutomaticallyProcessableRawPunchesAsync(int batchSize, DateTime retryBeforeUtc, CancellationToken cancellationToken = default);
    Task<T> ExecuteForRawWorkDateAsync<T>(string employeeCode, DateTime workDate, Func<Task<T>> action, CancellationToken cancellationToken = default);
    Task<RawAttendance?> GetRawEventAsync(long id, CancellationToken cancellationToken = default);
    Task<RawAttendance?> LockRawEventAsync(long id, string employeeCode, CancellationToken cancellationToken = default);
    Task<RawAttendance> BindRawWorkDateAsync(long id, DateTime workDate, CancellationToken cancellationToken = default);
    Task<RawAttendance> IgnoreRawEventAsync(long id, string reason, string user, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<RawAttendance>> GetProcessedDayEventsAsync(string employeeCode, DateTime workDate, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<RawAttendance>> GetEarlierPendingEventsAsync(string employeeCode, DateTime punchTime, long id, CancellationToken cancellationToken = default);
    Task<bool> HasApprovedCorrectionAsync(string employeeCode, DateTime workDate, CancellationToken cancellationToken = default);
    Task UpdateRawAttendanceAsync(RawAttendance raw, CancellationToken cancellationToken = default);
    Task<RawAttendance> AcceptMobileEvidenceAsync(RawAttendance proposed, Func<long, string> responseJson, CancellationToken cancellationToken = default);
    Task RecordRawAttemptAsync(long id, string user, string? reason, CancellationToken cancellationToken = default);
    Task<RawAttendance> AcceptDeviceEventAsync(RawAttendance proposed, CancellationToken cancellationToken = default);
    Task<RawAttendance?> GetMobileEventAsync(string employeeCode, string eventId, CancellationToken cancellationToken = default);
    Task AddRawAttendanceAsync(RawAttendance raw, CancellationToken cancellationToken = default);

    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}
