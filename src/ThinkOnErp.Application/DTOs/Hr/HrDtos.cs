using System;
using System.Collections.Generic;

namespace ThinkOnErp.Application.DTOs.Hr;

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
    bool IsDefault
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
    List<TaxBracketDto> Brackets
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
    string? Notes
);

public record CreateAdvanceRequestDto(
    string EmployeeCode,
    decimal AdvanceAmount,
    string TargetPayPeriod,
    string? Reason
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
    string Status
);

// Payroll Run & Calculation DTOs
public record CreatePayrollRunDto(
    long CompanyId,
    string PayPeriod,
    long? BranchId
);

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
);

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
