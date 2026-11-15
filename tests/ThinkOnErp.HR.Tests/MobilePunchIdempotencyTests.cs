using System.Text.Json;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using ThinkOnErp.Application.DTOs.Hr;
using ThinkOnErp.Application.Services.Hr;
using ThinkOnErp.Domain.Entities.Hr;
using ThinkOnErp.Domain.Interfaces;
using ThinkOnErp.Domain.Interfaces.Hr;
using Xunit;

namespace ThinkOnErp.HR.Tests;

public class MobilePunchIdempotencyTests
{
    private static readonly string EventId = "6b10bacc-267a-475d-a2c7-61f197244c42";
    private static CheckInRequestDto Input => new("E", 31m, 35m, EventId: EventId);
    private static RawAttendance Accepted(CheckInRequestDto dto)
    {
        var key = MobilePunchEventKey.Create(dto.EventId, dto.EmployeeCode, "IN", dto.Latitude, dto.Longitude, dto.BranchId, dto.AccuracyMeters, dto.DeviceId, dto.Source);
        return new() { EmployeeCode = "E", EventId = key.Id, EventPayloadHash = key.PayloadHash,
            EventResponseJson = JsonSerializer.Serialize(new AttendancePunchResultDto(true, "E", "IN", new DateTime(2026, 11, 5, 12, 0, 0, DateTimeKind.Utc),
                1, "Original branch", dto.Latitude, dto.Longitude, 5m, 100m, true, "INSIDE", "Accepted original event")) };
    }
    private static HrAttendanceService Service(IAttendanceCorrectionRepository repository, IEmployeeRepository employee, IHrUnitOfWork? uow = null,
        IShiftTimingService? timing = null, IAttendanceTimeSettingsRepository? settings = null)
        => new(repository, employee, Mock.Of<IBranchRepository>(), Mock.Of<IPolicyRepository>(), Mock.Of<IGeoLocationService>(),
            NullLogger<HrAttendanceService>.Instance, uow, timing, settings);
    private static Mock<IEmployeeRepository> Employee()
    {
        var mock = new Mock<IEmployeeRepository>(); mock.Setup(e => e.GetEmployeeByCodeAsync("E", default)).ReturnsAsync(new Employee { EmployeeCode = "E" }); return mock;
    }

    [Fact]
    public void EquivalentDecimalScalesAndUuidFormatsShareOneKey()
    {
        var first = MobilePunchEventKey.Create(EventId, "E", "IN", 31m, 35.000m, null, 10.00m, null, "MOBILE_APP");
        var second = MobilePunchEventKey.Create(Guid.Parse(EventId).ToString("N"), "E", "IN", 31.000m, 35m, null, 10m, null, " mobile_app ");
        Assert.Equal(first, second);
    }

    [Theory] [InlineData(null)] [InlineData("")] [InlineData("invalid")] [InlineData("00000000-0000-0000-0000-000000000000")]
    public async Task MissingOrInvalidEventIdStopsBeforeAnyRead(string? eventId)
    {
        var repository = new Mock<IAttendanceCorrectionRepository>(MockBehavior.Strict); var employee = new Mock<IEmployeeRepository>(MockBehavior.Strict);
        await Assert.ThrowsAsync<ArgumentException>(() => Service(repository.Object, employee.Object).CheckInAsync(Input with { EventId = eventId }, "U"));
        repository.VerifyNoOtherCalls(); employee.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task AcceptedRetryReturnsOriginalResponseWithoutReadingTimeSettingsOrWriting()
    {
        var repository = new Mock<IAttendanceCorrectionRepository>(); var raw = Accepted(Input); var employee = Employee();
        repository.Setup(r => r.GetMobileEventAsync("E", raw.EventId!, default)).ReturnsAsync(raw);
        var result = await Service(repository.Object, employee.Object).CheckInAsync(Input, "RETRY_USER");
        Assert.Equal("Original branch", result.BranchName); Assert.Equal(new DateTime(2026, 11, 5, 12, 0, 0, DateTimeKind.Utc), result.PunchTime);
        repository.Verify(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
        repository.Verify(r => r.AddRawAttendanceAsync(It.IsAny<RawAttendance>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Theory] [InlineData(0)] [InlineData(1)] [InlineData(2)] [InlineData(3)] [InlineData(4)] [InlineData(5)]
    public async Task ReusedIdCannotChangeAcceptedPayload(int field)
    {
        var repository = new Mock<IAttendanceCorrectionRepository>(); var raw = Accepted(Input);
        repository.Setup(r => r.GetMobileEventAsync("E", raw.EventId!, default)).ReturnsAsync(raw);
        var changed = field switch { 0 => Input with { Latitude = 32m }, 1 => Input with { Longitude = 36m }, 2 => Input with { BranchId = 5 },
            3 => Input with { DeviceId = "different" }, 4 => Input with { AccuracyMeters = 12m }, _ => Input with { Source = "ONLINE" } };
        await Assert.ThrowsAsync<MobilePunchEventConflictException>(() => Service(repository.Object, Employee().Object).CheckInAsync(changed, "U"));
        repository.Verify(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task SameIdCannotChangePunchType()
    {
        var repository = new Mock<IAttendanceCorrectionRepository>(); var raw = Accepted(Input);
        repository.Setup(r => r.GetMobileEventAsync("E", raw.EventId!, default)).ReturnsAsync(raw);
        await Assert.ThrowsAsync<MobilePunchEventConflictException>(() => Service(repository.Object, Employee().Object).CheckOutAsync(new("E", 31m, 35m, EventId: EventId), "U"));
    }

    [Fact]
    public async Task ReplayCannotBypassCurrentEmployeeScope()
    {
        var repository = new Mock<IAttendanceCorrectionRepository>(MockBehavior.Strict);
        await Assert.ThrowsAsync<KeyNotFoundException>(() => Service(repository.Object, Mock.Of<IEmployeeRepository>()).CheckInAsync(Input, "U"));
        repository.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task ConcurrentCaptureReturnsExistingReceiptWithoutAnotherProcessingAttempt()
    {
        var raw = Accepted(Input); raw.Id = 17;
        var repository = new Mock<IAttendanceCorrectionRepository>();
        repository.Setup(r => r.GetMobileEventAsync("E", raw.EventId!, default)).ReturnsAsync((RawAttendance?)null);
        repository.Setup(r => r.AcceptMobileEvidenceAsync(It.IsAny<RawAttendance>(), It.IsAny<Func<long, string>>(), default)).ReturnsAsync(raw);
        var result = await Service(repository.Object, Employee().Object).CheckInAsync(Input, "U");
        Assert.Equal("Accepted original event", result.Message);
        repository.Verify(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Theory] [InlineData(0)] [InlineData(1)] [InlineData(2)] [InlineData(3)]
    public async Task DeviceIntakeRejectsMissingStableIdentityOrUnknownTimeKindBeforePersistence(int invalid)
    {
        var repository = new Mock<IAttendanceCorrectionRepository>(MockBehavior.Strict);
        var engine = new AttendanceCalculationEngine(repository.Object);
        var time = new DateTime(2026, 11, 5, 12, 0, 0, invalid == 3 ? DateTimeKind.Unspecified : DateTimeKind.Utc);
        await Assert.ThrowsAsync<ArgumentException>(() => engine.IngestPunchAsync("E", time, "IN", invalid == 0 ? null : "DEVICE",
            source: invalid == 1 ? " " : "BIOMETRIC", eventId: invalid == 2 ? null : "SOURCE_EVENT_1"));
        repository.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task DeviceKeyUsesSourceIdentityWhilePayloadDetectsTimestampChanges()
    {
        var repository = new Mock<IAttendanceCorrectionRepository>();
        repository.Setup(r => r.AcceptDeviceEventAsync(It.IsAny<RawAttendance>(), default)).ReturnsAsync((RawAttendance raw, CancellationToken ct) => raw);
        var engine = new AttendanceCalculationEngine(repository.Object); var time = new DateTime(2026, 11, 5, 12, 0, 0, DateTimeKind.Utc);
        var first = await engine.IngestPunchAsync("E", time, "IN", "D1", eventId: "123");
        var changed = await engine.IngestPunchAsync("E", time.AddTicks(1), "IN", "D1", eventId: "123");
        var other = await engine.IngestPunchAsync("E", time, "IN", "D2", eventId: "123");
        Assert.Equal(first.EventId, changed.EventId); Assert.NotEqual(first.EventPayloadHash, changed.EventPayloadHash); Assert.NotEqual(first.EventId, other.EventId);
        Assert.False(first.IsProcessed); Assert.Null(first.EventResponseJson);
    }
}
