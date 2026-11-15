using System;
using System.Collections.Generic;

namespace ThinkOnErp.Domain.Entities.Hr;

public static class EndOfServiceTerminationTypes
{
    public const string Resignation = "RESIGNATION";
    public const string ContractExpiry = "CONTRACT_EXPIRY";
    public const string TerminationEmployer = "TERMINATION";
    public const string Retirement = "RETIREMENT";
    public const string Death = "DEATH";
    public const string JobAbandonment = "JOB_ABANDONMENT";

    public static readonly IReadOnlySet<string> All = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        Resignation, ContractExpiry, TerminationEmployer, Retirement, Death, JobAbandonment
    };
}

public static class EndOfServiceSettlementStatus
{
    public const string Draft = "DRAFT";
    public const string Approved = "APPROVED";
    public const string Posted = "POSTED";
    public const string Cancelled = "CANCELLED";
}

public sealed class EndOfServiceSettlement
{
    public long Id { get; set; }
    public long CompanyId { get; set; }
    public long? BranchId { get; set; }
    public string EmployeeCode { get; set; } = string.Empty;

    public string TerminationType { get; set; } = EndOfServiceTerminationTypes.Resignation;
    public string? TerminationReason { get; set; }
    public DateTime? NoticeDate { get; set; }
    public DateTime LastWorkingDate { get; set; }
    public DateTime SettlementDate { get; set; } = DateTime.UtcNow;

    // Service period details
    public DateTime HireDate { get; set; }
    public decimal ServiceYears { get; set; }
    public int ServiceMonths { get; set; }
    public int ServiceDays { get; set; }

    // Salary snapshot at termination
    public decimal BaseSalarySnapshot { get; set; }
    public decimal GrossSalarySnapshot { get; set; }
    public string CurrencyCode { get; set; } = "JOD";

    // Entitlements (المستحقات)
    public decimal SeverancePay { get; set; }
    public decimal UnpaidSalary { get; set; }
    public decimal LeaveEncashment { get; set; }
    public decimal UnusedLeaveDays { get; set; }
    public decimal NoticePeriodPay { get; set; }
    public decimal OtherEntitlements { get; set; }
    public decimal TotalEntitlements { get; set; }

    // Deductions & Liabilities (الالتزامات والاقتطاعات)
    public decimal OutstandingLoans { get; set; }
    public decimal OutstandingAdvances { get; set; }
    public decimal NegativeLeaveDeduction { get; set; }
    public decimal OtherDeductions { get; set; }
    public decimal TotalDeductions { get; set; }

    // Net Amount Payable
    public decimal NetSettlementAmount { get; set; }

    // Status and Workflow
    public string Status { get; set; } = EndOfServiceSettlementStatus.Draft;
    public string? CalculationNotes { get; set; }

    public string? ApprovedBy { get; set; }
    public DateTime? ApprovedAtUtc { get; set; }
    public string? CancelledBy { get; set; }
    public DateTime? CancelledAtUtc { get; set; }
    public string? CancellationReason { get; set; }

    public string CreationUser { get; set; } = string.Empty;
    public DateTime CreationDate { get; set; } = DateTime.UtcNow;
    public string? UpdateUser { get; set; }
    public DateTime? UpdateDate { get; set; }

    // Relationships
    public Employee? Employee { get; set; }
    public List<EndOfServiceSettlementLine> Lines { get; set; } = new();
}

public sealed class EndOfServiceSettlementLine
{
    public long Id { get; set; }
    public long SettlementId { get; set; }
    public string ComponentCode { get; set; } = string.Empty; // SEVERANCE, UNPAID_SALARY, LEAVE_CASHOUT, LOAN_DEDUCTION, ADVANCE_DEDUCTION, etc.
    public string ComponentType { get; set; } = "ENTITLEMENT"; // ENTITLEMENT, DEDUCTION
    public decimal Amount { get; set; }
    public string? Description { get; set; }

    public EndOfServiceSettlement Settlement { get; set; } = null!;
}

public sealed class EndOfServicePolicy
{
    public long Id { get; set; }
    public long CompanyId { get; set; }
    public DateTime? EffectiveFrom { get; set; }
    public DateTime? EffectiveTo { get; set; }
    public string PolicyName { get; set; } = "Default End of Service Policy";
    public string CalculationBasis { get; set; } = "BASIC_SALARY"; // BASIC_SALARY, GROSS_SALARY
    public decimal LeaveDailyRateDivisor { get; set; } = 30m;
    public int NoticePeriodDays { get; set; } = 30;
    public bool ProrateFinalMonthByWorkingDays { get; set; } = false;

    // Company configurable resignation entitlement factor:
    // e.g. < 2 years = 0%, 2 to 5 years = 33.33%, 5 to 10 years = 66.67%, 10+ years = 100%
    public decimal ResignationFactorUnderTwoYears { get; set; } = 0.0m;
    public decimal ResignationFactorTwoToFiveYears { get; set; } = 0.3333m;
    public decimal ResignationFactorFiveToTenYears { get; set; } = 0.6667m;
    public decimal ResignationFactorTenYearsAndAbove { get; set; } = 1.0m;

    // Rate per year: e.g. 0.5 month per year for first 5 years, 1.0 month per year thereafter
    public decimal FirstFiveYearsRatePerYear { get; set; } = 0.5m;
    public decimal SubsequentYearsRatePerYear { get; set; } = 1.0m;

    public bool IsActive { get; set; } = true;
    public string CreationUser { get; set; } = string.Empty;
    public DateTime CreationDate { get; set; } = DateTime.UtcNow;
    public string? UpdateUser { get; set; }
    public DateTime? UpdateDate { get; set; }
}

