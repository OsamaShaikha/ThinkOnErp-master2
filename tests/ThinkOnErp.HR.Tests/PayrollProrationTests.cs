using System;
using System.Threading;
using System.Threading.Tasks;
using Moq;
using ThinkOnErp.Application.Services.Hr;
using ThinkOnErp.Domain.Entities.Hr;
using ThinkOnErp.Domain.Interfaces.Hr;
using Xunit;

namespace ThinkOnErp.HR.Tests;

public class PayrollProrationTests
{
    private readonly Mock<IPolicyRepository> _policyRepoMock;
    private readonly Mock<IWorkCalendarService> _calendarServiceMock;
    private readonly PayrollProrationService _service;

    public PayrollProrationTests()
    {
        _policyRepoMock = new Mock<IPolicyRepository>();
        _calendarServiceMock = new Mock<IWorkCalendarService>();
        _service = new PayrollProrationService(_policyRepoMock.Object, _calendarServiceMock.Object);
    }

    [Fact]
    public void FullMonth_ReturnsFactorOfOne()
    {
        var periodStart = new DateTime(2026, 9, 1);
        var periodEnd = new DateTime(2026, 9, 30);
        var hireDate = new DateTime(2020, 1, 1);

        var policy = new ProrationPolicy { Method = "CALENDAR_DAYS", Code = "CAL_DEF" };
        var result = _service.CalculateProrationFactor(periodStart, periodEnd, hireDate, null, policy);

        Assert.Equal(1.000000m, result.ProrationFactor);
        Assert.Equal(30m, result.TotalBaseDays);
        Assert.Equal(30m, result.EligibleDays);
    }

    [Fact]
    public void MidMonthJoiner_CalendarDays_ReturnsExactRatio()
    {
        var periodStart = new DateTime(2026, 9, 1);
        var periodEnd = new DateTime(2026, 9, 30); // 30 days total
        var hireDate = new DateTime(2026, 9, 16); // 15 days eligible (16th through 30th)

        var policy = new ProrationPolicy { Method = "CALENDAR_DAYS", Code = "CAL_DEF" };
        var result = _service.CalculateProrationFactor(periodStart, periodEnd, hireDate, null, policy);

        Assert.Equal(0.500000m, result.ProrationFactor);
        Assert.Equal(30m, result.TotalBaseDays);
        Assert.Equal(15m, result.EligibleDays);
    }

    [Fact]
    public void MidMonthJoiner_Fixed30_ReturnsCorrectFactor()
    {
        var periodStart = new DateTime(2026, 1, 1);
        var periodEnd = new DateTime(2026, 1, 31); // 31 days in Jan
        var hireDate = new DateTime(2026, 1, 16);  // 16 days in Jan, but Fixed 30 caps base at 30

        var policy = new ProrationPolicy { Method = "FIXED_30", Code = "FIXED_30" };
        var result = _service.CalculateProrationFactor(periodStart, periodEnd, hireDate, null, policy);

        Assert.Equal(30m, result.TotalBaseDays);
        Assert.Equal(16m, result.EligibleDays);
        Assert.Equal(Math.Round(16m / 30m, 6), result.ProrationFactor);
    }

    [Fact]
    public async Task WorkingDays_Proration_UsesWorkCalendar()
    {
        var periodStart = new DateTime(2026, 9, 1);
        var periodEnd = new DateTime(2026, 9, 30);
        var hireDate = new DateTime(2026, 9, 15);

        _calendarServiceMock
            .Setup(c => c.CountWorkingDaysAsync(1, periodStart, periodEnd, It.IsAny<CancellationToken>()))
            .ReturnsAsync(22);

        _calendarServiceMock
            .Setup(c => c.CountWorkingDaysAsync(1, new DateTime(2026, 9, 15), periodEnd, It.IsAny<CancellationToken>()))
            .ReturnsAsync(11);

        var policy = new ProrationPolicy { Method = "WORKING_DAYS", Code = "WORK_DAYS" };
        var result = await _service.CalculateProrationFactorAsync(1, periodStart, periodEnd, hireDate, null, policy);

        Assert.Equal(0.500000m, result.ProrationFactor);
        Assert.Equal(22m, result.TotalBaseDays);
        Assert.Equal(11m, result.EligibleDays);
    }

    [Fact]
    public void JoinedAfterPeriodEnd_ReturnsZeroFactor()
    {
        var periodStart = new DateTime(2026, 9, 1);
        var periodEnd = new DateTime(2026, 9, 30);
        var hireDate = new DateTime(2026, 10, 1);

        var policy = new ProrationPolicy { Method = "CALENDAR_DAYS", Code = "CAL_DEF" };
        var result = _service.CalculateProrationFactor(periodStart, periodEnd, hireDate, null, policy);

        Assert.Equal(0.000000m, result.ProrationFactor);
        Assert.Equal(0m, result.EligibleDays);
    }

    [Fact]
    public void MidMonthTermination_CalendarDays_ReturnsExactRatio()
    {
        var periodStart = new DateTime(2026, 9, 1);
        var periodEnd = new DateTime(2026, 9, 30);
        var hireDate = new DateTime(2020, 1, 1);
        var termDate = new DateTime(2026, 9, 15); // 15 days (1st to 15th)

        var policy = new ProrationPolicy { Method = "CALENDAR_DAYS", Code = "CAL_DEF" };
        var result = _service.CalculateProrationFactor(periodStart, periodEnd, hireDate, termDate, policy);

        Assert.Equal(0.500000m, result.ProrationFactor);
        Assert.Equal(30m, result.TotalBaseDays);
        Assert.Equal(15m, result.EligibleDays);
    }

    [Fact]
    public void MidMonthTermination_Fixed30_ReturnsCappedRatio()
    {
        var periodStart = new DateTime(2026, 1, 1);
        var periodEnd = new DateTime(2026, 1, 31);
        var hireDate = new DateTime(2020, 1, 1);
        var termDate = new DateTime(2026, 1, 20); // 20 days

        var policy = new ProrationPolicy { Method = "FIXED_30", Code = "FIXED_30" };
        var result = _service.CalculateProrationFactor(periodStart, periodEnd, hireDate, termDate, policy);

        Assert.Equal(30m, result.TotalBaseDays);
        Assert.Equal(20m, result.EligibleDays);
        Assert.Equal(Math.Round(20m / 30m, 6), result.ProrationFactor);
    }

    [Fact]
    public void HiredAndTerminatedMidMonth_CalculatesExactSpan()
    {
        var periodStart = new DateTime(2026, 5, 1);
        var periodEnd = new DateTime(2026, 5, 31);
        var hireDate = new DateTime(2026, 5, 10);
        var termDate = new DateTime(2026, 5, 20); // 11 days (10th to 20th inclusive)

        var policy = new ProrationPolicy { Method = "CALENDAR_DAYS", Code = "CAL_DEF" };
        var result = _service.CalculateProrationFactor(periodStart, periodEnd, hireDate, termDate, policy);

        Assert.Equal(31m, result.TotalBaseDays);
        Assert.Equal(11m, result.EligibleDays);
        Assert.Equal(Math.Round(11m / 31m, 6), result.ProrationFactor);
    }

    [Fact]
    public void TerminatedBeforePeriodStart_ReturnsZeroFactor()
    {
        var periodStart = new DateTime(2026, 9, 1);
        var periodEnd = new DateTime(2026, 9, 30);
        var hireDate = new DateTime(2020, 1, 1);
        var termDate = new DateTime(2026, 8, 31);

        var policy = new ProrationPolicy { Method = "CALENDAR_DAYS", Code = "CAL_DEF" };
        var result = _service.CalculateProrationFactor(periodStart, periodEnd, hireDate, termDate, policy);

        Assert.Equal(0.000000m, result.ProrationFactor);
        Assert.Equal(0m, result.TotalBaseDays);
        Assert.Equal(0m, result.EligibleDays);
    }

    [Fact]
    public void LeapYear_February_CalendarDays_Uses29DaysBase()
    {
        // 2024 is a leap year (Feb 1 - Feb 29 = 29 days)
        var periodStart = new DateTime(2024, 2, 1);
        var periodEnd = new DateTime(2024, 2, 29);
        var hireDate = new DateTime(2024, 2, 16); // 14 days (16th to 29th)

        var policy = new ProrationPolicy { Method = "CALENDAR_DAYS", Code = "CAL_DEF" };
        var result = _service.CalculateProrationFactor(periodStart, periodEnd, hireDate, null, policy);

        Assert.Equal(29m, result.TotalBaseDays);
        Assert.Equal(14m, result.EligibleDays);
        Assert.Equal(Math.Round(14m / 29m, 6), result.ProrationFactor);
    }

    [Fact]
    public void NonLeapYear_February_CalendarDays_Uses28DaysBase()
    {
        // 2025 is not a leap year (Feb 1 - Feb 28 = 28 days)
        var periodStart = new DateTime(2025, 2, 1);
        var periodEnd = new DateTime(2025, 2, 28);
        var hireDate = new DateTime(2025, 2, 15); // 14 days (15th to 28th)

        var policy = new ProrationPolicy { Method = "CALENDAR_DAYS", Code = "CAL_DEF" };
        var result = _service.CalculateProrationFactor(periodStart, periodEnd, hireDate, null, policy);

        Assert.Equal(28m, result.TotalBaseDays);
        Assert.Equal(14m, result.EligibleDays);
        Assert.Equal(0.500000m, result.ProrationFactor);
    }

    [Fact]
    public void FullMonth_Fixed30_ReturnsFactorOfOneWith30DaysBase()
    {
        var periodStart = new DateTime(2026, 1, 1);
        var periodEnd = new DateTime(2026, 1, 31);
        var hireDate = new DateTime(2020, 1, 1);

        var policy = new ProrationPolicy { Method = "FIXED_30", Code = "FIXED_30" };
        var result = _service.CalculateProrationFactor(periodStart, periodEnd, hireDate, null, policy);

        Assert.Equal(1.000000m, result.ProrationFactor);
        Assert.Equal(30m, result.TotalBaseDays);
        Assert.Equal(30m, result.EligibleDays);
    }

    [Fact]
    public async Task Async_WhenPolicyIsNull_FetchesFromRepository()
    {
        var periodStart = new DateTime(2026, 9, 1);
        var periodEnd = new DateTime(2026, 9, 30);
        var hireDate = new DateTime(2026, 9, 16);

        var repoPolicy = new ProrationPolicy { Method = "FIXED_30", Code = "REPO_FIXED" };
        _policyRepoMock
            .Setup(r => r.GetActiveProrationPolicyAsync(1, periodStart, It.IsAny<CancellationToken>()))
            .ReturnsAsync(repoPolicy);

        var result = await _service.CalculateProrationFactorAsync(1, periodStart, periodEnd, hireDate, null, null);

        Assert.Equal("REPO_FIXED", result.PolicyCode);
        Assert.Equal("FIXED_30", result.MethodUsed);
        Assert.Equal(30m, result.TotalBaseDays);
        Assert.Equal(15m, result.EligibleDays);
    }

    [Fact]
    public async Task Async_WhenPolicyAndRepoReturnNull_DefaultsToCalendarDays()
    {
        var periodStart = new DateTime(2026, 9, 1);
        var periodEnd = new DateTime(2026, 9, 30);
        var hireDate = new DateTime(2026, 9, 16);

        _policyRepoMock
            .Setup(r => r.GetActiveProrationPolicyAsync(1, periodStart, It.IsAny<CancellationToken>()))
            .ReturnsAsync((ProrationPolicy?)null);

        var result = await _service.CalculateProrationFactorAsync(1, periodStart, periodEnd, hireDate, null, null);

        Assert.Equal("DEFAULT_PRORATION", result.PolicyCode);
        Assert.Equal("CALENDAR_DAYS", result.MethodUsed);
        Assert.Equal(0.500000m, result.ProrationFactor);
    }

    [Fact]
    public async Task WorkingDays_WhenCalendarReturnsZero_DefaultsTo22Days()
    {
        var periodStart = new DateTime(2026, 9, 1);
        var periodEnd = new DateTime(2026, 9, 30);
        var hireDate = new DateTime(2026, 9, 15);

        _calendarServiceMock
            .Setup(c => c.CountWorkingDaysAsync(1, periodStart, periodEnd, It.IsAny<CancellationToken>()))
            .ReturnsAsync(0); // 0 or negative defaults to 22

        _calendarServiceMock
            .Setup(c => c.CountWorkingDaysAsync(1, new DateTime(2026, 9, 15), periodEnd, It.IsAny<CancellationToken>()))
            .ReturnsAsync(11);

        var policy = new ProrationPolicy { Method = "WORKING_DAYS", Code = "WORK_DAYS" };
        var result = await _service.CalculateProrationFactorAsync(1, periodStart, periodEnd, hireDate, null, policy);

        Assert.Equal(22m, result.TotalBaseDays);
        Assert.Equal(11m, result.EligibleDays);
        Assert.Equal(0.500000m, result.ProrationFactor);
    }

    [Fact]
    public void UnrecognizedMethod_DefaultsToCalendarDays()
    {
        var periodStart = new DateTime(2026, 9, 1);
        var periodEnd = new DateTime(2026, 9, 30);
        var hireDate = new DateTime(2026, 9, 16);

        var policy = new ProrationPolicy { Method = "NON_EXISTENT_METHOD", Code = "CUSTOM" };
        var result = _service.CalculateProrationFactor(periodStart, periodEnd, hireDate, null, policy);

        Assert.Equal("CALENDAR_DAYS", result.MethodUsed);
        Assert.Equal(0.500000m, result.ProrationFactor);
        Assert.Equal(30m, result.TotalBaseDays);
        Assert.Equal(15m, result.EligibleDays);
    }

    [Fact]
    public void MidMonthTermination_InThirtyOneDayMonth_FixedThirty_CalculatesCorrectProration()
    {
        var periodStart = new DateTime(2026, 1, 1);
        var periodEnd = new DateTime(2026, 1, 31);
        var hireDate = new DateTime(2020, 1, 1);
        var terminationDate = new DateTime(2026, 1, 15);

        var policy = new ProrationPolicy { Method = "FIXED_30", Code = "FIXED_30" };
        var result = _service.CalculateProrationFactor(periodStart, periodEnd, hireDate, terminationDate, policy);

        Assert.Equal(30m, result.TotalBaseDays);
        Assert.Equal(15m, result.EligibleDays);
        Assert.Equal(0.500000m, result.ProrationFactor);
    }

    [Fact]
    public void FullMonthWorked_InThirtyOneDayMonth_FixedThirty_YieldsFactorOfOne()
    {
        var periodStart = new DateTime(2026, 1, 1);
        var periodEnd = new DateTime(2026, 1, 31);
        var hireDate = new DateTime(2020, 1, 1);
        var terminationDate = new DateTime(2026, 1, 31);

        var policy = new ProrationPolicy { Method = "FIXED_30", Code = "FIXED_30" };
        var result = _service.CalculateProrationFactor(periodStart, periodEnd, hireDate, terminationDate, policy);

        Assert.Equal(30m, result.TotalBaseDays);
        Assert.Equal(30m, result.EligibleDays);
        Assert.Equal(1.000000m, result.ProrationFactor);
    }

    [Fact]
    public void HireDateEqualsTerminationDate_YieldsSingleDayProration()
    {
        var periodStart = new DateTime(2026, 9, 1);
        var periodEnd = new DateTime(2026, 9, 30);
        var singleDay = new DateTime(2026, 9, 15);

        var policy = new ProrationPolicy { Method = "CALENDAR_DAYS", Code = "CAL_DEF" };
        var result = _service.CalculateProrationFactor(periodStart, periodEnd, singleDay, singleDay, policy);

        Assert.Equal(30m, result.TotalBaseDays);
        Assert.Equal(1m, result.EligibleDays);
        Assert.Equal(Math.Round(1m / 30m, 6), result.ProrationFactor);
    }

    [Fact]
    public void BothHireAndTerminationInsidePeriod_CalendarDays_CalculatesExactSpan()
    {
        var periodStart = new DateTime(2026, 9, 1);
        var periodEnd = new DateTime(2026, 9, 30);
        var hireDate = new DateTime(2026, 9, 5);
        var terminationDate = new DateTime(2026, 9, 24); // 20 days eligible

        var policy = new ProrationPolicy { Method = "CALENDAR_DAYS", Code = "CAL_DEF" };
        var result = _service.CalculateProrationFactor(periodStart, periodEnd, hireDate, terminationDate, policy);

        Assert.Equal(30m, result.TotalBaseDays);
        Assert.Equal(20m, result.EligibleDays);
        Assert.Equal(Math.Round(20m / 30m, 6), result.ProrationFactor);
    }

    [Fact]
    public void LeapYearFebruary_TwentyNineDays_CalendarDays_YieldsExactRatio()
    {
        var periodStart = new DateTime(2024, 2, 1);
        var periodEnd = new DateTime(2024, 2, 29); // 29 days in leap year
        var hireDate = new DateTime(2024, 2, 15); // 15 days eligible (15th to 29th)

        var policy = new ProrationPolicy { Method = "CALENDAR_DAYS", Code = "CAL_DEF" };
        var result = _service.CalculateProrationFactor(periodStart, periodEnd, hireDate, null, policy);

        Assert.Equal(29m, result.TotalBaseDays);
        Assert.Equal(15m, result.EligibleDays);
        Assert.Equal(Math.Round(15m / 29m, 6), result.ProrationFactor);
    }

    [Fact]
    public void NonLeapYearFebruary_TwentyEightDays_CalendarDays_YieldsExactRatio()
    {
        var periodStart = new DateTime(2025, 2, 1);
        var periodEnd = new DateTime(2025, 2, 28); // 28 days
        var hireDate = new DateTime(2025, 2, 15); // 14 days eligible (15th to 28th)

        var policy = new ProrationPolicy { Method = "CALENDAR_DAYS", Code = "CAL_DEF" };
        var result = _service.CalculateProrationFactor(periodStart, periodEnd, hireDate, null, policy);

        Assert.Equal(28m, result.TotalBaseDays);
        Assert.Equal(14m, result.EligibleDays);
        Assert.Equal(0.500000m, result.ProrationFactor);
    }

    [Fact]
    public void SingleDayPeriod_WhenHiredAndActive_YieldsFactorOfOne()
    {
        var singleDay = new DateTime(2026, 9, 15);
        var hireDate = new DateTime(2024, 1, 1);

        var policy = new ProrationPolicy { Method = "CALENDAR_DAYS", Code = "CAL_DEF" };
        var result = _service.CalculateProrationFactor(singleDay, singleDay, hireDate, null, policy);

        Assert.Equal(1m, result.TotalBaseDays);
        Assert.Equal(1m, result.EligibleDays);
        Assert.Equal(1.000000m, result.ProrationFactor);
    }

    [Fact]
    public async Task WorkingDays_WhenEligibleMatchesTotalWorkingDays_ReturnsFactorOfOne()
    {
        var periodStart = new DateTime(2026, 9, 1);
        var periodEnd = new DateTime(2026, 9, 30);
        var hireDate = new DateTime(2020, 1, 1);

        _calendarServiceMock
            .Setup(c => c.CountWorkingDaysAsync(1, periodStart, periodEnd, It.IsAny<CancellationToken>()))
            .ReturnsAsync(22);

        var policy = new ProrationPolicy { Method = "WORKING_DAYS", Code = "WORK_DAYS" };
        var result = await _service.CalculateProrationFactorAsync(1, periodStart, periodEnd, hireDate, null, policy);

        Assert.Equal(22m, result.TotalBaseDays);
        Assert.Equal(22m, result.EligibleDays);
        Assert.Equal(1.000000m, result.ProrationFactor);
    }

    [Fact]
    public async Task CalculateProrationFactorAsync_PropagatesCancellationToken_ToPolicyRepository()
    {
        using var cts = new CancellationTokenSource();
        var ct = cts.Token;
        var periodStart = new DateTime(2026, 9, 1);
        var periodEnd = new DateTime(2026, 9, 30);
        var hireDate = new DateTime(2020, 1, 1);

        _policyRepoMock
            .Setup(r => r.GetActiveProrationPolicyAsync(1, periodStart, ct))
            .ReturnsAsync(new ProrationPolicy { Method = "CALENDAR_DAYS", Code = "CAL_DEF" });

        var result = await _service.CalculateProrationFactorAsync(1, periodStart, periodEnd, hireDate, null, null, ct);

        Assert.Equal(1.000000m, result.ProrationFactor);
        _policyRepoMock.Verify(r => r.GetActiveProrationPolicyAsync(1, periodStart, ct), Times.Once);
    }

    [Fact]
    public async Task CalculateProrationFactorAsync_WhenCancelled_ThrowsOperationCanceledException()
    {
        using var cts = new CancellationTokenSource();
        cts.Cancel();
        var ct = cts.Token;
        var periodStart = new DateTime(2026, 9, 1);
        var periodEnd = new DateTime(2026, 9, 30);
        var hireDate = new DateTime(2020, 1, 1);

        _policyRepoMock
            .Setup(r => r.GetActiveProrationPolicyAsync(1, periodStart, ct))
            .ThrowsAsync(new OperationCanceledException(ct));

        await Assert.ThrowsAsync<OperationCanceledException>(() =>
            _service.CalculateProrationFactorAsync(1, periodStart, periodEnd, hireDate, null, null, ct));
    }

    [Fact]
    public async Task CalculateProrationFactorAsync_WorkingDays_PropagatesCancellationToken_ToCalendarService()
    {
        using var cts = new CancellationTokenSource();
        var ct = cts.Token;
        var periodStart = new DateTime(2026, 9, 1);
        var periodEnd = new DateTime(2026, 9, 30);
        var hireDate = new DateTime(2026, 9, 15);

        _calendarServiceMock
            .Setup(c => c.CountWorkingDaysAsync(1, periodStart, periodEnd, ct))
            .ReturnsAsync(22);
        _calendarServiceMock
            .Setup(c => c.CountWorkingDaysAsync(1, new DateTime(2026, 9, 15), periodEnd, ct))
            .ReturnsAsync(11);

        var policy = new ProrationPolicy { Method = "WORKING_DAYS", Code = "WORK_DAYS" };
        var result = await _service.CalculateProrationFactorAsync(1, periodStart, periodEnd, hireDate, null, policy, ct);

        Assert.Equal(0.500000m, result.ProrationFactor);
        _calendarServiceMock.Verify(c => c.CountWorkingDaysAsync(1, periodStart, periodEnd, ct), Times.Once);
        _calendarServiceMock.Verify(c => c.CountWorkingDaysAsync(1, new DateTime(2026, 9, 15), periodEnd, ct), Times.Once);
    }

    [Fact]
    public async Task CalculateProrationFactorAsync_WorkingDays_WhenCalendarThrowsOperationCanceled_Propagates()
    {
        using var cts = new CancellationTokenSource();
        cts.Cancel();
        var ct = cts.Token;
        var periodStart = new DateTime(2026, 9, 1);
        var periodEnd = new DateTime(2026, 9, 30);
        var hireDate = new DateTime(2026, 9, 15);

        _calendarServiceMock
            .Setup(c => c.CountWorkingDaysAsync(1, periodStart, periodEnd, ct))
            .ThrowsAsync(new OperationCanceledException(ct));

        var policy = new ProrationPolicy { Method = "WORKING_DAYS", Code = "WORK_DAYS" };
        await Assert.ThrowsAsync<OperationCanceledException>(() =>
            _service.CalculateProrationFactorAsync(1, periodStart, periodEnd, hireDate, null, policy, ct));
    }

    [Fact]
    public async Task WorkingDays_WhenWorkedDaysExceedTotalWorkingDays_CalculatesExactRatio()
    {
        var periodStart = new DateTime(2026, 9, 1);
        var periodEnd = new DateTime(2026, 9, 30);
        var hireDate = new DateTime(2020, 1, 1);

        _calendarServiceMock
            .Setup(c => c.CountWorkingDaysAsync(1, periodStart, periodEnd, It.IsAny<CancellationToken>()))
            .ReturnsAsync(22);

        var policy = new ProrationPolicy { Method = "WORKING_DAYS", Code = "WORK_DAYS" };
        var result = await _service.CalculateProrationFactorAsync(1, periodStart, periodEnd, hireDate, null, policy);

        Assert.Equal(22m, result.TotalBaseDays);
        Assert.Equal(22m, result.EligibleDays);
        Assert.Equal(1.000000m, result.ProrationFactor);
    }

    [Fact]
    public async Task WorkingDays_WhenWorkedDaysIsZero_ReturnsZeroFactorWithTotalBaseDays()
    {
        var periodStart = new DateTime(2026, 9, 1);
        var periodEnd = new DateTime(2026, 9, 30);
        var hireDate = new DateTime(2026, 9, 15);

        _calendarServiceMock
            .Setup(c => c.CountWorkingDaysAsync(1, periodStart, periodEnd, It.IsAny<CancellationToken>()))
            .ReturnsAsync(22);
        _calendarServiceMock
            .Setup(c => c.CountWorkingDaysAsync(1, new DateTime(2026, 9, 15), periodEnd, It.IsAny<CancellationToken>()))
            .ReturnsAsync(0);

        var policy = new ProrationPolicy { Method = "WORKING_DAYS", Code = "WORK_DAYS" };
        var result = await _service.CalculateProrationFactorAsync(1, periodStart, periodEnd, hireDate, null, policy);

        Assert.Equal(22m, result.TotalBaseDays);
        Assert.Equal(0m, result.EligibleDays);
        Assert.Equal(0.000000m, result.ProrationFactor);
    }

    [Theory]
    [InlineData("fixed_30")]
    [InlineData("Fixed_30")]
    [InlineData("FIXED_30")]
    public void FixedThirty_CasingInsensitive_YieldsSameResult(string method)
    {
        var periodStart = new DateTime(2026, 1, 1);
        var periodEnd = new DateTime(2026, 1, 31);
        var hireDate = new DateTime(2026, 1, 16);

        var policy = new ProrationPolicy { Method = method, Code = "FIXED_TEST" };
        var result = _service.CalculateProrationFactor(periodStart, periodEnd, hireDate, null, policy);

        Assert.Equal("FIXED_30", result.MethodUsed);
        Assert.Equal(30m, result.TotalBaseDays);
        Assert.Equal(16m, result.EligibleDays);
        Assert.Equal(Math.Round(16m / 30m, 6), result.ProrationFactor);
    }

    [Theory]
    [InlineData("calendar_days")]
    [InlineData("Calendar_Days")]
    [InlineData("CALENDAR_DAYS")]
    public void CalendarDays_CasingInsensitive_YieldsSameResult(string method)
    {
        var periodStart = new DateTime(2026, 9, 1);
        var periodEnd = new DateTime(2026, 9, 30);
        var hireDate = new DateTime(2026, 9, 16);

        var policy = new ProrationPolicy { Method = method, Code = "CAL_TEST" };
        var result = _service.CalculateProrationFactor(periodStart, periodEnd, hireDate, null, policy);

        Assert.Equal("CALENDAR_DAYS", result.MethodUsed);
        Assert.Equal(30m, result.TotalBaseDays);
        Assert.Equal(15m, result.EligibleDays);
        Assert.Equal(0.500000m, result.ProrationFactor);
    }

    [Fact]
    public void FixedThirty_LeapYearFebruary_MidMonthJoiner_CalculatesCorrectProration()
    {
        // 2024 is leap year: Feb 1 to Feb 29 (29 days)
        var periodStart = new DateTime(2024, 2, 1);
        var periodEnd = new DateTime(2024, 2, 29);
        var hireDate = new DateTime(2024, 2, 10); // 20 days eligible (10th to 29th)

        var policy = new ProrationPolicy { Method = "FIXED_30", Code = "FIXED_30" };
        var result = _service.CalculateProrationFactor(periodStart, periodEnd, hireDate, null, policy);

        Assert.Equal(30m, result.TotalBaseDays);
        Assert.Equal(20m, result.EligibleDays);
        Assert.Equal(Math.Round(20m / 30m, 6), result.ProrationFactor);
    }

    [Fact]
    public void FixedThirty_NonLeapYearFebruary_MidMonthJoiner_CalculatesCorrectProration()
    {
        // 2025 is non-leap year: Feb 1 to Feb 28 (28 days)
        var periodStart = new DateTime(2025, 2, 1);
        var periodEnd = new DateTime(2025, 2, 28);
        var hireDate = new DateTime(2025, 2, 10); // 19 days eligible (10th to 28th)

        var policy = new ProrationPolicy { Method = "FIXED_30", Code = "FIXED_30" };
        var result = _service.CalculateProrationFactor(periodStart, periodEnd, hireDate, null, policy);

        Assert.Equal(30m, result.TotalBaseDays);
        Assert.Equal(19m, result.EligibleDays);
        Assert.Equal(Math.Round(19m / 30m, 6), result.ProrationFactor);
    }

    [Fact]
    public void FixedThirty_ThirtyOneDayMonth_MidMonthJoiner_CappedAtThirty()
    {
        // Month has 31 days (e.g. October). Joiner on Day 2 -> 30 days worked.
        var periodStart = new DateTime(2026, 10, 1);
        var periodEnd = new DateTime(2026, 10, 31);
        var hireDate = new DateTime(2026, 10, 2); // 30 days (Oct 2 to Oct 31)

        var policy = new ProrationPolicy { Method = "FIXED_30", Code = "FIXED_30" };
        var result = _service.CalculateProrationFactor(periodStart, periodEnd, hireDate, null, policy);

        Assert.Equal(30m, result.TotalBaseDays);
        Assert.Equal(30m, result.EligibleDays);
        Assert.Equal(1.000000m, result.ProrationFactor);
    }

    [Fact]
    public void CalendarDays_ThirtyOneDayMonth_SpanComplementsSumToOne()
    {
        // Month has 31 days (August 2026). Part 1: Aug 1 to Aug 15 (15 days), Part 2: Aug 16 to Aug 31 (16 days).
        var periodStart = new DateTime(2026, 8, 1);
        var periodEnd = new DateTime(2026, 8, 31);
        var policy = new ProrationPolicy { Method = "CALENDAR_DAYS", Code = "CAL_DEF" };

        var part1 = _service.CalculateProrationFactor(periodStart, periodEnd, new DateTime(2020, 1, 1), new DateTime(2026, 8, 15), policy);
        var part2 = _service.CalculateProrationFactor(periodStart, periodEnd, new DateTime(2026, 8, 16), null, policy);

        Assert.Equal(31m, part1.TotalBaseDays);
        Assert.Equal(15m, part1.EligibleDays);
        Assert.Equal(31m, part2.TotalBaseDays);
        Assert.Equal(16m, part2.EligibleDays);
        Assert.Equal(1.000000m, part1.ProrationFactor + part2.ProrationFactor);
    }

    [Fact]
    public void CalendarDays_ThirtyDayMonth_SpanComplementsSumToOne()
    {
        // Month has 30 days (September 2026). Part 1: Sep 1 to Sep 15 (15 days), Part 2: Sep 16 to Sep 30 (15 days).
        var periodStart = new DateTime(2026, 9, 1);
        var periodEnd = new DateTime(2026, 9, 30);
        var policy = new ProrationPolicy { Method = "CALENDAR_DAYS", Code = "CAL_DEF" };

        var part1 = _service.CalculateProrationFactor(periodStart, periodEnd, new DateTime(2020, 1, 1), new DateTime(2026, 9, 15), policy);
        var part2 = _service.CalculateProrationFactor(periodStart, periodEnd, new DateTime(2026, 9, 16), null, policy);

        Assert.Equal(30m, part1.TotalBaseDays);
        Assert.Equal(15m, part1.EligibleDays);
        Assert.Equal(30m, part2.TotalBaseDays);
        Assert.Equal(15m, part2.EligibleDays);
        Assert.Equal(1.000000m, part1.ProrationFactor + part2.ProrationFactor);
    }

    [Fact]
    public void CalendarDays_WhenHiredOnExactLastDayOfPeriod_YieldsSingleDayRatio()
    {
        var periodStart = new DateTime(2026, 9, 1);
        var periodEnd = new DateTime(2026, 9, 30);
        var hireDate = new DateTime(2026, 9, 30);

        var policy = new ProrationPolicy { Method = "CALENDAR_DAYS", Code = "CAL_DEF" };
        var result = _service.CalculateProrationFactor(periodStart, periodEnd, hireDate, null, policy);

        Assert.Equal(30m, result.TotalBaseDays);
        Assert.Equal(1m, result.EligibleDays);
        Assert.Equal(Math.Round(1m / 30m, 6), result.ProrationFactor);
    }

    [Fact]
    public void CalendarDays_WhenTerminatedOnExactFirstDayOfPeriod_YieldsSingleDayRatio()
    {
        var periodStart = new DateTime(2026, 9, 1);
        var periodEnd = new DateTime(2026, 9, 30);
        var hireDate = new DateTime(2020, 1, 1);
        var termDate = new DateTime(2026, 9, 1);

        var policy = new ProrationPolicy { Method = "CALENDAR_DAYS", Code = "CAL_DEF" };
        var result = _service.CalculateProrationFactor(periodStart, periodEnd, hireDate, termDate, policy);

        Assert.Equal(30m, result.TotalBaseDays);
        Assert.Equal(1m, result.EligibleDays);
        Assert.Equal(Math.Round(1m / 30m, 6), result.ProrationFactor);
    }

    [Fact]
    public void FixedThirty_WhenHiredOnExactLastDayOfPeriod_YieldsSingleDayRatio()
    {
        var periodStart = new DateTime(2026, 9, 1);
        var periodEnd = new DateTime(2026, 9, 30);
        var hireDate = new DateTime(2026, 9, 30);

        var policy = new ProrationPolicy { Method = "FIXED_30", Code = "FIXED_30" };
        var result = _service.CalculateProrationFactor(periodStart, periodEnd, hireDate, null, policy);

        Assert.Equal(30m, result.TotalBaseDays);
        Assert.Equal(1m, result.EligibleDays);
        Assert.Equal(Math.Round(1m / 30m, 6), result.ProrationFactor);
    }

    [Fact]
    public void FixedThirty_WhenTerminatedOnExactFirstDayOfPeriod_YieldsSingleDayRatio()
    {
        var periodStart = new DateTime(2026, 9, 1);
        var periodEnd = new DateTime(2026, 9, 30);
        var hireDate = new DateTime(2020, 1, 1);
        var termDate = new DateTime(2026, 9, 1);

        var policy = new ProrationPolicy { Method = "FIXED_30", Code = "FIXED_30" };
        var result = _service.CalculateProrationFactor(periodStart, periodEnd, hireDate, termDate, policy);

        Assert.Equal(30m, result.TotalBaseDays);
        Assert.Equal(1m, result.EligibleDays);
        Assert.Equal(Math.Round(1m / 30m, 6), result.ProrationFactor);
    }

    [Fact]
    public void DatesWithTimeComponents_StrippedToDateOnly_DoesNotDistortDayCounts()
    {
        var periodStart = new DateTime(2026, 9, 1, 8, 30, 0);
        var periodEnd = new DateTime(2026, 9, 30, 17, 45, 0);
        var hireDate = new DateTime(2026, 9, 16, 23, 59, 59);

        var policy = new ProrationPolicy { Method = "CALENDAR_DAYS", Code = "CAL_DEF" };
        var result = _service.CalculateProrationFactor(periodStart, periodEnd, hireDate, null, policy);

        Assert.Equal(30m, result.TotalBaseDays);
        Assert.Equal(15m, result.EligibleDays);
        Assert.Equal(0.500000m, result.ProrationFactor);
    }

    [Fact]
    public void BothHireAndTerminationAfterPeriodEnd_ReturnsZeroFactor()
    {
        var periodStart = new DateTime(2026, 9, 1);
        var periodEnd = new DateTime(2026, 9, 30);
        var hireDate = new DateTime(2026, 10, 5);
        var termDate = new DateTime(2026, 10, 20);

        var policy = new ProrationPolicy { Method = "CALENDAR_DAYS", Code = "CAL_DEF" };
        var result = _service.CalculateProrationFactor(periodStart, periodEnd, hireDate, termDate, policy);

        Assert.Equal(0.000000m, result.ProrationFactor);
        Assert.Equal(0m, result.TotalBaseDays);
        Assert.Equal(0m, result.EligibleDays);
    }

    [Fact]
    public void BothHireAndTerminationBeforePeriodStart_ReturnsZeroFactor()
    {
        var periodStart = new DateTime(2026, 9, 1);
        var periodEnd = new DateTime(2026, 9, 30);
        var hireDate = new DateTime(2026, 8, 1);
        var termDate = new DateTime(2026, 8, 25);

        var policy = new ProrationPolicy { Method = "CALENDAR_DAYS", Code = "CAL_DEF" };
        var result = _service.CalculateProrationFactor(periodStart, periodEnd, hireDate, termDate, policy);

        Assert.Equal(0.000000m, result.ProrationFactor);
        Assert.Equal(0m, result.TotalBaseDays);
        Assert.Equal(0m, result.EligibleDays);
    }
}
