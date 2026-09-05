using System;
using System.Collections.Generic;

namespace ThinkOnErp.Application.DTOs.Hr;

public sealed class PayrollRunDto
{
    public long Id { get; set; }
    public long? BranchId { get; set; }
    public string PayPeriod { get; set; } = string.Empty;
    public DateTime RunDate { get; set; }
    public string Status { get; set; } = "DRAFT";
    public decimal TotalGrossSalary { get; set; }
    public decimal TotalNetSalary { get; set; }
    public decimal TotalEmployeeSsc { get; set; }
    public decimal TotalEmployerSsc { get; set; }
    public decimal TotalIncomeTax { get; set; }
    public decimal TotalNationalContribution { get; set; }
    public decimal TotalOtherDeductions { get; set; }
    public long? JournalVoucherId { get; set; }
    public string? CalculatedBy { get; set; }
    public DateTime? CalculationDate { get; set; }
    public string? ApprovedBy { get; set; }
    public DateTime? ApprovalDate { get; set; }
    public string? PostedBy { get; set; }
    public DateTime? PostDate { get; set; }
    public string? PaidBy { get; set; }
    public DateTime? PaymentDate { get; set; }
    public int EmployeeCount { get; set; }
    public List<PayrollRunLineDto> Lines { get; set; } = new();
}

public sealed class CreatePayrollRunDto
{
    public string PayPeriod { get; set; } = string.Empty; // Format: YYYY-MM
    public long? BranchId { get; set; }
}

public sealed class PayrollRunLineDto
{
    public long Id { get; set; }
    public long PayrollRunId { get; set; }
    public string EmployeeCode { get; set; } = string.Empty;
    public string EmployeeNameAr { get; set; } = string.Empty;
    public string EmployeeNameEn { get; set; } = string.Empty;
    public string? DepartmentCode { get; set; }
    public string? CostCenterCode { get; set; }
    public decimal BasicSalary { get; set; }
    public decimal TotalEarnings { get; set; }
    public decimal GrossSalary { get; set; }
    public decimal SscEligibleSalary { get; set; }
    public decimal SscEmployeeContribution { get; set; }
    public decimal SscEmployerContribution { get; set; }
    public decimal TaxableGross { get; set; }
    public decimal AnnualExemptions { get; set; }
    public decimal AnnualTaxableNet { get; set; }
    public decimal IncomeTaxWithheld { get; set; }
    public decimal NationalContributionWithheld { get; set; }
    public decimal OtherDeductions { get; set; }
    public decimal TotalDeductions { get; set; }
    public decimal NetPay { get; set; }
    public string PaymentMethod { get; set; } = "BANK_TRANSFER";
    public string? BankCode { get; set; }
    public string? Iban { get; set; }
    public string Status { get; set; } = "CALCULATED";
    public List<PayrollRunLineComponentDto> Components { get; set; } = new();
}

public sealed class PayrollRunLineComponentDto
{
    public long Id { get; set; }
    public string ComponentCode { get; set; } = string.Empty;
    public string ComponentNameEn { get; set; } = string.Empty;
    public string ComponentNameAr { get; set; } = string.Empty;
    public string ComponentType { get; set; } = "EARNING";
    public decimal Amount { get; set; }
    public string? GlAccountCode { get; set; }
}

public sealed class PayslipDto
{
    public long LineId { get; set; }
    public string PayPeriod { get; set; } = string.Empty;
    public string EmployeeCode { get; set; } = string.Empty;
    public string EmployeeNameAr { get; set; } = string.Empty;
    public string EmployeeNameEn { get; set; } = string.Empty;
    public string? DepartmentName { get; set; }
    public string? PositionTitle { get; set; }
    public string? SscNumber { get; set; }
    public string? NationalId { get; set; }
    public string? BankCode { get; set; }
    public string? Iban { get; set; }

    public decimal BasicSalary { get; set; }
    public decimal GrossSalary { get; set; }
    public decimal SscEmployeeContribution { get; set; }
    public decimal SscEmployerContribution { get; set; }
    public decimal IncomeTaxWithheld { get; set; }
    public decimal NationalContributionWithheld { get; set; }
    public decimal OtherDeductions { get; set; }
    public decimal TotalDeductions { get; set; }
    public decimal NetPay { get; set; }

    public List<PayrollRunLineComponentDto> Earnings { get; set; } = new();
    public List<PayrollRunLineComponentDto> Deductions { get; set; } = new();
}
