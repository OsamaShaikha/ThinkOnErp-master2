using Moq;
using ThinkOnErp.Application.DTOs.Hr;
using ThinkOnErp.Application.Services.Hr;
using ThinkOnErp.Domain.Entities.Hr;
using ThinkOnErp.Domain.Interfaces.Hr;
using Xunit;

namespace ThinkOnErp.HR.Tests;

public class AttendanceDayStatusTests
{
    private static readonly DateTimeOffset End = new(2026, 11, 5, 17, 0, 0, TimeSpan.Zero);
    private static AttendanceDayContext Context(bool holiday = false, bool leave = false, decimal days = 1m, bool online = false)
        => new(holiday, leave ? new[] { new LeaveRequest { Status = "APPROVED", LeaveTypeCode = "ANNUAL", DaysRequested = days } } : Array.Empty<LeaveRequest>(), online);

    [Theory]
    [InlineData(true, true, "PRESENT")] [InlineData(true, false, "INCOMPLETE")] [InlineData(false, true, "INCOMPLETE")]
    public void OnlineAndOtherAttendanceEvidenceAreCountedWithoutInventingMissingPunches(bool first, bool last, string expected)
    {
        var result = AttendanceDayStatusRules.Decide("WORKING", first, last, End, End.AddHours(1), Context(online: true));
        Assert.Equal(expected, result.Status); Assert.False(result.NeedsReview);
    }

    [Theory] [InlineData(-1, "PENDING")] [InlineData(0, "ABSENT")] [InlineData(1, "ABSENT")]
    public void AbsenceIsNotFinalUntilTheScheduledUtcEnd(int seconds, string expected)
        => Assert.Equal(expected, AttendanceDayStatusRules.Decide("WORKING", false, false, End, End.AddSeconds(seconds), Context()).Status);

    [Theory] [InlineData("REST_DAY")] [InlineData("UNSCHEDULED")]
    public void NonworkingDateDoesNotBecomeAbsence(string status)
        => Assert.Equal(status, AttendanceDayStatusRules.Decide(status, false, false, null, End, Context()).Status);

    [Fact]
    public void ApprovedLeaveAndHolidayExcuseAbsenceWithoutSelectingPayRates()
    {
        Assert.Equal("HOLIDAY", AttendanceDayStatusRules.Decide("WORKING", false, false, End, End.AddHours(1), Context(holiday: true)).Status);
        var leave = AttendanceDayStatusRules.Decide("WORKING", false, false, End, End.AddHours(1), Context(leave: true));
        Assert.Equal("ON_LEAVE", leave.Status); Assert.Equal("ANNUAL", leave.LeaveTypeCode);
    }

    [Theory] [InlineData(false)] [InlineData(true)]
    public void LeaveConflictAndPartialLeaveRequireReviewInsteadOfLosingEvidence(bool punched)
    {
        var result = AttendanceDayStatusRules.Decide("WORKING", punched, punched, End, End.AddHours(1), Context(leave: true, days: 0.5m));
        Assert.Equal("REVIEW_REQUIRED", result.Status); Assert.True(result.NeedsReview);
    }

    [Fact]
    public void MultipleApprovedLeavesCannotBeSilentlyChosen()
    {
        var context = Context(leave: true);
        context = context with { ApprovedLeaves = context.ApprovedLeaves.Concat(context.ApprovedLeaves).ToArray() };
        Assert.True(AttendanceDayStatusRules.Decide("WORKING", false, false, End, End.AddHours(1), context).NeedsReview);
    }

    [Fact]
    public void OrphanedProcessedOnlineEvidenceDoesNotBecomeAbsence()
        => Assert.Equal("REVIEW_REQUIRED", AttendanceDayStatusRules.Decide("WORKING", false, false, End, End.AddHours(1), Context(online: true)).Status);

    [Fact]
    public void UnprocessedEvidenceCannotBeHiddenByAnExistingCompleteDay()
        => Assert.Equal("REVIEW_REQUIRED", AttendanceDayStatusRules.Decide("WORKING", true, true, End, End.AddHours(1), Context() with { HasPendingEvidence = true }).Status);

    [Fact]
    public async Task SavingStatusMustPassPayrollLockBeforeReadingOrWriting()
    {
        var uow = new Mock<IHrUnitOfWork>(); var date = new DateTime(2026, 11, 5);
        uow.Setup(u => u.ExecuteForEmployeeDatesAsync("E", date, date, It.IsAny<Func<Task<AttendanceDayClassificationDto>>>(), default)).ThrowsAsync(new InvalidOperationException("closed payroll"));
        var attendance = new Mock<IAttendanceCorrectionRepository>(MockBehavior.Strict);
        var settings = new Mock<IAttendanceTimeSettingsRepository>(MockBehavior.Strict);
        var service = new AttendanceDayStatusService(Mock.Of<IShiftTimingService>(), Mock.Of<IAttendanceDayClassifier>(), attendance.Object, settings.Object, Mock.Of<IPolicyRepository>(), uow.Object);
        await Assert.ThrowsAsync<InvalidOperationException>(() => service.SaveAsync("E", DateOnly.FromDateTime(date), "HR"));
        attendance.VerifyNoOtherCalls(); settings.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task PayrollCalculationRejectsUnresolvedAttendanceReview()
    {
        var periods = new Mock<IPayrollPeriodRepository>(); var context = new Mock<IAttendanceDayContextRepository>();
        var period = new PayrollPeriod { CompanyId = 1, PeriodCode = "P", StartDate = End.Date.AddDays(-4), EndDate = End.Date };
        periods.Setup(r => r.GetPeriodByCodeAsync(1, "P", default)).ReturnsAsync(period);
        var employee = new Employee { EmployeeCode = "E", HireDate = period.StartDate.AddYears(-1) };
        employee.SalaryStructures.Add(new SalaryStructure { IsActive = true, EffectiveFrom = employee.HireDate, CurrencyCode = "JOD" });
        periods.Setup(r => r.GetActiveEmployeesAsync(null, default)).ReturnsAsync(new[] { employee });
        context.SetupSequence(r => r.HasReviewDaysAsync(It.Is<IReadOnlyList<string>>(c => c.Count == 1 && c[0] == "E"), period.StartDate, period.EndDate, default))
            .ReturnsAsync(false).ReturnsAsync(true);
        var service = new PayrollValidationService(periods.Object, Mock.Of<IPolicyRepository>(), context.Object);
        Assert.True((await service.ValidatePreCalculationAsync(1, "P")).IsValid);
        var result = await service.ValidatePreCalculationAsync(1, "P");
        Assert.False(result.IsValid); Assert.Contains(result.Errors, e => e.Contains("unresolved review-required"));
    }

    [Fact]
    public async Task PayrollApprovalAlsoRejectsReviewRequiredAttendance()
    {
        var periods = new Mock<IPayrollPeriodRepository>(); var context = new Mock<IAttendanceDayContextRepository>();
        var run = new PayrollRun { Id = 1, Status = "CALCULATED", PayPeriod = "P" }; run.Lines.Add(new() { EmployeeCode = "E" });
        var period = new PayrollPeriod { StartDate = End.Date.AddDays(-4), EndDate = End.Date };
        periods.Setup(r => r.GetPayrollRunByIdAsync(1, default)).ReturnsAsync(run);
        periods.Setup(r => r.GetPeriodForRunAsync(run, default)).ReturnsAsync(period);
        context.Setup(r => r.HasReviewDaysAsync(It.IsAny<IReadOnlyList<string>>(), period.StartDate, period.EndDate, default)).ReturnsAsync(true);
        var result = await new PayrollValidationService(periods.Object, Mock.Of<IPolicyRepository>(), context.Object).ValidatePostApprovalAsync(1);
        Assert.False(result.IsValid); Assert.Contains(result.Errors, e => e.Contains("Resolve them before approval"));
    }

    [Fact]
    public void Decide_WhenWorkingDayAndScheduledEndNull_ThrowsInvalidOperationException()
    {
        var ex = Assert.Throws<InvalidOperationException>(() =>
            AttendanceDayStatusRules.Decide("WORKING", false, false, null, End, Context()));
        Assert.Contains("Working-day classification requires an explicit scheduled end", ex.Message);
    }

    [Fact]
    public void Decide_WhenHasPendingEvidence_ReturnsReviewRequiredWithPendingEvidenceReason()
    {
        var result = AttendanceDayStatusRules.Decide("WORKING", true, true, End, End.AddHours(1),
            Context() with { HasPendingEvidence = true });
        Assert.Equal("REVIEW_REQUIRED", result.Status);
        Assert.True(result.NeedsReview);
        Assert.Contains("Unprocessed evidence must be applied or explicitly reviewed", result.ReviewReason);
    }

    [Fact]
    public void Decide_WhenPunchOverlapsApprovedLeave_ReturnsReviewRequiredWithConflictReason()
    {
        var result = AttendanceDayStatusRules.Decide("WORKING", true, false, End, End.AddHours(1),
            Context(leave: true, days: 1m));
        Assert.Equal("REVIEW_REQUIRED", result.Status);
        Assert.True(result.NeedsReview);
        Assert.Contains("Recorded attendance overlaps approved leave", result.ReviewReason);
    }

    [Fact]
    public void Decide_WhenSingleApprovedLeaveOfFullDay_ReturnsOnLeave()
    {
        var result = AttendanceDayStatusRules.Decide("WORKING", false, false, End, End.AddHours(1),
            Context(leave: true, days: 1m));
        Assert.Equal("ON_LEAVE", result.Status);
        Assert.Equal("ANNUAL", result.LeaveTypeCode);
        Assert.False(result.NeedsReview);
        Assert.Null(result.ReviewReason);
    }

    [Fact]
    public void Decide_WhenSingleApprovedLeaveOfPartialDay_ReturnsReviewRequired()
    {
        var result = AttendanceDayStatusRules.Decide("WORKING", false, false, End, End.AddHours(1),
            Context(leave: true, days: 0.5m));
        Assert.Equal("REVIEW_REQUIRED", result.Status);
        Assert.Equal("ANNUAL", result.LeaveTypeCode);
        Assert.True(result.NeedsReview);
        Assert.Contains("Partial-day leave has no timed interval", result.ReviewReason);
    }

    [Fact]
    public void Decide_WhenMultipleApprovedLeavesOverlap_ReturnsReviewRequiredWithMultipleReason()
    {
        var leaves = new List<LeaveRequest>
        {
            new() { Status = "APPROVED", LeaveTypeCode = "ANNUAL", DaysRequested = 1m },
            new() { Status = "APPROVED", LeaveTypeCode = "SICK", DaysRequested = 1m }
        };
        var context = new AttendanceDayContext(false, leaves, false);
        var result = AttendanceDayStatusRules.Decide("WORKING", false, false, End, End.AddHours(1), context);
        Assert.Equal("REVIEW_REQUIRED", result.Status);
        Assert.True(result.NeedsReview);
        Assert.Contains("Multiple approved leaves overlap this work date", result.ReviewReason);
    }

    [Fact]
    public void Decide_WhenOnlineEvidenceWithoutPunches_ReturnsReviewRequiredWithOnlineReason()
    {
        var result = AttendanceDayStatusRules.Decide("WORKING", false, false, End, End.AddHours(1), Context(online: true));
        Assert.Equal("REVIEW_REQUIRED", result.Status);
        Assert.True(result.NeedsReview);
        Assert.Contains("Processed online evidence has no daily punches", result.ReviewReason);
    }

    [Fact]
    public void Decide_WhenHoliday_ReturnsHolidayStatusWithoutReview()
    {
        var result = AttendanceDayStatusRules.Decide("WORKING", false, false, End, End.AddHours(1), Context(holiday: true));
        Assert.Equal("HOLIDAY", result.Status);
        Assert.False(result.NeedsReview);
        Assert.Null(result.ReviewReason);
    }

    [Theory]
    [InlineData("REST_DAY")]
    [InlineData("UNSCHEDULED")]
    public void Decide_WhenRestDayOrUnscheduled_ReturnsScheduleStatusDirectly(string scheduleStatus)
    {
        var result = AttendanceDayStatusRules.Decide(scheduleStatus, false, false, null, End, Context());
        Assert.Equal(scheduleStatus, result.Status);
        Assert.False(result.NeedsReview);
        Assert.Null(result.ReviewReason);
    }

    [Fact]
    public async Task PreviewAsync_WhenLegacyAttendanceHasNoTimeZone_ThrowsInvalidOperationException()
    {
        var timing = new Mock<IShiftTimingService>();
        var classifier = new Mock<IAttendanceDayClassifier>();
        var attendance = new Mock<IAttendanceCorrectionRepository>();
        var settings = new Mock<IAttendanceTimeSettingsRepository>();
        var policies = new Mock<IPolicyRepository>();
        var unitOfWork = new Mock<IHrUnitOfWork>();

        settings.Setup(s => s.GetForEmployeeAsync("E1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AttendanceTimeSettings { CompanyId = 1, TimeZoneId = "Jordan Standard Time" });
        var shiftDto = new EffectiveShiftDto("E1", new DateOnly(2026, 11, 5), "WORKING", "BASE", "A", 1, null, null, null, 0, 480, 8);
        timing.Setup(t => t.PreviewConfiguredAsync("E1", new DateOnly(2026, 11, 5), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new TimedShiftDto(shiftDto, "Jordan Standard Time", DateTimeOffset.UtcNow.AddHours(-8), DateTimeOffset.UtcNow, 480));
        attendance.Setup(a => a.GetAttendanceDayAsync("E1", new DateTime(2026, 11, 5), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AttendanceDay { EmployeeCode = "E1", AttendanceDate = new DateTime(2026, 11, 5), TimeZoneId = null });

        var service = new AttendanceDayStatusService(timing.Object, classifier.Object, attendance.Object, settings.Object, policies.Object, unitOfWork.Object);
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => service.PreviewAsync("E1", new DateOnly(2026, 11, 5)));
        Assert.Contains("Legacy attendance needs a reviewed time/shift snapshot", ex.Message);
    }

    [Fact]
    public async Task SaveAsync_WhenWorkDateInFuture_ThrowsInvalidOperationException()
    {
        var unitOfWork = new Mock<IHrUnitOfWork>();
        var futureDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(10));
        unitOfWork.Setup(u => u.ExecuteForEmployeeDatesAsync("E1", futureDate.ToDateTime(TimeOnly.MinValue), futureDate.ToDateTime(TimeOnly.MinValue), It.IsAny<Func<Task<AttendanceDayClassificationDto>>>(), It.IsAny<CancellationToken>()))
            .Returns((string emp, DateTime f, DateTime t, Func<Task<AttendanceDayClassificationDto>> action, CancellationToken ct) => action());

        var settings = new Mock<IAttendanceTimeSettingsRepository>();
        settings.Setup(s => s.GetForEmployeeAsync("E1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AttendanceTimeSettings { CompanyId = 1, TimeZoneId = "UTC" });

        var service = new AttendanceDayStatusService(Mock.Of<IShiftTimingService>(), Mock.Of<IAttendanceDayClassifier>(), Mock.Of<IAttendanceCorrectionRepository>(), settings.Object, Mock.Of<IPolicyRepository>(), unitOfWork.Object);
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => service.SaveAsync("E1", futureDate, "USER"));
        Assert.Contains("Future attendance dates cannot be finalized", ex.Message);
    }

    [Fact]
    public async Task SaveAsync_WhenPreviewResultIsPending_ThrowsInvalidOperationException()
    {
        var unitOfWork = new Mock<IHrUnitOfWork>();
        var pastDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-1));
        unitOfWork.Setup(u => u.ExecuteForEmployeeDatesAsync("E1", pastDate.ToDateTime(TimeOnly.MinValue), pastDate.ToDateTime(TimeOnly.MinValue), It.IsAny<Func<Task<AttendanceDayClassificationDto>>>(), It.IsAny<CancellationToken>()))
            .Returns((string emp, DateTime f, DateTime t, Func<Task<AttendanceDayClassificationDto>> action, CancellationToken ct) => action());

        var settings = new Mock<IAttendanceTimeSettingsRepository>();
        settings.Setup(s => s.GetForEmployeeAsync("E1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AttendanceTimeSettings { CompanyId = 1, TimeZoneId = "UTC" });

        var shiftDto = new EffectiveShiftDto("E1", pastDate, "WORKING", "BASE", "A", 1, null, null, null, 0, 480, 8);
        var timedDto = new TimedShiftDto(shiftDto, "UTC", DateTimeOffset.UtcNow.AddHours(-8), DateTimeOffset.UtcNow, 480);
        var timing = new Mock<IShiftTimingService>();
        timing.Setup(t => t.PreviewConfiguredAsync("E1", pastDate, It.IsAny<CancellationToken>()))
            .ReturnsAsync(timedDto);

        var attendance = new Mock<IAttendanceCorrectionRepository>();
        attendance.Setup(a => a.GetAttendanceDayAsync("E1", pastDate.ToDateTime(TimeOnly.MinValue), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AttendanceDay { EmployeeCode = "E1", AttendanceDate = pastDate.ToDateTime(TimeOnly.MinValue), TimeZoneId = "UTC" });

        var classifier = new Mock<IAttendanceDayClassifier>();
        classifier.Setup(c => c.ClassifyAsync(It.IsAny<AttendanceDay>(), It.IsAny<TimedShiftDto>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AttendanceDayClassificationDto(new AttendanceDay { Status = "PENDING" }, false, false, false, false, null));

        var service = new AttendanceDayStatusService(timing.Object, classifier.Object, attendance.Object, settings.Object, Mock.Of<IPolicyRepository>(), unitOfWork.Object);
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => service.SaveAsync("E1", pastDate, "USER"));
        Assert.Contains("The scheduled work period has not ended", ex.Message);
    }

    [Fact]
    public async Task SaveAsync_WhenNoExistingDay_AddsNewAttendanceDay()
    {
        var unitOfWork = new Mock<IHrUnitOfWork>();
        var pastDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-1));
        unitOfWork.Setup(u => u.ExecuteForEmployeeDatesAsync("E1", pastDate.ToDateTime(TimeOnly.MinValue), pastDate.ToDateTime(TimeOnly.MinValue), It.IsAny<Func<Task<AttendanceDayClassificationDto>>>(), It.IsAny<CancellationToken>()))
            .Returns((string emp, DateTime f, DateTime t, Func<Task<AttendanceDayClassificationDto>> action, CancellationToken ct) => action());

        var settings = new Mock<IAttendanceTimeSettingsRepository>();
        settings.Setup(s => s.GetForEmployeeAsync("E1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AttendanceTimeSettings { CompanyId = 1, TimeZoneId = "UTC" });

        var shiftDto = new EffectiveShiftDto("E1", pastDate, "WORKING", "BASE", "A", 1, null, null, null, 0, 480, 8);
        var timedDto = new TimedShiftDto(shiftDto, "UTC", DateTimeOffset.UtcNow.AddHours(-8), DateTimeOffset.UtcNow, 480);
        var timing = new Mock<IShiftTimingService>();
        timing.Setup(t => t.PreviewConfiguredAsync("E1", pastDate, It.IsAny<CancellationToken>()))
            .ReturnsAsync(timedDto);

        var attendance = new Mock<IAttendanceCorrectionRepository>();
        attendance.Setup(a => a.GetAttendanceDayAsync("E1", pastDate.ToDateTime(TimeOnly.MinValue), It.IsAny<CancellationToken>()))
            .ReturnsAsync((AttendanceDay?)null);

        var classifiedDay = new AttendanceDay { EmployeeCode = "E1", AttendanceDate = pastDate.ToDateTime(TimeOnly.MinValue), Status = "PRESENT" };
        var classifier = new Mock<IAttendanceDayClassifier>();
        classifier.Setup(c => c.ClassifyAsync(It.IsAny<AttendanceDay>(), It.IsAny<TimedShiftDto>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AttendanceDayClassificationDto(classifiedDay, false, false, false, false, null));

        var service = new AttendanceDayStatusService(timing.Object, classifier.Object, attendance.Object, settings.Object, Mock.Of<IPolicyRepository>(), unitOfWork.Object);
        var result = await service.SaveAsync("E1", pastDate, "SAVER_USER");

        Assert.Equal("PRESENT", result.Day.Status);
        Assert.Equal("SAVER_USER", result.Day.CreationUser);
        attendance.Verify(a => a.AddAttendanceDayAsync(classifiedDay, It.IsAny<CancellationToken>()), Times.Once);
        attendance.Verify(a => a.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Theory]
    [InlineData(DateTimeKind.Local, DateTimeKind.Utc)]
    [InlineData(DateTimeKind.Utc, DateTimeKind.Local)]
    [InlineData(DateTimeKind.Local, DateTimeKind.Local)]
    public void ScheduledAttendanceCalculator_ThrowsArgumentException_WhenPunchTimestampIsLocal(DateTimeKind inKind, DateTimeKind outKind)
    {
        var workDate = new DateOnly(2026, 11, 5);
        var shift = new EffectiveShiftDto("E1", workDate, "WORKING", "BASE", "S1", 1, null, null, null, 0, 480, 8);
        var startUtc = new DateTimeOffset(2026, 11, 5, 8, 0, 0, TimeSpan.Zero);
        var endUtc = new DateTimeOffset(2026, 11, 5, 17, 0, 0, TimeSpan.Zero);
        var timedShift = new TimedShiftDto(shift, "UTC", startUtc, endUtc, 540);
        var policy = new AttendancePolicy { GracePeriodMinutes = 15, EarlyLeaveToleranceMinutes = 15, MinMinutesForOvertime = 30 };

        var checkIn = DateTime.SpecifyKind(new DateTime(2026, 11, 5, 8, 0, 0), inKind);
        var checkOut = DateTime.SpecifyKind(new DateTime(2026, 11, 5, 17, 0, 0), outKind);

        var ex = Assert.Throws<ArgumentException>(() =>
            ScheduledAttendanceCalculator.Calculate("E1", workDate, checkIn, checkOut, timedShift, policy));
        Assert.Contains("Punch timestamps must be UTC", ex.Message);
    }

    [Fact]
    public void ScheduledAttendanceCalculator_ThrowsArgumentException_WhenCheckOutPrecedesCheckIn()
    {
        var workDate = new DateOnly(2026, 11, 5);
        var shift = new EffectiveShiftDto("E1", workDate, "WORKING", "BASE", "S1", 1, null, null, null, 0, 480, 8);
        var startUtc = new DateTimeOffset(2026, 11, 5, 8, 0, 0, TimeSpan.Zero);
        var endUtc = new DateTimeOffset(2026, 11, 5, 17, 0, 0, TimeSpan.Zero);
        var timedShift = new TimedShiftDto(shift, "UTC", startUtc, endUtc, 540);
        var policy = new AttendancePolicy { GracePeriodMinutes = 15, EarlyLeaveToleranceMinutes = 15, MinMinutesForOvertime = 30 };

        var checkIn = new DateTime(2026, 11, 5, 17, 0, 0, DateTimeKind.Utc);
        var checkOut = new DateTime(2026, 11, 5, 8, 0, 0, DateTimeKind.Utc);

        var ex = Assert.Throws<ArgumentException>(() =>
            ScheduledAttendanceCalculator.Calculate("E1", workDate, checkIn, checkOut, timedShift, policy));
        Assert.Contains("Check-out cannot precede check-in", ex.Message);
    }

    [Theory]
    [InlineData("E2", 2026, 11, 5)]
    [InlineData("E1", 2026, 11, 6)]
    public void ScheduledAttendanceCalculator_ThrowsInvalidOperationException_WhenEmployeeOrDateMismatches(string empCode, int year, int month, int day)
    {
        var workDate = new DateOnly(2026, 11, 5);
        var shift = new EffectiveShiftDto("E1", workDate, "WORKING", "BASE", "S1", 1, null, null, null, 0, 480, 8);
        var startUtc = new DateTimeOffset(2026, 11, 5, 8, 0, 0, TimeSpan.Zero);
        var endUtc = new DateTimeOffset(2026, 11, 5, 17, 0, 0, TimeSpan.Zero);
        var timedShift = new TimedShiftDto(shift, "UTC", startUtc, endUtc, 540);
        var policy = new AttendancePolicy();

        var ex = Assert.Throws<InvalidOperationException>(() =>
            ScheduledAttendanceCalculator.Calculate(empCode, new DateOnly(year, month, day), null, null, timedShift, policy));
        Assert.Contains("Attendance and scheduled employee/date must match", ex.Message);
    }

    [Theory]
    [InlineData(-1, 0, 0)]
    [InlineData(0, -1, 0)]
    [InlineData(0, 0, -1)]
    public void ScheduledAttendanceCalculator_ThrowsInvalidOperationException_WhenPolicyTolerancesNegative(int grace, int earlyTolerance, int minOvertime)
    {
        var workDate = new DateOnly(2026, 11, 5);
        var shift = new EffectiveShiftDto("E1", workDate, "WORKING", "BASE", "S1", 1, null, null, null, 0, 480, 8);
        var startUtc = new DateTimeOffset(2026, 11, 5, 8, 0, 0, TimeSpan.Zero);
        var endUtc = new DateTimeOffset(2026, 11, 5, 17, 0, 0, TimeSpan.Zero);
        var timedShift = new TimedShiftDto(shift, "UTC", startUtc, endUtc, 540);
        var policy = new AttendancePolicy { GracePeriodMinutes = grace, EarlyLeaveToleranceMinutes = earlyTolerance, MinMinutesForOvertime = minOvertime };

        var ex = Assert.Throws<InvalidOperationException>(() =>
            ScheduledAttendanceCalculator.Calculate("E1", workDate, null, null, timedShift, policy));
        Assert.Contains("Attendance tolerances cannot be negative", ex.Message);
    }

    [Fact]
    public void ScheduledAttendanceCalculator_ThrowsInvalidOperationException_WhenWorkingShiftLacksUtcBoundaries()
    {
        var workDate = new DateOnly(2026, 11, 5);
        var shift = new EffectiveShiftDto("E1", workDate, "WORKING", "BASE", "S1", 1, null, null, null, 0, 480, 8);
        var timedShift = new TimedShiftDto(shift, "UTC", null, null, null);
        var policy = new AttendancePolicy();

        var ex = Assert.Throws<InvalidOperationException>(() =>
            ScheduledAttendanceCalculator.Calculate("E1", workDate, null, null, timedShift, policy));
        Assert.Contains("Working shift must have UTC boundaries", ex.Message);
    }

    [Fact]
    public void ScheduledAttendanceCalculator_NightShiftCrossingMidnight_CalculatesCorrectHoursAndDeductions()
    {
        var workDate = new DateOnly(2026, 11, 5);
        var shift = new EffectiveShiftDto("E1", workDate, "WORKING", "BASE", "NIGHT", 1, null, null, null, 60, 480, 8);
        var startUtc = new DateTimeOffset(2026, 11, 5, 22, 0, 0, TimeSpan.Zero);
        var endUtc = new DateTimeOffset(2026, 11, 6, 6, 0, 0, TimeSpan.Zero);
        var timedShift = new TimedShiftDto(shift, "UTC", startUtc, endUtc, 480);
        var policy = new AttendancePolicy { GracePeriodMinutes = 15, EarlyLeaveToleranceMinutes = 15, MinMinutesForOvertime = 30 };

        var checkIn = new DateTime(2026, 11, 5, 22, 0, 0, DateTimeKind.Utc);
        var checkOut = new DateTime(2026, 11, 6, 6, 0, 0, DateTimeKind.Utc);

        var result = ScheduledAttendanceCalculator.Calculate("E1", workDate, checkIn, checkOut, timedShift, policy);

        Assert.Equal("PRESENT", result.Status);
        Assert.False(result.HasMissingPunch);
        Assert.Equal(7.00m, result.ScheduledHours); // (480 - 60) / 60
        Assert.Equal(7.00m, result.ActualWorkedHours); // gross 480 - 60 break = 420 / 60
        Assert.Equal(0, result.LateArrivalMinutes);
        Assert.Equal(0, result.EarlyLeaveMinutes);
        Assert.Equal(0m, result.OvertimeHours);
    }

    [Theory]
    [InlineData(0, false, 45)]     // rawLate 45 > 15 grace, autoDeduct false => 45
    [InlineData(0, true, 30)]      // rawLate 45 > 15 grace, autoDeduct true => 30 (45 - 15)
    [InlineData(35, false, 0)]     // late = 45 - 35 = 10 <= 15 grace => 0
    [InlineData(20, false, 25)]    // late = 45 - 20 = 25 > 15 grace, autoDeduct false => 25
    [InlineData(20, true, 10)]     // late = 45 - 20 = 25 > 15 grace, autoDeduct true => 10 (25 - 15)
    public void ScheduledAttendanceCalculator_ExcusedLateMinutes_MitigatesLateArrivalPenalty(int excusedLate, bool autoDeduct, int expectedLateMinutes)
    {
        var workDate = new DateOnly(2026, 11, 5);
        var shift = new EffectiveShiftDto("E1", workDate, "WORKING", "BASE", "S1", 1, null, null, null, 0, 540, 8);
        var startUtc = new DateTimeOffset(2026, 11, 5, 8, 0, 0, TimeSpan.Zero);
        var endUtc = new DateTimeOffset(2026, 11, 5, 17, 0, 0, TimeSpan.Zero);
        var timedShift = new TimedShiftDto(shift, "UTC", startUtc, endUtc, 540);
        var policy = new AttendancePolicy { GracePeriodMinutes = 15, EarlyLeaveToleranceMinutes = 15, AutoDeductLateArrival = autoDeduct };

        var checkIn = new DateTime(2026, 11, 5, 8, 45, 0, DateTimeKind.Utc); // 45 mins late
        var checkOut = new DateTime(2026, 11, 5, 17, 0, 0, DateTimeKind.Utc);

        var result = ScheduledAttendanceCalculator.Calculate("E1", workDate, checkIn, checkOut, timedShift, policy, excusedLateMinutes: excusedLate);

        Assert.Equal(expectedLateMinutes, result.LateArrivalMinutes);
    }

    [Theory]
    [InlineData(0, 45)]   // rawEarly 45 > 15 tolerance => 45
    [InlineData(35, 0)]   // early = 45 - 35 = 10 <= 15 tolerance => 0
    [InlineData(20, 25)]  // early = 45 - 20 = 25 > 15 tolerance => 25
    public void ScheduledAttendanceCalculator_ExcusedEarlyMinutes_MitigatesEarlyLeavePenalty(int excusedEarly, int expectedEarlyMinutes)
    {
        var workDate = new DateOnly(2026, 11, 5);
        var shift = new EffectiveShiftDto("E1", workDate, "WORKING", "BASE", "S1", 1, null, null, null, 0, 540, 8);
        var startUtc = new DateTimeOffset(2026, 11, 5, 8, 0, 0, TimeSpan.Zero);
        var endUtc = new DateTimeOffset(2026, 11, 5, 17, 0, 0, TimeSpan.Zero);
        var timedShift = new TimedShiftDto(shift, "UTC", startUtc, endUtc, 540);
        var policy = new AttendancePolicy { GracePeriodMinutes = 15, EarlyLeaveToleranceMinutes = 15 };

        var checkIn = new DateTime(2026, 11, 5, 8, 0, 0, DateTimeKind.Utc);
        var checkOut = new DateTime(2026, 11, 5, 16, 15, 0, DateTimeKind.Utc); // 45 mins early

        var result = ScheduledAttendanceCalculator.Calculate("E1", workDate, checkIn, checkOut, timedShift, policy, excusedEarlyMinutes: excusedEarly);

        Assert.Equal(expectedEarlyMinutes, result.EarlyLeaveMinutes);
    }

    [Theory]
    [InlineData(17, 25, 0)]      // Extra 25 mins < 30 min threshold => 0 overtime
    [InlineData(17, 30, 0.50)]   // Extra 30 mins == 30 min threshold => 0.50 hours
    [InlineData(18, 30, 1.50)]   // Extra 90 mins >= 30 min threshold => 1.50 hours
    public void ScheduledAttendanceCalculator_OvertimeCalculations_RespectsMinThreshold(int outHour, int outMinute, decimal expectedOvertime)
    {
        var workDate = new DateOnly(2026, 11, 5);
        var shift = new EffectiveShiftDto("E1", workDate, "WORKING", "BASE", "S1", 1, null, null, null, 0, 540, 8);
        var startUtc = new DateTimeOffset(2026, 11, 5, 8, 0, 0, TimeSpan.Zero);
        var endUtc = new DateTimeOffset(2026, 11, 5, 17, 0, 0, TimeSpan.Zero); // 9 hours = 540 mins
        var timedShift = new TimedShiftDto(shift, "UTC", startUtc, endUtc, 540);
        var policy = new AttendancePolicy { MinMinutesForOvertime = 30 };

        var checkIn = new DateTime(2026, 11, 5, 8, 0, 0, DateTimeKind.Utc);
        var checkOut = new DateTime(2026, 11, 5, outHour, outMinute, 0, DateTimeKind.Utc);

        var result = ScheduledAttendanceCalculator.Calculate("E1", workDate, checkIn, checkOut, timedShift, policy);

        Assert.Equal(expectedOvertime, result.OvertimeHours);
    }

    [Fact]
    public void ScheduledAttendanceCalculator_MultiSessionNetMinutes_OverridesSingleSessionNetMinutes()
    {
        var workDate = new DateOnly(2026, 11, 5);
        var shift = new EffectiveShiftDto("E1", workDate, "WORKING", "BASE", "S1", 1, null, null, null, 60, 540, 8);
        var startUtc = new DateTimeOffset(2026, 11, 5, 8, 0, 0, TimeSpan.Zero);
        var endUtc = new DateTimeOffset(2026, 11, 5, 17, 0, 0, TimeSpan.Zero);
        var timedShift = new TimedShiftDto(shift, "UTC", startUtc, endUtc, 540);
        var policy = new AttendancePolicy();

        var checkIn = new DateTime(2026, 11, 5, 8, 0, 0, DateTimeKind.Utc);
        var checkOut = new DateTime(2026, 11, 5, 17, 0, 0, DateTimeKind.Utc);

        var result = ScheduledAttendanceCalculator.Calculate("E1", workDate, checkIn, checkOut, timedShift, policy, multiSessionNetMinutes: 360m);

        Assert.Equal(6.00m, result.ActualWorkedHours); // 360 / 60
    }

    [Fact]
    public void ScheduledAttendanceCalculator_RestDayPunches_RecordedAsEvidenceWithoutOvertime()
    {
        var workDate = new DateOnly(2026, 11, 6);
        var shift = new EffectiveShiftDto("E1", workDate, "REST_DAY", "BASE", "REST", 1, null, null, null, 0, 0, 0);
        var timedShift = new TimedShiftDto(shift, "UTC", null, null, 0);
        var policy = new AttendancePolicy { MinMinutesForOvertime = 30 };

        var checkIn = new DateTime(2026, 11, 6, 9, 0, 0, DateTimeKind.Utc);
        var checkOut = new DateTime(2026, 11, 6, 15, 0, 0, DateTimeKind.Utc);

        var result = ScheduledAttendanceCalculator.Calculate("E1", workDate, checkIn, checkOut, timedShift, policy);

        // A punch on a rest day is evidence of physical presence, but not scheduled work or overtime pay
        Assert.Equal("PRESENT", result.Status);
        Assert.Equal(0m, result.ScheduledHours);
        Assert.Equal(6.00m, result.ActualWorkedHours);
        Assert.Equal(0m, result.OvertimeHours);
        Assert.Equal(0, result.LateArrivalMinutes);
        Assert.Equal(0, result.EarlyLeaveMinutes);
    }

    [Theory]
    [InlineData(true, false)]
    [InlineData(false, true)]
    public void ScheduledAttendanceCalculator_MissingPunch_SetsIncompleteStatus(bool hasCheckIn, bool hasCheckOut)
    {
        var workDate = new DateOnly(2026, 11, 5);
        var shift = new EffectiveShiftDto("E1", workDate, "WORKING", "BASE", "S1", 1, null, null, null, 0, 540, 8);
        var startUtc = new DateTimeOffset(2026, 11, 5, 8, 0, 0, TimeSpan.Zero);
        var endUtc = new DateTimeOffset(2026, 11, 5, 17, 0, 0, TimeSpan.Zero);
        var timedShift = new TimedShiftDto(shift, "UTC", startUtc, endUtc, 540);
        var policy = new AttendancePolicy();

        var checkIn = hasCheckIn ? new DateTime(2026, 11, 5, 8, 0, 0, DateTimeKind.Utc) : (DateTime?)null;
        var checkOut = hasCheckOut ? new DateTime(2026, 11, 5, 17, 0, 0, DateTimeKind.Utc) : (DateTime?)null;

        var result = ScheduledAttendanceCalculator.Calculate("E1", workDate, checkIn, checkOut, timedShift, policy);

        Assert.True(result.HasMissingPunch);
        Assert.Equal("INCOMPLETE", result.Status);
    }

    [Fact]
    public void ScheduledAttendanceCalculator_AbsentDay_SetsZeroActualHoursAndAbsentStatus()
    {
        var workDate = new DateOnly(2026, 11, 5);
        var shift = new EffectiveShiftDto("E1", workDate, "WORKING", "BASE", "S1", 1, null, null, null, 0, 540, 8);
        var startUtc = new DateTimeOffset(2026, 11, 5, 8, 0, 0, TimeSpan.Zero);
        var endUtc = new DateTimeOffset(2026, 11, 5, 17, 0, 0, TimeSpan.Zero);
        var timedShift = new TimedShiftDto(shift, "UTC", startUtc, endUtc, 540);
        var policy = new AttendancePolicy();

        var result = ScheduledAttendanceCalculator.Calculate("E1", workDate, null, null, timedShift, policy);

        Assert.False(result.HasMissingPunch);
        Assert.Equal("ABSENT", result.Status);
        Assert.Equal(0m, result.ActualWorkedHours);
        Assert.Equal(9.00m, result.ScheduledHours);
    }

    [Fact]
    public void ScheduledAttendanceCalculator_CopyMetrics_CopiesAllProperties_WhenSnapshotMatchesOrTargetUninitialized()
    {
        var source = new AttendanceDay
        {
            TimeZoneId = "UTC",
            ShiftCode = "S1",
            ShiftStartsAtUtc = new DateTime(2026, 11, 5, 8, 0, 0, DateTimeKind.Utc),
            ShiftEndsAtUtc = new DateTime(2026, 11, 5, 17, 0, 0, DateTimeKind.Utc),
            ShiftBreakMinutes = 60,
            ShiftSource = "BASE",
            ScheduledHours = 8.00m,
            ActualWorkedHours = 7.50m,
            LateArrivalMinutes = 15,
            EarlyLeaveMinutes = 10,
            OvertimeHours = 1.00m,
            LeaveTypeCode = "ANNUAL",
            HasMissingPunch = false,
            Status = "PRESENT"
        };

        var target = new AttendanceDay { EmployeeCode = "E1", AttendanceDate = new DateTime(2026, 11, 5) };

        ScheduledAttendanceCalculator.CopyMetrics(source, target);

        Assert.Equal("UTC", target.TimeZoneId);
        Assert.Equal("S1", target.ShiftCode);
        Assert.Equal(source.ShiftStartsAtUtc, target.ShiftStartsAtUtc);
        Assert.Equal(source.ShiftEndsAtUtc, target.ShiftEndsAtUtc);
        Assert.Equal(60, target.ShiftBreakMinutes);
        Assert.Equal("BASE", target.ShiftSource);
        Assert.Equal(8.00m, target.ScheduledHours);
        Assert.Equal(7.50m, target.ActualWorkedHours);
        Assert.Equal(15, target.LateArrivalMinutes);
        Assert.Equal(10, target.EarlyLeaveMinutes);
        Assert.Equal(1.00m, target.OvertimeHours);
        Assert.Equal("ANNUAL", target.LeaveTypeCode);
        Assert.False(target.HasMissingPunch);
        Assert.Equal("PRESENT", target.Status);
    }

    [Fact]
    public void ScheduledAttendanceCalculator_CopyMetrics_ThrowsInvalidOperationException_WhenTargetSnapshotDiffers()
    {
        var source = new AttendanceDay
        {
            TimeZoneId = "UTC",
            ShiftCode = "S1",
            ShiftStartsAtUtc = new DateTime(2026, 11, 5, 8, 0, 0, DateTimeKind.Utc),
            ShiftEndsAtUtc = new DateTime(2026, 11, 5, 17, 0, 0, DateTimeKind.Utc),
            ShiftBreakMinutes = 60,
            ShiftSource = "BASE",
            ScheduledHours = 8.00m
        };

        var target = new AttendanceDay
        {
            TimeZoneId = "Jordan Standard Time", // Different TimeZoneId!
            ShiftCode = "S1",
            ShiftStartsAtUtc = source.ShiftStartsAtUtc,
            ShiftEndsAtUtc = source.ShiftEndsAtUtc,
            ShiftBreakMinutes = 60,
            ShiftSource = "BASE",
            ScheduledHours = 8.00m
        };

        var ex = Assert.Throws<InvalidOperationException>(() => ScheduledAttendanceCalculator.CopyMetrics(source, target));
        Assert.Contains("Attendance shift/time snapshot differs from the current roster", ex.Message);
    }

    [Fact]
    public void AttendanceDayStatusRules_Decide_HolidayOnRestDay_ReturnsHoliday()
    {
        var context = new AttendanceDayContext(IsHoliday: true, ApprovedLeaves: Array.Empty<LeaveRequest>(), HasOnlineEvidence: false);
        var result = AttendanceDayStatusRules.Decide("REST_DAY", false, false, null, DateTimeOffset.UtcNow, context);

        Assert.Equal("HOLIDAY", result.Status);
        Assert.False(result.NeedsReview);
        Assert.Null(result.ReviewReason);
    }

    [Fact]
    public void AttendanceDayStatusRules_Decide_ConflictPunchWithHoliday_ReturnsPresent()
    {
        var context = new AttendanceDayContext(IsHoliday: true, ApprovedLeaves: Array.Empty<LeaveRequest>(), HasOnlineEvidence: false);
        var result = AttendanceDayStatusRules.Decide("WORKING", true, true, End, End.AddHours(1), context);

        Assert.Equal("PRESENT", result.Status);
        Assert.False(result.NeedsReview);
        Assert.Null(result.ReviewReason);
    }

    [Theory]
    [InlineData(0.25)]
    [InlineData(0.50)]
    [InlineData(0.75)]
    public void AttendanceDayStatusRules_Decide_VariousPartialLeaveDays_PreservesLeaveTypeAndFlagsReview(decimal days)
    {
        var context = Context(leave: true, days: days);
        var result = AttendanceDayStatusRules.Decide("WORKING", false, false, End, End.AddHours(1), context);

        Assert.Equal("REVIEW_REQUIRED", result.Status);
        Assert.Equal("ANNUAL", result.LeaveTypeCode);
        Assert.True(result.NeedsReview);
        Assert.Contains("Partial-day leave has no timed interval", result.ReviewReason);
    }

    [Fact]
    public void AttendanceDayStatusRules_Decide_PendingEvidence_FlagsReviewRequired()
    {
        var context = new AttendanceDayContext(IsHoliday: false, ApprovedLeaves: Array.Empty<LeaveRequest>(), HasOnlineEvidence: false, HasPendingEvidence: true);
        var result = AttendanceDayStatusRules.Decide("WORKING", true, true, End, End.AddHours(1), context);

        Assert.Equal("REVIEW_REQUIRED", result.Status);
        Assert.True(result.NeedsReview);
        Assert.Contains("Unprocessed evidence must be applied", result.ReviewReason);
    }

    [Fact]
    public void AttendanceDayStatusRules_Decide_WorkingWithoutEndUtc_ThrowsInvalidOperationException()
    {
        var context = new AttendanceDayContext(IsHoliday: false, ApprovedLeaves: Array.Empty<LeaveRequest>(), HasOnlineEvidence: false);
        var ex = Assert.Throws<InvalidOperationException>(() =>
            AttendanceDayStatusRules.Decide("WORKING", false, false, null, End, context));
        Assert.Contains("Working-day classification requires an explicit scheduled end", ex.Message);
    }

    [Fact]
    public void AttendanceDayStatusRules_Decide_UnscheduledWithoutPunch_ReturnsUnscheduled()
    {
        var context = new AttendanceDayContext(IsHoliday: false, ApprovedLeaves: Array.Empty<LeaveRequest>(), HasOnlineEvidence: false);
        var result = AttendanceDayStatusRules.Decide("UNSCHEDULED", false, false, null, End, context);

        Assert.Equal("UNSCHEDULED", result.Status);
        Assert.False(result.NeedsReview);
    }

    [Fact]
    public void AttendanceDayStatusRules_Decide_RestDayWithoutPunch_ReturnsRestDay()
    {
        var context = new AttendanceDayContext(IsHoliday: false, ApprovedLeaves: Array.Empty<LeaveRequest>(), HasOnlineEvidence: false);
        var result = AttendanceDayStatusRules.Decide("REST_DAY", false, false, null, End, context);

        Assert.Equal("REST_DAY", result.Status);
        Assert.False(result.NeedsReview);
    }

    [Fact]
    public void AttendanceDayStatusRules_Decide_FullDayLeave_ReturnsOnLeave()
    {
        var context = Context(leave: true, days: 1.0m);
        var result = AttendanceDayStatusRules.Decide("WORKING", false, false, End, End.AddHours(1), context);

        Assert.Equal("ON_LEAVE", result.Status);
        Assert.Equal("ANNUAL", result.LeaveTypeCode);
        Assert.False(result.NeedsReview);
    }

    [Fact]
    public async Task AttendanceDayStatusService_PreviewAsync_PropagatesCancellationToken()
    {
        using var cts = new CancellationTokenSource();
        var token = cts.Token;
        var workDate = new DateOnly(2026, 11, 5);
        var date = workDate.ToDateTime(TimeOnly.MinValue);

        var timing = new Mock<IShiftTimingService>();
        var classifier = new Mock<IAttendanceDayClassifier>();
        var attendance = new Mock<IAttendanceCorrectionRepository>();
        var settings = new Mock<IAttendanceTimeSettingsRepository>();
        var policies = new Mock<IPolicyRepository>();
        var unitOfWork = new Mock<IHrUnitOfWork>();

        var employeeSetting = new AttendanceTimeSettings { CompanyId = 1, TimeZoneId = "UTC" };
        var shiftDto = new EffectiveShiftDto("E1", workDate, "WORKING", "BASE", "A", 1, null, null, null, 0, 480, 8);
        var timedShift = new TimedShiftDto(shiftDto, "UTC", DateTimeOffset.UtcNow.AddHours(-8), DateTimeOffset.UtcNow, 480);

        settings.Setup(s => s.GetForEmployeeAsync("E1", token)).ReturnsAsync(employeeSetting);
        timing.Setup(t => t.PreviewConfiguredAsync("E1", workDate, token)).ReturnsAsync(timedShift);
        attendance.Setup(a => a.GetAttendanceDayAsync("E1", date, token)).ReturnsAsync((AttendanceDay?)null);
        policies.Setup(p => p.GetActiveAttendancePolicyAsync(1, date, token)).ReturnsAsync(new AttendancePolicy());
        classifier.Setup(c => c.ClassifyAsync(It.IsAny<AttendanceDay>(), timedShift, token))
            .ReturnsAsync(new AttendanceDayClassificationDto(new AttendanceDay { EmployeeCode = "E1" }, false, false, false, false, null));

        var service = new AttendanceDayStatusService(timing.Object, classifier.Object, attendance.Object, settings.Object, policies.Object, unitOfWork.Object);
        var result = await service.PreviewAsync("E1", workDate, token);

        Assert.NotNull(result);
        settings.Verify(s => s.GetForEmployeeAsync("E1", token), Times.Once);
        timing.Verify(t => t.PreviewConfiguredAsync("E1", workDate, token), Times.Once);
        attendance.Verify(a => a.GetAttendanceDayAsync("E1", date, token), Times.Once);
        policies.Verify(p => p.GetActiveAttendancePolicyAsync(1, date, token), Times.Once);
        classifier.Verify(c => c.ClassifyAsync(It.IsAny<AttendanceDay>(), timedShift, token), Times.Once);
    }

    [Fact]
    public async Task AttendanceDayStatusService_PreviewAsync_WhenLegacyAttendanceHasNullTimeZoneId_ThrowsInvalidOperationException()
    {
        var workDate = new DateOnly(2026, 11, 5);
        var date = workDate.ToDateTime(TimeOnly.MinValue);

        var timing = new Mock<IShiftTimingService>();
        var classifier = new Mock<IAttendanceDayClassifier>();
        var attendance = new Mock<IAttendanceCorrectionRepository>();
        var settings = new Mock<IAttendanceTimeSettingsRepository>();
        var policies = new Mock<IPolicyRepository>();
        var unitOfWork = new Mock<IHrUnitOfWork>();

        var employeeSetting = new AttendanceTimeSettings { CompanyId = 1, TimeZoneId = "UTC" };
        var shiftDto = new EffectiveShiftDto("E1", workDate, "WORKING", "BASE", "A", 1, null, null, null, 0, 480, 8);
        var timedShift = new TimedShiftDto(shiftDto, "UTC", DateTimeOffset.UtcNow.AddHours(-8), DateTimeOffset.UtcNow, 480);
        var legacyDay = new AttendanceDay { EmployeeCode = "E1", AttendanceDate = date, TimeZoneId = null };

        settings.Setup(s => s.GetForEmployeeAsync("E1", It.IsAny<CancellationToken>())).ReturnsAsync(employeeSetting);
        timing.Setup(t => t.PreviewConfiguredAsync("E1", workDate, It.IsAny<CancellationToken>())).ReturnsAsync(timedShift);
        attendance.Setup(a => a.GetAttendanceDayAsync("E1", date, It.IsAny<CancellationToken>())).ReturnsAsync(legacyDay);

        var service = new AttendanceDayStatusService(timing.Object, classifier.Object, attendance.Object, settings.Object, policies.Object, unitOfWork.Object);
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => service.PreviewAsync("E1", workDate));
        Assert.Contains("Legacy attendance needs a reviewed time/shift snapshot", ex.Message);
    }

    [Fact]
    public async Task AttendanceDayStatusService_SaveAsync_WhenWorkDateInFuture_ThrowsInvalidOperationException()
    {
        var tomorrow = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(1));
        var timing = new Mock<IShiftTimingService>();
        var classifier = new Mock<IAttendanceDayClassifier>();
        var attendance = new Mock<IAttendanceCorrectionRepository>();
        var settings = new Mock<IAttendanceTimeSettingsRepository>();
        var policies = new Mock<IPolicyRepository>();
        var unitOfWork = new Mock<IHrUnitOfWork>();

        unitOfWork.Setup(u => u.ExecuteForEmployeeDatesAsync(It.IsAny<string>(), It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<Func<Task<AttendanceDayClassificationDto>>>(), It.IsAny<CancellationToken>()))
            .Returns((string c, DateTime s, DateTime e, Func<Task<AttendanceDayClassificationDto>> a, CancellationToken ct) => a());

        settings.Setup(s => s.GetForEmployeeAsync("E1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AttendanceTimeSettings { TimeZoneId = "UTC" });

        var service = new AttendanceDayStatusService(timing.Object, classifier.Object, attendance.Object, settings.Object, policies.Object, unitOfWork.Object);
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => service.SaveAsync("E1", tomorrow, "HR"));
        Assert.Contains("Future attendance dates cannot be finalized", ex.Message);
    }

    [Fact]
    public async Task AttendanceDayStatusService_SaveAsync_WhenStatusPending_ThrowsInvalidOperationException()
    {
        var yesterday = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-1));
        var date = yesterday.ToDateTime(TimeOnly.MinValue);

        var timing = new Mock<IShiftTimingService>();
        var classifier = new Mock<IAttendanceDayClassifier>();
        var attendance = new Mock<IAttendanceCorrectionRepository>();
        var settings = new Mock<IAttendanceTimeSettingsRepository>();
        var policies = new Mock<IPolicyRepository>();
        var unitOfWork = new Mock<IHrUnitOfWork>();

        unitOfWork.Setup(u => u.ExecuteForEmployeeDatesAsync(It.IsAny<string>(), It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<Func<Task<AttendanceDayClassificationDto>>>(), It.IsAny<CancellationToken>()))
            .Returns((string c, DateTime s, DateTime e, Func<Task<AttendanceDayClassificationDto>> a, CancellationToken ct) => a());

        var employeeSetting = new AttendanceTimeSettings { CompanyId = 1, TimeZoneId = "UTC" };
        var shiftDto = new EffectiveShiftDto("E1", yesterday, "WORKING", "BASE", "A", 1, null, null, null, 0, 480, 8);
        var timedShift = new TimedShiftDto(shiftDto, "UTC", DateTimeOffset.UtcNow.AddHours(-8), DateTimeOffset.UtcNow, 480);

        settings.Setup(s => s.GetForEmployeeAsync("E1", It.IsAny<CancellationToken>())).ReturnsAsync(employeeSetting);
        timing.Setup(t => t.PreviewConfiguredAsync("E1", yesterday, It.IsAny<CancellationToken>())).ReturnsAsync(timedShift);
        attendance.Setup(a => a.GetAttendanceDayAsync("E1", date, It.IsAny<CancellationToken>())).ReturnsAsync((AttendanceDay?)null);
        policies.Setup(p => p.GetActiveAttendancePolicyAsync(1, date, It.IsAny<CancellationToken>())).ReturnsAsync(new AttendancePolicy());

        var dayPending = new AttendanceDay { EmployeeCode = "E1", Status = "PENDING", TimeZoneId = "UTC" };
        classifier.Setup(c => c.ClassifyAsync(It.IsAny<AttendanceDay>(), timedShift, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AttendanceDayClassificationDto(dayPending, false, false, false, false, null));

        var service = new AttendanceDayStatusService(timing.Object, classifier.Object, attendance.Object, settings.Object, policies.Object, unitOfWork.Object);
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => service.SaveAsync("E1", yesterday, "HR"));
        Assert.Contains("The scheduled work period has not ended", ex.Message);
    }

    [Fact]
    public async Task AttendanceDayStatusService_SaveAsync_WhenOldAttendanceNull_AddsNewDay()
    {
        var yesterday = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-1));
        var date = yesterday.ToDateTime(TimeOnly.MinValue);

        var timing = new Mock<IShiftTimingService>();
        var classifier = new Mock<IAttendanceDayClassifier>();
        var attendance = new Mock<IAttendanceCorrectionRepository>();
        var settings = new Mock<IAttendanceTimeSettingsRepository>();
        var policies = new Mock<IPolicyRepository>();
        var unitOfWork = new Mock<IHrUnitOfWork>();

        unitOfWork.Setup(u => u.ExecuteForEmployeeDatesAsync(It.IsAny<string>(), It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<Func<Task<AttendanceDayClassificationDto>>>(), It.IsAny<CancellationToken>()))
            .Returns((string c, DateTime s, DateTime e, Func<Task<AttendanceDayClassificationDto>> a, CancellationToken ct) => a());

        var employeeSetting = new AttendanceTimeSettings { CompanyId = 1, TimeZoneId = "UTC" };
        var shiftDto = new EffectiveShiftDto("E1", yesterday, "WORKING", "BASE", "A", 1, null, null, null, 0, 480, 8);
        var timedShift = new TimedShiftDto(shiftDto, "UTC", DateTimeOffset.UtcNow.AddHours(-8), DateTimeOffset.UtcNow, 480);

        settings.Setup(s => s.GetForEmployeeAsync("E1", It.IsAny<CancellationToken>())).ReturnsAsync(employeeSetting);
        timing.Setup(t => t.PreviewConfiguredAsync("E1", yesterday, It.IsAny<CancellationToken>())).ReturnsAsync(timedShift);
        attendance.Setup(a => a.GetAttendanceDayAsync("E1", date, It.IsAny<CancellationToken>())).ReturnsAsync((AttendanceDay?)null);
        policies.Setup(p => p.GetActiveAttendancePolicyAsync(1, date, It.IsAny<CancellationToken>())).ReturnsAsync(new AttendancePolicy());

        var dayPresent = new AttendanceDay { EmployeeCode = "E1", Status = "PRESENT", TimeZoneId = "UTC" };
        classifier.Setup(c => c.ClassifyAsync(It.IsAny<AttendanceDay>(), timedShift, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AttendanceDayClassificationDto(dayPresent, false, false, false, false, null));

        var service = new AttendanceDayStatusService(timing.Object, classifier.Object, attendance.Object, settings.Object, policies.Object, unitOfWork.Object);
        var result = await service.SaveAsync("E1", yesterday, "HR_USER");

        Assert.Equal("PRESENT", result.Day.Status);
        Assert.Equal("HR_USER", result.Day.CreationUser);
        attendance.Verify(a => a.AddAttendanceDayAsync(dayPresent, It.IsAny<CancellationToken>()), Times.Once);
        attendance.Verify(a => a.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task AttendanceDayStatusService_SaveAsync_WhenOldAttendanceExists_CopiesMetricsAndUpdatesDay()
    {
        var yesterday = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-1));
        var date = yesterday.ToDateTime(TimeOnly.MinValue);

        var timing = new Mock<IShiftTimingService>();
        var classifier = new Mock<IAttendanceDayClassifier>();
        var attendance = new Mock<IAttendanceCorrectionRepository>();
        var settings = new Mock<IAttendanceTimeSettingsRepository>();
        var policies = new Mock<IPolicyRepository>();
        var unitOfWork = new Mock<IHrUnitOfWork>();

        unitOfWork.Setup(u => u.ExecuteForEmployeeDatesAsync(It.IsAny<string>(), It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<Func<Task<AttendanceDayClassificationDto>>>(), It.IsAny<CancellationToken>()))
            .Returns((string c, DateTime s, DateTime e, Func<Task<AttendanceDayClassificationDto>> a, CancellationToken ct) => a());

        var employeeSetting = new AttendanceTimeSettings { CompanyId = 1, TimeZoneId = "UTC" };
        var shiftDto = new EffectiveShiftDto("E1", yesterday, "WORKING", "BASE", "A", 1, null, null, null, 0, 480, 8);
        var timedShift = new TimedShiftDto(shiftDto, "UTC", DateTimeOffset.UtcNow.AddHours(-8), DateTimeOffset.UtcNow, 480);
        var existingDay = new AttendanceDay
        {
            Id = 55,
            EmployeeCode = "E1",
            AttendanceDate = date,
            Status = "INCOMPLETE",
            TimeZoneId = "UTC",
            ShiftCode = "A",
            ShiftStartsAtUtc = timedShift.StartUtc!.Value.UtcDateTime,
            ShiftEndsAtUtc = timedShift.EndUtc!.Value.UtcDateTime,
            ShiftBreakMinutes = 0,
            ShiftSource = "BASE",
            ScheduledHours = 8m,
            CreationUser = "ORIGINAL_USER",
            CreationDate = DateTime.UtcNow.AddDays(-1)
        };

        settings.Setup(s => s.GetForEmployeeAsync("E1", It.IsAny<CancellationToken>())).ReturnsAsync(employeeSetting);
        timing.Setup(t => t.PreviewConfiguredAsync("E1", yesterday, It.IsAny<CancellationToken>())).ReturnsAsync(timedShift);
        attendance.Setup(a => a.GetAttendanceDayAsync("E1", date, It.IsAny<CancellationToken>())).ReturnsAsync(existingDay);
        policies.Setup(p => p.GetActiveAttendancePolicyAsync(1, date, It.IsAny<CancellationToken>())).ReturnsAsync(new AttendancePolicy());

        var dayCalculated = new AttendanceDay
        {
            Id = 55,
            EmployeeCode = "E1",
            Status = "PRESENT",
            TimeZoneId = "UTC",
            ShiftCode = "A",
            ShiftStartsAtUtc = timedShift.StartUtc!.Value.UtcDateTime,
            ShiftEndsAtUtc = timedShift.EndUtc!.Value.UtcDateTime,
            ShiftBreakMinutes = 0,
            ShiftSource = "BASE",
            ScheduledHours = 8m,
            ActualWorkedHours = 7.5m,
            LateArrivalMinutes = 12,
            EarlyLeaveMinutes = 4,
            OvertimeHours = 0.5m,
            LeaveTypeCode = "ANNUAL",
            HasMissingPunch = false
        };
        classifier.Setup(c => c.ClassifyAsync(It.IsAny<AttendanceDay>(), timedShift, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AttendanceDayClassificationDto(dayCalculated, false, false, false, false, null));

        var service = new AttendanceDayStatusService(timing.Object, classifier.Object, attendance.Object, settings.Object, policies.Object, unitOfWork.Object);
        var updateStartedAt = DateTime.UtcNow;
        var result = await service.SaveAsync("E1", yesterday, "HR_UPDATER");
        var updateFinishedAt = DateTime.UtcNow;

        Assert.Equal("PRESENT", result.Day.Status);
        Assert.Equal(55, result.Day.Id);
        Assert.Equal("UTC", existingDay.TimeZoneId);
        Assert.Equal("A", existingDay.ShiftCode);
        Assert.Equal(dayCalculated.ShiftStartsAtUtc, existingDay.ShiftStartsAtUtc);
        Assert.Equal(dayCalculated.ShiftEndsAtUtc, existingDay.ShiftEndsAtUtc);
        Assert.Equal(0, existingDay.ShiftBreakMinutes);
        Assert.Equal("BASE", existingDay.ShiftSource);
        Assert.Equal(8m, existingDay.ScheduledHours);
        Assert.Equal(7.5m, existingDay.ActualWorkedHours);
        Assert.Equal(12, existingDay.LateArrivalMinutes);
        Assert.Equal(4, existingDay.EarlyLeaveMinutes);
        Assert.Equal(0.5m, existingDay.OvertimeHours);
        Assert.Equal("ANNUAL", existingDay.LeaveTypeCode);
        Assert.False(existingDay.HasMissingPunch);
        Assert.Equal("PRESENT", existingDay.Status);
        Assert.Equal("HR_UPDATER", existingDay.UpdateUser);
        Assert.InRange(existingDay.UpdateDate!.Value, updateStartedAt, updateFinishedAt);
        attendance.Verify(a => a.UpdateAttendanceDayAsync(existingDay, It.IsAny<CancellationToken>()), Times.Once);
        attendance.Verify(a => a.AddAttendanceDayAsync(It.IsAny<AttendanceDay>(), It.IsAny<CancellationToken>()), Times.Never);
        attendance.Verify(a => a.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task AttendanceDayStatusService_PreviewAsync_WhenCancellationTokenCanceled_ThrowsOperationCanceledException()
    {
        using var cts = new CancellationTokenSource();
        cts.Cancel();
        var yesterday = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-1));

        var settings = new Mock<IAttendanceTimeSettingsRepository>();
        settings.Setup(s => s.GetForEmployeeAsync("E1", cts.Token))
            .ThrowsAsync(new OperationCanceledException(cts.Token));

        var service = new AttendanceDayStatusService(Mock.Of<IShiftTimingService>(), Mock.Of<IAttendanceDayClassifier>(),
            Mock.Of<IAttendanceCorrectionRepository>(), settings.Object, Mock.Of<IPolicyRepository>(), Mock.Of<IHrUnitOfWork>());

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => service.PreviewAsync("E1", yesterday, cts.Token));

        var date = yesterday.ToDateTime(TimeOnly.MinValue);
        var unitOfWork = new Mock<IHrUnitOfWork>();
        unitOfWork.Setup(u => u.ExecuteForEmployeeDatesAsync("E1", date, date,
                It.IsAny<Func<Task<AttendanceDayClassificationDto>>>(), cts.Token))
            .Returns((string _, DateTime _, DateTime _, Func<Task<AttendanceDayClassificationDto>> action, CancellationToken _) => action());
        var saveService = new AttendanceDayStatusService(Mock.Of<IShiftTimingService>(), Mock.Of<IAttendanceDayClassifier>(),
            Mock.Of<IAttendanceCorrectionRepository>(), settings.Object, Mock.Of<IPolicyRepository>(), unitOfWork.Object);

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => saveService.SaveAsync("E1", yesterday, "HR", cts.Token));
        unitOfWork.Verify(u => u.ExecuteForEmployeeDatesAsync("E1", date, date,
            It.IsAny<Func<Task<AttendanceDayClassificationDto>>>(), cts.Token), Times.Once);
    }

    [Fact]
    public async Task AttendanceDayStatusService_PreviewAsync_WhenPolicyNull_DefaultsToNewAttendancePolicy()
    {
        var yesterday = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-1));
        var date = yesterday.ToDateTime(TimeOnly.MinValue);

        var timing = new Mock<IShiftTimingService>();
        var classifier = new Mock<IAttendanceDayClassifier>();
        var attendance = new Mock<IAttendanceCorrectionRepository>();
        var settings = new Mock<IAttendanceTimeSettingsRepository>();
        var policies = new Mock<IPolicyRepository>();

        var employeeSetting = new AttendanceTimeSettings { CompanyId = 1, TimeZoneId = "UTC" };
        var shiftDto = new EffectiveShiftDto("E1", yesterday, "WORKING", "BASE", "A", 1, null, null, null, 0, 480, 8);
        var shiftStart = new DateTimeOffset(yesterday.ToDateTime(new TimeOnly(8, 0)), TimeSpan.Zero);
        var shiftEnd = new DateTimeOffset(yesterday.ToDateTime(new TimeOnly(17, 0)), TimeSpan.Zero);
        var timedShift = new TimedShiftDto(shiftDto, "UTC", shiftStart, shiftEnd, 540);

        settings.Setup(s => s.GetForEmployeeAsync("E1", It.IsAny<CancellationToken>())).ReturnsAsync(employeeSetting);
        timing.Setup(t => t.PreviewConfiguredAsync("E1", yesterday, It.IsAny<CancellationToken>())).ReturnsAsync(timedShift);
        attendance.Setup(a => a.GetAttendanceDayAsync("E1", date, It.IsAny<CancellationToken>())).ReturnsAsync(new AttendanceDay
        {
            EmployeeCode = "E1",
            AttendanceDate = date,
            TimeZoneId = "UTC",
            FirstCheckIn = shiftStart.AddHours(1).UtcDateTime,
            LastCheckOut = shiftEnd.AddMinutes(-5).UtcDateTime
        });
        policies.Setup(p => p.GetActiveAttendancePolicyAsync(1, date, It.IsAny<CancellationToken>())).ReturnsAsync((AttendancePolicy?)null);

        AttendanceDay? calculatedDay = null;
        var dayCalculated = new AttendanceDay { EmployeeCode = "E1", Status = "PRESENT", TimeZoneId = "UTC" };
        classifier.Setup(c => c.ClassifyAsync(It.IsAny<AttendanceDay>(), timedShift, It.IsAny<CancellationToken>()))
            .Callback<AttendanceDay, TimedShiftDto, CancellationToken>((day, _, _) => calculatedDay = day)
            .ReturnsAsync(new AttendanceDayClassificationDto(dayCalculated, false, false, false, false, null));

        var service = new AttendanceDayStatusService(timing.Object, classifier.Object, attendance.Object, settings.Object, policies.Object, Mock.Of<IHrUnitOfWork>());
        var result = await service.PreviewAsync("E1", yesterday);

        Assert.NotNull(result);
        Assert.Equal("PRESENT", result.Day.Status);
        Assert.NotNull(calculatedDay);
        Assert.Equal(45, calculatedDay.LateArrivalMinutes); // Default grace 15 and auto-deduction.
        Assert.Equal(0, calculatedDay.EarlyLeaveMinutes); // Default early-leave tolerance 10.
        Assert.Equal(0m, calculatedDay.OvertimeHours); // Default overtime threshold 30 minutes.
    }

    [Fact]
    public async Task AttendanceDayStatusService_SaveAsync_PropagatesCancellationToken()
    {
        using var cts = new CancellationTokenSource();
        var yesterday = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-1));
        var date = yesterday.ToDateTime(TimeOnly.MinValue);

        var timing = new Mock<IShiftTimingService>();
        var classifier = new Mock<IAttendanceDayClassifier>();
        var attendance = new Mock<IAttendanceCorrectionRepository>();
        var settings = new Mock<IAttendanceTimeSettingsRepository>();
        var policies = new Mock<IPolicyRepository>();
        var unitOfWork = new Mock<IHrUnitOfWork>();

        unitOfWork.Setup(u => u.ExecuteForEmployeeDatesAsync("E1", date, date, It.IsAny<Func<Task<AttendanceDayClassificationDto>>>(), cts.Token))
            .Returns((string c, DateTime s, DateTime e, Func<Task<AttendanceDayClassificationDto>> a, CancellationToken ct) => a());

        var employeeSetting = new AttendanceTimeSettings { CompanyId = 1, TimeZoneId = "UTC" };
        var shiftDto = new EffectiveShiftDto("E1", yesterday, "WORKING", "BASE", "A", 1, null, null, null, 0, 480, 8);
        var timedShift = new TimedShiftDto(shiftDto, "UTC", DateTimeOffset.UtcNow.AddHours(-8), DateTimeOffset.UtcNow, 480);

        settings.Setup(s => s.GetForEmployeeAsync("E1", cts.Token)).ReturnsAsync(employeeSetting);
        timing.Setup(t => t.PreviewConfiguredAsync("E1", yesterday, cts.Token)).ReturnsAsync(timedShift);
        var existingDay = new AttendanceDay
        {
            EmployeeCode = "E1",
            AttendanceDate = date,
            TimeZoneId = "UTC",
            ShiftCode = "A",
            ShiftStartsAtUtc = timedShift.StartUtc!.Value.UtcDateTime,
            ShiftEndsAtUtc = timedShift.EndUtc!.Value.UtcDateTime,
            ShiftBreakMinutes = 0,
            ShiftSource = "BASE",
            ScheduledHours = 8m
        };
        attendance.Setup(a => a.GetAttendanceDayAsync("E1", date, cts.Token)).ReturnsAsync(existingDay);
        policies.Setup(p => p.GetActiveAttendancePolicyAsync(1, date, cts.Token)).ReturnsAsync(new AttendancePolicy());

        var dayPresent = new AttendanceDay
        {
            EmployeeCode = "E1",
            Status = "PRESENT",
            TimeZoneId = "UTC",
            ShiftCode = "A",
            ShiftStartsAtUtc = timedShift.StartUtc!.Value.UtcDateTime,
            ShiftEndsAtUtc = timedShift.EndUtc!.Value.UtcDateTime,
            ShiftBreakMinutes = 0,
            ShiftSource = "BASE",
            ScheduledHours = 8m
        };
        classifier.Setup(c => c.ClassifyAsync(It.IsAny<AttendanceDay>(), timedShift, cts.Token))
            .ReturnsAsync(new AttendanceDayClassificationDto(dayPresent, false, false, false, false, null));

        var service = new AttendanceDayStatusService(timing.Object, classifier.Object, attendance.Object, settings.Object, policies.Object, unitOfWork.Object);
        var result = await service.SaveAsync("E1", yesterday, "HR_USER", cts.Token);

        Assert.Equal("PRESENT", result.Day.Status);
        unitOfWork.Verify(u => u.ExecuteForEmployeeDatesAsync("E1", date, date, It.IsAny<Func<Task<AttendanceDayClassificationDto>>>(), cts.Token), Times.Once);
        attendance.Verify(a => a.EnsureEmployeeWorkDateAsync("E1", date, cts.Token), Times.Once);
        attendance.Verify(a => a.UpdateAttendanceDayAsync(existingDay, cts.Token), Times.Once);
        attendance.Verify(a => a.AddAttendanceDayAsync(It.IsAny<AttendanceDay>(), It.IsAny<CancellationToken>()), Times.Never);
        attendance.Verify(a => a.SaveChangesAsync(cts.Token), Times.Once);
    }
}
