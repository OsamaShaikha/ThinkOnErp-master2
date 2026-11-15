using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using ThinkOnErp.Application.DTOs.Hr;
using ThinkOnErp.Application.Features.Hr.Attendance.Common;
using ThinkOnErp.Domain.Entities.Hr;
using ThinkOnErp.Domain.Interfaces;
using ThinkOnErp.Domain.Interfaces.Hr;

namespace ThinkOnErp.Application.Services.Hr;

[Obsolete("Use the MediatR requests in ThinkOnErp.Application.Features.Hr.Attendance.")]
public sealed class HrAttendanceService : IHrAttendanceService
{
    private readonly IAttendanceCorrectionRepository _attendanceRepo;
    private readonly IEmployeeRepository _employeeRepo;
    private readonly IBranchRepository _branchRepo;
    private readonly IPolicyRepository _policyRepo;
    private readonly IGeoLocationService _geoService;
    private readonly ILogger<HrAttendanceService> _logger;
    private readonly IShiftTimingService? _timing;
    private readonly IRawAttendanceProcessingService? _processing;
    private readonly IAttendanceBranchRepository? _branchAccess;
    private readonly IAttendanceTimeSettingsRepository? _timeSettings;

    public HrAttendanceService(
        IAttendanceCorrectionRepository attendanceRepo,
        IEmployeeRepository employeeRepo,
        IBranchRepository branchRepo,
        IPolicyRepository policyRepo,
        IGeoLocationService geoService,
        ILogger<HrAttendanceService> logger,
        IHrUnitOfWork? unitOfWork = null,
        IShiftTimingService? timing = null,
        IAttendanceTimeSettingsRepository? timeSettings = null,
        IAttendanceDayClassifier? classifier = null,
        IRawAttendanceProcessingService? processing = null,
        IAttendanceBranchRepository? branchAccess = null)
    {
        _attendanceRepo = attendanceRepo;
        _employeeRepo = employeeRepo;
        _branchRepo = branchRepo;
        _policyRepo = policyRepo;
        _geoService = geoService;
        _logger = logger;
        _timing = timing;
        _timeSettings = timeSettings;
        _processing = processing;
        _branchAccess = branchAccess;
    }

    public Task<AttendancePunchResultDto> CheckInAsync(CheckInRequestDto dto, string user, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(dto);
        return MobilePunchProcessor.RecordPunchAsync(
            _attendanceRepo, _employeeRepo, _branchRepo, _policyRepo, _geoService, _logger, _processing, _branchAccess,
            dto.EmployeeCode, dto.Latitude, dto.Longitude, dto.BranchId, dto.AccuracyMeters, dto.DeviceId, dto.Source, dto.EventId, "IN", user, cancellationToken);
    }

    public Task<AttendancePunchResultDto> CheckOutAsync(CheckOutRequestDto dto, string user, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(dto);
        return MobilePunchProcessor.RecordPunchAsync(
            _attendanceRepo, _employeeRepo, _branchRepo, _policyRepo, _geoService, _logger, _processing, _branchAccess,
            dto.EmployeeCode, dto.Latitude, dto.Longitude, dto.BranchId, dto.AccuracyMeters, dto.DeviceId, dto.Source, dto.EventId, "OUT", user, cancellationToken);
    }

    public async Task<TodayAttendanceStatusDto?> GetTodayStatusAsync(
        string employeeCode,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(employeeCode))
            return null;

        var timeSetting = await (_timeSettings ?? throw new InvalidOperationException("Company time settings are required.")).GetForEmployeeAsync(employeeCode, cancellationToken);
        var today = TimeZoneInfo.ConvertTime(DateTimeOffset.UtcNow, ShiftTimeRules.Zone(timeSetting.TimeZoneId)).Date;

        var statusSchedule = await AttendanceWorkDateResolver.ResolveAsync(
            employeeCode, new DateTimeOffset(DateTime.UtcNow), DateOnly.FromDateTime(today), "OUT",
            _timing ?? throw new InvalidOperationException("Configured shift timing is required."), _attendanceRepo, cancellationToken);

        today = statusSchedule.Shift.WorkDate.ToDateTime(TimeOnly.MinValue);
        var day = await _attendanceRepo.GetAttendanceDayAsync(employeeCode, today, cancellationToken);
        if (day == null)
        {
            return new TodayAttendanceStatusDto(
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
            );
        }

        string? checkInBranchName = null;
        if (day.CheckInBranchId.HasValue)
        {
            var b = _branchAccess != null ? await _branchAccess.GetAsync(day.CheckInBranchId.Value, cancellationToken) : await _branchRepo.GetByIdAsync(day.CheckInBranchId.Value);
            checkInBranchName = b?.BranchNameEn ?? b?.BranchNameLocal;
        }

        string? checkOutBranchName = null;
        if (day.CheckOutBranchId.HasValue)
        {
            var b = _branchAccess != null ? await _branchAccess.GetAsync(day.CheckOutBranchId.Value, cancellationToken) : await _branchRepo.GetByIdAsync(day.CheckOutBranchId.Value);
            checkOutBranchName = b?.BranchNameEn ?? b?.BranchNameLocal;
        }

        var isCheckedIn = day.FirstCheckIn.HasValue && !day.LastCheckOut.HasValue;

        return new TodayAttendanceStatusDto(
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
    }

    public async Task<IReadOnlyList<AttendanceDayHistoryDto>> GetAttendanceHistoryAsync(
        string employeeCode, DateOnly fromDate, DateOnly toDate, string? status = null,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(employeeCode)) throw new ArgumentException("Employee code is required.", nameof(employeeCode));
        if (fromDate > toDate || toDate.DayNumber - fromDate.DayNumber > 366)
            throw new ArgumentException("Attendance history range must be valid and no longer than 367 days.");

        var start = fromDate.ToDateTime(TimeOnly.MinValue);
        var end = toDate.ToDateTime(TimeOnly.MaxValue);
        var days = await _attendanceRepo.GetAttendanceDaysAsync(employeeCode.Trim(), start, end, cancellationToken);
        var normalizedStatus = string.IsNullOrWhiteSpace(status) ? null : status.Trim().ToUpperInvariant();
        return days.Where(day => normalizedStatus == null || string.Equals(day.Status, normalizedStatus, StringComparison.OrdinalIgnoreCase))
            .Select(day => new AttendanceDayHistoryDto(
                day.EmployeeCode, day.AttendanceDate, day.Status, day.ShiftCode,
                day.FirstCheckIn, day.LastCheckOut, day.ScheduledHours, day.ActualWorkedHours,
                day.LateArrivalMinutes, day.EarlyLeaveMinutes, day.OvertimeHours,
                day.HasMissingPunch, day.HasGeofenceViolation))
            .ToList();
    }

    public async Task<AttendanceMonthSummaryDto> GetAttendanceMonthSummaryAsync(
        string employeeCode, int year, int month, CancellationToken cancellationToken = default)
    {
        if (year is < 1 or > 9998 || month is < 1 or > 12)
            throw new ArgumentOutOfRangeException(nameof(month), "A valid year and month are required.");
        var from = new DateOnly(year, month, 1);
        var to = from.AddMonths(1).AddDays(-1);
        var days = await GetAttendanceHistoryAsync(employeeCode, from, to, cancellationToken: cancellationToken);
        return new AttendanceMonthSummaryDto(
            employeeCode.Trim(), year, month, days.Count,
            days.Count(day => day.Status.Equals("PRESENT", StringComparison.OrdinalIgnoreCase)),
            days.Count(day => day.Status.Equals("ABSENT", StringComparison.OrdinalIgnoreCase)),
            days.Count(day => day.Status.Equals("ON_LEAVE", StringComparison.OrdinalIgnoreCase)),
            days.Count(day => day.LateArrivalMinutes > 0),
            days.Sum(day => day.LateArrivalMinutes),
            days.Sum(day => day.ActualWorkedHours),
            days.Sum(day => day.OvertimeHours));
    }

    public async Task<IReadOnlyList<NearbyBranchDto>> GetNearbyBranchesAsync(
        decimal latitude,
        decimal longitude,
        long? companyId = null,
        CancellationToken cancellationToken = default)
    {
        if (latitude is < -90m or > 90m || longitude is < -180m or > 180m)
            throw new ArgumentException("Valid coordinates are required.");
        var branches = await (_branchAccess ?? throw new InvalidOperationException("Scoped HR branch access is required.")).ListAsync(companyId, cancellationToken);

        var result = new List<NearbyBranchDto>();

        foreach (var b in branches)
        {
            if (!b.IsActive) continue;

            decimal distance = 0m;
            bool isWithin = false;
            var radius = b.GeofenceRadiusMeters > 0 ? b.GeofenceRadiusMeters : 100m;

            if (b.Latitude.HasValue && b.Longitude.HasValue)
            {
                distance = _geoService.CalculateDistanceMeters(latitude, longitude, b.Latitude.Value, b.Longitude.Value);
                isWithin = distance <= radius;
            }

            result.Add(new NearbyBranchDto(
                BranchId: b.Id,
                BranchNameLocal: b.BranchNameLocal,
                BranchNameEn: b.BranchNameEn,
                Latitude: b.Latitude,
                Longitude: b.Longitude,
                GeofenceRadiusMeters: radius,
                DistanceMeters: distance,
                IsWithinRange: isWithin
            ));
        }

        return result.OrderBy(x => x.DistanceMeters).ToList();
    }
}
