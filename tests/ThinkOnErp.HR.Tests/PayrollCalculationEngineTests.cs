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
            .Setup(l => l.CalculateAndApplyDeductionsAsync(null, employee.EmployeeCode, period.PeriodCode, It.IsAny<decimal>(), It.IsAny<DeductionPolicy?>(), false, It.IsAny<CancellationToken>()))
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
            .Setup(l => l.CalculateAndApplyDeductionsAsync(null, employee.EmployeeCode, period.PeriodCode, It.IsAny<decimal>(), It.IsAny<DeductionPolicy?>(), false, It.IsAny<CancellationToken>()))
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
}
