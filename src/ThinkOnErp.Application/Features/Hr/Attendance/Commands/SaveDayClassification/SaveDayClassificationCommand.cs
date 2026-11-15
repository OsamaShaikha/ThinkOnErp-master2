using System;
using System.Threading;
using System.Threading.Tasks;
using FluentValidation;
using MediatR;
using ThinkOnErp.Application.Common;
using ThinkOnErp.Application.Services.Hr;
using ThinkOnErp.Domain.Entities.Hr;
using ThinkOnErp.Domain.Interfaces.Hr;

namespace ThinkOnErp.Application.Features.Hr.Attendance.Commands.SaveDayClassification;

public sealed record SaveDayClassificationCommand(string EmployeeCode, DateOnly WorkDate, string CurrentUser)
    : IRequest<ApiResponse<AttendanceDayClassificationDto>>;

public sealed class SaveDayClassificationCommandValidator : AbstractValidator<SaveDayClassificationCommand>
{
    public SaveDayClassificationCommandValidator()
    {
        RuleFor(x => x.EmployeeCode).NotEmpty().MaximumLength(100);
    }
}

public sealed class SaveDayClassificationCommandHandler : IRequestHandler<SaveDayClassificationCommand, ApiResponse<AttendanceDayClassificationDto>>
{
    private readonly IShiftTimingService _timing;
    private readonly IAttendanceDayClassifier _classifier;
    private readonly IAttendanceCorrectionRepository _attendance;
    private readonly IAttendanceTimeSettingsRepository _settings;
    private readonly IPolicyRepository _policies;
    private readonly IHrUnitOfWork _unitOfWork;

    public SaveDayClassificationCommandHandler(
        IShiftTimingService timing,
        IAttendanceDayClassifier classifier,
        IAttendanceCorrectionRepository attendance,
        IAttendanceTimeSettingsRepository settings,
        IPolicyRepository policies,
        IHrUnitOfWork unitOfWork)
    {
        _timing = timing;
        _classifier = classifier;
        _attendance = attendance;
        _settings = settings;
        _policies = policies;
        _unitOfWork = unitOfWork;
    }

    public async Task<ApiResponse<AttendanceDayClassificationDto>> Handle(SaveDayClassificationCommand request, CancellationToken cancellationToken)
    {
        var employeeCode = request.EmployeeCode;
        var workDate = request.WorkDate;
        var user = request.CurrentUser;

        var result = await _unitOfWork.ExecuteForEmployeeDatesAsync(
            employeeCode,
            workDate.ToDateTime(TimeOnly.MinValue),
            workDate.ToDateTime(TimeOnly.MinValue),
            async () =>
            {
                var setting = await _settings.GetForEmployeeAsync(employeeCode, cancellationToken);
                var today = DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(DateTimeOffset.UtcNow, ShiftTimeRules.Zone(setting.TimeZoneId)).DateTime);
                if (workDate > today)
                    throw new InvalidOperationException("Future attendance dates cannot be finalized.");

                await _attendance.EnsureEmployeeWorkDateAsync(employeeCode, workDate.ToDateTime(TimeOnly.MinValue), cancellationToken);
                var preview = await PreviewInternalAsync(employeeCode, workDate, setting, cancellationToken);

                if (preview.Day.Status == "PENDING")
                    throw new InvalidOperationException("The scheduled work period has not ended; absence cannot be finalized yet.");

                var old = await _attendance.GetAttendanceDayAsync(employeeCode, workDate.ToDateTime(TimeOnly.MinValue), cancellationToken);
                if (old == null)
                {
                    preview.Day.CreationUser = user;
                    preview.Day.CreationDate = DateTime.UtcNow;
                    await _attendance.AddAttendanceDayAsync(preview.Day, cancellationToken);
                }
                else
                {
                    ScheduledAttendanceCalculator.CopyMetrics(preview.Day, old);
                    old.UpdateUser = user;
                    old.UpdateDate = DateTime.UtcNow;
                    await _attendance.UpdateAttendanceDayAsync(old, cancellationToken);
                    preview = preview with { Day = old };
                }

                await _attendance.SaveChangesAsync(cancellationToken);
                return preview;
            },
            cancellationToken);

        return ApiResponse<AttendanceDayClassificationDto>.CreateSuccess(result);
    }

    private async Task<AttendanceDayClassificationDto> PreviewInternalAsync(string employeeCode, DateOnly workDate, AttendanceTimeSettings setting, CancellationToken ct)
    {
        var schedule = await _timing.PreviewConfiguredAsync(employeeCode, workDate, ct);
        var date = workDate.ToDateTime(TimeOnly.MinValue);
        var old = await _attendance.GetAttendanceDayAsync(employeeCode, date, ct);
        if (old != null && old.TimeZoneId == null)
            throw new InvalidOperationException("Legacy attendance needs a reviewed time/shift snapshot before classification.");

        var policy = await _policies.GetActiveAttendancePolicyAsync(setting.CompanyId, date, ct) ?? new AttendancePolicy();
        var calculated = ScheduledAttendanceCalculator.Calculate(employeeCode, workDate, old?.FirstCheckIn, old?.LastCheckOut, schedule, policy);
        calculated.Id = old?.Id ?? 0;
        return await _classifier.ClassifyAsync(calculated, schedule, ct);
    }
}
