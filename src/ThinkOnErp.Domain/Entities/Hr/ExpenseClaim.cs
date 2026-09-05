using System;
using System.Collections.Generic;

namespace ThinkOnErp.Domain.Entities.Hr;

public sealed class ExpenseClaim
{
    public long Id { get; set; }
    public string EmployeeCode { get; set; } = string.Empty;
    public string ClaimNumber { get; set; } = string.Empty;
    public DateTime ClaimDate { get; set; } = DateTime.UtcNow;
    public decimal TotalAmount { get; set; } = 0m;
    public string CurrencyCode { get; set; } = "JOD";
    public string ReimbursementMethod { get; set; } = "NEXT_PAYROLL_RUN"; // NEXT_PAYROLL_RUN, DIRECT_AP_PAYMENT
    public string Status { get; set; } = "SUBMITTED"; // SUBMITTED, APPROVED, REJECTED, REIMBURSED
    public string? ApprovedBy { get; set; }
    public DateTime? ApprovalDate { get; set; }
    public string? RejectionReason { get; set; }
    public string? ReimbursedInPayPeriod { get; set; } // When reimbursed via payroll
    public long? ApVoucherId { get; set; } // When reimbursed via AP
    public string? Description { get; set; }
    public string CreationUser { get; set; } = string.Empty;
    public DateTime CreationDate { get; set; } = DateTime.UtcNow;
    public string? UpdateUser { get; set; }
    public DateTime? UpdateDate { get; set; }

    public Employee? Employee { get; set; }
    public List<ExpenseClaimLine> Lines { get; set; } = new();
}
