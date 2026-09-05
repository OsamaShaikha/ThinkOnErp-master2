using System;
using System.Collections.Generic;

namespace ThinkOnErp.Application.DTOs.Hr;

public sealed class EndOfServiceProvisionDto
{
    public long Id { get; set; }
    public string EmployeeCode { get; set; } = string.Empty;
    public string EmployeeNameEn { get; set; } = string.Empty;
    public string PayPeriod { get; set; } = string.Empty;
    public decimal BasicSalary { get; set; }
    public decimal ServiceYears { get; set; }
    public decimal MonthlyAccrualAmount { get; set; }
    public decimal TotalAccumulatedProvision { get; set; }
    public long? JournalVoucherId { get; set; }
    public DateTime CreationDate { get; set; }
}

public sealed class RunProvisionAccrualResultDto
{
    public int ProcessedEmployees { get; set; }
    public decimal TotalAccruedAmount { get; set; }
    public string PayPeriod { get; set; } = string.Empty;
    public long? JournalVoucherId { get; set; }
    public string Message { get; set; } = string.Empty;
}

public sealed class FinalSettlementDto
{
    public long Id { get; set; }
    public string EmployeeCode { get; set; } = string.Empty;
    public string EmployeeNameAr { get; set; } = string.Empty;
    public string EmployeeNameEn { get; set; } = string.Empty;
    public DateTime TerminationDate { get; set; }
    public string TerminationReason { get; set; } = "RESIGNATION";
    public decimal ServiceYears { get; set; }
    public decimal LastBasicSalary { get; set; }

    public decimal EndOfServiceGratuity { get; set; }
    public decimal UnusedLeaveDays { get; set; }
    public decimal UnusedLeaveEncashment { get; set; }
    public decimal NoticePeriodPay { get; set; }
    public decimal OtherEntitlements { get; set; }
    public decimal TotalEntitlements { get; set; }

    public decimal LoanBalanceDeduction { get; set; }
    public decimal OtherDeductions { get; set; }
    public decimal TotalDeductions { get; set; }

    public decimal NetSettlementAmount { get; set; }
    public string Status { get; set; } = "DRAFT";
    public long? JournalVoucherId { get; set; }
    public string? ApprovedBy { get; set; }
    public DateTime? ApprovalDate { get; set; }
    public string? Notes { get; set; }
}

public sealed class CalculateFinalSettlementDto
{
    public string EmployeeCode { get; set; } = string.Empty;
    public DateTime TerminationDate { get; set; }
    public string TerminationReason { get; set; } = "RESIGNATION"; // RESIGNATION, DISMISSAL, CONTRACT_END, RETIREMENT, MUTUAL_AGREEMENT
    public decimal? NoticePeriodPay { get; set; }
    public decimal? OtherEntitlements { get; set; }
    public decimal? LoanBalanceDeduction { get; set; }
    public decimal? OtherDeductions { get; set; }
    public string? Notes { get; set; }
}
