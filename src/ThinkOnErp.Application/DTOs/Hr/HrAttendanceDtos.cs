using System;

namespace ThinkOnErp.Application.DTOs.Hr;

public record CheckInRequestDto(
    string EmployeeCode,
    decimal Latitude,
    decimal Longitude,
    long? BranchId = null,
    decimal? AccuracyMeters = null,
    string? DeviceId = null,
    string Source = "MOBILE_APP"
);

public record CheckOutRequestDto(
    string EmployeeCode,
    decimal Latitude,
    decimal Longitude,
    long? BranchId = null,
    decimal? AccuracyMeters = null,
    string? DeviceId = null,
    string Source = "MOBILE_APP"
);

public record AttendancePunchResultDto(
    bool Success,
    string EmployeeCode,
    string PunchType,
    DateTime PunchTime,
    long? BranchId,
    string BranchName,
    decimal? Latitude,
    decimal? Longitude,
    decimal DistanceMeters,
    decimal AllowedRadiusMeters,
    bool IsWithinGeofence,
    string GeofenceStatus,
    string Message
);

public record NearbyBranchDto(
    long BranchId,
    string BranchNameLocal,
    string BranchNameEn,
    decimal? Latitude,
    decimal? Longitude,
    decimal GeofenceRadiusMeters,
    decimal DistanceMeters,
    bool IsWithinRange
);

public record TodayAttendanceStatusDto(
    string EmployeeCode,
    DateTime AttendanceDate,
    string Status,
    DateTime? FirstCheckIn,
    DateTime? LastCheckOut,
    decimal ActualWorkedHours,
    long? CheckInBranchId,
    string? CheckInBranchName,
    bool? IsCheckInWithinGeofence,
    long? CheckOutBranchId,
    string? CheckOutBranchName,
    bool? IsCheckOutWithinGeofence,
    bool IsCurrentlyCheckedIn,
    bool HasGeofenceViolation
);
