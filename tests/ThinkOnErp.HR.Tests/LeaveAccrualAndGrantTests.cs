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
        _leaveRepoMock.Setup(r => r.GetHrLeaveSettingsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new HrLeaveSettings { LeaveYearStartMonth = 1, LeaveYearStartDay = 1 });
        _leaveRepoMock.Setup(r => r.ExecuteForLeaveEmployeeAsync(It.IsAny<string>(), It.IsAny<Func<Task<LeaveBalanceDto>>>(), It.IsAny<CancellationToken>()))
            .Returns((string code, Func<Task<LeaveBalanceDto>> action, CancellationToken token) => action());
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
            TracksBalance = true,
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
            AccrualMethod = "MONTHLY",
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
    public async Task MonthlyAccrual_JuniorEmployee_CatchesUpThroughRequestedMonth()
    {
        // Arrange: hired 2 years ago -> Tier 1 (14 days / yr = 1.17 days/mo)
        var emp = CreateEmployee("EMP001", new DateTime(2024, 1, 1));
        var leaveType = CreateAnnualLeaveType();
        var policy = CreateAnnualLeavePolicy();

        _employeeRepoMock.Setup(r => r.GetEmployeeByCodeAsync("EMP001", It.IsAny<CancellationToken>()))
            .ReturnsAsync(emp);
        _leaveRepoMock.Setup(r => r.GetLeaveTypeByCodeAsync("ANNUAL", It.IsAny<CancellationToken>()))
            .ReturnsAsync(leaveType);
        _leaveRepoMock.Setup(r => r.GetLeavePolicyByTypeCodeAsync("ANNUAL", It.IsAny<CancellationToken>(), It.IsAny<DateTime?>()))
            .ReturnsAsync(policy);
        _leaveRepoMock.Setup(r => r.GetLeaveBalanceAsync("EMP001", "ANNUAL", 2026, It.IsAny<CancellationToken>()))
            .ReturnsAsync((LeaveBalance?)null);

        LeaveBalance? savedBalance = null;
        _leaveRepoMock.Setup(r => r.ApplyAccrualTargetAsync(It.IsAny<LeaveBalance>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Callback<LeaveBalance, int, int, string, CancellationToken>((b, _, _, _, _) => savedBalance = b)
            .Returns((LeaveBalance b, int _, int _, string _, CancellationToken _) => Task.FromResult(b.AccruedDays));

        // Act
        var dto = new MonthlyLeaveAccrualDto(2026, 3, "ANNUAL", "EMP001");
        var result = await _service.RunMonthlyAccrualAsync(dto, "TEST_USER");

        // Assert
        Assert.Equal(1, result.TotalEmployeesProcessed);
        Assert.NotNull(savedBalance);
        Assert.Equal(3.5m, savedBalance!.AccruedDays);
        Assert.Equal(2026, savedBalance.YearNo);
        Assert.Equal("EMP001", savedBalance.EmployeeCode);
    }

    [Fact]
    public async Task MonthlyAccrual_SeniorEmployee_CatchesUpThroughRequestedMonth()
    {
        // Arrange: hired 7 years ago -> Tier 2 (21 days / yr = 1.75 days/mo)
        var emp = CreateEmployee("EMP002", new DateTime(2019, 1, 1));
        var leaveType = CreateAnnualLeaveType();
        var policy = CreateAnnualLeavePolicy();

        _employeeRepoMock.Setup(r => r.GetEmployeeByCodeAsync("EMP002", It.IsAny<CancellationToken>()))
            .ReturnsAsync(emp);
        _leaveRepoMock.Setup(r => r.GetLeaveTypeByCodeAsync("ANNUAL", It.IsAny<CancellationToken>()))
            .ReturnsAsync(leaveType);
        _leaveRepoMock.Setup(r => r.GetLeavePolicyByTypeCodeAsync("ANNUAL", It.IsAny<CancellationToken>(), It.IsAny<DateTime?>()))
            .ReturnsAsync(policy);
        _leaveRepoMock.Setup(r => r.GetLeaveBalanceAsync("EMP002", "ANNUAL", 2026, It.IsAny<CancellationToken>()))
            .ReturnsAsync((LeaveBalance?)null);

        LeaveBalance? savedBalance = null;
        _leaveRepoMock.Setup(r => r.ApplyAccrualTargetAsync(It.IsAny<LeaveBalance>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Callback<LeaveBalance, int, int, string, CancellationToken>((b, _, _, _, _) => savedBalance = b)
            .Returns((LeaveBalance b, int _, int _, string _, CancellationToken _) => Task.FromResult(b.AccruedDays));

        // Act
        var dto = new MonthlyLeaveAccrualDto(2026, 3, "ANNUAL", "EMP002");
        var result = await _service.RunMonthlyAccrualAsync(dto, "TEST_USER");

        // Assert
        Assert.Equal(1, result.TotalEmployeesProcessed);
        Assert.NotNull(savedBalance);
        Assert.Equal(5.25m, savedBalance!.AccruedDays);
    }

    [Fact]
    public async Task MonthlyAccrual_MidMonthHire_ProratesCorrectly()
    {
        // Arrange: hired on June 16, 2026 in a 30-day month -> 15 days active (June 16..30)
        // Rate = 14 / 12 = 1.1667 days/mo. Prorated = 1.1667 * (15 / 30) = 0.5833.
        var emp = CreateEmployee("EMP003", new DateTime(2026, 6, 16));
        var leaveType = CreateAnnualLeaveType();
        var policy = CreateAnnualLeavePolicy();

        _employeeRepoMock.Setup(r => r.GetEmployeeByCodeAsync("EMP003", It.IsAny<CancellationToken>()))
            .ReturnsAsync(emp);
        _leaveRepoMock.Setup(r => r.GetLeaveTypeByCodeAsync("ANNUAL", It.IsAny<CancellationToken>()))
            .ReturnsAsync(leaveType);
        _leaveRepoMock.Setup(r => r.GetLeavePolicyByTypeCodeAsync("ANNUAL", It.IsAny<CancellationToken>(), It.IsAny<DateTime?>()))
            .ReturnsAsync(policy);
        _leaveRepoMock.Setup(r => r.GetLeaveBalanceAsync("EMP003", "ANNUAL", 2026, It.IsAny<CancellationToken>()))
            .ReturnsAsync((LeaveBalance?)null);

        LeaveBalance? savedBalance = null;
        _leaveRepoMock.Setup(r => r.ApplyAccrualTargetAsync(It.IsAny<LeaveBalance>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Callback<LeaveBalance, int, int, string, CancellationToken>((b, _, _, _, _) => savedBalance = b)
            .Returns((LeaveBalance b, int _, int _, string _, CancellationToken _) => Task.FromResult(b.AccruedDays));

        // Act
        var dto = new MonthlyLeaveAccrualDto(2026, 6, "ANNUAL", "EMP003");
        var result = await _service.RunMonthlyAccrualAsync(dto, "TEST_USER");

        // Assert
        Assert.Equal(1, result.TotalEmployeesProcessed);
        Assert.NotNull(savedBalance);
        Assert.Equal(0.5833m, savedBalance!.AccruedDays);
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
        _leaveRepoMock.Setup(r => r.GetLeavePolicyByTypeCodeAsync("ANNUAL", It.IsAny<CancellationToken>(), It.IsAny<DateTime?>()))
            .ReturnsAsync(policy);
        _leaveRepoMock.Setup(r => r.GetLeaveBalanceAsync("EMP004", "ANNUAL", 2026, It.IsAny<CancellationToken>()))
            .ReturnsAsync(existingBalance);
        LeaveBalance? target = null;
        _leaveRepoMock.Setup(r => r.ApplyAccrualTargetAsync(It.IsAny<LeaveBalance>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Callback<LeaveBalance, int, int, string, CancellationToken>((b, _, _, _, _) => target = b)
            .ReturnsAsync(0.5m);

        // Act
        var dto = new MonthlyLeaveAccrualDto(2026, 12, "ANNUAL", "EMP004");
        var result = await _service.RunMonthlyAccrualAsync(dto, "TEST_USER");

        // Assert
        Assert.Equal(1, result.TotalEmployeesProcessed);
        Assert.Equal(0.5m, result.TotalDaysAccrued);
        Assert.Equal(14m, target!.AccruedDays);
        Assert.Equal(13.5m, existingBalance.AccruedDays); // repository owns applying the target atomically
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
        _leaveRepoMock.Setup(r => r.GetLeaveBalanceByCycleAsync("EMP005", "ANNUAL", new DateTime(2026, 1, 1), It.IsAny<CancellationToken>()))
            .ReturnsAsync((LeaveBalance?)null);

        LeaveBalance? created = null;
        _leaveRepoMock.Setup(r => r.AddLeaveBalanceAsync(It.IsAny<LeaveBalance>(), It.IsAny<CancellationToken>()))
            .Callback<LeaveBalance, CancellationToken>((b, _) => created = b)
            .Returns(Task.CompletedTask);

        // Act
        var dto = new GrantLeaveBalanceDto("EMP005", "ANNUAL", 2026, 10.0m, "OPENING_BALANCE", "Opening balance on migration", "opening-EMP005-ANNUAL-2026");
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
        _leaveRepoMock.Setup(r => r.GetLeaveBalanceByCycleAsync("EMP006", "ANNUAL", new DateTime(2026, 1, 1), It.IsAny<CancellationToken>()))
            .ReturnsAsync(existing);

        // Act: Add 2 compensatory days
        var dto = new GrantLeaveBalanceDto("EMP006", "ANNUAL", 2026, 2.0m, "COMPENSATORY", "Compensatory overtime grant", "comp-EMP006-ANNUAL-2026");
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
        _leaveRepoMock.Setup(r => r.GetLeavePolicyByTypeCodeAsync("ANNUAL", It.IsAny<CancellationToken>(), It.IsAny<DateTime?>()))
            .ReturnsAsync(policy);

        var savedBalances = new List<LeaveBalance>();
        _leaveRepoMock.Setup(r => r.GetLeaveBalanceAsync(It.IsAny<string>(), "ANNUAL", 2026, It.IsAny<CancellationToken>()))
            .ReturnsAsync((LeaveBalance?)null);
        policy.AccrualMethod = "YEARLY";
        _leaveRepoMock.Setup(r => r.ApplyAccrualTargetAsync(It.IsAny<LeaveBalance>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Callback<LeaveBalance, int, int, string, CancellationToken>((b, _, _, _, _) => savedBalances.Add(b))
            .Returns((LeaveBalance b, int _, int _, string _, CancellationToken _) => Task.FromResult(b.AccruedDays));

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
        _leaveRepoMock.Setup(r => r.GetLeavePolicyByTypeCodeAsync("ANNUAL", It.IsAny<CancellationToken>(), It.IsAny<DateTime?>()))
            .ReturnsAsync(policy);
        _leaveRepoMock.Setup(r => r.GetLeaveBalancesByYearAsync(2025, "ANNUAL", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<LeaveBalance> { bal2025 });

        _leaveRepoMock.Setup(r => r.GetLeaveBalanceAsync("EMP007", "ANNUAL", 2026, It.IsAny<CancellationToken>()))
            .ReturnsAsync((LeaveBalance?)null);
        LeaveYearClose? savedClose = null;
        _leaveRepoMock.Setup(r => r.SaveLeaveYearCloseDraftAsync(It.IsAny<LeaveYearClose>(), It.IsAny<CancellationToken>()))
            .Returns((LeaveYearClose close, CancellationToken _) =>
            {
                close.Id = 91;
                savedClose = close;
                return Task.FromResult(close);
            });
        _leaveRepoMock.Setup(r => r.PostLeaveYearCloseAsync(91, "ADMIN", It.IsAny<CancellationToken>()))
            .Returns(() =>
            {
                savedClose!.Status = "POSTED";
                return Task.FromResult<LeaveYearClose?>(savedClose);
            });

        // Act
        var dto = new YearEndRolloverDto(2025, 2026, "ANNUAL");
        var result = await _service.RunYearEndRolloverAsync(dto, "ADMIN");

        // Assert
        Assert.Equal(1, result.TotalEmployeesProcessed);
        Assert.Equal(7.0m, result.TotalDaysCarriedForward);
        Assert.Equal(3.0m, result.TotalDaysExpired);
        var line = Assert.Single(savedClose!.Lines);
        Assert.Equal(7.0m, line.CarriedDays);
        Assert.Equal(3.0m, line.ExpiredDays);
        Assert.Equal(new DateTime(2026, 1, 1), line.TargetCycleStartDate);
        _leaveRepoMock.Verify(r => r.PostLeaveYearCloseAsync(91, "ADMIN", It.IsAny<CancellationToken>()), Times.Once);
    }

    private static LeavePolicy CreateDailyLeavePolicy(int minServiceMonths = 0)
    {
        return new LeavePolicy
        {
            Id = 2,
            PolicyName = "سياسة التراكم اليومي",
            LeaveTypeCode = "ANNUAL",
            AccrualMethod = "DAILY",
            CycleBasis = "CALENDAR_YEAR",
            MinServiceMonths = minServiceMonths,
            Tier1YearsThreshold = 5,
            Tier1Days = 14,
            Tier2Days = 21,
            IsActive = true
        };
    }

    private static LeavePolicy CreateYearlyLeavePolicy(int minServiceMonths = 0)
    {
        return new LeavePolicy
        {
            Id = 3,
            PolicyName = "سياسة المنح السنوي",
            LeaveTypeCode = "ANNUAL",
            AccrualMethod = "YEARLY",
            CycleBasis = "CALENDAR_YEAR",
            MinServiceMonths = minServiceMonths,
            Tier1YearsThreshold = 5,
            Tier1Days = 14,
            Tier2Days = 21,
            IsActive = true
        };
    }

    [Fact]
    public async Task RunDailyAccrualAsync_WhenAsOfDateIsDefault_ThrowsArgumentException()
    {
        var dto = new DailyLeaveAccrualDto(default, "ANNUAL", null, "EMP001");
        var ex = await Assert.ThrowsAsync<ArgumentException>(() => _service.RunDailyAccrualAsync(dto, "ADMIN"));
        Assert.Contains("An accrual date is required", ex.Message);
    }

    [Fact]
    public async Task RunDailyAccrualAsync_WhenAsOfDateIsInFuture_ThrowsArgumentException()
    {
        var dto = new DailyLeaveAccrualDto(DateTime.UtcNow.Date.AddDays(5), "ANNUAL", null, "EMP001");
        var ex = await Assert.ThrowsAsync<ArgumentException>(() => _service.RunDailyAccrualAsync(dto, "ADMIN"));
        Assert.Contains("future company-local date", ex.Message);
    }

    [Fact]
    public async Task RunDailyAccrualAsync_WhenLeaveTypeNotFound_ThrowsInvalidOperationException()
    {
        _leaveRepoMock.Setup(r => r.GetLeaveTypeByCodeAsync("UNKNOWN", It.IsAny<CancellationToken>()))
            .ReturnsAsync((LeaveType?)null);

        var dto = new DailyLeaveAccrualDto(DateTime.UtcNow.Date.AddDays(-1), "UNKNOWN", null, "EMP001");
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => _service.RunDailyAccrualAsync(dto, "ADMIN"));
        Assert.Contains("Leave type 'UNKNOWN' not found", ex.Message);
    }

    [Fact]
    public async Task RunDailyAccrualAsync_WhenLeaveTypeInactive_ThrowsInvalidOperationException()
    {
        var inactiveType = CreateAnnualLeaveType();
        inactiveType.IsActive = false;
        _leaveRepoMock.Setup(r => r.GetLeaveTypeByCodeAsync("ANNUAL", It.IsAny<CancellationToken>()))
            .ReturnsAsync(inactiveType);

        var dto = new DailyLeaveAccrualDto(DateTime.UtcNow.Date.AddDays(-1), "ANNUAL", null, "EMP001");
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => _service.RunDailyAccrualAsync(dto, "ADMIN"));
        Assert.Contains("is inactive", ex.Message);
    }

    [Fact]
    public async Task RunDailyAccrualAsync_WhenLeaveTypeDoesNotTrackBalance_ThrowsInvalidOperationException()
    {
        var unmeteredType = CreateAnnualLeaveType();
        unmeteredType.TracksBalance = false;
        _leaveRepoMock.Setup(r => r.GetLeaveTypeByCodeAsync("ANNUAL", It.IsAny<CancellationToken>()))
            .ReturnsAsync(unmeteredType);

        var dto = new DailyLeaveAccrualDto(DateTime.UtcNow.Date.AddDays(-1), "ANNUAL", null, "EMP001");
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => _service.RunDailyAccrualAsync(dto, "ADMIN"));
        Assert.Contains("does not track a balance", ex.Message);
    }

    [Fact]
    public async Task RunDailyAccrualAsync_WhenEmployeeNotEligibleByMinServiceMonths_SkipsAccrual()
    {
        // Arrange: Hired 1 month ago, policy requires 3 months
        var asOfDate = new DateTime(2026, 3, 15);
        var emp = CreateEmployee("EMP008", new DateTime(2026, 2, 15));
        var leaveType = CreateAnnualLeaveType();
        var policy = CreateDailyLeavePolicy(minServiceMonths: 3);

        _employeeRepoMock.Setup(r => r.GetEmployeeByCodeAsync("EMP008", It.IsAny<CancellationToken>()))
            .ReturnsAsync(emp);
        _leaveRepoMock.Setup(r => r.GetLeaveTypeByCodeAsync("ANNUAL", It.IsAny<CancellationToken>()))
            .ReturnsAsync(leaveType);
        _leaveRepoMock.Setup(r => r.GetLeavePolicyByTypeCodeAsync("ANNUAL", It.IsAny<CancellationToken>(), It.IsAny<DateTime?>()))
            .ReturnsAsync(policy);

        var dto = new DailyLeaveAccrualDto(asOfDate, "ANNUAL", null, "EMP008");
        var result = await _service.RunDailyAccrualAsync(dto, "ADMIN");

        Assert.Equal(1, result.TotalEmployeesProcessed);
        Assert.Equal(0, result.UpdatedBalancesCount);
        Assert.Equal(0m, result.TotalDaysAccrued);
        _leaveRepoMock.Verify(r => r.ApplyAccrualTargetAsync(It.IsAny<LeaveBalance>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task RunDailyAccrualAsync_WhenPolicyMethodIsNotDaily_SkipsAccrual()
    {
        var asOfDate = new DateTime(2026, 3, 15);
        var emp = CreateEmployee("EMP009", new DateTime(2024, 1, 1));
        var leaveType = CreateAnnualLeaveType();
        var policy = CreateAnnualLeavePolicy(); // MONTHLY accrual method

        _employeeRepoMock.Setup(r => r.GetEmployeeByCodeAsync("EMP009", It.IsAny<CancellationToken>()))
            .ReturnsAsync(emp);
        _leaveRepoMock.Setup(r => r.GetLeaveTypeByCodeAsync("ANNUAL", It.IsAny<CancellationToken>()))
            .ReturnsAsync(leaveType);
        _leaveRepoMock.Setup(r => r.GetLeavePolicyByTypeCodeAsync("ANNUAL", It.IsAny<CancellationToken>(), It.IsAny<DateTime?>()))
            .ReturnsAsync(policy);

        var dto = new DailyLeaveAccrualDto(asOfDate, "ANNUAL", null, "EMP009");
        var result = await _service.RunDailyAccrualAsync(dto, "ADMIN");

        Assert.Equal(1, result.TotalEmployeesProcessed);
        Assert.Equal(0, result.UpdatedBalancesCount);
        Assert.Equal(0m, result.TotalDaysAccrued);
        _leaveRepoMock.Verify(r => r.ApplyAccrualTargetAsync(It.IsAny<LeaveBalance>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task RunDailyAccrualAsync_HappyPath_AccruesProRataCalendarDaysToDate()
    {
        // 2026 is not leap year (365 days).
        // 14 days annual quota.
        // Hired 2024-01-01 -> completed 2 years -> 14 days.
        // As of 2026-06-30: (181 days from Jan 1) -> 14 * 181 / 365 = 6.9425 days
        var asOfDate = new DateTime(2026, 6, 30);
        var emp = CreateEmployee("EMP010", new DateTime(2024, 1, 1));
        var leaveType = CreateAnnualLeaveType();
        var policy = CreateDailyLeavePolicy();

        _employeeRepoMock.Setup(r => r.GetEmployeeByCodeAsync("EMP010", It.IsAny<CancellationToken>()))
            .ReturnsAsync(emp);
        _leaveRepoMock.Setup(r => r.GetLeaveTypeByCodeAsync("ANNUAL", It.IsAny<CancellationToken>()))
            .ReturnsAsync(leaveType);
        _leaveRepoMock.Setup(r => r.GetLeavePolicyByTypeCodeAsync("ANNUAL", It.IsAny<CancellationToken>(), It.IsAny<DateTime?>()))
            .ReturnsAsync(policy);

        LeaveBalance? appliedBalance = null;
        _leaveRepoMock.Setup(r => r.ApplyAccrualTargetAsync(It.IsAny<LeaveBalance>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Callback<LeaveBalance, int, int, string, CancellationToken>((b, _, _, _, _) => appliedBalance = b)
            .ReturnsAsync(6.9425m);

        var dto = new DailyLeaveAccrualDto(asOfDate, "ANNUAL", null, "EMP010");
        var result = await _service.RunDailyAccrualAsync(dto, "ADMIN");

        Assert.Equal(1, result.TotalEmployeesProcessed);
        Assert.Equal(1, result.UpdatedBalancesCount);
        Assert.Equal(6.9425m, result.TotalDaysAccrued);
        Assert.NotNull(appliedBalance);
        Assert.Equal(6.9425m, appliedBalance!.AccruedDays);
        _leaveRepoMock.Verify(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task RunDailyAccrualAsync_Idempotent_SecondRunReturnsZeroAdded()
    {
        var asOfDate = new DateTime(2026, 6, 30);
        var emp = CreateEmployee("EMP011", new DateTime(2024, 1, 1));
        var leaveType = CreateAnnualLeaveType();
        var policy = CreateDailyLeavePolicy();

        _employeeRepoMock.Setup(r => r.GetEmployeeByCodeAsync("EMP011", It.IsAny<CancellationToken>()))
            .ReturnsAsync(emp);
        _leaveRepoMock.Setup(r => r.GetLeaveTypeByCodeAsync("ANNUAL", It.IsAny<CancellationToken>()))
            .ReturnsAsync(leaveType);
        _leaveRepoMock.Setup(r => r.GetLeavePolicyByTypeCodeAsync("ANNUAL", It.IsAny<CancellationToken>(), It.IsAny<DateTime?>()))
            .ReturnsAsync(policy);

        // When balance is already at or above target, ApplyAccrualTargetAsync returns 0m
        _leaveRepoMock.Setup(r => r.ApplyAccrualTargetAsync(It.IsAny<LeaveBalance>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(0m);

        var dto = new DailyLeaveAccrualDto(asOfDate, "ANNUAL", null, "EMP011");
        var result = await _service.RunDailyAccrualAsync(dto, "ADMIN");

        Assert.Equal(1, result.TotalEmployeesProcessed);
        Assert.Equal(0, result.UpdatedBalancesCount);
        Assert.Equal(0m, result.TotalDaysAccrued);
    }

    [Fact]
    public async Task RunYearlyAllocationAsync_WhenAsOfDateIsInFutureUtc_ThrowsArgumentException()
    {
        var dto = new YearlyLeaveAllocationDto(2026, "ANNUAL", null, "EMP012", false, AsOfDate: DateTime.UtcNow.Date.AddDays(5));
        var ex = await Assert.ThrowsAsync<ArgumentException>(() => _service.RunYearlyAllocationAsync(dto, "ADMIN"));
        Assert.Contains("future company-local date", ex.Message);
    }

    [Fact]
    public async Task RunYearlyAllocationAsync_WhenLeaveTypeNotFound_ThrowsInvalidOperationException()
    {
        _leaveRepoMock.Setup(r => r.GetLeaveTypeByCodeAsync("UNKNOWN", It.IsAny<CancellationToken>()))
            .ReturnsAsync((LeaveType?)null);

        var dto = new YearlyLeaveAllocationDto(2026, "UNKNOWN", null, "EMP012", false);
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => _service.RunYearlyAllocationAsync(dto, "ADMIN"));
        Assert.Contains("Leave type 'UNKNOWN' not found", ex.Message);
    }

    [Fact]
    public async Task RunYearlyAllocationAsync_WhenLeaveTypeInactive_ThrowsInvalidOperationException()
    {
        var inactiveType = CreateAnnualLeaveType();
        inactiveType.IsActive = false;
        _leaveRepoMock.Setup(r => r.GetLeaveTypeByCodeAsync("ANNUAL", It.IsAny<CancellationToken>()))
            .ReturnsAsync(inactiveType);

        var dto = new YearlyLeaveAllocationDto(2026, "ANNUAL", null, "EMP012", false);
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => _service.RunYearlyAllocationAsync(dto, "ADMIN"));
        Assert.Contains("is inactive", ex.Message);
    }

    [Fact]
    public async Task RunYearlyAllocationAsync_WhenLeaveTypeDoesNotTrackBalance_ThrowsInvalidOperationException()
    {
        var unmetered = CreateAnnualLeaveType();
        unmetered.TracksBalance = false;
        _leaveRepoMock.Setup(r => r.GetLeaveTypeByCodeAsync("ANNUAL", It.IsAny<CancellationToken>()))
            .ReturnsAsync(unmetered);

        var dto = new YearlyLeaveAllocationDto(2026, "ANNUAL", null, "EMP012", false);
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => _service.RunYearlyAllocationAsync(dto, "ADMIN"));
        Assert.Contains("does not track a balance", ex.Message);
    }

    [Fact]
    public async Task RunYearlyAllocationAsync_WhenPolicyMethodIsNotYearly_SkipsAllocation()
    {
        var emp = CreateEmployee("EMP013", new DateTime(2024, 1, 1));
        var leaveType = CreateAnnualLeaveType();
        var policy = CreateDailyLeavePolicy(); // DAILY, not YEARLY

        _employeeRepoMock.Setup(r => r.GetEmployeeByCodeAsync("EMP013", It.IsAny<CancellationToken>()))
            .ReturnsAsync(emp);
        _leaveRepoMock.Setup(r => r.GetLeaveTypeByCodeAsync("ANNUAL", It.IsAny<CancellationToken>()))
            .ReturnsAsync(leaveType);
        _leaveRepoMock.Setup(r => r.GetLeavePolicyByTypeCodeAsync("ANNUAL", It.IsAny<CancellationToken>(), It.IsAny<DateTime?>()))
            .ReturnsAsync(policy);

        var dto = new YearlyLeaveAllocationDto(2026, "ANNUAL", null, "EMP013", false);
        var result = await _service.RunYearlyAllocationAsync(dto, "ADMIN");

        Assert.Equal(1, result.TotalEmployeesProcessed);
        Assert.Equal(0, result.UpdatedBalancesCount);
        Assert.Equal(0m, result.TotalDaysAccrued);
        _leaveRepoMock.Verify(r => r.ApplyAccrualTargetAsync(It.IsAny<LeaveBalance>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task RunYearlyAllocationAsync_WhenEmployeeNotEligibleByServiceMonths_SkipsAllocation()
    {
        // Cycle is 2026 (2026-01-01 to 2026-12-31). Employee hired in 2026-11-01 with 6 months min service.
        var emp = CreateEmployee("EMP014", new DateTime(2026, 11, 1));
        var leaveType = CreateAnnualLeaveType();
        var policy = CreateYearlyLeavePolicy(minServiceMonths: 6); // Eligible May 2027 > 2026-12-31

        _employeeRepoMock.Setup(r => r.GetEmployeeByCodeAsync("EMP014", It.IsAny<CancellationToken>()))
            .ReturnsAsync(emp);
        _leaveRepoMock.Setup(r => r.GetLeaveTypeByCodeAsync("ANNUAL", It.IsAny<CancellationToken>()))
            .ReturnsAsync(leaveType);
        _leaveRepoMock.Setup(r => r.GetLeavePolicyByTypeCodeAsync("ANNUAL", It.IsAny<CancellationToken>(), It.IsAny<DateTime?>()))
            .ReturnsAsync(policy);

        var dto = new YearlyLeaveAllocationDto(2026, "ANNUAL", null, "EMP014", false);
        var result = await _service.RunYearlyAllocationAsync(dto, "ADMIN");

        Assert.Equal(1, result.TotalEmployeesProcessed);
        Assert.Equal(0, result.UpdatedBalancesCount);
        Assert.Equal(0m, result.TotalDaysAccrued);
    }

    [Fact]
    public async Task RunYearlyAllocationAsync_HappyPath_AllocatesFullAnnualQuota()
    {
        var emp = CreateEmployee("EMP015", new DateTime(2024, 1, 1));
        var leaveType = CreateAnnualLeaveType();
        var policy = CreateYearlyLeavePolicy();

        _employeeRepoMock.Setup(r => r.GetEmployeeByCodeAsync("EMP015", It.IsAny<CancellationToken>()))
            .ReturnsAsync(emp);
        _leaveRepoMock.Setup(r => r.GetLeaveTypeByCodeAsync("ANNUAL", It.IsAny<CancellationToken>()))
            .ReturnsAsync(leaveType);
        _leaveRepoMock.Setup(r => r.GetLeavePolicyByTypeCodeAsync("ANNUAL", It.IsAny<CancellationToken>(), It.IsAny<DateTime?>()))
            .ReturnsAsync(policy);

        LeaveBalance? appliedBalance = null;
        _leaveRepoMock.Setup(r => r.ApplyAccrualTargetAsync(It.IsAny<LeaveBalance>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Callback<LeaveBalance, int, int, string, CancellationToken>((b, _, _, _, _) => appliedBalance = b)
            .ReturnsAsync(14.0m);

        var dto = new YearlyLeaveAllocationDto(2026, "ANNUAL", null, "EMP015", false);
        var result = await _service.RunYearlyAllocationAsync(dto, "ADMIN");

        Assert.Equal(1, result.TotalEmployeesProcessed);
        Assert.Equal(1, result.UpdatedBalancesCount);
        Assert.Equal(14.0m, result.TotalDaysAccrued);
        Assert.NotNull(appliedBalance);
        Assert.Equal(14.0m, appliedBalance!.AccruedDays);
        Assert.Equal(2026, appliedBalance.YearNo);
        _leaveRepoMock.Verify(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task RunYearlyAllocationAsync_MidYearHire_ProratesQuotaAccurately()
    {
        // Hired July 1, 2026. Cycle is Jan 1 - Dec 31, 2026 (365 days).
        // Eligible from July 1, 2026. Days from July 1 to Dec 31 = 184 days.
        // Full quota = 14 days. Prorated quota = round(14 * 184 / 365, 4) = 7.0575 days.
        var emp = CreateEmployee("EMP016", new DateTime(2026, 7, 1));
        var leaveType = CreateAnnualLeaveType();
        var policy = CreateYearlyLeavePolicy();

        _employeeRepoMock.Setup(r => r.GetEmployeeByCodeAsync("EMP016", It.IsAny<CancellationToken>()))
            .ReturnsAsync(emp);
        _leaveRepoMock.Setup(r => r.GetLeaveTypeByCodeAsync("ANNUAL", It.IsAny<CancellationToken>()))
            .ReturnsAsync(leaveType);
        _leaveRepoMock.Setup(r => r.GetLeavePolicyByTypeCodeAsync("ANNUAL", It.IsAny<CancellationToken>(), It.IsAny<DateTime?>()))
            .ReturnsAsync(policy);

        LeaveBalance? appliedBalance = null;
        _leaveRepoMock.Setup(r => r.ApplyAccrualTargetAsync(It.IsAny<LeaveBalance>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Callback<LeaveBalance, int, int, string, CancellationToken>((b, _, _, _, _) => appliedBalance = b)
            .ReturnsAsync(7.0575m);

        var dto = new YearlyLeaveAllocationDto(2026, "ANNUAL", null, "EMP016", ProrateForMidYearHires: true);
        var result = await _service.RunYearlyAllocationAsync(dto, "ADMIN");

        Assert.Equal(1, result.TotalEmployeesProcessed);
        Assert.Equal(1, result.UpdatedBalancesCount);
        Assert.Equal(7.0575m, result.TotalDaysAccrued);
        Assert.NotNull(appliedBalance);
        Assert.Equal(7.0575m, appliedBalance!.AccruedDays);
    }

    [Fact]
    public async Task RunYearlyAllocationAsync_WhenDtoNull_ThrowsArgumentNullException()
    {
        await Assert.ThrowsAsync<ArgumentNullException>(() => _service.RunYearlyAllocationAsync(null!, "ADMIN"));
    }

    [Fact]
    public async Task RunMonthlyAccrualAsync_WhenDtoNull_ThrowsArgumentNullException()
    {
        await Assert.ThrowsAsync<ArgumentNullException>(() => _service.RunMonthlyAccrualAsync(null!, "ADMIN"));
    }

    [Fact]
    public async Task RunDailyAccrualAsync_WhenDtoNull_ThrowsArgumentNullException()
    {
        await Assert.ThrowsAsync<ArgumentNullException>(() => _service.RunDailyAccrualAsync(null!, "ADMIN"));
    }

    [Fact]
    public async Task RunMonthlyAccrualAsync_WhenLeaveTypeNotFound_ThrowsInvalidOperationException()
    {
        _leaveRepoMock.Setup(r => r.GetLeaveTypeByCodeAsync("UNKNOWN", It.IsAny<CancellationToken>()))
            .ReturnsAsync((LeaveType?)null);

        var dto = new MonthlyLeaveAccrualDto(2026, 3, "UNKNOWN", "EMP001");
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => _service.RunMonthlyAccrualAsync(dto, "ADMIN"));
        Assert.Contains("Leave type 'UNKNOWN' not found", ex.Message);
    }

    [Fact]
    public async Task RunMonthlyAccrualAsync_WhenLeaveTypeInactive_ThrowsInvalidOperationException()
    {
        var leaveType = CreateAnnualLeaveType();
        leaveType.IsActive = false;
        _leaveRepoMock.Setup(r => r.GetLeaveTypeByCodeAsync("ANNUAL", It.IsAny<CancellationToken>()))
            .ReturnsAsync(leaveType);

        var dto = new MonthlyLeaveAccrualDto(2026, 3, "ANNUAL", "EMP001");
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => _service.RunMonthlyAccrualAsync(dto, "ADMIN"));
        Assert.Contains("Leave type 'ANNUAL' is inactive", ex.Message);
    }

    [Fact]
    public async Task RunMonthlyAccrualAsync_WhenLeaveTypeDoesNotTrackBalance_ThrowsInvalidOperationException()
    {
        var leaveType = CreateAnnualLeaveType();
        leaveType.TracksBalance = false;
        _leaveRepoMock.Setup(r => r.GetLeaveTypeByCodeAsync("ANNUAL", It.IsAny<CancellationToken>()))
            .ReturnsAsync(leaveType);

        var dto = new MonthlyLeaveAccrualDto(2026, 3, "ANNUAL", "EMP001");
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => _service.RunMonthlyAccrualAsync(dto, "ADMIN"));
        Assert.Contains("Leave type 'ANNUAL' does not track a balance", ex.Message);
    }

    [Theory]
    [InlineData("TERMINATED", true)]
    [InlineData("ACTIVE", false)]
    public async Task RunYearlyAllocationAsync_WhenEmployeeInactiveOrTerminated_SkipsEmployee(string status, bool isActive)
    {
        var emp = CreateEmployee("EMP_SKIP", new DateTime(2024, 1, 1), status);
        emp.IsActive = isActive;
        var leaveType = CreateAnnualLeaveType();

        _employeeRepoMock.Setup(r => r.GetEmployeeByCodeAsync("EMP_SKIP", It.IsAny<CancellationToken>()))
            .ReturnsAsync(emp);
        _leaveRepoMock.Setup(r => r.GetLeaveTypeByCodeAsync("ANNUAL", It.IsAny<CancellationToken>()))
            .ReturnsAsync(leaveType);

        var dto = new YearlyLeaveAllocationDto(2026, "ANNUAL", null, "EMP_SKIP", false);
        var result = await _service.RunYearlyAllocationAsync(dto, "ADMIN");

        Assert.Equal(0, result.TotalEmployeesProcessed);
        Assert.Equal(0, result.UpdatedBalancesCount);
        Assert.Equal(0m, result.TotalDaysAccrued);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(13)]
    [InlineData(-1)]
    public async Task RunMonthlyAccrualAsync_WhenMonthOutOfRange_ThrowsArgumentException(int month)
    {
        var dto = new MonthlyLeaveAccrualDto(2026, month, "ANNUAL", "EMP001");
        var ex = await Assert.ThrowsAsync<ArgumentException>(() => _service.RunMonthlyAccrualAsync(dto, "ADMIN"));
        Assert.Contains("Month must be between 1 and 12", ex.Message);
    }

    [Fact]
    public async Task RunYearlyAllocationAsync_WhenAccrualTargetAlreadyMet_DoesNotIncrementBalance()
    {
        var emp = CreateEmployee("EMP017", new DateTime(2024, 1, 1));
        var leaveType = CreateAnnualLeaveType();
        var policy = CreateYearlyLeavePolicy();

        _employeeRepoMock.Setup(r => r.GetEmployeeByCodeAsync("EMP017", It.IsAny<CancellationToken>()))
            .ReturnsAsync(emp);
        _leaveRepoMock.Setup(r => r.GetLeaveTypeByCodeAsync("ANNUAL", It.IsAny<CancellationToken>()))
            .ReturnsAsync(leaveType);
        _leaveRepoMock.Setup(r => r.GetLeavePolicyByTypeCodeAsync("ANNUAL", It.IsAny<CancellationToken>(), It.IsAny<DateTime?>()))
            .ReturnsAsync(policy);

        // ApplyAccrualTargetAsync returns 0m because target has already been achieved
        _leaveRepoMock.Setup(r => r.ApplyAccrualTargetAsync(It.IsAny<LeaveBalance>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(0m);

        var dto = new YearlyLeaveAllocationDto(2026, "ANNUAL", null, "EMP017", false);
        var result = await _service.RunYearlyAllocationAsync(dto, "ADMIN");

        Assert.Equal(1, result.TotalEmployeesProcessed);
        Assert.Equal(0, result.UpdatedBalancesCount);
        Assert.Equal(0m, result.TotalDaysAccrued);
    }

    [Fact]
    public async Task RunYearlyAllocationAsync_MultiEmployeeBatch_ProcessesAllActiveEmployees()
    {
        var emp1 = CreateEmployee("EMP018", new DateTime(2024, 1, 1));
        var emp2 = CreateEmployee("EMP019", new DateTime(2023, 1, 1));
        var emp3Ineligible = CreateEmployee("EMP020", new DateTime(2026, 11, 1)); // Ineligible with min service

        var leaveType = CreateAnnualLeaveType();
        var policy = CreateYearlyLeavePolicy(minServiceMonths: 6);

        _employeeRepoMock.Setup(r => r.GetAllEmployeesForExportAsync(It.IsAny<string?>(), It.IsAny<string?>(), "ACTIVE", It.IsAny<long?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Employee> { emp1, emp2, emp3Ineligible });
        _leaveRepoMock.Setup(r => r.GetLeaveTypeByCodeAsync("ANNUAL", It.IsAny<CancellationToken>()))
            .ReturnsAsync(leaveType);
        _leaveRepoMock.Setup(r => r.GetLeavePolicyByTypeCodeAsync("ANNUAL", It.IsAny<CancellationToken>(), It.IsAny<DateTime?>()))
            .ReturnsAsync(policy);

        _leaveRepoMock.Setup(r => r.ApplyAccrualTargetAsync(It.Is<LeaveBalance>(b => b.EmployeeCode == "EMP018"), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(14m);
        _leaveRepoMock.Setup(r => r.ApplyAccrualTargetAsync(It.Is<LeaveBalance>(b => b.EmployeeCode == "EMP019"), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(14m);

        var dto = new YearlyLeaveAllocationDto(2026, "ANNUAL", null, null, false);
        var result = await _service.RunYearlyAllocationAsync(dto, "ADMIN");

        Assert.Equal(3, result.TotalEmployeesProcessed);
        Assert.Equal(2, result.UpdatedBalancesCount);
        Assert.Equal(28m, result.TotalDaysAccrued);
    }

    [Fact]
    public async Task RunDailyAccrualAsync_PropagatesCancellationToken()
    {
        using var cts = new CancellationTokenSource();
        var ct = cts.Token;

        var emp = CreateEmployee("EMP_CT", new DateTime(2024, 1, 1));
        var leaveType = CreateAnnualLeaveType();

        _employeeRepoMock.Setup(r => r.GetEmployeeByCodeAsync("EMP_CT", ct)).ReturnsAsync(emp);
        _leaveRepoMock.Setup(r => r.GetLeaveTypeByCodeAsync("ANNUAL", ct)).ReturnsAsync(leaveType);
        _leaveRepoMock.Setup(r => r.GetHrLeaveSettingsAsync(ct)).ReturnsAsync(new HrLeaveSettings());

        var dto = new DailyLeaveAccrualDto(new DateTime(2025, 6, 1), "ANNUAL", null, "EMP_CT");
        var result = await _service.RunDailyAccrualAsync(dto, "ADMIN", ct);

        Assert.NotNull(result);
        _employeeRepoMock.Verify(r => r.GetEmployeeByCodeAsync("EMP_CT", ct), Times.Once);
        _leaveRepoMock.Verify(r => r.SaveChangesAsync(ct), Times.Once);
    }

    [Fact]
    public async Task RunMonthlyAccrualAsync_PropagatesCancellationToken()
    {
        using var cts = new CancellationTokenSource();
        var ct = cts.Token;

        var emp = CreateEmployee("EMP_CT_MO", new DateTime(2024, 1, 1));
        var leaveType = CreateAnnualLeaveType();

        _employeeRepoMock.Setup(r => r.GetEmployeeByCodeAsync("EMP_CT_MO", ct)).ReturnsAsync(emp);
        _leaveRepoMock.Setup(r => r.GetLeaveTypeByCodeAsync("ANNUAL", ct)).ReturnsAsync(leaveType);
        _leaveRepoMock.Setup(r => r.GetHrLeaveSettingsAsync(ct)).ReturnsAsync(new HrLeaveSettings());

        var dto = new MonthlyLeaveAccrualDto(2025, 5, "ANNUAL", "EMP_CT_MO");
        var result = await _service.RunMonthlyAccrualAsync(dto, "ADMIN", ct);

        Assert.NotNull(result);
        _employeeRepoMock.Verify(r => r.GetEmployeeByCodeAsync("EMP_CT_MO", ct), Times.Once);
        _leaveRepoMock.Verify(r => r.SaveChangesAsync(ct), Times.Once);
    }

    [Fact]
    public async Task RunYearlyAllocationAsync_PropagatesCancellationToken()
    {
        using var cts = new CancellationTokenSource();
        var ct = cts.Token;

        var emp = CreateEmployee("EMP_CT_YR", new DateTime(2024, 1, 1));
        var leaveType = CreateAnnualLeaveType();

        _employeeRepoMock.Setup(r => r.GetEmployeeByCodeAsync("EMP_CT_YR", ct)).ReturnsAsync(emp);
        _leaveRepoMock.Setup(r => r.GetLeaveTypeByCodeAsync("ANNUAL", ct)).ReturnsAsync(leaveType);
        _leaveRepoMock.Setup(r => r.GetHrLeaveSettingsAsync(ct)).ReturnsAsync(new HrLeaveSettings());

        var dto = new YearlyLeaveAllocationDto(2025, "ANNUAL", null, "EMP_CT_YR", false);
        var result = await _service.RunYearlyAllocationAsync(dto, "ADMIN", ct);

        Assert.NotNull(result);
        _employeeRepoMock.Verify(r => r.GetEmployeeByCodeAsync("EMP_CT_YR", ct), Times.Once);
        _leaveRepoMock.Verify(r => r.SaveChangesAsync(ct), Times.Once);
    }

    [Fact]
    public async Task RunDailyAccrualAsync_ThrowsWhenFutureDateProvided()
    {
        var futureDate = DateTime.UtcNow.Date.AddDays(2);
        var dto = new DailyLeaveAccrualDto(futureDate, "ANNUAL", null, "EMP001");

        var ex = await Assert.ThrowsAsync<ArgumentException>(() => _service.RunDailyAccrualAsync(dto, "ADMIN"));
        Assert.Contains("future company-local date", ex.Message);
    }

    [Fact]
    public async Task RunDailyAccrualAsync_ThrowsWhenLeaveTypeInactive()
    {
        var leaveType = CreateAnnualLeaveType();
        leaveType.IsActive = false;

        _leaveRepoMock.Setup(r => r.GetLeaveTypeByCodeAsync("ANNUAL", It.IsAny<CancellationToken>()))
            .ReturnsAsync(leaveType);

        var dto = new DailyLeaveAccrualDto(new DateTime(2025, 6, 1), "ANNUAL", null, "EMP001");
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => _service.RunDailyAccrualAsync(dto, "ADMIN"));
        Assert.Contains("is inactive", ex.Message);
    }

    [Fact]
    public async Task RunDailyAccrualAsync_ThrowsWhenLeaveTypeDoesNotTrackBalance()
    {
        var leaveType = CreateAnnualLeaveType();
        leaveType.TracksBalance = false;

        _leaveRepoMock.Setup(r => r.GetLeaveTypeByCodeAsync("ANNUAL", It.IsAny<CancellationToken>()))
            .ReturnsAsync(leaveType);

        var dto = new DailyLeaveAccrualDto(new DateTime(2025, 6, 1), "ANNUAL", null, "EMP001");
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => _service.RunDailyAccrualAsync(dto, "ADMIN"));
        Assert.Contains("does not track a balance", ex.Message);
    }

    [Fact]
    public async Task RunDailyAccrualAsync_SkipsWhenPolicyAccrualMethodNotDaily()
    {
        var emp = CreateEmployee("EMP_MONTHLY", new DateTime(2024, 1, 1));
        var leaveType = CreateAnnualLeaveType();
        var policy = new LeavePolicy
        {
            LeaveTypeCode = "ANNUAL",
            AccrualMethod = "MONTHLY", // Not DAILY
            IsActive = true
        };

        _employeeRepoMock.Setup(r => r.GetEmployeeByCodeAsync("EMP_MONTHLY", It.IsAny<CancellationToken>())).ReturnsAsync(emp);
        _leaveRepoMock.Setup(r => r.GetLeaveTypeByCodeAsync("ANNUAL", It.IsAny<CancellationToken>())).ReturnsAsync(leaveType);
        _leaveRepoMock.Setup(r => r.GetLeavePolicyByTypeCodeAsync("ANNUAL", It.IsAny<CancellationToken>(), It.IsAny<DateTime?>()))
            .ReturnsAsync(policy);

        var dto = new DailyLeaveAccrualDto(new DateTime(2025, 6, 1), "ANNUAL", null, "EMP_MONTHLY");
        var result = await _service.RunDailyAccrualAsync(dto, "ADMIN");

        Assert.Equal(1, result.TotalEmployeesProcessed);
        Assert.Equal(0, result.UpdatedBalancesCount);
        Assert.Equal(0m, result.TotalDaysAccrued);
    }

    [Fact]
    public async Task RunMonthlyAccrualAsync_ThrowsWhenInvalidMonthNoProvided()
    {
        var dto = new MonthlyLeaveAccrualDto(2025, 13, "ANNUAL", "EMP001");

        var ex = await Assert.ThrowsAsync<ArgumentException>(() => _service.RunMonthlyAccrualAsync(dto, "ADMIN"));
        Assert.Contains("Month must be between 1 and 12", ex.Message);
    }

    [Fact]
    public async Task RunMonthlyAccrualAsync_ThrowsWhenLeaveTypeInactive()
    {
        var leaveType = CreateAnnualLeaveType();
        leaveType.IsActive = false;

        _leaveRepoMock.Setup(r => r.GetLeaveTypeByCodeAsync("ANNUAL", It.IsAny<CancellationToken>()))
            .ReturnsAsync(leaveType);

        var dto = new MonthlyLeaveAccrualDto(2025, 5, "ANNUAL", "EMP001");
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => _service.RunMonthlyAccrualAsync(dto, "ADMIN"));
        Assert.Contains("is inactive", ex.Message);
    }

    [Fact]
    public async Task RunYearlyAllocationAsync_ThrowsWhenLeaveTypeInactive()
    {
        var leaveType = CreateAnnualLeaveType();
        leaveType.IsActive = false;

        _leaveRepoMock.Setup(r => r.GetLeaveTypeByCodeAsync("ANNUAL", It.IsAny<CancellationToken>()))
            .ReturnsAsync(leaveType);

        var dto = new YearlyLeaveAllocationDto(2025, "ANNUAL", null, "EMP001", false);
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => _service.RunYearlyAllocationAsync(dto, "ADMIN"));
        Assert.Contains("is inactive", ex.Message);
    }

    [Fact]
    public async Task RunYearlyAllocationAsync_ThrowsWhenFutureDateProvided()
    {
        var futureDate = DateTime.UtcNow.Date.AddDays(5);
        var dto = new YearlyLeaveAllocationDto(2025, "ANNUAL", null, "EMP001", false, futureDate);

        var ex = await Assert.ThrowsAsync<ArgumentException>(() => _service.RunYearlyAllocationAsync(dto, "ADMIN"));
        Assert.Contains("future company-local date", ex.Message);
    }
}
