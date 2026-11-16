using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FluentValidation;
using MediatR;
using System.Net;
using ThinkOnErp.Application.Common;
using ThinkOnErp.Application.DTOs.Hr;
using ThinkOnErp.Domain.Interfaces.Hr;

namespace ThinkOnErp.Application.Features.Hr.Attendance.Queries.GetAttendanceDays;

public sealed record GetAttendanceDaysQuery(string EmployeeCode, DateOnly From, DateOnly To, string? Status)
    : IRequest<ApiResponse<IReadOnlyList<AttendanceDayHistoryDto>>>;

public sealed class GetAttendanceDaysQueryValidator : AbstractValidator<GetAttendanceDaysQuery>
{
    public GetAttendanceDaysQueryValidator()
    {
        RuleFor(x => x.EmployeeCode).NotEmpty();
    }
}

public sealed class GetAttendanceDaysQueryHandler
    : IRequestHandler<GetAttendanceDaysQuery, ApiResponse<IReadOnlyList<AttendanceDayHistoryDto>>>
{
    private readonly IAttendanceCorrectionRepository _attendanceRepo;

    public GetAttendanceDaysQueryHandler(IAttendanceCorrectionRepository attendanceRepo)
    {
        _attendanceRepo = attendanceRepo;
    }

    public async Task<ApiResponse<IReadOnlyList<AttendanceDayHistoryDto>>> Handle(
        GetAttendanceDaysQuery request,
        CancellationToken cancellationToken)
    {
        if (request.From > request.To || request.To.DayNumber - request.From.DayNumber > 366)
        {
            return ApiResponse<IReadOnlyList<AttendanceDayHistoryDto>>.CreateFailure(
                "Attendance history range must be valid and no longer than 367 days.",
                statusCode: (int)HttpStatusCode.BadRequest);
        }

        var start = request.From.ToDateTime(TimeOnly.MinValue);
        var end = request.To.ToDateTime(TimeOnly.MaxValue);
        var days = await _attendanceRepo.GetAttendanceDaysAsync(request.EmployeeCode.Trim(), start, end, cancellationToken);
        var normalizedStatus = string.IsNullOrWhiteSpace(request.Status) ? null : request.Status.Trim().ToUpperInvariant();

        var result = days
            .Where(day => normalizedStatus == null || string.Equals(day.Status, normalizedStatus, StringComparison.OrdinalIgnoreCase))
            .Select(day => new AttendanceDayHistoryDto(
                day.EmployeeCode,
                day.AttendanceDate,
                day.Status,
                day.ShiftCode,
                day.FirstCheckIn,
                day.LastCheckOut,
                day.ScheduledHours,
                day.ActualWorkedHours,
                day.LateArrivalMinutes,
                day.EarlyLeaveMinutes,
                day.OvertimeHours,
                day.HasMissingPunch,
                day.HasGeofenceViolation))
            .ToList();

        return ApiResponse<IReadOnlyList<AttendanceDayHistoryDto>>.CreateSuccess(
            result, "Attendance history retrieved successfully.");
    }
}
