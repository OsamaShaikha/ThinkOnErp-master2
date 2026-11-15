using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using ThinkOnErp.Application.DTOs.Hr;
using ThinkOnErp.Domain.Entities.Hr;
using ThinkOnErp.Domain.Interfaces.Hr;

namespace ThinkOnErp.Application.Services.Hr;

public interface IAttendanceCorrectionService
{
    Task<AttendanceCorrectionRequest> SubmitCorrectionRequestAsync(string employeeCode, DateTime attendanceDate, DateTime? requestedIn, DateTime? requestedOut, string reason, string user, CancellationToken cancellationToken = default);
    Task<AttendanceCorrectionRequest> SubmitRequestAsync(AttendanceCorrectionRequestDto dto, string user, CancellationToken cancellationToken = default);
    Task<AttendanceCorrectionRequest> ProcessCorrectionRequestAsync(long requestId, bool approved, string approverUser, string? rejectionReason = null, AttendancePolicy? policy = null, CancellationToken cancellationToken = default);
    Task<AttendanceCorrectionRequest> ProcessRequestAsync(long requestId, ProcessAttendanceCorrectionDto dto, string user, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<AttendanceCorrectionRequest>> GetRequestsAsync(string? employeeCode, string? status, CancellationToken cancellationToken = default);
}

public sealed class AttendanceCorrectionService : IAttendanceCorrectionService
{
    private readonly IAttendanceCorrectionRepository _repository;
    private readonly IAttendanceCalculationEngine _engine;
    private readonly IHrUnitOfWork? _unitOfWork;
    private readonly IPolicyRepository? _policies;
    private readonly TimeProvider _clock;
    private readonly IAttendanceTimeSettingsRepository? _timeSettings;
    private readonly IEmployeeRepository? _employeeRepository;

    public AttendanceCorrectionService(IAttendanceCorrectionRepository repository, IAttendanceCalculationEngine engine, IHrUnitOfWork? unitOfWork = null,
        IPolicyRepository? policies = null, IAttendanceTimeSettingsRepository? timeSettings = null, TimeProvider? clock = null,
        IEmployeeRepository? employeeRepository = null)
    {
        _repository = repository;
        _engine = engine;
        _unitOfWork = unitOfWork;
        _policies = policies; _timeSettings = timeSettings; _clock = clock ?? TimeProvider.System;
        _employeeRepository = employeeRepository;
    }

    public async Task<AttendanceCorrectionRequest> SubmitRequestAsync(AttendanceCorrectionRequestDto dto, string user, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(dto);
        return await SubmitCorrectionRequestAsync(
            dto.EmployeeCode,
            dto.AttendanceDate,
            dto.RequestedCheckIn,
            dto.RequestedCheckOut,
            dto.Reason,
            user,
            cancellationToken
        );
    }

    public async Task<AttendanceCorrectionRequest> SubmitCorrectionRequestAsync(
        string employeeCode,
        DateTime attendanceDate,
        DateTime? requestedIn,
        DateTime? requestedOut,
        string reason,
        string user,
        CancellationToken cancellationToken = default)
    {
        var request = new AttendanceCorrectionRequest
        {
            EmployeeCode = employeeCode, AttendanceDate = attendanceDate,
            RequestedCheckIn = requestedIn, RequestedCheckOut = requestedOut, Reason = reason?.Trim() ?? string.Empty,
            Status = "PENDING", CreationUser = user, CreationDate = DateTime.UtcNow
        };
        AttendanceCorrectionRules.ValidateInput(request);
        EnsurePastPunches(request);
        return ForRead(await _repository.SubmitCorrectionAsync(request, cancellationToken));
    }

    public async Task<AttendanceCorrectionRequest> ProcessRequestAsync(long requestId, ProcessAttendanceCorrectionDto dto, string user, CancellationToken cancellationToken = default)
    {
        if (dto == null)
            throw new ArgumentNullException(nameof(dto));

        return await ProcessCorrectionRequestAsync(requestId, dto.Approved, user, dto.RejectionReason, null, cancellationToken);
    }

    public async Task<AttendanceCorrectionRequest> ProcessCorrectionRequestAsync(
        long requestId,
        bool approved,
        string approverUser,
        string? rejectionReason = null,
        AttendancePolicy? policy = null,
        CancellationToken cancellationToken = default)
    {
        if (!approved && (string.IsNullOrWhiteSpace(rejectionReason) || rejectionReason.Trim().Length > 1000))
            throw new ArgumentException("A rejection reason up to 1000 characters is required.");
        if (!approved) return ForRead(await _repository.RejectCorrectionAsync(requestId, rejectionReason!, approverUser, cancellationToken));
        if (_unitOfWork == null)
            return await ProcessCorrectionRequestCoreAsync(requestId, approved, approverUser, rejectionReason, policy, cancellationToken);
        var request = await _repository.GetCorrectionRequestSnapshotAsync(requestId, cancellationToken)
            ?? throw new KeyNotFoundException($"Attendance correction request {requestId} not found.");
        return await _unitOfWork.ExecuteForEmployeeDatesAsync(request.EmployeeCode, request.AttendanceDate.Date, request.AttendanceDate.Date,
            () => ProcessCorrectionRequestCoreAsync(requestId, approved, approverUser, rejectionReason, policy, cancellationToken, request), cancellationToken);
    }

    private async Task<AttendanceCorrectionRequest> ProcessCorrectionRequestCoreAsync(
        long requestId, bool approved, string approverUser, string? rejectionReason,
        AttendancePolicy? policy, CancellationToken cancellationToken, AttendanceCorrectionRequest? expectedScope = null)
    {
        var request = await _repository.GetCorrectionRequestByIdAsync(requestId, cancellationToken);
        if (request == null)
        {
            throw new KeyNotFoundException($"Attendance correction request {requestId} not found.");
        }

        if (expectedScope != null && (request.EmployeeCode != expectedScope.EmployeeCode || request.AttendanceDate.Date != expectedScope.AttendanceDate.Date))
            throw new AttendanceCorrectionStaleException();

        if (request.Status != "PENDING")
        {
            throw new InvalidOperationException($"Request {requestId} has already been processed with status {request.Status}.");
        }

        if (approved)
        {
            var employeeUserName = _employeeRepository == null ? null :
                await _employeeRepository.GetLinkedUserNameByEmployeeCodeAsync(request.EmployeeCode, cancellationToken);
            HrApprovalGuard.EnsureNotSelfApproved(approverUser, request.CreationUser, employeeUserName, "attendance correction");
            EnsurePastPunches(request);
            await _repository.EnsureEmployeeWorkDateAsync(request.EmployeeCode, request.AttendanceDate, cancellationToken);
            var day = await _repository.GetAttendanceDayAsync(request.EmployeeCode, request.AttendanceDate, cancellationToken);
            if (request.OriginalDayVersion == null) throw new InvalidOperationException("Legacy pending correction needs reviewed resubmission before approval.");
            if (request.OriginalDayVersion != AttendanceCorrectionRules.Version(day)) throw new AttendanceCorrectionStaleException();
            if (day != null && day.TimeZoneId == null) throw new InvalidOperationException("Legacy attendance needs a reviewed time/shift snapshot before recalculation.");
            var first = request.RequestedCheckIn ?? day?.FirstCheckIn;
            var last = request.RequestedCheckOut ?? day?.LastCheckOut;
            if (policy == null && _policies != null && _timeSettings != null)
            {
                var setting = await _timeSettings.GetForEmployeeAsync(request.EmployeeCode, cancellationToken);
                policy = await _policies.GetActiveAttendancePolicyAsync(setting.CompanyId, request.AttendanceDate, cancellationToken);
            }
            var recalculated = await _engine.ProcessEmployeeDayAttendanceAsync(request.EmployeeCode, request.AttendanceDate,
                first, last, policy ?? new AttendancePolicy(), cancellationToken: cancellationToken);
            if (day == null)
            {
                recalculated.CreationUser = approverUser; recalculated.CreationDate = DateTime.UtcNow;
                await _repository.AddAttendanceDayAsync(recalculated, cancellationToken);
            }
            else
            {
                ScheduledAttendanceCalculator.CopyMetrics(recalculated, day);
                day.FirstCheckIn = first; day.LastCheckOut = last;
                day.UpdateUser = approverUser; day.UpdateDate = DateTime.UtcNow;
                await _repository.UpdateAttendanceDayAsync(day, cancellationToken);
            }
            request.AppliedCheckIn = first; request.AppliedCheckOut = last;
        }
        request.Status = approved ? "APPROVED" : "REJECTED";
        request.ApprovedBy = approverUser; request.ApprovalDate = DateTime.UtcNow;
        request.RejectionReason = approved ? null : rejectionReason?.Trim(); request.UpdateUser = approverUser; request.UpdateDate = DateTime.UtcNow;

        await _repository.UpdateCorrectionRequestAsync(request, cancellationToken);
        await _repository.SaveChangesAsync(cancellationToken);

        return ForRead(request);
    }

    private static AttendanceCorrectionRequest ForRead(AttendanceCorrectionRequest r)
    {
        static DateTime? Utc(DateTime? value) => value.HasValue ? DateTime.SpecifyKind(value.Value, DateTimeKind.Utc) : null;
        return new AttendanceCorrectionRequest
        {
            Id = r.Id, EmployeeCode = r.EmployeeCode, AttendanceDate = DateTime.SpecifyKind(r.AttendanceDate.Date, DateTimeKind.Unspecified),
            OldCheckIn = Utc(r.OldCheckIn), OldCheckOut = Utc(r.OldCheckOut), RequestedCheckIn = Utc(r.RequestedCheckIn), RequestedCheckOut = Utc(r.RequestedCheckOut),
            AppliedCheckIn = Utc(r.AppliedCheckIn), AppliedCheckOut = Utc(r.AppliedCheckOut), OriginalDayVersion = r.OriginalDayVersion,
            Reason = r.Reason, Status = r.Status, ApprovedBy = r.ApprovedBy, ApprovalDate = Utc(r.ApprovalDate), RejectionReason = r.RejectionReason,
            CreationUser = r.CreationUser, CreationDate = DateTime.SpecifyKind(r.CreationDate, DateTimeKind.Utc), UpdateUser = r.UpdateUser, UpdateDate = Utc(r.UpdateDate)
        };
    }

    private void EnsurePastPunches(AttendanceCorrectionRequest request)
    {
        var now = _clock.GetUtcNow().UtcDateTime;
        if (request.RequestedCheckIn > now || request.RequestedCheckOut > now)
            throw new ArgumentException("Future punch timestamps cannot be submitted or approved as attendance evidence.");
    }

    public async Task<IReadOnlyList<AttendanceCorrectionRequest>> GetRequestsAsync(string? employeeCode, string? status, CancellationToken cancellationToken = default)
    {
        return (await _repository.GetCorrectionRequestsAsync(employeeCode, status, cancellationToken)).Select(ForRead).ToArray();
    }
}
