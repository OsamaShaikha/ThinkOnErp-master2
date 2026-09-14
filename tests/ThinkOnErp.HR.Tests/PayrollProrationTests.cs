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
}
