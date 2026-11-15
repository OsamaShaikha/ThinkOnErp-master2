using System;

namespace ThinkOnErp.Application.DTOs.Hr;

public record CheckInRequestDto(
    string EmployeeCode,
    [property: global::System.Text.Json.Serialization.JsonRequired] decimal Latitude,
    [property: global::System.Text.Json.Serialization.JsonRequired] decimal Longitude,
    long? BranchId = null,
    decimal? AccuracyMeters = null,
    string? DeviceId = null,
    string Source = "MOBILE_APP",
    [param: global::System.ComponentModel.DataAnnotations.Required] string? EventId = null
);

public record CheckOutRequestDto(
    string EmployeeCode,
    [property: global::System.Text.Json.Serialization.JsonRequired] decimal Latitude,
    [property: global::System.Text.Json.Serialization.JsonRequired] decimal Longitude,
    long? BranchId = null,
    decimal? AccuracyMeters = null,
    string? DeviceId = null,
    string Source = "MOBILE_APP",
    [param: global::System.ComponentModel.DataAnnotations.Required] string? EventId = null
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
    string Message,
    long? RawEventId = null,
    bool EvidenceOnly = false
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

public record AttendanceDayHistoryDto(
    string EmployeeCode,
    DateTime AttendanceDate,
    string Status,
    string? ShiftCode,
    DateTime? FirstCheckIn,
    DateTime? LastCheckOut,
    decimal ScheduledHours,
    decimal ActualWorkedHours,
    int LateArrivalMinutes,
    int EarlyLeaveMinutes,
    decimal OvertimeHours,
    bool HasMissingPunch,
    bool HasGeofenceViolation);

public record AttendanceMonthSummaryDto(
    string EmployeeCode,
    int Year,
    int Month,
    int RecordedDays,
    int PresentDays,
    int AbsentDays,
    int LeaveDays,
    int LateDays,
    int TotalLateMinutes,
    decimal TotalWorkedHours,
    decimal TotalOvertimeHours);
