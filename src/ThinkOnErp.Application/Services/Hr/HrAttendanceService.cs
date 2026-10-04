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

    public HrAttendanceService(
        IAttendanceCorrectionRepository attendanceRepo,
        IEmployeeRepository employeeRepo,
        IBranchRepository branchRepo,
        IPolicyRepository policyRepo,
        IGeoLocationService geoService,
        ILogger<HrAttendanceService> logger)
    {
        _attendanceRepo = attendanceRepo;
        _employeeRepo = employeeRepo;
        _branchRepo = branchRepo;
        _policyRepo = policyRepo;
        _geoService = geoService;
        _logger = logger;
    }

    public async Task<AttendancePunchResultDto> CheckInAsync(
        CheckInRequestDto dto,
        string user,
        CancellationToken cancellationToken = default)
    {
        if (dto == null) throw new ArgumentNullException(nameof(dto));
        if (string.IsNullOrWhiteSpace(dto.EmployeeCode))
            throw new ArgumentException("Employee code is required.", nameof(dto.EmployeeCode));

        var now = DateTime.UtcNow;
        var employee = await _employeeRepo.GetEmployeeByCodeAsync(dto.EmployeeCode, cancellationToken);
        if (employee == null)
            throw new KeyNotFoundException($"Employee with code '{dto.EmployeeCode}' not found.");

        var branch = await ResolveBranchAsync(dto.BranchId, employee.BranchId, cancellationToken);
        var policy = await ResolvePolicyAsync(branch.CompanyId, now, cancellationToken);

        var proximity = _geoService.CheckProximity(dto.Latitude, dto.Longitude, branch, policy.DefaultAllowedRadiusMeters);

        // Check if strict rejection applies
        var shouldBlock = policy.EnforceGeofence &&
                          branch.EnforceGeofence &&
                          !proximity.IsWithinRange &&
                          string.Equals(policy.GeofenceViolationAction, "REJECT", StringComparison.OrdinalIgnoreCase);

        if (shouldBlock)
        {
            _logger.LogWarning(
                "Check-in rejected for employee {EmployeeCode}: outside branch {BranchId} range ({Distance}m > {Radius}m)",
                dto.EmployeeCode, branch.Id, proximity.DistanceMeters, proximity.AllowedRadiusMeters);

            return new AttendancePunchResultDto(
                Success: false,
                EmployeeCode: dto.EmployeeCode,
                PunchType: "IN",
                PunchTime: now,
                BranchId: branch.Id,
                BranchName: proximity.BranchName,
                Latitude: dto.Latitude,
                Longitude: dto.Longitude,
                DistanceMeters: proximity.DistanceMeters,
                AllowedRadiusMeters: proximity.AllowedRadiusMeters,
                IsWithinGeofence: false,
                GeofenceStatus: proximity.Status,
                Message: proximity.Message
            );
        }

        // Persist raw punch
        var raw = new RawAttendance
        {
            EmployeeCode = dto.EmployeeCode,
            PunchTime = now,
            PunchType = "IN",
            DeviceId = dto.DeviceId,
            Source = !string.IsNullOrWhiteSpace(dto.Source) ? dto.Source : "MOBILE_APP",
            BranchId = branch.Id,
            Latitude = dto.Latitude,
            Longitude = dto.Longitude,
            AccuracyMeters = dto.AccuracyMeters,
            DistanceToBranchMeters = proximity.DistanceMeters,
            IsWithinGeofence = proximity.IsWithinRange,
            GeofenceStatus = proximity.Status,
            IsProcessed = true,
            ProcessedDate = now,
            CreationUser = user,
            CreationDate = now
        };
        await _attendanceRepo.AddRawAttendanceAsync(raw, cancellationToken);

        // Update daily record
        var day = await _attendanceRepo.GetAttendanceDayAsync(dto.EmployeeCode, now.Date, cancellationToken);
        if (day == null)
        {
            day = new AttendanceDay
            {
                EmployeeCode = dto.EmployeeCode,
                AttendanceDate = now.Date,
                FirstCheckIn = now,
                CheckInBranchId = branch.Id,
                CheckInLatitude = dto.Latitude,
                CheckInLongitude = dto.Longitude,
                CheckInDistanceMeters = proximity.DistanceMeters,
                IsCheckInWithinGeofence = proximity.IsWithinRange,
                HasGeofenceViolation = !proximity.IsWithinRange,
                Status = "PRESENT",
                CreationUser = user,
                CreationDate = now
            };
            await _attendanceRepo.AddAttendanceDayAsync(day, cancellationToken);
        }
        else
        {
            if (!day.FirstCheckIn.HasValue)
            {
                day.FirstCheckIn = now;
                day.CheckInBranchId = branch.Id;
                day.CheckInLatitude = dto.Latitude;
                day.CheckInLongitude = dto.Longitude;
                day.CheckInDistanceMeters = proximity.DistanceMeters;
                day.IsCheckInWithinGeofence = proximity.IsWithinRange;
            }

            if (!proximity.IsWithinRange)
            {
                day.HasGeofenceViolation = true;
            }

            day.Status = "PRESENT";
            day.UpdateUser = user;
            day.UpdateDate = now;
            await _attendanceRepo.UpdateAttendanceDayAsync(day, cancellationToken);
        }

        await _attendanceRepo.SaveChangesAsync(cancellationToken);

        return new AttendancePunchResultDto(
            Success: true,
            EmployeeCode: dto.EmployeeCode,
            PunchType: "IN",
            PunchTime: now,
            BranchId: branch.Id,
            BranchName: proximity.BranchName,
            Latitude: dto.Latitude,
            Longitude: dto.Longitude,
            DistanceMeters: proximity.DistanceMeters,
            AllowedRadiusMeters: proximity.AllowedRadiusMeters,
            IsWithinGeofence: proximity.IsWithinRange,
            GeofenceStatus: proximity.Status,
            Message: proximity.IsWithinRange
                ? $"Check-in successful at branch '{proximity.BranchName}'."
                : $"Check-in registered with geofence warning ({proximity.DistanceMeters:F1}m from branch)."
        );
    }

    public async Task<AttendancePunchResultDto> CheckOutAsync(
        CheckOutRequestDto dto,
        string user,
        CancellationToken cancellationToken = default)
    {
        if (dto == null) throw new ArgumentNullException(nameof(dto));
        if (string.IsNullOrWhiteSpace(dto.EmployeeCode))
            throw new ArgumentException("Employee code is required.", nameof(dto.EmployeeCode));

        var now = DateTime.UtcNow;
        var employee = await _employeeRepo.GetEmployeeByCodeAsync(dto.EmployeeCode, cancellationToken);
        if (employee == null)
            throw new KeyNotFoundException($"Employee with code '{dto.EmployeeCode}' not found.");

        var branch = await ResolveBranchAsync(dto.BranchId, employee.BranchId, cancellationToken);
        var policy = await ResolvePolicyAsync(branch.CompanyId, now, cancellationToken);

        var proximity = _geoService.CheckProximity(dto.Latitude, dto.Longitude, branch, policy.DefaultAllowedRadiusMeters);

        // Check if strict rejection applies
        var shouldBlock = policy.EnforceGeofence &&
                          branch.EnforceGeofence &&
                          !proximity.IsWithinRange &&
                          string.Equals(policy.GeofenceViolationAction, "REJECT", StringComparison.OrdinalIgnoreCase);

        if (shouldBlock)
        {
            _logger.LogWarning(
                "Check-out rejected for employee {EmployeeCode}: outside branch {BranchId} range ({Distance}m > {Radius}m)",
                dto.EmployeeCode, branch.Id, proximity.DistanceMeters, proximity.AllowedRadiusMeters);

            return new AttendancePunchResultDto(
                Success: false,
                EmployeeCode: dto.EmployeeCode,
                PunchType: "OUT",
                PunchTime: now,
                BranchId: branch.Id,
                BranchName: proximity.BranchName,
                Latitude: dto.Latitude,
                Longitude: dto.Longitude,
                DistanceMeters: proximity.DistanceMeters,
                AllowedRadiusMeters: proximity.AllowedRadiusMeters,
                IsWithinGeofence: false,
                GeofenceStatus: proximity.Status,
                Message: proximity.Message
            );
        }

        // Persist raw punch
        var raw = new RawAttendance
        {
            EmployeeCode = dto.EmployeeCode,
            PunchTime = now,
            PunchType = "OUT",
            DeviceId = dto.DeviceId,
            Source = !string.IsNullOrWhiteSpace(dto.Source) ? dto.Source : "MOBILE_APP",
            BranchId = branch.Id,
            Latitude = dto.Latitude,
            Longitude = dto.Longitude,
            AccuracyMeters = dto.AccuracyMeters,
            DistanceToBranchMeters = proximity.DistanceMeters,
            IsWithinGeofence = proximity.IsWithinRange,
            GeofenceStatus = proximity.Status,
            IsProcessed = true,
            ProcessedDate = now,
            CreationUser = user,
            CreationDate = now
        };
        await _attendanceRepo.AddRawAttendanceAsync(raw, cancellationToken);

        // Update daily record
        var day = await _attendanceRepo.GetAttendanceDayAsync(dto.EmployeeCode, now.Date, cancellationToken);
        if (day == null)
        {
            day = new AttendanceDay
            {
                EmployeeCode = dto.EmployeeCode,
                AttendanceDate = now.Date,
                LastCheckOut = now,
                CheckOutBranchId = branch.Id,
                CheckOutLatitude = dto.Latitude,
                CheckOutLongitude = dto.Longitude,
                CheckOutDistanceMeters = proximity.DistanceMeters,
                IsCheckOutWithinGeofence = proximity.IsWithinRange,
                HasGeofenceViolation = !proximity.IsWithinRange,
                HasMissingPunch = true, // Check-out without check-in
                Status = "PRESENT",
                CreationUser = user,
                CreationDate = now
            };
            await _attendanceRepo.AddAttendanceDayAsync(day, cancellationToken);
        }
        else
        {
            day.LastCheckOut = now;
            day.CheckOutBranchId = branch.Id;
            day.CheckOutLatitude = dto.Latitude;
            day.CheckOutLongitude = dto.Longitude;
            day.CheckOutDistanceMeters = proximity.DistanceMeters;
            day.IsCheckOutWithinGeofence = proximity.IsWithinRange;

            if (!proximity.IsWithinRange)
            {
                day.HasGeofenceViolation = true;
            }

            if (day.FirstCheckIn.HasValue)
            {
                var diff = now - day.FirstCheckIn.Value;
                day.ActualWorkedHours = Math.Max(0m, Math.Round((decimal)diff.TotalHours, 2));
                day.HasMissingPunch = false;
            }

            day.Status = "PRESENT";
            day.UpdateUser = user;
            day.UpdateDate = now;
            await _attendanceRepo.UpdateAttendanceDayAsync(day, cancellationToken);
        }

        await _attendanceRepo.SaveChangesAsync(cancellationToken);

        return new AttendancePunchResultDto(
            Success: true,
            EmployeeCode: dto.EmployeeCode,
            PunchType: "OUT",
            PunchTime: now,
            BranchId: branch.Id,
            BranchName: proximity.BranchName,
            Latitude: dto.Latitude,
            Longitude: dto.Longitude,
            DistanceMeters: proximity.DistanceMeters,
            AllowedRadiusMeters: proximity.AllowedRadiusMeters,
            IsWithinGeofence: proximity.IsWithinRange,
            GeofenceStatus: proximity.Status,
            Message: proximity.IsWithinRange
                ? $"Check-out successful at branch '{proximity.BranchName}'."
                : $"Check-out registered with geofence warning ({proximity.DistanceMeters:F1}m from branch)."
        );
    }

    public async Task<TodayAttendanceStatusDto?> GetTodayStatusAsync(
        string employeeCode,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(employeeCode))
            return null;

        var today = DateTime.UtcNow.Date;
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
            var b = await _branchRepo.GetByIdAsync(day.CheckInBranchId.Value);
            checkInBranchName = b?.BranchNameEn ?? b?.BranchNameLocal;
        }

        string? checkOutBranchName = null;
        if (day.CheckOutBranchId.HasValue)
        {
            var b = await _branchRepo.GetByIdAsync(day.CheckOutBranchId.Value);
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
        var branches = companyId.HasValue
            ? await _branchRepo.GetByCompanyIdAsync(companyId.Value)
            : await _branchRepo.GetAllAsync();

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

    private async Task<SysBranch> ResolveBranchAsync(long? requestedBranchId, long? employeeBranchId, CancellationToken cancellationToken)
    {
        var targetId = requestedBranchId ?? employeeBranchId;
        if (!targetId.HasValue)
        {
            throw new InvalidOperationException("No branch specified and employee is not assigned to a default branch.");
        }

        var branch = await _branchRepo.GetByIdAsync(targetId.Value);
        if (branch == null)
        {
            throw new KeyNotFoundException($"Branch with ID {targetId.Value} not found.");
        }

        return branch;
    }

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
