using System;
using System.ComponentModel.DataAnnotations;

namespace ThinkOnErp.Application.DTOs.Hr;

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
