using System;
using System.Collections.Generic;

namespace ThinkOnErp.Domain.Entities.Hr;

public sealed class PayrollRunLine
{
    public long Id { get; set; }
    public long PayrollRunId { get; set; }
    public string EmployeeCode { get; set; } = string.Empty;
    public string? DepartmentCode { get; set; }
    public string? CostCenterCode { get; set; }
    public long? BranchId { get; set; }

    // Core Amounts (JOD)
    public decimal BasicSalary { get; set; }
    public decimal TotalEarnings { get; set; }
    public decimal GrossSalary { get; set; }

    // SSC Components
    public decimal SscEligibleSalary { get; set; }
    public decimal SscEmployeeContribution { get; set; } // 7.5%
    public decimal SscEmployerContribution { get; set; } // 14.25% + high risk

    // Tax Components
    public decimal TaxableGross { get; set; }
    public decimal AnnualExemptions { get; set; }
    public decimal AnnualTaxableNet { get; set; }
    public decimal IncomeTaxWithheld { get; set; }
    public decimal NationalContributionWithheld { get; set; }

    // Deductions & Final Net
    public decimal OtherDeductions { get; set; }
    public decimal TotalDeductions { get; set; }
    public decimal NetPay { get; set; }

    // Disbursement info
    public string PaymentMethod { get; set; } = "BANK_TRANSFER";
    public string? BankCode { get; set; }
    public string? Iban { get; set; }

    public string Status { get; set; } = "CALCULATED";
    public string CreationUser { get; set; } = string.Empty;
    public DateTime CreationDate { get; set; } = DateTime.UtcNow;

    public PayrollRun? PayrollRun { get; set; }
    public Employee? Employee { get; set; }
    public List<PayrollRunLineComponent> Components { get; set; } = new();
}
