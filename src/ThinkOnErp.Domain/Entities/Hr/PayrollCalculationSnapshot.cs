using System;

namespace ThinkOnErp.Domain.Entities.Hr;

public sealed class PayrollCalculationSnapshot
{
    public long Id { get; set; }
    public long PayrollRunLineId { get; set; }
    public string EmployeeCode { get; set; } = string.Empty;
    public string PayPeriod { get; set; } = string.Empty;
    public string ProrationPolicyCode { get; set; } = string.Empty;
    public string ProrationMethodUsed { get; set; } = string.Empty;
    public decimal ProrationFactor { get; set; } = 1.0m;
    public decimal EligibleDays { get; set; }
    public decimal TotalBaseDays { get; set; }
    public string TaxPolicyCode { get; set; } = string.Empty;
    public decimal TaxExemptionsApplied { get; set; }
    public string SscPolicyCode { get; set; } = string.Empty;
    public decimal SscCapApplied { get; set; }
    public decimal SscEmployeeRateApplied { get; set; }
    public decimal SscEmployerRateApplied { get; set; }
    public decimal OvertimeHoursApplied { get; set; }
    public decimal OvertimeEarningsApplied { get; set; }
    public decimal LoanDeductionsApplied { get; set; }
    public decimal LoanDeductionsCarriedForward { get; set; }
    public string MathematicalExplanationJson { get; set; } = "{}";
    public DateTime CalculationTimestamp { get; set; } = DateTime.UtcNow;

    public PayrollRunLine? PayrollRunLine { get; set; }
}
