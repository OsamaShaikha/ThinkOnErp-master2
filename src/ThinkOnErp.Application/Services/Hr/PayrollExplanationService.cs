using System;
using System.Linq;
using System.Threading.Tasks;
using ThinkOnErp.Application.DTOs.Hr;
using ThinkOnErp.Domain.Exceptions;
using ThinkOnErp.Domain.Interfaces.Hr;

namespace ThinkOnErp.Application.Services.Hr;

public sealed class PayrollExplanationService : IPayrollExplanationService
{
    private readonly IPayrollRepository _payrollRepo;
    private readonly IEmployeeRepository _employeeRepo;
    private readonly IPayrollPeriodRepository _periodRepo;

    public PayrollExplanationService(
        IPayrollRepository payrollRepo,
        IEmployeeRepository employeeRepo,
        IPayrollPeriodRepository periodRepo)
    {
        _payrollRepo = payrollRepo ?? throw new ArgumentNullException(nameof(payrollRepo));
        _employeeRepo = employeeRepo ?? throw new ArgumentNullException(nameof(employeeRepo));
        _periodRepo = periodRepo ?? throw new ArgumentNullException(nameof(periodRepo));
    }

    public async Task<PayrollExplanationDto> GetPayrollExplanationAsync(string employeeCode, string payPeriod)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(employeeCode);
        ArgumentException.ThrowIfNullOrWhiteSpace(payPeriod);

        var employee = await _employeeRepo.GetByCodeAsync(employeeCode);
        if (employee == null)
        {
            throw new HrNotFoundException($"الموظف ({employeeCode}) غير موجود.", "EMPLOYEE_NOT_FOUND");
        }

        var runs = await _payrollRepo.GetAllRunsAsync(payPeriod: payPeriod);
        var baseRun = runs.FirstOrDefault();
        if (baseRun == null)
        {
            throw new HrNotFoundException($"مسير الرواتب لشهر ({payPeriod}) غير موجود.", "RUN_NOT_FOUND");
        }

        var run = await _payrollRepo.GetRunByIdAsync(baseRun.Id, includeLines: true);
        if (run == null)
        {
            throw new HrNotFoundException($"مسير الرواتب لشهر ({payPeriod}) غير موجود.", "RUN_NOT_FOUND");
        }

        var line = run.Lines.FirstOrDefault(l => l.EmployeeCode == employeeCode);
        if (line == null)
        {
            throw new HrNotFoundException($"لا توجد تفاصيل رواتب للموظف ({employeeCode}) في شهر ({payPeriod}).", "LINE_NOT_FOUND");
        }

        var snapshot = await _periodRepo.GetSnapshotByLineIdAsync(line.Id);

        var dto = new PayrollExplanationDto
        {
            EmployeeCode = employeeCode,
            EmployeeName = employee.NameAr,
            PayPeriod = payPeriod,
            BasicSalary = line.BasicSalary,
            GrossSalary = line.GrossSalary,
            TotalEarnings = line.TotalEarnings,
            TotalDeductions = line.TotalDeductions,
            NetPay = line.NetPay,

            ProrationMethod = snapshot?.ProrationMethodUsed ?? "FIXED_30",
            ProrationFactor = snapshot?.ProrationFactor ?? 1.0m,
            EligibleDays = snapshot?.EligibleDays ?? 30m,
            TotalBaseDays = snapshot?.TotalBaseDays ?? 30m,

            SscEligibleSalary = line.SscEligibleSalary,
            SscEmployeeRate = snapshot?.SscEmployeeRateApplied ?? 0.075m,
            SscEmployeeContribution = line.SscEmployeeContribution,
            SscEmployerContribution = line.SscEmployerContribution,
            SscCapApplied = snapshot?.SscCapApplied ?? 3349.0m,

            AnnualTaxableNet = line.AnnualTaxableNet,
            AnnualExemptions = line.AnnualExemptions,
            MonthlyTaxWithheld = line.IncomeTaxWithheld,
            NationalContributionWithheld = line.NationalContributionWithheld,

            OvertimeHours = snapshot?.OvertimeHoursApplied ?? 0m,
            OvertimeEarnings = snapshot?.OvertimeEarningsApplied ?? 0m,
            LoanInstallmentDeducted = snapshot?.LoanDeductionsApplied ?? 0m,
            LoanAmountCarriedForward = snapshot?.LoanDeductionsCarriedForward ?? 0m,

            ComponentLines = line.Components.Select(c => new PayrollExplanationLineDto
            {
                ComponentCode = c.ComponentCode,
                ComponentNameEn = c.ComponentNameEn,
                ComponentNameAr = c.ComponentNameAr,
                ComponentType = c.ComponentType,
                Amount = c.Amount,
                CalculationFormula = c.ComponentCode switch
                {
                    "BASIC" => $"Prorated Basic: {line.BasicSalary:F3} JOD",
                    "SSC_EMPLOYEE" => $"{line.SscEligibleSalary:F3} JOD × 7.5%",
                    "INCOME_TAX" => $"Annual Tax / 12 on ({line.AnnualTaxableNet:F3} JOD)",
                    "OVERTIME" => $"{snapshot?.OvertimeHoursApplied:F2} hrs × Dynamic Rate",
                    _ => $"{c.ComponentType}: {c.Amount:F3} JOD"
                }
            }).ToList()
        };

        return dto;
    }
}
