using System;

namespace ThinkOnErp.Domain.Entities.Hr;

public sealed class AttendancePolicy
{
    public long Id { get; set; }
    public long CompanyId { get; set; }
    public string Code { get; set; } = string.Empty;
    public string NameAr { get; set; } = string.Empty;
    public string NameEn { get; set; } = string.Empty;
    public DateTime EffectiveFrom { get; set; }
    public DateTime? EffectiveTo { get; set; }
    public int GracePeriodMinutes { get; set; } = 15;
    public int EarlyLeaveToleranceMinutes { get; set; } = 10;
    public int MinMinutesForOvertime { get; set; } = 30;
    public bool AutoDeductLateArrival { get; set; } = true;
    public string MissingPunchHandling { get; set; } = "PENALIZE_HALF_DAY"; // PENALIZE_HALF_DAY, PENALIZE_FULL_DAY, FLAG_ONLY
    public bool IsDefault { get; set; }
    public bool IsActive { get; set; } = true;

    public string CreationUser { get; set; } = string.Empty;
    public DateTime CreationDate { get; set; } = DateTime.UtcNow;
    public string? UpdateUser { get; set; }
    public DateTime? UpdateDate { get; set; }
}

public sealed class RawAttendance
{
    public long Id { get; set; }
    public string EmployeeCode { get; set; } = string.Empty;
    public DateTime PunchTime { get; set; }
    public string PunchType { get; set; } = "IN"; // IN, OUT
    public string? DeviceId { get; set; }
    public string? ExternalReference { get; set; } // Can store PunchHash (SHA256)
    public string Source { get; set; } = "BIOMETRIC"; // BIOMETRIC, MANUAL, MOBILE_APP
    public bool IsProcessed { get; set; }
    public DateTime? ProcessedDate { get; set; }

    public string CreationUser { get; set; } = string.Empty;
    public DateTime CreationDate { get; set; } = DateTime.UtcNow;
}

public sealed class AttendanceDay
{
    public long Id { get; set; }
    public string EmployeeCode { get; set; } = string.Empty;
    public DateTime AttendanceDate { get; set; }
    public long? WorkCalendarId { get; set; }
    public string? ShiftCode { get; set; }
    public decimal ScheduledHours { get; set; } = 8.00m;
    public DateTime? FirstCheckIn { get; set; }
    public DateTime? LastCheckOut { get; set; }
    public decimal ActualWorkedHours { get; set; }
    public int LateArrivalMinutes { get; set; }
    public int EarlyLeaveMinutes { get; set; }
    public decimal OvertimeHours { get; set; }
    public string Status { get; set; } = "PRESENT"; // PRESENT, ABSENT, REST_DAY, HOLIDAY, ON_LEAVE
    public bool HasMissingPunch { get; set; }
    public string? LeaveTypeCode { get; set; }
    public string? Notes { get; set; }

    public string CreationUser { get; set; } = string.Empty;
    public DateTime CreationDate { get; set; } = DateTime.UtcNow;
    public string? UpdateUser { get; set; }
    public DateTime? UpdateDate { get; set; }
}

public sealed class AttendanceCorrectionRequest
{
    public long Id { get; set; }
    public string EmployeeCode { get; set; } = string.Empty;
    public DateTime AttendanceDate { get; set; }
    public DateTime? OldCheckIn { get; set; }
    public DateTime? OldCheckOut { get; set; }
    public DateTime? RequestedCheckIn { get; set; }
    public DateTime? RequestedCheckOut { get; set; }
    public string Reason { get; set; } = string.Empty;
    public string Status { get; set; } = "PENDING"; // PENDING, APPROVED, REJECTED
    public string? ApprovedBy { get; set; }
    public DateTime? ApprovalDate { get; set; }
    public string? RejectionReason { get; set; }

    public string CreationUser { get; set; } = string.Empty;
    public DateTime CreationDate { get; set; } = DateTime.UtcNow;
    public string? UpdateUser { get; set; }
    public DateTime? UpdateDate { get; set; }
}

public sealed class ShiftSchedule
{
    public string ShiftCode { get; set; } = string.Empty;
    public string NameLocal { get; set; } = string.Empty;
    public string NameEn { get; set; } = string.Empty;
    public TimeSpan StartTime { get; set; }
    public TimeSpan EndTime { get; set; }
    public int BreakMinutes { get; set; } = 60;
    public string WorkingDaysJson { get; set; } = "[0,1,2,3,4]"; // Days of week
    public bool IsActive { get; set; } = true;

    public string CreationUser { get; set; } = string.Empty;
    public DateTime CreationDate { get; set; } = DateTime.UtcNow;
    public string? UpdateUser { get; set; }
    public DateTime? UpdateDate { get; set; }
}

public sealed class EmployeeShiftAssignment
{
    public long Id { get; set; }
    public string EmployeeCode { get; set; } = string.Empty;
    public string ShiftCode { get; set; } = string.Empty;
    public DateTime EffectiveFrom { get; set; }
    public DateTime? EffectiveTo { get; set; }
    public bool IsActive { get; set; } = true;

    public string CreationUser { get; set; } = string.Empty;
    public DateTime CreationDate { get; set; } = DateTime.UtcNow;
    public string? UpdateUser { get; set; }
    public DateTime? UpdateDate { get; set; }
}
