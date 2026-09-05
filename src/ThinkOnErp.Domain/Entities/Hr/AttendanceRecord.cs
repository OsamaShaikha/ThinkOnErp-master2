using System;

namespace ThinkOnErp.Domain.Entities.Hr;

public sealed class AttendanceRecord
{
    public long Id { get; set; }
    public string EmployeeCode { get; set; } = string.Empty;
    public DateTime AttendanceDate { get; set; }
    public DateTime? ClockIn { get; set; }
    public DateTime? ClockOut { get; set; }
    public string Source { get; set; } = "WEB"; // MOBILE, BIOMETRIC, MANUAL, WEB
    public string Status { get; set; } = "ON_TIME"; // ON_TIME, LATE, EARLY_LEAVE, ABSENT, MISSED_PUNCH
    public int LateMinutes { get; set; } = 0;
    public int EarlyLeaveMinutes { get; set; } = 0;
    public decimal TotalWorkHours { get; set; } = 0m;
    public string? CorrectedBy { get; set; }
    public string? CorrectionReason { get; set; }
    public string? IdempotencyKey { get; set; }
    public string CreationUser { get; set; } = string.Empty;
    public DateTime CreationDate { get; set; } = DateTime.UtcNow;
    public string? UpdateUser { get; set; }
    public DateTime? UpdateDate { get; set; }

    public Employee? Employee { get; set; }
}
