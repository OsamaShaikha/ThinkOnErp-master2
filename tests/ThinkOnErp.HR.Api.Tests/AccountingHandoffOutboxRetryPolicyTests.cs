using System.Diagnostics.Metrics;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;
using ThinkOnErp.Domain.Entities.Hr;
using ThinkOnErp.Infrastructure.Data;
using ThinkOnErp.Infrastructure.Services.Hr;

namespace ThinkOnErp.HR.Api.Tests;

public sealed class AccountingHandoffOutboxRetryPolicyTests
{
    [Theory]
    [InlineData(1, 5)]
    [InlineData(2, 10)]
    [InlineData(3, 20)]
    [InlineData(4, 40)]
    [InlineData(5, 80)]
    [InlineData(6, 160)]
    [InlineData(7, 320)]
    [InlineData(8, 640)]
    [InlineData(9, 900)]
    [InlineData(10, 900)]
    public void RetryDelayUsesExponentialBackoffWithFifteenMinuteCap(int attemptCount, int expectedSeconds)
    {
        Assert.Equal(TimeSpan.FromSeconds(expectedSeconds), AccountingHandoffOutboxWorker.GetRetryDelay(attemptCount));
    }

    [Fact]
    public void RetryIsDueWithoutPreviousAttemptAndAtTheExactBoundary()
    {
        var now = new DateTime(2026, 11, 7, 12, 0, 0, DateTimeKind.Utc);
        var message = new HrAccountingOutboxMessage { AttemptCount = 2 };

        Assert.True(AccountingHandoffOutboxWorker.IsRetryDue(message, now));

        message.LastAttemptAtUtc = now.AddSeconds(-9);
        Assert.False(AccountingHandoffOutboxWorker.IsRetryDue(message, now));

        message.LastAttemptAtUtc = now.AddSeconds(-10);
        Assert.True(AccountingHandoffOutboxWorker.IsRetryDue(message, now));
    }

    [Fact]
    public async Task RelayBatchDeliversOutboxAndMovesQueuedHandoffOnce()
    {
        using var harness = new OutboxHarness();
        await harness.SeedAsync();

        await harness.Worker.RelayBatchAsync(CancellationToken.None);

        await using var db = harness.CreateDb();
        Assert.Equal("DELIVERED", (await db.HrAccountingOutboxMessages.SingleAsync()).Status);
        Assert.Equal("DELIVERED", (await db.HrAccountingHandoffs.SingleAsync()).DeliveryStatus);
        Assert.Single(await db.HrAccountingHandoffTransitions.ToListAsync());
        Assert.Equal("RECEIVED", (await db.AccountingHandoffInboxMessages.SingleAsync()).Status);
    }

    [Fact]
    public async Task RelayBatch_EmitsLowCardinalityDeliveryAndRetryMetrics()
    {
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

        using (var deliveredHarness = new OutboxHarness())
        {
            await deliveredHarness.SeedAsync();
            await deliveredHarness.Worker.RelayBatchAsync(CancellationToken.None);
        }

        using (var retryHarness = new OutboxHarness())
        {
            await retryHarness.SeedAsync(includeHandoff: false);
            await retryHarness.Worker.RelayBatchAsync(CancellationToken.None);
        }

        using (var conflictHarness = new OutboxHarness())
        {
            await conflictHarness.SeedAsync(existingInboxHash: "different-hash");
            await conflictHarness.Worker.RelayBatchAsync(CancellationToken.None);
        }

        Assert.Equal(3, measurements.Count(x => x.Instrument == "thinkonerp.hr.accounting.outbox.batches" &&
            x.Value == 1 && Equals(x.Tags.GetValueOrDefault("outcome"), "selected")));
        Assert.Contains(measurements, x => x.Instrument == "thinkonerp.hr.accounting.outbox.messages" && x.Value == 1 &&
            Equals(x.Tags.GetValueOrDefault("outcome"), "selected"));
        Assert.Contains(measurements, x => x.Instrument == "thinkonerp.hr.accounting.outbox.messages" && x.Value == 1 &&
            Equals(x.Tags.GetValueOrDefault("outcome"), "delivered"));
        Assert.Contains(measurements, x => x.Instrument == "thinkonerp.hr.accounting.outbox.messages" && x.Value == 1 &&
            Equals(x.Tags.GetValueOrDefault("outcome"), "retry"));
        Assert.Contains(measurements, x => x.Instrument == "thinkonerp.hr.accounting.outbox.messages" && x.Value == 1 &&
            Equals(x.Tags.GetValueOrDefault("outcome"), "conflict"));
        Assert.All(measurements, measurement => Assert.All(measurement.Tags.Keys, key => Assert.Equal("outcome", key)));
    }

    [Fact]
    public async Task RelayBatchTreatsMatchingInboxEventAsIdempotentDelivery()
    {
        using var harness = new OutboxHarness();
        await harness.SeedAsync(existingInboxHash: "payload-hash");

        await harness.Worker.RelayBatchAsync(CancellationToken.None);

        await using var db = harness.CreateDb();
        Assert.Equal("DELIVERED", (await db.HrAccountingOutboxMessages.SingleAsync()).Status);
        Assert.Single(await db.AccountingHandoffInboxMessages.ToListAsync());
    }

    [Fact]
    public async Task RelayBatchMarksSameEventAndHashFromDifferentHandoffAsConflict()
    {
        using var harness = new OutboxHarness();
        await harness.SeedAsync(existingInboxHash: "payload-hash", existingInboxHandoffId: 2);

        await harness.Worker.RelayBatchAsync(CancellationToken.None);

        await using var db = harness.CreateDb();
        var message = await db.HrAccountingOutboxMessages.SingleAsync();
        Assert.Equal("CONFLICT", message.Status);
        Assert.Contains("different", message.LastError, StringComparison.Ordinal);
        Assert.Equal("QUEUED", (await db.HrAccountingHandoffs.SingleAsync(x => x.Id == 1)).DeliveryStatus);
        Assert.Equal(2, (await db.AccountingHandoffInboxMessages.SingleAsync()).HandoffId);
    }

    [Fact]
    public async Task RelayBatchMarksDifferentPayloadForSameEventAsConflict()
    {
        using var harness = new OutboxHarness();
        await harness.SeedAsync(existingInboxHash: "different-hash");

        await harness.Worker.RelayBatchAsync(CancellationToken.None);

        await using var db = harness.CreateDb();
        var message = await db.HrAccountingOutboxMessages.SingleAsync();
        Assert.Equal("CONFLICT", message.Status);
        Assert.Equal(1, message.AttemptCount);
        Assert.Contains("different payload hash", message.LastError, StringComparison.Ordinal);
        Assert.Equal("QUEUED", (await db.HrAccountingHandoffs.SingleAsync()).DeliveryStatus);
    }

    [Fact]
    public async Task RelayFailureIsPersistedAndScheduledWithBackoff()
    {
        using var harness = new OutboxHarness();
        await harness.SeedAsync(includeHandoff: false);

        await harness.Worker.RelayBatchAsync(CancellationToken.None);

        await using var db = harness.CreateDb();
        var message = await db.HrAccountingOutboxMessages.SingleAsync();
        Assert.Equal("PENDING", message.Status);
        Assert.Equal(1, message.AttemptCount);
        Assert.NotNull(message.LastAttemptAtUtc);
        Assert.False(AccountingHandoffOutboxWorker.IsRetryDue(message, message.LastAttemptAtUtc!.Value.AddSeconds(4)));
        Assert.True(AccountingHandoffOutboxWorker.IsRetryDue(message, message.LastAttemptAtUtc.Value.AddSeconds(5)));
        Assert.False(string.IsNullOrWhiteSpace(message.LastError));
    }

    [Fact]
    public async Task BackedOffMessagesDoNotStarveDueMessagesBeyondFirstPage()
    {
        using var harness = new OutboxHarness();
        await harness.SeedBackoffStarvationAsync();

        await harness.Worker.RelayBatchAsync(CancellationToken.None);

        await using var db = harness.CreateDb();
        Assert.Equal("DELIVERED", (await db.HrAccountingOutboxMessages.SingleAsync(x => x.Id == 1)).Status);
        Assert.Equal(250, await db.HrAccountingOutboxMessages.CountAsync(x => x.Status == "PENDING"));
        Assert.Single(await db.AccountingHandoffInboxMessages.ToListAsync());
    }

    [Fact]
    public void DueMessageQueryTranslatesToOracleSqlWithoutConnecting()
    {
        var options = new DbContextOptionsBuilder<OracleDbContext>()
            .UseOracle("User Id=test;Password=test;Data Source=localhost:1521/XEPDB1")
            .Options;
        using var db = new OracleDbContext(options);

        var sql = AccountingHandoffOutboxWorker.QueryDueMessages(db, DateTime.UtcNow).Take(25).ToQueryString();

        Assert.Contains("HR_ACCOUNTING_OUTBOX", sql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("ATTEMPT_COUNT", sql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("LAST_ATTEMPT_AT_UTC", sql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("CASE", sql, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData(0, 5)]
    [InlineData(-1, 5)]
    [InlineData(11, 900)]
    [InlineData(100, 900)]
    public void RetryDelay_HandlesBoundaryAndExcessiveAttempts(int attemptCount, int expectedSeconds)
    {
        Assert.Equal(TimeSpan.FromSeconds(expectedSeconds), AccountingHandoffOutboxWorker.GetRetryDelay(attemptCount));
    }

    [Fact]
    public async Task RelayBatchSkipsNonPendingMessages()
    {
        using var harness = new OutboxHarness();
        await using (var db = harness.CreateDb())
        {
            db.HrAccountingOutboxMessages.AddRange(
                new HrAccountingOutboxMessage
                {
                    Id = 201, HandoffId = 1, EventKey = "evt-delivered", EventType = "PAYROLL_SUBMIT",
                    PayloadHash = "hash1", Status = "DELIVERED", AttemptCount = 1, CreatedAtUtc = DateTime.UtcNow
                },
                new HrAccountingOutboxMessage
                {
                    Id = 202, HandoffId = 1, EventKey = "evt-conflict", EventType = "PAYROLL_SUBMIT",
                    PayloadHash = "hash2", Status = "CONFLICT", AttemptCount = 1, CreatedAtUtc = DateTime.UtcNow
                }
            );
            await db.SaveChangesAsync();
        }

        await harness.Worker.RelayBatchAsync(CancellationToken.None);

        await using (var verifyDb = harness.CreateDb())
        {
            Assert.Equal("DELIVERED", (await verifyDb.HrAccountingOutboxMessages.SingleAsync(m => m.Id == 201)).Status);
            Assert.Equal("CONFLICT", (await verifyDb.HrAccountingOutboxMessages.SingleAsync(m => m.Id == 202)).Status);
            Assert.Equal(1, (await verifyDb.HrAccountingOutboxMessages.SingleAsync(m => m.Id == 201)).AttemptCount);
            Assert.Equal(1, (await verifyDb.HrAccountingOutboxMessages.SingleAsync(m => m.Id == 202)).AttemptCount);
            Assert.Empty(await verifyDb.AccountingHandoffInboxMessages.ToListAsync());
        }
    }

    [Fact]
    public async Task RelayBatchCancelledTokenAbortsWithoutDelivering()
    {
        using var harness = new OutboxHarness();
        await harness.SeedAsync();

        using var cts = new CancellationTokenSource();
        cts.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => harness.Worker.RelayBatchAsync(cts.Token));

        await using var verifyDb = harness.CreateDb();
        Assert.Equal("PENDING", (await verifyDb.HrAccountingOutboxMessages.SingleAsync()).Status);
        Assert.Empty(await verifyDb.AccountingHandoffInboxMessages.ToListAsync());
    }

    [Fact]
    public async Task RelayBatch_PayrollWithdrawal_SetsInboxStatusToWithdrawnAndPreservesHandoffStatus()
    {
        using var harness = new OutboxHarness();
        await using (var db = harness.CreateDb())
        {
            db.HrAccountingHandoffs.Add(new HrAccountingHandoff
            {
                Id = 15, CompanyId = 1, SourceType = "PAYROLL", SourceId = "run-15", SourceVersion = 1,
                Operation = "SUBMIT", HandoffReference = "HR-15", PayloadHash = "withdraw-payload-hash",
                DeliveryStatus = "WITHDRAWN", CreatedAtUtc = DateTime.UtcNow, CreatedBy = "hr"
            });
            db.HrAccountingOutboxMessages.Add(new HrAccountingOutboxMessage
            {
                Id = 15, HandoffId = 15, EventKey = "withdraw-event-15", EventType = "PAYROLL_WITHDRAW",
                PayloadHash = "withdraw-payload-hash", Status = "PENDING", CreatedAtUtc = DateTime.UtcNow
            });
            await db.SaveChangesAsync();
        }

        await harness.Worker.RelayBatchAsync(CancellationToken.None);

        await using (var verifyDb = harness.CreateDb())
        {
            var outbox = await verifyDb.HrAccountingOutboxMessages.SingleAsync(m => m.Id == 15);
            Assert.Equal("DELIVERED", outbox.Status);
            Assert.Equal(1, outbox.AttemptCount);

            var inbox = await verifyDb.AccountingHandoffInboxMessages.SingleAsync(m => m.EventKey == "withdraw-event-15");
            Assert.Equal("WITHDRAWN", inbox.Status);
            Assert.Equal(15, inbox.HandoffId);

            var handoff = await verifyDb.HrAccountingHandoffs.SingleAsync(h => h.Id == 15);
            Assert.Equal("WITHDRAWN", handoff.DeliveryStatus);
            Assert.Empty(await verifyDb.HrAccountingHandoffTransitions.Where(t => t.HandoffId == 15).ToListAsync());
        }
    }

    [Fact]
    public async Task RelayBatch_MultipleDueMessagesInSameBatch_DeliversAllSequentially()
    {
        using var harness = new OutboxHarness();
        await using (var db = harness.CreateDb())
        {
            for (int i = 10; i <= 12; i++)
            {
                db.HrAccountingHandoffs.Add(new HrAccountingHandoff
                {
                    Id = i, CompanyId = 1, SourceType = "PAYROLL", SourceId = $"run-{i}", SourceVersion = 1,
                    Operation = "SUBMIT", HandoffReference = $"HR-{i}", PayloadHash = $"hash-{i}",
                    DeliveryStatus = "QUEUED", CreatedAtUtc = DateTime.UtcNow, CreatedBy = "hr"
                });
                db.HrAccountingOutboxMessages.Add(new HrAccountingOutboxMessage
                {
                    Id = i, HandoffId = i, EventKey = $"event-{i}", EventType = "PAYROLL_SUBMIT",
                    PayloadHash = $"hash-{i}", Status = "PENDING", CreatedAtUtc = DateTime.UtcNow
                });
            }
            await db.SaveChangesAsync();
        }

        await harness.Worker.RelayBatchAsync(CancellationToken.None);

        await using (var verifyDb = harness.CreateDb())
        {
            var outboxMessages = await verifyDb.HrAccountingOutboxMessages.Where(m => m.Id >= 10 && m.Id <= 12).ToListAsync();
            Assert.Equal(3, outboxMessages.Count);
            Assert.All(outboxMessages, m => Assert.Equal("DELIVERED", m.Status));

            var handoffs = await verifyDb.HrAccountingHandoffs.Where(h => h.Id >= 10 && h.Id <= 12).ToListAsync();
            Assert.Equal(3, handoffs.Count);
            Assert.All(handoffs, h => Assert.Equal("DELIVERED", h.DeliveryStatus));

            var inboxMessages = await verifyDb.AccountingHandoffInboxMessages.Where(m => m.HandoffId >= 10 && m.HandoffId <= 12).ToListAsync();
            Assert.Equal(3, inboxMessages.Count);
            Assert.All(inboxMessages, m => Assert.Equal("RECEIVED", m.Status));
        }
    }

    [Fact]
    public void QueryDueMessages_PrioritizesUnattemptedMessagesFirst()
    {
        using var harness = new OutboxHarness();
        var now = DateTime.UtcNow;

        using (var db = harness.CreateDb())
        {
            db.HrAccountingOutboxMessages.AddRange(
                new HrAccountingOutboxMessage
                {
                    Id = 51, HandoffId = 1, EventKey = "attempted-due", EventType = "PAYROLL_SUBMIT",
                    PayloadHash = "hash1", Status = "PENDING", AttemptCount = 1,
                    CreatedAtUtc = now.AddMinutes(-10), LastAttemptAtUtc = now.AddMinutes(-1)
                },
                new HrAccountingOutboxMessage
                {
                    Id = 52, HandoffId = 2, EventKey = "unattempted", EventType = "PAYROLL_SUBMIT",
                    PayloadHash = "hash2", Status = "PENDING", AttemptCount = 0,
                    CreatedAtUtc = now.AddMinutes(-1), LastAttemptAtUtc = null
                }
            );
            db.SaveChanges();

            var dueMessages = AccountingHandoffOutboxWorker.QueryDueMessages(db, now).ToList();
            Assert.Equal(2, dueMessages.Count);
            // Unattempted (LastAttemptAtUtc == null) comes first according to `OrderBy(x => x.LastAttemptAtUtc.HasValue ? 1 : 0)`
            Assert.Equal(52, dueMessages[0].Id);
            Assert.Equal(51, dueMessages[1].Id);
        }
    }

    [Fact]
    public async Task RelayFailure_ClampsErrorMessageTo1000Chars()
    {
        using var harness = new OutboxHarness();
        // Seed outbox message without corresponding handoff, so SingleAsync throws
        await harness.SeedAsync(includeHandoff: false);

        await harness.Worker.RelayBatchAsync(CancellationToken.None);

        await using var verifyDb = harness.CreateDb();
        var message = await verifyDb.HrAccountingOutboxMessages.SingleAsync(m => m.Id == 1);
        Assert.Equal("PENDING", message.Status);
        Assert.Equal(1, message.AttemptCount);
        Assert.NotNull(message.LastError);
        Assert.True(message.LastError.Length <= 1000);
    }

    [Theory]
    [InlineData(1, 4, false)]
    [InlineData(1, 6, true)]
    [InlineData(3, 19, false)]
    [InlineData(3, 21, true)]
    [InlineData(5, 79, false)]
    [InlineData(5, 81, true)]
    [InlineData(8, 639, false)]
    [InlineData(8, 641, true)]
    [InlineData(9, 899, false)]
    [InlineData(9, 901, true)]
    public void QueryDueMessages_RespectsBackoffThresholds(int attempts, int elapsedSeconds, bool expectedDue)
    {
        using var harness = new OutboxHarness();
        var now = new DateTime(2026, 11, 7, 12, 0, 0, DateTimeKind.Utc);

        using (var db = harness.CreateDb())
        {
            db.HrAccountingOutboxMessages.Add(new HrAccountingOutboxMessage
            {
                Id = 301,
                HandoffId = 1,
                EventKey = $"threshold-{attempts}-{elapsedSeconds}",
                EventType = "PAYROLL_SUBMIT",
                PayloadHash = "hash",
                Status = "PENDING",
                AttemptCount = attempts,
                LastAttemptAtUtc = now.AddSeconds(-elapsedSeconds),
                CreatedAtUtc = now.AddHours(-1)
            });
            db.SaveChanges();

            var due = AccountingHandoffOutboxWorker.QueryDueMessages(db, now).Any(m => m.Id == 301);
            Assert.Equal(expectedDue, due);
        }
    }

    [Fact]
    public async Task RelayBatch_ConsecutiveFailures_IncrementsAttemptCountAndAppliesBackoff()
    {
        using var harness = new OutboxHarness();
        // Seed outbox without handoff to force failure
        await harness.SeedAsync(includeHandoff: false);

        // Attempt 1
        await harness.Worker.RelayBatchAsync(CancellationToken.None);

        await using (var db1 = harness.CreateDb())
        {
            var msg1 = await db1.HrAccountingOutboxMessages.SingleAsync(m => m.Id == 1);
            Assert.Equal(1, msg1.AttemptCount);
            Assert.Equal("PENDING", msg1.Status);
            // Fast forward time by making LastAttemptAtUtc older than 10s (attempt 2 delay is 10s)
            msg1.LastAttemptAtUtc = DateTime.UtcNow.AddSeconds(-15);
            await db1.SaveChangesAsync();
        }

        // Attempt 2
        await harness.Worker.RelayBatchAsync(CancellationToken.None);

        await using (var db2 = harness.CreateDb())
        {
            var msg2 = await db2.HrAccountingOutboxMessages.SingleAsync(m => m.Id == 1);
            Assert.Equal(2, msg2.AttemptCount);
            Assert.Equal("PENDING", msg2.Status);
            // Fast forward time by making LastAttemptAtUtc older than 20s (attempt 3 delay is 20s)
            msg2.LastAttemptAtUtc = DateTime.UtcNow.AddSeconds(-25);
            await db2.SaveChangesAsync();
        }

        // Attempt 3
        await harness.Worker.RelayBatchAsync(CancellationToken.None);

        await using (var db3 = harness.CreateDb())
        {
            var msg3 = await db3.HrAccountingOutboxMessages.SingleAsync(m => m.Id == 1);
            Assert.Equal(3, msg3.AttemptCount);
            Assert.Equal("PENDING", msg3.Status);
        }
    }

    [Fact]
    public async Task RelayBatch_WhenHandoffAlreadyDelivered_DeliversOutboxWithoutCreatingDuplicateTransition()
    {
        using var harness = new OutboxHarness();
        await using (var db = harness.CreateDb())
        {
            db.HrAccountingHandoffs.Add(new HrAccountingHandoff
            {
                Id = 77,
                CompanyId = 1,
                SourceType = "PAYROLL",
                SourceId = "run-77",
                SourceVersion = 1,
                Operation = "SUBMIT",
                HandoffReference = "HR-77",
                PayloadHash = "hash-77",
                DeliveryStatus = "DELIVERED",
                CreatedAtUtc = DateTime.UtcNow,
                CreatedBy = "hr"
            });
            db.HrAccountingOutboxMessages.Add(new HrAccountingOutboxMessage
            {
                Id = 77,
                HandoffId = 77,
                EventKey = "event-77",
                EventType = "PAYROLL_SUBMIT",
                PayloadHash = "hash-77",
                Status = "PENDING",
                CreatedAtUtc = DateTime.UtcNow
            });
            await db.SaveChangesAsync();
        }

        await harness.Worker.RelayBatchAsync(CancellationToken.None);

        await using (var verifyDb = harness.CreateDb())
        {
            var outbox = await verifyDb.HrAccountingOutboxMessages.SingleAsync(m => m.Id == 77);
            Assert.Equal("DELIVERED", outbox.Status);
            Assert.Equal(1, outbox.AttemptCount);

            var handoff = await verifyDb.HrAccountingHandoffs.SingleAsync(h => h.Id == 77);
            Assert.Equal("DELIVERED", handoff.DeliveryStatus);
            Assert.Empty(await verifyDb.HrAccountingHandoffTransitions.Where(t => t.HandoffId == 77).ToListAsync());
        }
    }

    [Theory]
    [InlineData("LOAN_FUNDING_REQUEST")]
    [InlineData("EOS_SETTLEMENT_PAYOUT_REQUEST")]
    public async Task RelayBatch_NonPayrollSubmitEventTypes_DeliverAndTransitionQueuedHandoff(string eventType)
    {
        using var harness = new OutboxHarness();
        await using (var db = harness.CreateDb())
        {
            db.HrAccountingHandoffs.Add(new HrAccountingHandoff
            {
                Id = 88,
                CompanyId = 1,
                SourceType = eventType == "LOAN_FUNDING_REQUEST" ? "LOAN" : "EOS",
                SourceId = "88",
                SourceVersion = 1,
                Operation = "FUND",
                HandoffReference = "HR-88",
                PayloadHash = "hash-88",
                DeliveryStatus = "QUEUED",
                CreatedAtUtc = DateTime.UtcNow,
                CreatedBy = "hr"
            });
            db.HrAccountingOutboxMessages.Add(new HrAccountingOutboxMessage
            {
                Id = 88,
                HandoffId = 88,
                EventKey = $"event-88-{eventType}",
                EventType = eventType,
                PayloadHash = "hash-88",
                Status = "PENDING",
                CreatedAtUtc = DateTime.UtcNow
            });
            await db.SaveChangesAsync();
        }

        await harness.Worker.RelayBatchAsync(CancellationToken.None);

        await using (var verifyDb = harness.CreateDb())
        {
            var outbox = await verifyDb.HrAccountingOutboxMessages.SingleAsync(m => m.Id == 88);
            Assert.Equal("DELIVERED", outbox.Status);

            var inbox = await verifyDb.AccountingHandoffInboxMessages.SingleAsync(m => m.HandoffId == 88);
            Assert.Equal("RECEIVED", inbox.Status);

            var handoff = await verifyDb.HrAccountingHandoffs.SingleAsync(h => h.Id == 88);
            Assert.Equal("DELIVERED", handoff.DeliveryStatus);

            var transition = await verifyDb.HrAccountingHandoffTransitions.SingleAsync(t => t.HandoffId == 88);
            Assert.Equal("QUEUED", transition.FromStatus);
            Assert.Equal("DELIVERED", transition.ToStatus);
            Assert.Equal("OUTBOX", transition.Actor);
        }
    }

    [Theory]
    [InlineData(0, 4, false)]
    [InlineData(0, 5, true)]
    [InlineData(-1, 4, false)]
    [InlineData(-1, 5, true)]
    [InlineData(-10, 5, true)]
    public void IsRetryDue_WhenAttemptCountZeroOrNegative_DefaultsToFiveSeconds(int attemptCount, int elapsedSeconds, bool expectedDue)
    {
        var now = new DateTime(2026, 11, 7, 12, 0, 0, DateTimeKind.Utc);
        var msg = new HrAccountingOutboxMessage
        {
            AttemptCount = attemptCount,
            LastAttemptAtUtc = now.AddSeconds(-elapsedSeconds)
        };
        Assert.Equal(expectedDue, AccountingHandoffOutboxWorker.IsRetryDue(msg, now));
    }

    [Theory]
    [InlineData(9, 899, false)]
    [InlineData(9, 900, true)]
    [InlineData(10, 899, false)]
    [InlineData(10, 900, true)]
    [InlineData(25, 899, false)]
    [InlineData(25, 900, true)]
    [InlineData(100, 900, true)]
    public void IsRetryDue_WhenAttemptCountHigh_CapsAtFifteenMinutes(int attemptCount, int elapsedSeconds, bool expectedDue)
    {
        var now = new DateTime(2026, 11, 7, 12, 0, 0, DateTimeKind.Utc);
        var msg = new HrAccountingOutboxMessage
        {
            AttemptCount = attemptCount,
            LastAttemptAtUtc = now.AddSeconds(-elapsedSeconds)
        };
        Assert.Equal(expectedDue, AccountingHandoffOutboxWorker.IsRetryDue(msg, now));
    }

    [Fact]
    public void QueryDueMessages_Ordering_SortsByLastAttemptThenCreatedAtThenId()
    {
        using var harness = new OutboxHarness();
        var now = DateTime.UtcNow;

        using (var db = harness.CreateDb())
        {
            db.HrAccountingOutboxMessages.AddRange(
                new HrAccountingOutboxMessage
                {
                    Id = 61, HandoffId = 1, EventKey = "evt-61", EventType = "PAYROLL_SUBMIT",
                    PayloadHash = "h61", Status = "PENDING", AttemptCount = 1,
                    CreatedAtUtc = now.AddMinutes(-5), LastAttemptAtUtc = now.AddMinutes(-1)
                },
                new HrAccountingOutboxMessage
                {
                    Id = 62, HandoffId = 2, EventKey = "evt-62", EventType = "PAYROLL_SUBMIT",
                    PayloadHash = "h62", Status = "PENDING", AttemptCount = 1,
                    CreatedAtUtc = now.AddMinutes(-10), LastAttemptAtUtc = now.AddMinutes(-1)
                },
                new HrAccountingOutboxMessage
                {
                    Id = 63, HandoffId = 3, EventKey = "evt-63", EventType = "PAYROLL_SUBMIT",
                    PayloadHash = "h63", Status = "PENDING", AttemptCount = 1,
                    CreatedAtUtc = now.AddMinutes(-2), LastAttemptAtUtc = now.AddMinutes(-2)
                }
            );
            db.SaveChanges();

            var due = AccountingHandoffOutboxWorker.QueryDueMessages(db, now).ToList();
            Assert.Equal(3, due.Count);
            Assert.Equal(63, due[0].Id);
            Assert.Equal(62, due[1].Id);
            Assert.Equal(61, due[2].Id);
        }
    }

    [Fact]
    public async Task RelayBatch_WhenAlreadyDeliveredWithSamePayloadHash_IncrementsAttemptCountAndKeepsSingleInboxMessage()
    {
        using var harness = new OutboxHarness();
        await harness.SeedAsync(includeHandoff: true, existingInboxHash: "payload-hash");

        await harness.Worker.RelayBatchAsync(CancellationToken.None);

        await using var verifyDb = harness.CreateDb();
        var outbox = await verifyDb.HrAccountingOutboxMessages.SingleAsync();
        Assert.Equal("DELIVERED", outbox.Status);
        Assert.Equal(1, outbox.AttemptCount);
        Assert.Null(outbox.LastError);

        var inboxes = await verifyDb.AccountingHandoffInboxMessages.ToListAsync();
        Assert.Single(inboxes);
        Assert.Equal("payload-hash", inboxes[0].PayloadHash);
    }

    private sealed class OutboxHarness : IDisposable
    {
        private readonly DbContextOptions<OracleDbContext> options;
        private readonly ServiceProvider provider;
        private readonly string storeName = Guid.NewGuid().ToString();

        public AccountingHandoffOutboxWorker Worker { get; }

        public OutboxHarness()
        {
            options = new DbContextOptionsBuilder<OracleDbContext>()
                .UseInMemoryDatabase(storeName)
                .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
                .Options;
            var services = new ServiceCollection();
            services.AddDbContext<OracleDbContext>(builder => builder.UseInMemoryDatabase(storeName)
                .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning)));
            provider = services.BuildServiceProvider();
            Worker = new AccountingHandoffOutboxWorker(provider.GetRequiredService<IServiceScopeFactory>(),
                NullLogger<AccountingHandoffOutboxWorker>.Instance);
        }

        public OracleDbContext CreateDb() => new(options);

        public async Task SeedAsync(bool includeHandoff = true, string? existingInboxHash = null, long existingInboxHandoffId = 1)
        {
            await using var db = CreateDb();
            if (includeHandoff)
            {
                db.HrAccountingHandoffs.Add(new HrAccountingHandoff
                {
                    Id = 1, CompanyId = 1, SourceType = "PAYROLL", SourceId = "run-1", SourceVersion = 1,
                    Operation = "SUBMIT", HandoffReference = "HR-1", PayloadHash = "payload-hash",
                    DeliveryStatus = "QUEUED", CreatedAtUtc = DateTime.UtcNow, CreatedBy = "test"
                });
                if (existingInboxHandoffId != 1)
                {
                    db.HrAccountingHandoffs.Add(new HrAccountingHandoff
                    {
                        Id = existingInboxHandoffId, CompanyId = 1, SourceType = "PAYROLL", SourceId = "run-other", SourceVersion = 1,
                        Operation = "SUBMIT", HandoffReference = "HR-OTHER", PayloadHash = "other-hash",
                        DeliveryStatus = "QUEUED", CreatedAtUtc = DateTime.UtcNow, CreatedBy = "test"
                    });
                }
            }
            db.HrAccountingOutboxMessages.Add(new HrAccountingOutboxMessage
            {
                Id = 1, HandoffId = 1, EventKey = "event-1", EventType = "PAYROLL_SUBMIT",
                PayloadHash = "payload-hash", Status = "PENDING", CreatedAtUtc = DateTime.UtcNow
            });
            if (existingInboxHash != null)
            {
                db.AccountingHandoffInboxMessages.Add(new AccountingHandoffInboxMessage
                {
                    Id = 1, HandoffId = existingInboxHandoffId, EventKey = "event-1", PayloadHash = existingInboxHash,
                    ReceivedAtUtc = DateTime.UtcNow, Status = "RECEIVED"
                });
            }
            await db.SaveChangesAsync();
        }

        public async Task SeedBackoffStarvationAsync()
        {
            var now = DateTime.UtcNow;
            await using var db = CreateDb();
            db.HrAccountingHandoffs.Add(new HrAccountingHandoff
            {
                Id = 1, CompanyId = 1, SourceType = "PAYROLL", SourceId = "run-1", SourceVersion = 1,
                Operation = "SUBMIT", HandoffReference = "HR-1", PayloadHash = "payload-hash",
                DeliveryStatus = "QUEUED", CreatedAtUtc = now, CreatedBy = "test"
            });
            db.HrAccountingOutboxMessages.Add(new HrAccountingOutboxMessage
            {
                Id = 1, HandoffId = 1, EventKey = "due-event", EventType = "PAYROLL_SUBMIT",
                PayloadHash = "payload-hash", Status = "PENDING", AttemptCount = 1,
                CreatedAtUtc = now.AddHours(-1), LastAttemptAtUtc = now.AddMinutes(-1)
            });
            for (var id = 2; id <= 251; id++)
            {
                db.HrAccountingOutboxMessages.Add(new HrAccountingOutboxMessage
                {
                    Id = id, HandoffId = 1000 + id, EventKey = $"backoff-{id}", EventType = "PAYROLL_SUBMIT",
                    PayloadHash = "payload-hash", Status = "PENDING", AttemptCount = 9,
                    CreatedAtUtc = now.AddHours(-2), LastAttemptAtUtc = now.AddMinutes(-10)
                });
            }
            await db.SaveChangesAsync();
        }

        public void Dispose() => provider.Dispose();
    }
}
