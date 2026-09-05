using System;
using System.Collections.Generic;

namespace ThinkOnErp.Application.DTOs.Hr;

public sealed class ShiftScheduleDto
{
    public string ShiftCode { get; set; } = string.Empty;
    public string NameAr { get; set; } = string.Empty;
    public string NameEn { get; set; } = string.Empty;
    public TimeSpan StartTime { get; set; }
    public TimeSpan EndTime { get; set; }
    public int BreakMinutes { get; set; }
    public string WorkingDaysJson { get; set; } = string.Empty;
    public bool IsActive { get; set; }
    public DateTime CreationDate { get; set; }
}

public sealed class CreateShiftScheduleDto
{
    public string ShiftCode { get; set; } = string.Empty;
    public string NameAr { get; set; } = string.Empty;
    public string NameEn { get; set; } = string.Empty;
    public TimeSpan StartTime { get; set; }
    public TimeSpan EndTime { get; set; }
    public int BreakMinutes { get; set; } = 60;
    public List<string>? WorkingDays { get; set; }
}

public sealed class AssignShiftDto
{
    public string EmployeeCode { get; set; } = string.Empty;
    public string ShiftCode { get; set; } = string.Empty;
    public DateTime EffectiveFrom { get; set; }
    public DateTime? EffectiveTo { get; set; }
}

public sealed class AttendanceRecordDto
{
    public long Id { get; set; }
    public string EmployeeCode { get; set; } = string.Empty;
    public string EmployeeNameAr { get; set; } = string.Empty;
    public string EmployeeNameEn { get; set; } = string.Empty;
    public string? DepartmentName { get; set; }
    public DateTime AttendanceDate { get; set; }
    public DateTime? ClockIn { get; set; }
    public DateTime? ClockOut { get; set; }
    public string Source { get; set; } = "WEB";
    public string Status { get; set; } = "ON_TIME";
    public int LateMinutes { get; set; }
    public int EarlyLeaveMinutes { get; set; }
    public decimal TotalWorkHours { get; set; }
    public string? CorrectedBy { get; set; }
    public string? CorrectionReason { get; set; }
}

public sealed class ClockInRequestDto
{
    public string EmployeeCode { get; set; } = string.Empty;
    public DateTime? Timestamp { get; set; }
    public string Source { get; set; } = "WEB"; // MOBILE, BIOMETRIC, MANUAL, WEB
    public string? IdempotencyKey { get; set; }
}

public sealed class ClockOutRequestDto
{
    public string EmployeeCode { get; set; } = string.Empty;
    public DateTime? Timestamp { get; set; }
    public string Source { get; set; } = "WEB";
    public string? IdempotencyKey { get; set; }
}

public sealed class CorrectAttendanceDto
{
    public DateTime? ClockIn { get; set; }
    public DateTime? ClockOut { get; set; }
    public string Status { get; set; } = "ON_TIME";
    public string CorrectionReason { get; set; } = string.Empty;
}

public sealed class OvertimeRecordDto
{
    public long Id { get; set; }
    public string EmployeeCode { get; set; } = string.Empty;
    public string EmployeeNameEn { get; set; } = string.Empty;
    public DateTime OvertimeDate { get; set; }
    public decimal Hours { get; set; }
    public decimal RateMultiplier { get; set; }
    public string Status { get; set; } = "PENDING";
    public string? Reason { get; set; }
    public string? ApprovedBy { get; set; }
    public DateTime? ApprovalDate { get; set; }
}

public sealed class RequestOvertimeDto
{
    public string EmployeeCode { get; set; } = string.Empty;
    public DateTime OvertimeDate { get; set; }
    public decimal Hours { get; set; }
    public decimal RateMultiplier { get; set; } = 1.25m;
    public string? Reason { get; set; }
}
