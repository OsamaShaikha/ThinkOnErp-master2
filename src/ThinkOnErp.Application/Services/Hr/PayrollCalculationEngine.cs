using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using ThinkOnErp.Domain.Entities.Hr;
using ThinkOnErp.Domain.Exceptions;
using ThinkOnErp.Domain.Interfaces.Hr;

namespace ThinkOnErp.Application.Services.Hr;

public sealed class PayrollCalculationEngine : IPayrollCalculationEngine
{
    private readonly IEmployeeRepository _employeeRepository;
    private readonly ICompensationRepository _compensationRepository;
    private readonly ILeaveRepository _leaveRepository;
    private readonly IPayrollProrationService _prorationService;
    private readonly IOvertimeCalculationService _overtimeService;
    private readonly ISSCCalculationService _sscService;
    private readonly ITaxCalculationEngine _taxEngine;
    private readonly ILoanDeductionService _loanDeductionService;
    private readonly IPayrollPeriodRepository _payrollPeriodRepo;
    private readonly ILogger<PayrollCalculationEngine> _logger;

    public PayrollCalculationEngine(
        IEmployeeRepository employeeRepository,
        ICompensationRepository compensationRepository,
        ILeaveRepository leaveRepository,
        IPayrollProrationService prorationService,
        IOvertimeCalculationService overtimeService,
        ISSCCalculationService sscService,
        ITaxCalculationEngine taxEngine,
        ILoanDeductionService loanDeductionService,
        IPayrollPeriodRepository payrollPeriodRepo,
        ILogger<PayrollCalculationEngine> logger)
    {
        _employeeRepository = employeeRepository ?? throw new ArgumentNullException(nameof(employeeRepository));
        _compensationRepository = compensationRepository ?? throw new ArgumentNullException(nameof(compensationRepository));
        _leaveRepository = leaveRepository ?? throw new ArgumentNullException(nameof(leaveRepository));
        _prorationService = prorationService ?? throw new ArgumentNullException(nameof(prorationService));
        _overtimeService = overtimeService ?? throw new ArgumentNullException(nameof(overtimeService));
        _sscService = sscService ?? throw new ArgumentNullException(nameof(sscService));
        _taxEngine = taxEngine ?? throw new ArgumentNullException(nameof(taxEngine));
        _loanDeductionService = loanDeductionService ?? throw new ArgumentNullException(nameof(loanDeductionService));
        _payrollPeriodRepo = payrollPeriodRepo ?? throw new ArgumentNullException(nameof(payrollPeriodRepo));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<PayrollRunLine> CalculateEmployeePayrollAsync(
        string employeeCode,
        string payPeriod,
        long payrollRunId,
        string currentUser)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(employeeCode);
        ArgumentException.ThrowIfNullOrWhiteSpace(payPeriod);

        // 1. Determine pay period target date
        var parts = payPeriod.Split('-');
        if (parts.Length != 2 || !int.TryParse(parts[0], out var year) || !int.TryParse(parts[1], out var month))
        {
            throw new HrValidationException($"صيغة فترة الراتب غير صحيحة ({payPeriod}). الصيغة المطلوبة هي YYYY-MM.", "INVALID_PAY_PERIOD");
        }

        var periodStart = new DateTime(year, month, 1, 0, 0, 0, DateTimeKind.Utc);
        var periodEnd = periodStart.AddMonths(1).AddDays(-1);

        var employee = await _employeeRepository.GetByCodeAsync(employeeCode, includeDetails: true);
        if (employee == null)
        {
            throw new HrNotFoundException($"الموظف ({employeeCode}) غير موجود.", "EMPLOYEE_NOT_FOUND");
        }

        var structure = await _compensationRepository.GetActiveStructureAsync(employeeCode, periodStart);
        if (structure == null)
        {
            throw new HrValidationException($"لا توجد هيكلية رواتب فعالة للموظف ({employeeCode}) لشهر ({payPeriod}).", "NO_ACTIVE_SALARY_STRUCTURE");
        }

        var companyId = 1L; // Multi-tenant company context

        // 2. Proration Factor Calculation
        var proration = await _prorationService.CalculateProrationFactorAsync(
            employeeCode: employeeCode,
            hireDate: employee.HireDate,
            terminationDate: employee.TerminationDate,
            periodStart: periodStart,
            periodEnd: periodEnd,
            companyId: companyId);

        var proratedBasic = Math.Round(structure.BasicSalary * proration.ProrationFactor, 3);

        var line = new PayrollRunLine
        {
            PayrollRunId = payrollRunId,
            EmployeeCode = employeeCode,
            DepartmentCode = employee.DepartmentCode,
            CostCenterCode = employee.Department?.CostCenterCode,
            BranchId = employee.BranchId,
            BasicSalary = proratedBasic,
            PaymentMethod = structure.PaymentMethod,
            BankCode = employee.BankName,
            Iban = employee.BankIban,
            CreationUser = string.IsNullOrWhiteSpace(currentUser) ? "SYSTEM" : currentUser,
            CreationDate = DateTime.UtcNow
        };

        // Add Basic Salary line component
        line.Components.Add(new PayrollRunLineComponent
        {
            ComponentCode = "BASIC",
            ComponentNameEn = "Basic Salary",
            ComponentNameAr = "الراتب الأساسي",
            ComponentType = "EARNING",
            Amount = proratedBasic,
            CreationDate = DateTime.UtcNow
        });

        // 3. Add active structure allowances (prorated if configured as recurring earnings)
        foreach (var structLine in structure.Lines.Where(l => l.IsActive))
        {
            var comp = structLine.Component;
            var isEarning = comp?.ComponentType == "EARNING" || comp?.ComponentType == "ALLOWANCE" || comp == null;
            var componentAmount = isEarning ? Math.Round(structLine.Amount * proration.ProrationFactor, 3) : structLine.Amount;

            line.Components.Add(new PayrollRunLineComponent
            {
                ComponentCode = structLine.ComponentCode,
                ComponentNameEn = comp?.NameEn ?? structLine.ComponentCode,
                ComponentNameAr = comp?.NameAr ?? structLine.ComponentCode,
                ComponentType = isEarning ? "EARNING" : "DEDUCTION",
                Amount = componentAmount,
                GlAccountCode = comp?.GlAccountCode,
                CreationDate = DateTime.UtcNow
            });
        }

        // 4. Dynamic Overtime Calculation
        var overtimeEarnings = await _overtimeService.CalculateOvertimeEarningsAsync(
            employeeCode: employeeCode,
            basicSalary: structure.BasicSalary,
            fromDate: periodStart,
            toDate: periodEnd,
            companyId: companyId);

        if (overtimeEarnings > 0)
        {
            line.Components.Add(new PayrollRunLineComponent
            {
                ComponentCode = "OVERTIME",
                ComponentNameEn = "Approved Overtime",
                ComponentNameAr = "العمل الإضافي المعتمد",
                ComponentType = "EARNING",
                Amount = overtimeEarnings,
                CreationDate = DateTime.UtcNow
            });
        }

        // 5. Unpaid Leave Impact Calculation
        var leaves = await _leaveRepository.GetRequestsAsync(employeeCode: employeeCode, status: "APPROVED", fromDate: periodStart, toDate: periodEnd);
        var unpaidLeaves = leaves.Where(l => l.LeaveTypeCode == "UNPAID").ToList();
        var unpaidDaysTotal = 0m;

        foreach (var l in unpaidLeaves)
        {
            var actualStart = l.StartDate < periodStart ? periodStart : l.StartDate;
            var actualEnd = l.EndDate > periodEnd ? periodEnd : l.EndDate;
            unpaidDaysTotal += (decimal)(actualEnd.Date - actualStart.Date).TotalDays + 1m;
        }

        if (unpaidDaysTotal > 0)
        {
            var dailyRate = structure.BasicSalary / 30m;
            var unpaidDeduction = Math.Round(unpaidDaysTotal * dailyRate, 3);
            line.Components.Add(new PayrollRunLineComponent
            {
                ComponentCode = "UNPAID_LEAVE",
                ComponentNameEn = $"Unpaid Leave ({unpaidDaysTotal} days)",
                ComponentNameAr = $"خصم إجازة بدون راتب ({unpaidDaysTotal} يوم)",
                ComponentType = "DEDUCTION",
                Amount = unpaidDeduction,
                CreationDate = DateTime.UtcNow
            });
        }

        // 6. Gross Salary Computation
        line.TotalEarnings = line.Components.Where(c => c.ComponentType == "EARNING").Sum(c => c.Amount);
        line.GrossSalary = line.TotalEarnings;

        // 7. Dynamic Social Security (SSC) Calculation
        var sscSubjectSum = line.Components
            .Where(c => c.ComponentType == "EARNING" && (c.ComponentCode == "BASIC" || c.ComponentCode == "HOUSING" || c.ComponentCode == "TRANSPORT" || c.ComponentCode == "OVERTIME"))
            .Sum(c => c.Amount);

        var sscResult = await _sscService.CalculateSSCAsync(
            sscGrossEarnings: sscSubjectSum,
            isHighRiskRole: employee.IsHighRiskRole,
            companyId: companyId,
            calculationDate: periodStart);

        line.SscEligibleSalary = sscResult.SscEligibleSalary;
        line.SscEmployeeContribution = sscResult.EmployeeContribution;
        line.SscEmployerContribution = sscResult.EmployerContribution;

        line.Components.Add(new PayrollRunLineComponent
        {
            ComponentCode = "SSC_EMPLOYEE",
            ComponentNameEn = "SSC Employee Contribution",
            ComponentNameAr = "اقتطاع الضمان الاجتماعي",
            ComponentType = "DEDUCTION",
            Amount = line.SscEmployeeContribution,
            CreationDate = DateTime.UtcNow
        });

        // 8. Dynamic Progressive Income Tax Calculation
        var taxableGrossMonthly = Math.Max(0, line.GrossSalary - line.SscEmployeeContribution);
        line.TaxableGross = taxableGrossMonthly;

        var taxResult = await _taxEngine.CalculateTaxAsync(
            monthlyTaxableGross: taxableGrossMonthly,
            taxExemptionCount: employee.TaxExemptionCount,
            companyId: companyId,
            calculationDate: periodStart);

        line.AnnualExemptions = taxResult.AnnualExemptionsApplied;
        line.AnnualTaxableNet = taxResult.AnnualTaxableNet;
        line.IncomeTaxWithheld = taxResult.MonthlyIncomeTax;
        line.NationalContributionWithheld = taxResult.MonthlyNationalContribution;

        if (line.IncomeTaxWithheld > 0)
        {
            line.Components.Add(new PayrollRunLineComponent
            {
                ComponentCode = "INCOME_TAX",
                ComponentNameEn = "Income Tax Withholding",
                ComponentNameAr = "ضريبة الدخل المستقطعة",
                ComponentType = "DEDUCTION",
                Amount = line.IncomeTaxWithheld,
                CreationDate = DateTime.UtcNow
            });
        }

        if (line.NationalContributionWithheld > 0)
        {
            line.Components.Add(new PayrollRunLineComponent
            {
                ComponentCode = "NATIONAL_CONTRIB",
                ComponentNameEn = "National Contribution Surcharge (1%)",
                ComponentNameAr = "المساهمة الوطنية للتكافل",
                ComponentType = "DEDUCTION",
                Amount = line.NationalContributionWithheld,
                CreationDate = DateTime.UtcNow
            });
        }

        // 9. Disposable Salary for Loans & Advance Deductions
        var preLoanNet = line.GrossSalary - line.SscEmployeeContribution - line.IncomeTaxWithheld - line.NationalContributionWithheld;

        var loanResult = await _loanDeductionService.ProcessPeriodLoanDeductionsAsync(
            employeeCode: employeeCode,
            payPeriod: payPeriod,
            disposableSalary: preLoanNet,
            companyId: companyId,
            calculationDate: periodStart);

        if (loanResult.TotalDeductedAmount > 0)
        {
            foreach (var item in loanResult.Items.Where(i => i.DeductedAmount > 0))
            {
                line.Components.Add(new PayrollRunLineComponent
                {
                    ComponentCode = item.LoanType,
                    ComponentNameEn = item.LoanType == "SALARY_ADVANCE" ? "Salary Advance Repayment" : $"Loan Installment ({item.LoanType})",
                    ComponentNameAr = item.LoanType == "SALARY_ADVANCE" ? "استرداد سلفة راتب" : $"قسط قرض ({item.LoanType})",
                    ComponentType = "DEDUCTION",
                    Amount = item.DeductedAmount,
                    CreationDate = DateTime.UtcNow
                });
            }
        }

        // 10. Other Deductions & Total Net Pay
        var otherDeductions = line.Components
            .Where(c => c.ComponentType == "DEDUCTION" && c.ComponentCode != "SSC_EMPLOYEE" && c.ComponentCode != "INCOME_TAX" && c.ComponentCode != "NATIONAL_CONTRIB")
            .Sum(c => c.Amount);

        line.OtherDeductions = otherDeductions;
        line.TotalDeductions = line.SscEmployeeContribution + line.IncomeTaxWithheld + line.NationalContributionWithheld + line.OtherDeductions;
        line.NetPay = Math.Round(line.GrossSalary - line.TotalDeductions, 3);

        if (line.NetPay < 0)
        {
            throw new HrValidationException($"صافي الراتب للموظف ({employeeCode}) سالب ({line.NetPay} د.أ). يرجى مراجعة الاستقطاعات.", "NEGATIVE_NET_PAY");
        }

        // 11. Create and Attach Explainable Snapshot
        var explanationObj = new
        {
            EmployeeCode = employeeCode,
            PayPeriod = payPeriod,
            structure.BasicSalary,
            ProratedBasic = proratedBasic,
            ProrationFactor = proration.ProrationFactor,
            ProrationMethod = proration.MethodUsed,
            GrossSalary = line.GrossSalary,
            SscEligibleSalary = line.SscEligibleSalary,
            SscEmployeeContribution = line.SscEmployeeContribution,
            SscEmployerContribution = line.SscEmployerContribution,
            TaxableGross = line.TaxableGross,
            AnnualTaxableNet = line.AnnualTaxableNet,
            IncomeTaxWithheld = line.IncomeTaxWithheld,
            NationalContributionWithheld = line.NationalContributionWithheld,
            LoanDeducted = loanResult.TotalDeductedAmount,
            LoanCarriedForward = loanResult.TotalCarriedForwardAmount,
            NetPay = line.NetPay
        };

        _logger.LogDebug("Calculated explainable payroll for {Emp}: Gross {Gross}, SSC {Ssc}, Tax {Tax}, Loans {Loan}, Net {Net}", employeeCode, line.GrossSalary, line.SscEmployeeContribution, line.IncomeTaxWithheld, loanResult.TotalDeductedAmount, line.NetPay);

        return line;
    }
}
