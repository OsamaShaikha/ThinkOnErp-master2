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

    public AttendanceCorrectionService(IAttendanceCorrectionRepository repository, IAttendanceCalculationEngine engine)
    {
        _repository = repository;
        _engine = engine;
    }

    public async Task<AttendanceCorrectionRequest> SubmitRequestAsync(AttendanceCorrectionRequestDto dto, string user, CancellationToken cancellationToken = default)
    {
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
        var existingDay = await _repository.GetAttendanceDayAsync(employeeCode, attendanceDate, cancellationToken);

        var request = new AttendanceCorrectionRequest
        {
            EmployeeCode = employeeCode,
            AttendanceDate = attendanceDate.Date,
            OldCheckIn = existingDay?.FirstCheckIn,
            OldCheckOut = existingDay?.LastCheckOut,
            RequestedCheckIn = requestedIn,
            RequestedCheckOut = requestedOut,
            Reason = reason,
            Status = "PENDING",
            CreationUser = user,
            CreationDate = DateTime.UtcNow
        };

        await _repository.AddCorrectionRequestAsync(request, cancellationToken);
        await _repository.SaveChangesAsync(cancellationToken);

        return request;
    }

    public async Task<AttendanceCorrectionRequest> ProcessRequestAsync(long requestId, ProcessAttendanceCorrectionDto dto, string user, CancellationToken cancellationToken = default)
    {
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
        var request = await _repository.GetCorrectionRequestByIdAsync(requestId, cancellationToken);
        if (request == null)
        {
            throw new InvalidOperationException($"Attendance correction request {requestId} not found.");
        }

        if (request.Status != "PENDING")
        {
            throw new InvalidOperationException($"Request {requestId} has already been processed with status {request.Status}.");
        }

        request.Status = approved ? "APPROVED" : "REJECTED";
        request.ApprovedBy = approverUser;
        request.ApprovalDate = DateTime.UtcNow;
        request.RejectionReason = rejectionReason;
        request.UpdateUser = approverUser;
        request.UpdateDate = DateTime.UtcNow;

        if (approved)
        {
            var day = await _repository.GetAttendanceDayAsync(request.EmployeeCode, request.AttendanceDate, cancellationToken);
            if (day == null)
            {
                day = new AttendanceDay
                {
                    EmployeeCode = request.EmployeeCode,
                    AttendanceDate = request.AttendanceDate,
                    FirstCheckIn = request.RequestedCheckIn,
                    LastCheckOut = request.RequestedCheckOut,
                    Status = "PRESENT",
                    CreationUser = approverUser,
                    CreationDate = DateTime.UtcNow
                };
                await _repository.AddAttendanceDayAsync(day, cancellationToken);
            }
            else
            {
                if (request.RequestedCheckIn.HasValue) day.FirstCheckIn = request.RequestedCheckIn.Value;
                if (request.RequestedCheckOut.HasValue) day.LastCheckOut = request.RequestedCheckOut.Value;
                day.Status = "PRESENT";
                day.UpdateUser = approverUser;
                day.UpdateDate = DateTime.UtcNow;
                await _repository.UpdateAttendanceDayAsync(day, cancellationToken);
            }
        }

        await _repository.UpdateCorrectionRequestAsync(request, cancellationToken);
        await _repository.SaveChangesAsync(cancellationToken);

        return request;
    }

    public async Task<IReadOnlyList<AttendanceCorrectionRequest>> GetRequestsAsync(string? employeeCode, string? status, CancellationToken cancellationToken = default)
    {
        return await _repository.GetCorrectionRequestsAsync(employeeCode, status, cancellationToken);
    }
}
