using System;
using System.ComponentModel.DataAnnotations;
using System.Collections.Generic;

namespace ThinkOnErp.Application.DTOs.Hr;

public sealed record EmployeeTeamMemberDto(string EmployeeCode, string NameLocal, string NameEn, string? DepartmentCode,
    string? PositionCode, long? BranchId, string EmploymentStatus, bool IsActive);

// Work Calendar & Holidays DTOs
public record CreateWorkCalendarDto(
    long CompanyId,
    string Code,
    string NameAr,
    string NameEn,
    DateTime EffectiveFrom,
    DateTime? EffectiveTo,
    bool IsDefault,
    List<WorkCalendarDayDto>? Days
);

public record WorkCalendarDayDto(
    int DayOfWeek,
    bool IsWorkingDay,
    decimal StandardWorkingHours,
    string? DefaultShiftCode
);

public record CreatePublicHolidayDto(
    long CompanyId,
    long? BranchId,
    string NameAr,
    string NameEn,
    DateTime HolidayDate,
    bool IsPaid,
    bool IsRecurring,
    string? Description
);

// Attendance DTOs
public record RawPunchInputDto(
    string EmployeeCode,
    DateTime PunchTime,
    string PunchType,
    string? DeviceId,
    string? Source
);

public record AttendanceCorrectionRequestDto(
    string EmployeeCode,
    DateTime AttendanceDate,
    DateTime? RequestedCheckIn,
    DateTime? RequestedCheckOut,
    string Reason
);

public record ProcessAttendanceCorrectionDto(
    bool Approved,
    string? RejectionReason
);

public record DailyAttendanceSummaryDto(
    string EmployeeCode,
    string EmployeeName,
    DateTime Date,
    decimal ScheduledHours,
    decimal ActualWorkedHours,
    int LateArrivalMinutes,
    int EarlyLeaveMinutes,
    decimal OvertimeHours,
    string Status,
    bool HasMissingPunch
);

// Dynamic Policies DTOs
public record AttendancePolicyDto(
    long CompanyId,
    string Code,
    string NameAr,
    string NameEn,
    DateTime EffectiveFrom,
    DateTime? EffectiveTo,
    int GracePeriodMinutes,
    int EarlyLeaveToleranceMinutes,
    int MinMinutesForOvertime,
    bool AutoDeductLateArrival,
    string MissingPunchHandling,
    bool IsDefault
);

public sealed record CreateAttendancePolicyDto(
    string Code, string NameAr, string NameEn, DateTime EffectiveFrom, DateTime? EffectiveTo,
    int GracePeriodMinutes, int EarlyLeaveToleranceMinutes, int MinMinutesForOvertime,
    bool AutoDeductLateArrival, string MissingPunchHandling, bool AllowMultipleSessions,
    bool IsDefault, bool EnforceGeofence, int DefaultAllowedRadiusMeters,
    string GeofenceViolationAction, bool AllowAnyBranchPunch);

public sealed record CreateEndOfServicePolicyDto(
    string PolicyName, DateTime EffectiveFrom, DateTime? EffectiveTo, string CalculationBasis,
    decimal LeaveDailyRateDivisor, int NoticePeriodDays, bool ProrateFinalMonthByWorkingDays,
    decimal ResignationFactorUnderTwoYears, decimal ResignationFactorTwoToFiveYears,
    decimal ResignationFactorFiveToTenYears, decimal ResignationFactorTenYearsAndAbove,
    decimal FirstFiveYearsRatePerYear, decimal SubsequentYearsRatePerYear);

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

// Loans & Advances DTOs
public record CreateLoanApplicationDto(
    string EmployeeCode,
    string LoanType,
    decimal PrincipalAmount,
    int TotalInstallments,
    DateTime StartDate,
    string? Notes,
    [property: Required] string? CurrencyCode = null
);

public record CreateAdvanceRequestDto(
    string EmployeeCode,
    decimal AdvanceAmount,
    string TargetPayPeriod,
    string? Reason,
    [property: Required] string? CurrencyCode = null
);

public record LoanRepaymentScheduleDto(
    long Id,
    int InstallmentNo,
    string PayPeriod,
    decimal ScheduledAmount,
    decimal PaidAmount,
    decimal CarriedForwardAmount,
    string Status,
    DateTime? PaidDate
);

public record SkipLoanInstallmentDto(string Reason);
public record CancelLoanDto(string Reason);
public record CancelAdvanceDto(string Reason);
public record SettleLoanEarlyDto(decimal SettleAmount, string? PaymentReference, string? Notes, DateTime? SettleDate = null);
public record SettleAdvanceEarlyDto(decimal SettleAmount, string? PaymentReference, string? Notes, DateTime? SettleDate = null);

// Payroll Adjustment (Variable Earnings / Deductions) DTOs
public record CreatePayrollAdjustmentDto(
    long CompanyId,
    string EmployeeCode,
    string PayPeriod,
    string ComponentCode,
    string AdjustmentType, // EARNING / DEDUCTION
    decimal Amount,
    string? Description
);

public record PayrollAdjustmentDecisionDto(string Reason, long? BranchId = null);

public record CreateOvertimePayrollAdjustmentDto(
    long CompanyId,
    string EmployeeCode,
    DateTime AttendanceDate,
    string PayPeriod,
    string ComponentCode,
    long BranchId
);

public record PayrollAdjustmentDto(
    long Id,
    long CompanyId,
    string EmployeeCode,
    string EmployeeName,
    string PayPeriod,
    string ComponentCode,
    string ComponentName,
    string AdjustmentType,
    decimal Amount,
    string? Description,
    string Status,
    string? DecisionReason = null,
    string? SourceType = null,
    string? SourceId = null,
    long? SourceBranchId = null,
    DateTime? SourceAttendanceDate = null,
    decimal? SourceOvertimeHours = null,
    decimal? SourceBaseSalary = null,
    string? SourceDayType = null,
    string? SourceCurrencyCode = null
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

public record CreateEndOfServiceRequest(
    string EmployeeCode,
    string TerminationType,
    DateTime LastWorkingDate,
    DateTime? NoticeDate = null,
    int NoticeCompensationDays = 0,
    decimal OtherEntitlements = 0m,
    decimal OtherDeductions = 0m,
    string? CalculationNotes = null
);

public record CancelEndOfServiceRequest(string Reason);

public record EndOfServiceSettlementDto(
    long Id,
    long CompanyId,
    long? BranchId,
    string EmployeeCode,
    string EmployeeName,
    string TerminationType,
    string? TerminationReason,
    DateTime? NoticeDate,
    DateTime LastWorkingDate,
    DateTime SettlementDate,
    decimal ServiceYears,
    int ServiceMonths,
    int ServiceDays,
    decimal BaseSalarySnapshot,
    decimal GrossSalarySnapshot,
    string CurrencyCode,
    decimal SeverancePay,
    decimal UnpaidSalary,
    decimal LeaveEncashment,
    decimal UnusedLeaveDays,
    decimal NoticePeriodPay,
    decimal OtherEntitlements,
    decimal TotalEntitlements,
    decimal OutstandingLoans,
    decimal OutstandingAdvances,
    decimal NegativeLeaveDeduction,
    decimal OtherDeductions,
    decimal TotalDeductions,
    decimal NetSettlementAmount,
    string Status,
    string? CalculationNotes,
    string? ApprovedBy,
    DateTime? ApprovedAtUtc,
    string? CancelledBy,
    DateTime? CancelledAtUtc,
    string? CancellationReason,
    List<EndOfServiceLineDto> Lines
);

public record EndOfServiceLineDto(
    long Id,
    string ComponentCode,
    string ComponentType,
    decimal Amount,
    string? Description
);

public record EndOfServiceHandoffStatusDto(
    long HandoffId,
    string HandoffReference,
    string DeliveryStatus,
    string Operation,
    int SourceVersion,
    DateTime CreatedAtUtc
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

// Employee Management DTOs
public record LinkEmployeeUserDto(long? UserId);
public record EmployeeUserLinkDto(string EmployeeCode, long? UserId);

public record CreateEmployeeDto(
    string EmployeeCode,
    string NameLocal,
    string NameEn,
    string NationalId,
    string Nationality,
    string? PassportNumber,
    DateTime DateOfBirth,
    string Gender,
    string MaritalStatus,
    string? Email,
    string? Phone,
    DateTime HireDate,
    DateTime? ProbationEndDate,
    string EmploymentType,
    string EmploymentStatus,
    string? DepartmentCode,
    string? PositionCode,
    long? BranchId,
    string? ManagerEmployeeCode,
    string? SscNumber,
    int TaxExemptionCount,
    bool IsHighRiskRole,
    string? BankName,
    string? BankAccountNumber,
    string? BankIban,
    DateTime? TerminationDate = null,
    string? TerminationReason = null,
    string? CitizenshipCountryCode = null
);

public record UpdateEmployeeDto(
    string NameLocal,
    string NameEn,
    string NationalId,
    string Nationality,
    string? PassportNumber,
    DateTime DateOfBirth,
    string Gender,
    string MaritalStatus,
    string? Email,
    string? Phone,
    DateTime HireDate,
    DateTime? ProbationEndDate,
    DateTime? TerminationDate,
    string? TerminationReason,
    string EmploymentType,
    string EmploymentStatus,
    string? DepartmentCode,
    string? PositionCode,
    long? BranchId,
    string? ManagerEmployeeCode,
    string? SscNumber,
    int TaxExemptionCount,
    bool IsHighRiskRole,
    string? BankName,
    string? BankAccountNumber,
    string? BankIban,
    bool IsActive,
    string? CitizenshipCountryCode = null
);

public record EmployeeSummaryDto(
    string EmployeeCode,
    string NameLocal,
    string NameEn,
    string? NationalId,
    string? DepartmentCode,
    string? PositionCode,
    string EmploymentStatus,
    decimal? BasicSalary,
    string? Phone,
    string? Email,
    long? UserId = null,
    long? BranchId = null, EmployeeFieldVisibilityDto? FieldVisibility = null
);

public record EmployeeDetailsDto(
    string EmployeeCode,
    string NameLocal,
    string NameEn,
    string? NationalId,
    string? Nationality,
    string? PassportNumber,
    DateTime? DateOfBirth,
    string? Gender,
    string? MaritalStatus,
    string? Email,
    string? Phone,
    DateTime HireDate,
    DateTime? ProbationEndDate,
    DateTime? TerminationDate,
    string? TerminationReason,
    string EmploymentType,
    string EmploymentStatus,
    string? DepartmentCode,
    string? PositionCode,
    long? BranchId,
    string? ManagerEmployeeCode,
    string? SscNumber,
    int? TaxExemptionCount,
    bool? IsHighRiskRole,
    string? BankName,
    string? BankAccountNumber,
    string? BankIban,
    bool IsActive,
    List<DependentDto> Dependents,
    SalaryStructureDetailsDto? ActiveSalaryStructure,
    long? UserId = null,
    EmployeeFieldVisibilityDto? FieldVisibility = null
)
{
    public string? CitizenshipCountryCode { get; init; }
}

public record CreateDependentDto(
    string NameLocal,
    string NameEn,
    string Relationship,
    DateTime DateOfBirth,
    string Gender,
    string? NationalId,
    bool IsTaxExemptionClaimed,
    bool IsMedicalCovered
);

public record DependentDto(
    long Id,
    string EmployeeCode,
    string NameLocal,
    string NameEn,
    string Relationship,
    DateTime DateOfBirth,
    string Gender,
    string? NationalId,
    bool IsTaxExemptionClaimed,
    bool IsMedicalCovered,
    bool IsActive
);

// Salary Structure & Components DTOs
public record AssignSalaryStructureDto(
    DateTime EffectiveFrom,
    DateTime? EffectiveTo,
    decimal BasicSalary,
    string CurrencyCode,
    string PaymentMethod,
    List<SalaryStructureLineDto>? Lines
);

public record SalaryStructureLineDto(
    string ComponentCode,
    decimal? Amount,
    decimal? Percent
);

public record SalaryStructureDetailsDto(
    long Id,
    string EmployeeCode,
    DateTime EffectiveFrom,
    DateTime? EffectiveTo,
    decimal BasicSalary,
    string CurrencyCode,
    string PaymentMethod,
    bool IsActive,
    List<SalaryStructureLineDetailDto> Lines
);

public record SalaryStructureLineDetailDto(
    long Id,
    string ComponentCode,
    string ComponentNameLocal,
    string ComponentNameEn,
    string ComponentType,
    decimal Amount,
    decimal? Percent,
    bool IsActive
);

public record CreateSalaryComponentDto(
    string ComponentCode,
    string NameLocal,
    string NameEn,
    string ComponentType, // BASIC, ALLOWANCE, DEDUCTION, EMPLOYER_CONTRIB
    string CalculationType, // FIXED, PERCENTAGE, FORMULA
    decimal? DefaultAmount,
    decimal? DefaultPercent,
    bool IsTaxable,
    bool IsSscApplicable,
    string? GlAccountCode
);

public record SalaryComponentDto(
    string ComponentCode,
    string NameLocal,
    string NameEn,
    string ComponentType,
    string CalculationType,
    decimal? DefaultAmount,
    decimal? DefaultPercent,
    bool IsTaxable,
    bool IsSscApplicable,
    string? GlAccountCode,
    bool IsActive
);

// Leaves & Absence DTOs
public record LeaveTypeDto(
    string LeaveTypeCode,
    string NameLocal,
    string NameEn,
    bool IsPaid,
    bool IsStatutory,
    decimal MaxDaysPerYear,
    bool CarryForwardAllowed,
    decimal CarryForwardCapDays,
    bool RequiresDocumentation,
    bool IsActive,
    bool TracksBalance,
    string YearEndExcessAction
);

public record CreateLeaveTypeDto(
    string LeaveTypeCode,
    string NameLocal,
    string NameEn,
    bool IsPaid = true,
    bool IsStatutory = true,
    decimal MaxDaysPerYear = 14m,
    bool CarryForwardAllowed = false,
    decimal CarryForwardCapDays = 0m,
    bool RequiresDocumentation = false,
    bool IsActive = true,
    bool TracksBalance = true,
    string YearEndExcessAction = "EXPIRE"
);

public record UpdateLeaveTypeDto(
    string NameLocal,
    string NameEn,
    bool IsPaid,
    bool IsStatutory,
    decimal MaxDaysPerYear,
    bool CarryForwardAllowed,
    decimal CarryForwardCapDays,
    bool RequiresDocumentation,
    bool IsActive,
    bool? TracksBalance = null,
    string? YearEndExcessAction = null
);

public record SubmitLeaveRequestDto(
    string EmployeeCode,
    string LeaveTypeCode,
    DateTime StartDate,
    DateTime EndDate,
    decimal DaysRequested,
    string? Reason,
    string? AttachmentFileRef
);

public sealed record CreatePayrollPeriodDto(
    [global::System.ComponentModel.DataAnnotations.Required]
    [global::System.ComponentModel.DataAnnotations.StringLength(20, MinimumLength = 1)]
    string PeriodCode,
    [global::System.ComponentModel.DataAnnotations.Required]
    [global::System.ComponentModel.DataAnnotations.StringLength(200, MinimumLength = 1)]
    string NameAr,
    [global::System.ComponentModel.DataAnnotations.Required]
    [global::System.ComponentModel.DataAnnotations.StringLength(200, MinimumLength = 1)]
    string NameEn,
    int FiscalYear,
    int Month,
    DateTime StartDate,
    DateTime EndDate,
    DateTime PayDate,
    [global::System.ComponentModel.DataAnnotations.Required]
    [global::System.ComponentModel.DataAnnotations.StringLength(50, MinimumLength = 1)]
    string PayrollType
);

public record ProcessLeaveRequestDto(
    bool Approved,
    string? RejectionReason
);

public record CancelLeaveRequestDto(
    [global::System.ComponentModel.DataAnnotations.Required]
    [global::System.ComponentModel.DataAnnotations.StringLength(500, MinimumLength = 1)]
    string Reason
);

public record LeaveRequestDto(
    long Id,
    string EmployeeCode,
    string EmployeeName,
    string LeaveTypeCode,
    string LeaveTypeName,
    DateTime StartDate,
    DateTime EndDate,
    decimal DaysRequested,
    string Status,
    string? Reason,
    string? ApprovedBy,
    DateTime? ApprovalDate,
    string? RejectionReason,
    DateTime CreationDate
)
{
    public string? CancelledBy { get; init; }
    public DateTime? CancellationDate { get; init; }
    public string? CancellationReason { get; init; }
}

public record LeaveDaysCalculationDto(
    string EmployeeCode,
    string LeaveTypeCode,
    DateTime StartDate,
    DateTime EndDate,
    int CalendarDays,
    int WorkingDays,
    decimal AvailableBalance,
    bool HasSufficientBalance,
    DateTime? CycleStartDate,
    DateTime? CycleEndDate
);

public record LeaveBalanceDto(
    string EmployeeCode,
    string LeaveTypeCode,
    string LeaveTypeName,
    int YearNo,
    decimal AccruedDays,
    decimal UsedDays,
    decimal CarriedForwardDays,
    decimal RemainingDays
)
{
    public decimal PendingDays { get; init; }
    public decimal AvailableDays { get; init; } = RemainingDays;
    public DateTime? CycleStartDate { get; init; }
    public DateTime? CycleEndDate { get; init; }
    public string DisplayFormat { get; init; } = "DECIMAL";
    public string? AccruedDisplay { get; init; }
    public string? UsedDisplay { get; init; }
    public string? CarriedForwardDisplay { get; init; }
    public string? RemainingDisplay { get; init; }
    public string? PendingDisplay { get; init; }
    public string? AvailableDisplay { get; init; }
}

public record HrLeaveSettingsDto(
    int LeaveYearStartMonth,
    int LeaveYearStartDay,
    string BalanceDisplayFormat,
    decimal? StandardWorkdayHours,
    bool AutomaticAccrualEnabled = false,
    string? AccrualTimeZoneId = null,
    string? EligibleCitizenshipCountryCode = null,
    string? LeaveCashoutWageBasisCode = null,
    string? LeaveCashoutBasisComponentCode = null,
    decimal? LeaveCashoutDayDivisor = null,
    string? LeaveCashoutPayComponentCode = null
);

public record UpdateHrLeaveSettingsDto(
    int LeaveYearStartMonth,
    int LeaveYearStartDay,
    string BalanceDisplayFormat,
    decimal? StandardWorkdayHours,
    bool? AutomaticAccrualEnabled = null,
    string? AccrualTimeZoneId = null,
    string? EligibleCitizenshipCountryCode = null,
    string? LeaveCashoutWageBasisCode = null,
    string? LeaveCashoutBasisComponentCode = null,
    decimal? LeaveCashoutDayDivisor = null,
    string? LeaveCashoutPayComponentCode = null
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

// Employee Excel Import/Export DTOs
public sealed class EmployeeImportResultDto
{
    public DateTime? PreviewGeneratedAtUtc { get; set; }
    public List<EmployeeImportPreviewDto> PreviewRows { get; set; } = new();
    public int TotalRows { get; set; }
    public int SuccessCount { get; set; }
    public int FailureCount { get; set; }
    public int InsertedCount { get; set; }
    public int UpdatedCount { get; set; }
    public bool IsValid => FailureCount == 0;
    public List<EmployeeImportErrorDto> Errors { get; set; } = new();
    public List<string> Warnings { get; set; } = new();
}

public sealed record EmployeeImportErrorDto(
    int RowNumber,
    string? Column,
    string Code,
    string Message
);

public sealed record EmployeeImportPreviewDto(int RowNumber,string EmployeeCode,string Action,bool RequiresUpdateExisting,
    string SalaryChange,DateTime? SalaryEffectiveFrom,DateTime AffectedFrom,
    IReadOnlyList<ThinkOnErp.Domain.Entities.Hr.EmployeeImportPeriodImpact> AffectedPeriods,
    bool HasUnmappedApprovedPayroll,IReadOnlyList<string> BlockingReasons,string? CurrencyCode=null,string? PaymentMethod=null);

public sealed class EmployeeImportRowDto
{
    [global::System.Text.Json.Serialization.JsonIgnore]
    public IReadOnlySet<string>? PresentColumns { get; set; }
    [global::System.Text.Json.Serialization.JsonIgnore]
    public IReadOnlySet<string>? SuppliedFields { get; set; }
    public int RowNumber { get; set; }
    public string EmployeeCode { get; set; } = string.Empty;
    public string NameLocal { get; set; } = string.Empty;
    public string NameEn { get; set; } = string.Empty;
    public string NationalId { get; set; } = string.Empty;
    public string Nationality { get; set; } = string.Empty;
    public string? CitizenshipCountryCode { get; set; }
    public string? PassportNumber { get; set; }
    public DateTime? DateOfBirth { get; set; }
    public string Gender { get; set; } = "M";
    public string MaritalStatus { get; set; } = "SINGLE";
    public string? Email { get; set; }
    public string? Phone { get; set; }
    public DateTime? HireDate { get; set; }
    public DateTime? ProbationEndDate { get; set; }
    public DateTime? TerminationDate { get; set; }
    public string? TerminationReason { get; set; }
    public string EmploymentType { get; set; } = "FULL_TIME";
    public string EmploymentStatus { get; set; } = "ACTIVE";
    public string? DepartmentCode { get; set; }
    public string? PositionCode { get; set; }
    public long? BranchId { get; set; }
    public string? ManagerEmployeeCode { get; set; }
    public string? SscNumber { get; set; }
    public int TaxExemptionCount { get; set; }
    public bool IsHighRiskRole { get; set; }
    public string? BankName { get; set; }
    public string? BankAccountNumber { get; set; }
    public string? BankIban { get; set; }
    public string? CurrencyCode { get; set; }
    public string? PaymentMethod { get; set; }
    public decimal? BasicSalary { get; set; }
    public DateTime? SalaryEffectiveFrom { get; set; }
    public bool IsActive { get; set; } = true;
}

public sealed class EmployeeExportRowDto
{
    public string EmployeeCode { get; set; } = string.Empty;
    public string NameLocal { get; set; } = string.Empty;
    public string NameEn { get; set; } = string.Empty;
    public string NationalId { get; set; } = string.Empty;
    public string Nationality { get; set; } = string.Empty;
    public string? CitizenshipCountryCode { get; set; }
    public string? PassportNumber { get; set; }
    public DateTime DateOfBirth { get; set; }
    public string Gender { get; set; } = "M";
    public string MaritalStatus { get; set; } = "SINGLE";
    public string? Email { get; set; }
    public string? Phone { get; set; }
    public DateTime HireDate { get; set; }
    public DateTime? ProbationEndDate { get; set; }
    public DateTime? TerminationDate { get; set; }
    public string? TerminationReason { get; set; }
    public string EmploymentType { get; set; } = "FULL_TIME";
    public string EmploymentStatus { get; set; } = "ACTIVE";
    public string? DepartmentCode { get; set; }
    public string? PositionCode { get; set; }
    public long? BranchId { get; set; }
    public string? ManagerEmployeeCode { get; set; }
    public string? SscNumber { get; set; }
    public int TaxExemptionCount { get; set; }
    public bool IsHighRiskRole { get; set; }
    public string? BankName { get; set; }
    public string? BankAccountNumber { get; set; }
    public string? BankIban { get; set; }
    public string? CurrencyCode { get; set; }
    public string? PaymentMethod { get; set; }
    public decimal? BasicSalary { get; set; }
    public DateTime? SalaryEffectiveFrom { get; set; }
    public bool IsActive { get; set; } = true;
}

// Vacation Accrual, Grant & Rollover DTOs
public record GrantLeaveBalanceDto(
    string EmployeeCode,
    string LeaveTypeCode,
    int YearNo,
    decimal Days,
    [global::System.ComponentModel.DataAnnotations.Required]
    [global::System.ComponentModel.DataAnnotations.StringLength(30, MinimumLength = 1)]
    string AdjustmentType, // OPENING_BALANCE, MANUAL_GRANT, COMPENSATORY, CORRECTION
    [global::System.ComponentModel.DataAnnotations.Required]
    [global::System.ComponentModel.DataAnnotations.StringLength(1000, MinimumLength = 1)]
    string? Reason,
    [global::System.ComponentModel.DataAnnotations.Required]
    [global::System.ComponentModel.DataAnnotations.StringLength(200, MinimumLength = 1)]
    string? IdempotencyKey = null
);

public record ReverseLeaveBalanceMovementDto(
    [global::System.ComponentModel.DataAnnotations.Required]
    [global::System.ComponentModel.DataAnnotations.StringLength(1000, MinimumLength = 1)]
    string Reason
);

public record YearlyLeaveAllocationDto(
    int YearNo,
    string? LeaveTypeCode,
    string? DepartmentCode,
    string? EmployeeCode,
    bool ProrateForMidYearHires,
    DateTime? AsOfDate = null
);

public record MonthlyLeaveAccrualDto(
    int YearNo,
    int MonthNo,
    string? LeaveTypeCode,
    string? EmployeeCode
);

public record DailyLeaveAccrualDto(
    DateTime AsOfDate,
    string? LeaveTypeCode,
    string? DepartmentCode,
    string? EmployeeCode
);

public record YearEndRolloverDto(
    int FromYear,
    int ToYear,
    string? LeaveTypeCode
);

public record LeaveYearCloseLineDto(
    string EmployeeCode,
    string LeaveTypeCode,
    int SourceYear,
    int TargetYear,
    DateTime SourceCycleStartDate,
    DateTime SourceCycleEndDate,
    DateTime TargetCycleStartDate,
    DateTime TargetCycleEndDate,
    decimal UnusedDays,
    decimal CarriedDays,
    decimal ExpiredDays,
    decimal CashoutDays
);

public record LeaveYearCloseDto(
    long Id,
    int FromYear,
    int ToYear,
    string LeaveTypeCode,
    string Status,
    string CreationUser,
    DateTime CreationDate,
    string? PostedBy,
    DateTime? PostedDate,
    string? UnpostedBy,
    DateTime? UnpostedDate,
    decimal TotalUnusedDays,
    decimal TotalCarriedDays,
    decimal TotalExpiredDays,
    decimal TotalCashoutDays,
    IReadOnlyList<LeaveYearCloseLineDto> Lines
);

public record CreateLeaveCashoutSettlementDto(
    [global::System.ComponentModel.DataAnnotations.Required]
    [global::System.ComponentModel.DataAnnotations.StringLength(200, MinimumLength = 1)]
    string SettlementKey,
    long LeaveYearCloseId,
    [global::System.ComponentModel.DataAnnotations.Required]
    [global::System.ComponentModel.DataAnnotations.StringLength(40, MinimumLength = 1)]
    string PayPeriod,
    [global::System.ComponentModel.DataAnnotations.Required]
    [global::System.ComponentModel.DataAnnotations.StringLength(20, MinimumLength = 1)]
    string CurrencyCode
);

public record LeaveCashoutSettlementLineDto(
    long LeaveYearCloseLineId,
    string EmployeeCode,
    string LeaveTypeCode,
    decimal CashoutDays,
    long SalaryStructureId,
    DateTime SalaryEffectiveFrom,
    decimal WageBasisAmount,
    decimal DayDivisor,
    decimal CashoutAmount,
    string CurrencyCode,
    string BasisSnapshotJson,
    long? PayrollAdjustmentId
);

public record LeaveCashoutSettlementDto(
    long Id,
    string SettlementKey,
    long LeaveYearCloseId,
    string PayPeriod,
    string CurrencyCode,
    string WageBasisCode,
    string? BasisComponentCode,
    decimal DayDivisor,
    string PayComponentCode,
    string Status,
    string CreationUser,
    DateTime CreationDate,
    string? PostedBy,
    DateTime? PostedDate,
    string? ReversedBy,
    DateTime? ReversedDate,
    string? ReversalReason,
    decimal TotalCashoutDays,
    decimal TotalAmount,
    IReadOnlyList<LeaveCashoutSettlementLineDto> Lines
);

public record ReverseLeaveCashoutSettlementDto(
    [global::System.ComponentModel.DataAnnotations.Required]
    [global::System.ComponentModel.DataAnnotations.StringLength(1000, MinimumLength = 1)]
    string Reason
);

public record LeaveBalanceMigrationAllocationDto(
    long SourceBalanceId,
    DateTime TargetCycleStartDate,
    DateTime TargetCycleEndDate,
    decimal AccruedDays,
    decimal UsedDays,
    decimal CarriedDays
);

public record CreateLeaveBalanceMigrationDto(
    [global::System.ComponentModel.DataAnnotations.Required]
    [global::System.ComponentModel.DataAnnotations.StringLength(200, MinimumLength = 1)]
    string MigrationKey,
    DateTime EffectiveDate,
    int TargetYearStartMonth,
    int TargetYearStartDay,
    [global::System.ComponentModel.DataAnnotations.Required]
    [global::System.ComponentModel.DataAnnotations.MinLength(1)]
    IReadOnlyList<LeaveBalanceMigrationAllocationDto> Allocations
);

public record LeaveBalanceMigrationLineDto(
    long SourceBalanceId,
    long? TargetBalanceId,
    string EmployeeCode,
    string LeaveTypeCode,
    DateTime SourceCycleStartDate,
    DateTime SourceCycleEndDate,
    decimal SourceAccruedDays,
    decimal SourceUsedDays,
    decimal SourceCarriedDays,
    DateTime TargetCycleStartDate,
    DateTime TargetCycleEndDate,
    decimal AllocatedAccruedDays,
    decimal AllocatedUsedDays,
    decimal AllocatedCarriedDays
);

public record LeaveBalanceMigrationDto(
    long Id,
    string MigrationKey,
    DateTime EffectiveDate,
    int SourceYearStartMonth,
    int SourceYearStartDay,
    int TargetYearStartMonth,
    int TargetYearStartDay,
    string Status,
    string CreationUser,
    DateTime CreationDate,
    string? PostedBy,
    DateTime? PostedDate,
    string? ReversedBy,
    DateTime? ReversedDate,
    IReadOnlyList<LeaveBalanceMigrationLineDto> Lines
);

public record LeaveAccrualResultDto(
    int TotalEmployeesProcessed,
    int UpdatedBalancesCount,
    decimal TotalDaysAccrued,
    List<string> Details
);

public record LeaveRolloverResultDto(
    int TotalEmployeesProcessed,
    int RolloverBalancesCount,
    decimal TotalDaysCarriedForward,
    decimal TotalDaysExpired,
    List<string> Details
);

public record LeavePolicyDetailsDto(
    long Id,
    string PolicyName,
    string LeaveTypeCode,
    string AccrualMethod,
    decimal AccrualRate,
    string ApplicableTo,
    int MinServiceMonths,
    int Tier1YearsThreshold,
    decimal Tier1Days,
    decimal Tier2Days,
    bool IsActive,
    decimal? AnnualEntitlementDays = null,
    string CycleBasis = "CALENDAR_YEAR",
    bool ProrateFirstCycle = false,
    DateTime? EffectiveFrom = null,
    DateTime? EffectiveTo = null
);

public record CreateLeavePolicyDto(
    string PolicyName,
    string LeaveTypeCode,
    string AccrualMethod,
    decimal AccrualRate,
    string ApplicableTo,
    int MinServiceMonths,
    int Tier1YearsThreshold,
    decimal Tier1Days,
    decimal Tier2Days,
    bool IsActive,
    decimal? AnnualEntitlementDays = null,
    string CycleBasis = "CALENDAR_YEAR",
    bool ProrateFirstCycle = false,
    DateTime? EffectiveFrom = null
);




public sealed record EmployeeFieldVisibilityDto(bool Identity,bool Bank,bool Salary,bool EditIdentity,bool EditBank,bool EditSalary);

[global::System.Text.Json.Serialization.JsonUnmappedMemberHandling(global::System.Text.Json.Serialization.JsonUnmappedMemberHandling.Disallow)]
public sealed record UpdateEmployeeWorkDto(string NameLocal,string NameEn,DateTime HireDate,DateTime? ProbationEndDate,
    DateTime? TerminationDate,string? TerminationReason,string EmploymentType,string EmploymentStatus,string? DepartmentCode,
    string? PositionCode,long? BranchId,string? ManagerEmployeeCode,[property: global::System.Text.Json.Serialization.JsonRequired] bool IsActive);

[global::System.Text.Json.Serialization.JsonUnmappedMemberHandling(global::System.Text.Json.Serialization.JsonUnmappedMemberHandling.Disallow)]
public sealed record UpdateEmployeeBankDto(
    [property: global::System.Text.Json.Serialization.JsonRequired] string? BankName,
    [property: global::System.Text.Json.Serialization.JsonRequired] string? BankAccountNumber,
    [property: global::System.Text.Json.Serialization.JsonRequired] string? BankIban);

[global::System.Text.Json.Serialization.JsonUnmappedMemberHandling(global::System.Text.Json.Serialization.JsonUnmappedMemberHandling.Disallow)]
public sealed record UpdateEmployeeIdentityDto(
    [property: global::System.Text.Json.Serialization.JsonRequired] string NationalId,
    [property: global::System.Text.Json.Serialization.JsonRequired] string Nationality,
    [property: global::System.Text.Json.Serialization.JsonRequired] string? PassportNumber,
    [property: global::System.Text.Json.Serialization.JsonRequired] DateTime DateOfBirth,
    [property: global::System.Text.Json.Serialization.JsonRequired] string Gender,
    [property: global::System.Text.Json.Serialization.JsonRequired] string MaritalStatus,
    [property: global::System.Text.Json.Serialization.JsonRequired] string? Email,
    [property: global::System.Text.Json.Serialization.JsonRequired] string? Phone,
    [property: global::System.Text.Json.Serialization.JsonRequired] string? SscNumber,
    [property: global::System.Text.Json.Serialization.JsonRequired] int TaxExemptionCount,
    [property: global::System.Text.Json.Serialization.JsonRequired] bool IsHighRiskRole,
    string? CitizenshipCountryCode = null);

// Short Leave / Permissions DTOs (PERM-01..05)
public record CreateShortLeaveDto(
    string EmployeeCode,
    string PermissionType,
    DateTime PermissionDate,
    TimeSpan StartTime,
    TimeSpan EndTime,
    string Reason,
    long CompanyId = 0,
    long? BranchId = null
);

public record RejectShortLeaveDto(string Reason);
public record CancelShortLeaveDto(string Reason);

public record ShortLeaveDto(
    long Id,
    long CompanyId,
    long? BranchId,
    string EmployeeCode,
    string? EmployeeName,
    string PermissionType,
    DateTime PermissionDate,
    TimeSpan StartTime,
    TimeSpan EndTime,
    int DurationMinutes,
    string Reason,
    string Status,
    string CompensationType,
    decimal DeductedSalaryAmount,
    decimal ConvertedLeaveDays,
    string? ApprovedBy,
    DateTime? ApprovedAtUtc,
    string? RejectedBy,
    DateTime? RejectedAtUtc,
    string? RejectionReason,
    string? CancelledBy,
    DateTime? CancelledAtUtc,
    string? CancellationReason,
    DateTime CreationDate
);

public record ShortLeavePolicyDto(
    long Id,
    long CompanyId,
    int MonthlyPaidMinutesQuota,
    int MaxMinutesPerRequest,
    bool AllowMiddayExit,
    string ExcessAction,
    string LeaveTypeCodeForDeduction,
    decimal StandardWorkdayHours,
    bool IsActive
);
