using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ThinkOnErp.Application.DTOs.Hr;
using ThinkOnErp.Domain.Entities.Hr;
using ThinkOnErp.Domain.Interfaces.Hr;

namespace ThinkOnErp.Application.Services.Hr;

public interface IPayrollValidationService
{
    Task<PayrollValidationResultDto> ValidatePreCalculationAsync(
        long companyId,
        string payPeriod,
        long? branchId = null,
        CancellationToken cancellationToken = default);

    Task<PayrollValidationResultDto> ValidatePostApprovalAsync(
        long payrollRunId,
        CancellationToken cancellationToken = default);
}

public sealed class PayrollValidationService : IPayrollValidationService
{
    private readonly IPayrollPeriodRepository _payrollPeriodRepo;
    private readonly IPolicyRepository _policyRepo;

    public PayrollValidationService(
        IPayrollPeriodRepository payrollPeriodRepo,
        IPolicyRepository policyRepo)
    {
        _payrollPeriodRepo = payrollPeriodRepo;
        _policyRepo = policyRepo;
    }

    public async Task<PayrollValidationResultDto> ValidatePreCalculationAsync(
        long companyId,
        string payPeriod,
        long? branchId = null,
        CancellationToken cancellationToken = default)
    {
        var errors = new List<string>();
        var warnings = new List<string>();

        // 1. Period check
        var period = await _payrollPeriodRepo.GetPeriodByCodeAsync(companyId, payPeriod, cancellationToken);
        if (period == null)
        {
            errors.Add($"Payroll period '{payPeriod}' does not exist for company {companyId}.");
            return new PayrollValidationResultDto(false, errors, warnings);
        }

        if (period.Status == "CLOSED")
        {
            errors.Add($"Payroll period '{payPeriod}' is already CLOSED. No calculations allowed.");
        }

        // 2. Existing run check
        var existingRun = await _payrollPeriodRepo.GetPayrollRunByPeriodAsync(payPeriod, branchId, cancellationToken);
        if (existingRun != null && (existingRun.Status == "POSTED_TO_GL" || existingRun.Status == "PAID"))
        {
            errors.Add($"Payroll period '{payPeriod}' already has a {existingRun.Status} run (ID: {existingRun.Id}). Calculations cannot be rerun without cancellation/reversal.");
        }

        // 3. Policies check
        var prorationPolicy = await _policyRepo.GetActiveProrationPolicyAsync(companyId, period.StartDate, cancellationToken);
        if (prorationPolicy == null)
        {
            warnings.Add($"No active Proration Policy found for {period.StartDate:yyyy-MM-dd}. Default CALENDAR_DAYS will be used.");
        }

        var sscPolicy = await _policyRepo.GetActiveSSCPolicyAsync(companyId, period.StartDate, cancellationToken);
        if (sscPolicy == null)
        {
            warnings.Add($"No active SSC Policy found for {period.StartDate:yyyy-MM-dd}. Default rates will be used.");
        }

        var taxPolicy = await _policyRepo.GetActiveTaxPolicyAsync(companyId, period.StartDate, cancellationToken);
        if (taxPolicy == null)
        {
            warnings.Add($"No active Tax Policy found for {period.StartDate:yyyy-MM-dd}. Default brackets will be used.");
        }

        // 4. Employees check
        var employees = await _payrollPeriodRepo.GetActiveEmployeesAsync(branchId, cancellationToken);
        if (employees.Count == 0)
        {
            errors.Add("No active employees found eligible for payroll calculation.");
        }
        else
        {
            foreach (var emp in employees)
            {
                var hasActiveSalary = emp.SalaryStructures.Any(s => s.IsActive && s.EffectiveFrom <= period.EndDate);
                if (!hasActiveSalary)
                {
                    warnings.Add($"Employee {emp.EmployeeCode} ({emp.NameLocal}) has no active salary structure effective for {payPeriod}.");
                }
            }
        }

        return new PayrollValidationResultDto(errors.Count == 0, errors, warnings);
    }

    public async Task<PayrollValidationResultDto> ValidatePostApprovalAsync(
        long payrollRunId,
        CancellationToken cancellationToken = default)
    {
        var errors = new List<string>();
        var warnings = new List<string>();

        var run = await _payrollPeriodRepo.GetPayrollRunByIdAsync(payrollRunId, cancellationToken);
        if (run == null)
        {
            errors.Add($"Payroll run with ID {payrollRunId} not found.");
            return new PayrollValidationResultDto(false, errors, warnings);
        }

        if (run.Status != "CALCULATED" && run.Status != "DRAFT")
        {
            errors.Add($"Payroll run {payrollRunId} is in '{run.Status}' status and cannot be approved.");
        }

        if (run.Lines.Count == 0)
        {
            errors.Add($"Payroll run {payrollRunId} has no lines to approve.");
        }

        var negativeLines = run.Lines.Where(l => l.NetPay < 0).ToList();
        if (negativeLines.Count > 0)
        {
            warnings.Add($"Payroll run contains {negativeLines.Count} employee(s) with negative net pay.");
        }

        return new PayrollValidationResultDto(errors.Count == 0, errors, warnings);
    }
}
