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

    public PayrollCalculationEngine(
        IPolicyRepository policyRepository,
        IPayrollProrationService prorationService,
        IOvertimeCalculationService overtimeService,
        ISSCCalculationService sscService,
        ITaxCalculationEngine taxEngine,
        ILoanDeductionService loanDeductionService,
        IPayrollAdjustmentRepository adjustmentRepository)
    {
        _policyRepository = policyRepository;
        _prorationService = prorationService;
        _overtimeService = overtimeService;
        _sscService = sscService;
        _taxEngine = taxEngine;
        _loanDeductionService = loanDeductionService;
        _adjustmentRepository = adjustmentRepository;
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
        var allAdjustments = await _adjustmentRepository.GetApprovedAdjustmentsForPeriodAsync(period.CompanyId, period.PeriodCode, cancellationToken);

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

            var line = await CalculateEmployeePayrollLineAsync(
                period,
                employee,
                prorationPolicy,
                sscPolicy,
                taxPolicy,
                deductionPolicy,
                empAdjustments,
                cancellationToken
            );

            run.Lines.Add(line);
        }

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
        CancellationToken cancellationToken = default)
    {
        // 1. Get current active salary structure
        var structure = employee.SalaryStructures
            .Where(s => s.IsActive && s.EffectiveFrom <= period.EndDate && (!s.EffectiveTo.HasValue || s.EffectiveTo.Value >= period.StartDate))
            .OrderByDescending(s => s.EffectiveFrom)
            .FirstOrDefault();

        decimal nominalBasic = structure?.BasicSalary ?? 0m;

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

        decimal proratedBasic = Math.Round(nominalBasic * prorationResult.ProrationFactor, 3);

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

        // 3. Allowances & Fixed Structure Lines
        decimal totalAllowances = 0m;
        decimal otherFixedDeductions = 0m;

        if (structure?.Lines != null)
        {
            foreach (var line in structure.Lines.Where(l => l.IsActive))
            {
                decimal compAmount = line.Amount;
                if (line.Percent.HasValue && line.Percent.Value > 0)
                {
                    compAmount = Math.Round(nominalBasic * (line.Percent.Value / 100m), 3);
                }

                // Prorate allowances according to proration factor
                decimal proratedCompAmount = Math.Round(compAmount * prorationResult.ProrationFactor, 3);

                var compType = line.Component?.ComponentType ?? "ALLOWANCE";
                if (compType == "ALLOWANCE" || compType == "EARNING")
                {
                    totalAllowances += proratedCompAmount;
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

        // 4. Overtime (0 hours default unless integrated from daily attendance)
        decimal overtimeHours = 0m;
        decimal overtimeEarnings = 0m;

        // 5. Variable Adjustments (from HR_PAYROLL_ADJUSTMENT)
        decimal variableEarnings = 0m;
        decimal variableDeductions = 0m;

        foreach (var adj in adjustments)
        {
            if (adj.AdjustmentType == "EARNING")
            {
                variableEarnings += adj.Amount;
                components.Add(new PayrollRunLineComponent
                {
                    ComponentCode = adj.ComponentCode,
                    ComponentNameLocal = adj.Component?.NameLocal ?? adj.ComponentCode,
                    ComponentNameEn = adj.Component?.NameEn ?? adj.ComponentCode,
                    ComponentType = "EARNING",
                    Amount = adj.Amount
                });
            }
            else if (adj.AdjustmentType == "DEDUCTION")
            {
                variableDeductions += adj.Amount;
                components.Add(new PayrollRunLineComponent
                {
                    ComponentCode = adj.ComponentCode,
                    ComponentNameLocal = adj.Component?.NameLocal ?? adj.ComponentCode,
                    ComponentNameEn = adj.Component?.NameEn ?? adj.ComponentCode,
                    ComponentType = "DEDUCTION",
                    Amount = adj.Amount
                });
            }
        }

        // 6. Gross Pay Calculation
        decimal totalEarnings = totalAllowances + overtimeEarnings + variableEarnings;
        decimal grossSalary = proratedBasic + totalEarnings;

        // 7. SSC Calculation
        // In Jordanian labor law: Basic + applicable allowances form the SSC eligible salary
        decimal sscEligibleSalary = grossSalary;
        var sscResult = _sscService.CalculateSSC(sscEligibleSalary, employee.IsHighRiskRole, sscPolicy);

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
        var taxResult = _taxEngine.CalculateIncomeTax(grossSalary, sscResult.EmployeeContribution, dependentCount, taxPolicy);

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
            cancellationToken: cancellationToken
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
            CostCenterCode = structure?.Lines.FirstOrDefault()?.Component?.GlAccountCode,
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
