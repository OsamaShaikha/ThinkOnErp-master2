using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using ThinkOnErp.Application.Services.Hr;
using ThinkOnErp.Domain.Entities.Hr;
using ThinkOnErp.Domain.Interfaces.Hr;
using Xunit;

namespace ThinkOnErp.HR.Tests;

public sealed class PayrollCalculationEngineTests
{
    private readonly Mock<IEmployeeRepository> _employeeRepoMock = new();
    private readonly Mock<ICompensationRepository> _compensationRepoMock = new();
    private readonly Mock<ILeaveRepository> _leaveRepoMock = new();
    private readonly Mock<IPayrollProrationService> _prorationServiceMock = new();
    private readonly Mock<IOvertimeCalculationService> _overtimeServiceMock = new();
    private readonly Mock<ISSCCalculationService> _sscServiceMock = new();
    private readonly Mock<ITaxCalculationEngine> _taxEngineMock = new();
    private readonly Mock<ILoanDeductionService> _loanServiceMock = new();
    private readonly Mock<IPayrollPeriodRepository> _periodRepoMock = new();

    private readonly PayrollCalculationEngine _engine;

    public PayrollCalculationEngineTests()
    {
        // Default mocks
        _prorationServiceMock.Setup(p => p.CalculateProrationFactorAsync(It.IsAny<string>(), It.IsAny<DateTime>(), It.IsAny<DateTime?>(), It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<long>()))
            .ReturnsAsync(new ProrationResult(1.0m, 30m, 30m, "FIXED_30", "POLICY_FIXED_30"));

        _overtimeServiceMock.Setup(o => o.CalculateOvertimeEarningsAsync(It.IsAny<string>(), It.IsAny<decimal>(), It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<long>()))
            .ReturnsAsync(0m);

        _leaveRepoMock.Setup(l => l.GetRequestsAsync(It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<DateTime?>(), It.IsAny<DateTime?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<LeaveRequest>());

        _loanServiceMock.Setup(l => l.ProcessPeriodLoanDeductionsAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<decimal>(), It.IsAny<long>(), It.IsAny<DateTime>()))
            .ReturnsAsync(new LoanDeductionResult(0m, 0m, new List<DeductedLoanItem>()));

        _sscServiceMock.Setup(s => s.CalculateSSCAsync(It.IsAny<decimal>(), It.IsAny<bool>(), It.IsAny<long>(), It.IsAny<DateTime>()))
            .ReturnsAsync(new SSCCalculationResult(1200.00m, 90.00m, 171.00m, 3349.00m, 0.075m, 0.1425m, "JORDAN_SSC_2026"));

        _taxEngineMock.Setup(t => t.CalculateTaxAsync(It.IsAny<decimal>(), It.IsAny<int>(), It.IsAny<long>(), It.IsAny<DateTime>()))
            .ReturnsAsync(new TaxCalculationResult(18.00m, 0m, 4320.00m, 9000.00m, "JORDAN_ISTD_2026"));

        _engine = new PayrollCalculationEngine(
            _employeeRepoMock.Object,
            _compensationRepoMock.Object,
            _leaveRepoMock.Object,
            _prorationServiceMock.Object,
            _overtimeServiceMock.Object,
            _sscServiceMock.Object,
            _taxEngineMock.Object,
            _loanServiceMock.Object,
            _periodRepoMock.Object,
            NullLogger<PayrollCalculationEngine>.Instance);
    }

    [Fact]
    public async Task CalculateEmployeePayroll_StandardSalary_ComputesFullPipelineCorrectly()
    {
        // Arrange
        var emp = new Employee
        {
            EmployeeCode = "EMP-001",
            NameAr = "أحمد الخالدي",
            NameEn = "Ahmad Al-Khalidi",
            TaxExemptionCount = 0,
            IsHighRiskRole = false,
            HireDate = new DateTime(2023, 1, 1),
            BranchId = 1
        };

        var structure = new EmployeeSalaryStructure
        {
            EmployeeCode = "EMP-001",
            BasicSalary = 1000.00m,
            IsActive = true,
            Lines = new List<EmployeeSalaryStructureLine>
            {
                new()
                {
                    ComponentCode = "HOUSING",
                    Amount = 200.00m,
                    Component = new SalaryComponent
                    {
                        ComponentCode = "HOUSING",
                        NameEn = "Housing Allowance",
                        NameAr = "بدل سكن",
                        ComponentType = "ALLOWANCE",
                        IsTaxable = true,
                        IsSscApplicable = true
                    }
                }
            }
        };

        _employeeRepoMock.Setup(r => r.GetByCodeAsync("EMP-001", true, It.IsAny<CancellationToken>()))
            .ReturnsAsync(emp);
        _compensationRepoMock.Setup(r => r.GetActiveStructureAsync("EMP-001", It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(structure);

        // Act
        var result = await _engine.CalculateEmployeePayrollAsync("EMP-001", "2026-08", 1, "TEST_USER");

        // Assert
        result.GrossSalary.Should().Be(1200.00m);
        result.SscEmployeeContribution.Should().Be(90.00m);
        result.SscEmployerContribution.Should().Be(171.00m);
        result.IncomeTaxWithheld.Should().Be(18.00m);
        result.NationalContributionWithheld.Should().Be(0m);
        // Net pay: Gross (1200) - SSC (90) - Tax (18) = 1092 JOD
        result.NetPay.Should().Be(1092.00m);
    }
}
