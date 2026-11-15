using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using ThinkOnErp.Domain.Entities.Hr;
using ThinkOnErp.Domain.Interfaces.Hr;

namespace ThinkOnErp.Application.Services.Hr;

public interface IPayrollCalculationEngine
{
    Task<PayrollRun> CalculatePayrollForPeriodAsync(
        PayrollPeriod period,
        IReadOnlyList<Employee> employees,
        long? branchId = null,
        string calculatedBy = "SYSTEM",
        CancellationToken cancellationToken = default);

    Task<PayrollRunLine> CalculateEmployeePayrollLineAsync(
        PayrollPeriod period,
        Employee employee,
        ProrationPolicy? prorationPolicy,
        SSCPolicy? sscPolicy,
        TaxPolicy? taxPolicy,
        DeductionPolicy? deductionPolicy,
        IReadOnlyList<PayrollAdjustment> adjustments,
        CancellationToken cancellationToken = default);
}

public sealed class PayrollCalculationEngine : IPayrollCalculationEngine
{
    private readonly IPolicyRepository _policyRepository;
    private readonly IPayrollProrationService _prorationService;
    private readonly IOvertimeCalculationService _overtimeService;
    private readonly ISSCCalculationService _sscService;
    private readonly ITaxCalculationEngine _taxEngine;
    private readonly ILoanDeductionService _loanDeductionService;
    private readonly IPayrollAdjustmentRepository _adjustmentRepository;
    private readonly ILeaveService _leaveService;
    private readonly IAttendanceCorrectionRepository? _attendanceRepository;

    public PayrollCalculationEngine(
        IPolicyRepository policyRepository,
        IPayrollProrationService prorationService,
        IOvertimeCalculationService overtimeService,
        ISSCCalculationService sscService,
        ITaxCalculationEngine taxEngine,
        ILoanDeductionService loanDeductionService,
        IPayrollAdjustmentRepository adjustmentRepository,
        ILeaveService leaveService,
        IAttendanceCorrectionRepository? attendanceRepository = null)
    {
        _policyRepository = policyRepository;
        _prorationService = prorationService;
        _overtimeService = overtimeService;
        _sscService = sscService;
        _taxEngine = taxEngine;
        _loanDeductionService = loanDeductionService;
        _adjustmentRepository = adjustmentRepository;
        _leaveService = leaveService;
        _attendanceRepository = attendanceRepository;
    }

    public async Task<PayrollRun> CalculatePayrollForPeriodAsync(
        PayrollPeriod period,
        IReadOnlyList<Employee> employees,
        long? branchId = null,
        string calculatedBy = "SYSTEM",
        CancellationToken cancellationToken = default)
    {
        // 1. Fetch active policies effective on period.StartDate
        var prorationPolicy = await _policyRepository.GetActiveProrationPolicyAsync(period.CompanyId, period.StartDate, cancellationToken);
        var sscPolicy = await _policyRepository.GetActiveSSCPolicyAsync(period.CompanyId, period.StartDate, cancellationToken);
        var taxPolicy = await _policyRepository.GetActiveTaxPolicyAsync(period.CompanyId, period.StartDate, cancellationToken);
        var deductionPolicy = await _policyRepository.GetActiveDeductionPolicyAsync(period.CompanyId, period.StartDate, cancellationToken);

        // 2. Fetch approved adjustments for this period
        var allAdjustments = await _adjustmentRepository.GetPayrollCalculationAdjustmentsAsync(period.CompanyId, period.PeriodCode, cancellationToken);
        var configuredCurrencyDecimals = await _policyRepository.GetConfiguredPayrollCurrencyDecimalsAsync(period.CompanyId, cancellationToken)
            ?? new Dictionary<string, int>(StringComparer.Ordinal);

        var run = new PayrollRun
        {
            PayPeriod = period.PeriodCode,
            RunDate = DateTime.UtcNow,
            BranchId = branchId,
            Status = "CALCULATED",
            CalculatedBy = calculatedBy,
            CalculationDate = DateTime.UtcNow,
            CreationUser = calculatedBy,
            CreationDate = DateTime.UtcNow
        };

        var roundingRulesByBranch = new Dictionary<long, int?>();
        foreach (var employee in employees)
        {
            if (!employee.IsActive || employee.EmploymentStatus == "TERMINATED")
            {
                // If employee was terminated before this period starts, skip
                if (employee.TerminationDate.HasValue && employee.TerminationDate.Value < period.StartDate)
                {
                    continue;
                }
            }

            // If employee joined after this period ends, skip
            if (employee.HireDate > period.EndDate)
            {
                continue;
            }

            var empAdjustments = allAdjustments.Where(a => a.EmployeeCode == employee.EmployeeCode).ToList();
            var roundingBranchId = branchId ?? employee.BranchId;
            int? roundingRules = null;
            if (roundingBranchId.HasValue)
            {
                if (!roundingRulesByBranch.TryGetValue(roundingBranchId.Value, out roundingRules))
                {
                    roundingRules = await _policyRepository.GetBranchRoundingRulesAsync(roundingBranchId.Value, cancellationToken) ?? 1;
                    if (roundingRules is < 1 or > 6)
                        throw new InvalidOperationException($"Branch {roundingBranchId.Value} has unsupported payroll rounding rules.");
                    roundingRulesByBranch[roundingBranchId.Value] = roundingRules;
                }
            }

            var line = await CalculateEmployeePayrollLineCoreAsync(
                period,
                employee,
                prorationPolicy,
                sscPolicy,
                taxPolicy,
                deductionPolicy,
                empAdjustments,
                cancellationToken,
                roundingRules,
                configuredCurrencyDecimals
            );

            run.Lines.Add(line);
        }

        // A single numeric total cannot represent multiple currencies without conversion.
        PayrollCurrencyRules.RequireSingleCurrency(run.Lines.Select(l => l.CurrencyCode));
        // Aggregate run totals
        run.TotalGrossSalary = run.Lines.Sum(l => l.GrossSalary);
        run.TotalNetSalary = run.Lines.Sum(l => l.NetPay);
        run.TotalEmployeeSsc = run.Lines.Sum(l => l.SscEmployeeContrib);
        run.TotalEmployerSsc = run.Lines.Sum(l => l.SscEmployerContrib);
        run.TotalIncomeTax = run.Lines.Sum(l => l.IncomeTaxWithheld);
        run.TotalNationalContrib = run.Lines.Sum(l => l.NationalContribWithheld);
        run.TotalOtherDeductions = run.Lines.Sum(l => l.OtherDeductions);

        return run;
    }

    public async Task<PayrollRunLine> CalculateEmployeePayrollLineAsync(
        PayrollPeriod period,
        Employee employee,
        ProrationPolicy? prorationPolicy,
        SSCPolicy? sscPolicy,
        TaxPolicy? taxPolicy,
        DeductionPolicy? deductionPolicy,
        IReadOnlyList<PayrollAdjustment> adjustments,
        CancellationToken cancellationToken = default) =>
        await CalculateEmployeePayrollLineCoreAsync(period, employee, prorationPolicy, sscPolicy, taxPolicy,
            deductionPolicy, adjustments, cancellationToken, null,
            await _policyRepository.GetConfiguredPayrollCurrencyDecimalsAsync(period.CompanyId, cancellationToken)
                ?? new Dictionary<string, int>(StringComparer.Ordinal));

    private async Task<PayrollRunLine> CalculateEmployeePayrollLineCoreAsync(
        PayrollPeriod period,
        Employee employee,
        ProrationPolicy? prorationPolicy,
        SSCPolicy? sscPolicy,
        TaxPolicy? taxPolicy,
        DeductionPolicy? deductionPolicy,
        IReadOnlyList<PayrollAdjustment> adjustments,
        CancellationToken cancellationToken,
        int? roundingRules,
        IReadOnlyDictionary<string, int>? configuredCurrencyDecimals = null)
    {
        // 1. Get current active salary structure
        var salarySegments = PayrollSalarySegments.Build(employee, period);
        var structure = salarySegments.LastOrDefault()?.Structure;

        if (structure == null)
            throw new InvalidOperationException($"Employee '{employee.EmployeeCode}' has no active salary structure covering this payroll period.");
        decimal nominalBasic = structure.BasicSalary;
        var currencyCode = SalaryPaymentMetadata.Normalize(structure.CurrencyCode);
        var configuredDecimals = configuredCurrencyDecimals != null && configuredCurrencyDecimals.TryGetValue(currencyCode, out var decimals)
            ? decimals
            : (int?)null;
        var roundingDecimals = PayrollCurrencyRules.GetCurrencyDecimals(structure.CurrencyCode, configuredDecimals);

        // 2. Dynamic Proration
        var prorationResult = await _prorationService.CalculateProrationFactorAsync(
            period.CompanyId,
            period.StartDate,
            period.EndDate,
            employee.HireDate,
            employee.TerminationDate,
            prorationPolicy,
            cancellationToken
        );

        var weightPolicy = prorationPolicy;
        var fixedThirtySegments = salarySegments.Count > 1 && string.Equals(prorationResult.MethodUsed, "FIXED_30", StringComparison.OrdinalIgnoreCase);
        if (fixedThirtySegments)
        {
            var weightMethod = prorationPolicy?.SalarySegmentAllocation switch
            {
                "CALENDAR_WEIGHTED" => "CALENDAR_DAYS",
                "WORKING_WEIGHTED" => "WORKING_DAYS",
                _ => throw new InvalidOperationException("Select CALENDAR_WEIGHTED or WORKING_WEIGHTED salary allocation for FIXED_30 transitions before calculation.")
            };
            weightPolicy = new ProrationPolicy { Method = weightMethod, Code = prorationPolicy!.Code };
        }
        var weightedSegments = new List<(PayrollSalarySegment Segment, decimal Factor)>();
        foreach (var segment in salarySegments)
        {
            var factor = prorationResult.ProrationFactor;
            if (salarySegments.Count > 1)
            {
                var segmentProration = await _prorationService.CalculateProrationFactorAsync(period.CompanyId,
                    period.StartDate, period.EndDate, segment.Start, segment.End, weightPolicy, cancellationToken);
                factor = segmentProration.ProrationFactor;
            }
            weightedSegments.Add((segment, factor));
        }
        if (fixedThirtySegments)
        {
            var totalWeight = weightedSegments.Sum(s => s.Factor);
            if (totalWeight <= 0m)
                throw new InvalidOperationException("The selected salary allocation has no eligible days. Review the work calendar and policy.");
            decimal allocated = 0m;
            for (var index = 0; index < weightedSegments.Count; index++)
            {
                var item = weightedSegments[index];
                var factor = index == weightedSegments.Count - 1 ? prorationResult.ProrationFactor - allocated
                    : Math.Round(prorationResult.ProrationFactor * item.Factor / totalWeight, 6);
                weightedSegments[index] = (item.Segment, factor);
                allocated += factor;
            }
        }
        decimal proratedBasic = PayrollCurrencyRules.Round(weightedSegments.Sum(s => s.Segment.Structure.BasicSalary * s.Factor), roundingDecimals, roundingRules);

        // 2b. Unpaid Leave deduction
        decimal unpaidLeaveDays = 0m;
        decimal unpaidLeaveDeduction = 0m;
        foreach (var item in weightedSegments)
        {
            var days = await _leaveService.GetUnpaidLeaveDaysInPeriodAsync(employee.EmployeeCode,
                item.Segment.Start, item.Segment.End, cancellationToken);
            unpaidLeaveDays += days;
            if (days > 0m && prorationResult.TotalBaseDays > 0m)
                unpaidLeaveDeduction += item.Segment.Structure.BasicSalary / prorationResult.TotalBaseDays * days;
        }
        unpaidLeaveDeduction = PayrollCurrencyRules.Round(unpaidLeaveDeduction, roundingDecimals, roundingRules);
        proratedBasic = Math.Max(0m, proratedBasic - unpaidLeaveDeduction);

        var components = new List<PayrollRunLineComponent>();

        // Basic line component
        components.Add(new PayrollRunLineComponent
        {
            ComponentCode = "BASIC",
            ComponentNameLocal = "الراتب الأساسي",
            ComponentNameEn = "Basic Salary",
            ComponentType = "EARNING",
            Amount = proratedBasic
        });

        if (unpaidLeaveDeduction > 0)
        {
            components.Add(new PayrollRunLineComponent
            {
                ComponentCode = "UNPAID_LEAVE",
                ComponentNameLocal = "خصم إجازة بدون راتب",
                ComponentNameEn = "Unpaid Leave Deduction",
                ComponentType = "DEDUCTION",
                Amount = unpaidLeaveDeduction
            });
        }

        // 3. Allowances & Fixed Structure Lines
        decimal totalAllowances = 0m;
        decimal taxableAllowances = 0m;
        decimal sscAllowances = 0m;
        decimal otherFixedDeductions = 0m;

        foreach (var item in weightedSegments)
        {
            foreach (var line in item.Segment.Structure.Lines.Where(l => l.IsActive))
            {
                var rawComponentAmount = line.Component is null
                    ? (line.Percent is > 0m
                        ? item.Segment.Structure.BasicSalary * (line.Percent.Value / 100m)
                        : line.Amount)
                    : SalaryStructureLineRules.ResolveAmount(line, line.Component, item.Segment.Structure.BasicSalary);
                var compAmount = PayrollCurrencyRules.Round(rawComponentAmount, roundingDecimals, roundingRules);

                // Prorate allowances according to proration factor
                decimal proratedCompAmount = PayrollCurrencyRules.Round(compAmount * item.Factor, roundingDecimals, roundingRules);

                var compType = line.Component?.ComponentType?.Trim().ToUpperInvariant() ?? "ALLOWANCE";
                if (compType == "ALLOWANCE" || compType == "EARNING")
                {
                    totalAllowances += proratedCompAmount;
                    if (line.Component?.IsTaxable != false) taxableAllowances += proratedCompAmount;
                    if (line.Component?.IsSscApplicable != false) sscAllowances += proratedCompAmount;
                    components.Add(new PayrollRunLineComponent
                    {
                        ComponentCode = line.ComponentCode,
                        ComponentNameLocal = line.Component?.NameLocal ?? line.ComponentCode,
                        ComponentNameEn = line.Component?.NameEn ?? line.ComponentCode,
                        ComponentType = "EARNING",
                        Amount = proratedCompAmount
                    });
                }
                else if (compType == "DEDUCTION")
                {
                    otherFixedDeductions += proratedCompAmount;
                    components.Add(new PayrollRunLineComponent
                    {
                        ComponentCode = line.ComponentCode,
                        ComponentNameLocal = line.Component?.NameLocal ?? line.ComponentCode,
                        ComponentNameEn = line.Component?.NameEn ?? line.ComponentCode,
                        ComponentType = "DEDUCTION",
                        Amount = proratedCompAmount
                    });
                }
            }
        }

        // PAY-15/PAY-17: raw attendance overtime is never paid automatically.
        // PAY-17 makes attendance overtime payable only through an explicitly approved
        // HR_PAYROLL_ADJUSTMENT, which is included with other approved adjustments below.
        decimal overtimeHours = 0m;
        decimal overtimeEarnings = 0m;


        // 5. Variable Adjustments (from HR_PAYROLL_ADJUSTMENT)
        decimal variableEarnings = 0m;
        decimal taxableVariable = 0m;
        decimal sscVariable = 0m;
        decimal variableDeductions = 0m;

        foreach (var adj in adjustments)
        {
            var adjustmentAmount = PayrollCurrencyRules.Round(adj.Amount, roundingDecimals, roundingRules);
            if (adj.AdjustmentType == "EARNING")
            {
                variableEarnings += adjustmentAmount;
                if (adj.Component?.IsTaxable != false) taxableVariable += adjustmentAmount;
                if (adj.Component?.IsSscApplicable != false) sscVariable += adjustmentAmount;
                components.Add(new PayrollRunLineComponent
                {
                    ComponentCode = adj.ComponentCode,
                    ComponentNameLocal = adj.Component?.NameLocal ?? adj.ComponentCode,
                    ComponentNameEn = adj.Component?.NameEn ?? adj.ComponentCode,
                    ComponentType = "EARNING",
                    Amount = adjustmentAmount
                });
            }
            else if (adj.AdjustmentType == "DEDUCTION")
            {
                variableDeductions += adjustmentAmount;
                components.Add(new PayrollRunLineComponent
                {
                    ComponentCode = adj.ComponentCode,
                    ComponentNameLocal = adj.Component?.NameLocal ?? adj.ComponentCode,
                    ComponentNameEn = adj.Component?.NameEn ?? adj.ComponentCode,
                    ComponentType = "DEDUCTION",
                    Amount = adjustmentAmount
                });
            }
        }

        // 6. Gross Pay Calculation
        decimal totalEarnings = totalAllowances + overtimeEarnings + variableEarnings;
        decimal grossSalary = proratedBasic + totalEarnings;

        // 7. SSC Calculation
        // In statutory rules: Basic + applicable allowances form the SSC eligible salary
        decimal sscEligibleSalary = proratedBasic + sscAllowances + sscVariable;
        var sscResult = _sscService.CalculateSSC(sscEligibleSalary, employee.IsHighRiskRole, sscPolicy, roundingDecimals, roundingRules)
            ?? _sscService.CalculateSSC(sscEligibleSalary, employee.IsHighRiskRole, sscPolicy, roundingDecimals)
            ?? _sscService.CalculateSSC(sscEligibleSalary, employee.IsHighRiskRole, sscPolicy)
            ?? new SSCResult(0, 0, 0, 0, 0, 0, 0, "DEFAULT");

        components.Add(new PayrollRunLineComponent
        {
            ComponentCode = "SSC_EMP",
            ComponentNameLocal = "اقتطاع الضمان الاجتماعي (موظف)",
            ComponentNameEn = "Social Security Contribution (Employee)",
            ComponentType = "DEDUCTION",
            Amount = sscResult.EmployeeContribution
        });

        components.Add(new PayrollRunLineComponent
        {
            ComponentCode = "SSC_EMPR",
            ComponentNameLocal = "مساهمة الضمان الاجتماعي (صاحب العمل)",
            ComponentNameEn = "Social Security Contribution (Employer)",
            ComponentType = "EMPLOYER_CONTRIB",
            Amount = sscResult.EmployerContribution
        });

        // 8. Income Tax Calculation
        int dependentCount = employee.Dependents.Count(d => d.IsActive && d.IsTaxExemptionClaimed) + employee.TaxExemptionCount;
        var taxableGross = proratedBasic + taxableAllowances + taxableVariable + overtimeEarnings;
        var taxResult = _taxEngine.CalculateIncomeTax(taxableGross, sscResult.EmployeeContribution, dependentCount, taxPolicy, roundingDecimals, roundingRules)
            ?? _taxEngine.CalculateIncomeTax(taxableGross, sscResult.EmployeeContribution, dependentCount, taxPolicy, roundingDecimals)
            ?? _taxEngine.CalculateIncomeTax(taxableGross, sscResult.EmployeeContribution, dependentCount, taxPolicy)
            ?? new TaxResult(0, 0, 0, 0, 0, 0, "DEFAULT");

        components.Add(new PayrollRunLineComponent
        {
            ComponentCode = "TAX_INCOME",
            ComponentNameLocal = "ضريبة الدخل المستقطعة",
            ComponentNameEn = "Income Tax Withheld",
            ComponentType = "DEDUCTION",
            Amount = taxResult.MonthlyTax
        });

        if (taxResult.MonthlyNationalSolidarityContrib > 0)
        {
            components.Add(new PayrollRunLineComponent
            {
                ComponentCode = "TAX_SOLIDARITY",
                ComponentNameLocal = "مساهمة التكافل الوطني",
                ComponentNameEn = "National Solidarity Contribution",
                ComponentType = "DEDUCTION",
                Amount = taxResult.MonthlyNationalSolidarityContrib
            });
        }

        // 9. Pre-Loan Net Pay & Loan Deductions
        decimal preLoanDeductions = sscResult.EmployeeContribution + taxResult.MonthlyTax + taxResult.MonthlyNationalSolidarityContrib + otherFixedDeductions + variableDeductions;
        decimal netPayBeforeLoans = grossSalary - preLoanDeductions;

        var loanResult = await _loanDeductionService.CalculateAndApplyDeductionsAsync(
            runLineId: null,
            employee.EmployeeCode,
            period.PeriodCode,
            netPayBeforeLoans,
            deductionPolicy,
            persistChanges: false,
            cancellationToken: cancellationToken,
            payrollCurrencyCode: structure.CurrencyCode ?? string.Empty
        );

        if (loanResult.TotalActualDeduction > 0)
        {
            components.Add(new PayrollRunLineComponent
            {
                ComponentCode = "LOAN_DEDUCTION",
                ComponentNameLocal = "اقتطاع السلف والقروض",
                ComponentNameEn = "Loans & Advances Deduction",
                ComponentType = "DEDUCTION",
                Amount = loanResult.TotalActualDeduction
            });
        }

        decimal totalOtherDeductions = otherFixedDeductions + variableDeductions + loanResult.TotalActualDeduction;
        decimal totalDeductions = sscResult.EmployeeContribution + taxResult.MonthlyTax + taxResult.MonthlyNationalSolidarityContrib + totalOtherDeductions;
        decimal netPay = grossSalary - totalDeductions;

        var runLine = new PayrollRunLine
        {
            EmployeeCode = employee.EmployeeCode,
            BranchId = employee.BranchId,
            DepartmentCode = employee.DepartmentCode,
            CostCenterCode = !string.IsNullOrWhiteSpace(employee.DepartmentCode) ? employee.DepartmentCode : structure?.Lines.FirstOrDefault()?.Component?.GlAccountCode,
            BasicSalary = proratedBasic,
            TotalEarnings = totalEarnings,
            GrossSalary = grossSalary,
            SscEligibleSalary = sscResult.EligibleSalary,
            SscEmployeeContrib = sscResult.EmployeeContribution,
            SscEmployerContrib = sscResult.EmployerContribution,
            TaxableGross = taxResult.MonthlyTaxableGross,
            AnnualExemptions = taxResult.TotalAnnualExemptions,
            AnnualTaxableNet = taxResult.AnnualTaxableNet,
            IncomeTaxWithheld = taxResult.MonthlyTax,
            NationalContribWithheld = taxResult.MonthlyNationalSolidarityContrib,
            OtherDeductions = totalOtherDeductions,
            TotalDeductions = totalDeductions,
            NetPay = netPay,
            PaymentMethod = structure?.PaymentMethod ?? "BANK_TRANSFER",
            CurrencyCode = structure?.CurrencyCode,
            BeneficiaryName = !string.IsNullOrWhiteSpace(employee.NameLocal) ? employee.NameLocal
                : !string.IsNullOrWhiteSpace(employee.NameEn) ? employee.NameEn : employee.EmployeeCode,
            BeneficiaryNationalId = employee.NationalId,
            BankCode = employee.BankName,
            Iban = employee.BankIban,
            Status = "CALCULATED",
            CreationUser = "SYSTEM",
            CreationDate = DateTime.UtcNow,
            Components = components
        };

        // 10. Build Snapshot and Explanation JSON
        var snapshotDetails = new
        {
            employeeCode = employee.EmployeeCode,
            employeeName = employee.NameLocal,
            payPeriod = period.PeriodCode,
            nominalBasicSalary = nominalBasic,
            salarySegmentAllocation = fixedThirtySegments ? prorationPolicy!.SalarySegmentAllocation : prorationResult.MethodUsed,
            salarySegments = weightedSegments.Select(s => new
            {
                structureId = s.Segment.Structure.Id,
                startDate = s.Segment.Start,
                endDate = s.Segment.End,
                basicSalary = s.Segment.Structure.BasicSalary,
                factor = s.Factor,
                basicEarned = PayrollCurrencyRules.Round(s.Segment.Structure.BasicSalary * s.Factor, roundingDecimals, roundingRules),
                paymentMethod = s.Segment.Structure.PaymentMethod,
                currencyCode = s.Segment.Structure.CurrencyCode
            }).ToArray(),
            hasMixedPaymentMethods = weightedSegments.Select(s => s.Segment.Structure.PaymentMethod).Distinct().Count() > 1,
            proration = new
            {
                factor = prorationResult.ProrationFactor,
                method = prorationResult.MethodUsed,
                policyCode = prorationPolicy?.Code ?? "DEFAULT",
                totalBaseDays = prorationResult.TotalBaseDays,
                eligibleDays = prorationResult.EligibleDays,
                proratedBasicSalary = proratedBasic
            },
            allowances = totalAllowances,
            overtime = new
            {
                hours = overtimeHours,
                earnings = overtimeEarnings
            },
            variableEarnings,
            grossSalary,
            ssc = new
            {
                policyCode = sscPolicy?.Code ?? "DEFAULT",
                eligibleSalary = sscResult.EligibleSalary,
                empRate = sscResult.EmployeeRate,
                emprRate = sscResult.EmployerRate,
                isHighRisk = employee.IsHighRiskRole,
                highRiskSurcharge = sscResult.HighRiskSurcharge,
                empContribution = sscResult.EmployeeContribution,
                emprContribution = sscResult.EmployerContribution
            },
            tax = new
            {
                policyCode = taxPolicy?.Code ?? "DEFAULT",
                annualGross = taxResult.AnnualGross,
                annualExemptions = taxResult.TotalAnnualExemptions,
                annualTaxableNet = taxResult.AnnualTaxableNet,
                monthlyIncomeTax = taxResult.MonthlyTax,
                monthlyNationalContrib = taxResult.MonthlyNationalSolidarityContrib
            },
            loans = new
            {
                policyCode = deductionPolicy?.Code ?? "DEFAULT",
                scheduled = loanResult.TotalScheduledDeduction,
                actual = loanResult.TotalActualDeduction,
                carriedForward = loanResult.TotalCarriedForward
            },
            netPay
        };

        runLine.Snapshot = new PayrollCalculationSnapshot
        {
            EmployeeCode = employee.EmployeeCode,
            PayPeriod = period.PeriodCode,
            CalculationTimestamp = DateTime.UtcNow,
            TotalBaseDays = prorationResult.TotalBaseDays,
            EligibleDays = prorationResult.EligibleDays,
            ProrationFactor = prorationResult.ProrationFactor,
            ProrationPolicyCode = prorationPolicy?.Code ?? "DEFAULT",
            ProrationMethodUsed = prorationResult.MethodUsed,
            OvertimeHoursApplied = overtimeHours,
            OvertimeEarningsApplied = overtimeEarnings,
            SscPolicyCode = sscPolicy?.Code ?? "DEFAULT",
            SscEmpRateApplied = sscResult.EmployeeRate,
            SscEmprRateApplied = sscResult.EmployerRate,
            SscCapApplied = sscPolicy?.MonthlyCeilingCap ?? 0m,
            TaxPolicyCode = taxPolicy?.Code ?? "DEFAULT",
            TaxExemptionsApplied = taxResult.TotalAnnualExemptions,
            LoanDeductionsApplied = loanResult.TotalActualDeduction,
            LoanDeductionsCarriedFwd = loanResult.TotalCarriedForward,
            ExplanationJson = JsonSerializer.Serialize(snapshotDetails, new JsonSerializerOptions { WriteIndented = true })
        };

        return runLine;
    }
}
