using System;

namespace ThinkOnErp.Application.DTOs.Hr;

public sealed class AttendanceCorrectionRequestDto
{
    public long Id { get; set; }
    public string EmployeeCode { get; set; } = string.Empty;
    public string? EmployeeName { get; set; }
    public DateTime AttendanceDate { get; set; }
    public DateTime? OldCheckIn { get; set; }
    public DateTime? OldCheckOut { get; set; }
    public DateTime? RequestedCheckIn { get; set; }
    public DateTime? RequestedCheckOut { get; set; }
    public string Reason { get; set; } = string.Empty;
    public string Status { get; set; } = "PENDING";
    public string? ApprovedBy { get; set; }
    public DateTime? ApprovalDate { get; set; }
    public string? RejectionReason { get; set; }
    public DateTime CreationDate { get; set; }
}

public sealed class CreateAttendanceCorrectionRequestDto
{
    public string EmployeeCode { get; set; } = string.Empty;
    public DateTime AttendanceDate { get; set; }
    public DateTime? RequestedCheckIn { get; set; }
    public DateTime? RequestedCheckOut { get; set; }
    public string Reason { get; set; } = string.Empty;
}

public sealed class ApproveAttendanceCorrectionDto
{
    public string? Notes { get; set; }
}

public sealed class RejectAttendanceCorrectionDto
{
    public string Reason { get; set; } = string.Empty;
}

public sealed class RawPunchDto
{
    public string EmployeeCode { get; set; } = string.Empty;
    public DateTime PunchTime { get; set; }
    public string PunchType { get; set; } = "IN"; // IN, OUT, AUTO
    public string Source { get; set; } = "BIOMETRIC"; // BIOMETRIC, MOBILE, WEB, MANUAL
    public string? DeviceId { get; set; }
    public string? ExternalReference { get; set; }
}

public sealed class AttendanceDayDto
{
    public long Id { get; set; }
    public string EmployeeCode { get; set; } = string.Empty;
    public string? EmployeeName { get; set; }
    public DateTime AttendanceDate { get; set; }
    public string? ShiftName { get; set; }
    public DateTime? FirstCheckIn { get; set; }
    public DateTime? LastCheckOut { get; set; }
    public decimal ScheduledHours { get; set; }
    public decimal ActualWorkedHours { get; set; }
    public int LateArrivalMinutes { get; set; }
    public int EarlyLeaveMinutes { get; set; }
    public decimal OvertimeHours { get; set; }
    public bool HasMissingPunch { get; set; }
    public string Status { get; set; } = "PRESENT";
    public string? Notes { get; set; }
}
