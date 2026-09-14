using System;
using System.Threading;
using System.Threading.Tasks;
using ThinkOnErp.Application.DTOs.Hr;
using ThinkOnErp.Domain.Interfaces.Hr;

namespace ThinkOnErp.Application.Services.Hr;

public interface IPayrollExplanationService
{
    Task<PayrollExplanationDto?> GetExplanationAsync(long runLineId, CancellationToken cancellationToken = default);
}

public sealed class PayrollExplanationService : IPayrollExplanationService
{
    private readonly IPayrollPeriodRepository _payrollPeriodRepo;

    public PayrollExplanationService(IPayrollPeriodRepository payrollPeriodRepo)
    {
        _payrollPeriodRepo = payrollPeriodRepo;
    }

    public async Task<PayrollExplanationDto?> GetExplanationAsync(long runLineId, CancellationToken cancellationToken = default)
    {
        var line = await _payrollPeriodRepo.GetRunLineWithSnapshotAsync(runLineId, cancellationToken);
        if (line == null) return null;

        var snapshot = line.Snapshot;

        return new PayrollExplanationDto(
            line.Id,
            line.EmployeeCode,
            line.Employee?.NameLocal ?? line.EmployeeCode,
            line.PayrollRun?.PayPeriod ?? snapshot?.PayPeriod ?? string.Empty,
            line.BasicSalary,
            snapshot?.ProrationFactor ?? 1.0m,
            snapshot?.ProrationMethodUsed ?? "NONE",
            line.BasicSalary,
            snapshot?.OvertimeHoursApplied ?? 0m,
            snapshot?.OvertimeEarningsApplied ?? 0m,
            line.GrossSalary,
            line.SscEligibleSalary,
            line.SscEmployeeContrib,
            line.SscEmployerContrib,
            line.TaxableGross,
            line.AnnualExemptions,
            line.IncomeTaxWithheld,
            line.NationalContribWithheld,
            (snapshot?.LoanDeductionsApplied ?? 0m) + (snapshot?.LoanDeductionsCarriedFwd ?? 0m),
            snapshot?.LoanDeductionsApplied ?? 0m,
            snapshot?.LoanDeductionsCarriedFwd ?? 0m,
            line.NetPay,
            snapshot?.ExplanationJson ?? "{}"
        );
    }
}
