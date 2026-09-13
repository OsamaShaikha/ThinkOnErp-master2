using System;
using ThinkOnErp.Domain.Entities;

namespace ThinkOnErp.Domain.Entities.Pos;

public class PosStaffAttendance
{
    public long Id { get; set; }
    public long BranchId { get; set; }
    public long UserId { get; set; }
    public long? ShiftId { get; set; }
    public DateTime ClockInTime { get; set; } = DateTime.UtcNow;
    public DateTime? ClockOutTime { get; set; }
    public decimal TotalHoursWorked { get; set; }
    public string? Notes { get; set; }

    // Navigation
    public SysBranch? Branch { get; set; }
    public SysUser? User { get; set; }
    public PosShift? Shift { get; set; }
}
