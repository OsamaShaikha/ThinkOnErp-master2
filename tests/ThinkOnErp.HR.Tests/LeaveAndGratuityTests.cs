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

public sealed class LeaveAndGratuityTests
{
    private readonly Mock<ILeaveRepository> _leaveRepoMock = new();
    private readonly Mock<IEmployeeRepository> _employeeRepoMock = new();
    private readonly LeaveService _leaveService;

    public LeaveAndGratuityTests()
    {
        _leaveService = new LeaveService(
            _leaveRepoMock.Object,
            _employeeRepoMock.Object,
            NullLogger<LeaveService>.Instance);
    }

    [Fact]
    public async Task RunMonthlyAccrual_SeniorEmployee_AccruesBasedOn21DayTier()
    {
        // Arrange: Employee with > 5 years of service
        var seniorEmp = new Employee
        {
            EmployeeCode = "EMP-SENIOR",
            NameEn = "Senior Consultant",
            HireDate = DateTime.UtcNow.AddYears(-6),
            EmploymentStatus = "ACTIVE",
            IsActive = true
        };

        var policy = new LeavePolicy
        {
            LeaveTypeCode = "ANNUAL",
            PolicyName = "Jordan Standard Annual Leave",
            AccrualMethod = "SERVICE_TIERED",
            Tier1YearsThreshold = 5,
            Tier1Days = 14m,
            Tier2Days = 21m, // 21 days for 5+ years
            IsActive = true
        };

        _employeeRepoMock.Setup(e => e.GetAllAsync(null, null, null, true, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Employee> { seniorEmp });

        _leaveRepoMock.Setup(l => l.GetAllActivePoliciesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<LeavePolicy> { policy });

        _leaveRepoMock.Setup(l => l.GetBalanceAsync("EMP-SENIOR", "ANNUAL", It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((LeaveBalance?)null);

        // Act
        var result = await _leaveService.RunMonthlyAccrualAsync(DateTime.UtcNow.Year, "TEST_USER");

        // Assert: 21 days / 12 months = 1.75 days monthly accrual
        result.ProcessedEmployees.Should().Be(1);
        _leaveRepoMock.Verify(l => l.AddBalanceAsync(It.Is<LeaveBalance>(b => b.AccruedDays == 1.75m), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task RunMonthlyAccrual_JuniorEmployee_AccruesBasedOn14DayTier()
    {
        // Arrange: Employee with < 5 years of service
        var juniorEmp = new Employee
        {
            EmployeeCode = "EMP-JUNIOR",
            NameEn = "Junior Developer",
            HireDate = DateTime.UtcNow.AddYears(-2),
            EmploymentStatus = "ACTIVE",
            IsActive = true
        };

        var policy = new LeavePolicy
        {
            LeaveTypeCode = "ANNUAL",
            PolicyName = "Jordan Standard Annual Leave",
            AccrualMethod = "SERVICE_TIERED",
            Tier1YearsThreshold = 5,
            Tier1Days = 14m,
            Tier2Days = 21m,
            IsActive = true
        };

        _employeeRepoMock.Setup(e => e.GetAllAsync(null, null, null, true, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Employee> { juniorEmp });

        _leaveRepoMock.Setup(l => l.GetAllActivePoliciesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<LeavePolicy> { policy });

        _leaveRepoMock.Setup(l => l.GetBalanceAsync("EMP-JUNIOR", "ANNUAL", It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((LeaveBalance?)null);

        // Act
        var result = await _leaveService.RunMonthlyAccrualAsync(DateTime.UtcNow.Year, "TEST_USER");

        // Assert: 14 days / 12 months = 1.17 days monthly accrual (rounded to 2 decimal places in service)
        result.ProcessedEmployees.Should().Be(1);
        _leaveRepoMock.Verify(l => l.AddBalanceAsync(It.Is<LeaveBalance>(b => b.AccruedDays == 1.17m), It.IsAny<CancellationToken>()), Times.Once);
    }
}
