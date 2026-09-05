using System;
using System.Collections.Generic;

namespace ThinkOnErp.Application.DTOs.Hr;

public sealed class EmployeeLoanDto
{
    public long Id { get; set; }
    public string EmployeeCode { get; set; } = string.Empty;
    public string? EmployeeName { get; set; }
    public string LoanType { get; set; } = "PERSONAL";
    public decimal PrincipalAmount { get; set; }
    public decimal TotalInstallments { get; set; }
    public decimal MonthlyInstallmentAmount { get; set; }
    public decimal TotalPaidAmount { get; set; }
    public decimal RemainingBalance { get; set; }
    public DateTime StartDate { get; set; }
    public DateTime? EndDate { get; set; }
    public string Status { get; set; } = "PENDING";
    public string? ApprovedBy { get; set; }
    public DateTime? ApprovalDate { get; set; }
    public string? Notes { get; set; }
    public List<LoanRepaymentScheduleDto> Schedules { get; set; } = new();
}

public sealed class CreateEmployeeLoanDto
{
    public string EmployeeCode { get; set; } = string.Empty;
    public string LoanType { get; set; } = "PERSONAL";
    public decimal PrincipalAmount { get; set; }
    public int TotalInstallments { get; set; }
    public DateTime StartDate { get; set; }
    public string? Notes { get; set; }
}

public sealed class LoanRepaymentScheduleDto
{
    public long Id { get; set; }
    public long EmployeeLoanId { get; set; }
    public int InstallmentNo { get; set; }
    public string PayPeriod { get; set; } = string.Empty;
    public decimal ScheduledAmount { get; set; }
    public decimal PaidAmount { get; set; }
    public decimal CarriedForwardAmount { get; set; }
    public string Status { get; set; } = "PENDING";
    public DateTime? PaidDate { get; set; }
}

public sealed class EmployeeAdvanceDto
{
    public long Id { get; set; }
    public string EmployeeCode { get; set; } = string.Empty;
    public string? EmployeeName { get; set; }
    public decimal AdvanceAmount { get; set; }
    public string TargetPayPeriod { get; set; } = string.Empty;
    public decimal DeductedAmount { get; set; }
    public decimal RemainingBalance { get; set; }
    public string Status { get; set; } = "PENDING";
    public string? Reason { get; set; }
    public string? ApprovedBy { get; set; }
    public DateTime? ApprovalDate { get; set; }
}

public sealed class CreateEmployeeAdvanceDto
{
    public string EmployeeCode { get; set; } = string.Empty;
    public decimal AdvanceAmount { get; set; }
    public string TargetPayPeriod { get; set; } = string.Empty;
    public string? Reason { get; set; }
}
