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
        var request = await _repository.GetCorrectionRequestByIdAsync(requestId, cancellationToken);
        if (request == null)
        {
            throw new KeyNotFoundException($"Attendance correction request {requestId} not found.");
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
            bool isNew = false;
            if (day == null)
            {
                isNew = true;
                day = new AttendanceDay
                {
                    EmployeeCode = request.EmployeeCode,
                    AttendanceDate = request.AttendanceDate,
                    FirstCheckIn = request.RequestedCheckIn,
                    LastCheckOut = request.RequestedCheckOut,
                    CreationUser = approverUser,
                    CreationDate = DateTime.UtcNow
                };
            }
            else
            {
                if (request.RequestedCheckIn.HasValue) day.FirstCheckIn = request.RequestedCheckIn.Value;
                if (request.RequestedCheckOut.HasValue) day.LastCheckOut = request.RequestedCheckOut.Value;
                day.UpdateUser = approverUser;
                day.UpdateDate = DateTime.UtcNow;
            }

            // Evaluate punch completeness and recalculate metrics
            if (day.FirstCheckIn.HasValue && day.LastCheckOut.HasValue)
            {
                day.HasMissingPunch = false;
                day.Status = "PRESENT";

                var diff = day.LastCheckOut.Value - day.FirstCheckIn.Value;
                if (diff.TotalHours < 0) diff = diff.Add(TimeSpan.FromHours(24));
                day.ActualWorkedHours = Math.Max(0m, Math.Round((decimal)diff.TotalHours, 2));

                // Recalculate late arrival against standard 08:00 start
                var targetStart = new TimeSpan(8, 0, 0);
                var actualStart = day.FirstCheckIn.Value.TimeOfDay;
                var grace = policy?.GracePeriodMinutes ?? 15;
                if (actualStart > targetStart)
                {
                    var lateTotal = (int)(actualStart - targetStart).TotalMinutes;
                    day.LateArrivalMinutes = lateTotal > grace
                        ? ((policy?.AutoDeductLateArrival ?? true) ? lateTotal - grace : lateTotal)
                        : 0;
                }
                else
                {
                    day.LateArrivalMinutes = 0;
                }

                // Recalculate early leave against standard 17:00 end
                var targetEnd = new TimeSpan(17, 0, 0);
                var actualEnd = day.LastCheckOut.Value.TimeOfDay;
                var earlyTolerance = policy?.EarlyLeaveToleranceMinutes ?? 10;
                if (actualEnd < targetEnd)
                {
                    var earlyTotal = (int)(targetEnd - actualEnd).TotalMinutes;
                    day.EarlyLeaveMinutes = earlyTotal > earlyTolerance ? earlyTotal : 0;
                }
                else
                {
                    day.EarlyLeaveMinutes = 0;
                }
            }
            else
            {
                day.HasMissingPunch = true;
                day.Status = "INCOMPLETE";
            }

            if (isNew)
            {
                await _repository.AddAttendanceDayAsync(day, cancellationToken);
            }
            else
            {
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
