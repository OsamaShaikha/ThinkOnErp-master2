using System;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using System.Net;
using ThinkOnErp.Application.Common;
using ThinkOnErp.Application.DTOs.Hr;
using ThinkOnErp.Application.Services.Hr;
using ThinkOnErp.Domain.Entities.Hr;
using ThinkOnErp.Domain.Interfaces;
using ThinkOnErp.Domain.Interfaces.Hr;

namespace ThinkOnErp.Application.Features.Hr.Attendance.Queries.GetTodayAttendanceStatus;

public sealed record GetTodayAttendanceStatusQuery(string EmployeeCode)
    : IRequest<ApiResponse<TodayAttendanceStatusDto>>;

public sealed class GetTodayAttendanceStatusQueryHandler
    : IRequestHandler<GetTodayAttendanceStatusQuery, ApiResponse<TodayAttendanceStatusDto>>
{
    private readonly IAttendanceCorrectionRepository _attendanceRepo;
    private readonly IAttendanceTimeSettingsRepository _timeSettings;
    private readonly IShiftTimingService _timing;
    private readonly IBranchRepository _branchRepo;
    private readonly IAttendanceBranchRepository? _branchAccess;

    public GetTodayAttendanceStatusQueryHandler(
        IAttendanceCorrectionRepository attendanceRepo,
        IAttendanceTimeSettingsRepository timeSettings,
        IShiftTimingService timing,
        IBranchRepository branchRepo,
        IAttendanceBranchRepository? branchAccess = null)
    {
        _attendanceRepo = attendanceRepo;
        _timeSettings = timeSettings;
        _timing = timing;
        _branchRepo = branchRepo;
        _branchAccess = branchAccess;
    }

    public async Task<ApiResponse<TodayAttendanceStatusDto>> Handle(
        GetTodayAttendanceStatusQuery request,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.EmployeeCode))
        {
            return ApiResponse<TodayAttendanceStatusDto>.CreateFailure(
                "Employee attendance record not found.", statusCode: (int)HttpStatusCode.NotFound);
        }

        var employeeCode = request.EmployeeCode;
        var timeSetting = await _timeSettings.GetForEmployeeAsync(employeeCode, cancellationToken);
        var today = TimeZoneInfo.ConvertTime(DateTimeOffset.UtcNow, ShiftTimeRules.Zone(timeSetting.TimeZoneId)).Date;

        var statusSchedule = await AttendanceWorkDateResolver.ResolveAsync(
            employeeCode,
            new DateTimeOffset(DateTime.UtcNow),
            DateOnly.FromDateTime(today),
            "OUT",
            _timing,
            _attendanceRepo,
            cancellationToken);

        today = statusSchedule.Shift.WorkDate.ToDateTime(TimeOnly.MinValue);
        var day = await _attendanceRepo.GetAttendanceDayAsync(employeeCode, today, cancellationToken);

        if (day == null)
        {
            return ApiResponse<TodayAttendanceStatusDto>.CreateSuccess(new TodayAttendanceStatusDto(
                EmployeeCode: employeeCode,
                AttendanceDate: today,
                Status: "NOT_CHECKED_IN",
                FirstCheckIn: null,
                LastCheckOut: null,
                ActualWorkedHours: 0m,
                CheckInBranchId: null,
                CheckInBranchName: null,
                IsCheckInWithinGeofence: null,
                CheckOutBranchId: null,
                CheckOutBranchName: null,
                IsCheckOutWithinGeofence: null,
                IsCurrentlyCheckedIn: false,
                HasGeofenceViolation: false
            ));
        }

        string? checkInBranchName = null;
        if (day.CheckInBranchId.HasValue)
        {
            var b = _branchAccess != null
                ? await _branchAccess.GetAsync(day.CheckInBranchId.Value, cancellationToken)
                : await _branchRepo.GetByIdAsync(day.CheckInBranchId.Value);
            checkInBranchName = b?.BranchNameEn ?? b?.BranchNameLocal;
        }

        string? checkOutBranchName = null;
        if (day.CheckOutBranchId.HasValue)
        {
            var b = _branchAccess != null
                ? await _branchAccess.GetAsync(day.CheckOutBranchId.Value, cancellationToken)
                : await _branchRepo.GetByIdAsync(day.CheckOutBranchId.Value);
            checkOutBranchName = b?.BranchNameEn ?? b?.BranchNameLocal;
        }

        var isCheckedIn = day.FirstCheckIn.HasValue && !day.LastCheckOut.HasValue;

        var result = new TodayAttendanceStatusDto(
            EmployeeCode: day.EmployeeCode,
            AttendanceDate: day.AttendanceDate,
            Status: day.Status,
            FirstCheckIn: day.FirstCheckIn,
            LastCheckOut: day.LastCheckOut,
            ActualWorkedHours: day.ActualWorkedHours,
            CheckInBranchId: day.CheckInBranchId,
            CheckInBranchName: checkInBranchName,
            IsCheckInWithinGeofence: day.IsCheckInWithinGeofence,
            CheckOutBranchId: day.CheckOutBranchId,
            CheckOutBranchName: checkOutBranchName,
            IsCheckOutWithinGeofence: day.IsCheckOutWithinGeofence,
            IsCurrentlyCheckedIn: isCheckedIn,
            HasGeofenceViolation: day.HasGeofenceViolation
        );

        return ApiResponse<TodayAttendanceStatusDto>.CreateSuccess(result);
    }
}
