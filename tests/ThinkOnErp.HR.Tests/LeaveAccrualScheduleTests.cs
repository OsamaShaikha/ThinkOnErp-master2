using System;
using ThinkOnErp.Infrastructure.Services.Hr;
using Xunit;

namespace ThinkOnErp.HR.Tests;

public sealed class LeaveAccrualScheduleTests
{
    [Fact]
    public void Resolve_UsesPreviousLocalDayAndMonthEndForWestOfUtcTenant()
    {
        var zone = TimeZoneInfo.CreateCustomTimeZone("HR-UTC-07", TimeSpan.FromHours(-7), "HR UTC-07", "HR UTC-07");
        var result = LeaveAccrualSchedule.Resolve(new DateTime(2026, 11, 7, 0, 30, 0, DateTimeKind.Utc), zone);

        Assert.Equal(new DateOnly(2026, 11, 6), result.LocalDate);
        Assert.Equal(new DateOnly(2026, 11, 5), result.LastCompletedLocalDate);
        Assert.Equal(new DateOnly(2026, 10, 31), result.PreviousMonthEnd);
    }

    [Fact]
    public void Resolve_AtUtcYearEndNeverReturnsFutureAccrualDateForFarEastTenant()
    {
        var zone = TimeZoneInfo.CreateCustomTimeZone("HR-UTC+14", TimeSpan.FromHours(14), "HR UTC+14", "HR UTC+14");
        var result = LeaveAccrualSchedule.Resolve(new DateTime(2026, 12, 31, 23, 30, 0, DateTimeKind.Utc), zone);

        Assert.Equal(new DateOnly(2027, 1, 1), result.LocalDate);
        Assert.Equal(new DateOnly(2026, 12, 31), result.LastCompletedLocalDate);
        Assert.Equal(new DateOnly(2026, 12, 31), result.PreviousMonthEnd);
        Assert.True(result.LastCompletedLocalDate <= DateOnly.FromDateTime(new DateTime(2026, 12, 31, 23, 30, 0, DateTimeKind.Utc)));
    }

    [Fact]
    public void Resolve_RejectsUnspecifiedClockKind()
    {
        var zone = TimeZoneInfo.CreateCustomTimeZone("HR-UTC", TimeSpan.Zero, "HR UTC", "HR UTC");
        Assert.Throws<ArgumentException>(() => LeaveAccrualSchedule.Resolve(new DateTime(2026, 11, 7, 0, 0, 0, DateTimeKind.Unspecified), zone));
    }

    [Fact]
    public void Resolve_RejectsLocalClockKind()
    {
        var zone = TimeZoneInfo.CreateCustomTimeZone("HR-UTC", TimeSpan.Zero, "HR UTC", "HR UTC");
        Assert.Throws<ArgumentException>(() => LeaveAccrualSchedule.Resolve(new DateTime(2026, 11, 7, 0, 0, 0, DateTimeKind.Local), zone));
    }

    [Fact]
    public void Resolve_NullTimeZone_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => LeaveAccrualSchedule.Resolve(DateTime.UtcNow, null!));
    }

    [Fact]
    public void Resolve_FirstDayOfMonth_PreviousMonthEndMatchesExactEndOfPriorMonth()
    {
        var zone = TimeZoneInfo.Utc;
        var result = LeaveAccrualSchedule.Resolve(new DateTime(2024, 3, 1, 10, 0, 0, DateTimeKind.Utc), zone);

        Assert.Equal(new DateOnly(2024, 3, 1), result.LocalDate);
        Assert.Equal(new DateOnly(2024, 2, 29), result.LastCompletedLocalDate); // 2024 is leap year
        Assert.Equal(new DateOnly(2024, 2, 29), result.PreviousMonthEnd);
    }

    [Fact]
    public void Resolve_FirstDayOfYear_PreviousMonthEndMatchesDecember31OfPriorYear()
    {
        var zone = TimeZoneInfo.Utc;
        var result = LeaveAccrualSchedule.Resolve(new DateTime(2026, 1, 1, 5, 0, 0, DateTimeKind.Utc), zone);

        Assert.Equal(new DateOnly(2026, 1, 1), result.LocalDate);
        Assert.Equal(new DateOnly(2025, 12, 31), result.LastCompletedLocalDate);
        Assert.Equal(new DateOnly(2025, 12, 31), result.PreviousMonthEnd);
    }

    [Fact]
    public void Resolve_MidMonth_PreviousMonthEndRemainsPriorMonth()
    {
        var zone = TimeZoneInfo.Utc;
        var result = LeaveAccrualSchedule.Resolve(new DateTime(2026, 7, 15, 12, 0, 0, DateTimeKind.Utc), zone);

        Assert.Equal(new DateOnly(2026, 7, 15), result.LocalDate);
        Assert.Equal(new DateOnly(2026, 7, 14), result.LastCompletedLocalDate);
        Assert.Equal(new DateOnly(2026, 6, 30), result.PreviousMonthEnd);
    }

    [Fact]
    public void Resolve_NonLeapYear_FebToMarchTransition_ReturnsFeb28()
    {
        var zone = TimeZoneInfo.Utc;
        var result = LeaveAccrualSchedule.Resolve(new DateTime(2025, 3, 1, 4, 0, 0, DateTimeKind.Utc), zone);

        Assert.Equal(new DateOnly(2025, 3, 1), result.LocalDate);
        Assert.Equal(new DateOnly(2025, 2, 28), result.LastCompletedLocalDate);
        Assert.Equal(new DateOnly(2025, 2, 28), result.PreviousMonthEnd);
    }

    [Fact]
    public void Resolve_LeapYear_OnLeapDay_ReturnsFeb28ForLastCompletedAndJan31ForPreviousMonthEnd()
    {
        var zone = TimeZoneInfo.Utc;
        var result = LeaveAccrualSchedule.Resolve(new DateTime(2024, 2, 29, 12, 0, 0, DateTimeKind.Utc), zone);

        Assert.Equal(new DateOnly(2024, 2, 29), result.LocalDate);
        Assert.Equal(new DateOnly(2024, 2, 28), result.LastCompletedLocalDate);
        Assert.Equal(new DateOnly(2024, 1, 31), result.PreviousMonthEnd);
    }

    [Fact]
    public void Resolve_LeapYear_DayAfterLeapDay_ReturnsFeb29ForBoth()
    {
        var zone = TimeZoneInfo.Utc;
        var result = LeaveAccrualSchedule.Resolve(new DateTime(2024, 3, 1, 0, 1, 0, DateTimeKind.Utc), zone);

        Assert.Equal(new DateOnly(2024, 3, 1), result.LocalDate);
        Assert.Equal(new DateOnly(2024, 2, 29), result.LastCompletedLocalDate);
        Assert.Equal(new DateOnly(2024, 2, 29), result.PreviousMonthEnd);
    }

    [Fact]
    public void Resolve_December31_ReturnsDecember30AndNovember30()
    {
        var zone = TimeZoneInfo.Utc;
        var result = LeaveAccrualSchedule.Resolve(new DateTime(2026, 12, 31, 23, 59, 59, DateTimeKind.Utc), zone);

        Assert.Equal(new DateOnly(2026, 12, 31), result.LocalDate);
        Assert.Equal(new DateOnly(2026, 12, 30), result.LastCompletedLocalDate);
        Assert.Equal(new DateOnly(2026, 11, 30), result.PreviousMonthEnd);
    }

    [Fact]
    public void Resolve_TokyoTimeZone_RollsOverBeforeUtc()
    {
        var tokyo = TimeZoneInfo.CreateCustomTimeZone("JST", TimeSpan.FromHours(9), "JST", "JST");
        var result = LeaveAccrualSchedule.Resolve(new DateTime(2026, 3, 31, 16, 0, 0, DateTimeKind.Utc), tokyo);

        Assert.Equal(new DateOnly(2026, 4, 1), result.LocalDate);
        Assert.Equal(new DateOnly(2026, 3, 31), result.LastCompletedLocalDate);
        Assert.Equal(new DateOnly(2026, 3, 31), result.PreviousMonthEnd);
    }

    [Fact]
    public void Resolve_NewYorkTimeZone_RollsOverAfterUtc()
    {
        var est = TimeZoneInfo.CreateCustomTimeZone("EST", TimeSpan.FromHours(-5), "EST", "EST");
        var result = LeaveAccrualSchedule.Resolve(new DateTime(2026, 4, 1, 2, 0, 0, DateTimeKind.Utc), est);

        Assert.Equal(new DateOnly(2026, 3, 31), result.LocalDate);
        Assert.Equal(new DateOnly(2026, 3, 30), result.LastCompletedLocalDate);
        Assert.Equal(new DateOnly(2026, 2, 28), result.PreviousMonthEnd);
    }

    [Fact]
    public void Resolve_IndiaTimeZone_HalfHourOffsetHandledAccurately()
    {
        var ist = TimeZoneInfo.CreateCustomTimeZone("IST", TimeSpan.FromHours(5.5), "IST", "IST");
        // 2026-06-30 18:45 UTC -> 2026-07-01 00:15 IST
        var result = LeaveAccrualSchedule.Resolve(new DateTime(2026, 6, 30, 18, 45, 0, DateTimeKind.Utc), ist);

        Assert.Equal(new DateOnly(2026, 7, 1), result.LocalDate);
        Assert.Equal(new DateOnly(2026, 6, 30), result.LastCompletedLocalDate);
        Assert.Equal(new DateOnly(2026, 6, 30), result.PreviousMonthEnd);
    }

    [Fact]
    public void Resolve_NepalTimeZone_QuarterHourOffsetHandledAccurately()
    {
        var npt = TimeZoneInfo.CreateCustomTimeZone("NPT", new TimeSpan(5, 45, 0), "NPT", "NPT");
        // 2026-09-30 18:20 UTC -> 2026-10-01 00:05 NPT
        var result = LeaveAccrualSchedule.Resolve(new DateTime(2026, 9, 30, 18, 20, 0, DateTimeKind.Utc), npt);

        Assert.Equal(new DateOnly(2026, 10, 1), result.LocalDate);
        Assert.Equal(new DateOnly(2026, 9, 30), result.LastCompletedLocalDate);
        Assert.Equal(new DateOnly(2026, 9, 30), result.PreviousMonthEnd);
    }

    [Fact]
    public void Resolve_QuarterBoundary_March31ToApril1Transition()
    {
        var zone = TimeZoneInfo.Utc;
        var result = LeaveAccrualSchedule.Resolve(new DateTime(2026, 4, 1, 0, 0, 0, DateTimeKind.Utc), zone);

        Assert.Equal(new DateOnly(2026, 4, 1), result.LocalDate);
        Assert.Equal(new DateOnly(2026, 3, 31), result.LastCompletedLocalDate);
        Assert.Equal(new DateOnly(2026, 3, 31), result.PreviousMonthEnd);
    }

    [Fact]
    public void Resolve_August31ToSeptember1Transition_CalculatesCorrectPreviousMonthEnd()
    {
        var zone = TimeZoneInfo.Utc;
        var result = LeaveAccrualSchedule.Resolve(new DateTime(2026, 9, 1, 0, 0, 0, DateTimeKind.Utc), zone);

        Assert.Equal(new DateOnly(2026, 9, 1), result.LocalDate);
        Assert.Equal(new DateOnly(2026, 8, 31), result.LastCompletedLocalDate);
        Assert.Equal(new DateOnly(2026, 8, 31), result.PreviousMonthEnd);
    }

    [Fact]
    public void Resolve_MidYearBoundary_June30ToJuly1Transition()
    {
        var zone = TimeZoneInfo.Utc;
        var result = LeaveAccrualSchedule.Resolve(new DateTime(2026, 7, 1, 0, 0, 0, DateTimeKind.Utc), zone);

        Assert.Equal(new DateOnly(2026, 7, 1), result.LocalDate);
        Assert.Equal(new DateOnly(2026, 6, 30), result.LastCompletedLocalDate);
        Assert.Equal(new DateOnly(2026, 6, 30), result.PreviousMonthEnd);
    }

    [Fact]
    public void LeaveAccrualRunDates_RecordEqualityAndValueSemantics()
    {
        var dates1 = new LeaveAccrualRunDates(new DateOnly(2026, 11, 7), new DateOnly(2026, 11, 6), new DateOnly(2026, 10, 31));
        var dates2 = new LeaveAccrualRunDates(new DateOnly(2026, 11, 7), new DateOnly(2026, 11, 6), new DateOnly(2026, 10, 31));
        var dates3 = new LeaveAccrualRunDates(new DateOnly(2026, 11, 8), new DateOnly(2026, 11, 7), new DateOnly(2026, 10, 31));

        Assert.Equal(dates1, dates2);
        Assert.NotEqual(dates1, dates3);
        Assert.Equal(dates1.GetHashCode(), dates2.GetHashCode());
    }

    [Theory]
    [InlineData(1, 31)]  // Jan: prior month is Dec (31 days)
    [InlineData(2, 31)]  // Feb: prior month is Jan (31 days)
    [InlineData(3, 28)]  // Mar 2025: prior month is Feb (28 days in non-leap year)
    [InlineData(4, 31)]  // Apr: prior month is Mar (31 days)
    [InlineData(5, 30)]  // May: prior month is Apr (30 days)
    [InlineData(6, 31)]  // Jun: prior month is May (31 days)
    [InlineData(7, 30)]  // Jul: prior month is Jun (30 days)
    [InlineData(8, 31)]  // Aug: prior month is Jul (31 days)
    [InlineData(9, 31)]  // Sep: prior month is Aug (31 days)
    [InlineData(10, 30)] // Oct: prior month is Sep (30 days)
    [InlineData(11, 31)] // Nov: prior month is Oct (31 days)
    [InlineData(12, 30)] // Dec: prior month is Nov (30 days)
    public void Resolve_EachMonthFirstDay_AccuratelyReturnsPriorMonthEnd(int nextMonth, int priorMonthLastDay)
    {
        var zone = TimeZoneInfo.Utc;
        var date = new DateTime(2025, nextMonth, 1, 0, 0, 0, DateTimeKind.Utc);
        var result = LeaveAccrualSchedule.Resolve(date, zone);

        var expectedPriorMonth = nextMonth == 1 ? 12 : nextMonth - 1;
        var expectedPriorYear = nextMonth == 1 ? 2024 : 2025;

        Assert.Equal(new DateOnly(expectedPriorYear, expectedPriorMonth, priorMonthLastDay), result.PreviousMonthEnd);
    }
}
