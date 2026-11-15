using Microsoft.EntityFrameworkCore;
using ThinkOnErp.Domain.Entities.Hr;
using ThinkOnErp.Infrastructure.Data;
using ThinkOnErp.Infrastructure.Repositories.Hr;
using Xunit;

namespace ThinkOnErp.HR.Api.Tests;

public sealed class RawAttendanceAutomaticQueueTests
{
    [Fact]
    public async Task AutomaticQueueIncludesNewAndRetryDueEventsButSkipsReviewAndRecentEvents()
    {
        var options = new DbContextOptionsBuilder<OracleDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        await using var db = new OracleDbContext(options);
        var cutoff = DateTime.UtcNow.AddMinutes(-5);
        db.RawAttendances.AddRange(
            Raw(1),
            Raw(2, attemptedAt: cutoff.AddSeconds(-1)),
            Raw(3, attemptedAt: cutoff.AddSeconds(1)),
            Raw(4, attemptedAt: cutoff.AddHours(-1), reason: "Needs reviewed shift"),
            Raw(5, processed: true));
        await db.SaveChangesAsync();

        var pending = await new AttendanceCorrectionRepository(db)
            .GetAutomaticallyProcessableRawPunchesAsync(50, cutoff);

        Assert.Equal(new long[] { 1, 2 }, pending.Select(x => x.Id));
    }

    [Fact]
    public async Task AutomaticQueue_OrdersUnattemptedBeforeRetries_AndOldestAttemptFirst()
    {
        var options = new DbContextOptionsBuilder<OracleDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        await using var db = new OracleDbContext(options);
        var cutoff = DateTime.UtcNow.AddMinutes(-5);

        // Unattempted: Id 10 (PunchTime at 08:00), Id 11 (PunchTime at 07:00)
        // Retry: Id 20 (attempted 10 mins ago), Id 21 (attempted 20 mins ago)
        var p10 = Raw(10);
        p10.PunchTime = new DateTime(2026, 11, 7, 8, 0, 0, DateTimeKind.Utc);
        var p11 = Raw(11);
        p11.PunchTime = new DateTime(2026, 11, 7, 7, 0, 0, DateTimeKind.Utc);

        var p20 = Raw(20, attemptedAt: cutoff.AddMinutes(-5)); // 10 mins ago
        var p21 = Raw(21, attemptedAt: cutoff.AddMinutes(-15)); // 20 mins ago

        db.RawAttendances.AddRange(p10, p11, p20, p21);
        await db.SaveChangesAsync();

        var pending = await new AttendanceCorrectionRepository(db)
            .GetAutomaticallyProcessableRawPunchesAsync(50, cutoff);

        // Expected order:
        // 1. Unattempted with earlier PunchTime: 11
        // 2. Unattempted with later PunchTime: 10
        // 3. Retry with older LastAttemptedAtUtc: 21
        // 4. Retry with newer LastAttemptedAtUtc: 20
        Assert.Equal(new long[] { 11, 10, 21, 20 }, pending.Select(x => x.Id));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(101)]
    [InlineData(500)]
    public async Task AutomaticQueue_InvalidBatchSize_ThrowsArgumentOutOfRangeException(int batchSize)
    {
        var options = new DbContextOptionsBuilder<OracleDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        await using var db = new OracleDbContext(options);
        var repository = new AttendanceCorrectionRepository(db);

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() =>
            repository.GetAutomaticallyProcessableRawPunchesAsync(batchSize, DateTime.UtcNow));
    }

    [Theory]
    [InlineData(DateTimeKind.Local)]
    [InlineData(DateTimeKind.Unspecified)]
    public async Task AutomaticQueue_NonUtcCutoff_ThrowsArgumentException(DateTimeKind kind)
    {
        var options = new DbContextOptionsBuilder<OracleDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        await using var db = new OracleDbContext(options);
        var repository = new AttendanceCorrectionRepository(db);

        var nonUtcDate = DateTime.SpecifyKind(DateTime.Now, kind);

        await Assert.ThrowsAsync<ArgumentException>(() =>
            repository.GetAutomaticallyProcessableRawPunchesAsync(10, nonUtcDate));
    }

    [Fact]
    public async Task AutomaticQueue_BatchSizeLimitsReturnedCount()
    {
        var options = new DbContextOptionsBuilder<OracleDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        await using var db = new OracleDbContext(options);
        var cutoff = DateTime.UtcNow;

        db.RawAttendances.AddRange(Raw(1), Raw(2), Raw(3), Raw(4), Raw(5));
        await db.SaveChangesAsync();

        var repository = new AttendanceCorrectionRepository(db);
        var results = await repository.GetAutomaticallyProcessableRawPunchesAsync(2, cutoff);

        Assert.Equal(2, results.Count);
    }

    [Fact]
    public async Task AutomaticQueue_EmptyDatabase_ReturnsEmptyList()
    {
        var options = new DbContextOptionsBuilder<OracleDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        await using var db = new OracleDbContext(options);
        var repository = new AttendanceCorrectionRepository(db);

        var results = await repository.GetAutomaticallyProcessableRawPunchesAsync(10, DateTime.UtcNow);

        Assert.Empty(results);
    }

    [Fact]
    public async Task AutomaticQueueRequiresUtcCutoffAndBoundedBatchSize()
    {
        var options = new DbContextOptionsBuilder<OracleDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        await using var db = new OracleDbContext(options);
        var repository = new AttendanceCorrectionRepository(db);

        await Assert.ThrowsAsync<ArgumentException>(() => repository.GetAutomaticallyProcessableRawPunchesAsync(
            10, DateTime.SpecifyKind(DateTime.Now, DateTimeKind.Local)));
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => repository.GetAutomaticallyProcessableRawPunchesAsync(
            101, DateTime.UtcNow));
    }

    [Fact]
    public void AutomaticQueueQueryTranslatesToOracleSqlWithoutConnecting()
    {
        var options = new DbContextOptionsBuilder<OracleDbContext>()
            .UseOracle("User Id=test;Password=test;Data Source=localhost:1521/XEPDB1")
            .Options;
        using var db = new OracleDbContext(options);

        var sql = AttendanceCorrectionRepository.BuildAutomaticProcessingQuery(db, DateTime.UtcNow)
            .Take(50)
            .ToQueryString();

        Assert.Contains("HR_RAW_ATTENDANCE", sql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("PROCESSING_REASON", sql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("LAST_ATTEMPTED_AT_UTC", sql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("CASE", sql, StringComparison.OrdinalIgnoreCase);
    }

    private static RawAttendance Raw(long id, DateTime? attemptedAt = null, string? reason = null, bool processed = false)
        => new()
        {
            Id = id,
            EmployeeCode = $"EMP-{id}",
            PunchTime = DateTime.UtcNow.AddMinutes(-id),
            PunchType = "IN",
            EventId = $"event-{id}",
            EventPayloadHash = new string('A', 64),
            Source = "BIOMETRIC",
            LastAttemptedAtUtc = attemptedAt,
            ProcessingReason = reason,
            IsProcessed = processed
        };
}
