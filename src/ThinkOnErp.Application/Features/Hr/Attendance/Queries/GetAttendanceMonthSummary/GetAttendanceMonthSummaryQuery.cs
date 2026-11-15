using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using System.Net;
using ThinkOnErp.Application.Common;
using ThinkOnErp.Application.DTOs.Hr;
using ThinkOnErp.Application.Features.Hr.Attendance.Queries.GetAttendanceDays;

namespace ThinkOnErp.Application.Features.Hr.Attendance.Queries.GetAttendanceMonthSummary;

public sealed record GetAttendanceMonthSummaryQuery(string EmployeeCode, int Year, int Month)
    : IRequest<ApiResponse<AttendanceMonthSummaryDto>>;

public sealed class GetAttendanceMonthSummaryQueryHandler
    : IRequestHandler<GetAttendanceMonthSummaryQuery, ApiResponse<AttendanceMonthSummaryDto>>
{
    private readonly IMediator _mediator;

    public GetAttendanceMonthSummaryQueryHandler(IMediator mediator)
    {
        _mediator = mediator;
    }

    public async Task<ApiResponse<AttendanceMonthSummaryDto>> Handle(
        GetAttendanceMonthSummaryQuery request,
        CancellationToken cancellationToken)
    {
        if (request.Year is < 1 or > 9998 || request.Month is < 1 or > 12)
        {
            return ApiResponse<AttendanceMonthSummaryDto>.CreateFailure(
                "A valid year and month are required.", statusCode: (int)HttpStatusCode.BadRequest);
        }

        var from = new DateOnly(request.Year, request.Month, 1);
        var to = from.AddMonths(1).AddDays(-1);

        var daysResponse = await _mediator.Send(
            new GetAttendanceDaysQuery(request.EmployeeCode, from, to, null), cancellationToken);

        if (!daysResponse.Success || daysResponse.Data == null)
        {
            return ApiResponse<AttendanceMonthSummaryDto>.CreateFailure(
                daysResponse.Message, statusCode: daysResponse.StatusCode);
        }

        var days = daysResponse.Data;
        var result = new AttendanceMonthSummaryDto(
            request.EmployeeCode.Trim(),
            request.Year,
            request.Month,
            days.Count,
            days.Count(day => day.Status.Equals("PRESENT", StringComparison.OrdinalIgnoreCase)),
            days.Count(day => day.Status.Equals("ABSENT", StringComparison.OrdinalIgnoreCase)),
            days.Count(day => day.Status.Equals("ON_LEAVE", StringComparison.OrdinalIgnoreCase)),
            days.Count(day => day.LateArrivalMinutes > 0),
            days.Sum(day => day.LateArrivalMinutes),
            days.Sum(day => day.ActualWorkedHours),
            days.Sum(day => day.OvertimeHours));

        return ApiResponse<AttendanceMonthSummaryDto>.CreateSuccess(
            result, "Attendance month summary retrieved successfully.");
    }
}
