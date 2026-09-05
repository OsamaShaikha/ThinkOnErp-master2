using System;

namespace ThinkOnErp.Domain.Entities.Hr;

public sealed class EmployeeAdvance
{
    public long Id { get; set; }
    public string EmployeeCode { get; set; } = string.Empty;
    public decimal AdvanceAmount { get; set; }
    public string TargetPayPeriod { get; set; } = string.Empty; // e.g. "2026-08"
    public decimal DeductedAmount { get; set; }
    public decimal RemainingBalance { get; set; }
    public string Status { get; set; } = "PENDING"; // PENDING, APPROVED, DEDUCTED, REJECTED, CANCELLED
    public string? Reason { get; set; }
    public string? ApprovedBy { get; set; }
    public DateTime? ApprovalDate { get; set; }
    public string CreationUser { get; set; } = string.Empty;
    public DateTime CreationDate { get; set; } = DateTime.UtcNow;
    public string? UpdateUser { get; set; }
    public DateTime? UpdateDate { get; set; }

    public Employee? Employee { get; set; }
}
