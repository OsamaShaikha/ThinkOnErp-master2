using System;

namespace ThinkOnErp.Domain.Entities.Hr;

public sealed class LoanRepaymentSchedule
{
    public long Id { get; set; }
    public long EmployeeLoanId { get; set; }
    public int InstallmentNo { get; set; }
    public string PayPeriod { get; set; } = string.Empty; // e.g. "2026-08"
    public decimal ScheduledAmount { get; set; }
    public decimal PaidAmount { get; set; }
    public decimal CarriedForwardAmount { get; set; }
    public string Status { get; set; } = "PENDING"; // PENDING, PAID, PARTIAL, SKIPPED, CANCELLED
    public DateTime? PaidDate { get; set; }
    public long? PayrollRunLineId { get; set; }

    public EmployeeLoan? EmployeeLoan { get; set; }
}
