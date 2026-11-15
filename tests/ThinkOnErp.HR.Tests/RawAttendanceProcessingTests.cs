using Moq;
using ThinkOnErp.Application.Services.Hr;
using ThinkOnErp.Domain.Entities.Hr;
using ThinkOnErp.Domain.Interfaces.Hr;
using Xunit;

namespace ThinkOnErp.HR.Tests;

public class RawAttendanceProcessingTests
{
    private static readonly DateOnly Date = new(2026, 3, 10);
    private static RawAttendance Raw(long id, int hour, string type) => new() { Id = id, EmployeeCode = "E", EventId = "D:" + id,
        EventPayloadHash = "HASH", PunchTime = Date.ToDateTime(new(hour, 0), DateTimeKind.Utc), PunchType = type,
        ResolvedWorkDate = Date.ToDateTime(TimeOnly.MinValue) };
    private static RawAttendanceProcessingService Service(IAttendanceCorrectionRepository repository, IShiftTimingService? timing = null,
        IAttendanceTimeSettingsRepository? settings = null, IAttendanceDayClassifier? classifier = null)
        => new(repository, timing ?? Mock.Of<IShiftTimingService>(), settings ?? Mock.Of<IAttendanceTimeSettingsRepository>(), Mock.Of<IPolicyRepository>(), classifier ?? Mock.Of<IAttendanceDayClassifier>());

    [Fact]
    public void SingleSessionReconstructsUnorderedEvidenceWithoutLosingExistingClock()
    {
        var summary = AttendanceEvidenceSummary.Build(new[] { Raw(2, 17, "OUT"), Raw(1, 9, "IN") }, null);
        Assert.False(summary.NeedsReview); Assert.Equal(Raw(1, 9, "IN").PunchTime, summary.FirstIn); Assert.Equal(Raw(2, 17, "OUT").PunchTime, summary.LastOut);
        var old = new AttendanceDay { FirstCheckIn = Raw(1, 9, "IN").PunchTime };
        Assert.Equal(old.FirstCheckIn, AttendanceEvidenceSummary.Build(new[] { Raw(2, 17, "OUT") }, old).FirstIn);
    }

    [Theory] [InlineData(0)] [InlineData(1)] [InlineData(2)]
    public void MultipleSessionsOrAmbiguousOrderingRequireReview(int scenario)
    {
        var events = scenario switch
        {
            0 => new[] { Raw(1, 9, "IN"), Raw(2, 12, "OUT"), Raw(3, 13, "IN"), Raw(4, 17, "OUT") },
            1 => new[] { Raw(1, 9, "IN"), Raw(2, 9, "OUT") },
            _ => new[] { Raw(1, 8, "OUT"), Raw(2, 9, "IN") }
        };
        Assert.True(AttendanceEvidenceSummary.Build(events, null).NeedsReview);
    }

    [Fact]
    public async Task TerminalEventDoesNotAcquireAnotherFinancialLock()
    {
        var repository = new Mock<IAttendanceCorrectionRepository>(MockBehavior.Strict); var raw = Raw(1, 9, "IN");
        raw.IsProcessed = true; raw.ProcessingOutcome = "APPLIED"; raw.AttendanceDayId = 42;
        repository.Setup(r => r.GetRawEventAsync(1, default)).ReturnsAsync(raw);
        var result = await Service(repository.Object).ProcessAsync(1, "HR");
        Assert.True(result.AlreadyTerminal); Assert.Equal(42, result.AttendanceDayId);
        repository.Verify(r => r.GetRawEventAsync(1, default), Times.Once); repository.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task LegacyEvidenceCannotBeGuessedAsUtcAndApplied()
    {
        var repository = new Mock<IAttendanceCorrectionRepository>(); var raw = Raw(1, 9, "IN"); raw.EventId = null;
        repository.Setup(r => r.GetRawEventAsync(1, default)).ReturnsAsync(raw);
        await Assert.ThrowsAsync<InvalidOperationException>(() => Service(repository.Object).ProcessAsync(1, "HR"));
        repository.Verify(r => r.BindRawWorkDateAsync(It.IsAny<long>(), It.IsAny<DateTime>(), default), Times.Never);
    }

    [Fact]
    public async Task ClosedPayrollLeavesEvidencePendingAndWritesNoDay()
    {
        var repository = new Mock<IAttendanceCorrectionRepository>(); var raw = Raw(1, 9, "IN");
        var timing = new Mock<IShiftTimingService>(); var settings = new Mock<IAttendanceTimeSettingsRepository>();
        var schedule = ScheduledAttendanceTests.Schedule(Date, 9, 17);
        repository.Setup(r => r.GetRawEventAsync(1, default)).ReturnsAsync(raw);
        repository.Setup(r => r.BindRawWorkDateAsync(1, Date.ToDateTime(TimeOnly.MinValue), default)).ReturnsAsync(raw);
        settings.Setup(s => s.GetForEmployeeAsync("E", default)).ReturnsAsync(new AttendanceTimeSettings { CompanyId = 1, TimeZoneId = "UTC" });
        timing.Setup(t => t.MatchConfiguredPunchAsync("E", It.IsAny<DateTimeOffset>(), "IN", default)).ReturnsAsync(schedule);
        repository.Setup(r => r.ExecuteForRawWorkDateAsync("E", Date.ToDateTime(TimeOnly.MinValue), It.IsAny<Func<Task<RawProcessingResult>>>(), default))
            .ThrowsAsync(new InvalidOperationException("closed period"));
        await Assert.ThrowsAsync<InvalidOperationException>(() => Service(repository.Object, timing.Object, settings.Object).ProcessAsync(1, "HR"));
        Assert.False(raw.IsProcessed);
        repository.Verify(r => r.AddAttendanceDayAsync(It.IsAny<AttendanceDay>(), default), Times.Never);
        repository.Verify(r => r.SaveChangesAsync(default), Times.Never);
        repository.Verify(r => r.BindRawWorkDateAsync(1, Date.ToDateTime(TimeOnly.MinValue), default), Times.Once);
    }

    [Fact]
    public async Task BatchReportsAnUnprocessableRowAndContinuesToAConcurrentTerminalRow()
    {
        var repository = new Mock<IAttendanceCorrectionRepository>(); var legacy = Raw(1, 9, "IN"); legacy.EventId = null;
        var terminal = Raw(2, 17, "OUT"); terminal.IsProcessed = true; terminal.ProcessingOutcome = "APPLIED";
        repository.Setup(r => r.GetUnprocessedRawPunchesAsync(100, default)).ReturnsAsync(new[] { legacy, terminal });
        repository.Setup(r => r.GetRawEventAsync(1, default)).ReturnsAsync(legacy); repository.Setup(r => r.GetRawEventAsync(2, default)).ReturnsAsync(terminal);
        var result = await Service(repository.Object).ProcessBatchAsync(100, "HR");
        Assert.True(result[0].NeedsReview); Assert.True(result[1].AlreadyTerminal);
    }

    [Theory] [InlineData(0)] [InlineData(101)]
    public async Task InvalidBatchSizeReadsNothing(int size)
    {
        var repository = new Mock<IAttendanceCorrectionRepository>(MockBehavior.Strict);
        await Assert.ThrowsAsync<ArgumentException>(() => Service(repository.Object).ProcessBatchAsync(size, "HR")); repository.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task GetAsync_WhenEventNotFound_ThrowsKeyNotFoundException()
    {
        var repository = new Mock<IAttendanceCorrectionRepository>();
        repository.Setup(r => r.GetRawEventAsync(999, default)).ReturnsAsync((RawAttendance?)null);

        await Assert.ThrowsAsync<KeyNotFoundException>(() => Service(repository.Object).GetAsync(999));
    }

    [Fact]
    public async Task GetAsync_WhenEventFound_ReturnsMappedDto()
    {
        var repository = new Mock<IAttendanceCorrectionRepository>();
        var raw = Raw(123, 8, "IN");
        raw.DeviceId = "BIO_TERMINAL_1";
        raw.Source = "DEVICE";
        raw.ExternalReference = "REF_ABC";
        raw.IsProcessed = true;
        raw.ProcessingOutcome = "APPLIED";
        raw.ProcessedBy = "SYSTEM";
        raw.ProcessedDate = DateTime.UtcNow;
        raw.ResolvedWorkDate = Date.ToDateTime(TimeOnly.MinValue);
        raw.AttendanceDayId = 77;
        repository.Setup(r => r.GetRawEventAsync(123, default)).ReturnsAsync(raw);

        var dto = await Service(repository.Object).GetAsync(123);

        Assert.Equal(123, dto.Id);
        Assert.Equal("E", dto.EmployeeCode);
        Assert.Equal("IN", dto.PunchType);
        Assert.Equal("BIO_TERMINAL_1", dto.DeviceId);
        Assert.Equal("DEVICE", dto.Source);
        Assert.Equal("REF_ABC", dto.ExternalReference);
        Assert.True(dto.IsProcessed);
        Assert.Equal("APPLIED", dto.Outcome);
        Assert.Equal("SYSTEM", dto.ProcessedBy);
        Assert.Equal(Date, dto.WorkDate);
        Assert.Equal(77, dto.AttendanceDayId);
    }

    [Fact]
    public async Task IgnoreAsync_DelegatesToRepositoryAndReturnsMappedDto()
    {
        var repository = new Mock<IAttendanceCorrectionRepository>();
        var ignored = Raw(456, 10, "IN");
        ignored.IsProcessed = true;
        ignored.ProcessingOutcome = "IGNORED";
        ignored.ProcessingReason = "Duplicate hardware punch";
        ignored.ProcessedBy = "SUPERVISOR";
        ignored.ProcessedDate = DateTime.UtcNow;
        repository.Setup(r => r.IgnoreRawEventAsync(456, "Duplicate hardware punch", "SUPERVISOR", default))
            .ReturnsAsync(ignored);

        var dto = await Service(repository.Object).IgnoreAsync(456, "Duplicate hardware punch", "SUPERVISOR");

        Assert.Equal(456, dto.Id);
        Assert.True(dto.IsProcessed);
        Assert.Equal("IGNORED", dto.Outcome);
        Assert.Equal("Duplicate hardware punch", dto.Reason);
        Assert.Equal("SUPERVISOR", dto.ProcessedBy);
    }

    [Fact]
    public async Task ProcessAsync_WhenEventNotFound_ThrowsKeyNotFoundException()
    {
        var repository = new Mock<IAttendanceCorrectionRepository>();
        repository.Setup(r => r.GetRawEventAsync(888, default)).ReturnsAsync((RawAttendance?)null);

        await Assert.ThrowsAsync<KeyNotFoundException>(() => Service(repository.Object).ProcessAsync(888, "HR"));
    }

    [Theory]
    [InlineData("BREAK")]
    [InlineData("UNKNOWN")]
    [InlineData("CHECKIN")]
    public async Task ProcessAsync_WhenPunchTypeInvalid_ThrowsInvalidOperationException(string invalidType)
    {
        var repository = new Mock<IAttendanceCorrectionRepository>();
        var raw = Raw(1, 9, invalidType);
        repository.Setup(r => r.GetRawEventAsync(1, default)).ReturnsAsync(raw);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => Service(repository.Object).ProcessAsync(1, "HR"));
        Assert.Contains("Invalid or future raw punch requires review", ex.Message);
        repository.Verify(r => r.RecordRawAttemptAsync(1, "HR", It.IsAny<string?>(), default), Times.AtLeastOnce);
    }

    [Fact]
    public async Task ProcessAsync_WhenPunchTimeInFuture_ThrowsInvalidOperationException()
    {
        var repository = new Mock<IAttendanceCorrectionRepository>();
        var raw = Raw(1, 9, "IN");
        raw.PunchTime = DateTime.UtcNow.AddDays(1);
        repository.Setup(r => r.GetRawEventAsync(1, default)).ReturnsAsync(raw);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => Service(repository.Object).ProcessAsync(1, "HR"));
        Assert.Contains("Invalid or future raw punch requires review", ex.Message);
    }

    [Fact]
    public async Task ProcessAsync_WhenEventPayloadHashNull_ThrowsInvalidOperationException()
    {
        var repository = new Mock<IAttendanceCorrectionRepository>();
        var raw = Raw(1, 9, "IN");
        raw.EventPayloadHash = null;
        repository.Setup(r => r.GetRawEventAsync(1, default)).ReturnsAsync(raw);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => Service(repository.Object).ProcessAsync(1, "HR"));
        Assert.Contains("Legacy raw evidence needs UTC/source identity review", ex.Message);
    }

    [Fact]
    public async Task ProcessAsync_WhenRawEvidenceChangedDuringLocking_ThrowsInvalidOperationException()
    {
        var repository = new Mock<IAttendanceCorrectionRepository>();
        var settings = new Mock<IAttendanceTimeSettingsRepository>();
        var timing = new Mock<IShiftTimingService>();
        var schedule = ScheduledAttendanceTests.Schedule(Date, 9, 17);
        var raw = Raw(1, 9, "IN");

        repository.Setup(r => r.GetRawEventAsync(1, default)).ReturnsAsync(raw);
        repository.Setup(r => r.BindRawWorkDateAsync(1, Date.ToDateTime(TimeOnly.MinValue), default)).ReturnsAsync(raw);
        settings.Setup(s => s.GetForEmployeeAsync("E", default)).ReturnsAsync(new AttendanceTimeSettings { CompanyId = 1, TimeZoneId = "UTC" });
        timing.Setup(t => t.MatchConfiguredPunchAsync("E", It.IsAny<DateTimeOffset>(), "IN", default)).ReturnsAsync(schedule);

        // While acquiring lock, evidence hash changed concurrently
        var tamperedRaw = Raw(1, 9, "IN");
        tamperedRaw.EventPayloadHash = "DIFFERENT_HASH";
        repository.Setup(r => r.LockRawEventAsync(1, "E", default)).ReturnsAsync(tamperedRaw);
        repository.Setup(r => r.ExecuteForRawWorkDateAsync("E", Date.ToDateTime(TimeOnly.MinValue), It.IsAny<Func<Task<RawProcessingResult>>>(), default))
            .Returns<string, DateTime, Func<Task<RawProcessingResult>>, CancellationToken>((emp, d, action, ct) => action());

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => Service(repository.Object, timing.Object, settings.Object).ProcessAsync(1, "HR"));
        Assert.Contains("Raw evidence changed while acquiring its locks", ex.Message);
    }

    [Fact]
    public async Task ProcessAsync_WhenPendingEvidenceVolumeExceeds1000_ThrowsInvalidOperationException()
    {
        var repository = new Mock<IAttendanceCorrectionRepository>();
        var settings = new Mock<IAttendanceTimeSettingsRepository>();
        var timing = new Mock<IShiftTimingService>();
        var schedule = ScheduledAttendanceTests.Schedule(Date, 9, 17);
        var raw = Raw(1, 9, "IN");

        repository.Setup(r => r.GetRawEventAsync(1, default)).ReturnsAsync(raw);
        repository.Setup(r => r.BindRawWorkDateAsync(1, Date.ToDateTime(TimeOnly.MinValue), default)).ReturnsAsync(raw);
        settings.Setup(s => s.GetForEmployeeAsync("E", default)).ReturnsAsync(new AttendanceTimeSettings { CompanyId = 1, TimeZoneId = "UTC" });
        timing.Setup(t => t.MatchConfiguredPunchAsync("E", It.IsAny<DateTimeOffset>(), "IN", default)).ReturnsAsync(schedule);
        repository.Setup(r => r.LockRawEventAsync(1, "E", default)).ReturnsAsync(raw);
        repository.Setup(r => r.ExecuteForRawWorkDateAsync("E", Date.ToDateTime(TimeOnly.MinValue), It.IsAny<Func<Task<RawProcessingResult>>>(), default))
            .Returns<string, DateTime, Func<Task<RawProcessingResult>>, CancellationToken>((emp, d, action, ct) => action());

        // Returns 1001 earlier pending events
        var hugePendingList = Enumerable.Range(1, 1001).Select(i => Raw(i + 10, 8, "IN")).ToList();
        repository.Setup(r => r.GetEarlierPendingEventsAsync("E", raw.PunchTime, 1, default)).ReturnsAsync(hugePendingList);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => Service(repository.Object, timing.Object, settings.Object).ProcessAsync(1, "HR"));
        Assert.Contains("Pending evidence volume requires reviewed batching", ex.Message);
    }

    [Fact]
    public async Task ProcessAsync_WhenEarlierPendingEventExistsForSameWorkDate_ThrowsInvalidOperationException()
    {
        var repository = new Mock<IAttendanceCorrectionRepository>();
        var settings = new Mock<IAttendanceTimeSettingsRepository>();
        var timing = new Mock<IShiftTimingService>();
        var schedule = ScheduledAttendanceTests.Schedule(Date, 9, 17);
        var raw = Raw(2, 10, "IN");

        repository.Setup(r => r.GetRawEventAsync(2, default)).ReturnsAsync(raw);
        repository.Setup(r => r.BindRawWorkDateAsync(2, Date.ToDateTime(TimeOnly.MinValue), default)).ReturnsAsync(raw);
        settings.Setup(s => s.GetForEmployeeAsync("E", default)).ReturnsAsync(new AttendanceTimeSettings { CompanyId = 1, TimeZoneId = "UTC" });
        timing.Setup(t => t.MatchConfiguredPunchAsync("E", It.IsAny<DateTimeOffset>(), "IN", default)).ReturnsAsync(schedule);
        repository.Setup(r => r.LockRawEventAsync(2, "E", default)).ReturnsAsync(raw);
        repository.Setup(r => r.ExecuteForRawWorkDateAsync("E", Date.ToDateTime(TimeOnly.MinValue), It.IsAny<Func<Task<RawProcessingResult>>>(), default))
            .Returns<string, DateTime, Func<Task<RawProcessingResult>>, CancellationToken>((emp, d, action, ct) => action());

        var earlierRaw = Raw(1, 8, "IN");
        earlierRaw.ResolvedWorkDate = Date.ToDateTime(TimeOnly.MinValue);
        repository.Setup(r => r.GetEarlierPendingEventsAsync("E", raw.PunchTime, 2, default)).ReturnsAsync(new[] { earlierRaw });

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => Service(repository.Object, timing.Object, settings.Object).ProcessAsync(2, "HR"));
        Assert.Contains("Earlier evidence for this work date must be processed or reviewed first", ex.Message);
    }

    [Fact]
    public async Task ProcessAsync_WhenApprovedManualCorrectionExists_ThrowsInvalidOperationException()
    {
        var repository = new Mock<IAttendanceCorrectionRepository>();
        var settings = new Mock<IAttendanceTimeSettingsRepository>();
        var timing = new Mock<IShiftTimingService>();
        var schedule = ScheduledAttendanceTests.Schedule(Date, 9, 17);
        var raw = Raw(1, 9, "IN");

        repository.Setup(r => r.GetRawEventAsync(1, default)).ReturnsAsync(raw);
        repository.Setup(r => r.BindRawWorkDateAsync(1, Date.ToDateTime(TimeOnly.MinValue), default)).ReturnsAsync(raw);
        settings.Setup(s => s.GetForEmployeeAsync("E", default)).ReturnsAsync(new AttendanceTimeSettings { CompanyId = 1, TimeZoneId = "UTC" });
        timing.Setup(t => t.MatchConfiguredPunchAsync("E", It.IsAny<DateTimeOffset>(), "IN", default)).ReturnsAsync(schedule);
        repository.Setup(r => r.LockRawEventAsync(1, "E", default)).ReturnsAsync(raw);
        repository.Setup(r => r.ExecuteForRawWorkDateAsync("E", Date.ToDateTime(TimeOnly.MinValue), It.IsAny<Func<Task<RawProcessingResult>>>(), default))
            .Returns<string, DateTime, Func<Task<RawProcessingResult>>, CancellationToken>((emp, d, action, ct) => action());
        repository.Setup(r => r.GetEarlierPendingEventsAsync("E", raw.PunchTime, 1, default)).ReturnsAsync(Array.Empty<RawAttendance>());
        repository.Setup(r => r.HasApprovedCorrectionAsync("E", Date.ToDateTime(TimeOnly.MinValue), default)).ReturnsAsync(true);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => Service(repository.Object, timing.Object, settings.Object).ProcessAsync(1, "HR"));
        Assert.Contains("This day has an approved manual correction", ex.Message);
    }

    [Fact]
    public async Task ProcessAsync_WhenLegacyDayHasNoTimeZone_ThrowsInvalidOperationException()
    {
        var repository = new Mock<IAttendanceCorrectionRepository>();
        var settings = new Mock<IAttendanceTimeSettingsRepository>();
        var timing = new Mock<IShiftTimingService>();
        var schedule = ScheduledAttendanceTests.Schedule(Date, 9, 17);
        var raw = Raw(1, 9, "IN");

        repository.Setup(r => r.GetRawEventAsync(1, default)).ReturnsAsync(raw);
        repository.Setup(r => r.BindRawWorkDateAsync(1, Date.ToDateTime(TimeOnly.MinValue), default)).ReturnsAsync(raw);
        settings.Setup(s => s.GetForEmployeeAsync("E", default)).ReturnsAsync(new AttendanceTimeSettings { CompanyId = 1, TimeZoneId = "UTC" });
        timing.Setup(t => t.MatchConfiguredPunchAsync("E", It.IsAny<DateTimeOffset>(), "IN", default)).ReturnsAsync(schedule);
        repository.Setup(r => r.LockRawEventAsync(1, "E", default)).ReturnsAsync(raw);
        repository.Setup(r => r.ExecuteForRawWorkDateAsync("E", Date.ToDateTime(TimeOnly.MinValue), It.IsAny<Func<Task<RawProcessingResult>>>(), default))
            .Returns<string, DateTime, Func<Task<RawProcessingResult>>, CancellationToken>((emp, d, action, ct) => action());
        repository.Setup(r => r.GetEarlierPendingEventsAsync("E", raw.PunchTime, 1, default)).ReturnsAsync(Array.Empty<RawAttendance>());
        repository.Setup(r => r.HasApprovedCorrectionAsync("E", Date.ToDateTime(TimeOnly.MinValue), default)).ReturnsAsync(false);

        var legacyDay = new AttendanceDay { AttendanceDate = Date.ToDateTime(TimeOnly.MinValue), TimeZoneId = null };
        repository.Setup(r => r.GetAttendanceDayAsync("E", Date.ToDateTime(TimeOnly.MinValue), default)).ReturnsAsync(legacyDay);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => Service(repository.Object, timing.Object, settings.Object).ProcessAsync(1, "HR"));
        Assert.Contains("Legacy day needs a reviewed shift/time snapshot", ex.Message);
    }

    [Fact]
    public async Task ProcessAsync_WhenSuccessfulInPunch_CreatesNewAttendanceDayAndBindsGeofence()
    {
        var repository = new Mock<IAttendanceCorrectionRepository>();
        var settings = new Mock<IAttendanceTimeSettingsRepository>();
        var timing = new Mock<IShiftTimingService>();
        var classifier = new Mock<IAttendanceDayClassifier>();
        var schedule = ScheduledAttendanceTests.Schedule(Date, 9, 17);
        var raw = Raw(1, 9, "IN");
        raw.BranchId = 5;
        raw.Latitude = 31.95m;
        raw.Longitude = 35.91m;
        raw.DistanceToBranchMeters = 25m;
        raw.IsWithinGeofence = true;

        repository.Setup(r => r.GetRawEventAsync(1, default)).ReturnsAsync(raw);
        repository.Setup(r => r.BindRawWorkDateAsync(1, Date.ToDateTime(TimeOnly.MinValue), default)).ReturnsAsync(raw);
        settings.Setup(s => s.GetForEmployeeAsync("E", default)).ReturnsAsync(new AttendanceTimeSettings { CompanyId = 1, TimeZoneId = "UTC" });
        timing.Setup(t => t.MatchConfiguredPunchAsync("E", It.IsAny<DateTimeOffset>(), "IN", default)).ReturnsAsync(schedule);
        repository.Setup(r => r.LockRawEventAsync(1, "E", default)).ReturnsAsync(raw);
        repository.Setup(r => r.ExecuteForRawWorkDateAsync("E", Date.ToDateTime(TimeOnly.MinValue), It.IsAny<Func<Task<RawProcessingResult>>>(), default))
            .Returns<string, DateTime, Func<Task<RawProcessingResult>>, CancellationToken>((emp, d, action, ct) => action());
        repository.Setup(r => r.GetEarlierPendingEventsAsync("E", raw.PunchTime, 1, default)).ReturnsAsync(Array.Empty<RawAttendance>());
        repository.Setup(r => r.HasApprovedCorrectionAsync("E", Date.ToDateTime(TimeOnly.MinValue), default)).ReturnsAsync(false);
        repository.Setup(r => r.GetAttendanceDayAsync("E", Date.ToDateTime(TimeOnly.MinValue), default)).ReturnsAsync((AttendanceDay?)null);
        repository.Setup(r => r.GetProcessedDayEventsAsync("E", Date.ToDateTime(TimeOnly.MinValue), default)).ReturnsAsync(Array.Empty<RawAttendance>());

        AttendanceDay? addedDay = null;
        repository.Setup(r => r.AddAttendanceDayAsync(It.IsAny<AttendanceDay>(), default))
            .Callback<AttendanceDay, CancellationToken>((d, ct) => { d.Id = 99; addedDay = d; })
            .Returns(Task.CompletedTask);

        classifier.Setup(c => c.ClassifyAsync(It.IsAny<AttendanceDay>(), schedule, default))
            .ReturnsAsync((AttendanceDay d, TimedShiftDto s, CancellationToken ct) => new AttendanceDayClassificationDto(d, false, false, false, false, null));

        var result = await Service(repository.Object, timing.Object, settings.Object, classifier.Object).ProcessAsync(1, "ADMIN");

        Assert.True(result.Applied);
        Assert.False(result.AlreadyTerminal);
        Assert.Equal(99, result.AttendanceDayId);
        Assert.Equal("APPLIED", result.Outcome);
        Assert.NotNull(addedDay);
        Assert.Equal(5, addedDay.CheckInBranchId);
        Assert.Equal(31.95m, addedDay.CheckInLatitude);
        Assert.Equal(35.91m, addedDay.CheckInLongitude);
        Assert.Equal(25m, addedDay.CheckInDistanceMeters);
        Assert.True(addedDay.IsCheckInWithinGeofence);
        Assert.False(addedDay.HasGeofenceViolation);
        Assert.True(raw.IsProcessed);
        Assert.Equal("APPLIED", raw.ProcessingOutcome);
        Assert.Equal("ADMIN", raw.ProcessedBy);
    }

    [Fact]
    public async Task ProcessAsync_WhenSuccessfulOutPunchForExistingDay_UpdatesAttendanceDay()
    {
        var repository = new Mock<IAttendanceCorrectionRepository>();
        var settings = new Mock<IAttendanceTimeSettingsRepository>();
        var timing = new Mock<IShiftTimingService>();
        var classifier = new Mock<IAttendanceDayClassifier>();
        var schedule = ScheduledAttendanceTests.Schedule(Date, 9, 17);
        var inPunch = Raw(1, 9, "IN");
        var outPunch = Raw(2, 17, "OUT");
        outPunch.BranchId = 5;
        outPunch.Latitude = 31.95m;
        outPunch.Longitude = 35.91m;
        outPunch.DistanceToBranchMeters = 30m;
        outPunch.IsWithinGeofence = true;

        var existingDay = ScheduledAttendanceCalculator.Calculate("E", Date, inPunch.PunchTime, null, schedule, new AttendancePolicy());
        existingDay.Id = 50;

        repository.Setup(r => r.GetRawEventAsync(2, default)).ReturnsAsync(outPunch);
        repository.Setup(r => r.BindRawWorkDateAsync(2, Date.ToDateTime(TimeOnly.MinValue), default)).ReturnsAsync(outPunch);
        settings.Setup(s => s.GetForEmployeeAsync("E", default)).ReturnsAsync(new AttendanceTimeSettings { CompanyId = 1, TimeZoneId = "UTC" });
        timing.Setup(t => t.PreviewConfiguredAsync("E", Date, default)).ReturnsAsync(schedule);
        repository.Setup(r => r.LockRawEventAsync(2, "E", default)).ReturnsAsync(outPunch);
        repository.Setup(r => r.ExecuteForRawWorkDateAsync("E", Date.ToDateTime(TimeOnly.MinValue), It.IsAny<Func<Task<RawProcessingResult>>>(), default))
            .Returns<string, DateTime, Func<Task<RawProcessingResult>>, CancellationToken>((emp, d, action, ct) => action());
        repository.Setup(r => r.GetEarlierPendingEventsAsync("E", outPunch.PunchTime, 2, default)).ReturnsAsync(Array.Empty<RawAttendance>());
        repository.Setup(r => r.HasApprovedCorrectionAsync("E", Date.ToDateTime(TimeOnly.MinValue), default)).ReturnsAsync(false);
        repository.Setup(r => r.GetAttendanceDayAsync("E", Date.ToDateTime(TimeOnly.MinValue), default)).ReturnsAsync(existingDay);
        repository.Setup(r => r.GetProcessedDayEventsAsync("E", Date.ToDateTime(TimeOnly.MinValue), default)).ReturnsAsync(new[] { inPunch });

        classifier.Setup(c => c.ClassifyAsync(It.IsAny<AttendanceDay>(), schedule, default))
            .ReturnsAsync((AttendanceDay d, TimedShiftDto s, CancellationToken ct) => new AttendanceDayClassificationDto(d, false, false, false, false, null));

        var result = await Service(repository.Object, timing.Object, settings.Object, classifier.Object).ProcessAsync(2, "ADMIN");

        Assert.True(result.Applied);
        Assert.Equal(50, result.AttendanceDayId);
        Assert.Equal(outPunch.PunchTime, existingDay.LastCheckOut);
        Assert.Equal(5, existingDay.CheckOutBranchId);
        Assert.Equal(31.95m, existingDay.CheckOutLatitude);
        Assert.Equal(35.91m, existingDay.CheckOutLongitude);
        Assert.Equal(30m, existingDay.CheckOutDistanceMeters);
        Assert.True(existingDay.IsCheckOutWithinGeofence);
        Assert.False(existingDay.HasGeofenceViolation);
        repository.Verify(r => r.UpdateAttendanceDayAsync(existingDay, default), Times.AtLeastOnce);
    }

    [Fact]
    public async Task ProcessAsync_WhenWithinGeofenceFalse_SetsHasGeofenceViolationTrue()
    {
        var repository = new Mock<IAttendanceCorrectionRepository>();
        var settings = new Mock<IAttendanceTimeSettingsRepository>();
        var timing = new Mock<IShiftTimingService>();
        var classifier = new Mock<IAttendanceDayClassifier>();
        var schedule = ScheduledAttendanceTests.Schedule(Date, 9, 17);
        var raw = Raw(1, 9, "IN");
        raw.BranchId = 5;
        raw.DistanceToBranchMeters = 500m;
        raw.IsWithinGeofence = false;

        repository.Setup(r => r.GetRawEventAsync(1, default)).ReturnsAsync(raw);
        repository.Setup(r => r.BindRawWorkDateAsync(1, Date.ToDateTime(TimeOnly.MinValue), default)).ReturnsAsync(raw);
        settings.Setup(s => s.GetForEmployeeAsync("E", default)).ReturnsAsync(new AttendanceTimeSettings { CompanyId = 1, TimeZoneId = "UTC" });
        timing.Setup(t => t.MatchConfiguredPunchAsync("E", It.IsAny<DateTimeOffset>(), "IN", default)).ReturnsAsync(schedule);
        repository.Setup(r => r.LockRawEventAsync(1, "E", default)).ReturnsAsync(raw);
        repository.Setup(r => r.ExecuteForRawWorkDateAsync("E", Date.ToDateTime(TimeOnly.MinValue), It.IsAny<Func<Task<RawProcessingResult>>>(), default))
            .Returns<string, DateTime, Func<Task<RawProcessingResult>>, CancellationToken>((emp, d, action, ct) => action());
        repository.Setup(r => r.GetEarlierPendingEventsAsync("E", raw.PunchTime, 1, default)).ReturnsAsync(Array.Empty<RawAttendance>());
        repository.Setup(r => r.HasApprovedCorrectionAsync("E", Date.ToDateTime(TimeOnly.MinValue), default)).ReturnsAsync(false);
        repository.Setup(r => r.GetAttendanceDayAsync("E", Date.ToDateTime(TimeOnly.MinValue), default)).ReturnsAsync((AttendanceDay?)null);
        repository.Setup(r => r.GetProcessedDayEventsAsync("E", Date.ToDateTime(TimeOnly.MinValue), default)).ReturnsAsync(Array.Empty<RawAttendance>());

        AttendanceDay? addedDay = null;
        repository.Setup(r => r.AddAttendanceDayAsync(It.IsAny<AttendanceDay>(), default))
            .Callback<AttendanceDay, CancellationToken>((d, ct) => { d.Id = 88; addedDay = d; })
            .Returns(Task.CompletedTask);

        classifier.Setup(c => c.ClassifyAsync(It.IsAny<AttendanceDay>(), schedule, default))
            .ReturnsAsync((AttendanceDay d, TimedShiftDto s, CancellationToken ct) => new AttendanceDayClassificationDto(d, false, false, false, false, null));

        var result = await Service(repository.Object, timing.Object, settings.Object, classifier.Object).ProcessAsync(1, "ADMIN");

        Assert.True(result.Applied);
        Assert.NotNull(addedDay);
        Assert.False(addedDay.IsCheckInWithinGeofence);
        Assert.True(addedDay.HasGeofenceViolation);
    }

    [Fact]
    public async Task GetAsync_PropagatesCancellationToken()
    {
        var repository = new Mock<IAttendanceCorrectionRepository>();
        using var cts = new CancellationTokenSource();
        cts.Cancel();
        repository.Setup(r => r.GetRawEventAsync(1, cts.Token)).ThrowsAsync(new OperationCanceledException(cts.Token));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => Service(repository.Object).GetAsync(1, cts.Token));
    }

    [Fact]
    public async Task IgnoreAsync_PropagatesCancellationToken()
    {
        var repository = new Mock<IAttendanceCorrectionRepository>();
        using var cts = new CancellationTokenSource();
        cts.Cancel();
        repository.Setup(r => r.IgnoreRawEventAsync(1, "reason", "USER", cts.Token)).ThrowsAsync(new OperationCanceledException(cts.Token));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => Service(repository.Object).IgnoreAsync(1, "reason", "USER", cts.Token));
    }

    [Fact]
    public async Task ProcessAsync_PropagatesCancellationToken()
    {
        var repository = new Mock<IAttendanceCorrectionRepository>();
        using var cts = new CancellationTokenSource();
        cts.Cancel();
        repository.Setup(r => r.GetRawEventAsync(1, cts.Token)).ThrowsAsync(new OperationCanceledException(cts.Token));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => Service(repository.Object).ProcessAsync(1, "USER", cts.Token));
    }

    [Fact]
    public async Task ProcessBatchAsync_PropagatesCancellationToken()
    {
        var repository = new Mock<IAttendanceCorrectionRepository>();
        using var cts = new CancellationTokenSource();
        cts.Cancel();
        repository.Setup(r => r.GetUnprocessedRawPunchesAsync(10, cts.Token)).ThrowsAsync(new OperationCanceledException(cts.Token));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => Service(repository.Object).ProcessBatchAsync(10, "USER", cts.Token));
    }

    [Fact]
    public async Task ProcessBatchAsync_WhenCancellationRequested_ThrowsOperationCanceledException()
    {
        var repository = new Mock<IAttendanceCorrectionRepository>();
        var raw = Raw(1, 9, "IN");
        using var cts = new CancellationTokenSource();
        repository.Setup(r => r.GetUnprocessedRawPunchesAsync(10, cts.Token)).ReturnsAsync(new[] { raw });
        cts.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => Service(repository.Object).ProcessBatchAsync(10, "USER", cts.Token));
    }

    [Fact]
    public async Task ProcessAsync_WhenRawEventBecomesNullDuringLocking_ThrowsKeyNotFoundException()
    {
        var repository = new Mock<IAttendanceCorrectionRepository>();
        var settings = new Mock<IAttendanceTimeSettingsRepository>();
        var timing = new Mock<IShiftTimingService>();
        var schedule = ScheduledAttendanceTests.Schedule(Date, 9, 17);
        var raw = Raw(1, 9, "IN");

        repository.Setup(r => r.GetRawEventAsync(1, default)).ReturnsAsync(raw);
        repository.Setup(r => r.BindRawWorkDateAsync(1, Date.ToDateTime(TimeOnly.MinValue), default)).ReturnsAsync(raw);
        settings.Setup(s => s.GetForEmployeeAsync("E", default)).ReturnsAsync(new AttendanceTimeSettings { CompanyId = 1, TimeZoneId = "UTC" });
        timing.Setup(t => t.MatchConfiguredPunchAsync("E", It.IsAny<DateTimeOffset>(), "IN", default)).ReturnsAsync(schedule);
        repository.Setup(r => r.LockRawEventAsync(1, "E", default)).ReturnsAsync((RawAttendance?)null);
        repository.Setup(r => r.ExecuteForRawWorkDateAsync("E", Date.ToDateTime(TimeOnly.MinValue), It.IsAny<Func<Task<RawProcessingResult>>>(), default))
            .Returns<string, DateTime, Func<Task<RawProcessingResult>>, CancellationToken>((emp, d, action, ct) => action());

        var ex = await Assert.ThrowsAsync<KeyNotFoundException>(() => Service(repository.Object, timing.Object, settings.Object).ProcessAsync(1, "HR"));
        Assert.Contains("Raw event no longer visible", ex.Message);
    }

    [Fact]
    public async Task ProcessAsync_WhenPunchTimeChangedDuringLocking_ThrowsInvalidOperationException()
    {
        var repository = new Mock<IAttendanceCorrectionRepository>();
        var settings = new Mock<IAttendanceTimeSettingsRepository>();
        var timing = new Mock<IShiftTimingService>();
        var schedule = ScheduledAttendanceTests.Schedule(Date, 9, 17);
        var raw = Raw(1, 9, "IN");

        repository.Setup(r => r.GetRawEventAsync(1, default)).ReturnsAsync(raw);
        repository.Setup(r => r.BindRawWorkDateAsync(1, Date.ToDateTime(TimeOnly.MinValue), default)).ReturnsAsync(raw);
        settings.Setup(s => s.GetForEmployeeAsync("E", default)).ReturnsAsync(new AttendanceTimeSettings { CompanyId = 1, TimeZoneId = "UTC" });
        timing.Setup(t => t.MatchConfiguredPunchAsync("E", It.IsAny<DateTimeOffset>(), "IN", default)).ReturnsAsync(schedule);

        var modifiedRaw = Raw(1, 9, "IN");
        modifiedRaw.PunchTime = modifiedRaw.PunchTime.AddMinutes(15);
        repository.Setup(r => r.LockRawEventAsync(1, "E", default)).ReturnsAsync(modifiedRaw);
        repository.Setup(r => r.ExecuteForRawWorkDateAsync("E", Date.ToDateTime(TimeOnly.MinValue), It.IsAny<Func<Task<RawProcessingResult>>>(), default))
            .Returns<string, DateTime, Func<Task<RawProcessingResult>>, CancellationToken>((emp, d, action, ct) => action());

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => Service(repository.Object, timing.Object, settings.Object).ProcessAsync(1, "HR"));
        Assert.Contains("Raw evidence changed while acquiring its locks", ex.Message);
    }

    [Fact]
    public async Task ProcessAsync_WhenPunchTypeChangedDuringLocking_ThrowsInvalidOperationException()
    {
        var repository = new Mock<IAttendanceCorrectionRepository>();
        var settings = new Mock<IAttendanceTimeSettingsRepository>();
        var timing = new Mock<IShiftTimingService>();
        var schedule = ScheduledAttendanceTests.Schedule(Date, 9, 17);
        var raw = Raw(1, 9, "IN");

        repository.Setup(r => r.GetRawEventAsync(1, default)).ReturnsAsync(raw);
        repository.Setup(r => r.BindRawWorkDateAsync(1, Date.ToDateTime(TimeOnly.MinValue), default)).ReturnsAsync(raw);
        settings.Setup(s => s.GetForEmployeeAsync("E", default)).ReturnsAsync(new AttendanceTimeSettings { CompanyId = 1, TimeZoneId = "UTC" });
        timing.Setup(t => t.MatchConfiguredPunchAsync("E", It.IsAny<DateTimeOffset>(), "IN", default)).ReturnsAsync(schedule);

        var modifiedRaw = Raw(1, 9, "OUT");
        repository.Setup(r => r.LockRawEventAsync(1, "E", default)).ReturnsAsync(modifiedRaw);
        repository.Setup(r => r.ExecuteForRawWorkDateAsync("E", Date.ToDateTime(TimeOnly.MinValue), It.IsAny<Func<Task<RawProcessingResult>>>(), default))
            .Returns<string, DateTime, Func<Task<RawProcessingResult>>, CancellationToken>((emp, d, action, ct) => action());

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => Service(repository.Object, timing.Object, settings.Object).ProcessAsync(1, "HR"));
        Assert.Contains("Raw evidence changed while acquiring its locks", ex.Message);
    }

    [Fact]
    public async Task ProcessAsync_WhenTerminalEventHasNullOutcome_NeedsReviewIsTrue()
    {
        var repository = new Mock<IAttendanceCorrectionRepository>();
        var raw = Raw(1, 9, "IN");
        raw.IsProcessed = true;
        raw.ProcessingOutcome = null;
        repository.Setup(r => r.GetRawEventAsync(1, default)).ReturnsAsync(raw);

        var result = await Service(repository.Object).ProcessAsync(1, "HR");
        Assert.True(result.AlreadyTerminal);
        Assert.True(result.NeedsReview);
        Assert.Contains("Legacy processed evidence has no outcome/link audit", result.Message);
    }

    [Fact]
    public void Map_MapsAllFieldsCorrectly()
    {
        var raw = Raw(55, 14, "OUT");
        raw.DeviceId = "DEV-99";
        raw.Source = "MOBILE";
        raw.ExternalReference = "EXT-REF";
        raw.IsProcessed = true;
        raw.ProcessingOutcome = "APPLIED";
        raw.ProcessingReason = "Normal check-out";
        raw.ProcessedBy = "SYSTEM";
        raw.ProcessedDate = new DateTime(2026, 3, 10, 14, 5, 0, DateTimeKind.Utc);
        raw.LastAttemptedBy = "ADMIN";
        raw.LastAttemptedAtUtc = new DateTime(2026, 3, 10, 14, 1, 0, DateTimeKind.Utc);
        raw.ResolvedWorkDate = Date.ToDateTime(TimeOnly.MinValue);
        raw.AttendanceDayId = 120;

        var dto = RawAttendanceProcessingService.Map(raw);

        Assert.Equal(55, dto.Id);
        Assert.Equal("E", dto.EmployeeCode);
        Assert.Equal("OUT", dto.PunchType);
        Assert.Equal("DEV-99", dto.DeviceId);
        Assert.Equal("MOBILE", dto.Source);
        Assert.Equal("EXT-REF", dto.ExternalReference);
        Assert.Equal("D:55", dto.EventId);
        Assert.True(dto.IsProcessed);
        Assert.Equal(Date, dto.WorkDate);
        Assert.Equal(120, dto.AttendanceDayId);
        Assert.Equal("APPLIED", dto.Outcome);
        Assert.Equal("Normal check-out", dto.Reason);
        Assert.Equal("SYSTEM", dto.ProcessedBy);
        Assert.Equal(raw.ProcessedDate, dto.ProcessedAtUtc?.UtcDateTime);
        Assert.Equal("ADMIN", dto.LastAttemptedBy);
        Assert.Equal(raw.LastAttemptedAtUtc, dto.LastAttemptedAtUtc?.UtcDateTime);
    }

    [Fact]
    public void AttendanceEvidenceSummary_Build_MultipleSessionsAllowed_ComputesCorrectDurationAndSessionsCount()
    {
        var punches = new[]
        {
            Raw(1, 8, "IN"),
            Raw(2, 12, "OUT"),
            Raw(3, 13, "IN"),
            Raw(4, 17, "OUT")
        };

        var summary = AttendanceEvidenceSummary.Build(punches, null, allowMultipleSessions: true);

        Assert.False(summary.NeedsReview);
        Assert.Equal(2, summary.TotalSessionsCount);
        Assert.Equal(480m, summary.TotalSessionMinutes); // (4h * 60) + (4h * 60) = 480
        Assert.Equal(60m, summary.TotalBreakGapMinutes);  // 12:00 to 13:00 = 60m
        Assert.False(summary.IsCurrentlyInside);
        Assert.Equal(punches[0].PunchTime, summary.FirstIn);
        Assert.Equal(punches[3].PunchTime, summary.LastOut);
    }

    [Fact]
    public void AttendanceEvidenceSummary_Build_WhenTiedOppositePunchesAtExactSameTick_FlagsNeedsReview()
    {
        var punchTime = Date.ToDateTime(new TimeOnly(9, 0), DateTimeKind.Utc);
        var inPunch = new RawAttendance { Id = 1, EmployeeCode = "E", PunchTime = punchTime, PunchType = "IN" };
        var outPunch = new RawAttendance { Id = 2, EmployeeCode = "E", PunchTime = punchTime, PunchType = "OUT" };

        var summary = AttendanceEvidenceSummary.Build(new[] { inPunch, outPunch }, null);

        Assert.True(summary.NeedsReview);
    }

    [Fact]
    public void AttendanceEvidenceSummary_Build_WhenNonStandardPunchTypePresent_FlagsNeedsReview()
    {
        var normalIn = Raw(1, 9, "IN");
        var breakPunch = Raw(2, 12, "BREAK");

        var summary = AttendanceEvidenceSummary.Build(new[] { normalIn, breakPunch }, null);

        Assert.True(summary.NeedsReview);
    }

    [Fact]
    public void AttendanceEvidenceSummary_Build_WhenFirstInAfterLastOut_FlagsNeedsReview()
    {
        // First IN is at 17:00, Last OUT is at 08:00 (inverted/reversed punches)
        var outPunch = Raw(1, 8, "OUT");
        var inPunch = Raw(2, 17, "IN");

        var summary = AttendanceEvidenceSummary.Build(new[] { outPunch, inPunch }, null);

        Assert.True(summary.NeedsReview);
    }

    [Fact]
    public void AttendanceEvidenceSummary_Build_WhenOpenSession_FlagsIsCurrentlyInside()
    {
        var inPunch = Raw(1, 9, "IN");

        var summary = AttendanceEvidenceSummary.Build(new[] { inPunch }, null, allowMultipleSessions: true);

        Assert.False(summary.NeedsReview);
        Assert.True(summary.IsCurrentlyInside);
        Assert.Equal(1, summary.TotalSessionsCount);
        Assert.Equal(0m, summary.TotalSessionMinutes);
    }

    [Fact]
    public void AttendanceEvidenceSummary_Build_WhenMultipleConsecutiveInPunches_TakesFirstInAndKeepsInside()
    {
        var in1 = Raw(1, 9, "IN");
        var in2 = new RawAttendance
        {
            Id = 2,
            EmployeeCode = "E",
            PunchTime = Date.ToDateTime(new TimeOnly(9, 5), DateTimeKind.Utc),
            PunchType = "IN"
        };

        var summary = AttendanceEvidenceSummary.Build(new[] { in1, in2 }, null);

        Assert.Equal(in1.PunchTime, summary.FirstIn);
        Assert.True(summary.IsCurrentlyInside);
    }

    [Fact]
    public async Task ProcessAsync_WhenEarlierEvidenceExistsForSameWorkDate_ThrowsInvalidOperationException()
    {
        var repository = new Mock<IAttendanceCorrectionRepository>();
        var timing = new Mock<IShiftTimingService>();
        var settings = new Mock<IAttendanceTimeSettingsRepository>();
        var schedule = ScheduledAttendanceTests.Schedule(Date, 9, 17);

        var currentRaw = Raw(2, 17, "OUT");
        var earlierRaw = Raw(1, 9, "IN");
        earlierRaw.ResolvedWorkDate = Date.ToDateTime(TimeOnly.MinValue);

        repository.Setup(r => r.GetRawEventAsync(2, default)).ReturnsAsync(currentRaw);
        repository.Setup(r => r.BindRawWorkDateAsync(2, Date.ToDateTime(TimeOnly.MinValue), default)).ReturnsAsync(currentRaw);
        repository.Setup(r => r.LockRawEventAsync(2, "E", default)).ReturnsAsync(currentRaw);
        settings.Setup(s => s.GetForEmployeeAsync("E", default)).ReturnsAsync(new AttendanceTimeSettings { CompanyId = 1, TimeZoneId = "UTC" });
        timing.Setup(t => t.MatchConfiguredPunchAsync("E", It.IsAny<DateTimeOffset>(), "OUT", default)).ReturnsAsync(schedule);

        repository.Setup(r => r.GetEarlierPendingEventsAsync("E", currentRaw.PunchTime, 2, default))
            .ReturnsAsync(new[] { earlierRaw });

        repository.Setup(r => r.ExecuteForRawWorkDateAsync("E", Date.ToDateTime(TimeOnly.MinValue), It.IsAny<Func<Task<RawProcessingResult>>>(), default))
            .Returns<string, DateTime, Func<Task<RawProcessingResult>>, CancellationToken>((emp, d, action, ct) => action());

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => Service(repository.Object, timing.Object, settings.Object).ProcessAsync(2, "HR"));
        Assert.Contains("Earlier evidence for this work date must be processed or reviewed first", ex.Message);
    }

    [Fact]
    public async Task ProcessAsync_WhenEarlierPendingVolumeExceeds1000_ThrowsInvalidOperationException()
    {
        var repository = new Mock<IAttendanceCorrectionRepository>();
        var timing = new Mock<IShiftTimingService>();
        var settings = new Mock<IAttendanceTimeSettingsRepository>();
        var schedule = ScheduledAttendanceTests.Schedule(Date, 9, 17);

        var currentRaw = Raw(1005, 17, "OUT");
        repository.Setup(r => r.GetRawEventAsync(1005, default)).ReturnsAsync(currentRaw);
        repository.Setup(r => r.BindRawWorkDateAsync(1005, Date.ToDateTime(TimeOnly.MinValue), default)).ReturnsAsync(currentRaw);
        repository.Setup(r => r.LockRawEventAsync(1005, "E", default)).ReturnsAsync(currentRaw);
        settings.Setup(s => s.GetForEmployeeAsync("E", default)).ReturnsAsync(new AttendanceTimeSettings { CompanyId = 1, TimeZoneId = "UTC" });
        timing.Setup(t => t.MatchConfiguredPunchAsync("E", It.IsAny<DateTimeOffset>(), "OUT", default)).ReturnsAsync(schedule);

        var bigList = new List<RawAttendance>();
        for (int i = 1; i <= 1001; i++) bigList.Add(Raw(i, 8, "IN"));

        repository.Setup(r => r.GetEarlierPendingEventsAsync("E", currentRaw.PunchTime, 1005, default))
            .ReturnsAsync(bigList);

        repository.Setup(r => r.ExecuteForRawWorkDateAsync("E", Date.ToDateTime(TimeOnly.MinValue), It.IsAny<Func<Task<RawProcessingResult>>>(), default))
            .Returns<string, DateTime, Func<Task<RawProcessingResult>>, CancellationToken>((emp, d, action, ct) => action());

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => Service(repository.Object, timing.Object, settings.Object).ProcessAsync(1005, "HR"));
        Assert.Contains("Pending evidence volume requires reviewed batching before processing this event", ex.Message);
    }

    [Fact]
    public async Task ProcessAsync_WhenDayHasApprovedCorrection_ThrowsInvalidOperationException()
    {
        var repository = new Mock<IAttendanceCorrectionRepository>();
        var timing = new Mock<IShiftTimingService>();
        var settings = new Mock<IAttendanceTimeSettingsRepository>();
        var schedule = ScheduledAttendanceTests.Schedule(Date, 9, 17);

        var currentRaw = Raw(1, 9, "IN");
        repository.Setup(r => r.GetRawEventAsync(1, default)).ReturnsAsync(currentRaw);
        repository.Setup(r => r.BindRawWorkDateAsync(1, Date.ToDateTime(TimeOnly.MinValue), default)).ReturnsAsync(currentRaw);
        repository.Setup(r => r.LockRawEventAsync(1, "E", default)).ReturnsAsync(currentRaw);
        settings.Setup(s => s.GetForEmployeeAsync("E", default)).ReturnsAsync(new AttendanceTimeSettings { CompanyId = 1, TimeZoneId = "UTC" });
        timing.Setup(t => t.MatchConfiguredPunchAsync("E", It.IsAny<DateTimeOffset>(), "IN", default)).ReturnsAsync(schedule);
        repository.Setup(r => r.GetEarlierPendingEventsAsync("E", currentRaw.PunchTime, 1, default))
            .ReturnsAsync(Array.Empty<RawAttendance>());

        repository.Setup(r => r.HasApprovedCorrectionAsync("E", Date.ToDateTime(TimeOnly.MinValue), default))
            .ReturnsAsync(true);

        repository.Setup(r => r.ExecuteForRawWorkDateAsync("E", Date.ToDateTime(TimeOnly.MinValue), It.IsAny<Func<Task<RawProcessingResult>>>(), default))
            .Returns<string, DateTime, Func<Task<RawProcessingResult>>, CancellationToken>((emp, d, action, ct) => action());

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => Service(repository.Object, timing.Object, settings.Object).ProcessAsync(1, "HR"));
        Assert.Contains("This day has an approved manual correction", ex.Message);
    }

    [Fact]
    public async Task ProcessAsync_WhenExistingDayHasNullTimeZoneId_ThrowsInvalidOperationException()
    {
        var repository = new Mock<IAttendanceCorrectionRepository>();
        var timing = new Mock<IShiftTimingService>();
        var settings = new Mock<IAttendanceTimeSettingsRepository>();
        var schedule = ScheduledAttendanceTests.Schedule(Date, 9, 17);

        var currentRaw = Raw(1, 9, "IN");
        repository.Setup(r => r.GetRawEventAsync(1, default)).ReturnsAsync(currentRaw);
        repository.Setup(r => r.BindRawWorkDateAsync(1, Date.ToDateTime(TimeOnly.MinValue), default)).ReturnsAsync(currentRaw);
        repository.Setup(r => r.LockRawEventAsync(1, "E", default)).ReturnsAsync(currentRaw);
        settings.Setup(s => s.GetForEmployeeAsync("E", default)).ReturnsAsync(new AttendanceTimeSettings { CompanyId = 1, TimeZoneId = "UTC" });
        timing.Setup(t => t.MatchConfiguredPunchAsync("E", It.IsAny<DateTimeOffset>(), "IN", default)).ReturnsAsync(schedule);
        repository.Setup(r => r.GetEarlierPendingEventsAsync("E", currentRaw.PunchTime, 1, default))
            .ReturnsAsync(Array.Empty<RawAttendance>());

        var legacyDay = new AttendanceDay { EmployeeCode = "E", AttendanceDate = Date.ToDateTime(TimeOnly.MinValue), TimeZoneId = null };
        repository.Setup(r => r.GetAttendanceDayAsync("E", Date.ToDateTime(TimeOnly.MinValue), default))
            .ReturnsAsync(legacyDay);

        repository.Setup(r => r.ExecuteForRawWorkDateAsync("E", Date.ToDateTime(TimeOnly.MinValue), It.IsAny<Func<Task<RawProcessingResult>>>(), default))
            .Returns<string, DateTime, Func<Task<RawProcessingResult>>, CancellationToken>((emp, d, action, ct) => action());

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => Service(repository.Object, timing.Object, settings.Object).ProcessAsync(1, "HR"));
        Assert.Contains("Legacy day needs a reviewed shift/time snapshot", ex.Message);
    }
}
