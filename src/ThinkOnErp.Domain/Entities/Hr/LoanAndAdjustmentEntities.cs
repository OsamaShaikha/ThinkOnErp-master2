using System;
using System.Collections.Generic;

namespace ThinkOnErp.Domain.Entities.Hr;

public sealed class EmployeeLoan
{
    public long Id { get; set; }
    public string EmployeeCode { get; set; } = string.Empty;
    public string LoanType { get; set; } = "PERSONAL"; // PERSONAL, HOUSING, EMERGENCY
    public decimal PrincipalAmount { get; set; }
    public decimal MonthlyInstallmentAmount { get; set; }
    public int TotalInstallments { get; set; }
    public decimal TotalPaidAmount { get; set; }
    public decimal RemainingBalance { get; set; }
    public DateTime StartDate { get; set; }
    public DateTime? EndDate { get; set; }
    public string Status { get; set; } = "PENDING"; // PENDING, APPROVED, ACTIVE, COMPLETED, CANCELLED
    public string? ApprovedBy { get; set; }
    public DateTime? ApprovalDate { get; set; }
    public string? Notes { get; set; }

    public string CreationUser { get; set; } = string.Empty;
    public DateTime CreationDate { get; set; } = DateTime.UtcNow;
    public string? UpdateUser { get; set; }
    public DateTime? UpdateDate { get; set; }

    public Employee? Employee { get; set; }
    public List<LoanRepaymentSchedule> Schedules { get; set; } = new();
}

public sealed class LoanRepaymentSchedule
{
    public long Id { get; set; }
    public long EmployeeLoanId { get; set; }
    public int InstallmentNo { get; set; }
    public string PayPeriod { get; set; } = string.Empty; // e.g. "2026-09"
    public decimal ScheduledAmount { get; set; }
    public decimal PaidAmount { get; set; }
    public decimal CarriedForwardAmount { get; set; }
    public string Status { get; set; } = "PENDING"; // PENDING, PAID, PARTIAL, SKIPPED
    public DateTime? PaidDate { get; set; }
    public long? PayrollRunLineId { get; set; }

    public EmployeeLoan? EmployeeLoan { get; set; }
}

public sealed class EmployeeAdvance
{
    public long Id { get; set; }
    public string EmployeeCode { get; set; } = string.Empty;
    public decimal AdvanceAmount { get; set; }
    public string TargetPayPeriod { get; set; } = string.Empty; // e.g. "2026-09"
    public decimal DeductedAmount { get; set; }
    public decimal RemainingBalance { get; set; }
    public string Status { get; set; } = "PENDING"; // PENDING, APPROVED, RECOVERED, CANCELLED
    public string? ApprovedBy { get; set; }
    public DateTime? ApprovalDate { get; set; }
    public string? Reason { get; set; }

    public string CreationUser { get; set; } = string.Empty;
    public DateTime CreationDate { get; set; } = DateTime.UtcNow;
    public string? UpdateUser { get; set; }
    public DateTime? UpdateDate { get; set; }

    public Employee? Employee { get; set; }
}

public sealed class PayrollAdjustment
{
    public long Id { get; set; }
    public long CompanyId { get; set; }
    public string EmployeeCode { get; set; } = string.Empty;
    public string PayPeriod { get; set; } = string.Empty; // e.g. "2026-09"
    public string ComponentCode { get; set; } = string.Empty;
    public string AdjustmentType { get; set; } = "EARNING"; // EARNING, DEDUCTION
    public decimal Amount { get; set; }
    public string? Description { get; set; }
    public string Status { get; set; } = "APPROVED"; // PENDING, APPROVED, PROCESSED, REJECTED
    public long? PayrollRunLineId { get; set; }

    public string CreationUser { get; set; } = string.Empty;
    public DateTime CreationDate { get; set; } = DateTime.UtcNow;
    public string? UpdateUser { get; set; }
    public DateTime? UpdateDate { get; set; }

    public Employee? Employee { get; set; }
    public SalaryComponent? Component { get; set; }
}
