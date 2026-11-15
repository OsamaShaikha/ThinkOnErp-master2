using System;
using System.Collections.Generic;

namespace ThinkOnErp.Domain.Entities.Hr;

public sealed class LeaveType
{
    public string LeaveTypeCode { get; set; } = string.Empty;
    public string NameLocal { get; set; } = string.Empty;
    public string NameEn { get; set; } = string.Empty;
    public bool IsPaid { get; set; } = true;
    public bool TracksBalance { get; set; } = true;
    public bool IsStatutory { get; set; } = true;
    public decimal MaxDaysPerYear { get; set; } = 14m;
    public bool CarryForwardAllowed { get; set; }
    public decimal CarryForwardCapDays { get; set; }
    public string YearEndExcessAction { get; set; } = "EXPIRE"; // EXPIRE, REQUIRE_USE, CASHOUT
    public bool RequiresDocumentation { get; set; }
    public bool IsActive { get; set; } = true;

    public string CreationUser { get; set; } = string.Empty;
    public DateTime CreationDate { get; set; } = DateTime.UtcNow;
    public string? UpdateUser { get; set; }
    public DateTime? UpdateDate { get; set; }

    public List<LeaveRequest> Requests { get; set; } = new();
    public List<LeaveBalance> Balances { get; set; } = new();
}

public sealed class LeaveRequest
{
    public long Id { get; set; }
    public string EmployeeCode { get; set; } = string.Empty;
    public string LeaveTypeCode { get; set; } = string.Empty;
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    public decimal DaysRequested { get; set; }
    public string Status { get; set; } = "PENDING"; // PENDING, APPROVED, REJECTED, CANCELLED
    public string? Reason { get; set; }
    public string? ApprovedBy { get; set; }
    public DateTime? ApprovalDate { get; set; }
    public string? RejectionReason { get; set; }
    public string? AttachmentFileRef { get; set; }
    public string? CancelledBy { get; set; }
    public DateTime? CancellationDate { get; set; }
    public string? CancellationReason { get; set; }

    public string CreationUser { get; set; } = string.Empty;
    public DateTime CreationDate { get; set; } = DateTime.UtcNow;
    public string? UpdateUser { get; set; }
    public DateTime? UpdateDate { get; set; }

    public Employee? Employee { get; set; }
    public LeaveType? LeaveType { get; set; }
}

public sealed class LeaveBalance
{
    public long Id { get; set; }
    public string EmployeeCode { get; set; } = string.Empty;
    public string LeaveTypeCode { get; set; } = string.Empty;
    public int YearNo { get; set; }
    // YearNo remains the compatibility label (the year in which the cycle starts).
    // These dates preserve the actual cycle even if company policy changes later.
    public DateTime? CycleStartDate { get; set; }
    public DateTime? CycleEndDate { get; set; }
    public decimal AccruedDays { get; set; }
    public decimal UsedDays { get; set; }
    public decimal CarriedForwardDays { get; set; }

    public string CreationUser { get; set; } = string.Empty;
    public DateTime CreationDate { get; set; } = DateTime.UtcNow;
    public string? UpdateUser { get; set; }
    public DateTime? UpdateDate { get; set; }

    public Employee? Employee { get; set; }
    public LeaveType? LeaveType { get; set; }
}

public sealed class HrLeaveSettings
{
    public long Id { get; set; }
    public int LeaveYearStartMonth { get; set; } = 1;
    public int LeaveYearStartDay { get; set; } = 1;
    public string BalanceDisplayFormat { get; set; } = "DECIMAL"; // DECIMAL, DHM
    public decimal? StandardWorkdayHours { get; set; }
    public bool AutomaticAccrualEnabled { get; set; }
    public string? AccrualTimeZoneId { get; set; }
    public string? EligibleCitizenshipCountryCode { get; set; }
    public string? LeaveCashoutWageBasisCode { get; set; }
    public string? LeaveCashoutBasisComponentCode { get; set; }
    public decimal? LeaveCashoutDayDivisor { get; set; }
    public string? LeaveCashoutPayComponentCode { get; set; }
    public string CreationUser { get; set; } = "SYSTEM";
    public DateTime CreationDate { get; set; } = DateTime.UtcNow;
    public string? UpdateUser { get; set; }
    public DateTime? UpdateDate { get; set; }
}

public sealed class LeaveYearClose
{
    public long Id { get; set; }
    public string CloseKey { get; set; } = string.Empty;
    public int FromYear { get; set; }
    public int ToYear { get; set; }
    public string LeaveTypeCode { get; set; } = "*";
    public string Status { get; set; } = "DRAFT"; // DRAFT, POSTED, UNPOSTED
    public string CreationUser { get; set; } = string.Empty;
    public DateTime CreationDate { get; set; } = DateTime.UtcNow;
    public string? PostedBy { get; set; }
    public DateTime? PostedDate { get; set; }
    public string? UnpostedBy { get; set; }
    public DateTime? UnpostedDate { get; set; }
    public List<LeaveYearCloseLine> Lines { get; set; } = new();
}

public sealed class LeaveYearCloseLine
{
    public long Id { get; set; }
    public long CloseId { get; set; }
    public string EmployeeCode { get; set; } = string.Empty;
    public string LeaveTypeCode { get; set; } = string.Empty;
    public int SourceYear { get; set; }
    public int TargetYear { get; set; }
    public long SourceBalanceId { get; set; }
    public DateTime? SourceCycleStartDate { get; set; }
    public DateTime? SourceCycleEndDate { get; set; }
    public DateTime TargetCycleStartDate { get; set; }
    public DateTime TargetCycleEndDate { get; set; }
    public decimal SourceAccruedBefore { get; set; }
    public decimal SourceUsedDays { get; set; }
    public decimal SourceCarriedBefore { get; set; }
    public decimal TargetAccruedBefore { get; set; }
    public decimal TargetCarriedBefore { get; set; }
    public decimal TargetUsedBefore { get; set; }
    public decimal UnusedDays { get; set; }
    public decimal CarriedDays { get; set; }
    public decimal ExpiredDays { get; set; }
    public decimal CashoutDays { get; set; }
    public long? TargetBalanceId { get; set; }
    public bool? TracksBalanceSnapshot { get; set; }
    public bool? CarryForwardAllowedSnapshot { get; set; }
    public decimal? CarryForwardCapDaysSnapshot { get; set; }
    public string? YearEndExcessActionSnapshot { get; set; }
    public LeaveYearClose? Close { get; set; }
}

public sealed class LeaveCashoutSettlement
{
    public long Id { get; set; }
    public string SettlementKey { get; set; } = string.Empty;
    public string PayloadHash { get; set; } = string.Empty;
    public long LeaveYearCloseId { get; set; }
    public string PayPeriod { get; set; } = string.Empty;
    public string WageBasisCode { get; set; } = string.Empty;
    public string? BasisComponentCode { get; set; }
    public decimal DayDivisor { get; set; }
    public string PayComponentCode { get; set; } = string.Empty;
    public string Status { get; set; } = "DRAFT"; // DRAFT, POSTED, REVERSED
    public string CreationUser { get; set; } = string.Empty;
    public DateTime CreationDate { get; set; } = DateTime.UtcNow;
    public string? PostedBy { get; set; }
    public DateTime? PostedDate { get; set; }
    public string? ReversedBy { get; set; }
    public DateTime? ReversedDate { get; set; }
    public string? ReversalReason { get; set; }
    public List<LeaveCashoutSettlementLine> Lines { get; set; } = new();
}

public sealed class LeaveCashoutSettlementLine
{
    public long Id { get; set; }
    public long SettlementId { get; set; }
    public long LeaveYearCloseLineId { get; set; }
    public string EmployeeCode { get; set; } = string.Empty;
    public string LeaveTypeCode { get; set; } = string.Empty;
    public long SalaryStructureId { get; set; }
    public DateTime SalaryEffectiveFrom { get; set; }
    public decimal CashoutDays { get; set; }
    public decimal WageBasisAmount { get; set; }
    public decimal DayDivisor { get; set; }
    public decimal CashoutAmount { get; set; }
    public string CurrencyCode { get; set; } = string.Empty;
    public string BasisSnapshotJson { get; set; } = string.Empty;
    public long? PayrollAdjustmentId { get; set; }
    public LeaveCashoutSettlement? Settlement { get; set; }
    public PayrollAdjustment? PayrollAdjustment { get; set; }
}

public sealed class LeavePolicy
{
    public long Id { get; set; }
    public string PolicyName { get; set; } = string.Empty;
    public string LeaveTypeCode { get; set; } = string.Empty;
    public DateTime EffectiveFrom { get; set; } = DateTime.UtcNow.Date;
    public DateTime? EffectiveTo { get; set; }
    public string AccrualMethod { get; set; } = "YEARLY"; // YEARLY, MONTHLY, DAILY
    public decimal AccrualRate { get; set; }
    public decimal? AnnualEntitlementDays { get; set; }
    public string CycleBasis { get; set; } = "CALENDAR_YEAR"; // CALENDAR_YEAR, COMPANY_LEAVE_YEAR, EMPLOYMENT_ANNIVERSARY
    public bool ProrateFirstCycle { get; set; }
    public string ApplicableTo { get; set; } = "ALL"; // ALL, GENDER_F, GENDER_M, CITIZENS
    public int MinServiceMonths { get; set; }
    public int Tier1YearsThreshold { get; set; } = 5;
    public decimal Tier1Days { get; set; } = 14m;
    public decimal Tier2Days { get; set; } = 21m;
    public bool IsActive { get; set; } = true;

    public string CreationUser { get; set; } = string.Empty;
    public DateTime CreationDate { get; set; } = DateTime.UtcNow;
    public string? UpdateUser { get; set; }
    public DateTime? UpdateDate { get; set; }

    public LeaveType? LeaveType { get; set; }
}
