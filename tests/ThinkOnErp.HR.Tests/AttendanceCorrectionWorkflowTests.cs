using Moq;
using ThinkOnErp.Application.DTOs.Hr;
using ThinkOnErp.Application.Services.Hr;
using ThinkOnErp.Domain.Entities.Hr;
using ThinkOnErp.Domain.Interfaces.Hr;
using Xunit;

namespace ThinkOnErp.HR.Tests;

public class AttendanceCorrectionWorkflowTests
{
    private static readonly DateTime Date = new(2026, 3, 10);
    private static AttendanceCorrectionRequest Valid => new() { EmployeeCode = "E", AttendanceDate = Date,
        RequestedCheckIn = DateTime.SpecifyKind(Date.AddHours(9), DateTimeKind.Utc), Reason = "Missing punch" };

    [Theory] [InlineData(0)] [InlineData(1)] [InlineData(2)] [InlineData(3)] [InlineData(4)] [InlineData(5)] [InlineData(6)]
    public async Task InvalidSubmissionNeverReachesRepository(int invalid)
    {
        var row = Valid;
        switch (invalid)
        {
            case 0: row.EmployeeCode = " "; break;
            case 1: row.Reason = " "; break;
            case 2: row.Reason = new string('x', 1001); break;
            case 3: row.AttendanceDate = Date.AddHours(1); break;
            case 4: row.RequestedCheckIn = null; break;
            case 5: row.RequestedCheckIn = Date.AddHours(9); break;
            case 6: row.RequestedCheckOut = row.RequestedCheckIn!.Value.AddMinutes(-1); break;
        }
        var repository = new Mock<IAttendanceCorrectionRepository>(MockBehavior.Strict);
        await Assert.ThrowsAsync<ArgumentException>(() => new AttendanceCorrectionService(repository.Object, Mock.Of<IAttendanceCalculationEngine>())
            .SubmitCorrectionRequestAsync(row.EmployeeCode, row.AttendanceDate, row.RequestedCheckIn, row.RequestedCheckOut, row.Reason, "U"));
        repository.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task FutureEvidenceAndUnreasonedRejectionAreRejectedBeforeAnyRead()
    {
        var repository = new Mock<IAttendanceCorrectionRepository>(MockBehavior.Strict);
        var service = new AttendanceCorrectionService(repository.Object, Mock.Of<IAttendanceCalculationEngine>());
        await Assert.ThrowsAsync<ArgumentException>(() => service.SubmitCorrectionRequestAsync("E", Date, DateTime.UtcNow.AddHours(1), null, "Future", "U"));
        await Assert.ThrowsAsync<ArgumentException>(() => service.ProcessCorrectionRequestAsync(1, false, "HR", " "));
        repository.VerifyNoOtherCalls();
    }

    [Fact]
    public void OriginalVersionIgnoresDatabaseKindAndEquivalentDecimalScale()
    {
        var day = new AttendanceDay { EmployeeCode = "E", AttendanceDate = Date, ScheduledHours = 7.5m, FirstCheckIn = Date.AddHours(9), CheckInLatitude = 31.00m };
        var original = AttendanceCorrectionRules.Version(day);
        day.FirstCheckIn = DateTime.SpecifyKind(day.FirstCheckIn.Value, DateTimeKind.Utc); day.ScheduledHours = 7.500m; day.CheckInLatitude = 31m;
        Assert.Equal(original, AttendanceCorrectionRules.Version(day));
        day.FirstCheckIn = day.FirstCheckIn.Value.AddTicks(1); Assert.NotEqual(original, AttendanceCorrectionRules.Version(day));
    }

    [Fact]
    public void NoOpAndMergedBackwardTimesCannotBecomeRequests()
    {
        var day = new AttendanceDay { EmployeeCode = "E", AttendanceDate = Date, TimeZoneId = "UTC", FirstCheckIn = Valid.RequestedCheckIn };
        Assert.Throws<ArgumentException>(() => AttendanceCorrectionRules.CaptureOriginal(Valid, day));
        var backwards = Valid; backwards.RequestedCheckIn = null; backwards.RequestedCheckOut = day.FirstCheckIn!.Value.AddHours(-1);
        Assert.Throws<ArgumentException>(() => AttendanceCorrectionRules.CaptureOriginal(backwards, day));
    }

    [Theory] [InlineData(false)] [InlineData(true)]
    public async Task ApprovalPreservesOriginalAndAppliedPunchesOrRejectsAStaleDay(bool changed)
    {
        var date = new DateOnly(2026, 3, 10); var schedule = ScheduledAttendanceTests.Schedule(date);
        var day = ScheduledAttendanceCalculator.Calculate("E", date, schedule.StartUtc!.Value.UtcDateTime, null, schedule, new());
        var request = new AttendanceCorrectionRequest { Id = 1, EmployeeCode = "E", AttendanceDate = Date, RequestedCheckOut = schedule.EndUtc!.Value.UtcDateTime.AddMinutes(-20), Reason = "Missing out" };
        AttendanceCorrectionRules.CaptureOriginal(request, day);
        var old = day.FirstCheckIn;
        if (changed) day.FirstCheckIn = day.FirstCheckIn!.Value.AddMinutes(1);
        var repository = new Mock<IAttendanceCorrectionRepository>(); var timing = new Mock<IShiftTimingService>();
        repository.Setup(r => r.GetCorrectionRequestByIdAsync(1, default)).ReturnsAsync(request);
        repository.Setup(r => r.GetAttendanceDayAsync("E", Date, default)).ReturnsAsync(day);
        timing.Setup(t => t.PreviewConfiguredAsync("E", date, default)).ReturnsAsync(schedule);
        var service = new AttendanceCorrectionService(repository.Object, new AttendanceCalculationEngine(repository.Object, timing.Object));
        if (changed)
        {
            await Assert.ThrowsAsync<AttendanceCorrectionStaleException>(() => service.ProcessCorrectionRequestAsync(1, true, "HR"));
            Assert.Equal("PENDING", request.Status); Assert.Null(request.AppliedCheckOut);
            repository.Verify(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
        }
        else
        {
            await service.ProcessCorrectionRequestAsync(1, true, "HR");
            Assert.Equal(old, request.OldCheckIn); Assert.Null(request.OldCheckOut);
            Assert.Equal(old, request.AppliedCheckIn); Assert.Equal(request.RequestedCheckOut, request.AppliedCheckOut);
            Assert.Equal("APPROVED", request.Status); Assert.False(day.HasMissingPunch);
        }
    }

    [Fact]
    public async Task ProcessRequestAsync_WhenDtoNull_ThrowsArgumentNullException()
    {
        var service = new AttendanceCorrectionService(Mock.Of<IAttendanceCorrectionRepository>(), Mock.Of<IAttendanceCalculationEngine>());
        await Assert.ThrowsAsync<ArgumentNullException>(() => service.ProcessRequestAsync(1, null!, "USER"));
    }

    [Fact]
    public async Task ProcessCorrectionRequest_WhenRejectionReasonExceeds1000Chars_ThrowsArgumentException()
    {
        var service = new AttendanceCorrectionService(Mock.Of<IAttendanceCorrectionRepository>(), Mock.Of<IAttendanceCalculationEngine>());
        var longReason = new string('R', 1001);
        var ex = await Assert.ThrowsAsync<ArgumentException>(() => service.ProcessCorrectionRequestAsync(1, false, "HR", longReason));
        Assert.Contains("A rejection reason up to 1000 characters is required", ex.Message);
    }

    [Fact]
    public async Task ProcessCorrectionRequest_WhenRequestNotFound_ThrowsKeyNotFoundException()
    {
        var repository = new Mock<IAttendanceCorrectionRepository>();
        repository.Setup(r => r.GetCorrectionRequestByIdAsync(999, It.IsAny<CancellationToken>()))
            .ReturnsAsync((AttendanceCorrectionRequest?)null);
        var service = new AttendanceCorrectionService(repository.Object, Mock.Of<IAttendanceCalculationEngine>());
        await Assert.ThrowsAsync<KeyNotFoundException>(() => service.ProcessCorrectionRequestAsync(999, true, "HR"));
    }

    [Fact]
    public async Task ProcessCorrectionRequest_WhenAlreadyProcessed_ThrowsInvalidOperationException()
    {
        var repository = new Mock<IAttendanceCorrectionRepository>();
        var request = new AttendanceCorrectionRequest { Id = 5, Status = "APPROVED" };
        repository.Setup(r => r.GetCorrectionRequestByIdAsync(5, It.IsAny<CancellationToken>()))
            .ReturnsAsync(request);
        var service = new AttendanceCorrectionService(repository.Object, Mock.Of<IAttendanceCalculationEngine>());
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => service.ProcessCorrectionRequestAsync(5, true, "HR"));
        Assert.Contains("already been processed with status APPROVED", ex.Message);
    }

    [Fact]
    public async Task ProcessCorrectionRequest_WhenOriginalDayVersionIsNull_ThrowsInvalidOperationException()
    {
        var repository = new Mock<IAttendanceCorrectionRepository>();
        var request = new AttendanceCorrectionRequest
        {
            Id = 6,
            Status = "PENDING",
            EmployeeCode = "E",
            AttendanceDate = Date,
            OriginalDayVersion = null
        };
        repository.Setup(r => r.GetCorrectionRequestByIdAsync(6, It.IsAny<CancellationToken>()))
            .ReturnsAsync(request);
        var service = new AttendanceCorrectionService(repository.Object, Mock.Of<IAttendanceCalculationEngine>());
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => service.ProcessCorrectionRequestAsync(6, true, "HR"));
        Assert.Contains("Legacy pending correction needs reviewed resubmission", ex.Message);
    }

    [Fact]
    public async Task ProcessCorrectionRequest_WhenUnitOfWorkConfigured_ExecutesInsideEmployeeDateLock()
    {
        var repository = new Mock<IAttendanceCorrectionRepository>();
        var unitOfWork = new Mock<IHrUnitOfWork>();
        var request = new AttendanceCorrectionRequest
        {
            Id = 7,
            Status = "PENDING",
            EmployeeCode = "E1",
            AttendanceDate = Date,
            OriginalDayVersion = "v1"
        };
        repository.Setup(r => r.GetCorrectionRequestSnapshotAsync(7, It.IsAny<CancellationToken>()))
            .ReturnsAsync(request);
        repository.Setup(r => r.GetCorrectionRequestByIdAsync(7, It.IsAny<CancellationToken>()))
            .ReturnsAsync(request);

        var locked = false;
        unitOfWork.Setup(u => u.ExecuteForEmployeeDatesAsync("E1", Date.Date, Date.Date, It.IsAny<Func<Task<AttendanceCorrectionRequest>>>(), It.IsAny<CancellationToken>()))
            .Returns(async (string emp, DateTime f, DateTime t, Func<Task<AttendanceCorrectionRequest>> action, CancellationToken ct) =>
            {
                locked = true;
                return await action();
            });

        var engine = new Mock<IAttendanceCalculationEngine>();
        engine.Setup(e => e.ProcessEmployeeDayAttendanceAsync("E1", Date, It.IsAny<DateTime?>(), It.IsAny<DateTime?>(), It.IsAny<AttendancePolicy>(), It.IsAny<decimal>(), It.IsAny<TimeSpan?>(), It.IsAny<TimeSpan?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AttendanceDay { EmployeeCode = "E1", AttendanceDate = Date });

        // When day is null, Version is computed for null
        var nullVersion = AttendanceCorrectionRules.Version(null);
        request.OriginalDayVersion = nullVersion;

        var service = new AttendanceCorrectionService(repository.Object, engine.Object, unitOfWork.Object);
        var result = await service.ProcessCorrectionRequestAsync(7, true, "LOCK_APPROVER");

        Assert.True(locked);
        Assert.Equal("APPROVED", result.Status);
        Assert.Equal("LOCK_APPROVER", result.ApprovedBy);
    }

    [Fact]
    public async Task GetRequestsAsync_ReturnsMappedRequestsForRead()
    {
        var repository = new Mock<IAttendanceCorrectionRepository>();
        var list = new List<AttendanceCorrectionRequest>
        {
            new() { Id = 1, EmployeeCode = "E1", AttendanceDate = Date, Status = "PENDING", Reason = "Missing in" },
            new() { Id = 2, EmployeeCode = "E2", AttendanceDate = Date, Status = "APPROVED", Reason = "Missing out" }
        };
        repository.Setup(r => r.GetCorrectionRequestsAsync(null, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(list);

        var service = new AttendanceCorrectionService(repository.Object, Mock.Of<IAttendanceCalculationEngine>());
        var requests = await service.GetRequestsAsync(null, null);

        Assert.Equal(2, requests.Count);
        Assert.Equal("E1", requests[0].EmployeeCode);
        Assert.Equal("PENDING", requests[0].Status);
        Assert.Equal("E2", requests[1].EmployeeCode);
        Assert.Equal("APPROVED", requests[1].Status);
    }

    [Fact]
    public async Task SubmitRequestAsync_WhenDtoNull_ThrowsArgumentNullException()
    {
        var service = new AttendanceCorrectionService(Mock.Of<IAttendanceCorrectionRepository>(), Mock.Of<IAttendanceCalculationEngine>());
        await Assert.ThrowsAsync<ArgumentNullException>(() => service.SubmitRequestAsync(null!, "USER"));
    }

    [Fact]
    public async Task SubmitRequestAsync_WithValidDto_SubmitsAndReturnsMappedRequest()
    {
        var repo = new Mock<IAttendanceCorrectionRepository>();
        var dto = new AttendanceCorrectionRequestDto(
            "EMP01",
            Date,
            DateTime.SpecifyKind(Date.AddHours(8), DateTimeKind.Utc),
            DateTime.SpecifyKind(Date.AddHours(16), DateTimeKind.Utc),
            "Gate reader was down"
        );

        repo.Setup(r => r.SubmitCorrectionAsync(It.IsAny<AttendanceCorrectionRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((AttendanceCorrectionRequest req, CancellationToken ct) =>
            {
                req.Id = 123;
                return req;
            });

        var service = new AttendanceCorrectionService(repo.Object, Mock.Of<IAttendanceCalculationEngine>());
        var result = await service.SubmitRequestAsync(dto, "TEST_USER");

        Assert.Equal(123, result.Id);
        Assert.Equal("EMP01", result.EmployeeCode);
        Assert.Equal("PENDING", result.Status);
        Assert.Equal("Gate reader was down", result.Reason);
        repo.Verify(r => r.SubmitCorrectionAsync(It.IsAny<AttendanceCorrectionRequest>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ProcessCorrectionRequest_WhenRejecting_DelegatesToRepositoryAndReturnsMapped()
    {
        var repo = new Mock<IAttendanceCorrectionRepository>();
        var rejectedRequest = new AttendanceCorrectionRequest
        {
            Id = 50,
            EmployeeCode = "E_REJ",
            AttendanceDate = Date,
            Status = "REJECTED",
            RejectionReason = "Unverifiable punch claim",
            ApprovedBy = "HR_DIRECTOR"
        };

        repo.Setup(r => r.RejectCorrectionAsync(50, "Unverifiable punch claim", "HR_DIRECTOR", It.IsAny<CancellationToken>()))
            .ReturnsAsync(rejectedRequest);

        var service = new AttendanceCorrectionService(repo.Object, Mock.Of<IAttendanceCalculationEngine>());
        var result = await service.ProcessCorrectionRequestAsync(50, false, "HR_DIRECTOR", "Unverifiable punch claim");

        Assert.Equal(50, result.Id);
        Assert.Equal("REJECTED", result.Status);
        Assert.Equal("Unverifiable punch claim", result.RejectionReason);
        repo.Verify(r => r.RejectCorrectionAsync(50, "Unverifiable punch claim", "HR_DIRECTOR", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public async Task ProcessCorrectionRequest_WhenRejectingWithWhitespaceReason_ThrowsArgumentException(string? invalidReason)
    {
        var service = new AttendanceCorrectionService(Mock.Of<IAttendanceCorrectionRepository>(), Mock.Of<IAttendanceCalculationEngine>());
        await Assert.ThrowsAsync<ArgumentException>(() => service.ProcessCorrectionRequestAsync(1, false, "HR", invalidReason));
    }

    [Fact]
    public async Task ProcessCorrectionRequest_WhenApprovedAndDayTimeZoneIsNull_ThrowsInvalidOperationException()
    {
        var repo = new Mock<IAttendanceCorrectionRepository>();
        var dayWithoutTz = new AttendanceDay
        {
            EmployeeCode = "E_NOTZ",
            AttendanceDate = Date,
            TimeZoneId = null // Legacy day without time zone
        };

        var request = new AttendanceCorrectionRequest
        {
            Id = 60,
            Status = "PENDING",
            EmployeeCode = "E_NOTZ",
            AttendanceDate = Date,
            OriginalDayVersion = AttendanceCorrectionRules.Version(dayWithoutTz),
            RequestedCheckIn = DateTime.SpecifyKind(Date.AddHours(8), DateTimeKind.Utc)
        };

        repo.Setup(r => r.GetCorrectionRequestByIdAsync(60, It.IsAny<CancellationToken>()))
            .ReturnsAsync(request);
        repo.Setup(r => r.GetAttendanceDayAsync("E_NOTZ", Date, It.IsAny<CancellationToken>()))
            .ReturnsAsync(dayWithoutTz);

        var service = new AttendanceCorrectionService(repo.Object, Mock.Of<IAttendanceCalculationEngine>());
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => service.ProcessCorrectionRequestAsync(60, true, "HR"));
        Assert.Contains("Legacy attendance needs a reviewed time/shift snapshot before recalculation", ex.Message);
    }

    [Fact]
    public async Task ProcessCorrectionRequest_WhenApprovedAndDayIsNull_AddsNewAttendanceDay()
    {
        var repo = new Mock<IAttendanceCorrectionRepository>();
        var engine = new Mock<IAttendanceCalculationEngine>();

        var request = new AttendanceCorrectionRequest
        {
            Id = 70,
            Status = "PENDING",
            EmployeeCode = "E_NEWDAY",
            AttendanceDate = Date,
            OriginalDayVersion = AttendanceCorrectionRules.Version(null),
            RequestedCheckIn = DateTime.SpecifyKind(Date.AddHours(8), DateTimeKind.Utc),
            RequestedCheckOut = DateTime.SpecifyKind(Date.AddHours(17), DateTimeKind.Utc)
        };

        repo.Setup(r => r.GetCorrectionRequestByIdAsync(70, It.IsAny<CancellationToken>()))
            .ReturnsAsync(request);
        repo.Setup(r => r.GetAttendanceDayAsync("E_NEWDAY", Date, It.IsAny<CancellationToken>()))
            .ReturnsAsync((AttendanceDay?)null);

        var calculatedDay = new AttendanceDay
        {
            EmployeeCode = "E_NEWDAY",
            AttendanceDate = Date,
            FirstCheckIn = request.RequestedCheckIn,
            LastCheckOut = request.RequestedCheckOut,
            TimeZoneId = "UTC"
        };
        engine.Setup(e => e.ProcessEmployeeDayAttendanceAsync("E_NEWDAY", Date, request.RequestedCheckIn, request.RequestedCheckOut, It.IsAny<AttendancePolicy>(), It.IsAny<decimal>(), It.IsAny<TimeSpan?>(), It.IsAny<TimeSpan?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(calculatedDay);

        var service = new AttendanceCorrectionService(repo.Object, engine.Object);
        var result = await service.ProcessCorrectionRequestAsync(70, true, "APPROVER");

        Assert.Equal("APPROVED", result.Status);
        Assert.Equal("APPROVER", result.ApprovedBy);
        repo.Verify(r => r.AddAttendanceDayAsync(It.IsAny<AttendanceDay>(), It.IsAny<CancellationToken>()), Times.Once);
        repo.Verify(r => r.UpdateCorrectionRequestAsync(request, It.IsAny<CancellationToken>()), Times.Once);
        repo.Verify(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ProcessCorrectionRequest_WhenScopeMismatchesSnapshot_ThrowsAttendanceCorrectionStaleException()
    {
        var repo = new Mock<IAttendanceCorrectionRepository>();
        var uow = new Mock<IHrUnitOfWork>();

        var snapshot = new AttendanceCorrectionRequest
        {
            Id = 80,
            EmployeeCode = "EMP_SNAP",
            AttendanceDate = Date,
            Status = "PENDING"
        };
        var modifiedRequest = new AttendanceCorrectionRequest
        {
            Id = 80,
            EmployeeCode = "EMP_CHANGED", // Employee changed
            AttendanceDate = Date,
            Status = "PENDING"
        };

        repo.Setup(r => r.GetCorrectionRequestSnapshotAsync(80, It.IsAny<CancellationToken>()))
            .ReturnsAsync(snapshot);
        repo.Setup(r => r.GetCorrectionRequestByIdAsync(80, It.IsAny<CancellationToken>()))
            .ReturnsAsync(modifiedRequest);

        uow.Setup(u => u.ExecuteForEmployeeDatesAsync(It.IsAny<string>(), It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<Func<Task<AttendanceCorrectionRequest>>>(), It.IsAny<CancellationToken>()))
            .Returns((string emp, DateTime f, DateTime t, Func<Task<AttendanceCorrectionRequest>> action, CancellationToken ct) => action());

        var service = new AttendanceCorrectionService(repo.Object, Mock.Of<IAttendanceCalculationEngine>(), uow.Object);
        await Assert.ThrowsAsync<AttendanceCorrectionStaleException>(() => service.ProcessCorrectionRequestAsync(80, true, "HR"));
    }

    [Fact]
    public async Task ProcessRequestAsync_WithDto_DelegatesCorrectly()
    {
        var repo = new Mock<IAttendanceCorrectionRepository>();
        var rejected = new AttendanceCorrectionRequest { Id = 90, Status = "REJECTED" };
        repo.Setup(r => r.RejectCorrectionAsync(90, "Duplicate punch request", "MANAGER", It.IsAny<CancellationToken>()))
            .ReturnsAsync(rejected);

        var service = new AttendanceCorrectionService(repo.Object, Mock.Of<IAttendanceCalculationEngine>());
        var dto = new ProcessAttendanceCorrectionDto(false, "Duplicate punch request");

        var result = await service.ProcessRequestAsync(90, dto, "MANAGER");
        Assert.Equal("REJECTED", result.Status);
    }

    [Fact]
    public async Task GetRequestsAsync_WithEmployeeCodeAndStatusFilter_DelegatesToRepository()
    {
        var repo = new Mock<IAttendanceCorrectionRepository>();
        repo.Setup(r => r.GetCorrectionRequestsAsync("EMP_FILTER", "PENDING", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<AttendanceCorrectionRequest>());

        var service = new AttendanceCorrectionService(repo.Object, Mock.Of<IAttendanceCalculationEngine>());
        await service.GetRequestsAsync("EMP_FILTER", "PENDING");

        repo.Verify(r => r.GetCorrectionRequestsAsync("EMP_FILTER", "PENDING", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task SubmitCorrectionRequestAsync_PropagatesCancellationToken()
    {
        using var cts = new CancellationTokenSource();
        var ct = cts.Token;
        var repo = new Mock<IAttendanceCorrectionRepository>();
        repo.Setup(r => r.SubmitCorrectionAsync(It.IsAny<AttendanceCorrectionRequest>(), ct))
            .ReturnsAsync(new AttendanceCorrectionRequest { Id = 101, EmployeeCode = "E", AttendanceDate = Date });

        var service = new AttendanceCorrectionService(repo.Object, Mock.Of<IAttendanceCalculationEngine>());
        var result = await service.SubmitCorrectionRequestAsync("E", Date, DateTime.UtcNow.AddMinutes(-5), null, "Forgot badge", "HR", ct);

        Assert.Equal(101, result.Id);
        repo.Verify(r => r.SubmitCorrectionAsync(It.IsAny<AttendanceCorrectionRequest>(), ct), Times.Once);
    }

    [Fact]
    public async Task SubmitRequestAsync_PropagatesCancellationToken()
    {
        using var cts = new CancellationTokenSource();
        var ct = cts.Token;
        var repo = new Mock<IAttendanceCorrectionRepository>();
        repo.Setup(r => r.SubmitCorrectionAsync(It.IsAny<AttendanceCorrectionRequest>(), ct))
            .ReturnsAsync(new AttendanceCorrectionRequest { Id = 102, EmployeeCode = "E", AttendanceDate = Date });

        var service = new AttendanceCorrectionService(repo.Object, Mock.Of<IAttendanceCalculationEngine>());
        var dto = new AttendanceCorrectionRequestDto("E", Date, DateTime.UtcNow.AddMinutes(-10), null, "Forgot badge");
        var result = await service.SubmitRequestAsync(dto, "HR", ct);

        Assert.Equal(102, result.Id);
        repo.Verify(r => r.SubmitCorrectionAsync(It.IsAny<AttendanceCorrectionRequest>(), ct), Times.Once);
    }

    [Fact]
    public async Task GetRequestsAsync_PropagatesCancellationToken()
    {
        using var cts = new CancellationTokenSource();
        var ct = cts.Token;
        var repo = new Mock<IAttendanceCorrectionRepository>();
        repo.Setup(r => r.GetCorrectionRequestsAsync("E", "PENDING", ct))
            .ReturnsAsync(new List<AttendanceCorrectionRequest>());

        var service = new AttendanceCorrectionService(repo.Object, Mock.Of<IAttendanceCalculationEngine>());
        var list = await service.GetRequestsAsync("E", "PENDING", ct);

        Assert.NotNull(list);
        repo.Verify(r => r.GetCorrectionRequestsAsync("E", "PENDING", ct), Times.Once);
    }

    [Fact]
    public async Task ProcessCorrectionRequestAsync_WhenRejecting_PropagatesCancellationToken()
    {
        using var cts = new CancellationTokenSource();
        var ct = cts.Token;
        var repo = new Mock<IAttendanceCorrectionRepository>();
        repo.Setup(r => r.RejectCorrectionAsync(55, "Invalid evidence", "SUPERVISOR", ct))
            .ReturnsAsync(new AttendanceCorrectionRequest { Id = 55, Status = "REJECTED" });

        var service = new AttendanceCorrectionService(repo.Object, Mock.Of<IAttendanceCalculationEngine>());
        var result = await service.ProcessCorrectionRequestAsync(55, false, "SUPERVISOR", "Invalid evidence", cancellationToken: ct);

        Assert.Equal("REJECTED", result.Status);
        repo.Verify(r => r.RejectCorrectionAsync(55, "Invalid evidence", "SUPERVISOR", ct), Times.Once);
    }

    [Theory]
    [InlineData("APPROVED")]
    [InlineData("REJECTED")]
    [InlineData("CANCELLED")]
    public async Task ProcessCorrectionRequestAsync_WhenNotPending_ThrowsInvalidOperationException(string nonPendingStatus)
    {
        var repo = new Mock<IAttendanceCorrectionRepository>();
        var request = new AttendanceCorrectionRequest
        {
            Id = 60,
            EmployeeCode = "E",
            AttendanceDate = Date,
            Status = nonPendingStatus
        };
        repo.Setup(r => r.GetCorrectionRequestByIdAsync(60, It.IsAny<CancellationToken>()))
            .ReturnsAsync(request);

        var service = new AttendanceCorrectionService(repo.Object, Mock.Of<IAttendanceCalculationEngine>());
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.ProcessCorrectionRequestAsync(60, true, "APPROVER"));

        Assert.Contains("has already been processed", ex.Message);
    }

    [Fact]
    public async Task ProcessCorrectionRequestAsync_WhenOriginalDayVersionNull_ThrowsInvalidOperationException()
    {
        var repo = new Mock<IAttendanceCorrectionRepository>();
        var request = new AttendanceCorrectionRequest
        {
            Id = 61,
            EmployeeCode = "E",
            AttendanceDate = Date,
            Status = "PENDING",
            OriginalDayVersion = null // Legacy request missing version
        };
        repo.Setup(r => r.GetCorrectionRequestByIdAsync(61, It.IsAny<CancellationToken>()))
            .ReturnsAsync(request);

        var service = new AttendanceCorrectionService(repo.Object, Mock.Of<IAttendanceCalculationEngine>());
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.ProcessCorrectionRequestAsync(61, true, "APPROVER"));

        Assert.Equal("Legacy pending correction needs reviewed resubmission before approval.", ex.Message);
    }

    [Fact]
    public async Task ProcessCorrectionRequestAsync_WhenAttendanceDayHasNullTimeZone_ThrowsInvalidOperationException()
    {
        var repo = new Mock<IAttendanceCorrectionRepository>();
        var day = new AttendanceDay
        {
            EmployeeCode = "E",
            AttendanceDate = Date,
            TimeZoneId = null // Legacy missing TimeZoneId
        };
        var request = new AttendanceCorrectionRequest
        {
            Id = 62,
            EmployeeCode = "E",
            AttendanceDate = Date,
            Status = "PENDING",
            OriginalDayVersion = AttendanceCorrectionRules.Version(day)
        };

        repo.Setup(r => r.GetCorrectionRequestByIdAsync(62, It.IsAny<CancellationToken>()))
            .ReturnsAsync(request);
        repo.Setup(r => r.GetAttendanceDayAsync("E", Date, It.IsAny<CancellationToken>()))
            .ReturnsAsync(day);

        var service = new AttendanceCorrectionService(repo.Object, Mock.Of<IAttendanceCalculationEngine>());
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.ProcessCorrectionRequestAsync(62, true, "APPROVER"));

        Assert.Equal("Legacy attendance needs a reviewed time/shift snapshot before recalculation.", ex.Message);
    }

    [Fact]
    public async Task ProcessCorrectionRequestAsync_WhenPolicyNull_ResolvesFromSettingsAndPolicies()
    {
        var repo = new Mock<IAttendanceCorrectionRepository>();
        var engine = new Mock<IAttendanceCalculationEngine>();
        var policies = new Mock<IPolicyRepository>();
        var timeSettings = new Mock<IAttendanceTimeSettingsRepository>();

        var day = new AttendanceDay
        {
            EmployeeCode = "E",
            AttendanceDate = Date,
            TimeZoneId = "UTC"
        };
        var request = new AttendanceCorrectionRequest
        {
            Id = 63,
            EmployeeCode = "E",
            AttendanceDate = Date,
            Status = "PENDING",
            OriginalDayVersion = AttendanceCorrectionRules.Version(day)
        };

        repo.Setup(r => r.GetCorrectionRequestByIdAsync(63, It.IsAny<CancellationToken>()))
            .ReturnsAsync(request);
        repo.Setup(r => r.GetAttendanceDayAsync("E", Date, It.IsAny<CancellationToken>()))
            .ReturnsAsync(day);

        timeSettings.Setup(t => t.GetForEmployeeAsync("E", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AttendanceTimeSettings { CompanyId = 5 });

        var expectedPolicy = new AttendancePolicy { Id = 10, CompanyId = 5 };
        policies.Setup(p => p.GetActiveAttendancePolicyAsync(5, Date, It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedPolicy);

        engine.Setup(e => e.ProcessEmployeeDayAttendanceAsync("E", Date, It.IsAny<DateTime?>(), It.IsAny<DateTime?>(), expectedPolicy, It.IsAny<decimal>(), It.IsAny<TimeSpan?>(), It.IsAny<TimeSpan?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(day);

        var service = new AttendanceCorrectionService(repo.Object, engine.Object, null, policies.Object, timeSettings.Object);
        var result = await service.ProcessCorrectionRequestAsync(63, true, "APPROVER");

        Assert.Equal("APPROVED", result.Status);
        policies.Verify(p => p.GetActiveAttendancePolicyAsync(5, Date, It.IsAny<CancellationToken>()), Times.Once);
    }
}
