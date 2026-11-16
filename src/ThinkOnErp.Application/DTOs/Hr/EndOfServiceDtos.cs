using System;
using System.Collections.Generic;

namespace ThinkOnErp.Application.DTOs.Hr;

public sealed record CreateEndOfServicePolicyDto(
    string PolicyName,
    DateTime EffectiveFrom,
    DateTime? EffectiveTo,
    string CalculationBasis,
    decimal LeaveDailyRateDivisor,
    int NoticePeriodDays,
    bool ProrateFinalMonthByWorkingDays,
    decimal ResignationFactorUnderTwoYears,
    decimal ResignationFactorTwoToFiveYears,
    decimal ResignationFactorFiveToTenYears,
    decimal ResignationFactorTenYearsAndAbove,
    decimal FirstFiveYearsRatePerYear,
    decimal SubsequentYearsRatePerYear
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

public sealed record EndOfServicePayoutHandoffDto(
    long HandoffId,
    string HandoffReference,
    string DeliveryStatus,
    string Operation,
    decimal Amount,
    string? Currency
);
