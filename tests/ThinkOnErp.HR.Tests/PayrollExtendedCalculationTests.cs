using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Moq;
using ThinkOnErp.Application.Services.Hr;
using ThinkOnErp.Domain.Entities.Hr;
using ThinkOnErp.Domain.Interfaces.Hr;
using Xunit;

namespace ThinkOnErp.HR.Tests;

public class PayrollExtendedCalculationTests
{
    private readonly Mock<IPolicyRepository> _policyRepoMock = new();
    private readonly Mock<IPayrollProrationService> _prorationServiceMock = new();
    private readonly Mock<IOvertimeCalculationService> _overtimeServiceMock = new();
    private readonly Mock<ISSCCalculationService> _sscServiceMock = new();
    private readonly Mock<ITaxCalculationEngine> _taxEngineMock = new();
    private readonly Mock<ILoanDeductionService> _loanDeductionServiceMock = new();
    private readonly Mock<IPayrollAdjustmentRepository> _adjustmentRepoMock = new();
    private readonly Mock<ILeaveService> _leaveServiceMock = new();
    private readonly PayrollCalculationEngine _engine;

    public PayrollExtendedCalculationTests()
    {
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
    public async Task MidPeriodHire_AppliesProration_ToBasicSalaryAndFixedAllowances()
    {
        var period = new PayrollPeriod
        {
            Id = 1,
            CompanyId = 1,
            PeriodCode = "2026-09",
            StartDate = new DateTime(2026, 9, 1),
            EndDate = new DateTime(2026, 9, 30)
        };

        var employee = new Employee
        {
            EmployeeCode = "HIRE_MID",
            NameLocal = "موظف تعيين جديد",
            HireDate = new DateTime(2026, 9, 16), // Hired on 16th -> 15 active days out of 30
            IsActive = true,
            EmploymentStatus = "ACTIVE"
        };

        var structure = new SalaryStructure
        {
            EmployeeCode = employee.EmployeeCode,
            EffectiveFrom = new DateTime(2026, 9, 16),
            BasicSalary = 1000m,
            IsActive = true,
            Lines = new List<SalaryStructureLine>
            {
                new()
                {
                    ComponentCode = "TRANS",
                    Amount = 200m,
                    IsActive = true,
                    Component = new SalaryComponent { ComponentCode = "TRANS", ComponentType = "ALLOWANCE" }
                }
            }
        };
        employee.SalaryStructures.Add(structure);

        // Proration mock: 15 active days / 30 calendar days = 0.5 factor
        _prorationServiceMock
            .Setup(p => p.CalculateProrationFactorAsync(1, period.StartDate, period.EndDate, employee.HireDate, null, It.IsAny<ProrationPolicy?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ProrationResult(0.5m, 15, 30, "CALENDAR_DAYS", "CAL_DEF"));

        _sscServiceMock
            .Setup(s => s.CalculateSSC(600m, false, It.IsAny<SSCPolicy?>())) // 500 basic + 100 transport = 600 gross
            .Returns(new SSCResult(600m, 45m, 85.5m, 0m, 0.075m, 0.1425m, 3617m, "DEF_SSC"));

        _taxEngineMock
            .Setup(t => t.CalculateIncomeTax(600m, 45m, 0, It.IsAny<TaxPolicy?>()))
            .Returns(new TaxResult(555m, 9000m, 0m, 7200m, 0m, 0m, "DEF_TAX"));

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
            Array.Empty<PayrollAdjustment>()
        );

        // Basic: 1000 * 0.5 = 500
        Assert.Equal(500m, line.BasicSalary);
        // Transport: 200 * 0.5 = 100
        Assert.Equal(100m, line.TotalEarnings);
        Assert.Equal(600m, line.GrossSalary);
        Assert.Equal(45m, line.SscEmployeeContrib);
        Assert.Equal(555m, line.NetPay); // 600 - 45 = 555
        Assert.NotNull(line.Snapshot);
        Assert.Equal(0.5m, line.Snapshot.ProrationFactor);
    }

    [Fact]
    public async Task MidPeriodTermination_AppliesProration_OnlyToActiveServiceDays()
    {
        var period = new PayrollPeriod
        {
            Id = 1,
            CompanyId = 1,
            PeriodCode = "2026-09",
            StartDate = new DateTime(2026, 9, 1),
            EndDate = new DateTime(2026, 9, 30)
        };

        var employee = new Employee
        {
            EmployeeCode = "TERM_MID",
            NameLocal = "موظف مغادر",
            HireDate = new DateTime(2020, 1, 1),
            TerminationDate = new DateTime(2026, 9, 10), // Terminated on 10th -> 10 active days
            IsActive = true,
            EmploymentStatus = "ACTIVE"
        };

        var structure = new SalaryStructure
        {
            EmployeeCode = employee.EmployeeCode,
            EffectiveFrom = new DateTime(2020, 1, 1),
            BasicSalary = 900m,
            IsActive = true
        };
        employee.SalaryStructures.Add(structure);

        // Proration mock: 10 active days / 30 = 0.33333333m
        decimal factor = Math.Round(10m / 30m, 4); // 0.3333
        _prorationServiceMock
            .Setup(p => p.CalculateProrationFactorAsync(1, period.StartDate, period.EndDate, employee.HireDate, employee.TerminationDate, It.IsAny<ProrationPolicy?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ProrationResult(factor, 10, 30, "CALENDAR_DAYS", "CAL_DEF"));

        decimal expectedBasic = Math.Round(900m * factor, 3);

        _sscServiceMock
            .Setup(s => s.CalculateSSC(expectedBasic, false, It.IsAny<SSCPolicy?>()))
            .Returns(new SSCResult(expectedBasic, 0m, 0m, 0m, 0m, 0m, 0m, "DEF_SSC"));

        _taxEngineMock
            .Setup(t => t.CalculateIncomeTax(expectedBasic, 0m, 0, It.IsAny<TaxPolicy?>()))
            .Returns(new TaxResult(expectedBasic, 0m, 0m, 0m, 0m, 0m, "DEF_TAX"));

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
            Array.Empty<PayrollAdjustment>()
        );

        Assert.Equal(expectedBasic, line.BasicSalary);
        Assert.Equal(expectedBasic, line.GrossSalary);
        Assert.Equal(expectedBasic, line.NetPay);
    }

    [Theory]
    [InlineData(2026, 2, 28, 28)] // Feb 2026 (non-leap)
    [InlineData(2024, 2, 29, 29)] // Feb 2024 (leap year)
    [InlineData(2026, 4, 30, 30)] // April (30 days)
    [InlineData(2026, 10, 31, 31)] // October (31 days)
    public async Task MonthLengthCalculations_CorrectlyRespectMonthCalendarDays(int year, int month, int lastDay, int expectedDays)
    {
        var period = new PayrollPeriod
        {
            Id = 1,
            CompanyId = 1,
            PeriodCode = $"{year}-{month:D2}",
            StartDate = new DateTime(year, month, 1),
            EndDate = new DateTime(year, month, lastDay)
        };

        var employee = new Employee
        {
            EmployeeCode = $"EMP_{month}",
            HireDate = new DateTime(2020, 1, 1),
            IsActive = true,
            EmploymentStatus = "ACTIVE"
        };

        employee.SalaryStructures.Add(new SalaryStructure
        {
            EmployeeCode = employee.EmployeeCode,
            EffectiveFrom = new DateTime(2020, 1, 1),
            BasicSalary = 1000m,
            IsActive = true
        });

        _prorationServiceMock
            .Setup(p => p.CalculateProrationFactorAsync(1, period.StartDate, period.EndDate, employee.HireDate, null, It.IsAny<ProrationPolicy?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ProrationResult(1.0m, expectedDays, expectedDays, "CALENDAR_DAYS", "CAL_DEF"));

        _sscServiceMock
            .Setup(s => s.CalculateSSC(1000m, false, It.IsAny<SSCPolicy?>()))
            .Returns(new SSCResult(1000m, 75m, 142.5m, 0m, 0.075m, 0.1425m, 3617m, "DEF_SSC"));

        _taxEngineMock
            .Setup(t => t.CalculateIncomeTax(1000m, 75m, 0, It.IsAny<TaxPolicy?>()))
            .Returns(new TaxResult(925m, 9000m, 0m, 11100m, 10m, 0m, "DEF_TAX"));

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
            Array.Empty<PayrollAdjustment>()
        );

        Assert.Equal(1000m, line.BasicSalary);
        Assert.Equal(1000m, line.GrossSalary);
        Assert.Equal(915m, line.NetPay); // 1000 - 75 (SSC) - 10 (Tax) = 915
        Assert.NotNull(line.Snapshot);
        Assert.Equal(1.0m, line.Snapshot.ProrationFactor);
    }

    [Fact]
    public async Task NegativeNetPay_WhenDeductionsExceedGross_IsCalculatedAccurately()
    {
        var period = new PayrollPeriod
        {
            Id = 1,
            CompanyId = 1,
            PeriodCode = "2026-09",
            StartDate = new DateTime(2026, 9, 1),
            EndDate = new DateTime(2026, 9, 30)
        };

        var employee = new Employee
        {
            EmployeeCode = "EMP_DEFICIT",
            HireDate = new DateTime(2020, 1, 1),
            IsActive = true,
            EmploymentStatus = "ACTIVE"
        };

        employee.SalaryStructures.Add(new SalaryStructure
        {
            EmployeeCode = employee.EmployeeCode,
            EffectiveFrom = new DateTime(2020, 1, 1),
            BasicSalary = 500m,
            IsActive = true
        });

        _prorationServiceMock
            .Setup(p => p.CalculateProrationFactorAsync(1, period.StartDate, period.EndDate, employee.HireDate, null, It.IsAny<ProrationPolicy?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ProrationResult(1.0m, 30, 30, "CALENDAR_DAYS", "CAL_DEF"));

        _sscServiceMock
            .Setup(s => s.CalculateSSC(500m, false, It.IsAny<SSCPolicy?>()))
            .Returns(new SSCResult(500m, 37.5m, 71.25m, 0m, 0.075m, 0.1425m, 3617m, "DEF_SSC"));

        _taxEngineMock
            .Setup(t => t.CalculateIncomeTax(500m, 37.5m, 0, It.IsAny<TaxPolicy?>()))
            .Returns(new TaxResult(462.5m, 9000m, 0m, 0m, 0m, 0m, "DEF_TAX"));

        // Loan deduction of 600 JOD exceeds the remaining salary
        _loanDeductionServiceMock
            .Setup(l => l.CalculateAndApplyDeductionsAsync(null, employee.EmployeeCode, period.PeriodCode, It.IsAny<decimal>(), It.IsAny<DeductionPolicy?>(), false, It.IsAny<CancellationToken>(), It.IsAny<string?>()))
            .ReturnsAsync(new LoanDeductionCalculationResult(600m, 600m, 0m, new(), new()));

        var line = await _engine.CalculateEmployeePayrollLineAsync(
            period,
            employee,
            null,
            null,
            null,
            null,
            Array.Empty<PayrollAdjustment>()
        );

        // Gross = 500
        // Deductions = 37.5 (SSC) + 600 (Loan) = 637.5
        // NetPay = 500 - 637.5 = -137.5
        Assert.Equal(500m, line.GrossSalary);
        Assert.Equal(637.5m, line.TotalDeductions);
        Assert.Equal(-137.5m, line.NetPay);
        Assert.True(line.NetPay < 0, "Net pay reflects payroll deficit when deductions exceed gross");
    }

    [Fact]
    public async Task MultipleUnpaidLeaveSlices_CompoundDeductionsAccurately()
    {
        var period = new PayrollPeriod
        {
            Id = 1,
            CompanyId = 1,
            PeriodCode = "2026-09",
            StartDate = new DateTime(2026, 9, 1),
            EndDate = new DateTime(2026, 9, 30)
        };

        var employee = new Employee
        {
            EmployeeCode = "EMP_MULTI_LEAVE",
            HireDate = new DateTime(2020, 1, 1),
            IsActive = true,
            EmploymentStatus = "ACTIVE"
        };

        employee.SalaryStructures.Add(new SalaryStructure
        {
            EmployeeCode = employee.EmployeeCode,
            EffectiveFrom = new DateTime(2020, 1, 1),
            BasicSalary = 1200m, // Daily rate = 1200 / 30 = 40 JOD/day
            IsActive = true
        });

        _prorationServiceMock
            .Setup(p => p.CalculateProrationFactorAsync(1, period.StartDate, period.EndDate, employee.HireDate, null, It.IsAny<ProrationPolicy?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ProrationResult(1.0m, 30, 30, "CALENDAR_DAYS", "CAL_DEF"));

        // 2 days + 3 days = 5 days total unpaid leave in the period
        _leaveServiceMock
            .Setup(l => l.GetUnpaidLeaveDaysInPeriodAsync(employee.EmployeeCode, period.StartDate, period.EndDate, It.IsAny<CancellationToken>()))
            .ReturnsAsync(5m);

        // Basic salary after 5 * 40 = 200 deduction is 1000 JOD
        _sscServiceMock
            .Setup(s => s.CalculateSSC(1000m, false, It.IsAny<SSCPolicy?>()))
            .Returns(new SSCResult(1000m, 75m, 142.5m, 0m, 0.075m, 0.1425m, 3617m, "DEF_SSC"));

        _taxEngineMock
            .Setup(t => t.CalculateIncomeTax(1000m, 75m, 0, It.IsAny<TaxPolicy?>()))
            .Returns(new TaxResult(925m, 9000m, 0m, 0m, 0m, 0m, "DEF_TAX"));

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
            Array.Empty<PayrollAdjustment>()
        );

        Assert.Equal(1000m, line.BasicSalary);
        Assert.Equal(1000m, line.GrossSalary);
        Assert.Contains(line.Components, c => c.ComponentCode == "UNPAID_LEAVE" && c.Amount == 200m);
    }

    [Fact]
    public async Task CurrencyConsistency_PreservesConfiguredCurrencyAcrossRunLine()
    {
        var period = new PayrollPeriod
        {
            Id = 1,
            CompanyId = 1,
            PeriodCode = "2026-09",
            StartDate = new DateTime(2026, 9, 1),
            EndDate = new DateTime(2026, 9, 30)
        };

        var employee = new Employee
        {
            EmployeeCode = "EMP_USD",
            HireDate = new DateTime(2020, 1, 1),
            IsActive = true,
            EmploymentStatus = "ACTIVE"
        };

        employee.SalaryStructures.Add(new SalaryStructure
        {
            EmployeeCode = employee.EmployeeCode,
            EffectiveFrom = new DateTime(2020, 1, 1),
            BasicSalary = 3000m,
            CurrencyCode = "USD",
            PaymentMethod = "BANK_TRANSFER",
            IsActive = true
        });

        _prorationServiceMock
            .Setup(p => p.CalculateProrationFactorAsync(1, period.StartDate, period.EndDate, employee.HireDate, null, It.IsAny<ProrationPolicy?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ProrationResult(1.0m, 30, 30, "CALENDAR_DAYS", "CAL_DEF"));

        _sscServiceMock
            .Setup(s => s.CalculateSSC(3000m, false, It.IsAny<SSCPolicy?>()))
            .Returns(new SSCResult(3000m, 225m, 427.5m, 0m, 0.075m, 0.1425m, 3617m, "DEF_SSC"));

        _taxEngineMock
            .Setup(t => t.CalculateIncomeTax(3000m, 225m, 0, It.IsAny<TaxPolicy?>()))
            .Returns(new TaxResult(2775m, 9000m, 0m, 0m, 0m, 0m, "DEF_TAX"));

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
            Array.Empty<PayrollAdjustment>()
        );

        Assert.Equal("USD", line.CurrencyCode);
        Assert.Equal("BANK_TRANSFER", line.PaymentMethod);
        Assert.Equal(3000m, line.BasicSalary);
        Assert.Equal(2775m, line.NetPay);
    }

    [Fact]
    public async Task CalculateEmployeePayrollLine_WithLoanDeductions_AddsLoanComponentAndComputesNetPay()
    {
        var period = new PayrollPeriod
        {
            Id = 1,
            CompanyId = 1,
            PeriodCode = "2026-09",
            StartDate = new DateTime(2026, 9, 1),
            EndDate = new DateTime(2026, 9, 30)
        };

        var employee = new Employee
        {
            EmployeeCode = "EMP_LOAN",
            HireDate = new DateTime(2020, 1, 1),
            IsActive = true,
            EmploymentStatus = "ACTIVE"
        };

        employee.SalaryStructures.Add(new SalaryStructure
        {
            EmployeeCode = employee.EmployeeCode,
            EffectiveFrom = new DateTime(2020, 1, 1),
            BasicSalary = 1000m,
            CurrencyCode = "JOD",
            PaymentMethod = "BANK_TRANSFER",
            IsActive = true
        });

        _prorationServiceMock
            .Setup(p => p.CalculateProrationFactorAsync(1, period.StartDate, period.EndDate, employee.HireDate, null, It.IsAny<ProrationPolicy?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ProrationResult(1.0m, 30, 30, "CALENDAR_DAYS", "CAL_DEF"));

        _sscServiceMock
            .Setup(s => s.CalculateSSC(1000m, false, It.IsAny<SSCPolicy?>()))
            .Returns(new SSCResult(1000m, 75m, 142.5m, 0m, 0.075m, 0.1425m, 3617m, "DEF_SSC"));

        _taxEngineMock
            .Setup(t => t.CalculateIncomeTax(1000m, 75m, 0, It.IsAny<TaxPolicy?>()))
            .Returns(new TaxResult(925m, 9000m, 0m, 0m, 0m, 0m, "DEF_TAX"));

        _loanDeductionServiceMock
            .Setup(l => l.CalculateAndApplyDeductionsAsync(null, employee.EmployeeCode, period.PeriodCode, 925m, It.IsAny<DeductionPolicy?>(), false, It.IsAny<CancellationToken>(), It.IsAny<string?>()))
            .ReturnsAsync(new LoanDeductionCalculationResult(150m, 150m, 0m, new(), new()));

        var line = await _engine.CalculateEmployeePayrollLineAsync(
            period, employee, null, null, null, null, Array.Empty<PayrollAdjustment>());

        Assert.Equal(1000m, line.BasicSalary);
        Assert.Equal(1000m, line.GrossSalary);
        Assert.Equal(150m, line.OtherDeductions);
        Assert.Equal(225m, line.TotalDeductions); // 75 SSC + 150 Loans
        Assert.Equal(775m, line.NetPay); // 1000 - 225
        Assert.Contains(line.Components, c => c.ComponentCode == "LOAN_DEDUCTION" && c.Amount == 150m && c.ComponentType == "DEDUCTION");
    }

    [Fact]
    public async Task CalculateEmployeePayrollLine_VariableAdjustmentEarnings_AddsToGrossAndTotalEarnings()
    {
        var period = new PayrollPeriod
        {
            Id = 1,
            CompanyId = 1,
            PeriodCode = "2026-09",
            StartDate = new DateTime(2026, 9, 1),
            EndDate = new DateTime(2026, 9, 30)
        };

        var employee = new Employee
        {
            EmployeeCode = "EMP_ADJ",
            HireDate = new DateTime(2020, 1, 1),
            IsActive = true,
            EmploymentStatus = "ACTIVE"
        };

        employee.SalaryStructures.Add(new SalaryStructure
        {
            EmployeeCode = employee.EmployeeCode,
            EffectiveFrom = new DateTime(2020, 1, 1),
            BasicSalary = 800m,
            CurrencyCode = "JOD",
            PaymentMethod = "BANK_TRANSFER",
            IsActive = true
        });

        var adjustments = new[]
        {
            new PayrollAdjustment
            {
                ComponentCode = "PROJECT_BONUS",
                Amount = 250m,
                AdjustmentType = "EARNING",
                Component = new SalaryComponent { ComponentCode = "PROJECT_BONUS", ComponentType = "ALLOWANCE" }
            }
        };

        _prorationServiceMock
            .Setup(p => p.CalculateProrationFactorAsync(1, period.StartDate, period.EndDate, employee.HireDate, null, It.IsAny<ProrationPolicy?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ProrationResult(1.0m, 30, 30, "CALENDAR_DAYS", "CAL_DEF"));

        _sscServiceMock
            .Setup(s => s.CalculateSSC(1050m, false, It.IsAny<SSCPolicy?>()))
            .Returns(new SSCResult(1050m, 78.75m, 149.625m, 0m, 0.075m, 0.1425m, 3617m, "DEF_SSC"));

        _taxEngineMock
            .Setup(t => t.CalculateIncomeTax(1050m, 78.75m, 0, It.IsAny<TaxPolicy?>()))
            .Returns(new TaxResult(971.25m, 9000m, 0m, 0m, 0m, 0m, "DEF_TAX"));

        _loanDeductionServiceMock
            .Setup(l => l.CalculateAndApplyDeductionsAsync(null, employee.EmployeeCode, period.PeriodCode, It.IsAny<decimal>(), It.IsAny<DeductionPolicy?>(), false, It.IsAny<CancellationToken>(), It.IsAny<string?>()))
            .ReturnsAsync(new LoanDeductionCalculationResult(0m, 0m, 0m, new(), new()));

        var line = await _engine.CalculateEmployeePayrollLineAsync(
            period, employee, null, null, null, null, adjustments);

        Assert.Equal(800m, line.BasicSalary);
        Assert.Equal(250m, line.TotalEarnings);
        Assert.Equal(1050m, line.GrossSalary);
        Assert.Contains(line.Components, c => c.ComponentCode == "PROJECT_BONUS" && c.Amount == 250m && c.ComponentType == "EARNING");
    }

    [Fact]
    public async Task CalculateEmployeePayrollLine_MultipleStructureAllowances_CorrectlyProratesEachAllowance()
    {
        var period = new PayrollPeriod
        {
            Id = 1,
            CompanyId = 1,
            PeriodCode = "2026-09",
            StartDate = new DateTime(2026, 9, 1),
            EndDate = new DateTime(2026, 9, 30)
        };

        var employee = new Employee
        {
            EmployeeCode = "EMP_MULTI_ALLOW",
            HireDate = new DateTime(2026, 9, 16), // Half month
            IsActive = true,
            EmploymentStatus = "ACTIVE"
        };

        employee.SalaryStructures.Add(new SalaryStructure
        {
            EmployeeCode = employee.EmployeeCode,
            EffectiveFrom = new DateTime(2026, 9, 16),
            BasicSalary = 1000m,
            CurrencyCode = "JOD",
            PaymentMethod = "BANK_TRANSFER",
            IsActive = true,
            Lines = new List<SalaryStructureLine>
            {
                new()
                {
                    ComponentCode = "TRANS",
                    Amount = 100m,
                    IsActive = true,
                    Component = new SalaryComponent { ComponentCode = "TRANS", ComponentType = "ALLOWANCE" }
                },
                new()
                {
                    ComponentCode = "PHONE",
                    Amount = 50m,
                    IsActive = true,
                    Component = new SalaryComponent { ComponentCode = "PHONE", ComponentType = "ALLOWANCE" }
                }
            }
        });

        _prorationServiceMock
            .Setup(p => p.CalculateProrationFactorAsync(1, period.StartDate, period.EndDate, employee.HireDate, null, It.IsAny<ProrationPolicy?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ProrationResult(0.5m, 15, 30, "CALENDAR_DAYS", "CAL_DEF"));

        _sscServiceMock
            .Setup(s => s.CalculateSSC(575m, false, It.IsAny<SSCPolicy?>())) // 500 basic + 50 trans + 25 phone
            .Returns(new SSCResult(575m, 43.125m, 81.938m, 0m, 0.075m, 0.1425m, 3617m, "DEF_SSC"));

        _taxEngineMock
            .Setup(t => t.CalculateIncomeTax(575m, 43.125m, 0, It.IsAny<TaxPolicy?>()))
            .Returns(new TaxResult(531.875m, 9000m, 0m, 0m, 0m, 0m, "DEF_TAX"));

        _loanDeductionServiceMock
            .Setup(l => l.CalculateAndApplyDeductionsAsync(null, employee.EmployeeCode, period.PeriodCode, It.IsAny<decimal>(), It.IsAny<DeductionPolicy?>(), false, It.IsAny<CancellationToken>(), It.IsAny<string?>()))
            .ReturnsAsync(new LoanDeductionCalculationResult(0m, 0m, 0m, new(), new()));

        var line = await _engine.CalculateEmployeePayrollLineAsync(
            period, employee, null, null, null, null, Array.Empty<PayrollAdjustment>());

        Assert.Equal(500m, line.BasicSalary);
        Assert.Equal(75m, line.TotalEarnings); // 50 + 25
        Assert.Equal(575m, line.GrossSalary);
        Assert.Contains(line.Components, c => c.ComponentCode == "TRANS" && c.Amount == 50m);
        Assert.Contains(line.Components, c => c.ComponentCode == "PHONE" && c.Amount == 25m);
    }
}

