using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace ThinkOnErp.Application.DTOs.Hr;

// Leave Types DTOs
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

// Leave Requests DTOs
public record SubmitLeaveRequestDto(
    string EmployeeCode,
    string LeaveTypeCode,
    DateTime StartDate,
    DateTime EndDate,
    decimal DaysRequested,
    string? Reason,
    string? AttachmentFileRef
);

public record ProcessLeaveRequestDto(
    bool Approved,
    string? RejectionReason
);

public record CancelLeaveRequestDto(
    [Required]
    [StringLength(500, MinimumLength = 1)]
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

// Vacation Accrual, Grant & Rollover DTOs
public record GrantLeaveBalanceDto(
    string EmployeeCode,
    string LeaveTypeCode,
    int YearNo,
    decimal Days,
    [Required]
    [StringLength(30, MinimumLength = 1)]
    string AdjustmentType,
    [Required]
    [StringLength(1000, MinimumLength = 1)]
    string? Reason,
    [Required]
    [StringLength(200, MinimumLength = 1)]
    string? IdempotencyKey = null
);

public record ReverseLeaveBalanceMovementDto(
    [Required]
    [StringLength(1000, MinimumLength = 1)]
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
    [Required]
    [StringLength(200, MinimumLength = 1)]
    string SettlementKey,
    long LeaveYearCloseId,
    [Required]
    [StringLength(40, MinimumLength = 1)]
    string PayPeriod,
    [Required]
    [StringLength(20, MinimumLength = 1)]
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
    [Required]
    [StringLength(1000, MinimumLength = 1)]
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
    [Required]
    [StringLength(200, MinimumLength = 1)]
    string MigrationKey,
    DateTime EffectiveDate,
    int TargetYearStartMonth,
    int TargetYearStartDay,
    [Required]
    [MinLength(1)]
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
