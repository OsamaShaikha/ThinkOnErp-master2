using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Moq;
using ThinkOnErp.Application.Services.Hr;
using ThinkOnErp.Domain.Entities.Hr;
using ThinkOnErp.Domain.Interfaces.Hr;
using Xunit;

namespace ThinkOnErp.HR.Tests;

public class PayrollCalculationEngineTests
{
    private readonly Mock<IPolicyRepository> _policyRepoMock;
    private readonly Mock<IPayrollProrationService> _prorationServiceMock;
    private readonly Mock<IOvertimeCalculationService> _overtimeServiceMock;
    private readonly Mock<ISSCCalculationService> _sscServiceMock;
    private readonly Mock<ITaxCalculationEngine> _taxEngineMock;
    private readonly Mock<ILoanDeductionService> _loanDeductionServiceMock;
    private readonly Mock<IPayrollAdjustmentRepository> _adjustmentRepoMock;
    private readonly Mock<ILeaveService> _leaveServiceMock;
    private readonly PayrollCalculationEngine _engine;

    public PayrollCalculationEngineTests()
    {
        _policyRepoMock = new Mock<IPolicyRepository>();
        _prorationServiceMock = new Mock<IPayrollProrationService>();
        _overtimeServiceMock = new Mock<IOvertimeCalculationService>();
        _sscServiceMock = new Mock<ISSCCalculationService>();
        _taxEngineMock = new Mock<ITaxCalculationEngine>();
        _loanDeductionServiceMock = new Mock<ILoanDeductionService>();
        _adjustmentRepoMock = new Mock<IPayrollAdjustmentRepository>();
        _leaveServiceMock = new Mock<ILeaveService>();

        _engine = new PayrollCalculationEngine(
            _policyRepoMock.Object,
            _prorationServiceMock.Object,
            _overtimeServiceMock.Object,
            _sscServiceMock.Object,
            _taxEngineMock.Object,
            _loanDeductionServiceMock.Object,
            _adjustmentRepoMock.Object,
            _leaveServiceMock.Object
        );
    }

    [Fact]
    public async Task FullPayrollCalculation_ComputesAllComponentsAndGeneratesSnapshot()
    {
        // 1. Setup Period
        var period = new PayrollPeriod
        {
            Id = 1,
            CompanyId = 1,
            PeriodCode = "2026-09",
            StartDate = new DateTime(2026, 9, 1),
            EndDate = new DateTime(2026, 9, 30)
        };

        // 2. Setup Employee with Salary Structure (Basic 1000 + Transport 100)
        var employee = new Employee
        {
            EmployeeCode = "EMP001",
            NameLocal = "أحمد خليل",
            HireDate = new DateTime(2022, 1, 1),
            IsActive = true,
            EmploymentStatus = "ACTIVE",
            IsHighRiskRole = false,
            TaxExemptionCount = 0
        };

        var structure = new SalaryStructure
        {
            EmployeeCode = employee.EmployeeCode,
            EffectiveFrom = new DateTime(2022, 1, 1),
            BasicSalary = 1000m,
            IsActive = true,
            Lines = new List<SalaryStructureLine>
            {
                new()
                {
                    ComponentCode = "TRANS",
                    Amount = 100m,
                    IsActive = true,
                    Component = new SalaryComponent { ComponentCode = "TRANS", NameLocal = "بدل مواصلات", ComponentType = "ALLOWANCE" }
                }
            }
        };
        employee.SalaryStructures.Add(structure);

        // 3. Setup Proration Mock (Full month -> 1.0 factor)
        _prorationServiceMock
            .Setup(p => p.CalculateProrationFactorAsync(1, period.StartDate, period.EndDate, employee.HireDate, null, It.IsAny<ProrationPolicy?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ProrationResult(1.0m, 30, 30, "CALENDAR_DAYS", "CAL_DEF"));

        // 4. Setup Variable Adjustments (Performance Bonus 150 JOD)
        var adjustments = new List<PayrollAdjustment>
        {
            new()
            {
                EmployeeCode = employee.EmployeeCode,
                PayPeriod = period.PeriodCode,
                ComponentCode = "PERF_BONUS",
                AdjustmentType = "EARNING",
                Amount = 150m,
                Status = "APPROVED",
                Component = new SalaryComponent { ComponentCode = "PERF_BONUS", NameLocal = "مكافأة تميز", ComponentType = "EARNING" }
            }
        };

        // 5. Setup SSC Mock (Gross = 1000 + 100 + 150 = 1250)
        // Emp SSC = 1250 * 7.5% = 93.750, Empr SSC = 1250 * 14.25% = 178.125
        _sscServiceMock
            .Setup(s => s.CalculateSSC(1250m, false, It.IsAny<SSCPolicy?>()))
            .Returns(new SSCResult(1250m, 93.750m, 178.125m, 0m, 0.075m, 0.1425m, 3617m, "DEF_SSC"));

        // 6. Setup Tax Mock (Tax = 15.500 JOD)
        _taxEngineMock
            .Setup(t => t.CalculateIncomeTax(1250m, 93.750m, 0, It.IsAny<TaxPolicy?>()))
            .Returns(new TaxResult(1156.25m, 9000m, 4875m, 15000m, 15.500m, 0m, "DEF_TAX"));

        // 7. Setup Loan Deduction Mock (50 JOD deduction)
        _loanDeductionServiceMock
            .Setup(l => l.CalculateAndApplyDeductionsAsync(null, employee.EmployeeCode, period.PeriodCode, It.IsAny<decimal>(), It.IsAny<DeductionPolicy?>(), false, It.IsAny<CancellationToken>(), It.IsAny<string?>()))
            .ReturnsAsync(new LoanDeductionCalculationResult(50m, 50m, 0m, new(), new()));

        // Act
        var line = await _engine.CalculateEmployeePayrollLineAsync(
            period,
            employee,
            null,
            null,
            null,
            null,
            adjustments
        );

        // Assert
        Assert.Equal("EMP001", line.EmployeeCode);
        Assert.Equal(1000m, line.BasicSalary);
        Assert.Equal(250m, line.TotalEarnings); // 100 transport + 150 bonus
        Assert.Equal(1250m, line.GrossSalary);
        Assert.Equal(93.750m, line.SscEmployeeContrib);
        Assert.Equal(178.125m, line.SscEmployerContrib);
        Assert.Equal(15.500m, line.IncomeTaxWithheld);
        Assert.Equal(50m, line.OtherDeductions); // 50 JOD loan deduction

        // Total deductions = 93.750 + 15.500 + 50 = 159.250
        // Net pay = 1250 - 159.250 = 1090.750
        Assert.Equal(159.250m, line.TotalDeductions);
        Assert.Equal(1090.750m, line.NetPay);

        // Check Snapshot
        Assert.NotNull(line.Snapshot);
        Assert.Equal(1.0m, line.Snapshot.ProrationFactor);
        Assert.Equal(50m, line.Snapshot.LoanDeductionsApplied);
        Assert.False(string.IsNullOrWhiteSpace(line.Snapshot.ExplanationJson));

        // Check Components breakdown
        Assert.Contains(line.Components, c => c.ComponentCode == "BASIC" && c.Amount == 1000m);
        Assert.Contains(line.Components, c => c.ComponentCode == "TRANS" && c.Amount == 100m);
        Assert.Contains(line.Components, c => c.ComponentCode == "PERF_BONUS" && c.Amount == 150m);
        Assert.Contains(line.Components, c => c.ComponentCode == "SSC_EMP" && c.Amount == 93.750m);
        Assert.Contains(line.Components, c => c.ComponentCode == "SSC_EMPR" && c.Amount == 178.125m);
        Assert.Contains(line.Components, c => c.ComponentCode == "TAX_INCOME" && c.Amount == 15.500m);
        Assert.Contains(line.Components, c => c.ComponentCode == "LOAN_DEDUCTION" && c.Amount == 50m);
    }

    [Fact]
    public async Task UnpaidLeave_DeductsFromBasic_AndAddsComponent()
    {
        var period = new PayrollPeriod
        {
            Id = 2,
            CompanyId = 1,
            PeriodCode = "2026-09",
            StartDate = new DateTime(2026, 9, 1),
            EndDate = new DateTime(2026, 9, 30)
        };

        var employee = new Employee
        {
            EmployeeCode = "EMP002",
            NameLocal = "سارة أحمد",
            HireDate = new DateTime(2021, 1, 1),
            IsActive = true,
            EmploymentStatus = "ACTIVE"
        };

        var structure = new SalaryStructure
        {
            EmployeeCode = employee.EmployeeCode,
            EffectiveFrom = new DateTime(2021, 1, 1),
            BasicSalary = 900m,
            IsActive = true
        };
        employee.SalaryStructures.Add(structure);

        // Proration 30 base days
        _prorationServiceMock
            .Setup(p => p.CalculateProrationFactorAsync(1, period.StartDate, period.EndDate, employee.HireDate, null, It.IsAny<ProrationPolicy?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ProrationResult(1.0m, 30, 30, "CALENDAR_DAYS", "CAL_DEF"));

        // 3 days of approved unpaid leave in this period
        // Daily rate = 900 / 30 = 30 JOD/day -> 3 days = 90 JOD deduction
        _leaveServiceMock
            .Setup(l => l.GetUnpaidLeaveDaysInPeriodAsync(employee.EmployeeCode, period.StartDate, period.EndDate, It.IsAny<CancellationToken>()))
            .ReturnsAsync(3m);

        _sscServiceMock
            .Setup(s => s.CalculateSSC(It.IsAny<decimal>(), false, It.IsAny<SSCPolicy?>()))
            .Returns(new SSCResult(810m, 60.75m, 115.425m, 0m, 0.075m, 0.1425m, 3617m, "DEF_SSC"));

        _taxEngineMock
            .Setup(t => t.CalculateIncomeTax(It.IsAny<decimal>(), It.IsAny<decimal>(), 0, It.IsAny<TaxPolicy?>()))
            .Returns(new TaxResult(749.25m, 9000m, 0m, 9720m, 0m, 0m, "DEF_TAX"));

        _loanDeductionServiceMock
            .Setup(l => l.CalculateAndApplyDeductionsAsync(null, employee.EmployeeCode, period.PeriodCode, It.IsAny<decimal>(), It.IsAny<DeductionPolicy?>(), false, It.IsAny<CancellationToken>(), It.IsAny<string?>()))
            .ReturnsAsync(new LoanDeductionCalculationResult(0m, 0m, 0m, new(), new()));

        var line = await _engine.CalculateEmployeePayrollLineAsync(
            period,
            employee,
            null,
            null,
            null,
            null,
            new List<PayrollAdjustment>()
        );

        // Basic salary after 90 JOD unpaid leave deduction = 810 JOD
        Assert.Equal(810m, line.BasicSalary);
        Assert.Equal(810m, line.GrossSalary);
        Assert.Contains(line.Components, c => c.ComponentCode == "UNPAID_LEAVE" && c.Amount == 90m);
    }

    private (PayrollPeriod Period, Employee Employee) PrepareRegressionPayroll()
    {
        var period = new PayrollPeriod { CompanyId = 1, PeriodCode = "2026-10", StartDate = new(2026, 10, 1), EndDate = new(2026, 10, 31) };
        var employee = new Employee { EmployeeCode = "REGRESSION", HireDate = new(2020, 1, 1) };
        employee.SalaryStructures.Add(new SalaryStructure { BasicSalary = 1000, EffectiveFrom = new(2020, 1, 1) });
        _prorationServiceMock.Setup(p => p.CalculateProrationFactorAsync(1, period.StartDate, period.EndDate, employee.HireDate, null, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ProrationResult(1, 31, 31, "CALENDAR_DAYS", "TEST"));
        _sscServiceMock.Setup(s => s.CalculateSSC(It.IsAny<decimal>(), false, null)).Returns(new SSCResult(1000, 0, 0, 0, 0, 0, 0, "TEST"));
        _taxEngineMock.Setup(t => t.CalculateIncomeTax(It.IsAny<decimal>(), 0, It.IsAny<int>(), null)).Returns(new TaxResult(0, 0, 0, 0, 0, 0, "TEST"));
        _loanDeductionServiceMock.Setup(l => l.CalculateAndApplyDeductionsAsync(null, employee.EmployeeCode, period.PeriodCode, It.IsAny<decimal>(), null, false, It.IsAny<CancellationToken>(), It.IsAny<string?>()))
            .ReturnsAsync(new LoanDeductionCalculationResult(0, 0, 0, new(), new()));
        return (period, employee);
    }

    [Fact]
    public async Task ExemptComponentsRemainInGrossButExcludedFromTaxAndSsc()
    {
        var (period, employee) = PrepareRegressionPayroll();
        employee.SalaryStructures[0].Lines.Add(new SalaryStructureLine { Amount = 100, ComponentCode = "EXEMPT", Component = new SalaryComponent { ComponentType = "ALLOWANCE", IsTaxable = false, IsSscApplicable = false } });
        var adjustments = new[] { new PayrollAdjustment { Amount = 50, AdjustmentType = "EARNING", ComponentCode = "BONUS", Component = new SalaryComponent { IsTaxable = true, IsSscApplicable = false } } };
        var line = await _engine.CalculateEmployeePayrollLineAsync(period, employee, null, null, null, null, adjustments);
        Assert.Equal(1150m, line.GrossSalary);
        _sscServiceMock.Verify(s => s.CalculateSSC(1000m, false, null), Times.Once);
        _taxEngineMock.Verify(t => t.CalculateIncomeTax(1050m, 0, 0, null), Times.Once);
    }

    [Fact]
    public async Task OvertimeDoesNotEnterPayrollWhileFeatureIsDeferred()
    {
        var (period, employee) = PrepareRegressionPayroll();
        var attendance = new Mock<IAttendanceCorrectionRepository>();
        attendance.Setup(a => a.GetAttendanceDaysAsync(employee.EmployeeCode, period.StartDate, period.EndDate, It.IsAny<CancellationToken>())).ReturnsAsync(new[] {
            new AttendanceDay { AttendanceDate = period.StartDate, OvertimeHours = 2, LastCheckOut = period.StartDate.AddHours(18) },
            new AttendanceDay { AttendanceDate = period.StartDate.AddDays(1), OvertimeHours = 4, HasMissingPunch = true }
        });
        _overtimeServiceMock.Setup(o => o.CalculateOvertimeEarningsAsync(1, 1000, 2, "NORMAL", period.StartDate, It.IsAny<CancellationToken>())).ReturnsAsync(20m);
        var engine = new PayrollCalculationEngine(_policyRepoMock.Object, _prorationServiceMock.Object, _overtimeServiceMock.Object,
            _sscServiceMock.Object, _taxEngineMock.Object, _loanDeductionServiceMock.Object, _adjustmentRepoMock.Object, _leaveServiceMock.Object, attendance.Object);
        var line = await engine.CalculateEmployeePayrollLineAsync(period, employee, null, null, null, null, Array.Empty<PayrollAdjustment>());
        Assert.Equal(1000m, line.GrossSalary);
        Assert.Equal(0m, line.Snapshot!.OvertimeHoursApplied);
        Assert.Equal(0m, line.Snapshot.OvertimeEarningsApplied);
        _overtimeServiceMock.Verify(o => o.CalculateOvertimeEarningsAsync(It.IsAny<long>(), It.IsAny<decimal>(), It.IsAny<decimal>(), It.IsAny<string>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task MissingSalaryStopsCalculationInsteadOfGeneratingZeroSalary()
    {
        var (period, employee) = PrepareRegressionPayroll(); employee.SalaryStructures.Clear();
        await Assert.ThrowsAsync<InvalidOperationException>(() => _engine.CalculateEmployeePayrollLineAsync(period, employee, null, null, null, null, Array.Empty<PayrollAdjustment>()));
    }

    [Fact]
    public async Task CalculatePayrollForPeriodAsync_WhenMultipleActiveEmployees_CalculatesAllAndAggregatesTotals()
    {
        var period = new PayrollPeriod
        {
            Id = 10,
            CompanyId = 1,
            PeriodCode = "2026-10",
            StartDate = new DateTime(2026, 10, 1),
            EndDate = new DateTime(2026, 10, 31)
        };

        var emp1 = new Employee { EmployeeCode = "EMP_1", HireDate = new DateTime(2020, 1, 1), IsActive = true, EmploymentStatus = "ACTIVE" };
        emp1.SalaryStructures.Add(new SalaryStructure { BasicSalary = 1000m, CurrencyCode = "JOD", EffectiveFrom = new DateTime(2020, 1, 1), IsActive = true });

        var emp2 = new Employee { EmployeeCode = "EMP_2", HireDate = new DateTime(2021, 1, 1), IsActive = true, EmploymentStatus = "ACTIVE" };
        emp2.SalaryStructures.Add(new SalaryStructure { BasicSalary = 2000m, CurrencyCode = "JOD", EffectiveFrom = new DateTime(2021, 1, 1), IsActive = true });

        _prorationServiceMock.Setup(p => p.CalculateProrationFactorAsync(1, period.StartDate, period.EndDate, It.IsAny<DateTime>(), null, It.IsAny<ProrationPolicy?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ProrationResult(1.0m, 31, 31, "CALENDAR_DAYS", "CAL"));

        _sscServiceMock.Setup(s => s.CalculateSSC(It.IsAny<decimal>(), false, It.IsAny<SSCPolicy?>(), It.IsAny<int>()))
            .Returns<decimal, bool, SSCPolicy?, int>((g, _, _, _) => new SSCResult(g, g * 0.075m, g * 0.1425m, 0m, 0.075m, 0.1425m, 3617m, "SSC"));

        _taxEngineMock.Setup(t => t.CalculateIncomeTax(It.IsAny<decimal>(), It.IsAny<decimal>(), It.IsAny<int>(), It.IsAny<TaxPolicy?>(), It.IsAny<int>()))
            .Returns<decimal, decimal, int, TaxPolicy?, int>((g, _, _, _, _) => new TaxResult(g, 9000m, 0m, g * 12, 10m, 0m, "TAX"));

        _loanDeductionServiceMock.Setup(l => l.CalculateAndApplyDeductionsAsync(null, It.IsAny<string>(), period.PeriodCode, It.IsAny<decimal>(), It.IsAny<DeductionPolicy?>(), false, It.IsAny<CancellationToken>(), It.IsAny<string?>()))
            .ReturnsAsync(new LoanDeductionCalculationResult(0m, 0m, 0m, new(), new()));

        _adjustmentRepoMock.Setup(a => a.GetPayrollCalculationAdjustmentsAsync(1, "2026-10", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<PayrollAdjustment>());

        var run = await _engine.CalculatePayrollForPeriodAsync(period, new[] { emp1, emp2 });

        Assert.NotNull(run);
        Assert.Equal("CALCULATED", run.Status);
        Assert.Equal("2026-10", run.PayPeriod);
        Assert.Equal(2, run.Lines.Count);

        // emp1: 1000 gross, 75 ssc, 10 tax -> net 915
        // emp2: 2000 gross, 150 ssc, 10 tax -> net 1840
        Assert.Equal(3000m, run.TotalGrossSalary);
        Assert.Equal(225m, run.TotalEmployeeSsc);
        Assert.Equal(427.500m, run.TotalEmployerSsc);
        Assert.Equal(20m, run.TotalIncomeTax);
        Assert.Equal(2755m, run.TotalNetSalary);
    }

    [Fact]
    public async Task CalculatePayrollForPeriodAsync_WhenEmployeeTerminatedBeforePeriod_SkipsEmployee()
    {
        var period = new PayrollPeriod
        {
            Id = 11,
            CompanyId = 1,
            PeriodCode = "2026-10",
            StartDate = new DateTime(2026, 10, 1),
            EndDate = new DateTime(2026, 10, 31)
        };

        var terminatedEmp = new Employee
        {
            EmployeeCode = "EMP_TERM_PAST",
            HireDate = new DateTime(2020, 1, 1),
            TerminationDate = new DateTime(2026, 9, 30), // Terminated before October 1
            EmploymentStatus = "TERMINATED",
            IsActive = false
        };

        _adjustmentRepoMock.Setup(a => a.GetPayrollCalculationAdjustmentsAsync(1, "2026-10", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<PayrollAdjustment>());

        var run = await _engine.CalculatePayrollForPeriodAsync(period, new[] { terminatedEmp });

        Assert.Empty(run.Lines);
        Assert.Equal(0m, run.TotalGrossSalary);
    }

    [Fact]
    public async Task CalculatePayrollForPeriodAsync_WhenEmployeeHiredAfterPeriod_SkipsEmployee()
    {
        var period = new PayrollPeriod
        {
            Id = 12,
            CompanyId = 1,
            PeriodCode = "2026-10",
            StartDate = new DateTime(2026, 10, 1),
            EndDate = new DateTime(2026, 10, 31)
        };

        var futureEmp = new Employee
        {
            EmployeeCode = "EMP_FUTURE",
            HireDate = new DateTime(2026, 11, 1), // Hired in November
            EmploymentStatus = "ACTIVE",
            IsActive = true
        };

        _adjustmentRepoMock.Setup(a => a.GetPayrollCalculationAdjustmentsAsync(1, "2026-10", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<PayrollAdjustment>());

        var run = await _engine.CalculatePayrollForPeriodAsync(period, new[] { futureEmp });

        Assert.Empty(run.Lines);
    }

    [Fact]
    public async Task CalculatePayrollForPeriodAsync_WhenTerminatedDuringPeriod_IncludesEmployee()
    {
        var period = new PayrollPeriod
        {
            Id = 13,
            CompanyId = 1,
            PeriodCode = "2026-10",
            StartDate = new DateTime(2026, 10, 1),
            EndDate = new DateTime(2026, 10, 31)
        };

        var midTermEmp = new Employee
        {
            EmployeeCode = "EMP_MID_TERM",
            HireDate = new DateTime(2020, 1, 1),
            TerminationDate = new DateTime(2026, 10, 15), // Terminated mid October
            EmploymentStatus = "TERMINATED",
            IsActive = false
        };
        midTermEmp.SalaryStructures.Add(new SalaryStructure
        {
            BasicSalary = 1000m,
            CurrencyCode = "JOD",
            EffectiveFrom = new DateTime(2020, 1, 1),
            IsActive = true
        });

        _prorationServiceMock.Setup(p => p.CalculateProrationFactorAsync(1, period.StartDate, period.EndDate, midTermEmp.HireDate, midTermEmp.TerminationDate, It.IsAny<ProrationPolicy?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ProrationResult(0.5m, 31, 15, "CALENDAR_DAYS", "CAL"));

        _sscServiceMock.Setup(s => s.CalculateSSC(It.IsAny<decimal>(), false, It.IsAny<SSCPolicy?>(), It.IsAny<int>()))
            .Returns(new SSCResult(500m, 37.5m, 71.25m, 0m, 0.075m, 0.1425m, 3617m, "SSC"));

        _taxEngineMock.Setup(t => t.CalculateIncomeTax(It.IsAny<decimal>(), It.IsAny<decimal>(), It.IsAny<int>(), It.IsAny<TaxPolicy?>(), It.IsAny<int>()))
            .Returns(new TaxResult(500m, 9000m, 0m, 6000m, 0m, 0m, "TAX"));

        _loanDeductionServiceMock.Setup(l => l.CalculateAndApplyDeductionsAsync(null, It.IsAny<string>(), period.PeriodCode, It.IsAny<decimal>(), It.IsAny<DeductionPolicy?>(), false, It.IsAny<CancellationToken>(), It.IsAny<string?>()))
            .ReturnsAsync(new LoanDeductionCalculationResult(0m, 0m, 0m, new(), new()));

        _adjustmentRepoMock.Setup(a => a.GetPayrollCalculationAdjustmentsAsync(1, "2026-10", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<PayrollAdjustment>());

        var run = await _engine.CalculatePayrollForPeriodAsync(period, new[] { midTermEmp });

        Assert.Single(run.Lines);
        Assert.Equal(500m, run.TotalGrossSalary);
    }

    [Fact]
    public async Task CalculatePayrollForPeriodAsync_WhenMixedCurrencies_ThrowsInvalidOperationException()
    {
        var period = new PayrollPeriod
        {
            Id = 14,
            CompanyId = 1,
            PeriodCode = "2026-10",
            StartDate = new DateTime(2026, 10, 1),
            EndDate = new DateTime(2026, 10, 31)
        };

        var empJod = new Employee { EmployeeCode = "EMP_JOD", HireDate = new DateTime(2020, 1, 1), IsActive = true };
        empJod.SalaryStructures.Add(new SalaryStructure { BasicSalary = 1000m, CurrencyCode = "JOD", EffectiveFrom = new DateTime(2020, 1, 1), IsActive = true });

        var empUsd = new Employee { EmployeeCode = "EMP_USD", HireDate = new DateTime(2020, 1, 1), IsActive = true };
        empUsd.SalaryStructures.Add(new SalaryStructure { BasicSalary = 1500m, CurrencyCode = "USD", EffectiveFrom = new DateTime(2020, 1, 1), IsActive = true });

        _prorationServiceMock.Setup(p => p.CalculateProrationFactorAsync(1, period.StartDate, period.EndDate, It.IsAny<DateTime>(), null, It.IsAny<ProrationPolicy?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ProrationResult(1.0m, 31, 31, "CALENDAR_DAYS", "CAL"));

        _sscServiceMock.Setup(s => s.CalculateSSC(It.IsAny<decimal>(), false, It.IsAny<SSCPolicy?>(), It.IsAny<int>()))
            .Returns(new SSCResult(1000m, 0m, 0m, 0m, 0m, 0m, 3617m, "SSC"));

        _taxEngineMock.Setup(t => t.CalculateIncomeTax(It.IsAny<decimal>(), It.IsAny<decimal>(), It.IsAny<int>(), It.IsAny<TaxPolicy?>(), It.IsAny<int>()))
            .Returns(new TaxResult(1000m, 0m, 0m, 12000m, 0m, 0m, "TAX"));

        _loanDeductionServiceMock.Setup(l => l.CalculateAndApplyDeductionsAsync(null, It.IsAny<string>(), period.PeriodCode, It.IsAny<decimal>(), It.IsAny<DeductionPolicy?>(), false, It.IsAny<CancellationToken>(), It.IsAny<string?>()))
            .ReturnsAsync(new LoanDeductionCalculationResult(0m, 0m, 0m, new(), new()));

        _adjustmentRepoMock.Setup(a => a.GetPayrollCalculationAdjustmentsAsync(1, "2026-10", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<PayrollAdjustment>());

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _engine.CalculatePayrollForPeriodAsync(period, new[] { empJod, empUsd }));

        Assert.Contains("Payroll cannot combine different currencies", ex.Message);
    }

    [Fact]
    public async Task CalculateEmployeePayrollLine_WithPercentageAllowance_CalculatesFromNominalBasic()
    {
        var (period, employee) = PrepareRegressionPayroll();
        employee.SalaryStructures[0].Lines.Add(new SalaryStructureLine
        {
            ComponentCode = "PCT_BONUS",
            Percent = 15.0m, // 15% of 1000 = 150
            IsActive = true,
            Component = new SalaryComponent { ComponentCode = "PCT_BONUS", ComponentType = "ALLOWANCE", CalculationType = "PERCENTAGE" }
        });

        var line = await _engine.CalculateEmployeePayrollLineAsync(period, employee, null, null, null, null, Array.Empty<PayrollAdjustment>());

        Assert.Equal(1150m, line.GrossSalary);
        Assert.Equal(150m, line.TotalEarnings);
        Assert.Contains(line.Components, c => c.ComponentCode == "PCT_BONUS" && c.Amount == 150m);
    }

    [Fact]
    public async Task CalculateEmployeePayrollLine_WithStructureDeduction_ProratesAndAddsToDeductions()
    {
        var (period, employee) = PrepareRegressionPayroll();
        employee.SalaryStructures[0].Lines.Add(new SalaryStructureLine
        {
            ComponentCode = "UNION_FEE",
            Amount = 40m,
            IsActive = true,
            Component = new SalaryComponent { ComponentCode = "UNION_FEE", ComponentType = "DEDUCTION" }
        });

        var line = await _engine.CalculateEmployeePayrollLineAsync(period, employee, null, null, null, null, Array.Empty<PayrollAdjustment>());

        Assert.Equal(40m, line.OtherDeductions);
        Assert.Equal(960m, line.NetPay); // 1000 - 40
        Assert.Contains(line.Components, c => c.ComponentCode == "UNION_FEE" && c.Amount == 40m && c.ComponentType == "DEDUCTION");
    }

    [Fact]
    public async Task CalculateEmployeePayrollLine_WithVariableDeductionAdjustment_AddsToDeductions()
    {
        var (period, employee) = PrepareRegressionPayroll();
        var adjustments = new[]
        {
            new PayrollAdjustment
            {
                ComponentCode = "PENALTY",
                Amount = 60m,
                AdjustmentType = "DEDUCTION",
                Component = new SalaryComponent { ComponentCode = "PENALTY", ComponentType = "DEDUCTION" }
            }
        };

        var line = await _engine.CalculateEmployeePayrollLineAsync(period, employee, null, null, null, null, adjustments);

        Assert.Equal(60m, line.OtherDeductions);
        Assert.Equal(940m, line.NetPay); // 1000 - 60
        Assert.Contains(line.Components, c => c.ComponentCode == "PENALTY" && c.Amount == 60m && c.ComponentType == "DEDUCTION");
    }

    [Fact]
    public async Task CalculateEmployeePayrollLine_Fixed30_MissingAllocationPolicy_ThrowsInvalidOperationException()
    {
        var (period, employee) = PrepareRegressionPayroll();

        // Two consecutive salary segments in the period
        employee.SalaryStructures[0].EffectiveFrom = new DateTime(2020, 1, 1);
        employee.SalaryStructures[0].EffectiveTo = new DateTime(2026, 10, 15);
        employee.SalaryStructures.Add(new SalaryStructure
        {
            BasicSalary = 1200m,
            EffectiveFrom = new DateTime(2026, 10, 16),
            IsActive = true
        });

        // Proration returns FIXED_30
        _prorationServiceMock.Setup(p => p.CalculateProrationFactorAsync(1, period.StartDate, period.EndDate, employee.HireDate, null, It.IsAny<ProrationPolicy?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ProrationResult(1.0m, 30, 30, "FIXED_30", "F30_POLICY"));

        // Proration policy has null SalarySegmentAllocation
        var policy = new ProrationPolicy
        {
            Code = "F30_POLICY",
            Method = "FIXED_30",
            SalarySegmentAllocation = null! // Missing allocation policy!
        };

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _engine.CalculateEmployeePayrollLineAsync(period, employee, policy, null, null, null, Array.Empty<PayrollAdjustment>()));

        Assert.Contains("Select CALENDAR_WEIGHTED or WORKING_WEIGHTED", ex.Message);
    }

    [Fact]
    public async Task CalculateEmployeePayrollLine_WhenSolidarityContribApplies_AddsTaxSolidarityComponent()
    {
        var (period, employee) = PrepareRegressionPayroll();

        _taxEngineMock.Setup(t => t.CalculateIncomeTax(It.IsAny<decimal>(), It.IsAny<decimal>(), It.IsAny<int>(), It.IsAny<TaxPolicy?>(), It.IsAny<int>()))
            .Returns(new TaxResult(1000m, 9000m, 0m, 12000m, 50m, 25m, "TAX_SOLID"));

        var line = await _engine.CalculateEmployeePayrollLineAsync(period, employee, null, null, null, null, Array.Empty<PayrollAdjustment>());

        Assert.Equal(25m, line.NationalContribWithheld);
        Assert.Contains(line.Components, c => c.ComponentCode == "TAX_SOLIDARITY" && c.Amount == 25m);
    }

    [Fact]
    public async Task CalculateEmployeePayrollLine_InheritsDepartmentCodeAsCostCenter_WhenPresent()
    {
        var (period, employee) = PrepareRegressionPayroll();
        employee.DepartmentCode = "DEP_FINANCE";

        var line = await _engine.CalculateEmployeePayrollLineAsync(period, employee, null, null, null, null, Array.Empty<PayrollAdjustment>());

        Assert.Equal("DEP_FINANCE", line.DepartmentCode);
        Assert.Equal("DEP_FINANCE", line.CostCenterCode);
    }

    [Fact]
    public async Task CalculateEmployeePayrollLine_GeneratesDetailedSnapshotJson()
    {
        var (period, employee) = PrepareRegressionPayroll();

        var line = await _engine.CalculateEmployeePayrollLineAsync(period, employee, null, null, null, null, Array.Empty<PayrollAdjustment>());

        Assert.NotNull(line.Snapshot);
        Assert.Contains("employeeCode", line.Snapshot.ExplanationJson);
        Assert.Contains("REGRESSION", line.Snapshot.ExplanationJson);
        Assert.Contains("grossSalary", line.Snapshot.ExplanationJson);
    }

    [Fact]
    public async Task CalculateEmployeePayrollLine_WhenSocialSecurityContribCalculated_PopulatesEmployeeAndEmployerLines()
    {
        var (period, employee) = PrepareRegressionPayroll();

        _sscServiceMock.Setup(s => s.CalculateSSC(1000m, false, null, It.IsAny<int>()))
            .Returns(new SSCResult(1000m, 75m, 142.5m, 217.5m, 0m, 0m, 0m, "SSC_POLICY"));

        var line = await _engine.CalculateEmployeePayrollLineAsync(period, employee, null, null, null, null, Array.Empty<PayrollAdjustment>());

        Assert.Equal(75m, line.SscEmployeeContrib);
        Assert.Equal(142.5m, line.SscEmployerContrib);
        Assert.Contains(line.Components, c => c.ComponentCode == "SSC_EMP" && c.Amount == 75m && c.ComponentType == "DEDUCTION");
        Assert.Contains(line.Components, c => c.ComponentCode == "SSC_EMPR" && c.Amount == 142.5m && c.ComponentType == "EMPLOYER_CONTRIB");
    }

    [Fact]
    public async Task CalculatePayrollForPeriodAsync_WhenTerminatedBeforePeriodStarts_SkipsEmployee()
    {
        var period = new PayrollPeriod
        {
            Id = 1,
            CompanyId = 1,
            PeriodCode = "2026-10",
            StartDate = new DateTime(2026, 10, 1),
            EndDate = new DateTime(2026, 10, 31)
        };

        var empTerminated = new Employee
        {
            EmployeeCode = "EMP_TERM",
            HireDate = new DateTime(2020, 1, 1),
            TerminationDate = new DateTime(2026, 9, 30), // Terminated before Oct 1
            IsActive = false,
            EmploymentStatus = "TERMINATED"
        };

        _adjustmentRepoMock.Setup(a => a.GetPayrollCalculationAdjustmentsAsync(1, "2026-10", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<PayrollAdjustment>());

        var run = await _engine.CalculatePayrollForPeriodAsync(period, new[] { empTerminated });

        Assert.Empty(run.Lines);
        Assert.Equal(0m, run.TotalGrossSalary);
    }

    [Fact]
    public async Task CalculatePayrollForPeriodAsync_WhenHiredAfterPeriodEnds_SkipsEmployee()
    {
        var period = new PayrollPeriod
        {
            Id = 1,
            CompanyId = 1,
            PeriodCode = "2026-10",
            StartDate = new DateTime(2026, 10, 1),
            EndDate = new DateTime(2026, 10, 31)
        };

        var empFuture = new Employee
        {
            EmployeeCode = "EMP_FUT",
            HireDate = new DateTime(2026, 11, 1), // Joined in November
            IsActive = true,
            EmploymentStatus = "ACTIVE"
        };

        _adjustmentRepoMock.Setup(a => a.GetPayrollCalculationAdjustmentsAsync(1, "2026-10", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<PayrollAdjustment>());

        var run = await _engine.CalculatePayrollForPeriodAsync(period, new[] { empFuture });

        Assert.Empty(run.Lines);
        Assert.Equal(0m, run.TotalGrossSalary);
    }

    [Fact]
    public async Task CalculateEmployeePayrollLine_WithUnpaidLeave_DeductsFromBasicAndAddsComponent()
    {
        var (period, employee) = PrepareRegressionPayroll();

        // 2 days unpaid leave
        _leaveServiceMock.Setup(l => l.GetUnpaidLeaveDaysInPeriodAsync(employee.EmployeeCode, It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(2m);

        var line = await _engine.CalculateEmployeePayrollLineAsync(period, employee, null, null, null, null, Array.Empty<PayrollAdjustment>());

        // 1000 basic / 31 base days * 2 = 64.516
        // proratedBasic = 1000 - 64.516 = 935.484
        Assert.Equal(64.516m, line.Components.First(c => c.ComponentCode == "UNPAID_LEAVE").Amount);
        Assert.Equal(935.484m, line.BasicSalary);
        Assert.Equal(935.484m, line.GrossSalary);
    }

    [Fact]
    public async Task CalculateEmployeePayrollLine_WithLoanDeductions_AddsLoanComponentAndReducesNetPay()
    {
        var (period, employee) = PrepareRegressionPayroll();

        _loanDeductionServiceMock.Setup(l => l.CalculateAndApplyDeductionsAsync(null, employee.EmployeeCode, period.PeriodCode, It.IsAny<decimal>(), null, false, It.IsAny<CancellationToken>(), It.IsAny<string?>()))
            .ReturnsAsync(new LoanDeductionCalculationResult(150m, 150m, 0m, new(), new()));

        var line = await _engine.CalculateEmployeePayrollLineAsync(period, employee, null, null, null, null, Array.Empty<PayrollAdjustment>());

        Assert.Equal(150m, line.OtherDeductions);
        Assert.Equal(850m, line.NetPay);
        Assert.Contains(line.Components, c => c.ComponentCode == "LOAN_DEDUCTION" && c.Amount == 150m);
    }

    [Fact]
    public async Task CalculateEmployeePayrollLine_WithActiveTaxExemptionClaimedOnDependents_SumsWithEmployeeTaxExemptions()
    {
        var (period, employee) = PrepareRegressionPayroll();
        employee.TaxExemptionCount = 1;
        employee.Dependents.Add(new EmployeeDependent { IsActive = true, IsTaxExemptionClaimed = true });
        employee.Dependents.Add(new EmployeeDependent { IsActive = true, IsTaxExemptionClaimed = true });
        employee.Dependents.Add(new EmployeeDependent { IsActive = false, IsTaxExemptionClaimed = true }); // Inactive - ignored
        employee.Dependents.Add(new EmployeeDependent { IsActive = true, IsTaxExemptionClaimed = false }); // Unclaimed - ignored

        int capturedDependentCount = -1;
        _taxEngineMock.Setup(t => t.CalculateIncomeTax(It.IsAny<decimal>(), It.IsAny<decimal>(), It.IsAny<int>(), null, It.IsAny<int>()))
            .Callback<decimal, decimal, int, TaxPolicy?, int>((_, _, depCount, _, _) => capturedDependentCount = depCount)
            .Returns(new TaxResult(0m, 0m, 0m, 0m, 0m, 0m, "TEST"));

        await _engine.CalculateEmployeePayrollLineAsync(period, employee, null, null, null, null, Array.Empty<PayrollAdjustment>());

        // 1 employee count + 2 active claimed dependents = 3
        Assert.Equal(3, capturedDependentCount);
    }

    [Fact]
    public async Task CalculateEmployeePayrollLine_PropagatesCancellationToken()
    {
        using var cts = new CancellationTokenSource();
        var ct = cts.Token;
        var (period, employee) = PrepareRegressionPayroll();

        _prorationServiceMock.Setup(p => p.CalculateProrationFactorAsync(1, period.StartDate, period.EndDate, employee.HireDate, null, null, ct))
            .ReturnsAsync(new ProrationResult(1m, 31, 31, "CALENDAR_DAYS", "TEST"));
        _leaveServiceMock.Setup(l => l.GetUnpaidLeaveDaysInPeriodAsync(employee.EmployeeCode, It.IsAny<DateTime>(), It.IsAny<DateTime>(), ct))
            .ReturnsAsync(0m);
        _loanDeductionServiceMock.Setup(l => l.CalculateAndApplyDeductionsAsync(null, employee.EmployeeCode, period.PeriodCode, It.IsAny<decimal>(), null, false, ct, It.IsAny<string?>()))
            .ReturnsAsync(new LoanDeductionCalculationResult(0m, 0m, 0m, new(), new()));

        var line = await _engine.CalculateEmployeePayrollLineAsync(period, employee, null, null, null, null, Array.Empty<PayrollAdjustment>(), ct);

        Assert.NotNull(line);
        _prorationServiceMock.Verify(p => p.CalculateProrationFactorAsync(1, period.StartDate, period.EndDate, employee.HireDate, null, null, ct), Times.Once);
        _leaveServiceMock.Verify(l => l.GetUnpaidLeaveDaysInPeriodAsync(employee.EmployeeCode, It.IsAny<DateTime>(), It.IsAny<DateTime>(), ct), Times.Once);
        _loanDeductionServiceMock.Verify(l => l.CalculateAndApplyDeductionsAsync(null, employee.EmployeeCode, period.PeriodCode, It.IsAny<decimal>(), null, false, ct, It.IsAny<string?>()), Times.Once);
    }

    [Fact]
    public async Task CalculatePayrollForPeriodAsync_PropagatesCancellationToken()
    {
        using var cts = new CancellationTokenSource();
        var ct = cts.Token;
        var period = new PayrollPeriod { CompanyId = 1, PeriodCode = "2026-10", StartDate = new(2026, 10, 1), EndDate = new(2026, 10, 31) };

        _policyRepoMock.Setup(p => p.GetActiveProrationPolicyAsync(1, period.StartDate, ct)).ReturnsAsync((ProrationPolicy?)null);
        _policyRepoMock.Setup(p => p.GetActiveSSCPolicyAsync(1, period.StartDate, ct)).ReturnsAsync((SSCPolicy?)null);
        _policyRepoMock.Setup(p => p.GetActiveTaxPolicyAsync(1, period.StartDate, ct)).ReturnsAsync((TaxPolicy?)null);
        _policyRepoMock.Setup(p => p.GetActiveDeductionPolicyAsync(1, period.StartDate, ct)).ReturnsAsync((DeductionPolicy?)null);
        _adjustmentRepoMock.Setup(a => a.GetPayrollCalculationAdjustmentsAsync(1, period.PeriodCode, ct))
            .ReturnsAsync(new List<PayrollAdjustment>());

        var run = await _engine.CalculatePayrollForPeriodAsync(period, Array.Empty<Employee>(), null, "SYSTEM", ct);

        Assert.NotNull(run);
        _policyRepoMock.Verify(p => p.GetActiveProrationPolicyAsync(1, period.StartDate, ct), Times.Once);
        _policyRepoMock.Verify(p => p.GetActiveSSCPolicyAsync(1, period.StartDate, ct), Times.Once);
        _policyRepoMock.Verify(p => p.GetActiveTaxPolicyAsync(1, period.StartDate, ct), Times.Once);
        _policyRepoMock.Verify(p => p.GetActiveDeductionPolicyAsync(1, period.StartDate, ct), Times.Once);
        _adjustmentRepoMock.Verify(a => a.GetPayrollCalculationAdjustmentsAsync(1, period.PeriodCode, ct), Times.Once);
    }

    [Fact]
    public async Task CalculatePayrollForPeriodAsync_WhenEmptyEmployeeList_ReturnsEmptyRunZeroTotals()
    {
        var period = new PayrollPeriod { CompanyId = 1, PeriodCode = "2026-10", StartDate = new(2026, 10, 1), EndDate = new(2026, 10, 31) };
        _adjustmentRepoMock.Setup(a => a.GetPayrollCalculationAdjustmentsAsync(1, period.PeriodCode, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<PayrollAdjustment>());

        var run = await _engine.CalculatePayrollForPeriodAsync(period, Array.Empty<Employee>(), branchId: 10, calculatedBy: "PAYROLL_ADMIN");

        Assert.NotNull(run);
        Assert.Equal("CALCULATED", run.Status);
        Assert.Equal("PAYROLL_ADMIN", run.CalculatedBy);
        Assert.Equal("PAYROLL_ADMIN", run.CreationUser);
        Assert.Equal(10L, run.BranchId);
        Assert.Empty(run.Lines);
        Assert.Equal(0m, run.TotalGrossSalary);
        Assert.Equal(0m, run.TotalNetSalary);
        Assert.Equal(0m, run.TotalEmployeeSsc);
        Assert.Equal(0m, run.TotalEmployerSsc);
        Assert.Equal(0m, run.TotalIncomeTax);
        Assert.Equal(0m, run.TotalNationalContrib);
        Assert.Equal(0m, run.TotalOtherDeductions);
    }

    [Fact]
    public async Task CalculateEmployeePayrollLine_WhenDepartmentCodeIsNullOrWhitespace_FallsBackToStructureLineGlAccount()
    {
        var (period, employee) = PrepareRegressionPayroll();
        employee.DepartmentCode = null;
        employee.SalaryStructures[0].Lines.Add(new SalaryStructureLine
        {
            ComponentCode = "BASE_ALLOW",
            Amount = 50m,
            IsActive = true,
            Component = new SalaryComponent { ComponentCode = "BASE_ALLOW", ComponentType = "ALLOWANCE", GlAccountCode = "GL_CC_500" }
        });

        var line = await _engine.CalculateEmployeePayrollLineAsync(period, employee, null, null, null, null, Array.Empty<PayrollAdjustment>());

        Assert.Null(line.DepartmentCode);
        Assert.Equal("GL_CC_500", line.CostCenterCode);
    }

    [Fact]
    public async Task CalculateEmployeePayrollLine_WhenDepartmentCodeAndGlAccountNull_CostCenterCodeIsNull()
    {
        var (period, employee) = PrepareRegressionPayroll();
        employee.DepartmentCode = "   "; // Whitespace
        employee.SalaryStructures[0].Lines.Clear();

        var line = await _engine.CalculateEmployeePayrollLineAsync(period, employee, null, null, null, null, Array.Empty<PayrollAdjustment>());

        Assert.Equal("   ", line.DepartmentCode);
        Assert.Null(line.CostCenterCode);
    }

    [Theory]
    [InlineData("أحمد المحلي", "Ahmed En", "أحمد المحلي")]
    [InlineData("", "Ahmed En", "Ahmed En")]
    [InlineData("   ", "   ", "REGRESSION")]
    [InlineData(null, null, "REGRESSION")]
    public async Task CalculateEmployeePayrollLine_BeneficiaryNameFallback(string? nameLocal, string? nameEn, string expectedBeneficiary)
    {
        var (period, employee) = PrepareRegressionPayroll();
        employee.NameLocal = nameLocal!;
        employee.NameEn = nameEn!;

        var line = await _engine.CalculateEmployeePayrollLineAsync(period, employee, null, null, null, null, Array.Empty<PayrollAdjustment>());

        Assert.Equal(expectedBeneficiary, line.BeneficiaryName);
    }

    [Fact]
    public async Task CalculateEmployeePayrollLine_Fixed30_WhenTotalWeightIsZeroOrNegative_ThrowsInvalidOperationException()
    {
        var (period, employee) = PrepareRegressionPayroll();
        employee.SalaryStructures[0].EffectiveFrom = new DateTime(2020, 1, 1);
        employee.SalaryStructures[0].EffectiveTo = new DateTime(2026, 10, 15);
        employee.SalaryStructures.Add(new SalaryStructure
        {
            BasicSalary = 1200m,
            EffectiveFrom = new DateTime(2026, 10, 16),
            IsActive = true
        });

        _prorationServiceMock.Setup(p => p.CalculateProrationFactorAsync(1, period.StartDate, period.EndDate, It.IsAny<DateTime>(), It.IsAny<DateTime?>(), It.IsAny<ProrationPolicy?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((long cId, DateTime pStart, DateTime pEnd, DateTime hDate, DateTime? tDate, ProrationPolicy? pol, CancellationToken ct) =>
            {
                if (pol?.Method == "CALENDAR_DAYS")
                {
                    return new ProrationResult(0m, 30, 0, "CALENDAR_DAYS", "ZERO_WEIGHT");
                }
                return new ProrationResult(1.0m, 30, 30, "FIXED_30", "F30_POLICY");
            });

        var policy = new ProrationPolicy
        {
            Code = "F30_POLICY",
            Method = "FIXED_30",
            SalarySegmentAllocation = "CALENDAR_WEIGHTED"
        };

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _engine.CalculateEmployeePayrollLineAsync(period, employee, policy, null, null, null, Array.Empty<PayrollAdjustment>()));

        Assert.Contains("The selected salary allocation has no eligible days", ex.Message);
    }

    [Fact]
    public async Task CalculateEmployeePayrollLine_WithMultipleSalarySegments_ApportionsAndRoundsCorrectly()
    {
        var (period, employee) = PrepareRegressionPayroll();
        // Segment 1: 1000m basic (Oct 1 to Oct 10) -> 10/31 of month
        employee.SalaryStructures[0].EffectiveFrom = new DateTime(2020, 1, 1);
        employee.SalaryStructures[0].EffectiveTo = new DateTime(2026, 10, 10);

        // Segment 2: 2000m basic (Oct 11 to Oct 31) -> 21/31 of month
        employee.SalaryStructures.Add(new SalaryStructure
        {
            Id = 2,
            BasicSalary = 2000m,
            EffectiveFrom = new DateTime(2026, 10, 11),
            IsActive = true,
            CurrencyCode = "JOD",
            PaymentMethod = "BANK_TRANSFER"
        });

        _prorationServiceMock.Setup(p => p.CalculateProrationFactorAsync(1, period.StartDate, period.EndDate, employee.HireDate, null, It.IsAny<ProrationPolicy?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ProrationResult(1.0m, 31, 31, "CALENDAR_DAYS", "CAL"));

        _prorationServiceMock.Setup(p => p.CalculateProrationFactorAsync(1, period.StartDate, period.EndDate, new DateTime(2026, 10, 1), new DateTime(2026, 10, 10), It.IsAny<ProrationPolicy?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ProrationResult(10m / 31m, 31, 10, "CALENDAR_DAYS", "CAL"));

        _prorationServiceMock.Setup(p => p.CalculateProrationFactorAsync(1, period.StartDate, period.EndDate, new DateTime(2026, 10, 11), new DateTime(2026, 10, 31), It.IsAny<ProrationPolicy?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ProrationResult(21m / 31m, 31, 21, "CALENDAR_DAYS", "CAL"));

        var line = await _engine.CalculateEmployeePayrollLineAsync(period, employee, null, null, null, null, Array.Empty<PayrollAdjustment>());

        decimal expectedBasic = Math.Round((1000m * (10m / 31m)) + (2000m * (21m / 31m)), 3);
        Assert.Equal(expectedBasic, line.BasicSalary);
        Assert.NotNull(line.Snapshot);
        Assert.Contains("salarySegments", line.Snapshot.ExplanationJson);
    }

    [Fact]
    public async Task CalculateEmployeePayrollLine_DetectsMixedPaymentMethodsInSnapshot()
    {
        var (period, employee) = PrepareRegressionPayroll();
        employee.SalaryStructures[0].EffectiveFrom = new DateTime(2020, 1, 1);
        employee.SalaryStructures[0].EffectiveTo = new DateTime(2026, 10, 15);
        employee.SalaryStructures[0].PaymentMethod = "BANK_TRANSFER";

        employee.SalaryStructures.Add(new SalaryStructure
        {
            Id = 3,
            BasicSalary = 1000m,
            EffectiveFrom = new DateTime(2026, 10, 16),
            IsActive = true,
            CurrencyCode = "JOD",
            PaymentMethod = "CASH" // Mixed payment methods!
        });

        _prorationServiceMock.Setup(p => p.CalculateProrationFactorAsync(1, period.StartDate, period.EndDate, employee.HireDate, null, It.IsAny<ProrationPolicy?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ProrationResult(1.0m, 31, 31, "CALENDAR_DAYS", "CAL"));

        _prorationServiceMock.Setup(p => p.CalculateProrationFactorAsync(1, period.StartDate, period.EndDate, It.IsAny<DateTime>(), It.IsAny<DateTime?>(), It.IsAny<ProrationPolicy?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ProrationResult(0.5m, 31, 15, "CALENDAR_DAYS", "CAL"));

        var line = await _engine.CalculateEmployeePayrollLineAsync(period, employee, null, null, null, null, Array.Empty<PayrollAdjustment>());

        Assert.NotNull(line.Snapshot);
        Assert.Contains("\"hasMixedPaymentMethods\": true", line.Snapshot.ExplanationJson);
    }

    [Fact]
    public async Task CalculateEmployeePayrollLine_UnpaidLeaveExceedingProratedBasic_FloorsBasicSalaryAtZero()
    {
        var (period, employee) = PrepareRegressionPayroll();

        // 40 unpaid leave days in 31-day month -> 1000 / 31 * 40 = 1290.323m deduction
        _leaveServiceMock.Setup(l => l.GetUnpaidLeaveDaysInPeriodAsync(employee.EmployeeCode, It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(40m);

        var line = await _engine.CalculateEmployeePayrollLineAsync(period, employee, null, null, null, null, Array.Empty<PayrollAdjustment>());

        // proratedBasic = Math.Max(0m, proratedBasic - unpaidLeaveDeduction)
        Assert.Equal(0m, line.BasicSalary);
        Assert.Equal(0m, line.GrossSalary);
        Assert.Contains(line.Components, c => c.ComponentCode == "UNPAID_LEAVE" && c.Amount > 1000m);
    }

    [Fact]
    public async Task CalculateEmployeePayrollLine_WhenAdjustmentHasNullComponent_DefaultsNameToCodeAndType()
    {
        var (period, employee) = PrepareRegressionPayroll();
        var adjustments = new[]
        {
            new PayrollAdjustment
            {
                ComponentCode = "SPECIAL_ALLOW",
                Amount = 75m,
                AdjustmentType = "EARNING",
                Component = null // Null component entity
            },
            new PayrollAdjustment
            {
                ComponentCode = "MISC_DEDUCT",
                Amount = 25m,
                AdjustmentType = "DEDUCTION",
                Component = null // Null component entity
            }
        };

        var line = await _engine.CalculateEmployeePayrollLineAsync(period, employee, null, null, null, null, adjustments);

        var earnComp = line.Components.First(c => c.ComponentCode == "SPECIAL_ALLOW");
        Assert.Equal("SPECIAL_ALLOW", earnComp.ComponentNameLocal);
        Assert.Equal("SPECIAL_ALLOW", earnComp.ComponentNameEn);
        Assert.Equal("EARNING", earnComp.ComponentType);
        Assert.Equal(75m, earnComp.Amount);

        var dedComp = line.Components.First(c => c.ComponentCode == "MISC_DEDUCT");
        Assert.Equal("MISC_DEDUCT", dedComp.ComponentNameLocal);
        Assert.Equal("MISC_DEDUCT", dedComp.ComponentNameEn);
        Assert.Equal("DEDUCTION", dedComp.ComponentType);
        Assert.Equal(25m, dedComp.Amount);
    }

    [Fact]
    public async Task CalculateEmployeePayrollLine_WhenStructureLineHasNullComponent_DefaultsNameToCodeAndAllowance()
    {
        var (period, employee) = PrepareRegressionPayroll();
        employee.SalaryStructures[0].Lines.Add(new SalaryStructureLine
        {
            ComponentCode = "STRUCT_NULL_COMP",
            Amount = 120m,
            IsActive = true,
            Component = null // Null Component
        });

        var line = await _engine.CalculateEmployeePayrollLineAsync(period, employee, null, null, null, null, Array.Empty<PayrollAdjustment>());

        var comp = line.Components.First(c => c.ComponentCode == "STRUCT_NULL_COMP");
        Assert.Equal("STRUCT_NULL_COMP", comp.ComponentNameLocal);
        Assert.Equal("STRUCT_NULL_COMP", comp.ComponentNameEn);
        Assert.Equal("EARNING", comp.ComponentType);
        Assert.Equal(120m, comp.Amount);
        Assert.Equal(1120m, line.GrossSalary);
    }

    [Fact]
    public async Task CalculateEmployeePayrollLine_WhenPreLoanDeductionsExceedGross_PassesZeroOrNegativeToLoanDeductionService()
    {
        var (period, employee) = PrepareRegressionPayroll();
        // Gross is 1000m. Add fixed deductions of 1200m so preLoanDeductions = 1200m and netPayBeforeLoans = -200m
        employee.SalaryStructures[0].Lines.Add(new SalaryStructureLine
        {
            ComponentCode = "HEAVY_DEDUCT",
            Amount = 1200m,
            IsActive = true,
            Component = new SalaryComponent { ComponentCode = "HEAVY_DEDUCT", ComponentType = "DEDUCTION" }
        });

        decimal capturedNetPayBeforeLoans = 0m;
        _loanDeductionServiceMock.Setup(l => l.CalculateAndApplyDeductionsAsync(null, employee.EmployeeCode, period.PeriodCode, It.IsAny<decimal>(), null, false, It.IsAny<CancellationToken>(), It.IsAny<string?>()))
            .Callback<long?, string, string, decimal, DeductionPolicy?, bool, CancellationToken, string?>((_, _, _, netBefore, _, _, _, _) => capturedNetPayBeforeLoans = netBefore)
            .ReturnsAsync(new LoanDeductionCalculationResult(0m, 0m, 0m, new(), new()));

        var line = await _engine.CalculateEmployeePayrollLineAsync(period, employee, null, null, null, null, Array.Empty<PayrollAdjustment>());

        Assert.Equal(-200m, capturedNetPayBeforeLoans);
        Assert.Equal(-200m, line.NetPay);
    }

    [Theory]
    [InlineData("JOD", 3)]
    [InlineData("JPY", 0)]
    [InlineData("USD", 2)]
    [InlineData("KWD", 3)]
    [InlineData("EUR", 2)]
    public async Task CalculateEmployeePayrollLine_SetsCurrencySpecificRounding_ForDifferentCurrencies(string currencyCode, int expectedDecimals)
    {
        var (period, employee) = PrepareRegressionPayroll();
        employee.SalaryStructures[0].CurrencyCode = currencyCode;

        int capturedSscDecimals = -1;
        int capturedTaxDecimals = -1;

        _sscServiceMock.Setup(s => s.CalculateSSC(It.IsAny<decimal>(), false, null, It.IsAny<int>()))
            .Callback<decimal, bool, SSCPolicy?, int>((_, _, _, dec) => capturedSscDecimals = dec)
            .Returns(new SSCResult(1000m, 0m, 0m, 0m, 0m, 0m, 0m, "SSC"));

        _taxEngineMock.Setup(t => t.CalculateIncomeTax(It.IsAny<decimal>(), It.IsAny<decimal>(), It.IsAny<int>(), null, It.IsAny<int>()))
            .Callback<decimal, decimal, int, TaxPolicy?, int>((_, _, _, _, dec) => capturedTaxDecimals = dec)
            .Returns(new TaxResult(1000m, 0m, 0m, 0m, 0m, 0m, "TAX"));

        var line = await _engine.CalculateEmployeePayrollLineAsync(period, employee, null, null, null, null, Array.Empty<PayrollAdjustment>());

        Assert.Equal(expectedDecimals, capturedSscDecimals);
        Assert.Equal(expectedDecimals, capturedTaxDecimals);
        Assert.Equal(currencyCode, line.CurrencyCode);
    }
}
