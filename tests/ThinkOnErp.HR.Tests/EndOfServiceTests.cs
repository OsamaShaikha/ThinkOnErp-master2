using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using ThinkOnErp.Application.DTOs.Hr;
using ThinkOnErp.Application.Services.Hr;
using ThinkOnErp.Domain.Entities.Hr;
using ThinkOnErp.Domain.Interfaces.Accounting;
using ThinkOnErp.Domain.Interfaces.Hr;
using Xunit;

namespace ThinkOnErp.HR.Tests;

public sealed class EndOfServiceTests
{
    private readonly Mock<IEndOfServiceRepository> _eosRepoMock = new();
    private readonly Mock<IEmployeeRepository> _employeeRepoMock = new();
    private readonly Mock<ICompensationRepository> _compensationRepoMock = new();
    private readonly Mock<ILeaveRepository> _leaveRepoMock = new();
    private readonly Mock<IGlVoucherRepository> _glVoucherRepoMock = new();
    private readonly EndOfServiceService _eosService;

    public EndOfServiceTests()
    {
        _eosService = new EndOfServiceService(
            _eosRepoMock.Object,
            _employeeRepoMock.Object,
            _compensationRepoMock.Object,
            _leaveRepoMock.Object,
            _glVoucherRepoMock.Object,
            NullLogger<EndOfServiceService>.Instance);
    }

    [Fact]
    public async Task CalculateFinalSettlement_WithGratuityAndLeaveEncashment_ComputesAccurateNetPayout()
    {
        // Arrange: 3 years service, 1,000 JOD basic salary, 15 unused annual leave days, 200 JOD loan deduction
        var terminationDate = new DateTime(2026, 6, 30);
        var hireDate = new DateTime(2023, 6, 30); // 3.00 years

        var emp = new Employee
        {
            EmployeeCode = "EMP-EOS-01",
            NameEn = "Departing Senior Engineer",
            HireDate = hireDate,
            EmploymentStatus = "ACTIVE",
            IsActive = true
        };

        var structure = new EmployeeSalaryStructure
        {
            EmployeeCode = "EMP-EOS-01",
            BasicSalary = 1000.00m,
            IsActive = true
        };

        var leaveBalance = new LeaveBalance
        {
            EmployeeCode = "EMP-EOS-01",
            LeaveTypeCode = "ANNUAL",
            Year = 2026,
            AccruedDays = 15m,
            UsedDays = 0m,
            CarriedForwardDays = 0m
        };

        _employeeRepoMock.Setup(e => e.GetByCodeAsync("EMP-EOS-01", true, It.IsAny<CancellationToken>()))
            .ReturnsAsync(emp);

        _compensationRepoMock.Setup(c => c.GetActiveStructureAsync("EMP-EOS-01", terminationDate, It.IsAny<CancellationToken>()))
            .ReturnsAsync(structure);

        _leaveRepoMock.Setup(l => l.GetBalancesByEmployeeAsync("EMP-EOS-01", 2026, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<LeaveBalance> { leaveBalance });

        _eosRepoMock.Setup(e => e.GetSettlementByIdAsync(It.IsAny<long>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((long id, CancellationToken _) => new FinalSettlement
            {
                Id = id,
                EmployeeCode = "EMP-EOS-01",
                ServiceYears = 3.00m,
                LastBasicSalary = 1000.00m,
                EndOfServiceGratuity = 3000.00m, // 3 yrs * 1000 JOD
                UnusedLeaveDays = 15m,
                UnusedLeaveEncashment = 500.00m, // 15 * (1000/30) = 500 JOD
                LoanBalanceDeduction = 200.00m,
                Status = "DRAFT"
            });

        var dto = new CalculateFinalSettlementDto
        {
            EmployeeCode = "EMP-EOS-01",
            TerminationDate = terminationDate,
            TerminationReason = "RESIGNATION",
            LoanBalanceDeduction = 200.00m
        };

        // Act
        var result = await _eosService.CalculateFinalSettlementAsync(dto, "HR_ADMIN");

        // Assert:
        // Gratuity: 3,000 JOD
        // Leave encashment: 500 JOD
        // Entitlements: 3,500 JOD
        // Deductions: 200 JOD
        // Net: 3,300 JOD
        result.EndOfServiceGratuity.Should().Be(3000.00m);
        result.UnusedLeaveEncashment.Should().Be(500.00m);
        result.TotalEntitlements.Should().Be(3500.00m);
        result.TotalDeductions.Should().Be(200.00m);
        result.NetSettlementAmount.Should().Be(3300.00m);
    }
}
