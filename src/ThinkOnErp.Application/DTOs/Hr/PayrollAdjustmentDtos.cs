using System;

namespace ThinkOnErp.Application.DTOs.Hr;

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
