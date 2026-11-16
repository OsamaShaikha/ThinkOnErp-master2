using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace ThinkOnErp.Application.DTOs.Hr;

public record OvertimeRuleDto(
    long CompanyId,
    string Code,
    string NameAr,
    string NameEn,
    DateTime EffectiveFrom,
    DateTime? EffectiveTo,
    string DayType,
    decimal Multiplier,
    int MinimumMinutes,
    int MaximumMinutes,
    string HourlyDivisorFormula,
    bool RequiresApproval
);

public record ProrationPolicyDto(
    long CompanyId,
    string Code,
    string NameAr,
    string NameEn,
    DateTime EffectiveFrom,
    DateTime? EffectiveTo,
    string Method,
    bool IsDefault,
    string SalarySegmentAllocation = "UNCONFIGURED"
);

public record DeductionPolicyDto(
    long CompanyId,
    string Code,
    string NameAr,
    string NameEn,
    DateTime EffectiveFrom,
    DateTime? EffectiveTo,
    decimal MaxDeductionPercentage,
    decimal MinNetPayGuarantee,
    bool AutoCapAndCarryForward,
    bool AllowNegativeNetPay,
    bool IsDefault
);

public record TaxPolicyDto(
    long CompanyId,
    string Code,
    string NameAr,
    string NameEn,
    DateTime EffectiveFrom,
    DateTime? EffectiveTo,
    decimal PersonalExemptionSelf,
    decimal PersonalExemptionDependent,
    decimal NationalContribThreshold,
    decimal NationalContribRate,
    bool IsSscTaxDeductible,
    string CalculationFrequency,
    List<TaxBracketDto> Brackets,
    int? MaxExemptedDependents = null
);

public record TaxBracketDto(
    int BracketOrder,
    decimal LowerLimit,
    decimal? UpperLimit,
    decimal RatePercent,
    string? Description
);

public record SSCPolicyDto(
    long CompanyId,
    string Code,
    string NameAr,
    string NameEn,
    DateTime EffectiveFrom,
    DateTime? EffectiveTo,
    decimal EmployeeContribRate,
    decimal EmployerContribRate,
    decimal HighRiskSurchargeRate,
    decimal MonthlyCeilingCap,
    decimal MinimumWageFloor
);

// Payroll Run & Calculation DTOs
public record CreatePayrollRunDto(
    long CompanyId,
    string PayPeriod,
    long? BranchId
);

public record CancelPayrollRunDto(string Reason);
public record ReversePayrollGlDto(
    [property: global::System.Text.Json.Serialization.JsonRequired] DateTime ReversalDate,
    [property: global::System.Text.Json.Serialization.JsonRequired] string Reason);
public record PayrollGlReversalResultDto(long PayrollRunId, string Status, long? JournalVoucherId);

public record PayrollCalculationResultDto(
    long PayrollRunId,
    string PayPeriod,
    int EmployeeCount,
    decimal TotalGross,
    decimal TotalNet,
    decimal TotalEmployeeSsc,
    decimal TotalEmployerSsc,
    decimal TotalIncomeTax,
    decimal TotalNationalContrib,
    decimal TotalOtherDeductions,
    List<PayslipSummaryDto> Payslips
)
{
    public string Status { get; init; } = string.Empty;
    public string? CancelledBy { get; init; }
    public DateTime? CancellationDate { get; init; }
    public string? CancellationReason { get; init; }
    public long? CorrectionOfRunId { get; init; }
    public long? CorrectionRootRunId { get; init; }
    public int CorrectionVersion { get; init; }
    public string? CorrectionReason { get; init; }
}

public record PayslipSummaryDto(
    long RunLineId,
    string EmployeeCode,
    string EmployeeName,
    decimal BasicSalary,
    decimal GrossSalary,
    decimal TotalEarnings,
    decimal SscEmployeeContrib,
    decimal IncomeTaxWithheld,
    decimal OtherDeductions,
    decimal TotalDeductions,
    decimal NetPay,
    string PaymentMethod
);

public record PayrollRunListItemDto(
    long PayrollRunId,
    string PayPeriod,
    long? BranchId,
    string Status,
    DateTime RunDate,
    int EmployeeCount,
    decimal TotalGross,
    decimal TotalNet,
    int CorrectionVersion);

public record ClosePayrollPeriodRequest(long CompanyId, string Reason);
public record ReopenPayrollPeriodRequest(long CompanyId, string Reason);

public record PayrollRunComparisonDto(
    long CurrentRunId,
    string CurrentPayPeriod,
    long? CurrentBranchId,
    int CurrentCorrectionVersion,
    string CurrentStatus,
    long? BaselineRunId,
    string? BaselinePayPeriod,
    int? BaselineCorrectionVersion,
    string? BaselineStatus,
    PayrollComparisonSummaryDto Summary,
    List<PayrollEmployeeVarianceDto> EmployeeVariances
);

public record PayrollComparisonSummaryDto(
    int CurrentEmployeeCount,
    int BaselineEmployeeCount,
    int AddedEmployeesCount,
    int RemovedEmployeesCount,
    int RetainedEmployeesCount,
    decimal CurrentGrossSalary,
    decimal BaselineGrossSalary,
    decimal GrossSalaryVariance,
    decimal CurrentNetPay,
    decimal BaselineNetPay,
    decimal NetPayVariance,
    decimal CurrentBasicSalary,
    decimal BaselineBasicSalary,
    decimal BasicSalaryVariance,
    decimal CurrentTotalEarnings,
    decimal BaselineTotalEarnings,
    decimal TotalEarningsVariance,
    decimal CurrentTotalDeductions,
    decimal BaselineTotalDeductions,
    decimal TotalDeductionsVariance,
    decimal CurrentSscEmployee,
    decimal BaselineSscEmployee,
    decimal SscEmployeeVariance,
    decimal CurrentTaxWithheld,
    decimal BaselineTaxWithheld,
    decimal TaxWithheldVariance
);

public record PayrollEmployeeVarianceDto(
    string EmployeeCode,
    string EmployeeName,
    string ChangeType, // "NEW", "REMOVED", "INCREASED", "DECREASED", "UNCHANGED"
    decimal PreviousGross,
    decimal CurrentGross,
    decimal GrossVariance,
    decimal PreviousNetPay,
    decimal CurrentNetPay,
    decimal NetPayVariance,
    decimal PreviousBasic,
    decimal CurrentBasic,
    string PrimaryReason
);

public record PayrollExplanationDto(
    long RunLineId,
    string EmployeeCode,
    string EmployeeName,
    string PayPeriod,
    decimal BaseSalary,
    decimal ProrationFactor,
    string ProrationMethod,
    decimal ProratedBase,
    decimal OvertimeHours,
    decimal OvertimeEarnings,
    decimal GrossPay,
    decimal SscEligibleSalary,
    decimal SscEmployeeShare,
    decimal SscEmployerShare,
    decimal TaxableGross,
    decimal TaxExemptions,
    decimal IncomeTax,
    decimal NationalSolidarityContrib,
    decimal ScheduledLoanDeduction,
    decimal ActualLoanDeduction,
    decimal CarriedForwardDeduction,
    decimal NetPay,
    string DetailsJson
);

public record PostGlVoucherResultDto(
    long PayrollRunId,
    long JournalVoucherId,
    decimal TotalDebit,
    decimal TotalCredit,
    bool IsBalanced,
    DateTime PostedAt
);

public record PayrollValidationResultDto(
    bool IsValid,
    List<string> Errors,
    List<string> Warnings
);

public sealed record CreatePayrollPeriodDto(
    [Required]
    [StringLength(20, MinimumLength = 1)]
    string PeriodCode,
    [Required]
    [StringLength(200, MinimumLength = 1)]
    string NameAr,
    [Required]
    [StringLength(200, MinimumLength = 1)]
    string NameEn,
    int FiscalYear,
    int Month,
    DateTime StartDate,
    DateTime EndDate,
    DateTime PayDate,
    [Required]
    [StringLength(50, MinimumLength = 1)]
    string PayrollType
);

// Bank Export DTOs
public record BankPayrollExportRecordDto(
    int SequenceNo,
    string EmployeeCode,
    string NationalId,
    string FullName,
    string BankCode,
    string Iban,
    string Currency,
    decimal NetAmount,
    string Remarks
);

public record BankPayrollExportResultDto(
    long PayrollRunId,
    string PayPeriod,
    string Format,
    int TotalRecords,
    decimal TotalAmount,
    byte[] FileBytes,
    string FileName,
    string ContentType
);

public sealed record WithdrawHandoffRequest(string Reason);
public sealed record PayrollCorrectionRequest(string Reason);
