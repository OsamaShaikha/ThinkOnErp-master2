using System;

namespace ThinkOnErp.Domain.Entities.Hr;

public sealed class RawAttendance
{
    public long Id { get; set; }
    public string EmployeeCode { get; set; } = string.Empty;
    public DateTime PunchTime { get; set; }
    public string PunchType { get; set; } = "IN"; // IN, OUT, AUTO
    public string Source { get; set; } = "BIOMETRIC"; // BIOMETRIC, MOBILE, WEB, MANUAL
    public string? DeviceId { get; set; }
    public string? ExternalReference { get; set; }
    public bool IsProcessed { get; set; }
    public DateTime? ProcessedDate { get; set; }
    public string CreationUser { get; set; } = string.Empty;
    public DateTime CreationDate { get; set; } = DateTime.UtcNow;

    public Employee? Employee { get; set; }
}
