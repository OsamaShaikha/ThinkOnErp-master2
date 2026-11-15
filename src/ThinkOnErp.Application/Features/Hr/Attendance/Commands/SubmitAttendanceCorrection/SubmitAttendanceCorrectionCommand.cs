using System;
using System.Threading;
using System.Threading.Tasks;
using FluentValidation;
using MediatR;
using ThinkOnErp.Application.Common;
using ThinkOnErp.Application.DTOs.Hr;
using ThinkOnErp.Application.Features.Hr.Attendance.Common;
using ThinkOnErp.Domain.Entities.Hr;
using ThinkOnErp.Domain.Interfaces.Hr;

namespace ThinkOnErp.Application.Features.Hr.Attendance.Commands.SubmitAttendanceCorrection;

public sealed record SubmitAttendanceCorrectionCommand(AttendanceCorrectionRequestDto Dto, string CurrentUser)
    : IRequest<ApiResponse<AttendanceCorrectionRequest>>;

public sealed class SubmitAttendanceCorrectionCommandValidator : AbstractValidator<SubmitAttendanceCorrectionCommand>
{
    public SubmitAttendanceCorrectionCommandValidator()
    {
        RuleFor(x => x.Dto).NotNull();
        RuleFor(x => x.Dto.EmployeeCode).NotEmpty().MaximumLength(100);
        RuleFor(x => x.Dto.Reason).NotEmpty().MaximumLength(1000);
    }
}

public sealed class SubmitAttendanceCorrectionCommandHandler
    : IRequestHandler<SubmitAttendanceCorrectionCommand, ApiResponse<AttendanceCorrectionRequest>>
{
    private readonly IAttendanceCorrectionRepository _repository;
    private readonly TimeProvider _clock;

    public SubmitAttendanceCorrectionCommandHandler(
        IAttendanceCorrectionRepository repository,
        TimeProvider? clock = null)
    {
        _repository = repository;
        _clock = clock ?? TimeProvider.System;
    }

    public async Task<ApiResponse<AttendanceCorrectionRequest>> Handle(
        SubmitAttendanceCorrectionCommand request,
        CancellationToken cancellationToken)
    {
        var dto = request.Dto;
        var entity = new AttendanceCorrectionRequest
        {
            EmployeeCode = dto.EmployeeCode,
            AttendanceDate = dto.AttendanceDate,
            RequestedCheckIn = dto.RequestedCheckIn,
            RequestedCheckOut = dto.RequestedCheckOut,
            Reason = dto.Reason?.Trim() ?? string.Empty,
            Status = "PENDING",
            CreationUser = request.CurrentUser,
            CreationDate = DateTime.UtcNow
        };

        AttendanceCorrectionRules.ValidateInput(entity);
        EnsurePastPunches(entity);

        var saved = await _repository.SubmitCorrectionAsync(entity, cancellationToken);
        return ApiResponse<AttendanceCorrectionRequest>.CreateSuccess(
            AttendanceCommon.ForRead(saved),
            "Correction request submitted successfully.");
    }

    private void EnsurePastPunches(AttendanceCorrectionRequest entity)
    {
        var now = _clock.GetUtcNow().UtcDateTime;
        if (entity.RequestedCheckIn > now || entity.RequestedCheckOut > now)
            throw new ArgumentException("Future punch timestamps cannot be submitted or approved as attendance evidence.");
    }
}
