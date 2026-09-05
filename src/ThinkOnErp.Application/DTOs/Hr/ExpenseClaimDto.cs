using System;
using System.Collections.Generic;

namespace ThinkOnErp.Application.DTOs.Hr;

public sealed class ExpenseClaimDto
{
    public long Id { get; set; }
    public string EmployeeCode { get; set; } = string.Empty;
    public string EmployeeNameEn { get; set; } = string.Empty;
    public string ClaimNumber { get; set; } = string.Empty;
    public DateTime ClaimDate { get; set; }
    public decimal TotalAmount { get; set; }
    public string CurrencyCode { get; set; } = "JOD";
    public string ReimbursementMethod { get; set; } = "NEXT_PAYROLL_RUN";
    public string Status { get; set; } = "SUBMITTED";
    public string? ApprovedBy { get; set; }
    public DateTime? ApprovalDate { get; set; }
    public string? RejectionReason { get; set; }
    public string? ReimbursedInPayPeriod { get; set; }
    public string? Description { get; set; }
    public List<ExpenseClaimLineDto> Lines { get; set; } = new();
}

public sealed class ExpenseClaimLineDto
{
    public long Id { get; set; }
    public DateTime ExpenseDate { get; set; }
    public string Category { get; set; } = "TRAVEL";
    public string Description { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public string? ReceiptFileReference { get; set; }
    public string? ExpenseGlAccountCode { get; set; }
}

public sealed class SubmitExpenseClaimDto
{
    public string EmployeeCode { get; set; } = string.Empty;
    public string ReimbursementMethod { get; set; } = "NEXT_PAYROLL_RUN"; // NEXT_PAYROLL_RUN, DIRECT_AP_PAYMENT
    public string? Description { get; set; }
    public List<CreateExpenseClaimLineDto> Lines { get; set; } = new();
}

public sealed class CreateExpenseClaimLineDto
{
    public DateTime ExpenseDate { get; set; }
    public string Category { get; set; } = "TRAVEL";
    public string Description { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public string? ReceiptFileReference { get; set; }
    public string? ExpenseGlAccountCode { get; set; }
}
