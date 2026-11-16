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

public record RawPunchInputDto(
    string EmployeeCode,
    DateTime PunchTime,
    string PunchType,
    string? DeviceId,
    string? Source
);

public record AttendanceCorrectionRequestDto(
    string EmployeeCode,
    DateTime AttendanceDate,
    DateTime? RequestedCheckIn,
    DateTime? RequestedCheckOut,
    string Reason
);

public record ProcessAttendanceCorrectionDto(
    bool Approved,
    string? RejectionReason
);

public record DailyAttendanceSummaryDto(
    string EmployeeCode,
    string EmployeeName,
    DateTime Date,
    decimal ScheduledHours,
    decimal ActualWorkedHours,
    int LateArrivalMinutes,
    int EarlyLeaveMinutes,
    decimal OvertimeHours,
    string Status,
    bool HasMissingPunch
);

public record AttendancePolicyDto(
    long CompanyId,
    string Code,
    string NameAr,
    string NameEn,
    DateTime EffectiveFrom,
    DateTime? EffectiveTo,
    int GracePeriodMinutes,
    int EarlyLeaveToleranceMinutes,
    int MinMinutesForOvertime,
    bool AutoDeductLateArrival,
    string MissingPunchHandling,
    bool IsDefault
);

public sealed record CreateAttendancePolicyDto(
    string Code, string NameAr, string NameEn, DateTime EffectiveFrom, DateTime? EffectiveTo,
    int GracePeriodMinutes, int EarlyLeaveToleranceMinutes, int MinMinutesForOvertime,
    bool AutoDeductLateArrival, string MissingPunchHandling, bool AllowMultipleSessions,
    bool IsDefault, bool EnforceGeofence, int DefaultAllowedRadiusMeters,
    string GeofenceViolationAction, bool AllowAnyBranchPunch);

public record InitializeAttendanceTimeDto(string TimeZoneId);

public record DeviceAttendanceDto(string EmployeeCode, string OccurredAt, string PunchType, string DeviceId, string EventId, string Source = "BIOMETRIC");

public record ProcessRawBatchDto(int Size = 100);

public record IgnoreRawAttendanceDto(string Reason);
