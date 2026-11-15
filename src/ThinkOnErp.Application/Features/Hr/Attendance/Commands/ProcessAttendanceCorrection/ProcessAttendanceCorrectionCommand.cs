using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using FluentValidation;
using MediatR;
using System.Net;
using ThinkOnErp.Application.Common;
using ThinkOnErp.Application.DTOs.Hr;
using ThinkOnErp.Application.Services.Hr;
using ThinkOnErp.Application.Features.Hr.Attendance.Common;
using ThinkOnErp.Domain.Entities.Hr;
using ThinkOnErp.Domain.Interfaces.Hr;

namespace ThinkOnErp.Application.Features.Hr.Attendance.Commands.ProcessAttendanceCorrection;

public sealed record ProcessAttendanceCorrectionCommand(long Id, ProcessAttendanceCorrectionDto Dto, string CurrentUser)
    : IRequest<ApiResponse<AttendanceCorrectionRequest>>;

public sealed class ProcessAttendanceCorrectionCommandValidator : AbstractValidator<ProcessAttendanceCorrectionCommand>
{
    public ProcessAttendanceCorrectionCommandValidator()
    {
        RuleFor(x => x.Id).GreaterThan(0);
        RuleFor(x => x.Dto).NotNull();
        RuleFor(x => x.Dto.RejectionReason)
            .NotEmpty().WithMessage("A rejection reason up to 1000 characters is required.")
            .MaximumLength(1000).WithMessage("A rejection reason up to 1000 characters is required.")
            .When(x => !x.Dto.Approved);
    }
}

public sealed class ProcessAttendanceCorrectionCommandHandler
    : IRequestHandler<ProcessAttendanceCorrectionCommand, ApiResponse<AttendanceCorrectionRequest>>
{
    private readonly IAttendanceCorrectionRepository _repository;
    private readonly IAttendanceCalculationEngine _engine;
    private readonly IHrUnitOfWork _unitOfWork;
    private readonly IPolicyRepository _policies;
    private readonly IAttendanceTimeSettingsRepository _timeSettings;
    private readonly IEmployeeRepository _employeeRepository;
    private readonly TimeProvider _clock;

    public ProcessAttendanceCorrectionCommandHandler(
        IAttendanceCorrectionRepository repository,
        IAttendanceCalculationEngine engine,
        IHrUnitOfWork unitOfWork,
        IPolicyRepository policies,
        IAttendanceTimeSettingsRepository timeSettings,
        IEmployeeRepository employeeRepository,
        TimeProvider? clock = null)
    {
        _repository = repository;
        _engine = engine;
        _unitOfWork = unitOfWork;
        _policies = policies;
        _timeSettings = timeSettings;
        _employeeRepository = employeeRepository;
        _clock = clock ?? TimeProvider.System;
    }

    public async Task<ApiResponse<AttendanceCorrectionRequest>> Handle(
        ProcessAttendanceCorrectionCommand request,
        CancellationToken cancellationToken)
    {
        var id = request.Id;
        var dto = request.Dto;
        var approverUser = request.CurrentUser;

        try
        {
            AttendanceCorrectionRequest processed;
            if (!dto.Approved)
            {
                if (string.IsNullOrWhiteSpace(dto.RejectionReason) || dto.RejectionReason.Trim().Length > 1000)
                    throw new ArgumentException("A rejection reason up to 1000 characters is required.");

                processed = AttendanceCommon.ForRead(await _repository.RejectCorrectionAsync(
                    id, dto.RejectionReason.Trim(), approverUser, cancellationToken));
            }
            else
            {
                var snapshot = await _repository.GetCorrectionRequestSnapshotAsync(id, cancellationToken)
                    ?? throw new KeyNotFoundException($"Attendance correction request {id} not found.");

                processed = await _unitOfWork.ExecuteForEmployeeDatesAsync(
                    snapshot.EmployeeCode,
                    snapshot.AttendanceDate.Date,
                    snapshot.AttendanceDate.Date,
                    () => ProcessCoreAsync(id, dto.Approved, approverUser, dto.RejectionReason, cancellationToken, snapshot),
                    cancellationToken);
            }

            return ApiResponse<AttendanceCorrectionRequest>.CreateSuccess(
                processed,
                "Correction request processed successfully.");
        }
        catch (AttendanceCorrectionStaleException ex)
        {
            return ApiResponse<AttendanceCorrectionRequest>.CreateFailure(ex.Message, statusCode: (int)HttpStatusCode.Conflict);
        }
    }

    private async Task<AttendanceCorrectionRequest> ProcessCoreAsync(
        long requestId,
        bool approved,
        string approverUser,
        string? rejectionReason,
        CancellationToken cancellationToken,
        AttendanceCorrectionRequest? expectedScope = null)
    {
        var request = await _repository.GetCorrectionRequestByIdAsync(requestId, cancellationToken)
            ?? throw new KeyNotFoundException($"Attendance correction request {requestId} not found.");

        if (expectedScope != null && (request.EmployeeCode != expectedScope.EmployeeCode || request.AttendanceDate.Date != expectedScope.AttendanceDate.Date))
            throw new AttendanceCorrectionStaleException();

        if (request.Status != "PENDING")
            throw new InvalidOperationException($"Request {requestId} has already been processed with status {request.Status}.");

        if (approved)
        {
            var employeeUserName = await _employeeRepository.GetLinkedUserNameByEmployeeCodeAsync(request.EmployeeCode, cancellationToken);
            HrApprovalGuard.EnsureNotSelfApproved(approverUser, request.CreationUser, employeeUserName, "attendance correction");
            EnsurePastPunches(request);

            await _repository.EnsureEmployeeWorkDateAsync(request.EmployeeCode, request.AttendanceDate, cancellationToken);
            var day = await _repository.GetAttendanceDayAsync(request.EmployeeCode, request.AttendanceDate, cancellationToken);
            if (request.OriginalDayVersion == null)
                throw new InvalidOperationException("Legacy pending correction needs reviewed resubmission before approval.");
            if (request.OriginalDayVersion != AttendanceCorrectionRules.Version(day))
                throw new AttendanceCorrectionStaleException();
            if (day != null && day.TimeZoneId == null)
                throw new InvalidOperationException("Legacy attendance needs a reviewed time/shift snapshot before recalculation.");

            var first = request.RequestedCheckIn ?? day?.FirstCheckIn;
            var last = request.RequestedCheckOut ?? day?.LastCheckOut;

            var setting = await _timeSettings.GetForEmployeeAsync(request.EmployeeCode, cancellationToken);
            var policy = await _policies.GetActiveAttendancePolicyAsync(setting.CompanyId, request.AttendanceDate, cancellationToken)
                ?? new AttendancePolicy();

            var recalculated = await _engine.ProcessEmployeeDayAttendanceAsync(
                request.EmployeeCode, request.AttendanceDate, first, last, policy, cancellationToken: cancellationToken);

            if (day == null)
            {
                recalculated.CreationUser = approverUser;
                recalculated.CreationDate = DateTime.UtcNow;
                await _repository.AddAttendanceDayAsync(recalculated, cancellationToken);
            }
            else
            {
                ScheduledAttendanceCalculator.CopyMetrics(recalculated, day);
                day.FirstCheckIn = first;
                day.LastCheckOut = last;
                day.UpdateUser = approverUser;
                day.UpdateDate = DateTime.UtcNow;
                await _repository.UpdateAttendanceDayAsync(day, cancellationToken);
            }

            request.AppliedCheckIn = first;
            request.AppliedCheckOut = last;
        }

        request.Status = approved ? "APPROVED" : "REJECTED";
        request.ApprovedBy = approverUser;
        request.ApprovalDate = DateTime.UtcNow;
        request.RejectionReason = approved ? null : rejectionReason?.Trim();
        request.UpdateUser = approverUser;
        request.UpdateDate = DateTime.UtcNow;

        await _repository.UpdateCorrectionRequestAsync(request, cancellationToken);
        await _repository.SaveChangesAsync(cancellationToken);

        return AttendanceCommon.ForRead(request);
    }

    private void EnsurePastPunches(AttendanceCorrectionRequest request)
    {
        var now = _clock.GetUtcNow().UtcDateTime;
        if (request.RequestedCheckIn > now || request.RequestedCheckOut > now)
            throw new ArgumentException("Future punch timestamps cannot be submitted or approved as attendance evidence.");
    }
}
