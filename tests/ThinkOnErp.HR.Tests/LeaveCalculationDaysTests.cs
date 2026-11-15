using System;
using System.Threading;
using System.Threading.Tasks;
using Moq;
using ThinkOnErp.Application.DTOs.Hr;
using ThinkOnErp.Application.Services.Hr;
using ThinkOnErp.Domain.Entities.Hr;
using ThinkOnErp.Domain.Interfaces.Hr;
using Xunit;

namespace ThinkOnErp.HR.Tests;

public class LeaveCalculationDaysTests
{
    private readonly Mock<ILeaveRepository> _leaveRepo = new();
    private readonly Mock<IEmployeeRepository> _employeeRepo = new();
    private readonly Mock<IWorkCalendarService> _calendarService = new();

    public LeaveCalculationDaysTests()
    {
        _leaveRepo.Setup(r => r.GetHrLeaveSettingsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new HrLeaveSettings());
    }

    [Fact]
    public async Task CalculateLeaveDaysAsync_WithWorkCalendar_CountsCalendarAndWorkingDaysCorrectly()
    {
        var emp = new Employee
        {
            EmployeeCode = "EMP01",
            HireDate = new DateTime(2020, 1, 1),
            EmploymentStatus = "ACTIVE",
            IsActive = true
        };

        var leaveType = new LeaveType
        {
            LeaveTypeCode = "ANNUAL",
            TracksBalance = true,
            IsActive = true
        };

        var balance = new LeaveBalance
        {
            EmployeeCode = "EMP01",
            LeaveTypeCode = "ANNUAL",
            YearNo = 2026,
            AccruedDays = 14m,
            UsedDays = 2m,
            CarriedForwardDays = 0m
        };

        _employeeRepo.Setup(r => r.GetEmployeeByCodeAsync("EMP01", It.IsAny<CancellationToken>()))
            .ReturnsAsync(emp);
        _leaveRepo.Setup(r => r.GetLeaveTypeByCodeAsync("ANNUAL", It.IsAny<CancellationToken>()))
            .ReturnsAsync(leaveType);
        _leaveRepo.Setup(r => r.GetLeaveBalanceByCycleAsync("EMP01", "ANNUAL", It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(balance);
        _leaveRepo.Setup(r => r.GetPendingLeaveDaysAsync("EMP01", "ANNUAL", It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<long?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(2m);

        // Sunday 2026-06-07 to Saturday 2026-06-13 (7 calendar days)
        var start = new DateTime(2026, 6, 7);
        var end = new DateTime(2026, 6, 13);

        // Calendar service returns 4 working days (e.g. 5 weekdays minus 1 holiday)
        _calendarService.Setup(c => c.CountWorkingDaysAsync(1L, start, end, It.IsAny<CancellationToken>()))
            .ReturnsAsync(4);

        var service = new LeaveService(_leaveRepo.Object, _employeeRepo.Object, null, _calendarService.Object);

        var result = await service.CalculateLeaveDaysAsync("EMP01", "ANNUAL", start, end, 1L);

        Assert.Equal(7, result.CalendarDays);
        Assert.Equal(4, result.WorkingDays);
        // Available: 14 accrued - 2 used - 2 pending = 10 available
        Assert.Equal(10m, result.AvailableBalance);
        Assert.True(result.HasSufficientBalance); // 10 >= 4
    }

    [Fact]
    public async Task CalculateLeaveDaysAsync_WithoutWorkCalendar_DefaultsToExcludingFridaySaturday()
    {
        var emp = new Employee { EmployeeCode = "EMP01", HireDate = new DateTime(2020, 1, 1) };
        var leaveType = new LeaveType { LeaveTypeCode = "ANNUAL", TracksBalance = false };

        _employeeRepo.Setup(r => r.GetEmployeeByCodeAsync("EMP01", It.IsAny<CancellationToken>()))
            .ReturnsAsync(emp);
        _leaveRepo.Setup(r => r.GetLeaveTypeByCodeAsync("ANNUAL", It.IsAny<CancellationToken>()))
            .ReturnsAsync(leaveType);

        // Sunday 2026-06-07 to Thursday 2026-06-11 (5 days, all weekdays)
        var start = new DateTime(2026, 6, 7);
        var end = new DateTime(2026, 6, 11);

        var service = new LeaveService(_leaveRepo.Object, _employeeRepo.Object, null, null);

        var result = await service.CalculateLeaveDaysAsync("EMP01", "ANNUAL", start, end, 1L);

        Assert.Equal(5, result.CalendarDays);
        Assert.Equal(5, result.WorkingDays);
        Assert.True(result.HasSufficientBalance);
    }

    [Fact]
    public async Task CalculateLeaveDaysAsync_WhenTracksBalanceAndInsufficient_ReturnsFalseForSufficientBalance()
    {
        var emp = new Employee { EmployeeCode = "EMP01", HireDate = new DateTime(2020, 1, 1) };
        var leaveType = new LeaveType { LeaveTypeCode = "ANNUAL", TracksBalance = true };
        var balance = new LeaveBalance
        {
            EmployeeCode = "EMP01",
            LeaveTypeCode = "ANNUAL",
            YearNo = 2026,
            AccruedDays = 3m,
            UsedDays = 2m,
            CarriedForwardDays = 0m
        };

        _employeeRepo.Setup(r => r.GetEmployeeByCodeAsync("EMP01", It.IsAny<CancellationToken>()))
            .ReturnsAsync(emp);
        _leaveRepo.Setup(r => r.GetLeaveTypeByCodeAsync("ANNUAL", It.IsAny<CancellationToken>()))
            .ReturnsAsync(leaveType);
        _leaveRepo.Setup(r => r.GetLeaveBalanceByCycleAsync("EMP01", "ANNUAL", It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(balance);
        _leaveRepo.Setup(r => r.GetPendingLeaveDaysAsync("EMP01", "ANNUAL", It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<long?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(0m);

        // Available = 3 - 2 = 1 day
        var start = new DateTime(2026, 6, 7);
        var end = new DateTime(2026, 6, 9); // 3 working days

        _calendarService.Setup(c => c.CountWorkingDaysAsync(1L, start, end, It.IsAny<CancellationToken>()))
            .ReturnsAsync(3);

        var service = new LeaveService(_leaveRepo.Object, _employeeRepo.Object, null, _calendarService.Object);

        var result = await service.CalculateLeaveDaysAsync("EMP01", "ANNUAL", start, end, 1L);

        Assert.Equal(3, result.WorkingDays);
        Assert.Equal(1m, result.AvailableBalance);
        Assert.False(result.HasSufficientBalance); // 1 < 3
    }

    [Fact]
    public async Task CalculateLeaveDaysAsync_WhenEndDateBeforeStartDate_ThrowsArgumentException()
    {
        var service = new LeaveService(_leaveRepo.Object, _employeeRepo.Object);

        var ex = await Assert.ThrowsAsync<ArgumentException>(() =>
            service.CalculateLeaveDaysAsync("EMP01", "ANNUAL", new DateTime(2026, 6, 10), new DateTime(2026, 6, 5), 1L));

        Assert.Contains("End date cannot be earlier than start date", ex.Message);
    }

    [Fact]
    public async Task CalculateLeaveDaysAsync_WhenEmployeeNotFound_ThrowsKeyNotFoundException()
    {
        _employeeRepo.Setup(r => r.GetEmployeeByCodeAsync("NOT_EXIST", It.IsAny<CancellationToken>()))
            .ReturnsAsync((Employee?)null);

        var service = new LeaveService(_leaveRepo.Object, _employeeRepo.Object);

        await Assert.ThrowsAsync<KeyNotFoundException>(() =>
            service.CalculateLeaveDaysAsync("NOT_EXIST", "ANNUAL", new DateTime(2026, 6, 1), new DateTime(2026, 6, 5), 1L));
    }

    [Fact]
    public async Task CalculateLeaveDaysAsync_WhenLeaveTypeNotFound_ThrowsKeyNotFoundException()
    {
        var emp = new Employee { EmployeeCode = "EMP01", HireDate = new DateTime(2020, 1, 1) };
        _employeeRepo.Setup(r => r.GetEmployeeByCodeAsync("EMP01", It.IsAny<CancellationToken>())).ReturnsAsync(emp);
        _leaveRepo.Setup(r => r.GetLeaveTypeByCodeAsync("BAD_TYPE", It.IsAny<CancellationToken>())).ReturnsAsync((LeaveType?)null);

        var service = new LeaveService(_leaveRepo.Object, _employeeRepo.Object);

        await Assert.ThrowsAsync<KeyNotFoundException>(() =>
            service.CalculateLeaveDaysAsync("EMP01", "BAD_TYPE", new DateTime(2026, 6, 1), new DateTime(2026, 6, 5), 1L));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task CalculateLeaveDaysAsync_WhenNullOrWhitespaceEmployeeCode_ThrowsArgumentException(string? empCode)
    {
        var service = new LeaveService(_leaveRepo.Object, _employeeRepo.Object);

        await Assert.ThrowsAsync<ArgumentException>(() =>
            service.CalculateLeaveDaysAsync(empCode!, "ANNUAL", new DateTime(2026, 6, 1), new DateTime(2026, 6, 5), 1L));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task CalculateLeaveDaysAsync_WhenNullOrWhitespaceLeaveTypeCode_ThrowsArgumentException(string? leaveTypeCode)
    {
        var service = new LeaveService(_leaveRepo.Object, _employeeRepo.Object);

        await Assert.ThrowsAsync<ArgumentException>(() =>
            service.CalculateLeaveDaysAsync("EMP01", leaveTypeCode!, new DateTime(2026, 6, 1), new DateTime(2026, 6, 5), 1L));
    }

    [Fact]
    public async Task CalculateLeaveDaysAsync_SingleDayLeave_OnWorkingDay_CountsOneCalendarAndOneWorkingDay()
    {
        var emp = new Employee { EmployeeCode = "EMP01", HireDate = new DateTime(2020, 1, 1) };
        var leaveType = new LeaveType { LeaveTypeCode = "ANNUAL", TracksBalance = false };

        _employeeRepo.Setup(r => r.GetEmployeeByCodeAsync("EMP01", It.IsAny<CancellationToken>())).ReturnsAsync(emp);
        _leaveRepo.Setup(r => r.GetLeaveTypeByCodeAsync("ANNUAL", It.IsAny<CancellationToken>())).ReturnsAsync(leaveType);

        // Wednesday 2026-06-10 (1 working day)
        var date = new DateTime(2026, 6, 10);
        var service = new LeaveService(_leaveRepo.Object, _employeeRepo.Object, null, null);

        var result = await service.CalculateLeaveDaysAsync("EMP01", "ANNUAL", date, date, 1L);

        Assert.Equal(1, result.CalendarDays);
        Assert.Equal(1, result.WorkingDays);
        Assert.True(result.HasSufficientBalance);
    }

    [Fact]
    public async Task CalculateLeaveDaysAsync_SingleDayLeave_OnWeekend_CountsOneCalendarAndZeroWorkingDays()
    {
        var emp = new Employee { EmployeeCode = "EMP01", HireDate = new DateTime(2020, 1, 1) };
        var leaveType = new LeaveType { LeaveTypeCode = "ANNUAL", TracksBalance = false };

        _employeeRepo.Setup(r => r.GetEmployeeByCodeAsync("EMP01", It.IsAny<CancellationToken>())).ReturnsAsync(emp);
        _leaveRepo.Setup(r => r.GetLeaveTypeByCodeAsync("ANNUAL", It.IsAny<CancellationToken>())).ReturnsAsync(leaveType);

        // Friday 2026-06-12 (weekend in default fallback)
        var friday = new DateTime(2026, 6, 12);
        var service = new LeaveService(_leaveRepo.Object, _employeeRepo.Object, null, null);

        var result = await service.CalculateLeaveDaysAsync("EMP01", "ANNUAL", friday, friday, 1L);

        Assert.Equal(1, result.CalendarDays);
        Assert.Equal(0, result.WorkingDays);
        Assert.True(result.HasSufficientBalance);
    }

    [Fact]
    public async Task CalculateLeaveDaysAsync_WeekendOnlyRange_CountsCalendarDaysWithZeroWorkingDays()
    {
        var emp = new Employee { EmployeeCode = "EMP01", HireDate = new DateTime(2020, 1, 1) };
        var leaveType = new LeaveType { LeaveTypeCode = "ANNUAL", TracksBalance = false };

        _employeeRepo.Setup(r => r.GetEmployeeByCodeAsync("EMP01", It.IsAny<CancellationToken>())).ReturnsAsync(emp);
        _leaveRepo.Setup(r => r.GetLeaveTypeByCodeAsync("ANNUAL", It.IsAny<CancellationToken>())).ReturnsAsync(leaveType);

        // Friday 2026-06-12 to Saturday 2026-06-13 (2 days, weekend only)
        var start = new DateTime(2026, 6, 12);
        var end = new DateTime(2026, 6, 13);
        var service = new LeaveService(_leaveRepo.Object, _employeeRepo.Object, null, null);

        var result = await service.CalculateLeaveDaysAsync("EMP01", "ANNUAL", start, end, 1L);

        Assert.Equal(2, result.CalendarDays);
        Assert.Equal(0, result.WorkingDays);
        Assert.True(result.HasSufficientBalance);
    }

    [Fact]
    public async Task CalculateLeaveDaysAsync_RangeCoveringWeekend_ExcludesWeekendDaysCorrectly()
    {
        var emp = new Employee { EmployeeCode = "EMP01", HireDate = new DateTime(2020, 1, 1) };
        var leaveType = new LeaveType { LeaveTypeCode = "ANNUAL", TracksBalance = false };

        _employeeRepo.Setup(r => r.GetEmployeeByCodeAsync("EMP01", It.IsAny<CancellationToken>())).ReturnsAsync(emp);
        _leaveRepo.Setup(r => r.GetLeaveTypeByCodeAsync("ANNUAL", It.IsAny<CancellationToken>())).ReturnsAsync(leaveType);

        // Thursday 2026-06-11 to Sunday 2026-06-14 (4 calendar days: Thu, Fri, Sat, Sun => 2 working days)
        var start = new DateTime(2026, 6, 11);
        var end = new DateTime(2026, 6, 14);
        var service = new LeaveService(_leaveRepo.Object, _employeeRepo.Object, null, null);

        var result = await service.CalculateLeaveDaysAsync("EMP01", "ANNUAL", start, end, 1L);

        Assert.Equal(4, result.CalendarDays);
        Assert.Equal(2, result.WorkingDays);
        Assert.True(result.HasSufficientBalance);
    }

    [Fact]
    public async Task CalculateLeaveDaysAsync_WithCarriedForwardAndPendingDeductions_CalculatesAvailableBalanceAccurately()
    {
        var emp = new Employee { EmployeeCode = "EMP01", HireDate = new DateTime(2020, 1, 1) };
        var leaveType = new LeaveType { LeaveTypeCode = "ANNUAL", TracksBalance = true };
        var balance = new LeaveBalance
        {
            EmployeeCode = "EMP01",
            LeaveTypeCode = "ANNUAL",
            YearNo = 2026,
            AccruedDays = 15m,
            CarriedForwardDays = 5m,
            UsedDays = 3m
        };

        _employeeRepo.Setup(r => r.GetEmployeeByCodeAsync("EMP01", It.IsAny<CancellationToken>())).ReturnsAsync(emp);
        _leaveRepo.Setup(r => r.GetLeaveTypeByCodeAsync("ANNUAL", It.IsAny<CancellationToken>())).ReturnsAsync(leaveType);
        _leaveRepo.Setup(r => r.GetLeaveBalanceByCycleAsync("EMP01", "ANNUAL", It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(balance);
        _leaveRepo.Setup(r => r.GetPendingLeaveDaysAsync("EMP01", "ANNUAL", It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<long?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(4m);

        // Sunday 2026-06-07 to Thursday 2026-06-11 (5 working days)
        var start = new DateTime(2026, 6, 7);
        var end = new DateTime(2026, 6, 11);
        var service = new LeaveService(_leaveRepo.Object, _employeeRepo.Object, null, null);

        var result = await service.CalculateLeaveDaysAsync("EMP01", "ANNUAL", start, end, 1L);

        // Available: Accrued (15) + Carried (5) - Used (3) - Pending (4) = 13
        Assert.Equal(5, result.CalendarDays);
        Assert.Equal(5, result.WorkingDays);
        Assert.Equal(13m, result.AvailableBalance);
        Assert.True(result.HasSufficientBalance); // 13 >= 5
    }

    [Fact]
    public async Task CalculateLeaveDaysAsync_ExactBalanceMatch_ReturnsTrueForSufficientBalance()
    {
        var emp = new Employee { EmployeeCode = "EMP01", HireDate = new DateTime(2020, 1, 1) };
        var leaveType = new LeaveType { LeaveTypeCode = "ANNUAL", TracksBalance = true };
        var balance = new LeaveBalance
        {
            EmployeeCode = "EMP01",
            LeaveTypeCode = "ANNUAL",
            YearNo = 2026,
            AccruedDays = 5m,
            CarriedForwardDays = 0m,
            UsedDays = 0m
        };

        _employeeRepo.Setup(r => r.GetEmployeeByCodeAsync("EMP01", It.IsAny<CancellationToken>())).ReturnsAsync(emp);
        _leaveRepo.Setup(r => r.GetLeaveTypeByCodeAsync("ANNUAL", It.IsAny<CancellationToken>())).ReturnsAsync(leaveType);
        _leaveRepo.Setup(r => r.GetLeaveBalanceByCycleAsync("EMP01", "ANNUAL", It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(balance);
        _leaveRepo.Setup(r => r.GetPendingLeaveDaysAsync("EMP01", "ANNUAL", It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<long?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(0m);

        var start = new DateTime(2026, 6, 7);
        var end = new DateTime(2026, 6, 11); // 5 working days
        var service = new LeaveService(_leaveRepo.Object, _employeeRepo.Object, null, null);

        var result = await service.CalculateLeaveDaysAsync("EMP01", "ANNUAL", start, end, 1L);

        Assert.Equal(5m, result.AvailableBalance);
        Assert.Equal(5, result.WorkingDays);
        Assert.True(result.HasSufficientBalance); // exactly equal
    }

    [Fact]
    public async Task CalculateLeaveDaysAsync_WhenLeaveFallsOnCalendarHolidays_WorkCalendarDeterminesWorkingDays()
    {
        var emp = new Employee { EmployeeCode = "EMP01", HireDate = new DateTime(2020, 1, 1) };
        var leaveType = new LeaveType { LeaveTypeCode = "ANNUAL", TracksBalance = true };
        var balance = new LeaveBalance
        {
            EmployeeCode = "EMP01",
            LeaveTypeCode = "ANNUAL",
            YearNo = 2026,
            AccruedDays = 2m,
            UsedDays = 0m
        };

        _employeeRepo.Setup(r => r.GetEmployeeByCodeAsync("EMP01", It.IsAny<CancellationToken>())).ReturnsAsync(emp);
        _leaveRepo.Setup(r => r.GetLeaveTypeByCodeAsync("ANNUAL", It.IsAny<CancellationToken>())).ReturnsAsync(leaveType);
        _leaveRepo.Setup(r => r.GetLeaveBalanceByCycleAsync("EMP01", "ANNUAL", It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(balance);
        _leaveRepo.Setup(r => r.GetPendingLeaveDaysAsync("EMP01", "ANNUAL", It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<long?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(0m);

        var start = new DateTime(2026, 6, 7);
        var end = new DateTime(2026, 6, 11); // 5 calendar weekdays, but 3 are public holidays

        _calendarService.Setup(c => c.CountWorkingDaysAsync(1L, start, end, It.IsAny<CancellationToken>()))
            .ReturnsAsync(2); // Only 2 actual working days

        var service = new LeaveService(_leaveRepo.Object, _employeeRepo.Object, null, _calendarService.Object);

        var result = await service.CalculateLeaveDaysAsync("EMP01", "ANNUAL", start, end, 1L);

        Assert.Equal(5, result.CalendarDays);
        Assert.Equal(2, result.WorkingDays);
        Assert.Equal(2m, result.AvailableBalance);
        Assert.True(result.HasSufficientBalance); // 2 >= 2
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task CalculateLeaveDaysAsync_ThrowsWhenEmployeeCodeWhitespace(string? invalidCode)
    {
        var service = new LeaveService(_leaveRepo.Object, _employeeRepo.Object, null, null);
        var ex = await Assert.ThrowsAsync<ArgumentException>(() =>
            service.CalculateLeaveDaysAsync(invalidCode!, "ANNUAL", DateTime.Today, DateTime.Today, 1L));
        Assert.Equal("employeeCode", ex.ParamName);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task CalculateLeaveDaysAsync_ThrowsWhenLeaveTypeCodeWhitespace(string? invalidType)
    {
        var service = new LeaveService(_leaveRepo.Object, _employeeRepo.Object, null, null);
        var ex = await Assert.ThrowsAsync<ArgumentException>(() =>
            service.CalculateLeaveDaysAsync("EMP01", invalidType!, DateTime.Today, DateTime.Today, 1L));
        Assert.Equal("leaveTypeCode", ex.ParamName);
    }

    [Fact]
    public async Task CalculateLeaveDaysAsync_ThrowsWhenEndDateEarlierThanStartDate()
    {
        var service = new LeaveService(_leaveRepo.Object, _employeeRepo.Object, null, null);
        var start = new DateTime(2026, 6, 10);
        var end = new DateTime(2026, 6, 9);
        var ex = await Assert.ThrowsAsync<ArgumentException>(() =>
            service.CalculateLeaveDaysAsync("EMP01", "ANNUAL", start, end, 1L));
        Assert.Equal("endDate", ex.ParamName);
        Assert.Contains("End date cannot be earlier than start date", ex.Message);
    }

    [Fact]
    public async Task CalculateLeaveDaysAsync_ThrowsWhenEmployeeNotFound()
    {
        _employeeRepo.Setup(r => r.GetEmployeeByCodeAsync("NOT_FOUND", It.IsAny<CancellationToken>()))
            .ReturnsAsync((Employee?)null);

        var service = new LeaveService(_leaveRepo.Object, _employeeRepo.Object, null, null);
        var ex = await Assert.ThrowsAsync<KeyNotFoundException>(() =>
            service.CalculateLeaveDaysAsync("NOT_FOUND", "ANNUAL", DateTime.Today, DateTime.Today, 1L));
        Assert.Contains("Employee 'NOT_FOUND' not found", ex.Message);
    }

    [Fact]
    public async Task CalculateLeaveDaysAsync_ThrowsWhenLeaveTypeNotFound()
    {
        var emp = new Employee { EmployeeCode = "EMP01", HireDate = new DateTime(2020, 1, 1) };
        _employeeRepo.Setup(r => r.GetEmployeeByCodeAsync("EMP01", It.IsAny<CancellationToken>()))
            .ReturnsAsync(emp);
        _leaveRepo.Setup(r => r.GetLeaveTypeByCodeAsync("NOT_FOUND", It.IsAny<CancellationToken>()))
            .ReturnsAsync((LeaveType?)null);

        var service = new LeaveService(_leaveRepo.Object, _employeeRepo.Object, null, null);
        var ex = await Assert.ThrowsAsync<KeyNotFoundException>(() =>
            service.CalculateLeaveDaysAsync("EMP01", "NOT_FOUND", DateTime.Today, DateTime.Today, 1L));
        Assert.Contains("Leave type 'NOT_FOUND' not found", ex.Message);
    }

    [Fact]
    public async Task CalculateLeaveDaysAsync_WhenDoesNotTrackBalance_HasSufficientBalanceAlwaysTrue()
    {
        var emp = new Employee { EmployeeCode = "EMP01", HireDate = new DateTime(2020, 1, 1) };
        var leaveType = new LeaveType { LeaveTypeCode = "UNPAID", TracksBalance = false };

        _employeeRepo.Setup(r => r.GetEmployeeByCodeAsync("EMP01", It.IsAny<CancellationToken>())).ReturnsAsync(emp);
        _leaveRepo.Setup(r => r.GetLeaveTypeByCodeAsync("UNPAID", It.IsAny<CancellationToken>())).ReturnsAsync(leaveType);

        var start = new DateTime(2026, 6, 7); // Sunday
        var end = new DateTime(2026, 6, 11);  // Thursday (5 working days)
        var service = new LeaveService(_leaveRepo.Object, _employeeRepo.Object, null, null);

        var result = await service.CalculateLeaveDaysAsync("EMP01", "UNPAID", start, end, 1L);

        Assert.Equal(5, result.CalendarDays);
        Assert.Equal(5, result.WorkingDays);
        Assert.Equal(0m, result.AvailableBalance);
        Assert.True(result.HasSufficientBalance);
    }

    [Fact]
    public async Task CalculateLeaveDaysAsync_SingleDayWeekend_ReturnsOneCalendarDayAndZeroWorkingDays()
    {
        var emp = new Employee { EmployeeCode = "EMP01", HireDate = new DateTime(2020, 1, 1) };
        var leaveType = new LeaveType { LeaveTypeCode = "UNPAID", TracksBalance = false };

        _employeeRepo.Setup(r => r.GetEmployeeByCodeAsync("EMP01", It.IsAny<CancellationToken>())).ReturnsAsync(emp);
        _leaveRepo.Setup(r => r.GetLeaveTypeByCodeAsync("UNPAID", It.IsAny<CancellationToken>())).ReturnsAsync(leaveType);

        var friday = new DateTime(2026, 6, 12);
        var service = new LeaveService(_leaveRepo.Object, _employeeRepo.Object, null, null);

        var result = await service.CalculateLeaveDaysAsync("EMP01", "UNPAID", friday, friday, 1L);

        Assert.Equal(1, result.CalendarDays);
        Assert.Equal(0, result.WorkingDays);
        Assert.True(result.HasSufficientBalance);
    }

    [Fact]
    public async Task CalculateLeaveDaysAsync_SingleDayWeekday_ReturnsOneCalendarDayAndOneWorkingDay()
    {
        var emp = new Employee { EmployeeCode = "EMP01", HireDate = new DateTime(2020, 1, 1) };
        var leaveType = new LeaveType { LeaveTypeCode = "UNPAID", TracksBalance = false };

        _employeeRepo.Setup(r => r.GetEmployeeByCodeAsync("EMP01", It.IsAny<CancellationToken>())).ReturnsAsync(emp);
        _leaveRepo.Setup(r => r.GetLeaveTypeByCodeAsync("UNPAID", It.IsAny<CancellationToken>())).ReturnsAsync(leaveType);

        var tuesday = new DateTime(2026, 6, 9);
        var service = new LeaveService(_leaveRepo.Object, _employeeRepo.Object, null, null);

        var result = await service.CalculateLeaveDaysAsync("EMP01", "UNPAID", tuesday, tuesday, 1L);

        Assert.Equal(1, result.CalendarDays);
        Assert.Equal(1, result.WorkingDays);
        Assert.True(result.HasSufficientBalance);
    }

    [Fact]
    public async Task CalculateLeaveDaysAsync_InsufficientBalance_ReturnsHasSufficientBalanceFalse()
    {
        var emp = new Employee { EmployeeCode = "EMP01", HireDate = new DateTime(2020, 1, 1) };
        var leaveType = new LeaveType { LeaveTypeCode = "ANNUAL", TracksBalance = true };
        var balance = new LeaveBalance
        {
            EmployeeCode = "EMP01",
            LeaveTypeCode = "ANNUAL",
            YearNo = 2026,
            AccruedDays = 3m,
            CarriedForwardDays = 0m,
            UsedDays = 1m
        };

        _employeeRepo.Setup(r => r.GetEmployeeByCodeAsync("EMP01", It.IsAny<CancellationToken>())).ReturnsAsync(emp);
        _leaveRepo.Setup(r => r.GetLeaveTypeByCodeAsync("ANNUAL", It.IsAny<CancellationToken>())).ReturnsAsync(leaveType);
        _leaveRepo.Setup(r => r.GetLeaveBalanceByCycleAsync("EMP01", "ANNUAL", It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(balance);
        _leaveRepo.Setup(r => r.GetPendingLeaveDaysAsync("EMP01", "ANNUAL", It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<long?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(0m);

        var start = new DateTime(2026, 6, 7); // Sunday
        var end = new DateTime(2026, 6, 11);  // Thursday (5 working days)
        var service = new LeaveService(_leaveRepo.Object, _employeeRepo.Object, null, null);

        var result = await service.CalculateLeaveDaysAsync("EMP01", "ANNUAL", start, end, 1L);

        Assert.Equal(2m, result.AvailableBalance); // 3 accrued - 1 used = 2 available
        Assert.Equal(5, result.WorkingDays);
        Assert.False(result.HasSufficientBalance); // 2 < 5
    }

    [Fact]
    public async Task CalculateLeaveDaysAsync_PendingDaysExceedsRemaining_ResultsInNegativeAvailableBalance()
    {
        var emp = new Employee { EmployeeCode = "EMP01", HireDate = new DateTime(2020, 1, 1) };
        var leaveType = new LeaveType { LeaveTypeCode = "ANNUAL", TracksBalance = true };
        var balance = new LeaveBalance
        {
            EmployeeCode = "EMP01",
            LeaveTypeCode = "ANNUAL",
            YearNo = 2026,
            AccruedDays = 10m,
            CarriedForwardDays = 0m,
            UsedDays = 8m
        };

        _employeeRepo.Setup(r => r.GetEmployeeByCodeAsync("EMP01", It.IsAny<CancellationToken>())).ReturnsAsync(emp);
        _leaveRepo.Setup(r => r.GetLeaveTypeByCodeAsync("ANNUAL", It.IsAny<CancellationToken>())).ReturnsAsync(leaveType);
        _leaveRepo.Setup(r => r.GetLeaveBalanceByCycleAsync("EMP01", "ANNUAL", It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(balance);
        _leaveRepo.Setup(r => r.GetPendingLeaveDaysAsync("EMP01", "ANNUAL", It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<long?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(5m); // 5 pending while only 2 remaining

        var start = new DateTime(2026, 6, 7);
        var end = new DateTime(2026, 6, 8); // 2 working days
        var service = new LeaveService(_leaveRepo.Object, _employeeRepo.Object, null, null);

        var result = await service.CalculateLeaveDaysAsync("EMP01", "ANNUAL", start, end, 1L);

        Assert.Equal(-3m, result.AvailableBalance); // 10 - 8 - 5 = -3
        Assert.Equal(2, result.WorkingDays);
        Assert.False(result.HasSufficientBalance); // -3 < 2
    }

    [Fact]
    public async Task CalculateLeaveDaysAsync_WhenBalanceNull_CalculatesAccruedToDateFromPolicy()
    {
        var emp = new Employee
        {
            EmployeeCode = "EMP01",
            HireDate = new DateTime(2020, 1, 1),
            EmploymentStatus = "ACTIVE",
            IsActive = true
        };

        var leaveType = new LeaveType
        {
            LeaveTypeCode = "ANNUAL",
            TracksBalance = true,
            IsActive = true
        };

        var policy = new LeavePolicy
        {
            Id = 1,
            LeaveTypeCode = "ANNUAL",
            AccrualMethod = "YEARLY",
            AnnualEntitlementDays = 21m,
            IsActive = true,
            EffectiveFrom = new DateTime(2020, 1, 1)
        };

        _employeeRepo.Setup(r => r.GetEmployeeByCodeAsync("EMP01", It.IsAny<CancellationToken>()))
            .ReturnsAsync(emp);
        _leaveRepo.Setup(r => r.GetLeaveTypeByCodeAsync("ANNUAL", It.IsAny<CancellationToken>()))
            .ReturnsAsync(leaveType);
        _leaveRepo.Setup(r => r.GetLeavePolicyByTypeCodeAsync("ANNUAL", It.IsAny<CancellationToken>(), It.IsAny<DateTime?>()))
            .ReturnsAsync(policy);
        _leaveRepo.Setup(r => r.GetLeaveBalanceByCycleAsync("EMP01", "ANNUAL", It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((LeaveBalance?)null); // No balance record yet
        _leaveRepo.Setup(r => r.GetPendingLeaveDaysAsync("EMP01", "ANNUAL", It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<long?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(0m);

        var start = new DateTime(2026, 6, 7); // Sunday
        var end = new DateTime(2026, 6, 9);   // Tuesday (3 days)

        var service = new LeaveService(_leaveRepo.Object, _employeeRepo.Object, null, null);

        var result = await service.CalculateLeaveDaysAsync("EMP01", "ANNUAL", start, end, 1L);

        Assert.Equal(3, result.CalendarDays);
        Assert.Equal(3, result.WorkingDays);
        Assert.Equal(21m, result.AvailableBalance);
        Assert.True(result.HasSufficientBalance);
    }

    [Fact]
    public async Task CalculateLeaveDaysAsync_ZeroWorkingDays_WhenTracksBalance_ReturnsHasSufficientBalanceTrue()
    {
        var emp = new Employee { EmployeeCode = "EMP01", HireDate = new DateTime(2020, 1, 1) };
        var leaveType = new LeaveType { LeaveTypeCode = "ANNUAL", TracksBalance = true };
        var balance = new LeaveBalance
        {
            EmployeeCode = "EMP01",
            LeaveTypeCode = "ANNUAL",
            YearNo = 2026,
            AccruedDays = 0m,
            CarriedForwardDays = 0m,
            UsedDays = 0m
        };

        _employeeRepo.Setup(r => r.GetEmployeeByCodeAsync("EMP01", It.IsAny<CancellationToken>())).ReturnsAsync(emp);
        _leaveRepo.Setup(r => r.GetLeaveTypeByCodeAsync("ANNUAL", It.IsAny<CancellationToken>())).ReturnsAsync(leaveType);
        _leaveRepo.Setup(r => r.GetLeaveBalanceByCycleAsync("EMP01", "ANNUAL", It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(balance);
        _leaveRepo.Setup(r => r.GetPendingLeaveDaysAsync("EMP01", "ANNUAL", It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<long?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(0m);

        // Friday 2026-06-12 to Saturday 2026-06-13 (weekend only: 0 working days)
        var start = new DateTime(2026, 6, 12);
        var end = new DateTime(2026, 6, 13);
        var service = new LeaveService(_leaveRepo.Object, _employeeRepo.Object, null, null);

        var result = await service.CalculateLeaveDaysAsync("EMP01", "ANNUAL", start, end, 1L);

        Assert.Equal(2, result.CalendarDays);
        Assert.Equal(0, result.WorkingDays);
        Assert.Equal(0m, result.AvailableBalance);
        Assert.True(result.HasSufficientBalance); // 0 >= 0 is true
    }

    [Fact]
    public async Task CalculateLeaveDaysAsync_MultiYearSpan_CalculatesCalendarDaysAndWorkingDays()
    {
        var emp = new Employee { EmployeeCode = "EMP01", HireDate = new DateTime(2020, 1, 1) };
        var leaveType = new LeaveType { LeaveTypeCode = "UNPAID", TracksBalance = false };

        _employeeRepo.Setup(r => r.GetEmployeeByCodeAsync("EMP01", It.IsAny<CancellationToken>())).ReturnsAsync(emp);
        _leaveRepo.Setup(r => r.GetLeaveTypeByCodeAsync("UNPAID", It.IsAny<CancellationToken>())).ReturnsAsync(leaveType);

        // Wednesday 2026-12-30 to Monday 2027-01-04 (6 calendar days: Wed, Thu, Fri[off], Sat[off], Sun, Mon => 4 working days)
        var start = new DateTime(2026, 12, 30);
        var end = new DateTime(2027, 1, 4);
        var service = new LeaveService(_leaveRepo.Object, _employeeRepo.Object, null, null);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.CalculateLeaveDaysAsync("EMP01", "UNPAID", start, end, 1L));
        Assert.Contains("Split this leave calculation at the leave-year boundary", ex.Message);
    }

    [Fact]
    public async Task CalculateLeaveDaysAsync_LeapYearBoundary_February28ToMarch01_CalculatesAccurately()
    {
        var emp = new Employee { EmployeeCode = "EMP01", HireDate = new DateTime(2020, 1, 1) };
        var leaveType = new LeaveType { LeaveTypeCode = "UNPAID", TracksBalance = false };

        _employeeRepo.Setup(r => r.GetEmployeeByCodeAsync("EMP01", It.IsAny<CancellationToken>())).ReturnsAsync(emp);
        _leaveRepo.Setup(r => r.GetLeaveTypeByCodeAsync("UNPAID", It.IsAny<CancellationToken>())).ReturnsAsync(leaveType);

        // In 2024 (leap year): 2024-02-28 (Wed), 2024-02-29 (Thu), 2024-03-01 (Fri - off) => 3 calendar days, 2 working days
        var start = new DateTime(2024, 2, 28);
        var end = new DateTime(2024, 3, 1);
        var service = new LeaveService(_leaveRepo.Object, _employeeRepo.Object, null, null);

        var result = await service.CalculateLeaveDaysAsync("EMP01", "UNPAID", start, end, 1L);

        Assert.Equal(3, result.CalendarDays);
        Assert.Equal(2, result.WorkingDays);
        Assert.True(result.HasSufficientBalance);
    }

    [Fact]
    public async Task CalculateLeaveDaysAsync_ZeroPendingLeaveDays_DoesNotAffectAvailableBalance()
    {
        var emp = new Employee { EmployeeCode = "EMP01", HireDate = new DateTime(2020, 1, 1) };
        var leaveType = new LeaveType { LeaveTypeCode = "ANNUAL", TracksBalance = true };
        var balance = new LeaveBalance
        {
            EmployeeCode = "EMP01",
            LeaveTypeCode = "ANNUAL",
            YearNo = 2026,
            AccruedDays = 14m,
            CarriedForwardDays = 2m,
            UsedDays = 4m
        };

        _employeeRepo.Setup(r => r.GetEmployeeByCodeAsync("EMP01", It.IsAny<CancellationToken>())).ReturnsAsync(emp);
        _leaveRepo.Setup(r => r.GetLeaveTypeByCodeAsync("ANNUAL", It.IsAny<CancellationToken>())).ReturnsAsync(leaveType);
        _leaveRepo.Setup(r => r.GetLeaveBalanceByCycleAsync("EMP01", "ANNUAL", It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(balance);
        _leaveRepo.Setup(r => r.GetPendingLeaveDaysAsync("EMP01", "ANNUAL", It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<long?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(0m);

        var start = new DateTime(2026, 6, 7);
        var end = new DateTime(2026, 6, 9);
        var service = new LeaveService(_leaveRepo.Object, _employeeRepo.Object, null, null);

        var result = await service.CalculateLeaveDaysAsync("EMP01", "ANNUAL", start, end, 1L);

        // 14 + 2 - 4 - 0 = 12
        Assert.Equal(12m, result.AvailableBalance);
        Assert.True(result.HasSufficientBalance);
    }

    [Fact]
    public async Task CalculateLeaveDaysAsync_PopulatesCycleDates_WhenTrackingBalance()
    {
        var emp = new Employee { EmployeeCode = "EMP01", HireDate = new DateTime(2020, 1, 1) };
        var leaveType = new LeaveType { LeaveTypeCode = "ANNUAL", TracksBalance = true };
        var balance = new LeaveBalance
        {
            EmployeeCode = "EMP01",
            LeaveTypeCode = "ANNUAL",
            YearNo = 2026,
            AccruedDays = 10m,
            UsedDays = 2m
        };

        _employeeRepo.Setup(r => r.GetEmployeeByCodeAsync("EMP01", It.IsAny<CancellationToken>())).ReturnsAsync(emp);
        _leaveRepo.Setup(r => r.GetLeaveTypeByCodeAsync("ANNUAL", It.IsAny<CancellationToken>())).ReturnsAsync(leaveType);
        _leaveRepo.Setup(r => r.GetLeaveBalanceByCycleAsync("EMP01", "ANNUAL", It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(balance);
        _leaveRepo.Setup(r => r.GetPendingLeaveDaysAsync("EMP01", "ANNUAL", It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<long?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(0m);

        var start = new DateTime(2026, 6, 7);
        var end = new DateTime(2026, 6, 8);
        var service = new LeaveService(_leaveRepo.Object, _employeeRepo.Object, null, null);

        var result = await service.CalculateLeaveDaysAsync("EMP01", "ANNUAL", start, end, 1L);

        Assert.NotNull(result.CycleStartDate);
        Assert.NotNull(result.CycleEndDate);
        Assert.True(result.CycleEndDate > result.CycleStartDate);
    }

    [Fact]
    public async Task CalculateLeaveDaysAsync_CycleDatesAreNull_WhenNotTrackingBalance()
    {
        var emp = new Employee { EmployeeCode = "EMP01", HireDate = new DateTime(2020, 1, 1) };
        var leaveType = new LeaveType { LeaveTypeCode = "UNPAID", TracksBalance = false };

        _employeeRepo.Setup(r => r.GetEmployeeByCodeAsync("EMP01", It.IsAny<CancellationToken>())).ReturnsAsync(emp);
        _leaveRepo.Setup(r => r.GetLeaveTypeByCodeAsync("UNPAID", It.IsAny<CancellationToken>())).ReturnsAsync(leaveType);

        var start = new DateTime(2026, 6, 7);
        var end = new DateTime(2026, 6, 8);
        var service = new LeaveService(_leaveRepo.Object, _employeeRepo.Object, null, null);

        var result = await service.CalculateLeaveDaysAsync("EMP01", "UNPAID", start, end, 1L);

        Assert.Null(result.CycleStartDate);
        Assert.Null(result.CycleEndDate);
    }

    [Fact]
    public async Task CalculateLeaveDaysAsync_PropagatesCancellationToken()
    {
        var service = new LeaveService(_leaveRepo.Object, _employeeRepo.Object, null, null);

        using var cts = new CancellationTokenSource();
        cts.Cancel();

        _employeeRepo.Setup(r => r.GetEmployeeByCodeAsync("EMP01", cts.Token))
            .ThrowsAsync(new OperationCanceledException());

        await Assert.ThrowsAsync<OperationCanceledException>(() =>
            service.CalculateLeaveDaysAsync("EMP01", "ANNUAL", new DateTime(2026, 6, 7), new DateTime(2026, 6, 8), 1L, cts.Token));
    }

    [Fact]
    public async Task CalculateLeaveDaysAsync_WhenTimeComponentsPresent_StripsTimeAndCountsDaysAccurately()
    {
        var emp = new Employee { EmployeeCode = "EMP01", HireDate = new DateTime(2020, 1, 1) };
        var leaveType = new LeaveType { LeaveTypeCode = "ANNUAL", TracksBalance = false };

        _employeeRepo.Setup(r => r.GetEmployeeByCodeAsync("EMP01", It.IsAny<CancellationToken>())).ReturnsAsync(emp);
        _leaveRepo.Setup(r => r.GetLeaveTypeByCodeAsync("ANNUAL", It.IsAny<CancellationToken>())).ReturnsAsync(leaveType);

        // Sunday 2026-06-07 14:30:45 to Tuesday 2026-06-09 08:15:00 (3 days: Sun, Mon, Tue)
        var startWithTime = new DateTime(2026, 6, 7, 14, 30, 45);
        var endWithTime = new DateTime(2026, 6, 9, 8, 15, 0);

        var service = new LeaveService(_leaveRepo.Object, _employeeRepo.Object, null, null);
        var result = await service.CalculateLeaveDaysAsync("EMP01", "ANNUAL", startWithTime, endWithTime, 1L);

        Assert.Equal(new DateTime(2026, 6, 7), result.StartDate);
        Assert.Equal(new DateTime(2026, 6, 9), result.EndDate);
        Assert.Equal(3, result.CalendarDays);
        Assert.Equal(3, result.WorkingDays);
        Assert.True(result.HasSufficientBalance);
    }

    [Fact]
    public async Task CalculateLeaveDaysAsync_ExactBoundaryAvailableBalance_OneTenthDeficit_FailsSufficientBalance()
    {
        var emp = new Employee { EmployeeCode = "EMP01", HireDate = new DateTime(2020, 1, 1) };
        var leaveType = new LeaveType { LeaveTypeCode = "ANNUAL", TracksBalance = true };
        var balance = new LeaveBalance
        {
            EmployeeCode = "EMP01",
            LeaveTypeCode = "ANNUAL",
            YearNo = 2026,
            AccruedDays = 2.9m,
            CarriedForwardDays = 0m,
            UsedDays = 0m
        };

        _employeeRepo.Setup(r => r.GetEmployeeByCodeAsync("EMP01", It.IsAny<CancellationToken>())).ReturnsAsync(emp);
        _leaveRepo.Setup(r => r.GetLeaveTypeByCodeAsync("ANNUAL", It.IsAny<CancellationToken>())).ReturnsAsync(leaveType);
        _leaveRepo.Setup(r => r.GetLeaveBalanceByCycleAsync("EMP01", "ANNUAL", It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(balance);
        _leaveRepo.Setup(r => r.GetPendingLeaveDaysAsync("EMP01", "ANNUAL", It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<long?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(0m);

        // 3 working days (Sun, Mon, Tue)
        var start = new DateTime(2026, 6, 7);
        var end = new DateTime(2026, 6, 9);
        var service = new LeaveService(_leaveRepo.Object, _employeeRepo.Object, null, null);

        var result = await service.CalculateLeaveDaysAsync("EMP01", "ANNUAL", start, end, 1L);

        Assert.Equal(2.9m, result.AvailableBalance);
        Assert.Equal(3, result.WorkingDays);
        Assert.False(result.HasSufficientBalance); // 2.9 < 3
    }

    [Fact]
    public async Task CalculateLeaveDaysAsync_FractionalDaysAvailable_ExceedingWorkingDays_PassesSufficientBalance()
    {
        var emp = new Employee { EmployeeCode = "EMP01", HireDate = new DateTime(2020, 1, 1) };
        var leaveType = new LeaveType { LeaveTypeCode = "ANNUAL", TracksBalance = true };
        var balance = new LeaveBalance
        {
            EmployeeCode = "EMP01",
            LeaveTypeCode = "ANNUAL",
            YearNo = 2026,
            AccruedDays = 3.1m,
            CarriedForwardDays = 0m,
            UsedDays = 0m
        };

        _employeeRepo.Setup(r => r.GetEmployeeByCodeAsync("EMP01", It.IsAny<CancellationToken>())).ReturnsAsync(emp);
        _leaveRepo.Setup(r => r.GetLeaveTypeByCodeAsync("ANNUAL", It.IsAny<CancellationToken>())).ReturnsAsync(leaveType);
        _leaveRepo.Setup(r => r.GetLeaveBalanceByCycleAsync("EMP01", "ANNUAL", It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(balance);
        _leaveRepo.Setup(r => r.GetPendingLeaveDaysAsync("EMP01", "ANNUAL", It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<long?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(0m);

        // 3 working days (Sun, Mon, Tue)
        var start = new DateTime(2026, 6, 7);
        var end = new DateTime(2026, 6, 9);
        var service = new LeaveService(_leaveRepo.Object, _employeeRepo.Object, null, null);

        var result = await service.CalculateLeaveDaysAsync("EMP01", "ANNUAL", start, end, 1L);

        Assert.Equal(3.1m, result.AvailableBalance);
        Assert.Equal(3, result.WorkingDays);
        Assert.True(result.HasSufficientBalance); // 3.1 >= 3
    }

    [Fact]
    public async Task CalculateLeaveDaysAsync_WhenBalanceNull_AndPolicyInactive_RejectsIneligibleEmployee()
    {
        var emp = new Employee { EmployeeCode = "EMP01", HireDate = new DateTime(2020, 1, 1) };
        var leaveType = new LeaveType { LeaveTypeCode = "ANNUAL", TracksBalance = true };
        var policy = new LeavePolicy
        {
            LeaveTypeCode = "ANNUAL",
            AnnualEntitlementDays = 21m,
            IsActive = false
        };

        _employeeRepo.Setup(r => r.GetEmployeeByCodeAsync("EMP01", It.IsAny<CancellationToken>())).ReturnsAsync(emp);
        _leaveRepo.Setup(r => r.GetLeaveTypeByCodeAsync("ANNUAL", It.IsAny<CancellationToken>())).ReturnsAsync(leaveType);
        _leaveRepo.Setup(r => r.GetLeavePolicyByTypeCodeAsync("ANNUAL", It.IsAny<CancellationToken>(), It.IsAny<DateTime?>()))
            .ReturnsAsync(policy);
        _leaveRepo.Setup(r => r.GetLeaveBalanceByCycleAsync("EMP01", "ANNUAL", It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((LeaveBalance?)null);
        _leaveRepo.Setup(r => r.GetPendingLeaveDaysAsync("EMP01", "ANNUAL", It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<long?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(0m);

        var start = new DateTime(2026, 6, 7);
        var end = new DateTime(2026, 6, 8); // 2 working days
        var service = new LeaveService(_leaveRepo.Object, _employeeRepo.Object, null, null);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.CalculateLeaveDaysAsync("EMP01", "ANNUAL", start, end, 1L));
        Assert.Contains("not eligible under this leave policy", ex.Message);
    }

    [Fact]
    public async Task CalculateLeaveDaysAsync_WhenBalanceNull_AndEmployeeUnderMinServiceMonths_ReturnsZeroAccrued()
    {
        var emp = new Employee
        {
            EmployeeCode = "EMP01",
            HireDate = DateTime.UtcNow.Date.AddDays(-20) // Only 20 days of service
        };
        var leaveType = new LeaveType { LeaveTypeCode = "ANNUAL", TracksBalance = true };
        var policy = new LeavePolicy
        {
            LeaveTypeCode = "ANNUAL",
            AnnualEntitlementDays = 21m,
            MinServiceMonths = 3, // Requires 3 months
            IsActive = true
        };

        _employeeRepo.Setup(r => r.GetEmployeeByCodeAsync("EMP01", It.IsAny<CancellationToken>())).ReturnsAsync(emp);
        _leaveRepo.Setup(r => r.GetLeaveTypeByCodeAsync("ANNUAL", It.IsAny<CancellationToken>())).ReturnsAsync(leaveType);
        _leaveRepo.Setup(r => r.GetLeavePolicyByTypeCodeAsync("ANNUAL", It.IsAny<CancellationToken>(), It.IsAny<DateTime?>()))
            .ReturnsAsync(policy);
        _leaveRepo.Setup(r => r.GetLeaveBalanceByCycleAsync("EMP01", "ANNUAL", It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((LeaveBalance?)null);
        _leaveRepo.Setup(r => r.GetPendingLeaveDaysAsync("EMP01", "ANNUAL", It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<long?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(0m);

        var start = DateTime.UtcNow.Date.AddDays(1);
        var end = DateTime.UtcNow.Date.AddDays(2);
        var service = new LeaveService(_leaveRepo.Object, _employeeRepo.Object, null, null);

        var result = await service.CalculateLeaveDaysAsync("EMP01", "ANNUAL", start, end, 1L);

        Assert.Equal(0m, result.AvailableBalance);
    }

    [Fact]
    public async Task CalculateLeaveDaysAsync_WhenBalanceNull_MonthlyAccrualMethod_CalculatesProratedAccrual()
    {
        var emp = new Employee
        {
            EmployeeCode = "EMP01",
            HireDate = new DateTime(2020, 1, 1),
            EmploymentStatus = "ACTIVE",
            IsActive = true
        };
        var leaveType = new LeaveType { LeaveTypeCode = "ANNUAL", TracksBalance = true, IsActive = true };
        var policy = new LeavePolicy
        {
            LeaveTypeCode = "ANNUAL",
            AccrualMethod = "MONTHLY",
            AnnualEntitlementDays = 24m, // 2 days per month
            IsActive = true
        };

        _employeeRepo.Setup(r => r.GetEmployeeByCodeAsync("EMP01", It.IsAny<CancellationToken>())).ReturnsAsync(emp);
        _leaveRepo.Setup(r => r.GetLeaveTypeByCodeAsync("ANNUAL", It.IsAny<CancellationToken>())).ReturnsAsync(leaveType);
        _leaveRepo.Setup(r => r.GetLeavePolicyByTypeCodeAsync("ANNUAL", It.IsAny<CancellationToken>(), It.IsAny<DateTime?>()))
            .ReturnsAsync(policy);
        _leaveRepo.Setup(r => r.GetLeaveBalanceByCycleAsync("EMP01", "ANNUAL", It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((LeaveBalance?)null);
        _leaveRepo.Setup(r => r.GetPendingLeaveDaysAsync("EMP01", "ANNUAL", It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<long?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(0m);

        var start = new DateTime(2026, 6, 7);
        var end = new DateTime(2026, 6, 8);
        var service = new LeaveService(_leaveRepo.Object, _employeeRepo.Object, null, null);

        var result = await service.CalculateLeaveDaysAsync("EMP01", "ANNUAL", start, end, 1L);

        // Monthly accrual should yield a positive earned amount for employee with service
        Assert.True(result.AvailableBalance > 0m);
    }

    [Fact]
    public async Task CalculateLeaveDaysAsync_WhenBalanceNull_DailyAccrualMethod_CalculatesProratedAccrual()
    {
        var emp = new Employee
        {
            EmployeeCode = "EMP01",
            HireDate = new DateTime(2020, 1, 1),
            EmploymentStatus = "ACTIVE",
            IsActive = true
        };
        var leaveType = new LeaveType { LeaveTypeCode = "ANNUAL", TracksBalance = true, IsActive = true };
        var policy = new LeavePolicy
        {
            LeaveTypeCode = "ANNUAL",
            AccrualMethod = "DAILY",
            AnnualEntitlementDays = 365m, // 1 day per calendar day
            IsActive = true
        };

        _employeeRepo.Setup(r => r.GetEmployeeByCodeAsync("EMP01", It.IsAny<CancellationToken>())).ReturnsAsync(emp);
        _leaveRepo.Setup(r => r.GetLeaveTypeByCodeAsync("ANNUAL", It.IsAny<CancellationToken>())).ReturnsAsync(leaveType);
        _leaveRepo.Setup(r => r.GetLeavePolicyByTypeCodeAsync("ANNUAL", It.IsAny<CancellationToken>(), It.IsAny<DateTime?>()))
            .ReturnsAsync(policy);
        _leaveRepo.Setup(r => r.GetLeaveBalanceByCycleAsync("EMP01", "ANNUAL", It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((LeaveBalance?)null);
        _leaveRepo.Setup(r => r.GetPendingLeaveDaysAsync("EMP01", "ANNUAL", It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<long?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(0m);

        var start = new DateTime(2026, 6, 7);
        var end = new DateTime(2026, 6, 8);
        var service = new LeaveService(_leaveRepo.Object, _employeeRepo.Object, null, null);

        var result = await service.CalculateLeaveDaysAsync("EMP01", "ANNUAL", start, end, 1L);

        // Daily accrual yields positive earned days proportional to days in cycle
        Assert.True(result.AvailableBalance > 0m);
    }

    [Fact]
    public async Task CalculateLeaveDaysAsync_EmploymentAnniversaryCycle_CalculatesCycleDatesBasedOnHireDate()
    {
        var emp = new Employee
        {
            EmployeeCode = "EMP01",
            HireDate = new DateTime(2022, 4, 15),
            EmploymentStatus = "ACTIVE",
            IsActive = true
        };
        var leaveType = new LeaveType { LeaveTypeCode = "ANNUAL", TracksBalance = true, IsActive = true };
        var policy = new LeavePolicy
        {
            LeaveTypeCode = "ANNUAL",
            CycleBasis = "EMPLOYMENT_ANNIVERSARY",
            AnnualEntitlementDays = 14m,
            IsActive = true
        };

        _employeeRepo.Setup(r => r.GetEmployeeByCodeAsync("EMP01", It.IsAny<CancellationToken>())).ReturnsAsync(emp);
        _leaveRepo.Setup(r => r.GetLeaveTypeByCodeAsync("ANNUAL", It.IsAny<CancellationToken>())).ReturnsAsync(leaveType);
        _leaveRepo.Setup(r => r.GetLeavePolicyByTypeCodeAsync("ANNUAL", It.IsAny<CancellationToken>(), It.IsAny<DateTime?>()))
            .ReturnsAsync(policy);
        _leaveRepo.Setup(r => r.GetLeaveBalanceByCycleAsync("EMP01", "ANNUAL", It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((LeaveBalance?)null);
        _leaveRepo.Setup(r => r.GetPendingLeaveDaysAsync("EMP01", "ANNUAL", It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<long?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(0m);

        // Requested for 2026-06-10 (anniversary cycle is 2026-04-15 to 2027-04-14)
        var start = new DateTime(2026, 6, 10);
        var end = new DateTime(2026, 6, 11);
        var service = new LeaveService(_leaveRepo.Object, _employeeRepo.Object, null, null);

        var result = await service.CalculateLeaveDaysAsync("EMP01", "ANNUAL", start, end, 1L);

        Assert.NotNull(result.CycleStartDate);
        Assert.NotNull(result.CycleEndDate);
        Assert.Equal(4, result.CycleStartDate.Value.Month);
        Assert.Equal(15, result.CycleStartDate.Value.Day);
    }

    [Fact]
    public async Task CalculateLeaveDaysAsync_PropagatesCancellationToken_ToAllDependencies()
    {
        var emp = new Employee { EmployeeCode = "EMP01", HireDate = new DateTime(2020, 1, 1) };
        var leaveType = new LeaveType { LeaveTypeCode = "ANNUAL", TracksBalance = true };
        var balance = new LeaveBalance { AccruedDays = 10m };

        using var cts = new CancellationTokenSource();
        var ct = cts.Token;

        _employeeRepo.Setup(r => r.GetEmployeeByCodeAsync("EMP01", ct)).ReturnsAsync(emp);
        _leaveRepo.Setup(r => r.GetLeaveTypeByCodeAsync("ANNUAL", ct)).ReturnsAsync(leaveType);
        _leaveRepo.Setup(r => r.GetHrLeaveSettingsAsync(ct)).ReturnsAsync(new HrLeaveSettings());
        _leaveRepo.Setup(r => r.GetLeaveBalanceByCycleAsync("EMP01", "ANNUAL", It.IsAny<DateTime>(), ct)).ReturnsAsync(balance);
        _leaveRepo.Setup(r => r.GetPendingLeaveDaysAsync("EMP01", "ANNUAL", It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<long?>(), ct)).ReturnsAsync(0m);
        _calendarService.Setup(c => c.CountWorkingDaysAsync(1L, It.IsAny<DateTime>(), It.IsAny<DateTime>(), ct)).ReturnsAsync(2);

        var service = new LeaveService(_leaveRepo.Object, _employeeRepo.Object, null, _calendarService.Object);
        var result = await service.CalculateLeaveDaysAsync("EMP01", "ANNUAL", new DateTime(2026, 6, 7), new DateTime(2026, 6, 8), 1L, ct);

        Assert.Equal(2, result.WorkingDays);
        _calendarService.Verify(c => c.CountWorkingDaysAsync(1L, It.IsAny<DateTime>(), It.IsAny<DateTime>(), ct), Times.Once);
        _leaveRepo.Verify(r => r.GetLeaveBalanceByCycleAsync("EMP01", "ANNUAL", It.IsAny<DateTime>(), ct), Times.Once);
    }

    [Fact]
    public async Task CalculateLeaveDaysAsync_WhenCalendarThrowsOperationCanceled_PropagatesImmediately()
    {
        var emp = new Employee { EmployeeCode = "EMP01", HireDate = new DateTime(2020, 1, 1) };
        var leaveType = new LeaveType { LeaveTypeCode = "ANNUAL", TracksBalance = false };

        using var cts = new CancellationTokenSource();
        cts.Cancel();

        _employeeRepo.Setup(r => r.GetEmployeeByCodeAsync("EMP01", cts.Token)).ReturnsAsync(emp);
        _leaveRepo.Setup(r => r.GetLeaveTypeByCodeAsync("ANNUAL", cts.Token)).ReturnsAsync(leaveType);
        _calendarService.Setup(c => c.CountWorkingDaysAsync(1L, It.IsAny<DateTime>(), It.IsAny<DateTime>(), cts.Token))
            .ThrowsAsync(new OperationCanceledException());

        var service = new LeaveService(_leaveRepo.Object, _employeeRepo.Object, null, _calendarService.Object);

        await Assert.ThrowsAsync<OperationCanceledException>(() =>
            service.CalculateLeaveDaysAsync("EMP01", "ANNUAL", new DateTime(2026, 6, 7), new DateTime(2026, 6, 8), 1L, cts.Token));
    }

    [Fact]
    public async Task CalculateLeaveDaysAsync_NonLeapYearFebruary_CountsCalendarAndWorkingDaysAccurately()
    {
        var emp = new Employee { EmployeeCode = "EMP01", HireDate = new DateTime(2020, 1, 1) };
        var leaveType = new LeaveType { LeaveTypeCode = "UNPAID", TracksBalance = false };

        _employeeRepo.Setup(r => r.GetEmployeeByCodeAsync("EMP01", It.IsAny<CancellationToken>())).ReturnsAsync(emp);
        _leaveRepo.Setup(r => r.GetLeaveTypeByCodeAsync("UNPAID", It.IsAny<CancellationToken>())).ReturnsAsync(leaveType);

        // 2025 is not leap year: Feb 27 (Thu), Feb 28 (Fri - off), Mar 01 (Sat - off), Mar 02 (Sun - work)
        // 4 calendar days, 2 working days
        var start = new DateTime(2025, 2, 27);
        var end = new DateTime(2025, 3, 2);
        var service = new LeaveService(_leaveRepo.Object, _employeeRepo.Object, null, null);

        var result = await service.CalculateLeaveDaysAsync("EMP01", "UNPAID", start, end, 1L);

        Assert.Equal(4, result.CalendarDays);
        Assert.Equal(2, result.WorkingDays);
        Assert.True(result.HasSufficientBalance);
    }

    [Fact]
    public async Task CalculateLeaveDaysAsync_CustomCompanyId_PassedDirectlyToCalendarService()
    {
        var emp = new Employee { EmployeeCode = "EMP01", HireDate = new DateTime(2020, 1, 1) };
        var leaveType = new LeaveType { LeaveTypeCode = "UNPAID", TracksBalance = false };

        _employeeRepo.Setup(r => r.GetEmployeeByCodeAsync("EMP01", It.IsAny<CancellationToken>())).ReturnsAsync(emp);
        _leaveRepo.Setup(r => r.GetLeaveTypeByCodeAsync("UNPAID", It.IsAny<CancellationToken>())).ReturnsAsync(leaveType);
        _calendarService.Setup(c => c.CountWorkingDaysAsync(42L, It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(3);

        var service = new LeaveService(_leaveRepo.Object, _employeeRepo.Object, null, _calendarService.Object);
        var result = await service.CalculateLeaveDaysAsync("EMP01", "UNPAID", new DateTime(2026, 6, 7), new DateTime(2026, 6, 9), 42L);

        Assert.Equal(3, result.WorkingDays);
        _calendarService.Verify(c => c.CountWorkingDaysAsync(42L, It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task CalculateLeaveDaysAsync_FullMonthLeave_CalculatesCorrectDays()
    {
        var emp = new Employee { EmployeeCode = "EMP01", HireDate = new DateTime(2020, 1, 1) };
        var leaveType = new LeaveType { LeaveTypeCode = "UNPAID", TracksBalance = false };

        _employeeRepo.Setup(r => r.GetEmployeeByCodeAsync("EMP01", It.IsAny<CancellationToken>())).ReturnsAsync(emp);
        _leaveRepo.Setup(r => r.GetLeaveTypeByCodeAsync("UNPAID", It.IsAny<CancellationToken>())).ReturnsAsync(leaveType);

        // April 2026 has 30 days. April 1 is Wednesday, April 30 is Thursday.
        // Weekends: Fri/Sat pairs = Apr 3-4, 10-11, 17-18, 24-25 = 8 weekend days.
        // Working days = 30 - 8 = 22 working days.
        var start = new DateTime(2026, 4, 1);
        var end = new DateTime(2026, 4, 30);
        var service = new LeaveService(_leaveRepo.Object, _employeeRepo.Object, null, null);

        var result = await service.CalculateLeaveDaysAsync("EMP01", "UNPAID", start, end, 1L);

        Assert.Equal(30, result.CalendarDays);
        Assert.Equal(22, result.WorkingDays);
        Assert.True(result.HasSufficientBalance);
    }

    [Fact]
    public async Task CalculateLeaveDaysAsync_CrossYearSpanningLeave_CalculatesCalendarAndWorkingDays()
    {
        var emp = new Employee { EmployeeCode = "EMP01", HireDate = new DateTime(2020, 1, 1) };
        var leaveType = new LeaveType { LeaveTypeCode = "UNPAID", TracksBalance = false };

        _employeeRepo.Setup(r => r.GetEmployeeByCodeAsync("EMP01", It.IsAny<CancellationToken>())).ReturnsAsync(emp);
        _leaveRepo.Setup(r => r.GetLeaveTypeByCodeAsync("UNPAID", It.IsAny<CancellationToken>())).ReturnsAsync(leaveType);

        // 2026-12-28 (Mon) to 2027-01-08 (Fri)
        // 12 calendar days:
        // Week 1: Dec 28 (Mon), Dec 29 (Tue), Dec 30 (Wed), Dec 31 (Thu), Jan 1 (Fri-off), Jan 2 (Sat-off), Jan 3 (Sun) -> 5 working, 2 off
        // Week 2: Jan 4 (Mon), Jan 5 (Tue), Jan 6 (Wed), Jan 7 (Thu), Jan 8 (Fri-off) -> 4 working, 1 off
        // Total working: 9 working days, 3 weekend days
        var start = new DateTime(2026, 12, 28);
        var end = new DateTime(2027, 1, 8);
        var service = new LeaveService(_leaveRepo.Object, _employeeRepo.Object, null, null);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.CalculateLeaveDaysAsync("EMP01", "UNPAID", start, end, 1L));
        Assert.Contains("Split this leave calculation at the leave-year boundary", ex.Message);
    }
}

