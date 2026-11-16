using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ThinkOnErp.Application.Common;
using ThinkOnErp.Application.DTOs.Hr;
using ThinkOnErp.Application.Services.Hr;
using ThinkOnErp.Domain.Entities.Hr;
using ThinkOnErp.Domain.Interfaces.Hr;

namespace ThinkOnErp.Application.Features.Hr.Payroll.Common;

internal static class PayrollCommon
{
    internal static ApiResponse<T> AccountingHandoffRequired<T>() => ApiResponse<T>.CreateFailure(
        "Payroll journals and payments are managed by Accounting. The independent handoff workflow must be enabled first.",
        new() { "HR_ACCOUNTING_HANDOFF_REQUIRED" }, statusCode: 409);

    internal static PayslipSummaryDto MapPayslipSummary(PayrollRunLine line) => new(
        line.Id,
        line.EmployeeCode,
        line.Employee?.NameLocal ?? line.EmployeeCode,
        line.BasicSalary,
        line.GrossSalary,
        line.TotalEarnings,
        line.SscEmployeeContrib,
        line.IncomeTaxWithheld,
        line.OtherDeductions,
        line.TotalDeductions,
        line.NetPay,
        line.PaymentMethod);

    internal static PayrollCalculationResultDto MapToResultDto(PayrollRun run)
    {
        var payslips = run.Lines.Select(MapPayslipSummary).ToList();

        return new PayrollCalculationResultDto(
            run.Id,
            run.PayPeriod,
            run.Lines.Count,
            run.TotalGrossSalary,
            run.TotalNetSalary,
            run.TotalEmployeeSsc,
            run.TotalEmployerSsc,
            run.TotalIncomeTax,
            run.TotalNationalContrib,
            run.TotalOtherDeductions,
            payslips
        )
        {
            Status = run.Status,
            CancelledBy = run.CancelledBy,
            CancellationDate = run.CancellationDate,
            CancellationReason = run.CancellationReason,
            CorrectionOfRunId = run.CorrectionOfRunId,
            CorrectionRootRunId = run.CorrectionRootRunId,
            CorrectionVersion = run.CorrectionVersion,
            CorrectionReason = run.CorrectionReason
        };
    }

    internal static async Task<PayrollCalculationResultDto> CalculatePayrollRunCoreAsync(
        IPayrollPeriodRepository payrollPeriodRepo,
        IPayrollCalculationEngine calculationEngine,
        IPayrollValidationService validationService,
        IPayrollInputSnapshotService? inputSnapshots,
        CreatePayrollRunDto dto,
        string user,
        CancellationToken cancellationToken,
        long? correctionOfRunId = null,
        long? correctionRootRunId = null,
        int correctionVersion = 1,
        string? correctionReason = null)
    {
        if (!dto.BranchId.HasValue) throw new ArgumentException("Calculate a separate payroll run for each branch.");
        var period = await payrollPeriodRepo.GetPeriodByCodeAsync(dto.CompanyId, dto.PayPeriod, cancellationToken)
            ?? throw new InvalidOperationException($"Period '{dto.PayPeriod}' not found.");
        var inputsBefore = inputSnapshots == null ? null : await inputSnapshots.CaptureAsync(period, dto.BranchId, cancellationToken);
        // 1. Validation
        var validation = await validationService.ValidatePreCalculationAsync(dto.CompanyId, dto.PayPeriod, dto.BranchId, cancellationToken);
        if (!validation.IsValid)
        {
            throw new InvalidOperationException($"Payroll validation failed: {string.Join(", ", validation.Errors)}");
        }

        var employees = await payrollPeriodRepo.GetActiveEmployeesAsync(dto.BranchId, cancellationToken);

        // 2. Calculation
        var run = await calculationEngine.CalculatePayrollForPeriodAsync(period, employees, dto.BranchId, user, cancellationToken);
        run.CorrectionOfRunId = correctionOfRunId;
        run.CorrectionRootRunId = correctionRootRunId;
        run.CorrectionVersion = correctionVersion;
        run.CorrectionReason = correctionReason;
        if (inputSnapshots != null)
        {
            var inputsAfter = await inputSnapshots.CaptureAsync(period, dto.BranchId, cancellationToken);
            if (inputsBefore != inputsAfter)
                throw new InvalidOperationException("Payroll inputs changed during calculation. Retry the calculation using the updated inputs.");
            run.InputFingerprint = inputsAfter;
        }

        // 3. Persist run
        await payrollPeriodRepo.AddPayrollRunAsync(run, cancellationToken);
        await payrollPeriodRepo.SaveChangesAsync(cancellationToken);
        await payrollPeriodRepo.LinkApprovedAdjustmentsToRunAsync(dto.CompanyId, run, user, cancellationToken);
        await payrollPeriodRepo.SaveChangesAsync(cancellationToken);

        return MapToResultDto(run);
    }

    internal static PayrollRunComparisonDto BuildComparison(PayrollRun currentRun, PayrollRun? baselineRun)
    {
        var employeeVariances = new List<PayrollEmployeeVarianceDto>();

        if (baselineRun == null)
        {
            foreach (var line in currentRun.Lines)
            {
                var empName = line.Employee?.NameLocal ?? (!string.IsNullOrWhiteSpace(line.BeneficiaryName) ? line.BeneficiaryName : line.EmployeeCode);
                employeeVariances.Add(new PayrollEmployeeVarianceDto(
                    line.EmployeeCode,
                    empName,
                    "NEW",
                    0m,
                    line.GrossSalary,
                    line.GrossSalary,
                    0m,
                    line.NetPay,
                    line.NetPay,
                    0m,
                    line.BasicSalary,
                    "Initial payroll run - new employee"
                ));
            }

            var initialSummary = new PayrollComparisonSummaryDto(
                currentRun.Lines.Count,
                0,
                currentRun.Lines.Count,
                0,
                0,
                currentRun.TotalGrossSalary,
                0m,
                currentRun.TotalGrossSalary,
                currentRun.TotalNetSalary,
                0m,
                currentRun.TotalNetSalary,
                currentRun.Lines.Sum(l => l.BasicSalary),
                0m,
                currentRun.Lines.Sum(l => l.BasicSalary),
                currentRun.Lines.Sum(l => l.TotalEarnings),
                0m,
                currentRun.Lines.Sum(l => l.TotalEarnings),
                currentRun.TotalOtherDeductions,
                0m,
                currentRun.TotalOtherDeductions,
                currentRun.TotalEmployeeSsc,
                0m,
                currentRun.TotalEmployeeSsc,
                currentRun.TotalIncomeTax,
                0m,
                currentRun.TotalIncomeTax
            );

            return new PayrollRunComparisonDto(
                currentRun.Id,
                currentRun.PayPeriod,
                currentRun.BranchId,
                currentRun.CorrectionVersion,
                currentRun.Status,
                null,
                null,
                null,
                null,
                initialSummary,
                employeeVariances
            );
        }

        var currentDict = currentRun.Lines.ToDictionary(l => l.EmployeeCode, StringComparer.Ordinal);
        var baselineDict = baselineRun.Lines.ToDictionary(l => l.EmployeeCode, StringComparer.Ordinal);

        int addedCount = 0;
        int retainedCount = 0;

        foreach (var (code, curr) in currentDict)
        {
            var empName = curr.Employee?.NameLocal ?? (!string.IsNullOrWhiteSpace(curr.BeneficiaryName) ? curr.BeneficiaryName : curr.EmployeeCode);
            if (!baselineDict.TryGetValue(code, out var prev))
            {
                addedCount++;
                employeeVariances.Add(new PayrollEmployeeVarianceDto(
                    code,
                    empName,
                    "NEW",
                    0m,
                    curr.GrossSalary,
                    curr.GrossSalary,
                    0m,
                    curr.NetPay,
                    curr.NetPay,
                    0m,
                    curr.BasicSalary,
                    "New hire / newly included in payroll"
                ));
            }
            else
            {
                retainedCount++;
                var netDiff = curr.NetPay - prev.NetPay;
                var grossDiff = curr.GrossSalary - prev.GrossSalary;
                var basicDiff = curr.BasicSalary - prev.BasicSalary;

                string changeType = netDiff switch
                {
                    > 0.001m => "INCREASED",
                    < -0.001m => "DECREASED",
                    _ => "UNCHANGED"
                };

                string reason;
                if (Math.Abs(basicDiff) > 0.001m)
                    reason = $"Basic salary changed from {prev.BasicSalary:F3} to {curr.BasicSalary:F3}";
                else if (curr.Components.Any(c => c.ComponentCode == "UNPAID_LEAVE") || prev.Components.Any(c => c.ComponentCode == "UNPAID_LEAVE"))
                    reason = "Unpaid leave deduction variance";
                else if (Math.Abs(curr.TotalEarnings - prev.TotalEarnings) > 0.001m)
                    reason = "Allowance / earnings variance";
                else if (Math.Abs(curr.OtherDeductions - prev.OtherDeductions) > 0.001m)
                    reason = "Loan / other deduction variance";
                else if (Math.Abs(curr.IncomeTaxWithheld - prev.IncomeTaxWithheld) > 0.001m || Math.Abs(curr.SscEmployeeContrib - prev.SscEmployeeContrib) > 0.001m)
                    reason = "Statutory deduction (Tax/SSC) variance";
                else
                    reason = "No variance";

                employeeVariances.Add(new PayrollEmployeeVarianceDto(
                    code,
                    empName,
                    changeType,
                    prev.GrossSalary,
                    curr.GrossSalary,
                    grossDiff,
                    prev.NetPay,
                    curr.NetPay,
                    netDiff,
                    prev.BasicSalary,
                    curr.BasicSalary,
                    reason
                ));
            }
        }

        int removedCount = 0;
        foreach (var (code, prev) in baselineDict)
        {
            if (!currentDict.ContainsKey(code))
            {
                removedCount++;
                var empName = prev.Employee?.NameLocal ?? (!string.IsNullOrWhiteSpace(prev.BeneficiaryName) ? prev.BeneficiaryName : prev.EmployeeCode);
                employeeVariances.Add(new PayrollEmployeeVarianceDto(
                    code,
                    empName,
                    "REMOVED",
                    prev.GrossSalary,
                    0m,
                    -prev.GrossSalary,
                    prev.NetPay,
                    0m,
                    -prev.NetPay,
                    prev.BasicSalary,
                    0m,
                    "Terminated / excluded from current payroll"
                ));
            }
        }

        var baselineBasic = baselineRun.Lines.Sum(l => l.BasicSalary);
        var currentBasic = currentRun.Lines.Sum(l => l.BasicSalary);
        var baselineEarnings = baselineRun.Lines.Sum(l => l.TotalEarnings);
        var currentEarnings = currentRun.Lines.Sum(l => l.TotalEarnings);

        var summary = new PayrollComparisonSummaryDto(
            currentRun.Lines.Count,
            baselineRun.Lines.Count,
            addedCount,
            removedCount,
            retainedCount,
            currentRun.TotalGrossSalary,
            baselineRun.TotalGrossSalary,
            currentRun.TotalGrossSalary - baselineRun.TotalGrossSalary,
            currentRun.TotalNetSalary,
            baselineRun.TotalNetSalary,
            currentRun.TotalNetSalary - baselineRun.TotalNetSalary,
            currentBasic,
            baselineBasic,
            currentBasic - baselineBasic,
            currentEarnings,
            baselineEarnings,
            currentEarnings - baselineEarnings,
            currentRun.TotalOtherDeductions,
            baselineRun.TotalOtherDeductions,
            currentRun.TotalOtherDeductions - baselineRun.TotalOtherDeductions,
            currentRun.TotalEmployeeSsc,
            baselineRun.TotalEmployeeSsc,
            currentRun.TotalEmployeeSsc - baselineRun.TotalEmployeeSsc,
            currentRun.TotalIncomeTax,
            baselineRun.TotalIncomeTax,
            currentRun.TotalIncomeTax - baselineRun.TotalIncomeTax
        );

        return new PayrollRunComparisonDto(
            currentRun.Id,
            currentRun.PayPeriod,
            currentRun.BranchId,
            currentRun.CorrectionVersion,
            currentRun.Status,
            baselineRun.Id,
            baselineRun.PayPeriod,
            baselineRun.CorrectionVersion,
            baselineRun.Status,
            summary,
            employeeVariances
        );
    }
}
