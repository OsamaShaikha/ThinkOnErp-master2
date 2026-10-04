using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Moq;
using ThinkOnErp.Application.DTOs.Hr;
using ThinkOnErp.Application.Services.Hr;
using ThinkOnErp.Domain.Entities.Hr;
using ThinkOnErp.Domain.Interfaces.Hr;
using Xunit;

namespace ThinkOnErp.HR.Tests;

public class LeaveAccrualAndGrantTests
{
    private readonly Mock<ILeaveRepository> _leaveRepoMock;
    private readonly Mock<IEmployeeRepository> _employeeRepoMock;
    private readonly LeaveService _service;

    public LeaveAccrualAndGrantTests()
    {
        _leaveRepoMock = new Mock<ILeaveRepository>();
        _employeeRepoMock = new Mock<IEmployeeRepository>();
        _service = new LeaveService(_leaveRepoMock.Object, _employeeRepoMock.Object);
    }

    private static LeaveType CreateAnnualLeaveType()
    {
        return new LeaveType
        {
            LeaveTypeCode = "ANNUAL",
            NameLocal = "إجازة سنوية",
            NameEn = "Annual Leave",
            IsPaid = true,
            IsStatutory = true,
            MaxDaysPerYear = 14,
            CarryForwardAllowed = true,
            CarryForwardCapDays = 7,
            IsActive = true
        };
    }

    private static LeavePolicy CreateAnnualLeavePolicy()
    {
        return new LeavePolicy
        {
            Id = 1,
            PolicyName = "سياسة الإجازات السنوية",
            LeaveTypeCode = "ANNUAL",
            AccrualMethod = "TENURE_BASED",
            Tier1YearsThreshold = 5,
            Tier1Days = 14,
            Tier2Days = 21,
            IsActive = true
        };
    }

    private static Employee CreateEmployee(string code, DateTime hireDate, string status = "ACTIVE")
    {
        return new Employee
        {
            EmployeeCode = code,
            NameLocal = "موظف اختبار",
            NameEn = "Test Employee",
            HireDate = hireDate,
            EmploymentStatus = status,
            IsActive = true
        };
    }

    [Fact]
    public async Task MonthlyAccrual_JuniorEmployee_Accrues1_17Days()
    {
        // Arrange: hired 2 years ago -> Tier 1 (14 days / yr = 1.17 days/mo)
        var emp = CreateEmployee("EMP001", new DateTime(2024, 1, 1));
        var leaveType = CreateAnnualLeaveType();
        var policy = CreateAnnualLeavePolicy();

        _employeeRepoMock.Setup(r => r.GetEmployeeByCodeAsync("EMP001", It.IsAny<CancellationToken>()))
            .ReturnsAsync(emp);
        _leaveRepoMock.Setup(r => r.GetLeaveTypeByCodeAsync("ANNUAL", It.IsAny<CancellationToken>()))
            .ReturnsAsync(leaveType);
        _leaveRepoMock.Setup(r => r.GetLeavePolicyByTypeCodeAsync("ANNUAL", It.IsAny<CancellationToken>()))
            .ReturnsAsync(policy);
        _leaveRepoMock.Setup(r => r.GetLeaveBalanceAsync("EMP001", "ANNUAL", 2026, It.IsAny<CancellationToken>()))
            .ReturnsAsync((LeaveBalance?)null);

        LeaveBalance? savedBalance = null;
        _leaveRepoMock.Setup(r => r.AddLeaveBalanceAsync(It.IsAny<LeaveBalance>(), It.IsAny<CancellationToken>()))
            .Callback<LeaveBalance, CancellationToken>((b, _) => savedBalance = b)
            .Returns(Task.CompletedTask);

        // Act
        var dto = new MonthlyLeaveAccrualDto(2026, 3, "ANNUAL", "EMP001");
        var result = await _service.RunMonthlyAccrualAsync(dto, "TEST_USER");

        // Assert
        Assert.Equal(1, result.TotalEmployeesProcessed);
        Assert.NotNull(savedBalance);
        Assert.Equal(1.17m, savedBalance!.AccruedDays);
        Assert.Equal(2026, savedBalance.YearNo);
        Assert.Equal("EMP001", savedBalance.EmployeeCode);
    }

    [Fact]
    public async Task MonthlyAccrual_SeniorEmployee_Accrues1_75Days()
    {
        // Arrange: hired 7 years ago -> Tier 2 (21 days / yr = 1.75 days/mo)
        var emp = CreateEmployee("EMP002", new DateTime(2019, 1, 1));
        var leaveType = CreateAnnualLeaveType();
        var policy = CreateAnnualLeavePolicy();

        _employeeRepoMock.Setup(r => r.GetEmployeeByCodeAsync("EMP002", It.IsAny<CancellationToken>()))
            .ReturnsAsync(emp);
        _leaveRepoMock.Setup(r => r.GetLeaveTypeByCodeAsync("ANNUAL", It.IsAny<CancellationToken>()))
            .ReturnsAsync(leaveType);
        _leaveRepoMock.Setup(r => r.GetLeavePolicyByTypeCodeAsync("ANNUAL", It.IsAny<CancellationToken>()))
            .ReturnsAsync(policy);
        _leaveRepoMock.Setup(r => r.GetLeaveBalanceAsync("EMP002", "ANNUAL", 2026, It.IsAny<CancellationToken>()))
            .ReturnsAsync((LeaveBalance?)null);

        LeaveBalance? savedBalance = null;
        _leaveRepoMock.Setup(r => r.AddLeaveBalanceAsync(It.IsAny<LeaveBalance>(), It.IsAny<CancellationToken>()))
            .Callback<LeaveBalance, CancellationToken>((b, _) => savedBalance = b)
            .Returns(Task.CompletedTask);

        // Act
        var dto = new MonthlyLeaveAccrualDto(2026, 3, "ANNUAL", "EMP002");
        var result = await _service.RunMonthlyAccrualAsync(dto, "TEST_USER");

        // Assert
        Assert.Equal(1, result.TotalEmployeesProcessed);
        Assert.NotNull(savedBalance);
        Assert.Equal(1.75m, savedBalance!.AccruedDays);
    }

    [Fact]
    public async Task MonthlyAccrual_MidMonthHire_ProratesCorrectly()
    {
        // Arrange: hired on June 16, 2026 in a 30-day month -> 15 days active (June 16..30)
        // Rate = 14 / 12 = 1.1667 days/mo. Prorated = 1.1667 * (15 / 30) = 0.5833 -> round 0.58
        var emp = CreateEmployee("EMP003", new DateTime(2026, 6, 16));
        var leaveType = CreateAnnualLeaveType();
        var policy = CreateAnnualLeavePolicy();

        _employeeRepoMock.Setup(r => r.GetEmployeeByCodeAsync("EMP003", It.IsAny<CancellationToken>()))
            .ReturnsAsync(emp);
        _leaveRepoMock.Setup(r => r.GetLeaveTypeByCodeAsync("ANNUAL", It.IsAny<CancellationToken>()))
            .ReturnsAsync(leaveType);
        _leaveRepoMock.Setup(r => r.GetLeavePolicyByTypeCodeAsync("ANNUAL", It.IsAny<CancellationToken>()))
            .ReturnsAsync(policy);
        _leaveRepoMock.Setup(r => r.GetLeaveBalanceAsync("EMP003", "ANNUAL", 2026, It.IsAny<CancellationToken>()))
            .ReturnsAsync((LeaveBalance?)null);

        LeaveBalance? savedBalance = null;
        _leaveRepoMock.Setup(r => r.AddLeaveBalanceAsync(It.IsAny<LeaveBalance>(), It.IsAny<CancellationToken>()))
            .Callback<LeaveBalance, CancellationToken>((b, _) => savedBalance = b)
            .Returns(Task.CompletedTask);

        // Act
        var dto = new MonthlyLeaveAccrualDto(2026, 6, "ANNUAL", "EMP003");
        var result = await _service.RunMonthlyAccrualAsync(dto, "TEST_USER");

        // Assert
        Assert.Equal(1, result.TotalEmployeesProcessed);
        Assert.NotNull(savedBalance);
        Assert.Equal(0.58m, savedBalance!.AccruedDays);
    }

    [Fact]
    public async Task MonthlyAccrual_RespectsAnnualQuotaCap()
    {
        // Arrange: already has 13.5 days accrued. Policy max is 14 days. Accrual would be 1.17 days.
        // It should cap at 14.0 days (+0.5 days added).
        var emp = CreateEmployee("EMP004", new DateTime(2024, 1, 1));
        var leaveType = CreateAnnualLeaveType();
        var policy = CreateAnnualLeavePolicy();

        var existingBalance = new LeaveBalance
        {
            Id = 10,
            EmployeeCode = "EMP004",
            LeaveTypeCode = "ANNUAL",
            YearNo = 2026,
            AccruedDays = 13.5m,
            UsedDays = 0,
            CarriedForwardDays = 0
        };

        _employeeRepoMock.Setup(r => r.GetEmployeeByCodeAsync("EMP004", It.IsAny<CancellationToken>()))
            .ReturnsAsync(emp);
        _leaveRepoMock.Setup(r => r.GetLeaveTypeByCodeAsync("ANNUAL", It.IsAny<CancellationToken>()))
            .ReturnsAsync(leaveType);
        _leaveRepoMock.Setup(r => r.GetLeavePolicyByTypeCodeAsync("ANNUAL", It.IsAny<CancellationToken>()))
            .ReturnsAsync(policy);
        _leaveRepoMock.Setup(r => r.GetLeaveBalanceAsync("EMP004", "ANNUAL", 2026, It.IsAny<CancellationToken>()))
            .ReturnsAsync(existingBalance);

        // Act
        var dto = new MonthlyLeaveAccrualDto(2026, 12, "ANNUAL", "EMP004");
        var result = await _service.RunMonthlyAccrualAsync(dto, "TEST_USER");

        // Assert
        Assert.Equal(1, result.TotalEmployeesProcessed);
        Assert.Equal(14.0m, existingBalance.AccruedDays);
        _leaveRepoMock.Verify(r => r.UpdateLeaveBalanceAsync(existingBalance, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GrantOrAdjustBalance_OpeningBalance_SetsCorrectAccruedDays()
    {
        // Arrange
        var emp = CreateEmployee("EMP005", new DateTime(2025, 1, 1));
        var leaveType = CreateAnnualLeaveType();

        _employeeRepoMock.Setup(r => r.GetEmployeeByCodeAsync("EMP005", It.IsAny<CancellationToken>()))
            .ReturnsAsync(emp);
        _leaveRepoMock.Setup(r => r.GetLeaveTypeByCodeAsync("ANNUAL", It.IsAny<CancellationToken>()))
            .ReturnsAsync(leaveType);
        _leaveRepoMock.Setup(r => r.GetLeaveBalanceAsync("EMP005", "ANNUAL", 2026, It.IsAny<CancellationToken>()))
            .ReturnsAsync((LeaveBalance?)null);

        LeaveBalance? created = null;
        _leaveRepoMock.Setup(r => r.AddLeaveBalanceAsync(It.IsAny<LeaveBalance>(), It.IsAny<CancellationToken>()))
            .Callback<LeaveBalance, CancellationToken>((b, _) => created = b)
            .Returns(Task.CompletedTask);

        // Act
        var dto = new GrantLeaveBalanceDto("EMP005", "ANNUAL", 2026, 10.0m, "OPENING_BALANCE", "Opening balance on migration");
        var result = await _service.GrantOrAdjustBalanceAsync(dto, "ADMIN");

        // Assert
        Assert.NotNull(created);
        Assert.Equal(10.0m, created!.AccruedDays);
        Assert.Equal(10.0m, result.AccruedDays);
        Assert.Equal(10.0m, result.RemainingDays);
    }

    [Fact]
    public async Task GrantOrAdjustBalance_ManualAdjustment_AddsDays()
    {
        // Arrange
        var emp = CreateEmployee("EMP006", new DateTime(2025, 1, 1));
        var leaveType = CreateAnnualLeaveType();

        var existing = new LeaveBalance
        {
            Id = 55,
            EmployeeCode = "EMP006",
            LeaveTypeCode = "ANNUAL",
            YearNo = 2026,
            AccruedDays = 5.0m,
            UsedDays = 2.0m,
            CarriedForwardDays = 0m
        };

        _employeeRepoMock.Setup(r => r.GetEmployeeByCodeAsync("EMP006", It.IsAny<CancellationToken>()))
            .ReturnsAsync(emp);
        _leaveRepoMock.Setup(r => r.GetLeaveTypeByCodeAsync("ANNUAL", It.IsAny<CancellationToken>()))
            .ReturnsAsync(leaveType);
        _leaveRepoMock.Setup(r => r.GetLeaveBalanceAsync("EMP006", "ANNUAL", 2026, It.IsAny<CancellationToken>()))
            .ReturnsAsync(existing);

        // Act: Add 2 compensatory days
        var dto = new GrantLeaveBalanceDto("EMP006", "ANNUAL", 2026, 2.0m, "ADJUSTMENT", "Compensatory overtime grant");
        var result = await _service.GrantOrAdjustBalanceAsync(dto, "ADMIN");

        // Assert
        Assert.Equal(7.0m, existing.AccruedDays);
        Assert.Equal(7.0m, result.AccruedDays);
        Assert.Equal(5.0m, result.RemainingDays); // (7 + 0) - 2 = 5
        _leaveRepoMock.Verify(r => r.UpdateLeaveBalanceAsync(existing, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task YearlyAllocation_GrantsUpfrontDaysBasedOnTenure()
    {
        // Arrange: 1 junior (14 days) and 1 senior (21 days)
        var junior = CreateEmployee("EMP_JUNIOR", new DateTime(2024, 1, 1));
        var senior = CreateEmployee("EMP_SENIOR", new DateTime(2018, 1, 1));
        var leaveType = CreateAnnualLeaveType();
        var policy = CreateAnnualLeavePolicy();

        _employeeRepoMock.Setup(r => r.GetAllEmployeesForExportAsync(null, null, "ACTIVE", null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Employee> { junior, senior });
        _leaveRepoMock.Setup(r => r.GetLeaveTypeByCodeAsync("ANNUAL", It.IsAny<CancellationToken>()))
            .ReturnsAsync(leaveType);
        _leaveRepoMock.Setup(r => r.GetLeavePolicyByTypeCodeAsync("ANNUAL", It.IsAny<CancellationToken>()))
            .ReturnsAsync(policy);

        var savedBalances = new List<LeaveBalance>();
        _leaveRepoMock.Setup(r => r.GetLeaveBalanceAsync(It.IsAny<string>(), "ANNUAL", 2026, It.IsAny<CancellationToken>()))
            .ReturnsAsync((LeaveBalance?)null);
        _leaveRepoMock.Setup(r => r.AddLeaveBalanceAsync(It.IsAny<LeaveBalance>(), It.IsAny<CancellationToken>()))
            .Callback<LeaveBalance, CancellationToken>((b, _) => savedBalances.Add(b))
            .Returns(Task.CompletedTask);

        // Act
        var dto = new YearlyLeaveAllocationDto(2026, "ANNUAL", null, null, false);
        var result = await _service.RunYearlyAllocationAsync(dto, "ADMIN");

        // Assert
        Assert.Equal(2, result.TotalEmployeesProcessed);
        Assert.Equal(2, savedBalances.Count);

        var juniorBal = savedBalances.First(b => b.EmployeeCode == "EMP_JUNIOR");
        var seniorBal = savedBalances.First(b => b.EmployeeCode == "EMP_SENIOR");

        Assert.Equal(14.0m, juniorBal.AccruedDays);
        Assert.Equal(21.0m, seniorBal.AccruedDays);
    }

    [Fact]
    public async Task YearEndRollover_TransfersUnusedBalanceUpToCap()
    {
        // Arrange:
        // Employee has Year 2025 balance: Accrued = 14, Used = 4 -> Remaining = 10 days
        // Policy allows CarryForwardCapDays = 7 days.
        // Therefore, 7 days should rollover into 2026, and 3 days expire.
        var emp = CreateEmployee("EMP007", new DateTime(2023, 1, 1));
        var leaveType = CreateAnnualLeaveType();
        var policy = CreateAnnualLeavePolicy();

        var bal2025 = new LeaveBalance
        {
            Id = 101,
            EmployeeCode = "EMP007",
            LeaveTypeCode = "ANNUAL",
            YearNo = 2025,
            AccruedDays = 14.0m,
            UsedDays = 4.0m,
            CarriedForwardDays = 0m
        };

        _employeeRepoMock.Setup(r => r.GetEmployeeByCodeAsync("EMP007", It.IsAny<CancellationToken>()))
            .ReturnsAsync(emp);
        _leaveRepoMock.Setup(r => r.GetLeaveTypeByCodeAsync("ANNUAL", It.IsAny<CancellationToken>()))
            .ReturnsAsync(leaveType);
        _leaveRepoMock.Setup(r => r.GetLeavePolicyByTypeCodeAsync("ANNUAL", It.IsAny<CancellationToken>()))
            .ReturnsAsync(policy);
        _leaveRepoMock.Setup(r => r.GetLeaveBalancesByYearAsync(2025, "ANNUAL", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<LeaveBalance> { bal2025 });

        LeaveBalance? bal2026 = null;
        _leaveRepoMock.Setup(r => r.GetLeaveBalanceAsync("EMP007", "ANNUAL", 2026, It.IsAny<CancellationToken>()))
            .ReturnsAsync((LeaveBalance?)null);
        _leaveRepoMock.Setup(r => r.AddLeaveBalanceAsync(It.IsAny<LeaveBalance>(), It.IsAny<CancellationToken>()))
            .Callback<LeaveBalance, CancellationToken>((b, _) => bal2026 = b)
            .Returns(Task.CompletedTask);

        // Act
        var dto = new YearEndRolloverDto(2025, 2026, "ANNUAL");
        var result = await _service.RunYearEndRolloverAsync(dto, "ADMIN");

        // Assert
        Assert.Equal(1, result.TotalEmployeesProcessed);
        Assert.Equal(7.0m, result.TotalDaysCarriedForward);
        Assert.Equal(3.0m, result.TotalDaysExpired);
        Assert.NotNull(bal2026);
        Assert.Equal(7.0m, bal2026!.CarriedForwardDays);
        Assert.Equal(0m, bal2026.AccruedDays);
        Assert.Equal(2026, bal2026.YearNo);
    }
}
