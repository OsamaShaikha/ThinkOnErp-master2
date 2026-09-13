using System;

namespace ThinkOnErp.Application.DTOs.Pos;

public class ClockInDto
{
    public long BranchId { get; set; }
    public long UserId { get; set; }
    public long? ShiftId { get; set; }
    public string? Notes { get; set; }
}

public class ClockOutDto
{
    public long AttendanceId { get; set; }
    public string? Notes { get; set; }
}

public class StaffAttendanceDto
{
    public long Id { get; set; }
    public long BranchId { get; set; }
    public long UserId { get; set; }
    public string? UserName { get; set; }
    public long? ShiftId { get; set; }
    public DateTime ClockInTime { get; set; }
    public DateTime? ClockOutTime { get; set; }
    public decimal TotalHoursWorked { get; set; }
    public string? Notes { get; set; }
}
