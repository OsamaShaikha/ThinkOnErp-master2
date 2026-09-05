using System;
using System.Collections.Generic;

namespace ThinkOnErp.Application.DTOs.Hr;

public sealed class PayrollPeriodDto
{
    public long Id { get; set; }
    public long CompanyId { get; set; }
    public string PeriodCode { get; set; } = string.Empty;
    public string NameEn { get; set; } = string.Empty;
    public string NameAr { get; set; } = string.Empty;
    public int FiscalYear { get; set; }
    public int Month { get; set; }
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    public DateTime PayDate { get; set; }
    public string PayrollType { get; set; } = "MONTHLY";
    public string Status { get; set; } = "OPEN";
}

public sealed class CreatePayrollPeriodDto
{
    public long CompanyId { get; set; }
    public string PeriodCode { get; set; } = string.Empty;
    public string NameEn { get; set; } = string.Empty;
    public string NameAr { get; set; } = string.Empty;
    public int FiscalYear { get; set; }
    public int Month { get; set; }
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    public DateTime PayDate { get; set; }
    public string PayrollType { get; set; } = "MONTHLY";
}

public sealed class PayrollExplanationDto
{
    public string EmployeeCode { get; set; } = string.Empty;
    public string EmployeeName { get; set; } = string.Empty;
    public string PayPeriod { get; set; } = string.Empty;
    public decimal BasicSalary { get; set; }
    public decimal GrossSalary { get; set; }
    public decimal TotalEarnings { get; set; }
    public decimal TotalDeductions { get; set; }
    public decimal NetPay { get; set; }

    public string ProrationMethod { get; set; } = string.Empty;
    public decimal ProrationFactor { get; set; }
    public decimal EligibleDays { get; set; }
    public decimal TotalBaseDays { get; set; }

    public decimal SscEligibleSalary { get; set; }
    public decimal SscEmployeeRate { get; set; }
    public decimal SscEmployeeContribution { get; set; }
    public decimal SscEmployerContribution { get; set; }
    public decimal SscCapApplied { get; set; }

    public decimal AnnualTaxableNet { get; set; }
    public decimal AnnualExemptions { get; set; }
    public decimal MonthlyTaxWithheld { get; set; }
    public decimal NationalContributionWithheld { get; set; }

    public decimal OvertimeHours { get; set; }
    public decimal OvertimeEarnings { get; set; }
    public decimal LoanInstallmentDeducted { get; set; }
    public decimal LoanAmountCarriedForward { get; set; }

    public List<PayrollExplanationLineDto> ComponentLines { get; set; } = new();
}

public sealed class PayrollExplanationLineDto
{
    public string ComponentCode { get; set; } = string.Empty;
    public string ComponentNameEn { get; set; } = string.Empty;
    public string ComponentNameAr { get; set; } = string.Empty;
    public string ComponentType { get; set; } = "EARNING"; // EARNING, DEDUCTION
    public decimal Amount { get; set; }
    public string? CalculationFormula { get; set; }
}

public sealed class PayrollValidationResultDto
{
    public bool IsValid { get; set; }
    public int TotalEmployees { get; set; }
    public int ErrorCount => Errors.Count;
    public int WarningCount => Warnings.Count;
    public List<PayrollValidationMessageDto> Errors { get; set; } = new();
    public List<PayrollValidationMessageDto> Warnings { get; set; } = new();
}

public sealed class PayrollValidationMessageDto
{
    public string? EmployeeCode { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public string Severity { get; set; } = "ERROR"; // ERROR, WARNING
}
