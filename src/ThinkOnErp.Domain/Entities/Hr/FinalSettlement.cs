using System;

namespace ThinkOnErp.Domain.Entities.Hr;

public sealed class FinalSettlement
{
    public long Id { get; set; }
    public string EmployeeCode { get; set; } = string.Empty;
    public DateTime TerminationDate { get; set; }
    public string TerminationReason { get; set; } = "RESIGNATION"; // RESIGNATION, DISMISSAL, CONTRACT_END, RETIREMENT, MUTUAL_AGREEMENT
    public decimal ServiceYears { get; set; }
    public decimal LastBasicSalary { get; set; }

    // Entitlements
    public decimal EndOfServiceGratuity { get; set; }
    public decimal UnusedLeaveDays { get; set; }
    public decimal UnusedLeaveEncashment { get; set; }
    public decimal NoticePeriodPay { get; set; }
    public decimal OtherEntitlements { get; set; }
    public decimal TotalEntitlements => EndOfServiceGratuity + UnusedLeaveEncashment + NoticePeriodPay + OtherEntitlements;

    // Deductions
    public decimal LoanBalanceDeduction { get; set; }
    public decimal OtherDeductions { get; set; }
    public decimal TotalDeductions => LoanBalanceDeduction + OtherDeductions;

    // Net Payout
    public decimal NetSettlementAmount => TotalEntitlements - TotalDeductions;

    public string Status { get; set; } = "DRAFT"; // DRAFT, APPROVED, POSTED, PAID
    public long? JournalVoucherId { get; set; }
    public string? ApprovedBy { get; set; }
    public DateTime? ApprovalDate { get; set; }
    public string? Notes { get; set; }
    public string CreationUser { get; set; } = string.Empty;
    public DateTime CreationDate { get; set; } = DateTime.UtcNow;
    public string? UpdateUser { get; set; }
    public DateTime? UpdateDate { get; set; }

    public Employee? Employee { get; set; }
}
