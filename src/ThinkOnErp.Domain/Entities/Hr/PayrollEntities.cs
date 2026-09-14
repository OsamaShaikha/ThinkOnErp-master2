using System;
using System.Collections.Generic;

namespace ThinkOnErp.Domain.Entities.Hr;

public sealed class PayrollPeriod
{
    public long Id { get; set; }
    public long CompanyId { get; set; }
    public string PeriodCode { get; set; } = string.Empty; // e.g. "2026-09"
    public string NameAr { get; set; } = string.Empty;
    public string NameEn { get; set; } = string.Empty;
    public int FiscalYear { get; set; }
    public int Month { get; set; }
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    public DateTime PayDate { get; set; }
    public string PayrollType { get; set; } = "MONTHLY"; // MONTHLY, BIWEEKLY, SPECIAL
    public string Status { get; set; } = "OPEN"; // OPEN, PROCESSING, CLOSED

    public string CreationUser { get; set; } = string.Empty;
    public DateTime CreationDate { get; set; } = DateTime.UtcNow;
    public string? UpdateUser { get; set; }
    public DateTime? UpdateDate { get; set; }
}

public sealed class PayrollRun
{
    public long Id { get; set; }
    public string PayPeriod { get; set; } = string.Empty;
    public DateTime RunDate { get; set; } = DateTime.UtcNow;
    public long? BranchId { get; set; }
    public string Status { get; set; } = "DRAFT"; // DRAFT, CALCULATED, APPROVED, POSTED_TO_GL, PAID, CANCELLED

    public decimal TotalGrossSalary { get; set; }
    public decimal TotalNetSalary { get; set; }
    public decimal TotalEmployeeSsc { get; set; }
    public decimal TotalEmployerSsc { get; set; }
    public decimal TotalIncomeTax { get; set; }
    public decimal TotalNationalContrib { get; set; }
    public decimal TotalOtherDeductions { get; set; }

    public string? CalculatedBy { get; set; }
    public DateTime? CalculationDate { get; set; }
    public string? ApprovedBy { get; set; }
    public DateTime? ApprovalDate { get; set; }
    public string? PostedBy { get; set; }
    public DateTime? PostDate { get; set; }
    public string? PaidBy { get; set; }
    public DateTime? PaymentDate { get; set; }
    public long? JournalVoucherId { get; set; } // GL link

    public string CreationUser { get; set; } = string.Empty;
    public DateTime CreationDate { get; set; } = DateTime.UtcNow;
    public string? UpdateUser { get; set; }
    public DateTime? UpdateDate { get; set; }

    public List<PayrollRunLine> Lines { get; set; } = new();
}

public sealed class PayrollRunLine
{
    public long Id { get; set; }
    public long PayrollRunId { get; set; }
    public string EmployeeCode { get; set; } = string.Empty;
    public long? BranchId { get; set; }
    public string? DepartmentCode { get; set; }
    public string? CostCenterCode { get; set; }

    public decimal BasicSalary { get; set; }
    public decimal TotalEarnings { get; set; }
    public decimal GrossSalary { get; set; }
    public decimal SscEligibleSalary { get; set; }
    public decimal SscEmployeeContrib { get; set; }
    public decimal SscEmployerContrib { get; set; }
    public decimal TaxableGross { get; set; }
    public decimal AnnualExemptions { get; set; }
    public decimal AnnualTaxableNet { get; set; }
    public decimal IncomeTaxWithheld { get; set; }
    public decimal NationalContribWithheld { get; set; }
    public decimal OtherDeductions { get; set; }
    public decimal TotalDeductions { get; set; }
    public decimal NetPay { get; set; }

    public string PaymentMethod { get; set; } = "BANK_TRANSFER";
    public string? BankCode { get; set; }
    public string? Iban { get; set; }
    public string Status { get; set; } = "CALCULATED";

    public string CreationUser { get; set; } = string.Empty;
    public DateTime CreationDate { get; set; } = DateTime.UtcNow;

    public PayrollRun? PayrollRun { get; set; }
    public Employee? Employee { get; set; }
    public List<PayrollRunLineComponent> Components { get; set; } = new();
    public PayrollCalculationSnapshot? Snapshot { get; set; }
}

public sealed class PayrollRunLineComponent
{
    public long Id { get; set; }
    public long PayrollLineId { get; set; }
    public string ComponentCode { get; set; } = string.Empty;
    public string ComponentNameLocal { get; set; } = string.Empty;
    public string ComponentNameEn { get; set; } = string.Empty;
    public string ComponentType { get; set; } = "EARNING"; // EARNING, DEDUCTION, EMPLOYER_CONTRIB
    public decimal Amount { get; set; }

    public PayrollRunLine? PayrollRunLine { get; set; }
}

public sealed class PayrollCalculationSnapshot
{
    public long Id { get; set; }
    public long PayrollRunLineId { get; set; }
    public string EmployeeCode { get; set; } = string.Empty;
    public string PayPeriod { get; set; } = string.Empty;
    public DateTime CalculationTimestamp { get; set; } = DateTime.UtcNow;

    public decimal TotalBaseDays { get; set; }
    public decimal EligibleDays { get; set; }
    public decimal ProrationFactor { get; set; } = 1.0m;
    public string ProrationPolicyCode { get; set; } = string.Empty;
    public string ProrationMethodUsed { get; set; } = string.Empty;

    public decimal OvertimeHoursApplied { get; set; }
    public decimal OvertimeEarningsApplied { get; set; }

    public string SscPolicyCode { get; set; } = string.Empty;
    public decimal SscEmpRateApplied { get; set; }
    public decimal SscEmprRateApplied { get; set; }
    public decimal SscCapApplied { get; set; }

    public string TaxPolicyCode { get; set; } = string.Empty;
    public decimal TaxExemptionsApplied { get; set; }

    public decimal LoanDeductionsApplied { get; set; }
    public decimal LoanDeductionsCarriedFwd { get; set; }

    public string ExplanationJson { get; set; } = string.Empty; // Full JSON breakdown for explanation service

    public PayrollRunLine? PayrollRunLine { get; set; }
}
