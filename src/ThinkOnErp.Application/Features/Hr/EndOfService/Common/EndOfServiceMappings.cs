using System.Linq;
using ThinkOnErp.Application.DTOs.Hr;
using ThinkOnErp.Domain.Entities.Hr;

namespace ThinkOnErp.Application.Features.Hr.EndOfService.Common;

internal static class EndOfServiceMappings
{
    public static EndOfServiceSettlementDto ToDto(EndOfServiceSettlement s)
    {
        var empName = s.Employee?.NameLocal ?? (!string.IsNullOrWhiteSpace(s.Employee?.NameEn) ? s.Employee.NameEn : s.EmployeeCode);
        var lines = s.Lines.Select(l => new EndOfServiceLineDto(l.Id, l.ComponentCode, l.ComponentType, l.Amount, l.Description)).ToList();

        return new EndOfServiceSettlementDto(
            s.Id,
            s.CompanyId,
            s.BranchId,
            s.EmployeeCode,
            empName,
            s.TerminationType,
            s.TerminationReason,
            s.NoticeDate,
            s.LastWorkingDate,
            s.SettlementDate,
            s.ServiceYears,
            s.ServiceMonths,
            s.ServiceDays,
            s.BaseSalarySnapshot,
            s.GrossSalarySnapshot,
            s.CurrencyCode,
            s.SeverancePay,
            s.UnpaidSalary,
            s.LeaveEncashment,
            s.UnusedLeaveDays,
            s.NoticePeriodPay,
            s.OtherEntitlements,
            s.TotalEntitlements,
            s.OutstandingLoans,
            s.OutstandingAdvances,
            s.NegativeLeaveDeduction,
            s.OtherDeductions,
            s.TotalDeductions,
            s.NetSettlementAmount,
            s.Status,
            s.CalculationNotes,
            s.ApprovedBy,
            s.ApprovedAtUtc,
            s.CancelledBy,
            s.CancelledAtUtc,
            s.CancellationReason,
            lines
        );
    }
}
