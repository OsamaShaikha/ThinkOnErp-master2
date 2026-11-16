using System.Text.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using ThinkOnErp.Application.DTOs.Hr;
using ThinkOnErp.Domain.Entities;
using ThinkOnErp.Domain.Entities.Hr;
using ThinkOnErp.Domain.Interfaces;
using ThinkOnErp.Domain.Interfaces.Hr;

namespace ThinkOnErp.Application.Services.Hr;

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
        IHrUnitOfWork? unitOfWork = null, IShiftTimingService? timing = null, IAttendanceTimeSettingsRepository? timeSettings = null, IAttendanceDayClassifier? classifier = null, IRawAttendanceProcessingService? processing = null, IAttendanceBranchRepository? branchAccess = null)
    {
        _attendanceRepo = attendanceRepo;
        _employeeRepo = employeeRepo;
        _branchRepo = branchRepo;
        _policyRepo = policyRepo;
        _geoService = geoService;
        _logger = logger;
        _timing = timing; _timeSettings = timeSettings; _processing = processing; _branchAccess = branchAccess;
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

    public Task<AttendancePunchResultDto> CheckInAsync(CheckInRequestDto dto, string user, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(dto);
        return RecordPunch(dto.EmployeeCode, dto.Latitude, dto.Longitude, dto.BranchId, dto.AccuracyMeters, dto.DeviceId, dto.Source, dto.EventId, "IN", user, cancellationToken);
    }

    public Task<AttendancePunchResultDto> CheckOutAsync(CheckOutRequestDto dto, string user, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(dto);
        return RecordPunch(dto.EmployeeCode, dto.Latitude, dto.Longitude, dto.BranchId, dto.AccuracyMeters, dto.DeviceId, dto.Source, dto.EventId, "OUT", user, cancellationToken);
    }

    private async Task<AttendancePunchResultDto> RecordPunch(string employeeCode, decimal latitude, decimal longitude, long? branchId,
        decimal? accuracy, string? deviceId, string? source, string? eventId, string type, string user, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(employeeCode) || employeeCode.Length > 100 || latitude < -90m || latitude > 90m || longitude < -180m || longitude > 180m ||
            accuracy < 0m || accuracy > 99999.99m || deviceId?.Length > 200 || source?.Length > 100)
            throw new ArgumentException("Valid employee, coordinates, accuracy and source/device field lengths are required.");
        var key = MobilePunchEventKey.Create(eventId, employeeCode, type, latitude, longitude, branchId, accuracy, deviceId, source);
        var employee = await _employeeRepo.GetEmployeeByCodeAsync(employeeCode, ct) ?? throw new KeyNotFoundException("Employee not found in the current HR scope.");
        var replay = await TryReplay(employeeCode, key, ct); if (replay != null) return replay;
        if (!string.IsNullOrWhiteSpace(source) && source.Trim().ToUpperInvariant() != "MOBILE_APP")
            throw new ArgumentException("Mobile punch source must be MOBILE_APP; trusted device/online intake uses its own permission-protected endpoint.");
        var now = DateTime.UtcNow;
        var target = branchId ?? employee.BranchId;
        // Legacy direct service callers can only use the already scoped employee's assigned branch.
        // Production DI supplies the explicit company/branch-scoped repository.
        if (_branchAccess == null && target != employee.BranchId) throw new UnauthorizedAccessException("Scoped branch access is required for a different punch branch.");
        var branch = target.HasValue ? _branchAccess != null ? await _branchAccess.GetAsync(target.Value, ct) : await _branchRepo.GetByIdAsync(target.Value) : null;
        if (target.HasValue && branch == null) throw new KeyNotFoundException("Punch branch not found in the current HR scope.");
        if (branch != null && employee.BranchId.HasValue && branch.Id != employee.BranchId.Value)
        {
            var assignedBranch = await _branchRepo.GetByIdAsync(employee.BranchId.Value);
            if (assignedBranch?.CompanyId != branch.CompanyId) throw new UnauthorizedAccessException("The punch branch is outside the employee company.");
        }
        var policy = await ResolvePolicyAsync(branch?.CompanyId, now, ct);
        var geoAction = policy.GeofenceViolationAction?.Trim().ToUpperInvariant();
        GeofenceCheckResult location;
        string? rejected;
        if (branch == null || !branch.IsActive || !branch.CompanyId.HasValue)
        {
            location = new GeofenceCheckResult { Status = "NO_ACTIVE_BRANCH", BranchId = null, Message = "No active authorized branch is configured for this punch." };
            rejected = location.Message;
        }
        else
        {
            location = _geoService.CheckProximity(latitude, longitude, branch, branch.GeofenceRadiusMeters > 0m ? null : policy.DefaultAllowedRadiusMeters);
            if (!policy.EnforceGeofence) location = new GeofenceCheckResult { IsWithinRange = true, BranchId = branch.Id,
                BranchName = location.BranchName, DistanceMeters = location.DistanceMeters, AllowedRadiusMeters = location.AllowedRadiusMeters,
                Status = "GEOFENCE_DISABLED", Message = "Company policy disables geofence enforcement." };
            rejected = geoAction is not ("REJECT" or "FLAG_AND_ALLOW") ? "Geofence policy action is invalid; review company settings." : !policy.AllowAnyBranchPunch && employee.BranchId != branch.Id ? "Attendance policy does not allow a different branch." :
                policy.EnforceGeofence && branch.EnforceGeofence && !location.IsWithinRange && geoAction == "REJECT"
                    ? string.IsNullOrWhiteSpace(location.Message) ? "Punch rejected by the configured geofence policy." : location.Message : null;
        }
        var raw = new RawAttendance
        {
            EmployeeCode = employeeCode, PunchTime = now, PunchType = type, DeviceId = deviceId,
            Source = string.IsNullOrWhiteSpace(source) ? "MOBILE_APP" : source.Trim().ToUpperInvariant(), EventId = key.Id, EventPayloadHash = key.PayloadHash,
            BranchId = branch?.IsActive == true ? branch.Id : null, Latitude = latitude, Longitude = longitude, AccuracyMeters = accuracy,
            DistanceToBranchMeters = location.DistanceMeters, IsWithinGeofence = location.Status == "GEOFENCE_DISABLED" ? null : location.IsWithinRange, GeofenceStatus = location.Status,
            IsProcessed = rejected != null, ProcessingOutcome = rejected == null ? null : "REJECTED",
            ProcessingReason = rejected == null ? null : rejected.Length <= 1000 ? rejected : rejected[..1000],
            ProcessedBy = rejected == null ? null : user, ProcessedDate = rejected == null ? null : now, CreationUser = user, CreationDate = now
        };
        string Receipt(long id) => JsonSerializer.Serialize(new AttendancePunchResultDto(rejected == null, employeeCode, type, now, raw.BranchId,
            branch?.BranchNameEn ?? branch?.BranchNameLocal ?? string.Empty, latitude, longitude, location.DistanceMeters, location.AllowedRadiusMeters,
            location.IsWithinRange, location.Status, rejected ?? "Attendance evidence saved. Check day status for its financial processing outcome.", id, rejected == null));
        var stored = await _attendanceRepo.AcceptMobileEvidenceAsync(raw, Receipt, ct);
        var result = JsonSerializer.Deserialize<AttendancePunchResultDto>(stored.EventResponseJson ?? throw new InvalidOperationException("Evidence receipt was not saved."))
            ?? throw new InvalidOperationException("Evidence receipt could not be read.");
        if (raw.Id > 0 && stored.Id == raw.Id && !stored.IsProcessed && _processing != null)
        {
            try { await _processing.ProcessAsync(stored.Id, user, ct); }
            catch (OperationCanceledException) { throw; }
            catch (Exception ex)
            {
                _logger.LogWarning("Attendance evidence {RawId} was saved; financial processing deferred ({ErrorType}).", stored.Id, ex.GetType().Name);
                var reason = ex is InvalidOperationException or ArgumentException or KeyNotFoundException ? ex.Message : "Financial processing failed; review the evidence queue.";
                try { await _attendanceRepo.RecordRawAttemptAsync(stored.Id, user, reason, ct); }
                catch (OperationCanceledException) { throw; }
                catch (Exception recordingError) { _logger.LogWarning("Evidence {RawId} is durable but its attempt note could not be saved ({ErrorType}).", stored.Id, recordingError.GetType().Name); }
            }
        }
        return result;
    }

    public async Task<TodayAttendanceStatusDto?> GetTodayStatusAsync(
        string employeeCode,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(employeeCode))
            return null;

        var timeSetting = await (_timeSettings ?? throw new InvalidOperationException("Company time settings are required.")).GetForEmployeeAsync(employeeCode, cancellationToken);
        var today = TimeZoneInfo.ConvertTime(DateTimeOffset.UtcNow, ShiftTimeRules.Zone(timeSetting.TimeZoneId)).Date;
        var statusSchedule = await ResolvePunchSchedule(employeeCode, DateTime.UtcNow, DateOnly.FromDateTime(today), "OUT", cancellationToken);
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

    public async Task<IReadOnlyList<NearbyBranchDto>> GetNearbyBranchesAsync(
        decimal latitude,
        decimal longitude,
        long? companyId = null,
        CancellationToken cancellationToken = default)
    {
        if (latitude is < -90m or > 90m || longitude is < -180m or > 180m) throw new ArgumentException("Valid coordinates are required.");
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

    private async Task<AttendancePunchResultDto?> TryReplay(string employeeCode, MobilePunchEventKey key, CancellationToken ct)
    {
        var raw = await _attendanceRepo.GetMobileEventAsync(employeeCode, key.Id, ct);
        if (raw == null) return null;
        if (raw.EventPayloadHash != key.PayloadHash) throw new MobilePunchEventConflictException();
        if (string.IsNullOrEmpty(raw.EventResponseJson)) throw new InvalidOperationException("Accepted event response is missing; review the stored evidence.");
        return JsonSerializer.Deserialize<AttendancePunchResultDto>(raw.EventResponseJson)
            ?? throw new InvalidOperationException("Accepted event response could not be read.");
    }

    private Task<TimedShiftDto> ResolvePunchSchedule(string employeeCode, DateTime now, DateOnly localDate, string type, CancellationToken ct)
        => AttendanceWorkDateResolver.ResolveAsync(employeeCode, new DateTimeOffset(now), localDate, type,
            _timing ?? throw new InvalidOperationException("Configured shift timing is required."), _attendanceRepo, ct);

    private async Task<AttendancePolicy> ResolvePolicyAsync(long? companyId, DateTime date, CancellationToken cancellationToken)
    {
        if (companyId.HasValue)
        {
            var policy = await _policyRepo.GetActiveAttendancePolicyAsync(companyId.Value, date, cancellationToken);
            if (policy != null) return policy;
        }

        // Return sensible default policy
        return new AttendancePolicy
        {
            EnforceGeofence = true,
            DefaultAllowedRadiusMeters = 100,
            GeofenceViolationAction = "REJECT",
            AllowAnyBranchPunch = false
        };
    }
}
