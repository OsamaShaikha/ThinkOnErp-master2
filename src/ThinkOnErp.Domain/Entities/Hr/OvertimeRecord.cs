using System;

namespace ThinkOnErp.Domain.Entities.Hr;

public sealed class OvertimeRecord
{
    public long Id { get; set; }
    public string EmployeeCode { get; set; } = string.Empty;
    public DateTime OvertimeDate { get; set; }
    public decimal Hours { get; set; }
    public decimal RateMultiplier { get; set; } = 1.25m; // 1.25 for regular overtime, 1.50 for weekend/holiday
    public string Status { get; set; } = "PENDING"; // PENDING, APPROVED, REJECTED
    public string? Reason { get; set; }
    public string? ApprovedBy { get; set; }
    public DateTime? ApprovalDate { get; set; }
    public string CreationUser { get; set; } = string.Empty;
    public DateTime CreationDate { get; set; } = DateTime.UtcNow;
    public string? UpdateUser { get; set; }
    public DateTime? UpdateDate { get; set; }

    public Employee? Employee { get; set; }
}
