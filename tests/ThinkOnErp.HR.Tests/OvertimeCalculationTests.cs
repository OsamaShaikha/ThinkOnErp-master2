using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Moq;
using ThinkOnErp.Application.DTOs.Hr;
using ThinkOnErp.Application.Services.Hr;
using ThinkOnErp.Domain.Entities.Hr;
using ThinkOnErp.Domain.Interfaces.Hr;
using Xunit;

namespace ThinkOnErp.HR.Tests;

public class OvertimeCalculationTests
{
    private readonly Mock<IPolicyRepository> _policyRepoMock = new();
    private readonly OvertimeCalculationService _service;

    public OvertimeCalculationTests()
    {
        _service = new OvertimeCalculationService(_policyRepoMock.Object);
    }

    #region DeriveHourlyWage Tests

    [Theory]
    [InlineData(1200, 240, 5.0000)]
    [InlineData(2400, 240, 10.0000)]
    [InlineData(1000, 200, 5.0000)]
    [InlineData(850, 160, 5.3125)]
    [InlineData(1000, 240, 4.1667)]
    public void DeriveHourlyWage_CalculatesAccurately_WithConfiguredHours(decimal salary, int hours, decimal expectedWage)
    {
        var wage = _service.DeriveHourlyWage(salary, hours);
        Assert.Equal(expectedWage, wage);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(-240)]
    public void DeriveHourlyWage_WhenStandardHoursZeroOrNegative_DefaultsToTwoHundredForty(int invalidHours)
    {
        var wage = _service.DeriveHourlyWage(1200m, invalidHours);
        Assert.Equal(5.0000m, wage); // 1200 / 240 = 5.0000
    }

    [Fact]
    public void DeriveHourlyWage_DefaultStandardHoursIs240()
    {
        var wage = _service.DeriveHourlyWage(2400m);
        Assert.Equal(10.0000m, wage);
    }

    [Fact]
    public void DeriveHourlyWage_WhenSalaryZeroOrNegative_ReturnsZeroOrNegative()
    {
        Assert.Equal(0.0000m, _service.DeriveHourlyWage(0m));
        Assert.Equal(-5.0000m, _service.DeriveHourlyWage(-1200m));
    }

    #endregion

    #region CalculateOvertimeEarningsAsync - Guard Tests

    [Theory]
    [InlineData(0)]
    [InlineData(-1.5)]
    [InlineData(-10)]
    public async Task CalculateOvertimeEarningsAsync_WhenOvertimeHoursZeroOrNegative_ReturnsZero(decimal hours)
    {
        var earnings = await _service.CalculateOvertimeEarningsAsync(1L, 1000m, hours, "REGULAR", new DateTime(2026, 11, 10));
        Assert.Equal(0.00m, earnings);
        _policyRepoMock.Verify(r => r.GetActiveOvertimeRulesAsync(It.IsAny<long>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-500)]
    public async Task CalculateOvertimeEarningsAsync_WhenBaseSalaryZeroOrNegative_ReturnsZero(decimal salary)
    {
        var earnings = await _service.CalculateOvertimeEarningsAsync(1L, salary, 4m, "REGULAR", new DateTime(2026, 11, 10));
        Assert.Equal(0.00m, earnings);
        _policyRepoMock.Verify(r => r.GetActiveOvertimeRulesAsync(It.IsAny<long>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    #endregion

    #region Default Multipliers Tests (When No Rule Configured)

    [Fact]
    public async Task CalculateOvertimeEarningsAsync_WhenNoRulesConfigured_RegularDay_AppliesOnePointTwentyFiveMultiplier()
    {
        // 1200 salary / 240 = 5.0000 wage. 4 hours * 5.0000 * 1.25 = 25.000
        _policyRepoMock.Setup(r => r.GetActiveOvertimeRulesAsync(1L, It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<OvertimeRule>());

        var earnings = await _service.CalculateOvertimeEarningsAsync(1L, 1200m, 4m, "REGULAR", new DateTime(2026, 11, 10));
        Assert.Equal(25.000m, earnings);
    }

    [Fact]
    public async Task CalculateOvertimeEarningsAsync_WhenNoRulesConfigured_WeekendDay_AppliesOnePointFiveMultiplier()
    {
        // 1200 salary / 240 = 5.0000 wage. 4 hours * 5.0000 * 1.50 = 30.000
        _policyRepoMock.Setup(r => r.GetActiveOvertimeRulesAsync(1L, It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<OvertimeRule>());

        var earnings = await _service.CalculateOvertimeEarningsAsync(1L, 1200m, 4m, "WEEKEND", new DateTime(2026, 11, 14));
        Assert.Equal(30.000m, earnings);
    }

    [Fact]
    public async Task CalculateOvertimeEarningsAsync_WhenNoRulesConfigured_HolidayDay_AppliesTwoPointZeroMultiplier()
    {
        // 1200 salary / 240 = 5.0000 wage. 4 hours * 5.0000 * 2.00 = 40.000
        _policyRepoMock.Setup(r => r.GetActiveOvertimeRulesAsync(1L, It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<OvertimeRule>());

        var earnings = await _service.CalculateOvertimeEarningsAsync(1L, 1200m, 4m, "HOLIDAY", new DateTime(2026, 11, 15));
        Assert.Equal(40.000m, earnings);
    }

    [Theory]
    [InlineData("regular")]
    [InlineData("Regular")]
    [InlineData("REGULAR")]
    [InlineData("normal")]
    [InlineData("OTHER")]
    public async Task CalculateOvertimeEarningsAsync_WhenUnknownOrRegularDayType_DefaultsToOnePointTwentyFive(string dayType)
    {
        _policyRepoMock.Setup(r => r.GetActiveOvertimeRulesAsync(1L, It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<OvertimeRule>());

        var earnings = await _service.CalculateOvertimeEarningsAsync(1L, 1200m, 2m, dayType, new DateTime(2026, 11, 10));
        Assert.Equal(12.500m, earnings); // 2 * 5 * 1.25 = 12.5
    }

    #endregion

    #region Custom Overtime Rules Tests

    [Fact]
    public async Task CalculateOvertimeEarningsAsync_WhenCustomRuleMatched_AppliesCustomMultiplier()
    {
        var customRules = new List<OvertimeRule>
        {
            new()
            {
                CompanyId = 1L,
                Code = "OT_REG_135",
                DayType = "REGULAR",
                Multiplier = 1.35m,
                IsActive = true
            },
            new()
            {
                CompanyId = 1L,
                Code = "OT_WKD_175",
                DayType = "WEEKEND",
                Multiplier = 1.75m,
                IsActive = true
            }
        };

        _policyRepoMock.Setup(r => r.GetActiveOvertimeRulesAsync(1L, It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(customRules);

        // Regular with 1.35x: 1200 / 240 = 5.0000 wage * 4 * 1.35 = 27.000m
        var regEarnings = await _service.CalculateOvertimeEarningsAsync(1L, 1200m, 4m, "REGULAR", new DateTime(2026, 11, 10));
        Assert.Equal(27.000m, regEarnings);

        // Weekend with 1.75x: 5.0000 wage * 4 * 1.75 = 35.000m
        var wkdEarnings = await _service.CalculateOvertimeEarningsAsync(1L, 1200m, 4m, "WEEKEND", new DateTime(2026, 11, 14));
        Assert.Equal(35.000m, wkdEarnings);
    }

    [Fact]
    public async Task CalculateOvertimeEarningsAsync_MatchingIsCaseInsensitive()
    {
        var customRules = new List<OvertimeRule>
        {
            new()
            {
                CompanyId = 1L,
                Code = "OT_CUSTOM",
                DayType = "SpecialNight",
                Multiplier = 2.25m,
                IsActive = true
            }
        };

        _policyRepoMock.Setup(r => r.GetActiveOvertimeRulesAsync(1L, It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(customRules);

        var earnings = await _service.CalculateOvertimeEarningsAsync(1L, 1200m, 2m, "specialnight", new DateTime(2026, 11, 10));
        Assert.Equal(22.500m, earnings); // 2 * 5 * 2.25 = 22.500
    }

    [Fact]
    public async Task CalculateOvertimeEarningsAsync_PropagatesCancellationToken()
    {
        using var cts = new CancellationTokenSource();
        var ct = cts.Token;

        _policyRepoMock.Setup(r => r.GetActiveOvertimeRulesAsync(1L, It.IsAny<DateTime>(), ct))
            .ReturnsAsync(new List<OvertimeRule>());

        var result = await _service.CalculateOvertimeEarningsAsync(1L, 1200m, 2m, "REGULAR", new DateTime(2026, 11, 10), ct);

        Assert.Equal(12.500m, result);
        _policyRepoMock.Verify(r => r.GetActiveOvertimeRulesAsync(1L, It.IsAny<DateTime>(), ct), Times.Once);
    }

    [Fact]
    public async Task CalculateOvertimeEarningsAsync_WhenCanceled_ThrowsOperationCanceledException()
    {
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        _policyRepoMock.Setup(r => r.GetActiveOvertimeRulesAsync(1L, It.IsAny<DateTime>(), cts.Token))
            .ThrowsAsync(new OperationCanceledException());

        await Assert.ThrowsAsync<OperationCanceledException>(() =>
            _service.CalculateOvertimeEarningsAsync(1L, 1200m, 2m, "REGULAR", new DateTime(2026, 11, 10), cts.Token));
    }

    #endregion

    #region Attendance Punch to Overtime Hours Integration (ScheduledAttendanceCalculator)

    private static TimedShiftDto CreateStandardShift(TimeSpan start, TimeSpan end, int breakMinutes = 60)
    {
        var workDate = new DateOnly(2026, 11, 10);
        var workDateTime = new DateTime(2026, 11, 10, 0, 0, 0, DateTimeKind.Utc);
        var startUtc = workDateTime.Add(start);
        var endUtc = workDateTime.Add(end);
        var elapsed = (decimal)(endUtc - startUtc).TotalMinutes;
        var scheduledMinutes = elapsed - breakMinutes;

        var shiftDto = new EffectiveShiftDto(
            "EMP_OT",
            workDate,
            "WORKING",
            "ROSTER",
            "DAY",
            1L,
            null,
            null,
            null,
            breakMinutes,
            scheduledMinutes,
            Math.Round(scheduledMinutes / 60m, 2)
        );

        return new TimedShiftDto(
            shiftDto,
            "Asia/Amman",
            startUtc,
            endUtc,
            elapsed
        );
    }

    [Fact]
    public void AttendanceOvertime_WhenWorkedExactShiftHours_OvertimeIsZero()
    {
        // Shift 08:00 to 17:00 (9 hours gross - 1 hour break = 8 hours net = 480 mins)
        var schedule = CreateStandardShift(new TimeSpan(8, 0, 0), new TimeSpan(17, 0, 0), 60);
        var policy = new AttendancePolicy { MinMinutesForOvertime = 30 };

        var checkIn = new DateTime(2026, 11, 10, 8, 0, 0, DateTimeKind.Utc);
        var checkOut = new DateTime(2026, 11, 10, 17, 0, 0, DateTimeKind.Utc);

        var day = ScheduledAttendanceCalculator.Calculate(
            "EMP_OT",
            new DateOnly(2026, 11, 10),
            checkIn,
            checkOut,
            schedule,
            policy
        );

        Assert.Equal(8.00m, day.ScheduledHours);
        Assert.Equal(8.00m, day.ActualWorkedHours);
        Assert.Equal(0.00m, day.OvertimeHours);
    }

    [Fact]
    public void AttendanceOvertime_WhenExtraMinutesBelowTolerance_OvertimeRemainsZero()
    {
        // Worked 15 mins extra (495 mins net vs 480 scheduled) -> 15 mins < MinMinutesForOvertime (30)
        var schedule = CreateStandardShift(new TimeSpan(8, 0, 0), new TimeSpan(17, 0, 0), 60);
        var policy = new AttendancePolicy { MinMinutesForOvertime = 30 };

        var checkIn = new DateTime(2026, 11, 10, 8, 0, 0, DateTimeKind.Utc);
        var checkOut = new DateTime(2026, 11, 10, 17, 15, 0, DateTimeKind.Utc); // 15 mins extra

        var day = ScheduledAttendanceCalculator.Calculate(
            "EMP_OT",
            new DateOnly(2026, 11, 10),
            checkIn,
            checkOut,
            schedule,
            policy
        );

        Assert.Equal(8.00m, day.ScheduledHours);
        Assert.Equal(8.25m, day.ActualWorkedHours);
        Assert.Equal(0.00m, day.OvertimeHours); // Below 30 mins threshold
    }

    [Fact]
    public void AttendanceOvertime_WhenExtraMinutesMeetOrExceedTolerance_OvertimeCalculatedAccurately()
    {
        // Worked 90 mins extra (570 mins net vs 480 scheduled) -> 90 mins >= 30 mins -> 1.5 hours OT
        var schedule = CreateStandardShift(new TimeSpan(8, 0, 0), new TimeSpan(17, 0, 0), 60);
        var policy = new AttendancePolicy { MinMinutesForOvertime = 30 };

        var checkIn = new DateTime(2026, 11, 10, 8, 0, 0, DateTimeKind.Utc);
        var checkOut = new DateTime(2026, 11, 10, 18, 30, 0, DateTimeKind.Utc); // 90 mins extra

        var day = ScheduledAttendanceCalculator.Calculate(
            "EMP_OT",
            new DateOnly(2026, 11, 10),
            checkIn,
            checkOut,
            schedule,
            policy
        );

        Assert.Equal(8.00m, day.ScheduledHours);
        Assert.Equal(9.50m, day.ActualWorkedHours);
        Assert.Equal(1.50m, day.OvertimeHours);
    }

    [Fact]
    public void AttendanceOvertime_WhenEarlyCheckInAndLateCheckOutCombined_CalculatesTotalExtraHours()
    {
        // Shift 08:00 - 17:00 (480 mins net)
        // CheckIn 07:30 (30 mins early) + CheckOut 18:30 (90 mins late) = 120 mins extra = 2.00 hours OT
        var schedule = CreateStandardShift(new TimeSpan(8, 0, 0), new TimeSpan(17, 0, 0), 60);
        var policy = new AttendancePolicy { MinMinutesForOvertime = 30 };

        var checkIn = new DateTime(2026, 11, 10, 7, 30, 0, DateTimeKind.Utc);
        var checkOut = new DateTime(2026, 11, 10, 18, 30, 0, DateTimeKind.Utc);

        var day = ScheduledAttendanceCalculator.Calculate(
            "EMP_OT",
            new DateOnly(2026, 11, 10),
            checkIn,
            checkOut,
            schedule,
            policy
        );

        Assert.Equal(8.00m, day.ScheduledHours);
        Assert.Equal(10.00m, day.ActualWorkedHours);
        Assert.Equal(2.00m, day.OvertimeHours);
    }

    [Fact]
    public void AttendanceOvertime_OnRestDay_DoesNotGenerateScheduledOvertime()
    {
        // Rest day punches are treated as evidence, not scheduled work
        var shiftDto = new EffectiveShiftDto(
            "EMP_REST",
            new DateOnly(2026, 11, 13),
            "REST_DAY",
            "ROSTER",
            "REST",
            null,
            null,
            null,
            null,
            0,
            0m,
            0m
        );

        var schedule = new TimedShiftDto(shiftDto, "Asia/Amman", null, null, null);
        var policy = new AttendancePolicy { MinMinutesForOvertime = 30 };

        var checkIn = new DateTime(2026, 11, 13, 9, 0, 0, DateTimeKind.Utc);
        var checkOut = new DateTime(2026, 11, 13, 14, 0, 0, DateTimeKind.Utc);

        var day = ScheduledAttendanceCalculator.Calculate(
            "EMP_REST",
            new DateOnly(2026, 11, 13),
            checkIn,
            checkOut,
            schedule,
            policy
        );

        Assert.Equal(0.00m, day.ScheduledHours);
        Assert.Equal(0.00m, day.OvertimeHours);
    }

    [Fact]
    public void AttendanceOvertime_NegativeTolerance_ThrowsInvalidOperationException()
    {
        var schedule = CreateStandardShift(new TimeSpan(8, 0, 0), new TimeSpan(17, 0, 0), 60);
        var policy = new AttendancePolicy { MinMinutesForOvertime = -10 };

        var checkIn = new DateTime(2026, 11, 10, 8, 0, 0, DateTimeKind.Utc);
        var checkOut = new DateTime(2026, 11, 10, 17, 0, 0, DateTimeKind.Utc);

        var ex = Assert.Throws<InvalidOperationException>(() =>
            ScheduledAttendanceCalculator.Calculate(
                "EMP_OT",
                new DateOnly(2026, 11, 10),
                checkIn,
                checkOut,
                schedule,
                policy
            ));

        Assert.Contains("tolerances cannot be negative", ex.Message);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task CalculateOvertimeEarningsAsync_WhenDayTypeNullOrWhitespace_DefaultsToOnePointTwentyFive(string? dayType)
    {
        _policyRepoMock.Setup(r => r.GetActiveOvertimeRulesAsync(1L, It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<OvertimeRule>());

        var earnings = await _service.CalculateOvertimeEarningsAsync(1L, 1200m, 4m, dayType!, new DateTime(2026, 11, 10));
        Assert.Equal(25.000m, earnings); // 4 * 5.0000 * 1.25 = 25.000m
    }

    [Fact]
    public async Task CalculateOvertimeEarningsAsync_WhenDayTypeHasWhitespace_TrimsAndMatchesRule()
    {
        var rules = new List<OvertimeRule>
        {
            new() { CompanyId = 1L, Code = "OT_WKD", DayType = "WEEKEND", Multiplier = 1.80m, IsActive = true }
        };

        _policyRepoMock.Setup(r => r.GetActiveOvertimeRulesAsync(1L, It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(rules);

        var earnings = await _service.CalculateOvertimeEarningsAsync(1L, 1200m, 4m, "  WEEKEND  ", new DateTime(2026, 11, 10));
        Assert.Equal(36.000m, earnings); // 4 * 5.0000 * 1.80 = 36.000m
    }

    [Fact]
    public async Task CalculateOvertimeEarningsAsync_WhenRepositoryReturnsNullList_DefaultsMultiplierSafely()
    {
        _policyRepoMock.Setup(r => r.GetActiveOvertimeRulesAsync(1L, It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((IReadOnlyList<OvertimeRule>?)null!);

        var earnings = await _service.CalculateOvertimeEarningsAsync(1L, 1200m, 2m, "WEEKEND", new DateTime(2026, 11, 10));
        Assert.Equal(15.000m, earnings); // 2 * 5 * 1.50 = 15.000m
    }

    [Fact]
    public void AttendanceOvertime_WhenMinMinutesForOvertimeIsZero_CalculatesAnyExtraMinutes()
    {
        // Shift 8:00 to 17:00 (480 mins net). Worked until 17:15 (495 mins net -> 15 mins extra)
        var schedule = CreateStandardShift(new TimeSpan(8, 0, 0), new TimeSpan(17, 0, 0), 60);
        var policy = new AttendancePolicy { MinMinutesForOvertime = 0 };

        var checkIn = new DateTime(2026, 11, 10, 8, 0, 0, DateTimeKind.Utc);
        var checkOut = new DateTime(2026, 11, 10, 17, 15, 0, DateTimeKind.Utc);

        var day = ScheduledAttendanceCalculator.Calculate(
            "EMP_OT",
            new DateOnly(2026, 11, 10),
            checkIn,
            checkOut,
            schedule,
            policy
        );

        Assert.Equal(8.00m, day.ScheduledHours);
        Assert.Equal(8.25m, day.ActualWorkedHours);
        Assert.Equal(0.25m, day.OvertimeHours);
    }

    [Fact]
    public void AttendanceOvertime_WhenCheckedOutEarly_OvertimeIsZero()
    {
        // Shift 8:00 to 17:00. Left at 16:30 (450 mins net vs 480 scheduled)
        var schedule = CreateStandardShift(new TimeSpan(8, 0, 0), new TimeSpan(17, 0, 0), 60);
        var policy = new AttendancePolicy { MinMinutesForOvertime = 30, EarlyLeaveToleranceMinutes = 15 };

        var checkIn = new DateTime(2026, 11, 10, 8, 0, 0, DateTimeKind.Utc);
        var checkOut = new DateTime(2026, 11, 10, 16, 30, 0, DateTimeKind.Utc);

        var day = ScheduledAttendanceCalculator.Calculate(
            "EMP_OT",
            new DateOnly(2026, 11, 10),
            checkIn,
            checkOut,
            schedule,
            policy
        );

        Assert.Equal(8.00m, day.ScheduledHours);
        Assert.Equal(7.50m, day.ActualWorkedHours);
        Assert.Equal(0.00m, day.OvertimeHours);
        Assert.Equal(30, day.EarlyLeaveMinutes);
    }

    [Fact]
    public void AttendanceOvertime_WhenLateArrivalCompensatedWithOverstay_ExtraNetMinutesCalculated()
    {
        // Shift 8:00 to 17:00 (480 mins net).
        // Arrived at 08:30 (30 mins late). Left at 18:30 (90 mins after shift end).
        // Gross = 10 hours = 600 mins. Break = 60 mins. Net = 540 mins.
        // Scheduled = 480 mins. Extra = 540 - 480 = 60 mins -> 1.00 hour overtime!
        var schedule = CreateStandardShift(new TimeSpan(8, 0, 0), new TimeSpan(17, 0, 0), 60);
        var policy = new AttendancePolicy { MinMinutesForOvertime = 30, GracePeriodMinutes = 10 };

        var checkIn = new DateTime(2026, 11, 10, 8, 30, 0, DateTimeKind.Utc);
        var checkOut = new DateTime(2026, 11, 10, 18, 30, 0, DateTimeKind.Utc);

        var day = ScheduledAttendanceCalculator.Calculate(
            "EMP_OT",
            new DateOnly(2026, 11, 10),
            checkIn,
            checkOut,
            schedule,
            policy
        );

        Assert.Equal(8.00m, day.ScheduledHours);
        Assert.Equal(9.00m, day.ActualWorkedHours);
        Assert.Equal(1.00m, day.OvertimeHours);
        Assert.Equal(20, day.LateArrivalMinutes); // 30 mins raw late - 10 min grace (AutoDeductLateArrival = true)
    }

    [Fact]
    public void CopyMetrics_CopiesOvertimeHours_Accurately()
    {
        var source = new AttendanceDay
        {
            EmployeeCode = "EMP_OT",
            AttendanceDate = new DateTime(2026, 11, 10),
            ShiftCode = "DAY",
            TimeZoneId = "Asia/Amman",
            ShiftStartsAtUtc = new DateTime(2026, 11, 10, 5, 0, 0, DateTimeKind.Utc),
            ShiftEndsAtUtc = new DateTime(2026, 11, 10, 14, 0, 0, DateTimeKind.Utc),
            ShiftBreakMinutes = 60,
            ShiftSource = "ROSTER",
            ScheduledHours = 8.00m,
            ActualWorkedHours = 10.00m,
            OvertimeHours = 2.00m,
            LateArrivalMinutes = 0,
            EarlyLeaveMinutes = 0,
            Status = "PRESENT",
            HasMissingPunch = false
        };

        var target = new AttendanceDay();
        ScheduledAttendanceCalculator.CopyMetrics(source, target);

        Assert.Equal(source.OvertimeHours, target.OvertimeHours);
        Assert.Equal(source.ActualWorkedHours, target.ActualWorkedHours);
        Assert.Equal(source.ScheduledHours, target.ScheduledHours);
        Assert.Equal(source.Status, target.Status);
    }

    #endregion

    [Theory]
    [InlineData(0.25, 0.00)]
    [InlineData(0.50, 6.00)]
    [InlineData(2.00, 12.00)]
    public async Task CalculateOvertimeEarningsAsync_UsesCompanyDivisorAndMinuteRange(decimal hours, decimal expected)
    {
        _policyRepoMock.Setup(r => r.GetActiveOvertimeRulesAsync(1L, It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<OvertimeRule>
            {
                new()
                {
                    Code = "OT_REG", DayType = "REGULAR", Multiplier = 2m,
                    MinimumMinutes = 30, MaximumMinutes = 60,
                    HourlyDivisorFormula = "MONTHLY_SALARY / 200"
                }
            });

        var earnings = await _service.CalculateOvertimeEarningsAsync(1L, 1200m, hours, "REGULAR", new DateTime(2026, 11, 10));

        Assert.Equal(expected, earnings);
    }

    [Theory]
    [InlineData("MONTHLY_SALARY / 0")]
    [InlineData("MONTHLY_SALARY / 240 + 10")]
    [InlineData("HOURLY_RATE")]
    public async Task CalculateOvertimeEarningsAsync_RejectsUnsupportedCompanyDivisorFormula(string formula)
    {
        _policyRepoMock.Setup(r => r.GetActiveOvertimeRulesAsync(1L, It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<OvertimeRule>
            {
                new() { Code = "OT_BAD", DayType = "REGULAR", HourlyDivisorFormula = formula }
            });

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _service.CalculateOvertimeEarningsAsync(1L, 1200m, 1m, "REGULAR", new DateTime(2026, 11, 10)));
    }
}
