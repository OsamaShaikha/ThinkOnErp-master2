using Moq;
using Microsoft.Extensions.Logging.Abstractions;
using ThinkOnErp.Application.DTOs.Hr;
using ThinkOnErp.Application.Services.Hr;
using ThinkOnErp.Domain.Entities;
using ThinkOnErp.Domain.Entities.Hr;
using ThinkOnErp.Domain.Interfaces;
using ThinkOnErp.Domain.Interfaces.Hr;
using Xunit;

namespace ThinkOnErp.HR.Tests;

public class ScheduledAttendanceTests
{
    [Fact]
    public void RecalculationCannotSilentlyReplaceTheSavedShiftSnapshot()
    {
        var schedule = Schedule(Date);
        var saved = ScheduledAttendanceCalculator.Calculate("E", Date, null, null, schedule, new());
        var changed = ScheduledAttendanceCalculator.Calculate("E", Date, null, null, schedule with { TimeZoneId = "Different" }, new());
        Assert.Throws<InvalidOperationException>(() => ScheduledAttendanceCalculator.CopyMetrics(changed, saved));
        Assert.Equal("UTC", saved.TimeZoneId);
    }

    [Fact]
    public async Task LegacyDayWithoutTimingSnapshotCannotBeApprovedSilently()
    {
        var repository = new Mock<IAttendanceCorrectionRepository>();
        repository.Setup(r => r.GetCorrectionRequestByIdAsync(1, default)).ReturnsAsync(new AttendanceCorrectionRequest { Id = 1, EmployeeCode = "E", AttendanceDate = Date.ToDateTime(TimeOnly.MinValue) });
        repository.Setup(r => r.GetAttendanceDayAsync("E", Date.ToDateTime(TimeOnly.MinValue), default)).ReturnsAsync(new AttendanceDay { EmployeeCode = "E" });
        await Assert.ThrowsAsync<InvalidOperationException>(() => new AttendanceCorrectionService(repository.Object, Mock.Of<IAttendanceCalculationEngine>()).ProcessCorrectionRequestAsync(1, true, "HR"));
        repository.Verify(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    private static readonly DateOnly Date = new(2026, 11, 5);
    public static TimedShiftDto Schedule(DateOnly date, int startHour = 22, int endHour = 6)
    {
        var start = date.ToDateTime(new(startHour, 0)); var end = date.ToDateTime(new(endHour, 0));
        if (end < start) end = end.AddDays(1);
        return new(new("E", date, "WORKING", "DAILY_MANUAL", "NIGHT", null, 1, start, end, 30, 450, 7.5m), "UTC",
            new(DateTime.SpecifyKind(start, DateTimeKind.Utc)), new(DateTime.SpecifyKind(end, DateTimeKind.Utc)), (decimal)(end - start).TotalMinutes);
    }

    [Fact]
    public void NightCalculationUsesUtcDatesAndConfiguredBreak()
    {
        var schedule = Schedule(Date);
        var result = ScheduledAttendanceCalculator.Calculate("E", Date, schedule.StartUtc!.Value.UtcDateTime.AddMinutes(25), schedule.EndUtc!.Value.UtcDateTime.AddMinutes(-20), schedule,
            new() { GracePeriodMinutes = 10, EarlyLeaveToleranceMinutes = 5 });
        Assert.Equal(15, result.LateArrivalMinutes); Assert.Equal(20, result.EarlyLeaveMinutes);
        Assert.Equal(7.5m, result.ScheduledHours); Assert.Equal(6.75m, result.ActualWorkedHours); Assert.Equal(0m, result.OvertimeHours);
        Assert.Equal("NIGHT", result.ShiftCode); Assert.False(result.HasMissingPunch);
    }

    [Theory] [InlineData(false)] [InlineData(true)]
    public void OnePunchIsIncompleteAndRetainsTheAvailableTimingMetric(bool arrival)
    {
        var schedule = Schedule(Date);
        var result = ScheduledAttendanceCalculator.Calculate("E", Date, arrival ? schedule.StartUtc!.Value.UtcDateTime.AddMinutes(30) : null,
            arrival ? null : schedule.EndUtc!.Value.UtcDateTime.AddMinutes(-30), schedule, new() { GracePeriodMinutes = 0, EarlyLeaveToleranceMinutes = 0 });
        Assert.True(result.HasMissingPunch); Assert.Equal("INCOMPLETE", result.Status);
        Assert.Equal(30, arrival ? result.LateArrivalMinutes : result.EarlyLeaveMinutes); Assert.Equal(0m, result.ActualWorkedHours);
    }

    [Fact]
    public void BackwardTimestampRequiresCorrectionInsteadOfAddingTwentyFourHours()
    {
        var schedule = Schedule(Date);
        Assert.Throws<ArgumentException>(() => ScheduledAttendanceCalculator.Calculate("E", Date, schedule.EndUtc!.Value.UtcDateTime, schedule.StartUtc!.Value.UtcDateTime, schedule, new()));
    }

    [Theory] [InlineData(420, 6.5)] [InlineData(540, 8.5)]
    public void ActualAndScheduledDurationRespectDstElapsedMinutes(int elapsed, double hours)
    {
        var schedule = Schedule(Date); schedule = schedule with { EndUtc = schedule.StartUtc!.Value.AddMinutes(elapsed), ElapsedMinutes = elapsed };
        var result = ScheduledAttendanceCalculator.Calculate("E", Date, schedule.StartUtc!.Value.UtcDateTime, schedule.EndUtc!.Value.UtcDateTime, schedule, new());
        Assert.Equal((decimal)hours, result.ScheduledHours); Assert.Equal((decimal)hours, result.ActualWorkedHours); Assert.Equal(0m, result.OvertimeHours);
    }

    [Theory] [InlineData("REST_DAY")] [InlineData("UNSCHEDULED")]
    public void NoPunchesOnUnscheduledOrRestDatesDoNotBecomeAbsence(string status)
    {
        var schedule = new TimedShiftDto(new("E", Date, status, "NONE", null, null, null, null, null, 0, 0, 0), "UTC", null, null, null);
        var result = ScheduledAttendanceCalculator.Calculate("E", Date, null, null, schedule, new());
        Assert.Equal(status, result.Status); Assert.Equal(0m, result.ScheduledHours);
    }

    [Fact]
    public async Task CorrectionUsesTheSameConfiguredNightCalculation()
    {
        var repository = new Mock<IAttendanceCorrectionRepository>(); var timing = new Mock<IShiftTimingService>();
        var date = Date.AddDays(-2); var schedule = Schedule(date);
        timing.Setup(t => t.PreviewConfiguredAsync("E", date, default)).ReturnsAsync(schedule);
        var request = new AttendanceCorrectionRequest { Id = 1, EmployeeCode = "E", AttendanceDate = date.ToDateTime(TimeOnly.MinValue), OriginalDayVersion = AttendanceCorrectionRules.Version(null),
            RequestedCheckIn = schedule.StartUtc!.Value.UtcDateTime, RequestedCheckOut = schedule.EndUtc!.Value.UtcDateTime };
        repository.Setup(r => r.GetCorrectionRequestByIdAsync(1, default)).ReturnsAsync(request);
        AttendanceDay? saved = null;
        repository.Setup(r => r.AddAttendanceDayAsync(It.IsAny<AttendanceDay>(), default)).Callback<AttendanceDay, CancellationToken>((d, _) => saved = d).Returns(Task.CompletedTask);
        await new AttendanceCorrectionService(repository.Object, new AttendanceCalculationEngine(repository.Object, timing.Object)).ProcessCorrectionRequestAsync(1, true, "HR");
        Assert.NotNull(saved); Assert.Equal("NIGHT", saved!.ShiftCode); Assert.Equal(7.5m, saved.ActualWorkedHours); Assert.Equal(0, saved.LateArrivalMinutes);
    }

    [Fact]
    public async Task MobileCheckoutKeepsThePreviousOpenWorkDateAndConfiguredMetrics()
    {
        var today = DateOnly.FromDateTime(System.DateTime.UtcNow); var previous = today.AddDays(-1); var schedule = Schedule(previous);
        var repository = new Mock<IAttendanceCorrectionRepository>(); var employees = new Mock<IEmployeeRepository>(); var branches = new Mock<IBranchRepository>();
        var timing = new Mock<IShiftTimingService>(); var settings = new Mock<IAttendanceTimeSettingsRepository>(); var geo = new Mock<IGeoLocationService>();
        settings.Setup(s => s.GetForEmployeeAsync("E", default)).ReturnsAsync(new AttendanceTimeSettings { CompanyId = 1, TimeZoneId = "UTC" });
        employees.Setup(e => e.GetEmployeeByCodeAsync("E", default)).ReturnsAsync(new Employee { EmployeeCode = "E", BranchId = 1 });
        branches.Setup(b => b.GetByIdAsync(1)).ReturnsAsync(new SysBranch { Id = 1, CompanyId = 1, IsActive = true });
        geo.Setup(g => g.CheckProximity(It.IsAny<decimal>(), It.IsAny<decimal>(), It.IsAny<SysBranch>(), It.IsAny<decimal?>())).Returns(new GeofenceCheckResult { IsWithinRange = true });
        var day = new AttendanceDay { Id = 42, EmployeeCode = "E", AttendanceDate = previous.ToDateTime(TimeOnly.MinValue), FirstCheckIn = schedule.StartUtc!.Value.UtcDateTime, ShiftCode = "NIGHT", TimeZoneId = "UTC", ShiftStartsAtUtc = schedule.StartUtc.Value.UtcDateTime, ShiftEndsAtUtc = schedule.EndUtc!.Value.UtcDateTime, ShiftBreakMinutes = 30, ShiftSource = "DAILY_MANUAL", ScheduledHours = 7.5m };
        repository.Setup(r => r.GetAttendanceDayAsync("E", day.AttendanceDate, default)).ReturnsAsync(day);
        timing.Setup(t => t.PreviewConfiguredAsync("E", previous, default)).ReturnsAsync(schedule);
        RawAttendance? evidence = null;
        repository.Setup(r => r.AcceptMobileEvidenceAsync(It.IsAny<RawAttendance>(), It.IsAny<Func<long, string>>(), default))
            .ReturnsAsync((RawAttendance raw, Func<long, string> receipt, CancellationToken ct) => { raw.Id = 7; raw.EventResponseJson = receipt(7); evidence = raw; return raw; });
        repository.Setup(r => r.GetRawEventAsync(7, default)).ReturnsAsync(() => evidence);
        repository.Setup(r => r.LockRawEventAsync(7, "E", default)).ReturnsAsync(() => evidence);
        repository.Setup(r => r.BindRawWorkDateAsync(7, It.IsAny<DateTime>(), default)).ReturnsAsync((long id, DateTime d, CancellationToken ct) => { evidence!.ResolvedWorkDate = d; return evidence; });
        repository.Setup(r => r.ExecuteForRawWorkDateAsync("E", It.IsAny<DateTime>(), It.IsAny<Func<Task<RawProcessingResult>>>(), default))
            .Returns((string employee, DateTime d, Func<Task<RawProcessingResult>> action, CancellationToken ct) => action());
        repository.Setup(r => r.GetEarlierPendingEventsAsync("E", It.IsAny<DateTime>(), 7, default)).ReturnsAsync(Array.Empty<RawAttendance>());
        repository.Setup(r => r.GetProcessedDayEventsAsync("E", It.IsAny<DateTime>(), default)).ReturnsAsync(Array.Empty<RawAttendance>());
        var classifier = new Mock<IAttendanceDayClassifier>();
        classifier.Setup(c => c.ClassifyAsync(It.IsAny<AttendanceDay>(), It.IsAny<TimedShiftDto>(), default))
            .ReturnsAsync((AttendanceDay d, TimedShiftDto t, CancellationToken ct) => new AttendanceDayClassificationDto(d, false, false, false, false, null));
        var processing = new RawAttendanceProcessingService(repository.Object, timing.Object, settings.Object, Mock.Of<IPolicyRepository>(), classifier.Object);
        var service = new HrAttendanceService(repository.Object, employees.Object, branches.Object, Mock.Of<IPolicyRepository>(), geo.Object,
            NullLogger<HrAttendanceService>.Instance, timing: timing.Object, timeSettings: settings.Object, processing: processing);
        await service.CheckOutAsync(new("E", 0m, 0m, EventId: Guid.NewGuid().ToString()), "MOBILE");
        Assert.Equal(previous.ToDateTime(TimeOnly.MinValue), day.AttendanceDate); Assert.Equal(7.5m, day.ScheduledHours); Assert.False(day.HasMissingPunch);
        timing.Verify(t => t.PreviewConfiguredAsync("E", today, default), Times.Never);
    }
}
