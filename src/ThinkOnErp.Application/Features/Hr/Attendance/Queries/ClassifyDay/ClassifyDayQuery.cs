using System;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using ThinkOnErp.Application.Common;
using ThinkOnErp.Application.Services.Hr;
using ThinkOnErp.Domain.Entities.Hr;
using ThinkOnErp.Domain.Interfaces.Hr;

namespace ThinkOnErp.Application.Features.Hr.Attendance.Queries.ClassifyDay;

public sealed record ClassifyDayQuery(string EmployeeCode, DateOnly WorkDate)
    : IRequest<ApiResponse<AttendanceDayClassificationDto>>;

public sealed class ClassifyDayQueryHandler
    : IRequestHandler<ClassifyDayQuery, ApiResponse<AttendanceDayClassificationDto>>
{
    private readonly IShiftTimingService _timing;
    private readonly IAttendanceDayClassifier _classifier;
    private readonly IAttendanceCorrectionRepository _attendance;
    private readonly IAttendanceTimeSettingsRepository _settings;
    private readonly IPolicyRepository _policies;

    public ClassifyDayQueryHandler(
        IShiftTimingService timing,
        IAttendanceDayClassifier classifier,
        IAttendanceCorrectionRepository attendance,
        IAttendanceTimeSettingsRepository settings,
        IPolicyRepository policies)
    {
        _timing = timing;
        _classifier = classifier;
        _attendance = attendance;
        _settings = settings;
        _policies = policies;
    }

    public async Task<ApiResponse<AttendanceDayClassificationDto>> Handle(
        ClassifyDayQuery request,
        CancellationToken cancellationToken)
    {
        var employeeCode = request.EmployeeCode;
        var workDate = request.WorkDate;

        var setting = await _settings.GetForEmployeeAsync(employeeCode, cancellationToken);
        var schedule = await _timing.PreviewConfiguredAsync(employeeCode, workDate, cancellationToken);
        var date = workDate.ToDateTime(TimeOnly.MinValue);
        var old = await _attendance.GetAttendanceDayAsync(employeeCode, date, cancellationToken);
        if (old != null && old.TimeZoneId == null)
            throw new InvalidOperationException("Legacy attendance needs a reviewed time/shift snapshot before classification.");

        var policy = await _policies.GetActiveAttendancePolicyAsync(setting.CompanyId, date, cancellationToken) ?? new AttendancePolicy();
        var calculated = ScheduledAttendanceCalculator.Calculate(employeeCode, workDate, old?.FirstCheckIn, old?.LastCheckOut, schedule, policy);
        calculated.Id = old?.Id ?? 0;

        var classified = await _classifier.ClassifyAsync(calculated, schedule, cancellationToken);
        return ApiResponse<AttendanceDayClassificationDto>.CreateSuccess(classified);
    }
}
