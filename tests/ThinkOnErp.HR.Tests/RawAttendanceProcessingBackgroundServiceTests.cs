using System;
using System.Collections.Generic;
using System.Diagnostics.Metrics;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using ThinkOnErp.Application.Services.Hr;
using ThinkOnErp.Domain.Entities.Hr;
using ThinkOnErp.Domain.Interfaces.Hr;
using ThinkOnErp.Infrastructure.Data;
using ThinkOnErp.Infrastructure.Services.Hr;
using Xunit;

namespace ThinkOnErp.HR.Tests;

public sealed class RawAttendanceProcessingBackgroundServiceTests
{
    private readonly Mock<IAttendanceCorrectionRepository> _repository = new();
    private readonly Mock<IRawAttendanceProcessingService> _processor = new();
    private readonly Mock<IServiceScopeFactory> _scopeFactory = new();
    private readonly IConfiguration _configuration = new ConfigurationBuilder().Build();
    private readonly RawAttendanceProcessingBackgroundService _service;

    public RawAttendanceProcessingBackgroundServiceTests()
    {
        _service = new RawAttendanceProcessingBackgroundService(
            _scopeFactory.Object,
            _configuration,
            NullLogger<RawAttendanceProcessingBackgroundService>.Instance);
    }

    [Fact]
    public async Task ProcessTenantEventsAsync_WhenNoEventsPending_DoesNotCallProcessor()
    {
        _repository.Setup(r => r.GetAutomaticallyProcessableRawPunchesAsync(It.IsAny<int>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<RawAttendance>());

        await _service.ProcessTenantEventsAsync(
            companyId: 1,
            _repository.Object,
            _processor.Object,
            db: null,
            batchSize: 50,
            retryDelayMinutes: 5,
            CancellationToken.None);

        _processor.Verify(p => p.ProcessAsync(It.IsAny<long>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ProcessTenantEventsAsync_WhenEventApplied_CallsProcessorAndProcessesSuccessfully()
    {
        var punch = new RawAttendance { Id = 101, EmployeeCode = "EMP-101" };
        _repository.Setup(r => r.GetAutomaticallyProcessableRawPunchesAsync(It.IsAny<int>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { punch });

        var successResult = new RawProcessingResult(101, Applied: true, AlreadyTerminal: false, WorkDate: new DateOnly(2026, 11, 8),
            AttendanceDayId: 501, Outcome: "APPLIED", NeedsReview: false, Message: "Success");

        _processor.Setup(p => p.ProcessAsync(101, "SYSTEM:HR_ATTENDANCE_RAW", It.IsAny<CancellationToken>()))
            .ReturnsAsync(successResult);

        await _service.ProcessTenantEventsAsync(
            companyId: 1,
            _repository.Object,
            _processor.Object,
            db: null,
            batchSize: 50,
            retryDelayMinutes: 5,
            CancellationToken.None);

        _processor.Verify(p => p.ProcessAsync(101, "SYSTEM:HR_ATTENDANCE_RAW", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ProcessTenantEventsAsync_WhenEventNeedsReview_HandlesGracefullyWithoutThrowing()
    {
        var punch = new RawAttendance { Id = 102, EmployeeCode = "EMP-102" };
        _repository.Setup(r => r.GetAutomaticallyProcessableRawPunchesAsync(It.IsAny<int>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { punch });

        var reviewResult = new RawProcessingResult(102, Applied: false, AlreadyTerminal: false, WorkDate: new DateOnly(2026, 11, 8),
            AttendanceDayId: null, Outcome: "REVIEW_REQUIRED", NeedsReview: true, Message: "Multiple shifts conflict");

        _processor.Setup(p => p.ProcessAsync(102, "SYSTEM:HR_ATTENDANCE_RAW", It.IsAny<CancellationToken>()))
            .ReturnsAsync(reviewResult);

        await _service.ProcessTenantEventsAsync(
            companyId: 2,
            _repository.Object,
            _processor.Object,
            db: null,
            batchSize: 50,
            retryDelayMinutes: 5,
            CancellationToken.None);

        _processor.Verify(p => p.ProcessAsync(102, "SYSTEM:HR_ATTENDANCE_RAW", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Theory]
    [InlineData("Invalid operation during punch")]
    [InlineData("Argument validation error")]
    public async Task ProcessTenantEventsAsync_WhenReviewExceptionThrown_CatchesAndTreatsAsReview(string error)
    {
        var punch = new RawAttendance { Id = 103, EmployeeCode = "EMP-103" };
        _repository.Setup(r => r.GetAutomaticallyProcessableRawPunchesAsync(It.IsAny<int>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { punch });

        _processor.Setup(p => p.ProcessAsync(103, "SYSTEM:HR_ATTENDANCE_RAW", It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException(error));

        await _service.ProcessTenantEventsAsync(
            companyId: 1,
            _repository.Object,
            _processor.Object,
            db: null,
            batchSize: 50,
            retryDelayMinutes: 5,
            CancellationToken.None);

        _processor.Verify(p => p.ProcessAsync(103, "SYSTEM:HR_ATTENDANCE_RAW", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ProcessTenantEventsAsync_WhenGeneralExceptionThrown_CatchesAsTransientFailure()
    {
        var punch = new RawAttendance { Id = 104, EmployeeCode = "EMP-104" };
        _repository.Setup(r => r.GetAutomaticallyProcessableRawPunchesAsync(It.IsAny<int>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { punch });

        _processor.Setup(p => p.ProcessAsync(104, "SYSTEM:HR_ATTENDANCE_RAW", It.IsAny<CancellationToken>()))
            .ThrowsAsync(new TimeoutException("Database connection timeout"));

        await _service.ProcessTenantEventsAsync(
            companyId: 1,
            _repository.Object,
            _processor.Object,
            db: null,
            batchSize: 50,
            retryDelayMinutes: 5,
            CancellationToken.None);

        _processor.Verify(p => p.ProcessAsync(104, "SYSTEM:HR_ATTENDANCE_RAW", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ProcessTenantEventsAsync_WhenMultipleEventsPending_ProcessesAllIteratively()
    {
        var punches = new[]
        {
            new RawAttendance { Id = 201, EmployeeCode = "EMP-A" },
            new RawAttendance { Id = 202, EmployeeCode = "EMP-B" },
            new RawAttendance { Id = 203, EmployeeCode = "EMP-C" }
        };

        _repository.Setup(r => r.GetAutomaticallyProcessableRawPunchesAsync(It.IsAny<int>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(punches);

        var result = new RawProcessingResult(0, Applied: true, AlreadyTerminal: false, WorkDate: new DateOnly(2026, 11, 8),
            AttendanceDayId: 1, Outcome: "APPLIED", NeedsReview: false, Message: "Done");

        _processor.Setup(p => p.ProcessAsync(It.IsAny<long>(), "SYSTEM:HR_ATTENDANCE_RAW", It.IsAny<CancellationToken>()))
            .ReturnsAsync(result);

        await _service.ProcessTenantEventsAsync(
            companyId: 5,
            _repository.Object,
            _processor.Object,
            db: null,
            batchSize: 50,
            retryDelayMinutes: 5,
            CancellationToken.None);

        _processor.Verify(p => p.ProcessAsync(201, "SYSTEM:HR_ATTENDANCE_RAW", It.IsAny<CancellationToken>()), Times.Once);
        _processor.Verify(p => p.ProcessAsync(202, "SYSTEM:HR_ATTENDANCE_RAW", It.IsAny<CancellationToken>()), Times.Once);
        _processor.Verify(p => p.ProcessAsync(203, "SYSTEM:HR_ATTENDANCE_RAW", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ProcessTenantEventsAsync_WhenAlreadyTerminal_HandlesCleanlyWithoutReviewOrTransientError()
    {
        var punch = new RawAttendance { Id = 301, EmployeeCode = "EMP-301" };
        _repository.Setup(r => r.GetAutomaticallyProcessableRawPunchesAsync(It.IsAny<int>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { punch });

        var terminalResult = new RawProcessingResult(301, Applied: false, AlreadyTerminal: true, WorkDate: new DateOnly(2026, 11, 8),
            AttendanceDayId: null, Outcome: "ALREADY_TERMINAL", NeedsReview: false, Message: "Event was previously completed");

        _processor.Setup(p => p.ProcessAsync(301, "SYSTEM:HR_ATTENDANCE_RAW", It.IsAny<CancellationToken>()))
            .ReturnsAsync(terminalResult);

        await _service.ProcessTenantEventsAsync(
            companyId: 1,
            _repository.Object,
            _processor.Object,
            db: null,
            batchSize: 50,
            retryDelayMinutes: 5,
            CancellationToken.None);

        _processor.Verify(p => p.ProcessAsync(301, "SYSTEM:HR_ATTENDANCE_RAW", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ProcessTenantEventsAsync_WhenArgumentExceptionThrown_CatchesAndTreatsAsReview()
    {
        var punch = new RawAttendance { Id = 302, EmployeeCode = "EMP-302" };
        _repository.Setup(r => r.GetAutomaticallyProcessableRawPunchesAsync(It.IsAny<int>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { punch });

        _processor.Setup(p => p.ProcessAsync(302, "SYSTEM:HR_ATTENDANCE_RAW", It.IsAny<CancellationToken>()))
            .ThrowsAsync(new ArgumentException("Invalid punch type"));

        await _service.ProcessTenantEventsAsync(
            companyId: 1,
            _repository.Object,
            _processor.Object,
            db: null,
            batchSize: 50,
            retryDelayMinutes: 5,
            CancellationToken.None);

        _processor.Verify(p => p.ProcessAsync(302, "SYSTEM:HR_ATTENDANCE_RAW", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ProcessTenantEventsAsync_WhenCancellationTokenCancelled_ThrowsOperationCanceledException()
    {
        var punch = new RawAttendance { Id = 303, EmployeeCode = "EMP-303" };
        _repository.Setup(r => r.GetAutomaticallyProcessableRawPunchesAsync(It.IsAny<int>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { punch });

        using var cts = new CancellationTokenSource();
        cts.Cancel();

        _processor.Setup(p => p.ProcessAsync(303, "SYSTEM:HR_ATTENDANCE_RAW", It.IsAny<CancellationToken>()))
            .ThrowsAsync(new OperationCanceledException(cts.Token));

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            _service.ProcessTenantEventsAsync(
                companyId: 1,
                _repository.Object,
                _processor.Object,
                db: null,
                batchSize: 50,
                retryDelayMinutes: 5,
                cts.Token));
    }

    [Fact]
    public async Task ProcessTenantEventsAsync_WhenTransientExceptionThrownAndDbProvided_ClearsChangeTracker()
    {
        var options = new DbContextOptionsBuilder<OracleDbContext>()
            .UseInMemoryDatabase(databaseName: "RawAttendance_ClearTracker_Db")
            .Options;
        using var db = new OracleDbContext(options);

        var punch = new RawAttendance { Id = 304, EmployeeCode = "EMP-304" };
        _repository.Setup(r => r.GetAutomaticallyProcessableRawPunchesAsync(It.IsAny<int>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { punch });

        _processor.Setup(p => p.ProcessAsync(304, "SYSTEM:HR_ATTENDANCE_RAW", It.IsAny<CancellationToken>()))
            .ThrowsAsync(new TimeoutException("Temporary database lock"));

        await _service.ProcessTenantEventsAsync(
            companyId: 1,
            _repository.Object,
            _processor.Object,
            db: db,
            batchSize: 50,
            retryDelayMinutes: 5,
            CancellationToken.None);

        _processor.Verify(p => p.ProcessAsync(304, "SYSTEM:HR_ATTENDANCE_RAW", It.IsAny<CancellationToken>()), Times.Once);
        Assert.False(db.ChangeTracker.HasChanges());
    }

    [Fact]
    public async Task ProcessTenantEventsAsync_QueriesRepositoryWithSpecifiedBatchSizeAndRetryCutoff()
    {
        DateTime capturedBefore = DateTime.MinValue;
        int capturedBatch = 0;

        _repository.Setup(r => r.GetAutomaticallyProcessableRawPunchesAsync(It.IsAny<int>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .Callback<int, DateTime, CancellationToken>((batch, cutoff, _) =>
            {
                capturedBatch = batch;
                capturedBefore = cutoff;
            })
            .ReturnsAsync(Array.Empty<RawAttendance>());

        var beforeCall = DateTime.UtcNow.AddMinutes(-10);

        await _service.ProcessTenantEventsAsync(
            companyId: 1,
            _repository.Object,
            _processor.Object,
            db: null,
            batchSize: 75,
            retryDelayMinutes: 10,
            CancellationToken.None);

        Assert.Equal(75, capturedBatch);
        Assert.True(capturedBefore >= beforeCall.AddSeconds(-2) && capturedBefore <= DateTime.UtcNow.AddMinutes(-10).AddSeconds(2));
    }

    [Fact]
    public async Task ProcessTenantEventsAsync_PropagatesCancellationToken()
    {
        using var cts = new CancellationTokenSource();
        cts.Cancel();
        _repository.Setup(r => r.GetAutomaticallyProcessableRawPunchesAsync(It.IsAny<int>(), It.IsAny<DateTime>(), cts.Token))
            .ThrowsAsync(new OperationCanceledException(cts.Token));

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            _service.ProcessTenantEventsAsync(
                companyId: 1,
                _repository.Object,
                _processor.Object,
                db: null,
                batchSize: 50,
                retryDelayMinutes: 5,
                cts.Token));
    }

    [Fact]
    public async Task ProcessTenantEventsAsync_WhenCancellationTokenCancelledBetweenEvents_ThrowsOperationCanceledException()
    {
        var punch1 = new RawAttendance { Id = 501, EmployeeCode = "EMP-1" };
        var punch2 = new RawAttendance { Id = 502, EmployeeCode = "EMP-2" };
        using var cts = new CancellationTokenSource();

        _repository.Setup(r => r.GetAutomaticallyProcessableRawPunchesAsync(It.IsAny<int>(), It.IsAny<DateTime>(), cts.Token))
            .ReturnsAsync(new[] { punch1, punch2 });

        _processor.Setup(p => p.ProcessAsync(501, "SYSTEM:HR_ATTENDANCE_RAW", cts.Token))
            .ReturnsAsync(new RawProcessingResult(501, true, false, new DateOnly(2026, 11, 8), 1, "APPLIED", false, null))
            .Callback(() => cts.Cancel());

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            _service.ProcessTenantEventsAsync(
                companyId: 1,
                _repository.Object,
                _processor.Object,
                db: null,
                batchSize: 50,
                retryDelayMinutes: 5,
                cts.Token));

        _processor.Verify(p => p.ProcessAsync(501, "SYSTEM:HR_ATTENDANCE_RAW", It.IsAny<CancellationToken>()), Times.Once);
        _processor.Verify(p => p.ProcessAsync(502, "SYSTEM:HR_ATTENDANCE_RAW", It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ProcessTenantEventsAsync_WhenDbProvidedAndSuccessful_ChangeTrackerNotCleared()
    {
        var options = new DbContextOptionsBuilder<OracleDbContext>()
            .UseInMemoryDatabase(databaseName: "RawAttendance_Success_Db")
            .Options;
        using var db = new OracleDbContext(options);
        db.Add(new AttendancePolicy { CompanyId = 99 });

        var punch = new RawAttendance { Id = 601, EmployeeCode = "EMP-601" };
        _repository.Setup(r => r.GetAutomaticallyProcessableRawPunchesAsync(It.IsAny<int>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { punch });

        var successResult = new RawProcessingResult(601, true, false, new DateOnly(2026, 11, 8), 1, "APPLIED", false, null);
        _processor.Setup(p => p.ProcessAsync(601, "SYSTEM:HR_ATTENDANCE_RAW", It.IsAny<CancellationToken>()))
            .ReturnsAsync(successResult);

        await _service.ProcessTenantEventsAsync(
            companyId: 1,
            _repository.Object,
            _processor.Object,
            db: db,
            batchSize: 50,
            retryDelayMinutes: 5,
            CancellationToken.None);

        Assert.True(db.ChangeTracker.HasChanges());
    }

    [Fact]
    public async Task ProcessTenantEventsAsync_EmitsLowCardinalityOutcomeMetrics()
    {
        var punches = new[]
        {
            new RawAttendance { Id = 701, EmployeeCode = "EMP-METRIC-A" },
            new RawAttendance { Id = 702, EmployeeCode = "EMP-METRIC-B" },
            new RawAttendance { Id = 703, EmployeeCode = "EMP-METRIC-C" }
        };
        _repository.Setup(r => r.GetAutomaticallyProcessableRawPunchesAsync(It.IsAny<int>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(punches);
        _processor.Setup(p => p.ProcessAsync(701, "SYSTEM:HR_ATTENDANCE_RAW", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new RawProcessingResult(701, true, false, new DateOnly(2026, 11, 8), 601, "APPLIED", false, "Success"));
        _processor.Setup(p => p.ProcessAsync(702, "SYSTEM:HR_ATTENDANCE_RAW", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new RawProcessingResult(702, false, false, new DateOnly(2026, 11, 8), null, "REVIEW_REQUIRED", true, "Needs review"));
        _processor.Setup(p => p.ProcessAsync(703, "SYSTEM:HR_ATTENDANCE_RAW", It.IsAny<CancellationToken>()))
            .ThrowsAsync(new TimeoutException("Temporary processing failure"));

        var measurements = new List<(string Instrument, long Value, Dictionary<string, object?> Tags)>();
        using var listener = new MeterListener();
        listener.InstrumentPublished = (instrument, meterListener) =>
        {
            if (instrument.Meter.Name == "ThinkOnErp.HR") meterListener.EnableMeasurementEvents(instrument);
        };
        listener.SetMeasurementEventCallback<long>((instrument, value, tags, _) =>
        {
            var copiedTags = new Dictionary<string, object?>(StringComparer.Ordinal);
            foreach (var tag in tags) copiedTags[tag.Key] = tag.Value;
            measurements.Add((instrument.Name, value, copiedTags));
        });
        listener.Start();

        await _service.ProcessTenantEventsAsync(77, _repository.Object, _processor.Object, null, 50, 5, CancellationToken.None);

        Assert.Contains(measurements, x => x.Instrument == "thinkonerp.hr.attendance.raw.batches" && x.Value == 1 &&
            Equals(x.Tags.GetValueOrDefault("outcome"), "selected"));
        Assert.Contains(measurements, x => x.Instrument == "thinkonerp.hr.attendance.raw.events" && x.Value == 3 &&
            Equals(x.Tags.GetValueOrDefault("outcome"), "selected"));
        Assert.Contains(measurements, x => x.Instrument == "thinkonerp.hr.attendance.raw.events" && x.Value == 1 &&
            Equals(x.Tags.GetValueOrDefault("outcome"), "applied"));
        Assert.Contains(measurements, x => x.Instrument == "thinkonerp.hr.attendance.raw.events" && x.Value == 1 &&
            Equals(x.Tags.GetValueOrDefault("outcome"), "review"));
        Assert.Contains(measurements, x => x.Instrument == "thinkonerp.hr.attendance.raw.events" && x.Value == 1 &&
            Equals(x.Tags.GetValueOrDefault("outcome"), "transient_failure"));
        Assert.All(measurements, measurement =>
        {
            Assert.All(measurement.Tags.Keys, key => Assert.Equal("outcome", key));
            Assert.DoesNotContain("EMP-METRIC", string.Join(";", measurement.Tags.Values));
        });
    }
}
