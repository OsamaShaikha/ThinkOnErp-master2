using Moq;
using ThinkOnErp.Application.DTOs.Hr;
using ThinkOnErp.Application.Services.Hr;
using ThinkOnErp.Domain.Entities.Hr;
using ThinkOnErp.Domain.Interfaces.Hr;
using Xunit;

namespace ThinkOnErp.HR.Tests;

public class ShiftTimingTests
{
    [Theory]
    [InlineData("2026-11-05T12:00:00")]
    [InlineData("2026-11-05")]
    [InlineData("2026-11-05T12:00:00+99:00")]
    public void OffsetlessOrInvalidPunchTimeIsRejected(string value)
        => Assert.Throws<ArgumentException>(() => ShiftTimeRules.ParseInstant(value));

    [Theory]
    [InlineData("2026-11-05T15:00:00+03:00")]
    [InlineData("2026-11-05T12:00:00Z")]
    public void PunchOffsetNormalizesToTheSameInstant(string value)
        => Assert.Equal(new DateTimeOffset(2026, 11, 5, 12, 0, 0, TimeSpan.Zero), ShiftTimeRules.ParseInstant(value));

    private static TimeZoneInfo DstZone()
    {
        var spring = TimeZoneInfo.TransitionTime.CreateFixedDateRule(new DateTime(1, 1, 1, 2, 0, 0), 3, 8);
        var autumn = TimeZoneInfo.TransitionTime.CreateFixedDateRule(new DateTime(1, 1, 1, 2, 0, 0), 11, 1);
        return TimeZoneInfo.CreateCustomTimeZone("TestDst", TimeSpan.FromHours(-5), "Test", "Test", "Test Summer",
            new[] { TimeZoneInfo.AdjustmentRule.CreateAdjustmentRule(new DateTime(2026, 1, 1), new DateTime(2026, 12, 31), TimeSpan.FromHours(1), spring, autumn) });
    }

    [Theory]
    [InlineData(3, 7, 7)] [InlineData(10, 31, 9)]
    public void OvernightElapsedTimeReflectsDstWithoutChangingLocalSchedule(int month, int day, int hours)
    {
        var start = new DateTime(2026, month, day, 22, 0, 0); var end = start.Date.AddDays(1).AddHours(6);
        var result = ShiftTimeRules.ToUtc(start, end, DstZone());
        Assert.Equal(hours * 60m, result.ElapsedMinutes); Assert.Equal(TimeSpan.Zero, result.StartUtc.Offset);
    }

    [Theory] [InlineData(3, 8, 2)] [InlineData(11, 1, 1)]
    public void GapAndAmbiguousClockBoundariesAreRejected(int month, int day, int hour)
        => Assert.Throws<InvalidOperationException>(() => ShiftTimeRules.ToUtc(new DateTime(2026, month, day, hour, 30, 0), new DateTime(2026, month, day, 6, 0, 0), DstZone()));

    [Theory] [InlineData("IN", 0, true)] [InlineData("OUT", 0, false)] [InlineData("IN", 8, false)] [InlineData("OUT", 8, true)]
    public void SharedBoundaryBelongsToIncomingOrOutgoingShiftAccordingToPunchType(string type, int hour, bool expected)
    {
        var start = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
        Assert.Equal(expected, ShiftTimeRules.Contains(new(start, start.AddHours(8), 480), start.AddHours(hour), type));
    }

    private static EffectiveShiftDto Working(DateOnly date, int startHour, int endHour)
    {
        var start = date.ToDateTime(new(startHour, 0)); var end = date.ToDateTime(new(endHour, 0));
        if (end < start) end = end.AddDays(1);
        return new("E", date, "WORKING", "BASE", "S", 1, null, start, end, 0, 480, 8);
    }
    private static EffectiveShiftDto Rest(DateOnly date) => new("E", date, "REST_DAY", "BASE", null, null, null, null, null, 0, 0, 0);

    [Fact]
    public async Task AfterMidnightPunchKeepsPreviousStartDate()
    {
        var shifts = new Mock<IEffectiveShiftService>(); var date = new DateOnly(2026, 11, 5);
        shifts.Setup(s => s.ResolveAsync("E", date.AddDays(-1), default)).ReturnsAsync(Working(date.AddDays(-1), 22, 6));
        shifts.Setup(s => s.ResolveAsync("E", date, default)).ReturnsAsync(Rest(date));
        var result = await new ShiftTimingService(shifts.Object).MatchPunchAsync("E", new DateTimeOffset(2026, 11, 5, 5, 0, 0, TimeSpan.Zero), "OUT", "UTC");
        Assert.Equal(date.AddDays(-1), result!.Shift.WorkDate);
    }

    [Fact]
    public async Task OverlappingWorkDatesRejectPunchInsteadOfGuessing()
    {
        var shifts = new Mock<IEffectiveShiftService>(); var date = new DateOnly(2026, 11, 5);
        shifts.Setup(s => s.ResolveAsync("E", date.AddDays(-1), default)).ReturnsAsync(Working(date.AddDays(-1), 22, 6));
        shifts.Setup(s => s.ResolveAsync("E", date, default)).ReturnsAsync(Working(date, 4, 12));
        await Assert.ThrowsAsync<InvalidOperationException>(() => new ShiftTimingService(shifts.Object).MatchPunchAsync("E", new DateTimeOffset(2026, 11, 5, 5, 0, 0, TimeSpan.Zero), "IN", "UTC"));
    }

    [Fact]
    public async Task OffSchedulePunchHasNoGuessedWorkDate()
    {
        var shifts = new Mock<IEffectiveShiftService>(); var date = new DateOnly(2026, 11, 5);
        shifts.Setup(s => s.ResolveAsync("E", It.IsAny<DateOnly>(), default)).ReturnsAsync((string employee, DateOnly day, CancellationToken ct) => Rest(day));
        Assert.Null(await new ShiftTimingService(shifts.Object).MatchPunchAsync("E", new DateTimeOffset(2026, 11, 5, 5, 0, 0, TimeSpan.Zero), "IN", "UTC"));
    }

    [Theory]
    [InlineData("BREAK")]
    [InlineData("MIDDAY")]
    [InlineData("")]
    [InlineData("in")] // case-sensitive check
    public async Task MatchPunchAsync_WhenPunchTypeInvalid_ThrowsArgumentException(string punchType)
    {
        var shifts = new Mock<IEffectiveShiftService>();
        var service = new ShiftTimingService(shifts.Object);
        var ex = await Assert.ThrowsAsync<ArgumentException>(() =>
            service.MatchPunchAsync("E", DateTimeOffset.UtcNow, punchType, "UTC"));
        Assert.Contains("Punch type must be IN or OUT", ex.Message);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void ShiftTimeRules_Zone_WhenNullOrWhitespace_ThrowsArgumentException(string? zoneId)
    {
        var ex = Assert.Throws<ArgumentException>(() => ShiftTimeRules.Zone(zoneId!));
        Assert.Contains("An explicit company time zone is required", ex.Message);
    }

    [Fact]
    public void ShiftTimeRules_Zone_WhenExceeds100Chars_ThrowsArgumentException()
    {
        var longZone = new string('Z', 101);
        var ex = Assert.Throws<ArgumentException>(() => ShiftTimeRules.Zone(longZone));
        Assert.Contains("An explicit company time zone is required", ex.Message);
    }

    [Fact]
    public void ShiftTimeRules_Zone_WhenUnknown_ThrowsArgumentException()
    {
        Assert.Throws<ArgumentException>(() => ShiftTimeRules.Zone("Completely/Unknown_Zone_12345"));
    }

    [Fact]
    public void ShiftTimeRules_Zone_WhenUtc_ResolvesSuccessfully()
    {
        var zone = ShiftTimeRules.Zone("UTC");
        Assert.Equal("UTC", zone.Id);
    }

    [Fact]
    public async Task PreviewConfiguredAsync_WhenSettingsRepositoryNull_ThrowsInvalidOperationException()
    {
        var shifts = new Mock<IEffectiveShiftService>();
        var service = new ShiftTimingService(shifts.Object, settings: null);
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.PreviewConfiguredAsync("E", new DateOnly(2026, 11, 5)));
        Assert.Contains("Time settings repository is required", ex.Message);
    }

    [Fact]
    public async Task MatchConfiguredPunchAsync_WhenSettingsRepositoryNull_ThrowsInvalidOperationException()
    {
        var shifts = new Mock<IEffectiveShiftService>();
        var service = new ShiftTimingService(shifts.Object, settings: null);
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.MatchConfiguredPunchAsync("E", DateTimeOffset.UtcNow, "IN"));
        Assert.Contains("Time settings repository is required", ex.Message);
    }

    [Fact]
    public async Task PreviewConfiguredAsync_WithSettingsRepository_DelegatesWithConfiguredTimeZone()
    {
        var shifts = new Mock<IEffectiveShiftService>();
        var settingsRepo = new Mock<IAttendanceTimeSettingsRepository>();
        var date = new DateOnly(2026, 11, 5);

        settingsRepo.Setup(s => s.GetForEmployeeAsync("E", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AttendanceTimeSettings { TimeZoneId = "UTC" });

        shifts.Setup(s => s.ResolveAsync("E", date, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Working(date, 9, 17));

        var service = new ShiftTimingService(shifts.Object, settingsRepo.Object);
        var timedShift = await service.PreviewConfiguredAsync("E", date);

        Assert.NotNull(timedShift);
        Assert.Equal("UTC", timedShift.TimeZoneId);
        Assert.NotNull(timedShift.StartUtc);
        Assert.NotNull(timedShift.EndUtc);
        Assert.Equal(480m, timedShift.ElapsedMinutes);
    }

    [Fact]
    public async Task PreviewAsync_WhenShiftNotWorking_ReturnsNullBoundariesAndElapsed()
    {
        var shifts = new Mock<IEffectiveShiftService>();
        var date = new DateOnly(2026, 11, 5);

        shifts.Setup(s => s.ResolveAsync("E", date, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Rest(date));

        var service = new ShiftTimingService(shifts.Object);
        var timedShift = await service.PreviewAsync("E", date, "UTC");

        Assert.Equal("REST_DAY", timedShift.Shift.Status);
        Assert.Null(timedShift.StartUtc);
        Assert.Null(timedShift.EndUtc);
        Assert.Null(timedShift.ElapsedMinutes);
    }

    [Fact]
    public async Task PreviewAsync_WhenWorkingShiftHasNullLocalStart_ThrowsInvalidOperationException()
    {
        var shifts = new Mock<IEffectiveShiftService>();
        var date = new DateOnly(2026, 11, 5);

        var corruptedShift = new EffectiveShiftDto(
            "E", date, "WORKING", "BASE", "SHIFT1", 1, null,
            StartsAtLocal: null, EndsAtLocal: null, 0, 480, 8);

        shifts.Setup(s => s.ResolveAsync("E", date, It.IsAny<CancellationToken>()))
            .ReturnsAsync(corruptedShift);

        var service = new ShiftTimingService(shifts.Object);
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.PreviewAsync("E", date, "UTC"));
        Assert.Contains("Working shift has no boundaries", ex.Message);
    }

    [Fact]
    public void ShiftTimeRules_ToUtc_WhenKindNotUnspecified_ThrowsArgumentException()
    {
        var startUtc = DateTime.UtcNow; // DateTimeKind.Utc
        var endUtc = startUtc.AddHours(8);
        var zone = TimeZoneInfo.Utc;

        var ex = Assert.Throws<ArgumentException>(() =>
            ShiftTimeRules.ToUtc(startUtc, endUtc, zone));
        Assert.Contains("Shift boundaries must be local clock values without a UTC/local kind", ex.Message);
    }

    [Fact]
    public void ShiftTimeRules_ToUtc_WhenEndBeforeStart_ThrowsInvalidOperationException()
    {
        var startLocal = new DateTime(2026, 1, 1, 17, 0, 0, DateTimeKind.Unspecified);
        var endLocal = new DateTime(2026, 1, 1, 9, 0, 0, DateTimeKind.Unspecified); // Before start
        var zone = TimeZoneInfo.Utc;

        var ex = Assert.Throws<InvalidOperationException>(() =>
            ShiftTimeRules.ToUtc(startLocal, endLocal, zone));
        Assert.Contains("Shift UTC end must follow its start", ex.Message);
    }

    [Theory]
    [InlineData("UNKNOWN")]
    [InlineData("LUNCH")]
    [InlineData("in")]
    [InlineData("out")]
    public void ShiftTimeRules_Contains_WhenPunchTypeInvalid_ThrowsArgumentException(string punchType)
    {
        var window = new ShiftUtcWindow(DateTimeOffset.UtcNow, DateTimeOffset.UtcNow.AddHours(8), 480);
        var ex = Assert.Throws<ArgumentException>(() =>
            ShiftTimeRules.Contains(window, DateTimeOffset.UtcNow.AddHours(2), punchType));
        Assert.Contains("Punch type must be IN or OUT", ex.Message);
    }

    [Fact]
    public void ShiftTimeRules_Contains_ExactBoundaryConditions()
    {
        var start = new DateTimeOffset(2026, 11, 10, 8, 0, 0, TimeSpan.Zero);
        var end = start.AddHours(8); // 16:00
        var window = new ShiftUtcWindow(start, end, 480);

        // At exact start instant
        Assert.True(ShiftTimeRules.Contains(window, start, "IN"));
        Assert.False(ShiftTimeRules.Contains(window, start, "OUT"));

        // At exact end instant
        Assert.False(ShiftTimeRules.Contains(window, end, "IN"));
        Assert.True(ShiftTimeRules.Contains(window, end, "OUT"));

        // Strictly inside
        var midpoint = start.AddHours(4);
        Assert.True(ShiftTimeRules.Contains(window, midpoint, "IN"));
        Assert.True(ShiftTimeRules.Contains(window, midpoint, "OUT"));

        // Before start
        var before = start.AddMinutes(-1);
        Assert.False(ShiftTimeRules.Contains(window, before, "IN"));
        Assert.False(ShiftTimeRules.Contains(window, before, "OUT"));

        // After end
        var after = end.AddMinutes(1);
        Assert.False(ShiftTimeRules.Contains(window, after, "IN"));
        Assert.False(ShiftTimeRules.Contains(window, after, "OUT"));
    }

    [Theory]
    [InlineData("2026-11-10T08:00:00Z", 2026, 11, 10, 8, 0, 0, 0)]
    [InlineData("2026-11-10T11:00:00+03:00", 2026, 11, 10, 8, 0, 0, 0)]
    [InlineData("2026-11-10T03:00:00-05:00", 2026, 11, 10, 8, 0, 0, 0)]
    [InlineData("2026-06-15T14:30:45.500+02:00", 2026, 6, 15, 12, 30, 45, 500)]
    public void ShiftTimeRules_ParseInstant_Theory_ParsesAndNormalizesCorrectly(
        string input, int year, int month, int day, int hour, int minute, int second, int millisecond)
    {
        var instant = ShiftTimeRules.ParseInstant(input);
        var expectedUtc = new DateTimeOffset(year, month, day, hour, minute, second, millisecond, TimeSpan.Zero);
        Assert.Equal(expectedUtc, instant.ToUniversalTime());
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("2026-11-10 08:00:00Z")]  // Missing 'T'
    [InlineData("2026-11-10T08:00:00")]    // Missing offset / Z
    [InlineData("Not a valid date time instant")]
    public void ShiftTimeRules_ParseInstant_Theory_InvalidInputs_ThrowsArgumentException(string? input)
    {
        var ex = Assert.Throws<ArgumentException>(() => ShiftTimeRules.ParseInstant(input!));
        Assert.Contains("Punch instant must be ISO 8601 with Z or an explicit UTC offset", ex.Message);
    }

    [Fact]
    public async Task ShiftTimingService_MatchPunchAsync_StandardDayShift_MatchesBothInAndOut()
    {
        var shifts = new Mock<IEffectiveShiftService>();
        var date = new DateOnly(2026, 11, 10);
        shifts.Setup(s => s.ResolveAsync("E", date, default)).ReturnsAsync(Working(date, 8, 17));
        shifts.Setup(s => s.ResolveAsync("E", date.AddDays(-1), default)).ReturnsAsync(Rest(date.AddDays(-1)));

        var service = new ShiftTimingService(shifts.Object);

        // Check-in at 08:15 (inside)
        var inPunch = new DateTimeOffset(2026, 11, 10, 8, 15, 0, TimeSpan.Zero);
        var matchedIn = await service.MatchPunchAsync("E", inPunch, "IN", "UTC");
        Assert.NotNull(matchedIn);
        Assert.Equal(date, matchedIn!.Shift.WorkDate);

        // Check-out at 17:00 (exact boundary)
        var outPunch = new DateTimeOffset(2026, 11, 10, 17, 0, 0, TimeSpan.Zero);
        var matchedOut = await service.MatchPunchAsync("E", outPunch, "OUT", "UTC");
        Assert.NotNull(matchedOut);
        Assert.Equal(date, matchedOut!.Shift.WorkDate);
    }

    [Fact]
    public async Task ShiftTimingService_MatchPunchAsync_OvernightShift_PunchesAcrossMidnight_BothAnchorToStartWorkDate()
    {
        var shifts = new Mock<IEffectiveShiftService>();
        var nov4 = new DateOnly(2026, 11, 4);
        var nov5 = new DateOnly(2026, 11, 5);
        var nov6 = new DateOnly(2026, 11, 6);

        // Nov 4: Rest day
        shifts.Setup(s => s.ResolveAsync("E", nov4, default)).ReturnsAsync(Rest(nov4));
        // Nov 5: Night shift 22:00 to 06:00
        shifts.Setup(s => s.ResolveAsync("E", nov5, default)).ReturnsAsync(Working(nov5, 22, 6));
        // Nov 6: Rest day
        shifts.Setup(s => s.ResolveAsync("E", nov6, default)).ReturnsAsync(Rest(nov6));

        var service = new ShiftTimingService(shifts.Object);

        // Punch IN on Nov 5 at 22:15 UTC
        var inPunch = new DateTimeOffset(2026, 11, 5, 22, 15, 0, TimeSpan.Zero);
        var matchIn = await service.MatchPunchAsync("E", inPunch, "IN", "UTC");
        Assert.NotNull(matchIn);
        Assert.Equal(nov5, matchIn!.Shift.WorkDate);

        // Punch OUT on Nov 6 at 05:45 UTC (cross midnight)
        var outPunch = new DateTimeOffset(2026, 11, 6, 5, 45, 0, TimeSpan.Zero);
        var matchOut = await service.MatchPunchAsync("E", outPunch, "OUT", "UTC");
        Assert.NotNull(matchOut);
        Assert.Equal(nov5, matchOut!.Shift.WorkDate); // Anchored to Nov 5!
    }

    [Fact]
    public async Task MatchConfiguredPunchAsync_WithValidSettings_MatchesSuccessfully()
    {
        var shifts = new Mock<IEffectiveShiftService>();
        var settingsRepo = new Mock<IAttendanceTimeSettingsRepository>();
        var date = new DateOnly(2026, 11, 10);

        settingsRepo.Setup(s => s.GetForEmployeeAsync("E", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AttendanceTimeSettings { TimeZoneId = "UTC" });
        shifts.Setup(s => s.ResolveAsync("E", date, default)).ReturnsAsync(Working(date, 9, 17));
        shifts.Setup(s => s.ResolveAsync("E", date.AddDays(-1), default)).ReturnsAsync(Rest(date.AddDays(-1)));

        var service = new ShiftTimingService(shifts.Object, settingsRepo.Object);

        var punch = new DateTimeOffset(2026, 11, 10, 9, 5, 0, TimeSpan.Zero);
        var matched = await service.MatchConfiguredPunchAsync("E", punch, "IN");

        Assert.NotNull(matched);
        Assert.Equal("UTC", matched!.TimeZoneId);
        Assert.Equal(date, matched.Shift.WorkDate);
    }
}
