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

public sealed class AccountingHandoffPostingReceiptTests
{
    [Fact]
    public async Task ReplayingTheSamePostedVoucherEventIsIdempotent()
    {
        using var harness = new PostingReceiptHarness();
        await harness.SeedAsync();

        await using (var db = harness.CreateDb())
        {
            var service = harness.CreateService(db);
            await service.ConfirmJournalPostedAsync(11, 7, "posting-event-001", "accounting-user");
            await service.ConfirmJournalPostedAsync(11, 7, "posting-event-001", "accounting-user");
        }

        await using var verify = harness.CreateDb();
        Assert.Equal("POSTED", (await verify.HrAccountingHandoffs.SingleAsync()).DeliveryStatus);
        Assert.Single(await verify.AccountingHandoffPostingReceipts.ToListAsync());
        Assert.Single(await verify.HrAccountingHandoffTransitions.ToListAsync());
    }

    [Fact]
    public async Task ConfirmJournalPosted_LocksSourceAndLoanSchedulePeriodsBeforeSettlingReservations()
    {
        using var harness = new PostingReceiptHarness();
        await harness.SeedPayrollSubmitWithLoanScheduleAsync();
        var unitOfWork = new Mock<IHrUnitOfWork>();
        unitOfWork.Setup(u => u.ExecuteForPayrollPeriodsAsync(3, "2026-11",
                It.Is<IReadOnlyCollection<string>>(periods => periods.Contains("2026-11") && periods.Contains("2026-12")),
                It.IsAny<Func<Task<bool>>>(), It.IsAny<CancellationToken>()))
            .Returns((long _, string _, IReadOnlyCollection<string> _, Func<Task<bool>> action, CancellationToken _) => action());

        await using (var db = harness.CreateDb())
        {
            await harness.CreateService(db, unitOfWork.Object).ConfirmJournalPostedAsync(11, 7, "posting-event-001", "accounting-user");
        }

        await using var verify = harness.CreateDb();
        unitOfWork.Verify(u => u.ExecuteForPayrollPeriodsAsync(3, "2026-11",
            It.Is<IReadOnlyCollection<string>>(periods => periods.Contains("2026-11") && periods.Contains("2026-12")),
            It.IsAny<Func<Task<bool>>>(), It.IsAny<CancellationToken>()), Times.Once);
        var loan = await verify.EmployeeLoans.SingleAsync(l => l.Id == 41);
        Assert.Equal("COMPLETED", loan.Status);
        Assert.Equal(0m, loan.RemainingBalance);
        Assert.Equal("PAID", (await verify.LoanRepaymentSchedules.SingleAsync(s => s.Id == 151)).Status);
    }

    [Theory]
    [InlineData("short")] // < 8 characters
    [InlineData("")]
    [InlineData(null)]
    public async Task ConfirmJournalPosted_EventKeyInvalid_ThrowsArgumentException(string? eventKey)
    {
        using var harness = new PostingReceiptHarness();
        await harness.SeedAsync();

        await using var db = harness.CreateDb();
        var service = harness.CreateService(db);

        await Assert.ThrowsAsync<ArgumentException>(() =>
            service.ConfirmJournalPostedAsync(11, 7, eventKey!, "accounting-user"));
    }

    [Fact]
    public async Task ConfirmJournalPosted_HandoffNotFound_ThrowsKeyNotFoundException()
    {
        using var harness = new PostingReceiptHarness();
        await harness.SeedAsync();

        await using var db = harness.CreateDb();
        var service = harness.CreateService(db);

        var ex = await Assert.ThrowsAsync<KeyNotFoundException>(() =>
            service.ConfirmJournalPostedAsync(999, 7, "posting-event-001", "accounting-user"));

        Assert.Contains("Handoff not found", ex.Message);
    }

    [Fact]
    public async Task ConfirmJournalPosted_VoucherNotFound_ThrowsKeyNotFoundException()
    {
        using var harness = new PostingReceiptHarness();
        await harness.SeedAsync();

        await using var db = harness.CreateDb();
        var service = harness.CreateService(db);

        var ex = await Assert.ThrowsAsync<KeyNotFoundException>(() =>
            service.ConfirmJournalPostedAsync(11, 9999, "posting-event-001", "accounting-user"));

        Assert.Contains("Journal voucher was not found", ex.Message);
    }

    [Fact]
    public async Task ConfirmJournalPosted_VoucherNotLinkedToHandoff_ThrowsInvalidOperationException()
    {
        using var harness = new PostingReceiptHarness();
        await harness.SeedAsync();

        // Create voucher linked to a different handoff
        harness.SetupVoucher(100, branchId: 2, sourceRefId: 999, status: 3, postDate: DateTime.UtcNow);

        await using var db = harness.CreateDb();
        var service = harness.CreateService(db);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.ConfirmJournalPostedAsync(11, 100, "posting-event-001", "accounting-user"));

        Assert.Contains("not linked to this handoff", ex.Message);
    }

    [Fact]
    public async Task ConfirmJournalPosted_VoucherNotPostedStatus_ThrowsInvalidOperationException()
    {
        using var harness = new PostingReceiptHarness();
        await harness.SeedAsync();

        // Voucher has status 1 (Draft), not 3 (Posted)
        harness.SetupVoucher(101, branchId: 2, sourceRefId: 11, status: 1, postDate: null);

        await using var db = harness.CreateDb();
        var service = harness.CreateService(db);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.ConfirmJournalPostedAsync(11, 101, "posting-event-001", "accounting-user"));

        Assert.Contains("Only an ERP-posted journal voucher can produce a posting receipt", ex.Message);
    }

    [Fact]
    public async Task ConfirmJournalPosted_HandoffNotAcceptedStatus_ThrowsInvalidOperationException()
    {
        using var harness = new PostingReceiptHarness();
        await harness.SeedAsync();

        await using (var db = harness.CreateDb())
        {
            var handoff = await db.HrAccountingHandoffs.SingleAsync(h => h.Id == 11);
            handoff.DeliveryStatus = "QUEUED"; // Not ACCEPTED
            await db.SaveChangesAsync();
        }

        await using (var db = harness.CreateDb())
        {
            var service = harness.CreateService(db);
            var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
                service.ConfirmJournalPostedAsync(11, 7, "posting-event-001", "accounting-user"));

            Assert.Contains("Only an accepted handoff can receive its posting receipt", ex.Message);
        }
    }

    [Fact]
    public async Task SameHandoffCannotAcceptDifferentPostedVoucherForExistingReceipt()
    {
        using var harness = new PostingReceiptHarness();
        await harness.SeedAsync();

        await using (var db = harness.CreateDb())
        {
            var service = harness.CreateService(db);
            await service.ConfirmJournalPostedAsync(11, 7, "posting-event-001", "accounting-user");
            await Assert.ThrowsAsync<InvalidOperationException>(() =>
                service.ConfirmJournalPostedAsync(11, 8, "posting-event-001", "accounting-user"));
        }

        await using var verify = harness.CreateDb();
        Assert.Equal("POSTED", (await verify.HrAccountingHandoffs.SingleAsync()).DeliveryStatus);
        Assert.Single(await verify.AccountingHandoffPostingReceipts.ToListAsync());
        Assert.Single(await verify.HrAccountingHandoffTransitions.ToListAsync());
    }

    [Fact]
    public async Task ConfirmJournalPosted_EventKeyExceeds160Chars_ThrowsArgumentException()
    {
        using var harness = new PostingReceiptHarness();
        await harness.SeedAsync();

        var longEventKey = new string('k', 161);
        await using var db = harness.CreateDb();
        var service = harness.CreateService(db);

        var ex = await Assert.ThrowsAsync<ArgumentException>(() =>
            service.ConfirmJournalPostedAsync(11, 7, longEventKey, "accounting-user"));
        Assert.Contains("unique posting receipt event key is required", ex.Message);
    }

    [Fact]
    public async Task ConfirmJournalPosted_EventKeyReusedForDifferentHandoff_ThrowsInvalidOperationException()
    {
        using var harness = new PostingReceiptHarness();
        await harness.SeedAsync();

        await using (var seedDb = harness.CreateDb())
        {
            seedDb.AccountingHandoffPostingReceipts.Add(new AccountingHandoffPostingReceipt
            {
                Id = 99,
                HandoffId = 999, // Different handoff
                VoucherId = 7,
                EventKey = "posting-event-001", // Reused event key
                PayloadHash = "prior-hash",
                ReceivedAtUtc = DateTime.UtcNow,
                ReceivedBy = "other-user"
            });
            await seedDb.SaveChangesAsync();
        }

        await using var db = harness.CreateDb();
        var service = harness.CreateService(db);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.ConfirmJournalPostedAsync(11, 7, "posting-event-001", "accounting-user"));
        Assert.Contains("conflicting posting receipt already exists", ex.Message);
    }

    [Fact]
    public async Task ConfirmJournalPosted_VoucherMissingPostDate_ThrowsInvalidOperationException()
    {
        using var harness = new PostingReceiptHarness();
        await harness.SeedAsync();

        // Status 3 but PostDate is null
        harness.SetupVoucher(102, branchId: 2, sourceRefId: 11, status: 3, postDate: null);

        await using var db = harness.CreateDb();
        var service = harness.CreateService(db);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.ConfirmJournalPostedAsync(11, 102, "posting-event-001", "accounting-user"));
        Assert.Contains("Only an ERP-posted journal voucher can produce a posting receipt", ex.Message);
    }

    [Fact]
    public async Task ConfirmJournalPosted_VoucherBranchMismatch_ThrowsInvalidOperationException()
    {
        using var harness = new PostingReceiptHarness();
        await harness.SeedAsync();

        // BranchId 99 mismatches handoff branch 2
        harness.SetupVoucher(103, branchId: 99, sourceRefId: 11, status: 3, postDate: DateTime.UtcNow);

        await using var db = harness.CreateDb();
        var service = harness.CreateService(db);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.ConfirmJournalPostedAsync(11, 103, "posting-event-001", "accounting-user"));
        Assert.Contains("Journal voucher is not linked to this handoff", ex.Message);
    }

    [Fact]
    public async Task ConfirmJournalPosted_SetsAuditFieldsAndReceiptDetailsCorrectly()
    {
        using var harness = new PostingReceiptHarness();
        await harness.SeedAsync();

        var beforeCall = DateTime.UtcNow;
        await using (var db = harness.CreateDb())
        {
            var service = harness.CreateService(db);
            await service.ConfirmJournalPostedAsync(11, 7, "posting-event-verified", "audit-actor");
        }

        await using var verify = harness.CreateDb();
        var handoff = await verify.HrAccountingHandoffs.Include(h => h.Transitions).SingleAsync();
        Assert.Equal("POSTED", handoff.DeliveryStatus);

        var transition = Assert.Single(handoff.Transitions);
        Assert.Equal("ACCEPTED", transition.FromStatus);
        Assert.Equal("POSTED", transition.ToStatus);
        Assert.Equal("audit-actor", transition.Actor);
        Assert.True(transition.OccurredAtUtc >= beforeCall);

        var receipt = Assert.Single(await verify.AccountingHandoffPostingReceipts.ToListAsync());
        Assert.Equal(11, receipt.HandoffId);
        Assert.Equal(7, receipt.VoucherId);
        Assert.Equal("posting-event-verified", receipt.EventKey);
        Assert.Equal("audit-actor", receipt.ReceivedBy);
        Assert.True(receipt.ReceivedAtUtc >= beforeCall);
    }

    [Theory]
    [InlineData("QUEUED")]
    [InlineData("DELIVERED")]
    [InlineData("POSTED")]
    [InlineData("WITHDRAWN")]
    [InlineData("REVERSED")]
    public async Task ConfirmJournalPosted_DeliveryStatusNotAccepted_Theory(string status)
    {
        using var harness = new PostingReceiptHarness();
        await harness.SeedAsync();

        await using (var setupDb = harness.CreateDb())
        {
            var handoff = await setupDb.HrAccountingHandoffs.SingleAsync(h => h.Id == 11);
            handoff.DeliveryStatus = status;
            await setupDb.SaveChangesAsync();
        }

        await using var db = harness.CreateDb();
        var service = harness.CreateService(db);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.ConfirmJournalPostedAsync(11, 7, "posting-event-status", "accounting-user"));
        Assert.Contains("Only an accepted handoff can receive its posting receipt", ex.Message);
    }

    [Fact]
    public async Task ConfirmJournalPosted_PropagatesCancellationToken()
    {
        using var harness = new PostingReceiptHarness();
        await harness.SeedAsync();

        using var cts = new CancellationTokenSource();
        cts.Cancel();

        await using var db = harness.CreateDb();
        var service = harness.CreateService(db);

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            service.ConfirmJournalPostedAsync(11, 7, "posting-event-canceled", "accounting-user", cts.Token));
    }

    [Fact]
    public async Task ConfirmJournalPosted_TrimsEventKeyWhitespace()
    {
        using var harness = new PostingReceiptHarness();
        await harness.SeedAsync();

        await using (var db = harness.CreateDb())
        {
            var service = harness.CreateService(db);
            await service.ConfirmJournalPostedAsync(11, 7, "   posting-event-trimmed   ", "accounting-user");
        }

        await using var verify = harness.CreateDb();
        var receipt = await verify.AccountingHandoffPostingReceipts.SingleAsync();
        Assert.Equal("posting-event-trimmed", receipt.EventKey);
    }

    [Fact]
    public async Task ConfirmJournalPosted_ForPayrollSubmit_WhenSourceIdInvalid_ThrowsInvalidOperationException()
    {
        using var harness = new PostingReceiptHarness();
        await harness.SeedPayrollSubmitAsync(22, sourceId: "NOT_A_NUMBER");
        harness.SetupVoucher(20, branchId: 2, sourceRefId: 22, status: 3, postDate: DateTime.UtcNow);

        await using var db = harness.CreateDb();
        var service = harness.CreateService(db);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.ConfirmJournalPostedAsync(22, 20, "event-invalid-src", "accounting-user"));
        Assert.Contains("Payroll handoff source ID is invalid", ex.Message);
    }

    [Fact]
    public async Task ConfirmJournalPosted_ForPayrollSubmit_WhenMalformedReservationReference_ThrowsInvalidOperationException()
    {
        using var harness = new PostingReceiptHarness();
        await harness.SeedPayrollSubmitAsync(23, sourceId: "100");
        harness.SetupVoucher(21, branchId: 2, sourceRefId: 23, status: 3, postDate: DateTime.UtcNow);

        await using (var db = harness.CreateDb())
        {
            var handoff = await db.HrAccountingHandoffs.SingleAsync(h => h.Id == 23);
            handoff.Lines.Add(new HrAccountingHandoffLine
            {
                HandoffId = 23,
                LineType = "REFERENCE:SCHEDULE",
                SourceReference = "BAD_FORMAT", // Missing 4 parts with PAYROLL_LINE
                Amount = 100m,
                CurrencyCode = "JOD"
            });
            await db.SaveChangesAsync();
        }

        await using (var db = harness.CreateDb())
        {
            var service = harness.CreateService(db);
            var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
                service.ConfirmJournalPostedAsync(23, 21, "event-malformed-ref", "accounting-user"));
            Assert.Contains("Payroll reservation reference is malformed", ex.Message);
        }
    }

    [Fact]
    public async Task ConfirmJournalPosted_ForPayrollSubmit_WhenAdvanceMissingOrNotReservedByHandoff_ThrowsInvalidOperationException()
    {
        using var harness = new PostingReceiptHarness();
        await harness.SeedPayrollSubmitAsync(24, sourceId: "100");
        harness.SetupVoucher(22, branchId: 2, sourceRefId: 24, status: 3, postDate: DateTime.UtcNow);

        await using (var db = harness.CreateDb())
        {
            var handoff = await db.HrAccountingHandoffs.SingleAsync(h => h.Id == 24);
            handoff.Lines.Add(new HrAccountingHandoffLine
            {
                HandoffId = 24,
                LineType = "REFERENCE:ADVANCE",
                SourceReference = "ADVANCE:999:PAYROLL_LINE:10",
                Amount = 100m,
                CurrencyCode = "JOD"
            });
            await db.SaveChangesAsync();
        }

        await using (var db = harness.CreateDb())
        {
            var service = harness.CreateService(db);
            var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
                service.ConfirmJournalPostedAsync(24, 22, "event-missing-adv", "accounting-user"));
            Assert.Contains("A payroll reservation is missing or no longer belongs to this handoff", ex.Message);
        }
    }

    [Fact]
    public async Task ConfirmJournalPosted_ForPayrollSubmit_WhenAdvanceChangedBeforeReceipt_ThrowsInvalidOperationException()
    {
        using var harness = new PostingReceiptHarness();
        await harness.SeedPayrollSubmitAsync(25, sourceId: "100");
        harness.SetupVoucher(23, branchId: 2, sourceRefId: 25, status: 3, postDate: DateTime.UtcNow);

        await using (var db = harness.CreateDb())
        {
            var handoff = await db.HrAccountingHandoffs.SingleAsync(h => h.Id == 25);
            handoff.Lines.Add(new HrAccountingHandoffLine
            {
                HandoffId = 25,
                LineType = "REFERENCE:ADVANCE",
                SourceReference = "ADVANCE:501:PAYROLL_LINE:1",
                Amount = 150m,
                CurrencyCode = "JOD"
            });
            db.EmployeeAdvances.Add(new EmployeeAdvance
            {
                Id = 501,
                EmployeeCode = "EMP_ADV",
                Status = "ACTIVE",
                AdvanceAmount = 200m,
                RemainingBalance = 100m, // Less than 150m!
                CurrencyCode = "JOD",
                ReservedByHandoffId = 25
            });
            await db.SaveChangesAsync();
        }

        await using (var db = harness.CreateDb())
        {
            var service = harness.CreateService(db);
            var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
                service.ConfirmJournalPostedAsync(25, 23, "event-adv-changed", "accounting-user"));
            Assert.Contains("Reserved advance 501 changed before posting receipt", ex.Message);
        }
    }

    [Fact]
    public async Task ConfirmJournalPosted_ForPayrollSubmit_WhenAdvanceCurrencyMismatches_ThrowsInvalidOperationException()
    {
        using var harness = new PostingReceiptHarness();
        await harness.SeedPayrollSubmitAsync(26, sourceId: "100");
        harness.SetupVoucher(24, branchId: 2, sourceRefId: 26, status: 3, postDate: DateTime.UtcNow);

        await using (var db = harness.CreateDb())
        {
            var handoff = await db.HrAccountingHandoffs.SingleAsync(h => h.Id == 26);
            handoff.Lines.Add(new HrAccountingHandoffLine
            {
                HandoffId = 26,
                LineType = "REFERENCE:ADVANCE",
                SourceReference = "ADVANCE:502:PAYROLL_LINE:1",
                Amount = 100m,
                CurrencyCode = "JOD"
            });
            db.EmployeeAdvances.Add(new EmployeeAdvance
            {
                Id = 502,
                EmployeeCode = "EMP_ADV",
                Status = "ACTIVE",
                AdvanceAmount = 100m,
                RemainingBalance = 100m,
                CurrencyCode = "USD", // Mismatched currency
                ReservedByHandoffId = 26
            });
            await db.SaveChangesAsync();
        }

        await using (var db = harness.CreateDb())
        {
            var service = harness.CreateService(db);
            var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
                service.ConfirmJournalPostedAsync(26, 24, "event-adv-curr-mismatch", "accounting-user"));
            Assert.Contains("Reserved advance 502 changed before posting receipt", ex.Message);
        }
    }

    [Fact]
    public async Task ConfirmJournalPosted_ForPayrollSubmit_WhenLoanScheduleChangedBeforeReceipt_ThrowsInvalidOperationException()
    {
        using var harness = new PostingReceiptHarness();
        await harness.SeedPayrollSubmitAsync(27, sourceId: "100");
        harness.SetupVoucher(25, branchId: 2, sourceRefId: 27, status: 3, postDate: DateTime.UtcNow);

        await using (var db = harness.CreateDb())
        {
            var handoff = await db.HrAccountingHandoffs.SingleAsync(h => h.Id == 27);
            handoff.Lines.Add(new HrAccountingHandoffLine
            {
                HandoffId = 27,
                LineType = "REFERENCE:SCHEDULE",
                SourceReference = "SCHEDULE:601:PAYROLL_LINE:1",
                Amount = 200m,
                CurrencyCode = "JOD"
            });
            var loan = new EmployeeLoan { Id = 801, Status = "PENDING", CurrencyCode = "JOD" }; // Loan not ACTIVE
            db.LoanRepaymentSchedules.Add(new LoanRepaymentSchedule
            {
                Id = 601,
                EmployeeLoanId = 801,
                EmployeeLoan = loan,
                ScheduledAmount = 200m,
                PaidAmount = 0m,
                ReservedByHandoffId = 27
            });
            await db.SaveChangesAsync();
        }

        await using (var db = harness.CreateDb())
        {
            var service = harness.CreateService(db);
            var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
                service.ConfirmJournalPostedAsync(27, 25, "event-sch-loan-not-active", "accounting-user"));
            Assert.Contains("Reserved loan schedule 601 changed before posting receipt", ex.Message);
        }
    }

    [Fact]
    public async Task ConfirmJournalPosted_ForPayrollSubmit_SettlesAdvancesAndSchedules_AndGeneratesSettlementSnapshot()
    {
        using var harness = new PostingReceiptHarness();
        await harness.SeedPayrollSubmitAsync(28, sourceId: "100");
        harness.SetupVoucher(26, branchId: 2, sourceRefId: 28, status: 3, postDate: DateTime.UtcNow);

        await using (var db = harness.CreateDb())
        {
            var handoff = await db.HrAccountingHandoffs.SingleAsync(h => h.Id == 28);
            handoff.Lines.Add(new HrAccountingHandoffLine
            {
                HandoffId = 28,
                LineType = "REFERENCE:ADVANCE",
                SourceReference = "ADVANCE:503:PAYROLL_LINE:1",
                Amount = 100m,
                CurrencyCode = "JOD"
            });
            handoff.Lines.Add(new HrAccountingHandoffLine
            {
                HandoffId = 28,
                LineType = "REFERENCE:SCHEDULE",
                SourceReference = "SCHEDULE:602:PAYROLL_LINE:1",
                Amount = 150m,
                CurrencyCode = "JOD"
            });

            db.EmployeeAdvances.Add(new EmployeeAdvance
            {
                Id = 503,
                EmployeeCode = "EMP_ADV",
                Status = "ACTIVE",
                AdvanceAmount = 100m,
                RemainingBalance = 100m,
                DeductedAmount = 0m,
                CurrencyCode = "JOD",
                ReservedByHandoffId = 28
            });

            var loan = new EmployeeLoan
            {
                Id = 802,
                Status = "ACTIVE",
                CurrencyCode = "JOD",
                TotalPaidAmount = 0m,
                RemainingBalance = 450m
            };
            db.LoanRepaymentSchedules.Add(new LoanRepaymentSchedule
            {
                Id = 602,
                EmployeeLoanId = 802,
                EmployeeLoan = loan,
                ScheduledAmount = 150m,
                PaidAmount = 0m,
                Status = "PENDING",
                ReservedByHandoffId = 28
            });
            await db.SaveChangesAsync();
        }

        await using (var db = harness.CreateDb())
        {
            var service = harness.CreateService(db);
            await service.ConfirmJournalPostedAsync(28, 26, "event-settle-snapshot", "finance-user");
        }

        await using (var verify = harness.CreateDb())
        {
            var receipt = await verify.AccountingHandoffPostingReceipts.SingleAsync(r => r.HandoffId == 28);
            Assert.NotNull(receipt.SettlementSnapshotJson);
            Assert.Contains("\"Kind\":\"ADVANCE\"", receipt.SettlementSnapshotJson);
            Assert.Contains("\"Kind\":\"SCHEDULE\"", receipt.SettlementSnapshotJson);

            var advance = await verify.EmployeeAdvances.SingleAsync(a => a.Id == 503);
            Assert.Equal("RECOVERED", advance.Status);
            Assert.Equal(0m, advance.RemainingBalance);
            Assert.Equal(100m, advance.DeductedAmount);
            Assert.Null(advance.ReservedByHandoffId);

            var schedule = await verify.LoanRepaymentSchedules.Include(s => s.EmployeeLoan).SingleAsync(s => s.Id == 602);
            Assert.Equal("PAID", schedule.Status);
            Assert.Equal(150m, schedule.PaidAmount);
            Assert.Equal(0m, schedule.CarriedForwardAmount);
            Assert.Null(schedule.ReservedByHandoffId);
            Assert.Equal(150m, schedule.EmployeeLoan!.TotalPaidAmount);
            Assert.Equal(300m, schedule.EmployeeLoan.RemainingBalance);
        }
    }

    [Fact]
    public async Task ConfirmJournalPosted_ForPayrollSubmit_WhenSchedulePartiallySettled_SetsStatusToPartial()
    {
        using var harness = new PostingReceiptHarness();
        await harness.SeedPayrollSubmitAsync(29, sourceId: "100");
        harness.SetupVoucher(27, branchId: 2, sourceRefId: 29, status: 3, postDate: DateTime.UtcNow);

        await using (var db = harness.CreateDb())
        {
            var handoff = await db.HrAccountingHandoffs.SingleAsync(h => h.Id == 29);
            handoff.Lines.Add(new HrAccountingHandoffLine
            {
                HandoffId = 29,
                LineType = "REFERENCE:SCHEDULE",
                SourceReference = "SCHEDULE:603:PAYROLL_LINE:2",
                Amount = 80m, // Only 80m of 200m scheduled
                CurrencyCode = "JOD"
            });

            var loan = new EmployeeLoan
            {
                Id = 803,
                Status = "ACTIVE",
                CurrencyCode = "JOD",
                TotalPaidAmount = 0m,
                RemainingBalance = 600m
            };
            db.LoanRepaymentSchedules.Add(new LoanRepaymentSchedule
            {
                Id = 603,
                EmployeeLoanId = 803,
                EmployeeLoan = loan,
                ScheduledAmount = 200m,
                PaidAmount = 0m,
                Status = "PENDING",
                ReservedByHandoffId = 29
            });
            await db.SaveChangesAsync();
        }

        await using (var db = harness.CreateDb())
        {
            var service = harness.CreateService(db);
            await service.ConfirmJournalPostedAsync(29, 27, "event-partial-schedule", "finance-user");
        }

        await using (var verify = harness.CreateDb())
        {
            var schedule = await verify.LoanRepaymentSchedules.Include(s => s.EmployeeLoan).SingleAsync(s => s.Id == 603);
            Assert.Equal("PARTIAL", schedule.Status);
            Assert.Equal(80m, schedule.PaidAmount);
            Assert.Equal(120m, schedule.CarriedForwardAmount);
            Assert.Null(schedule.ReservedByHandoffId);
            Assert.Equal(80m, schedule.EmployeeLoan!.TotalPaidAmount);
            Assert.Equal(520m, schedule.EmployeeLoan.RemainingBalance);
        }
    }

    [Fact]
    public async Task ConfirmJournalPosted_ReplayWithDifferentVoucherId_ThrowsInvalidOperationException()
    {
        using var harness = new PostingReceiptHarness();
        await harness.SeedAsync();

        await using (var db = harness.CreateDb())
        {
            var service = harness.CreateService(db);
            await service.ConfirmJournalPostedAsync(11, 7, "posting-event-001", "accounting-user");
        }

        // Replay with different voucher 8
        await using (var db = harness.CreateDb())
        {
            var service = harness.CreateService(db);
            var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
                service.ConfirmJournalPostedAsync(11, 8, "posting-event-001", "accounting-user"));
            Assert.Contains("conflicting posting receipt already exists", ex.Message);
        }
    }

    [Fact]
    public async Task ConfirmJournalPosted_ReplayWithDifferentPayloadHash_ThrowsInvalidOperationException()
    {
        using var harness = new PostingReceiptHarness();
        await harness.SeedAsync();

        await using (var db = harness.CreateDb())
        {
            var service = harness.CreateService(db);
            await service.ConfirmJournalPostedAsync(11, 7, "posting-event-001", "accounting-user");
        }

        // Modify voucher total amount so payload hash changes
        harness.SetupVoucher(7, branchId: 2, sourceRefId: 11, status: 3, postDate: new DateTime(2026, 11, 7, 10, 0, 0, DateTimeKind.Utc), totalDebit: 999m, totalCredit: 999m);

        await using (var db = harness.CreateDb())
        {
            var service = harness.CreateService(db);
            var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
                service.ConfirmJournalPostedAsync(11, 7, "posting-event-001", "accounting-user"));
            Assert.Contains("conflicting posting receipt already exists", ex.Message);
        }
    }

    private sealed class PostingReceiptHarness : IDisposable
    {
        private readonly List<(long HandoffId, long VoucherId)> journalLinks = [(11, 7)];
        private readonly string storeName = Guid.NewGuid().ToString();
        private readonly DbContextOptions<OracleDbContext> options;
        private readonly Mock<IGlVoucherRepository> vouchers = new();

        public PostingReceiptHarness()
        {
            options = new DbContextOptionsBuilder<OracleDbContext>()
                .UseInMemoryDatabase(storeName)
                .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
                .Options;

            foreach (var voucherId in new long[] { 7, 8 })
            {
                vouchers.Setup(x => x.GetByIdAsync(voucherId, It.IsAny<CancellationToken>()))
                    .ReturnsAsync(new GlVoucherHeader
                    {
                        Id = voucherId,
                        BranchId = 2,
                        SourceSystemCode = "HR_HANDOFF",
                        SourceRefId = 11,
                        Status = 3,
                        PostDate = new DateTime(2026, 11, 7, 10, 0, 0, DateTimeKind.Utc),
                        TotalLocalDebit = voucherId == 7 ? 100m : 125m,
                        TotalLocalCredit = voucherId == 7 ? 100m : 125m
                    });
            }
        }

        public void SetupVoucher(long id, long branchId, long sourceRefId, int status, DateTime? postDate, decimal totalDebit = 100m, decimal totalCredit = 100m)
        {
            journalLinks.Add((sourceRefId, id));
            vouchers.Setup(x => x.GetByIdAsync(id, It.IsAny<CancellationToken>()))
                .ReturnsAsync(new GlVoucherHeader
                {
                    Id = id,
                    BranchId = branchId,
                    SourceSystemCode = "HR_HANDOFF",
                    SourceRefId = sourceRefId,
                    Status = status,
                    PostDate = postDate,
                    TotalLocalDebit = totalDebit,
                    TotalLocalCredit = totalCredit
                });
        }

        public OracleDbContext CreateDb() => new(options);

        public HrAccountingHandoffService CreateService(OracleDbContext db, IHrUnitOfWork? unitOfWork = null)
        {
            var handoff = db.HrAccountingHandoffs.Include(h => h.Lines)
                .SingleOrDefault(h => h.SourceType == "PAYROLL" && h.Operation == "SUBMIT");
            if (handoff != null && long.TryParse(handoff.SourceId, out var runId))
            {
                if (!db.PayrollRuns.Any(run => run.Id == runId))
                {
                    var lines = new List<PayrollRunLine>();
                    foreach (var group in handoff.Lines.Where(line => line.LineType is "REFERENCE:ADVANCE" or "REFERENCE:SCHEDULE")
                                 .Select(line => (Line: line, Parts: (line.SourceReference ?? string.Empty).Split(':')))
                                 .Where(item => item.Parts.Length == 4 && long.TryParse(item.Parts[3], out _))
                                 .GroupBy(item => long.Parse(item.Parts[3])))
                    {
                        var first = group.First();
                        var sourceId = long.TryParse(first.Parts[1], out var parsedSourceId) ? parsedSourceId : 0;
                        var employeeCode = first.Parts[0] == "ADVANCE"
                            ? db.EmployeeAdvances.Where(source => source.Id == sourceId).Select(source => source.EmployeeCode).FirstOrDefault()
                            : db.LoanRepaymentSchedules.Where(source => source.Id == sourceId).Select(source => source.EmployeeLoan!.EmployeeCode).FirstOrDefault();
                        lines.Add(new PayrollRunLine
                        {
                            Id = group.Key, PayrollRunId = runId, EmployeeCode = employeeCode ?? "EMP-TEST",
                            BranchId = handoff.BranchId, CurrencyCode = first.Line.CurrencyCode,
                            Components = [new PayrollRunLineComponent
                            {
                                ComponentCode = "LOAN_DEDUCTION", ComponentNameEn = "Loan deduction",
                                ComponentType = "DEDUCTION", Amount = group.Sum(item => item.Line.Amount)
                            }]
                        });
                    }
                    db.PayrollRuns.Add(new PayrollRun
                    {
                        Id = runId, PayPeriod = handoff.PayPeriod ?? "2026-11", BranchId = handoff.BranchId, Status = "APPROVED",
                        CorrectionVersion = handoff.SourceVersion, ApprovalDate = handoff.SourceApprovedAtUtc, Lines = lines
                    });
                }
            }
            foreach (var persistedHandoff in db.HrAccountingHandoffs.ToList())
                foreach (var journalLink in journalLinks.Where(link => link.HandoffId == persistedHandoff.Id).Distinct())
                    if (!db.AccountingHandoffJournalLinks.Any(link => link.HandoffId == journalLink.HandoffId && link.VoucherId == journalLink.VoucherId))
                        db.AccountingHandoffJournalLinks.Add(new AccountingHandoffJournalLink
                        {
                            HandoffId = journalLink.HandoffId, VoucherId = journalLink.VoucherId, CreatedAtUtc = DateTime.UtcNow, CreatedBy = "accounting"
                        });
            db.SaveChanges();
            return new HrAccountingHandoffService(db, vouchers.Object, Mock.Of<IHrPayrollPostingService>(),
                Mock.Of<IReceiptPaymentVoucherService>(), Mock.Of<IGlVoucherService>(), unitOfWork);
        }

        public async Task SeedAsync()
        {
            await using var db = CreateDb();
            db.HrAccountingHandoffs.Add(new HrAccountingHandoff
            {
                Id = 11,
                CompanyId = 3,
                BranchId = 2,
                SourceType = "LOAN",
                SourceId = "41",
                SourceVersion = 1,
                Operation = "FUND",
                HandoffReference = "HR-HANDOFF-11",
                PayloadHash = "handoff-payload",
                DeliveryStatus = "ACCEPTED",
                CreatedAtUtc = new DateTime(2026, 11, 7, 9, 0, 0, DateTimeKind.Utc),
                CreatedBy = "hr-user"
            });
            await db.SaveChangesAsync();
        }

        public async Task SeedPayrollSubmitAsync(long handoffId = 22, string sourceId = "100")
        {
            await using var db = CreateDb();
            db.HrAccountingHandoffs.Add(new HrAccountingHandoff
            {
                Id = handoffId,
                CompanyId = 3,
                BranchId = 2,
                SourceType = "PAYROLL",
                SourceId = sourceId,
                SourceVersion = 1,
                Operation = "SUBMIT",
                HandoffReference = $"HR-HANDOFF-{handoffId}",
                PayloadHash = "payroll-handoff-payload",
                DeliveryStatus = "ACCEPTED",
                CreatedAtUtc = new DateTime(2026, 11, 7, 9, 0, 0, DateTimeKind.Utc),
                CreatedBy = "hr-user"
            });
            await db.SaveChangesAsync();
        }

        public async Task SeedPayrollSubmitWithLoanScheduleAsync()
        {
            await using var db = CreateDb();
            var handoffId = 11;
            var loan = new EmployeeLoan
            {
                Id = 41, EmployeeCode = "E-41", PrincipalAmount = 50m, CurrencyCode = "USD",
                RemainingBalance = 50m, Status = "ACTIVE"
            };
            db.EmployeeLoans.Add(loan);
            db.LoanRepaymentSchedules.AddRange(
                new LoanRepaymentSchedule
                {
                    Id = 151, EmployeeLoanId = 41, EmployeeLoan = loan, InstallmentNo = 1, PayPeriod = "2026-11",
                    ScheduledAmount = 50m, Status = "PENDING", ReservedByHandoffId = handoffId
                },
                new LoanRepaymentSchedule
                {
                    Id = 152, EmployeeLoanId = 41, EmployeeLoan = loan, InstallmentNo = 2, PayPeriod = "2026-12",
                    ScheduledAmount = 50m, Status = "PENDING"
                });
            db.HrAccountingHandoffs.Add(new HrAccountingHandoff
            {
                Id = handoffId, CompanyId = 3, BranchId = 2, SourceType = "PAYROLL", SourceId = "100",
                SourceVersion = 1, Operation = "SUBMIT", PayPeriod = "2026-11", HandoffReference = "HR-HANDOFF-11",
                PayloadHash = "handoff-payload", DeliveryStatus = "ACCEPTED",
                CreatedAtUtc = new DateTime(2026, 11, 7, 9, 0, 0, DateTimeKind.Utc), CreatedBy = "hr-user",
                Lines = [new HrAccountingHandoffLine
                {
                    LineType = "REFERENCE:SCHEDULE", CurrencyCode = "USD", Amount = 50m, BranchId = 2,
                    SourceReference = "SCHEDULE:151:PAYROLL_LINE:1001"
                }]
            });
            await db.SaveChangesAsync();
        }

        public void Dispose() { }
    }
}
