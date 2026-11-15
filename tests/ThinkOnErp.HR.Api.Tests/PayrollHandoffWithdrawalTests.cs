using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Moq;
using ThinkOnErp.Application.Services.Accounting;
using ThinkOnErp.Application.Services.Hr;
using ThinkOnErp.Domain.Entities.Accounting;
using ThinkOnErp.Domain.Entities.Hr;
using ThinkOnErp.Domain.Interfaces.Accounting;
using ThinkOnErp.Infrastructure.Data;
using ThinkOnErp.Infrastructure.Services.Hr;
using Xunit;

namespace ThinkOnErp.HR.Api.Tests;

public sealed class PayrollHandoffWithdrawalTests
{
    [Fact]
    public async Task WithdrawingQueuedHandoffCancelsPendingSubmitAndReleasesReservations()
    {
        using var harness = new WithdrawalHarness();
        await harness.SeedAsync("QUEUED", "PENDING", includeReservation: true);

        await using (var db = harness.CreateDb())
        {
            var status = await harness.CreateService(db).WithdrawPayrollHandoffAsync(9, "duplicate run", "hr-admin");
            Assert.Equal("WITHDRAWN", status!.DeliveryStatus);
        }

        await using var verify = harness.CreateDb();
        Assert.Equal("CANCELLED", (await verify.HrAccountingOutboxMessages.SingleAsync(x => x.EventType == "PAYROLL_SUBMIT")).Status);
        Assert.Equal("PENDING", (await verify.HrAccountingOutboxMessages.SingleAsync(x => x.EventType == "PAYROLL_WITHDRAW")).Status);
        Assert.Null((await verify.EmployeeAdvances.SingleAsync()).ReservedByHandoffId);
        Assert.Single(await verify.HrAccountingHandoffTransitions.Where(x => x.ToStatus == "WITHDRAWN").ToListAsync());
    }

    [Fact]
    public async Task WithdrawingDeliveredHandoffQueuesWithdrawalWithoutRewritingDeliveredEvent()
    {
        using var harness = new WithdrawalHarness();
        await harness.SeedAsync("DELIVERED", "DELIVERED");

        await using (var db = harness.CreateDb())
            await harness.CreateService(db).WithdrawPayrollHandoffAsync(9, "run corrected", "hr-admin");

        await using var verify = harness.CreateDb();
        Assert.Equal("DELIVERED", (await verify.HrAccountingOutboxMessages.SingleAsync(x => x.EventType == "PAYROLL_SUBMIT")).Status);
        Assert.Single(await verify.HrAccountingOutboxMessages.Where(x => x.EventType == "PAYROLL_WITHDRAW" && x.Status == "PENDING").ToListAsync());
        Assert.Equal("WITHDRAWN", (await verify.HrAccountingHandoffs.SingleAsync()).DeliveryStatus);
    }

    [Fact]
    public async Task ExactWithdrawalReplayDoesNotCreateAnotherWithdrawalEvent()
    {
        using var harness = new WithdrawalHarness();
        await harness.SeedAsync("QUEUED", "PENDING");

        await using (var db = harness.CreateDb())
        {
            var service = harness.CreateService(db);
            await service.WithdrawPayrollHandoffAsync(9, "duplicate run", "hr-admin");
            await service.WithdrawPayrollHandoffAsync(9, "duplicate run", "hr-admin");
        }

        await using var verify = harness.CreateDb();
        Assert.Single(await verify.HrAccountingOutboxMessages.Where(x => x.EventType == "PAYROLL_WITHDRAW").ToListAsync());
        Assert.Single(await verify.HrAccountingHandoffTransitions.Where(x => x.ToStatus == "WITHDRAWN").ToListAsync());
    }

    [Fact]
    public async Task AcceptedHandoffCannotBeWithdrawn()
    {
        using var harness = new WithdrawalHarness();
        await harness.SeedAsync("ACCEPTED", "DELIVERED");

        await using (var db = harness.CreateDb())
            await Assert.ThrowsAsync<InvalidOperationException>(() =>
                harness.CreateService(db).WithdrawPayrollHandoffAsync(9, "too late", "hr-admin"));

        await using var verify = harness.CreateDb();
        Assert.Equal("ACCEPTED", (await verify.HrAccountingHandoffs.SingleAsync()).DeliveryStatus);
        Assert.Empty(await verify.HrAccountingOutboxMessages.Where(x => x.EventType == "PAYROLL_WITHDRAW").ToListAsync());
    }

    [Fact]
    public async Task AccountingReturnRequiresReasonAndReleasesPayrollReservation()
    {
        using var harness = new WithdrawalHarness();
        await harness.SeedAsync("DELIVERED", "DELIVERED", includeReservation: true);

        await using (var db = harness.CreateDb())
        {
            var service = harness.CreateService(db);
            await Assert.ThrowsAsync<ArgumentException>(() => service.ReviewAsync(11, accept: false, reason: " ", user: "accounting-user"));
            var returned = await service.ReviewAsync(11, accept: false, reason: "wrong payroll period", user: "accounting-user");
            Assert.Equal("RETURNED", returned.DeliveryStatus);
        }

        await using var verify = harness.CreateDb();
        Assert.Equal("RETURNED", (await verify.HrAccountingHandoffs.SingleAsync()).DeliveryStatus);
        Assert.Equal("wrong payroll period", (await verify.HrAccountingHandoffTransitions.SingleAsync(x => x.ToStatus == "RETURNED")).Reason);
        Assert.Null((await verify.EmployeeAdvances.SingleAsync()).ReservedByHandoffId);
    }

    [Fact]
    public async Task AccountingCanAcceptOnlyDeliveredHandoff()
    {
        using var harness = new WithdrawalHarness();
        await harness.SeedAsync("QUEUED", "PENDING");

        await using var db = harness.CreateDb();
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            harness.CreateService(db).ReviewAsync(11, accept: true, reason: null, user: "accounting-user"));
        await using var verify = harness.CreateDb();
        Assert.Equal("QUEUED", (await verify.HrAccountingHandoffs.SingleAsync()).DeliveryStatus);
        Assert.Empty(await verify.HrAccountingHandoffTransitions.Where(x => x.ToStatus == "ACCEPTED").ToListAsync());
    }

    [Fact]
    public async Task WithdrawPayrollHandoffAsync_WhenHandoffMissing_ReturnsNull()
    {
        using var harness = new WithdrawalHarness();
        await harness.SeedAsync("QUEUED", "PENDING");

        await using var db = harness.CreateDb();
        var result = await harness.CreateService(db).WithdrawPayrollHandoffAsync(9999, "Valid reason", "hr-admin");
        Assert.Null(result);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task WithdrawPayrollHandoffAsync_RejectsEmptyOrWhitespaceReason(string invalidReason)
    {
        using var harness = new WithdrawalHarness();
        await harness.SeedAsync("QUEUED", "PENDING");

        await using var db = harness.CreateDb();
        await Assert.ThrowsAsync<ArgumentException>(() =>
            harness.CreateService(db).WithdrawPayrollHandoffAsync(9, invalidReason, "hr-admin"));
    }

    [Fact]
    public async Task WithdrawPayrollHandoffAsync_RejectsReasonExceeding1000Chars()
    {
        using var harness = new WithdrawalHarness();
        await harness.SeedAsync("QUEUED", "PENDING");

        var longReason = new string('A', 1001);
        await using var db = harness.CreateDb();
        await Assert.ThrowsAsync<ArgumentException>(() =>
            harness.CreateService(db).WithdrawPayrollHandoffAsync(9, longReason, "hr-admin"));
    }

    [Fact]
    public async Task WithdrawPayrollHandoffAsync_WhenAlreadyWithdrawnWithDifferentReason_ThrowsInvalidOperationException()
    {
        using var harness = new WithdrawalHarness();
        await harness.SeedAsync("QUEUED", "PENDING");

        await using (var db = harness.CreateDb())
        {
            var service = harness.CreateService(db);
            await service.WithdrawPayrollHandoffAsync(9, "Initial withdrawal", "hr-admin");
            var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
                service.WithdrawPayrollHandoffAsync(9, "Different reason", "hr-admin"));
            Assert.Contains("Payroll handoff has already been withdrawn and cannot be changed", ex.Message);
        }
    }

    [Fact]
    public async Task WithdrawPayrollHandoffAsync_WhenStatusIsPostedOrReversed_ThrowsInvalidOperationException()
    {
        using var harness = new WithdrawalHarness();
        await harness.SeedAsync("POSTED", "DELIVERED");

        await using var db = harness.CreateDb();
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            harness.CreateService(db).WithdrawPayrollHandoffAsync(9, "Cancel posted", "hr-admin"));
        Assert.Contains("Payroll handoff can be withdrawn only before Accounting accepts it", ex.Message);
    }

    [Fact]
    public async Task WithdrawPayrollHandoffAsync_ReleasesLoanScheduleReservationsAsWell()
    {
        using var harness = new WithdrawalHarness();
        await harness.SeedAsync("QUEUED", "PENDING", includeReservation: true);

        await using (var db = harness.CreateDb())
        {
            db.LoanRepaymentSchedules.Add(new LoanRepaymentSchedule
            {
                Id = 101,
                EmployeeLoanId = 51,
                InstallmentNo = 1,
                ScheduledAmount = 100m,
                PaidAmount = 0m,
                Status = "PENDING",
                ReservedByHandoffId = 11
            });
            await db.SaveChangesAsync();
        }

        await using (var db = harness.CreateDb())
        {
            var service = harness.CreateService(db);
            var status = await service.WithdrawPayrollHandoffAsync(9, "release all reservations", "hr-admin");
            Assert.Equal("WITHDRAWN", status!.DeliveryStatus);
        }

        await using (var verify = harness.CreateDb())
        {
            var schedule = await verify.LoanRepaymentSchedules.SingleAsync(s => s.Id == 101);
            Assert.Null(schedule.ReservedByHandoffId);
            var advance = await verify.EmployeeAdvances.SingleAsync();
            Assert.Null(advance.ReservedByHandoffId);
        }
    }

    [Fact]
    public async Task ReviewAsync_WhenHandoffNotFound_ThrowsKeyNotFoundException()
    {
        using var harness = new WithdrawalHarness();
        await harness.SeedAsync("DELIVERED", "DELIVERED");

        await using var db = harness.CreateDb();
        var ex = await Assert.ThrowsAsync<KeyNotFoundException>(() =>
            harness.CreateService(db).ReviewAsync(9999, accept: true, reason: null, user: "accounting-user"));
        Assert.Contains("Handoff was not found in the Accounting scope", ex.Message);
    }

    [Fact]
    public async Task ReviewAsync_AcceptDeliveredHandoff_TransitionsToAcceptedAndPreservesReservations()
    {
        using var harness = new WithdrawalHarness();
        await harness.SeedAsync("DELIVERED", "DELIVERED", includeReservation: true);

        await using (var db = harness.CreateDb())
        {
            var service = harness.CreateService(db);
            var accepted = await service.ReviewAsync(11, accept: true, reason: null, user: "accounting-officer");
            Assert.Equal("ACCEPTED", accepted.DeliveryStatus);
        }

        await using var verify = harness.CreateDb();
        var handoff = await verify.HrAccountingHandoffs.SingleAsync();
        Assert.Equal("ACCEPTED", handoff.DeliveryStatus);
        var transition = await verify.HrAccountingHandoffTransitions.SingleAsync(t => t.ToStatus == "ACCEPTED");
        Assert.Equal("accounting-officer", transition.Actor);
        Assert.Null(transition.Reason);
        // Advance reservation should remain intact for settlement
        var advance = await verify.EmployeeAdvances.SingleAsync();
        Assert.Equal(11L, advance.ReservedByHandoffId);
    }

    [Fact]
    public async Task ReviewAsync_WhenStatusIsAcceptedOrWithdrawn_ThrowsInvalidOperationException()
    {
        using var harness = new WithdrawalHarness();
        await harness.SeedAsync("WITHDRAWN", "CANCELLED");

        await using var db = harness.CreateDb();
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            harness.CreateService(db).ReviewAsync(11, accept: true, reason: null, user: "accounting-user"));
        Assert.Contains("Only a delivered handoff can be reviewed", ex.Message);
    }

    [Fact]
    public async Task GetPayrollHandoffStatusAsync_ReturnsCorrectStatusAndTransitionDetails()
    {
        using var harness = new WithdrawalHarness();
        await harness.SeedAsync("QUEUED", "PENDING");

        await using var db = harness.CreateDb();
        var service = harness.CreateService(db);

        // Missing run returns null
        Assert.Null(await service.GetPayrollHandoffStatusAsync(9999));

        // Existing run returns accurate status
        var status = await service.GetPayrollHandoffStatusAsync(9);
        Assert.NotNull(status);
        Assert.Equal(11L, status.HandoffId);
        Assert.Equal("HR-PAY-3-9-1", status.HandoffReference);
        Assert.Equal(1, status.SourceVersion);
        Assert.Equal("QUEUED", status.DeliveryStatus);
        Assert.Equal("2026-11", status.PayPeriod);
    }

    [Fact]
    public async Task ReviewAsync_ReturnReasonExceeding1000Chars_ThrowsArgumentException()
    {
        using var harness = new WithdrawalHarness();
        await harness.SeedAsync("DELIVERED", "DELIVERED");

        var longReason = new string('R', 1001);
        await using var db = harness.CreateDb();
        var service = harness.CreateService(db);
        await Assert.ThrowsAsync<ArgumentException>(() =>
            service.ReviewAsync(11, accept: false, reason: longReason, user: "accounting-user"));
    }

    [Fact]
    public async Task ReviewAsync_DeliveredLoanHandoff_AcceptsCleanly()
    {
        using var harness = new WithdrawalHarness();
        await using (var db = harness.CreateDb())
        {
            db.HrAccountingHandoffs.Add(new HrAccountingHandoff
            {
                Id = 22,
                CompanyId = 3,
                BranchId = 2,
                SourceType = "LOAN",
                SourceId = "55",
                SourceVersion = 1,
                Operation = "FUND",
                HandoffReference = "HR-LOAN-3-55-1",
                PayloadHash = "loan-hash",
                DeliveryStatus = "DELIVERED",
                CreatedAtUtc = DateTime.UtcNow,
                CreatedBy = "hr-admin",
                Transitions = [new HrAccountingHandoffTransition { FromStatus = "QUEUED", ToStatus = "DELIVERED", Actor = "hr-admin", OccurredAtUtc = DateTime.UtcNow }]
            });
            await db.SaveChangesAsync();
        }

        await using (var db = harness.CreateDb())
        {
            var service = harness.CreateService(db);
            var reviewed = await service.ReviewAsync(22, accept: true, reason: null, user: "loan-officer");
            Assert.Equal("ACCEPTED", reviewed.DeliveryStatus);
        }

        await using var verify = harness.CreateDb();
        var loanHandoff = await verify.HrAccountingHandoffs.SingleAsync(h => h.Id == 22);
        Assert.Equal("ACCEPTED", loanHandoff.DeliveryStatus);
    }

    [Fact]
    public async Task ReviewAsync_DeliveredLoanHandoff_ReturnRejectsCleanly()
    {
        using var harness = new WithdrawalHarness();
        await using (var db = harness.CreateDb())
        {
            db.HrAccountingHandoffs.Add(new HrAccountingHandoff
            {
                Id = 23,
                CompanyId = 3,
                BranchId = 2,
                SourceType = "LOAN",
                SourceId = "56",
                SourceVersion = 1,
                Operation = "FUND",
                HandoffReference = "HR-LOAN-3-56-1",
                PayloadHash = "loan-hash-2",
                DeliveryStatus = "DELIVERED",
                CreatedAtUtc = DateTime.UtcNow,
                CreatedBy = "hr-admin",
                Transitions = [new HrAccountingHandoffTransition { FromStatus = "QUEUED", ToStatus = "DELIVERED", Actor = "hr-admin", OccurredAtUtc = DateTime.UtcNow }]
            });
            await db.SaveChangesAsync();
        }

        await using (var db = harness.CreateDb())
        {
            var service = harness.CreateService(db);
            var reviewed = await service.ReviewAsync(23, accept: false, reason: "Insufficient liquidity", user: "finance-mgr");
            Assert.Equal("RETURNED", reviewed.DeliveryStatus);
        }

        await using var verify = harness.CreateDb();
        var loanHandoff = await verify.HrAccountingHandoffs.SingleAsync(h => h.Id == 23);
        Assert.Equal("RETURNED", loanHandoff.DeliveryStatus);
    }

    [Fact]
    public async Task GetAccountingInboxAsync_FiltersStatusesCorrectlyAndExcludesPosted()
    {
        using var harness = new WithdrawalHarness();
        await using (var db = harness.CreateDb())
        {
            db.HrAccountingHandoffs.AddRange(
                new HrAccountingHandoff
                {
                    Id = 31, CompanyId = 3, BranchId = 2, SourceType = "PAYROLL", SourceId = "101", SourceVersion = 1,
                    Operation = "SUBMIT", HandoffReference = "HR-P-31", PayloadHash = "h31", DeliveryStatus = "DELIVERED",
                    CreatedAtUtc = DateTime.UtcNow.AddMinutes(-10), CreatedBy = "user1",
                    Lines = [new HrAccountingHandoffLine { LineType = "CONTROL:NET_PAY", CurrencyCode = "USD", Amount = 1000m }]
                },
                new HrAccountingHandoff
                {
                    Id = 32, CompanyId = 3, BranchId = 2, SourceType = "PAYROLL", SourceId = "102", SourceVersion = 1,
                    Operation = "SUBMIT", HandoffReference = "HR-P-32", PayloadHash = "h32", DeliveryStatus = "POSTED", // Should be EXCLUDED
                    CreatedAtUtc = DateTime.UtcNow.AddMinutes(-8), CreatedBy = "user2",
                    Lines = [new HrAccountingHandoffLine { LineType = "CONTROL:NET_PAY", CurrencyCode = "USD", Amount = 2000m }]
                },
                new HrAccountingHandoff
                {
                    Id = 33, CompanyId = 3, BranchId = 2, SourceType = "PAYROLL", SourceId = "103", SourceVersion = 1,
                    Operation = "SUBMIT", HandoffReference = "HR-P-33", PayloadHash = "h33", DeliveryStatus = "ACCEPTED",
                    CreatedAtUtc = DateTime.UtcNow.AddMinutes(-5), CreatedBy = "user3",
                    Lines = [new HrAccountingHandoffLine { LineType = "CONTROL:NET_PAY", CurrencyCode = "USD", Amount = 3000m }]
                },
                new HrAccountingHandoff
                {
                    Id = 34, CompanyId = 3, BranchId = 2, SourceType = "PAYROLL", SourceId = "104", SourceVersion = 1,
                    Operation = "SUBMIT", HandoffReference = "HR-P-34", PayloadHash = "h34", DeliveryStatus = "WITHDRAWN",
                    CreatedAtUtc = DateTime.UtcNow.AddMinutes(-2), CreatedBy = "user4",
                    Lines = [new HrAccountingHandoffLine { LineType = "CONTROL:NET_PAY", CurrencyCode = "USD", Amount = 4000m }]
                }
            );
            await db.SaveChangesAsync();
        }

        await using (var db = harness.CreateDb())
        {
            var service = harness.CreateService(db);
            var inbox = await service.GetAccountingInboxAsync();
            var ids = inbox.Select(i => i.HandoffId).ToList();
            Assert.Contains(31L, ids);
            Assert.DoesNotContain(32L, ids); // POSTED is excluded
            Assert.Contains(33L, ids);
            Assert.Contains(34L, ids);
        }
    }

    [Fact]
    public async Task GetPayrollHandoffPaymentStatusAsync_VariousPaymentStates()
    {
        using var harness = new WithdrawalHarness();
        await using (var db = harness.CreateDb())
        {
            db.HrAccountingHandoffs.Add(new HrAccountingHandoff
            {
                Id = 41, CompanyId = 3, BranchId = 2, SourceType = "PAYROLL", SourceId = "701", SourceVersion = 1,
                Operation = "SUBMIT", HandoffReference = "HR-PAY-701", PayloadHash = "hash701", DeliveryStatus = "POSTED",
                CreatedAtUtc = DateTime.UtcNow, CreatedBy = "user",
                Lines = [new HrAccountingHandoffLine { LineType = "CONTROL:NET_PAY", CurrencyCode = "USD", Amount = 500m }]
            });
            await db.SaveChangesAsync();
        }

        await using (var db = harness.CreateDb())
        {
            var service = harness.CreateService(db);
            // 1. When no payments exist -> UNPAID
            var unpaidStatus = await service.GetPayrollHandoffPaymentStatusAsync(701);
            Assert.NotNull(unpaidStatus);
            Assert.Equal("UNPAID", unpaidStatus.PaymentStatus);
            Assert.Equal(500m, unpaidStatus.PayableAmount);
            Assert.Equal(0m, unpaidStatus.PaidAmount);
            Assert.Equal(500m, unpaidStatus.RemainingAmount);
        }

        // Add confirmed payment of 200m -> PARTIAL
        await using (var db = harness.CreateDb())
        {
            db.AccountingHandoffPayments.Add(new AccountingHandoffPayment
            {
                HandoffId = 41, PaymentSequence = 1, CurrencyCode = "USD", Amount = 200m,
                Method = "BANK", Status = "CONFIRMED", IdempotencyKey = "key-part-1",
                CreatedAtUtc = DateTime.UtcNow, CreatedBy = "user"
            });
            await db.SaveChangesAsync();
        }

        await using (var db = harness.CreateDb())
        {
            var service = harness.CreateService(db);
            var partialStatus = await service.GetPayrollHandoffPaymentStatusAsync(701);
            Assert.NotNull(partialStatus);
            Assert.Equal("PARTIAL", partialStatus.PaymentStatus);
            Assert.Equal(200m, partialStatus.PaidAmount);
            Assert.Equal(300m, partialStatus.RemainingAmount);
        }

        // Add confirmed payment of 300m -> PAID
        await using (var db = harness.CreateDb())
        {
            db.AccountingHandoffPayments.Add(new AccountingHandoffPayment
            {
                HandoffId = 41, PaymentSequence = 2, CurrencyCode = "USD", Amount = 300m,
                Method = "BANK", Status = "CONFIRMED", IdempotencyKey = "key-paid-2",
                CreatedAtUtc = DateTime.UtcNow, CreatedBy = "user"
            });
            await db.SaveChangesAsync();
        }

        await using (var db = harness.CreateDb())
        {
            var service = harness.CreateService(db);
            var paidStatus = await service.GetPayrollHandoffPaymentStatusAsync(701);
            Assert.NotNull(paidStatus);
            Assert.Equal("PAID", paidStatus.PaymentStatus);
            Assert.Equal(500m, paidStatus.PaidAmount);
            Assert.Equal(0m, paidStatus.RemainingAmount);
        }
    }

    [Fact]
    public async Task GetAccountingPaymentSummaryAsync_WhenHandoffNotPayroll_ThrowsInvalidOperationException()
    {
        using var harness = new WithdrawalHarness();
        await using (var db = harness.CreateDb())
        {
            db.HrAccountingHandoffs.Add(new HrAccountingHandoff
            {
                Id = 51, CompanyId = 3, BranchId = 2, SourceType = "LOAN", SourceId = "88", SourceVersion = 1,
                Operation = "FUND", HandoffReference = "HR-LOAN-88", PayloadHash = "hash88", DeliveryStatus = "POSTED",
                CreatedAtUtc = DateTime.UtcNow, CreatedBy = "user"
            });
            await db.SaveChangesAsync();
        }

        await using (var db = harness.CreateDb())
        {
            var service = harness.CreateService(db);
            var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
                service.GetAccountingPaymentSummaryAsync(51));
            Assert.Contains("Payment status is available only for payroll submission handoffs", ex.Message);
        }
    }

    [Fact]
    public async Task WithdrawPayrollHandoffAsync_WhenDeliveryStatusReturned_ThrowsInvalidOperationException()
    {
        using var harness = new WithdrawalHarness();
        await harness.SeedAsync("RETURNED", "DELIVERED");

        await using var db = harness.CreateDb();
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            harness.CreateService(db).WithdrawPayrollHandoffAsync(9, "trying to withdraw returned run", "hr-admin"));
        Assert.Contains("Payroll handoff can be withdrawn only before Accounting accepts it", ex.Message);
    }

    [Theory]
    [InlineData("QUEUED")]
    [InlineData("ACCEPTED")]
    [InlineData("RETURNED")]
    [InlineData("POSTED")]
    [InlineData("REVERSED")]
    public async Task ReviewAsync_WhenStatusNotDelivered_ThrowsInvalidOperationException(string nonDeliveredStatus)
    {
        using var harness = new WithdrawalHarness();
        await harness.SeedAsync(nonDeliveredStatus, "DELIVERED");

        await using var db = harness.CreateDb();
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            harness.CreateService(db).ReviewAsync(11, accept: true, reason: null, user: "acct-officer"));
        Assert.Contains("Only a delivered handoff can be reviewed", ex.Message);
    }

    [Fact]
    public async Task WithdrawPayrollHandoffAsync_PropagatesCancellationToken()
    {
        using var harness = new WithdrawalHarness();
        await harness.SeedAsync("QUEUED", "PENDING");

        using var cts = new CancellationTokenSource();
        cts.Cancel();

        await using var db = harness.CreateDb();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            harness.CreateService(db).WithdrawPayrollHandoffAsync(9, "Cancelled reason", "hr-admin", cts.Token));
    }

    [Fact]
    public async Task ReviewAsync_PropagatesCancellationToken()
    {
        using var harness = new WithdrawalHarness();
        await harness.SeedAsync("DELIVERED", "DELIVERED");

        using var cts = new CancellationTokenSource();
        cts.Cancel();

        await using var db = harness.CreateDb();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            harness.CreateService(db).ReviewAsync(11, accept: true, reason: null, user: "acct-user", cts.Token));
    }

    [Fact]
    public async Task GetPayrollHandoffStatusAsync_PropagatesCancellationToken()
    {
        using var harness = new WithdrawalHarness();
        await harness.SeedAsync("QUEUED", "PENDING");

        using var cts = new CancellationTokenSource();
        cts.Cancel();

        await using var db = harness.CreateDb();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            harness.CreateService(db).GetPayrollHandoffStatusAsync(9, cts.Token));
    }

    [Fact]
    public async Task GetAccountingInboxAsync_PropagatesCancellationToken()
    {
        using var harness = new WithdrawalHarness();
        await harness.SeedAsync("DELIVERED", "DELIVERED");

        using var cts = new CancellationTokenSource();
        cts.Cancel();

        await using var db = harness.CreateDb();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            harness.CreateService(db).GetAccountingInboxAsync(cts.Token));
    }

    [Fact]
    public async Task GetPayrollHandoffPaymentStatusAsync_PropagatesCancellationToken()
    {
        using var harness = new WithdrawalHarness();
        await harness.SeedAsync("DELIVERED", "DELIVERED");

        using var cts = new CancellationTokenSource();
        cts.Cancel();

        await using var db = harness.CreateDb();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            harness.CreateService(db).GetPayrollHandoffPaymentStatusAsync(9, cts.Token));
    }

    [Fact]
    public async Task GetAccountingPaymentSummaryAsync_PropagatesCancellationToken()
    {
        using var harness = new WithdrawalHarness();
        await harness.SeedAsync("DELIVERED", "DELIVERED");

        using var cts = new CancellationTokenSource();
        cts.Cancel();

        await using var db = harness.CreateDb();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            harness.CreateService(db).GetAccountingPaymentSummaryAsync(11, cts.Token));
    }

    [Fact]
    public async Task GetAccountingPaymentSummaryAsync_DistinguishesPaymentStatusesAccurately()
    {
        using var harness = new WithdrawalHarness();
        await using (var db = harness.CreateDb())
        {
            db.HrAccountingHandoffs.Add(new HrAccountingHandoff
            {
                Id = 61, CompanyId = 3, BranchId = 2, SourceType = "PAYROLL", SourceId = "801", SourceVersion = 1,
                Operation = "SUBMIT", HandoffReference = "HR-PAY-801", PayloadHash = "hash801", DeliveryStatus = "POSTED",
                CreatedAtUtc = DateTime.UtcNow, CreatedBy = "user",
                Lines = [new HrAccountingHandoffLine { LineType = "CONTROL:NET_PAY", CurrencyCode = "USD", Amount = 1000m }]
            });
            // 1 CONFIRMED: 400m
            db.AccountingHandoffPayments.Add(new AccountingHandoffPayment
            {
                HandoffId = 61, PaymentSequence = 1, CurrencyCode = "USD", Amount = 400m,
                Method = "BANK", Status = "CONFIRMED", IdempotencyKey = "key-conf-1",
                CreatedAtUtc = DateTime.UtcNow, CreatedBy = "user"
            });
            // 1 PREPARED: 200m
            db.AccountingHandoffPayments.Add(new AccountingHandoffPayment
            {
                HandoffId = 61, PaymentSequence = 2, CurrencyCode = "USD", Amount = 200m,
                Method = "BANK", Status = "PREPARED", IdempotencyKey = "key-prep-2",
                CreatedAtUtc = DateTime.UtcNow, CreatedBy = "user"
            });
            // 1 FAILED: 150m (does not count as paid or prepared)
            db.AccountingHandoffPayments.Add(new AccountingHandoffPayment
            {
                HandoffId = 61, PaymentSequence = 3, CurrencyCode = "USD", Amount = 150m,
                Method = "BANK", Status = "FAILED", IdempotencyKey = "key-fail-3",
                CreatedAtUtc = DateTime.UtcNow, CreatedBy = "user"
            });
            // 1 REVERSED: 100m (counts as reversed, not paid)
            db.AccountingHandoffPayments.Add(new AccountingHandoffPayment
            {
                HandoffId = 61, PaymentSequence = 4, CurrencyCode = "USD", Amount = 100m,
                Method = "BANK", Status = "REVERSED", IdempotencyKey = "key-rev-4",
                CreatedAtUtc = DateTime.UtcNow, CreatedBy = "user"
            });
            await db.SaveChangesAsync();
        }

        await using (var db = harness.CreateDb())
        {
            var service = harness.CreateService(db);
            var summary = await service.GetAccountingPaymentSummaryAsync(61);
            Assert.NotNull(summary);
            Assert.Equal(1000m, summary.PayableAmount);
            Assert.Equal(400m, summary.PaidAmount);
            Assert.Equal(200m, summary.PreparedAmount);
            Assert.Equal(100m, summary.ReversedAmount);
            Assert.Equal(400m, summary.RemainingAmount); // 1000 - 400 - 200 = 400
        }
    }

    [Fact]
    public async Task WithdrawPayrollHandoffAsync_WhenWithdrawnWithDifferentUser_ThrowsInvalidOperationException()
    {
        using var harness = new WithdrawalHarness();
        await harness.SeedAsync("QUEUED", "PENDING");

        await using (var db = harness.CreateDb())
        {
            var service = harness.CreateService(db);
            await service.WithdrawPayrollHandoffAsync(9, "Valid withdrawal reason", "user-original");
            var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
                service.WithdrawPayrollHandoffAsync(9, "Valid withdrawal reason", "user-different"));
            Assert.Contains("Payroll handoff has already been withdrawn and cannot be changed", ex.Message);
        }
    }

    [Theory]
    [InlineData("X")]
    [InlineData("   padded reason   ")]
    public async Task WithdrawPayrollHandoffAsync_ReasonLengthBoundaries_Succeeds(string reason)
    {
        using var harness = new WithdrawalHarness();
        await harness.SeedAsync("QUEUED", "PENDING");

        await using (var db = harness.CreateDb())
        {
            var result = await harness.CreateService(db).WithdrawPayrollHandoffAsync(9, reason, "hr-admin");
            Assert.NotNull(result);
            Assert.Equal("WITHDRAWN", result.DeliveryStatus);
        }

        await using var verify = harness.CreateDb();
        var transition = await verify.HrAccountingHandoffTransitions.SingleAsync(t => t.ToStatus == "WITHDRAWN");
        Assert.Equal(reason.Trim(), transition.Reason);
    }

    [Fact]
    public async Task WithdrawPayrollHandoffAsync_MaxReason1000Chars_Succeeds()
    {
        using var harness = new WithdrawalHarness();
        await harness.SeedAsync("QUEUED", "PENDING");

        var maxReason = new string('Z', 1000);
        await using (var db = harness.CreateDb())
        {
            var result = await harness.CreateService(db).WithdrawPayrollHandoffAsync(9, maxReason, "hr-admin");
            Assert.NotNull(result);
            Assert.Equal("WITHDRAWN", result.DeliveryStatus);
        }

        await using var verify = harness.CreateDb();
        var transition = await verify.HrAccountingHandoffTransitions.SingleAsync(t => t.ToStatus == "WITHDRAWN");
        Assert.Equal(maxReason, transition.Reason);
    }

    [Fact]
    public async Task WithdrawPayrollHandoffAsync_WhenSubmitOutboxMessageAlreadyDelivered_WithdrawsAndQueuesWithdrawalMessage()
    {
        using var harness = new WithdrawalHarness();
        await harness.SeedAsync("DELIVERED", "DELIVERED");

        await using (var db = harness.CreateDb())
        {
            var status = await harness.CreateService(db).WithdrawPayrollHandoffAsync(9, "Correction needed", "hr-admin");
            Assert.Equal("WITHDRAWN", status!.DeliveryStatus);
        }

        await using var verify = harness.CreateDb();
        var submitMsg = await verify.HrAccountingOutboxMessages.SingleAsync(m => m.EventType == "PAYROLL_SUBMIT");
        Assert.Equal("DELIVERED", submitMsg.Status);
        var withdrawMsg = await verify.HrAccountingOutboxMessages.SingleAsync(m => m.EventType == "PAYROLL_WITHDRAW");
        Assert.Equal("PENDING", withdrawMsg.Status);
    }

    [Fact]
    public async Task WithdrawPayrollHandoffAsync_ReleasesMultipleLoansAndAdvancesSimultaneously()
    {
        using var harness = new WithdrawalHarness();
        await harness.SeedAsync("QUEUED", "PENDING", includeReservation: true);

        await using (var db = harness.CreateDb())
        {
            db.EmployeeAdvances.Add(new EmployeeAdvance
            {
                Id = 42, EmployeeCode = "E-42", AdvanceAmount = 200m, CurrencyCode = "USD",
                RemainingBalance = 200m, Status = "ACTIVE", ReservedByHandoffId = 11
            });
            db.LoanRepaymentSchedules.AddRange(
                new LoanRepaymentSchedule
                {
                    Id = 201, EmployeeLoanId = 61, InstallmentNo = 1, ScheduledAmount = 150m,
                    PaidAmount = 0m, Status = "PENDING", ReservedByHandoffId = 11
                },
                new LoanRepaymentSchedule
                {
                    Id = 202, EmployeeLoanId = 62, InstallmentNo = 3, ScheduledAmount = 250m,
                    PaidAmount = 0m, Status = "PENDING", ReservedByHandoffId = 11
                }
            );
            await db.SaveChangesAsync();
        }

        await using (var db = harness.CreateDb())
        {
            var status = await harness.CreateService(db).WithdrawPayrollHandoffAsync(9, "release all multi reservations", "hr-admin");
            Assert.Equal("WITHDRAWN", status!.DeliveryStatus);
        }

        await using var verify = harness.CreateDb();
        var advances = await verify.EmployeeAdvances.ToListAsync();
        Assert.All(advances, a => Assert.Null(a.ReservedByHandoffId));
        var schedules = await verify.LoanRepaymentSchedules.ToListAsync();
        Assert.All(schedules, s => Assert.Null(s.ReservedByHandoffId));
    }

    [Theory]
    [InlineData("R")]
    [InlineData("   padded return reason   ")]
    public async Task ReviewAsync_ReturnReasonLengthBoundaries_Succeeds(string reason)
    {
        using var harness = new WithdrawalHarness();
        await harness.SeedAsync("DELIVERED", "DELIVERED");

        await using (var db = harness.CreateDb())
        {
            var reviewed = await harness.CreateService(db).ReviewAsync(11, accept: false, reason: reason, user: "acct-user");
            Assert.Equal("RETURNED", reviewed.DeliveryStatus);
        }

        await using var verify = harness.CreateDb();
        var transition = await verify.HrAccountingHandoffTransitions.SingleAsync(t => t.ToStatus == "RETURNED");
        Assert.Equal(reason.Trim(), transition.Reason);
    }

    [Fact]
    public async Task ReviewAsync_MaxReturnReason1000Chars_Succeeds()
    {
        using var harness = new WithdrawalHarness();
        await harness.SeedAsync("DELIVERED", "DELIVERED");

        var maxReason = new string('M', 1000);
        await using (var db = harness.CreateDb())
        {
            var reviewed = await harness.CreateService(db).ReviewAsync(11, accept: false, reason: maxReason, user: "acct-user");
            Assert.Equal("RETURNED", reviewed.DeliveryStatus);
        }

        await using var verify = harness.CreateDb();
        var transition = await verify.HrAccountingHandoffTransitions.SingleAsync(t => t.ToStatus == "RETURNED");
        Assert.Equal(maxReason, transition.Reason);
    }

    [Fact]
    public async Task ReviewAsync_WhenAcceptingWithReason_IgnoresReasonAndStoresNull()
    {
        using var harness = new WithdrawalHarness();
        await harness.SeedAsync("DELIVERED", "DELIVERED");

        await using (var db = harness.CreateDb())
        {
            var accepted = await harness.CreateService(db).ReviewAsync(11, accept: true, reason: "this note is ignored", user: "acct-officer");
            Assert.Equal("ACCEPTED", accepted.DeliveryStatus);
        }

        await using var verify = harness.CreateDb();
        var transition = await verify.HrAccountingHandoffTransitions.SingleAsync(t => t.ToStatus == "ACCEPTED");
        Assert.Null(transition.Reason);
    }

    [Fact]
    public async Task GetAccountingPaymentSummaryAsync_WhenBranchIdIsNull_ThrowsInvalidOperationException()
    {
        using var harness = new WithdrawalHarness();
        await using (var db = harness.CreateDb())
        {
            db.HrAccountingHandoffs.Add(new HrAccountingHandoff
            {
                Id = 71, CompanyId = 3, BranchId = null, SourceType = "PAYROLL", SourceId = "901", SourceVersion = 1,
                Operation = "SUBMIT", HandoffReference = "HR-P-71", PayloadHash = "h71", DeliveryStatus = "POSTED",
                CreatedAtUtc = DateTime.UtcNow, CreatedBy = "user",
                Lines = [new HrAccountingHandoffLine { LineType = "CONTROL:NET_PAY", CurrencyCode = "USD", Amount = 100m }]
            });
            await db.SaveChangesAsync();
        }

        await using (var db = harness.CreateDb())
        {
            var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
                harness.CreateService(db).GetAccountingPaymentSummaryAsync(71));
            Assert.Contains("Payroll handoff has no branch", ex.Message);
        }
    }

    [Fact]
    public async Task GetAccountingPaymentSummaryAsync_WhenNoNetPayLine_ThrowsInvalidOperationException()
    {
        using var harness = new WithdrawalHarness();
        await using (var db = harness.CreateDb())
        {
            db.HrAccountingHandoffs.Add(new HrAccountingHandoff
            {
                Id = 72, CompanyId = 3, BranchId = 2, SourceType = "PAYROLL", SourceId = "902", SourceVersion = 1,
                Operation = "SUBMIT", HandoffReference = "HR-P-72", PayloadHash = "h72", DeliveryStatus = "POSTED",
                CreatedAtUtc = DateTime.UtcNow, CreatedBy = "user",
                Lines = [new HrAccountingHandoffLine { LineType = "CONTROL:GROSS", CurrencyCode = "USD", Amount = 100m }]
            });
            await db.SaveChangesAsync();
        }

        await using (var db = harness.CreateDb())
        {
            var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
                harness.CreateService(db).GetAccountingPaymentSummaryAsync(72));
            Assert.Contains("Payroll handoff has no single-currency net-pay amount", ex.Message);
        }
    }

    [Fact]
    public async Task GetAccountingPaymentSummaryAsync_WhenMultipleDistinctCurrenciesInNetPayLines_ThrowsInvalidOperationException()
    {
        using var harness = new WithdrawalHarness();
        await using (var db = harness.CreateDb())
        {
            db.HrAccountingHandoffs.Add(new HrAccountingHandoff
            {
                Id = 73, CompanyId = 3, BranchId = 2, SourceType = "PAYROLL", SourceId = "903", SourceVersion = 1,
                Operation = "SUBMIT", HandoffReference = "HR-P-73", PayloadHash = "h73", DeliveryStatus = "POSTED",
                CreatedAtUtc = DateTime.UtcNow, CreatedBy = "user",
                Lines = [
                    new HrAccountingHandoffLine { LineType = "CONTROL:NET_PAY", CurrencyCode = "USD", Amount = 100m },
                    new HrAccountingHandoffLine { LineType = "CONTROL:NET_PAY", CurrencyCode = "EUR", Amount = 200m }
                ]
            });
            await db.SaveChangesAsync();
        }

        await using (var db = harness.CreateDb())
        {
            var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
                harness.CreateService(db).GetAccountingPaymentSummaryAsync(73));
            Assert.Contains("Payroll handoff has no single-currency net-pay amount", ex.Message);
        }
    }

    [Fact]
    public async Task GetAccountingPaymentSummaryAsync_WhenPaymentCurrencyMismatchesHandoff_ThrowsInvalidOperationException()
    {
        using var harness = new WithdrawalHarness();
        await using (var db = harness.CreateDb())
        {
            db.HrAccountingHandoffs.Add(new HrAccountingHandoff
            {
                Id = 74, CompanyId = 3, BranchId = 2, SourceType = "PAYROLL", SourceId = "904", SourceVersion = 1,
                Operation = "SUBMIT", HandoffReference = "HR-P-74", PayloadHash = "h74", DeliveryStatus = "POSTED",
                CreatedAtUtc = DateTime.UtcNow, CreatedBy = "user",
                Lines = [new HrAccountingHandoffLine { LineType = "CONTROL:NET_PAY", CurrencyCode = "USD", Amount = 500m }]
            });
            db.AccountingHandoffPayments.Add(new AccountingHandoffPayment
            {
                HandoffId = 74, PaymentSequence = 1, CurrencyCode = "EUR", Amount = 100m,
                Method = "BANK", Status = "PREPARED", IdempotencyKey = "key-mismatch-curr",
                CreatedAtUtc = DateTime.UtcNow, CreatedBy = "user"
            });
            await db.SaveChangesAsync();
        }

        await using (var db = harness.CreateDb())
        {
            var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
                harness.CreateService(db).GetAccountingPaymentSummaryAsync(74));
            Assert.Contains("Payment history contains a currency that does not match the payroll handoff", ex.Message);
        }
    }

    [Fact]
    public async Task GetAccountingPaymentSummaryAsync_WhenMultiplePaymentVouchersLinkedToSinglePayment_ThrowsInvalidOperationException()
    {
        using var harness = new WithdrawalHarness();
        await using (var db = harness.CreateDb())
        {
            db.HrAccountingHandoffs.Add(new HrAccountingHandoff
            {
                Id = 75, CompanyId = 3, BranchId = 2, SourceType = "PAYROLL", SourceId = "905", SourceVersion = 1,
                Operation = "SUBMIT", HandoffReference = "HR-P-75", PayloadHash = "h75", DeliveryStatus = "POSTED",
                CreatedAtUtc = DateTime.UtcNow, CreatedBy = "user",
                Lines = [new HrAccountingHandoffLine { LineType = "CONTROL:NET_PAY", CurrencyCode = "USD", Amount = 500m }]
            });
            db.AccountingHandoffPayments.Add(new AccountingHandoffPayment
            {
                Id = 991, HandoffId = 75, PaymentSequence = 1, CurrencyCode = "USD", Amount = 100m,
                Method = "BANK", Status = "PREPARED", IdempotencyKey = "key-multi-pv",
                CreatedAtUtc = DateTime.UtcNow, CreatedBy = "user"
            });
            await db.SaveChangesAsync();
        }

        harness.GlVouchersMock
            .Setup(v => v.GetBySourceAsync(2, "HR_PAYMENT", 991, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<GlVoucherHeader>
            {
                new() { Id = 301, BranchId = 2 },
                new() { Id = 302, BranchId = 2 }
            });

        await using (var db = harness.CreateDb())
        {
            var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
                harness.CreateService(db).GetAccountingPaymentSummaryAsync(75));
            Assert.Contains("More than one Payment Voucher is linked to a payment instruction", ex.Message);
        }
    }

    [Fact]
    public async Task GetAccountingPaymentSummaryAsync_WhenHandoffNotFound_ReturnsNull()
    {
        using var harness = new WithdrawalHarness();
        await harness.SeedAsync("DELIVERED", "DELIVERED");

        await using (var db = harness.CreateDb())
        {
            var result = await harness.CreateService(db).GetAccountingPaymentSummaryAsync(99999);
            Assert.Null(result);
        }
    }

    [Fact]
    public async Task GetPayrollHandoffPaymentStatusAsync_WhenHandoffNotFound_ReturnsNull()
    {
        using var harness = new WithdrawalHarness();
        await harness.SeedAsync("DELIVERED", "DELIVERED");

        await using (var db = harness.CreateDb())
        {
            var result = await harness.CreateService(db).GetPayrollHandoffPaymentStatusAsync(99999);
            Assert.Null(result);
        }
    }

    [Fact]
    public async Task GetPayrollHandoffPaymentStatusAsync_WhenOnlyReversedPaymentsExist_ReturnsReversedStatus()
    {
        using var harness = new WithdrawalHarness();
        await using (var db = harness.CreateDb())
        {
            db.HrAccountingHandoffs.Add(new HrAccountingHandoff
            {
                Id = 81, CompanyId = 3, BranchId = 2, SourceType = "PAYROLL", SourceId = "990", SourceVersion = 1,
                Operation = "SUBMIT", HandoffReference = "HR-P-81", PayloadHash = "h81", DeliveryStatus = "POSTED",
                CreatedAtUtc = DateTime.UtcNow, CreatedBy = "user",
                Lines = [new HrAccountingHandoffLine { LineType = "CONTROL:NET_PAY", CurrencyCode = "USD", Amount = 500m }]
            });
            db.AccountingHandoffPayments.Add(new AccountingHandoffPayment
            {
                HandoffId = 81, PaymentSequence = 1, CurrencyCode = "USD", Amount = 100m,
                Method = "BANK", Status = "REVERSED", IdempotencyKey = "key-only-rev",
                CreatedAtUtc = DateTime.UtcNow, CreatedBy = "user"
            });
            await db.SaveChangesAsync();
        }

        await using (var db = harness.CreateDb())
        {
            var status = await harness.CreateService(db).GetPayrollHandoffPaymentStatusAsync(990);
            Assert.NotNull(status);
            Assert.Equal("REVERSED", status.PaymentStatus);
            Assert.Equal(0m, status.PaidAmount);
            Assert.Equal(100m, status.ReversedAmount);
            Assert.Equal(500m, status.RemainingAmount);
        }
    }

    private sealed class WithdrawalHarness : IDisposable
    {
        private readonly string storeName = Guid.NewGuid().ToString();
        private readonly DbContextOptions<OracleDbContext> options;
        public Mock<IGlVoucherRepository> GlVouchersMock { get; } = new();

        public WithdrawalHarness()
        {
            options = new DbContextOptionsBuilder<OracleDbContext>()
                .UseInMemoryDatabase(storeName)
                .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
                .Options;

            GlVouchersMock
                .Setup(v => v.GetBySourceAsync(It.IsAny<long>(), It.IsAny<string>(), It.IsAny<long>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new List<GlVoucherHeader>());
        }

        public OracleDbContext CreateDb() => new(options);

        public HrAccountingHandoffService CreateService(OracleDbContext db) => new(
            db,
            GlVouchersMock.Object,
            Mock.Of<IHrPayrollPostingService>(),
            Mock.Of<IReceiptPaymentVoucherService>(),
            Mock.Of<IGlVoucherService>());

        public async Task SeedAsync(string deliveryStatus, string submitStatus, bool includeReservation = false)
        {
            await using var db = CreateDb();
            var handoff = new HrAccountingHandoff
            {
                Id = 11,
                CompanyId = 3,
                BranchId = 2,
                SourceType = "PAYROLL",
                SourceId = "9",
                SourceVersion = 1,
                Operation = "SUBMIT",
                PayPeriod = "2026-11",
                HandoffReference = "HR-PAY-3-9-1",
                PayloadHash = "payload-hash",
                DeliveryStatus = deliveryStatus,
                CreatedAtUtc = new DateTime(2026, 11, 7, 9, 0, 0, DateTimeKind.Utc),
                CreatedBy = "hr-admin",
                Transitions = [new HrAccountingHandoffTransition { FromStatus = "", ToStatus = deliveryStatus, Actor = "hr-admin", OccurredAtUtc = DateTime.UtcNow }],
                OutboxMessages = [new HrAccountingOutboxMessage { EventKey = "submit-event-001", EventType = "PAYROLL_SUBMIT", PayloadHash = "payload-hash", Status = submitStatus, CreatedAtUtc = DateTime.UtcNow }]
            };
            db.HrAccountingHandoffs.Add(handoff);
            if (includeReservation)
                db.EmployeeAdvances.Add(new EmployeeAdvance { Id = 41, EmployeeCode = "E-41", AdvanceAmount = 100m, CurrencyCode = "USD", RemainingBalance = 100m, Status = "ACTIVE", ReservedByHandoffId = 11 });
            await db.SaveChangesAsync();
        }

        public void Dispose() { }
    }
}
