using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using ThinkOnErp.Application.DTOs.Accounting.Vouchers;
using ThinkOnErp.Application.Services.Accounting;
using ThinkOnErp.Application.Services.Hr;
using ThinkOnErp.Domain.Entities.Accounting;
using ThinkOnErp.Domain.Entities.Hr;
using ThinkOnErp.Domain.Interfaces.Accounting;
using ThinkOnErp.Infrastructure.Data;
using ThinkOnErp.Infrastructure.Services.Hr;
using Xunit;

namespace ThinkOnErp.HR.Api.Tests;

public sealed class AccountingHandoffJournalReversalTests
{
    [Fact]
    public async Task ReverseJournalVoucherSucceedsAndRestoresAdvanceSettlementSnapshot()
    {
        using var harness = new JournalReversalHarness();
        await harness.SeedAdvanceAsync();

        await using (var db = harness.CreateDb())
        {
            var service = harness.CreateService(db);
            var reversalId = await service.ReverseJournalVoucherAsync(11, DateTime.UtcNow.Date, "Payroll correction needed", "accounting-user");
            Assert.Equal(8, reversalId);
        }

        await using var verify = harness.CreateDb();
        var handoff = await verify.HrAccountingHandoffs.Include(h => h.Transitions).SingleAsync();
        Assert.Equal("REVERSED", handoff.DeliveryStatus);
        var transition = Assert.Single(handoff.Transitions.Where(t => t.ToStatus == "REVERSED"));
        Assert.Equal("Payroll correction needed", transition.Reason);
        Assert.Equal("accounting-user", transition.Actor);

        var advance = await verify.EmployeeAdvances.SingleAsync();
        Assert.Equal(0m, advance.DeductedAmount);
        Assert.Equal(100m, advance.RemainingBalance);
        Assert.Equal("ACTIVE", advance.Status);
        Assert.Equal("accounting-user", advance.UpdateUser);

        harness.GlVouchers.Verify(v => v.ReverseVoucherAsync(7, "accounting-user", "Payroll correction needed", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ReverseJournalVoucherUsesRealGlVoucherServiceAndCreatesExactOffset()
    {
        using var harness = new JournalReversalHarness();
        await harness.SeedAdvanceAsync();

        await using (var db = harness.CreateDb())
        {
            var glService = harness.CreateRealGlVoucherService();
            var result = await harness.CreateService(db, glService)
                .ReverseJournalVoucherAsync(11, DateTime.UtcNow.Date, "Payroll correction needed", "accounting-user");

            Assert.Equal(8, result);
        }

        var reversal = Assert.IsType<GlVoucherHeader>(harness.CreatedReversal);
        Assert.Equal("GL_REVERSAL", reversal.SourceSystemCode);
        Assert.Equal(7, reversal.SourceRefId);
        Assert.Equal(3, reversal.Status);
        Assert.Equal(reversal.TotalLocalDebit, reversal.TotalLocalCredit);
        Assert.Equal(3, reversal.Details.Count);
        Assert.Collection(reversal.Details,
            line => Assert.Equal((0m, 100m), (line.Debit, line.Credit)),
            line => Assert.Equal((40m, 0m), (line.Debit, line.Credit)),
            line => Assert.Equal((60m, 0m), (line.Debit, line.Credit)));

        await using var verify = harness.CreateDb();
        var handoff = await verify.HrAccountingHandoffs.SingleAsync();
        Assert.Equal("REVERSED", handoff.DeliveryStatus);
    }

    [Fact]
    public async Task ReplayOfSameJournalReversalIsIdempotent()
    {
        using var harness = new JournalReversalHarness();
        await harness.SeedAdvanceAsync();

        await using (var db = harness.CreateDb())
        {
            var service = harness.CreateService(db);
            var first = await service.ReverseJournalVoucherAsync(11, DateTime.UtcNow.Date, "Payroll correction needed", "accounting-user");
            Assert.Equal(8, first);
        }

        await using (var db = harness.CreateDb())
        {
            var service = harness.CreateService(db);
            var second = await service.ReverseJournalVoucherAsync(11, DateTime.UtcNow.Date, "Payroll correction needed", "retry-user");
            Assert.Equal(8, second);
        }

        harness.GlVouchers.Verify(v => v.ReverseVoucherAsync(7, It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ReverseJournalVoucherFailsIfActivePaymentExists()
    {
        using var harness = new JournalReversalHarness();
        await harness.SeedAdvanceAsync();

        await using (var db = harness.CreateDb())
        {
            db.AccountingHandoffPayments.Add(new AccountingHandoffPayment
            {
                Id = 91,
                HandoffId = 11,
                Amount = 50m,
                CurrencyCode = "USD",
                Method = "BANK",
                IdempotencyKey = "pay-key-active-91",
                Status = "CONFIRMED",
                CreatedAtUtc = DateTime.UtcNow,
                CreatedBy = "accounting"
            });
            await db.SaveChangesAsync();
        }

        await using (var db = harness.CreateDb())
        {
            var service = harness.CreateService(db);
            var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
                service.ReverseJournalVoucherAsync(11, DateTime.UtcNow.Date, "Payroll correction needed", "accounting-user"));
            Assert.Contains("Void prepared payments and reverse every confirmed payment", ex.Message);
        }
    }

    [Fact]
    public async Task ReverseJournalVoucherFailsIfNotPosted()
    {
        using var harness = new JournalReversalHarness();
        await harness.SeedAdvanceAsync();

        await using (var setupDb = harness.CreateDb())
        {
            var handoff = await setupDb.HrAccountingHandoffs.SingleAsync();
            handoff.DeliveryStatus = "ACCEPTED";
            await setupDb.SaveChangesAsync();
        }

        await using var db = harness.CreateDb();
        var service = harness.CreateService(db);
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.ReverseJournalVoucherAsync(11, DateTime.UtcNow.Date, "Payroll correction needed", "accounting-user"));
        Assert.Contains("Only a posted handoff can have its Journal Voucher reversed", ex.Message);
    }

    [Fact]
    public async Task ReverseJournalVoucherFailsIfLegacyPostingHasReferencesWithoutSnapshot()
    {
        using var harness = new JournalReversalHarness();
        await harness.SeedAdvanceAsync();

        await using (var setupDb = harness.CreateDb())
        {
            var receipt = await setupDb.AccountingHandoffPostingReceipts.SingleAsync();
            receipt.SettlementSnapshotJson = null;
            await setupDb.SaveChangesAsync();
        }

        await using var db = harness.CreateDb();
        var service = harness.CreateService(db);
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.ReverseJournalVoucherAsync(11, DateTime.UtcNow.Date, "Payroll correction needed", "accounting-user"));
        Assert.Contains("predates settlement snapshots", ex.Message);
    }

    [Fact]
    public async Task ReverseJournalVoucherFailsIfAccountingReversalDoesNotInvertVoucher()
    {
        using var harness = new JournalReversalHarness();
        harness.CorruptReversal();
        await harness.SeedAdvanceAsync();

        await using var db = harness.CreateDb();
        var service = harness.CreateService(db);
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.ReverseJournalVoucherAsync(11, DateTime.UtcNow.Date, "Payroll correction needed", "accounting-user"));
        Assert.Contains("does not exactly offset its source", ex.Message);
    }

    [Fact]
    public async Task ReverseJournalVoucherSucceedsAndRestoresLoanScheduleSettlementSnapshot()
    {
        using var harness = new JournalReversalHarness();
        await harness.SeedLoanScheduleAsync();

        await using (var db = harness.CreateDb())
        {
            var service = harness.CreateService(db);
            var reversalId = await service.ReverseJournalVoucherAsync(11, DateTime.UtcNow.Date, "Loan payroll correction needed", "accounting-user");
            Assert.Equal(8, reversalId);
        }

        await using var verify = harness.CreateDb();
        var handoff = await verify.HrAccountingHandoffs.SingleAsync();
        Assert.Equal("REVERSED", handoff.DeliveryStatus);

        var schedule = await verify.LoanRepaymentSchedules.SingleAsync(s => s.Id == 101);
        Assert.Equal("PENDING", schedule.Status);
        Assert.Equal(0m, schedule.PaidAmount);
        Assert.Equal(0m, schedule.CarriedForwardAmount);
        Assert.Null(schedule.PaidDate);
        Assert.Null(schedule.PayrollRunLineId);

        var loan = await verify.EmployeeLoans.SingleAsync(l => l.Id == 51);
        Assert.Equal(0m, loan.TotalPaidAmount);
        Assert.Equal(500m, loan.RemainingBalance);
        Assert.Equal("ACTIVE", loan.Status);
        Assert.Equal("accounting-user", loan.UpdateUser);
    }

    [Fact]
    public async Task ReverseJournalVoucherSucceedsAndRestoresBothAdvanceAndLoanSchedules()
    {
        using var harness = new JournalReversalHarness();
        await harness.SeedBothAdvanceAndLoanAsync();

        await using (var db = harness.CreateDb())
        {
            var service = harness.CreateService(db);
            var reversalId = await service.ReverseJournalVoucherAsync(11, DateTime.UtcNow.Date, "Mixed payroll correction", "accounting-user");
            Assert.Equal(8, reversalId);
        }

        await using var verify = harness.CreateDb();
        var advance = await verify.EmployeeAdvances.SingleAsync();
        Assert.Equal(0m, advance.DeductedAmount);
        Assert.Equal(100m, advance.RemainingBalance);

        var schedule = await verify.LoanRepaymentSchedules.SingleAsync();
        Assert.Equal("PENDING", schedule.Status);
        Assert.Equal(0m, schedule.PaidAmount);

        var loan = await verify.EmployeeLoans.SingleAsync();
        Assert.Equal(0m, loan.TotalPaidAmount);
        Assert.Equal(500m, loan.RemainingBalance);
    }

    [Fact]
    public async Task ReverseJournalVoucherFailsIfLoanWasModifiedAfterSettlement()
    {
        using var harness = new JournalReversalHarness();
        await harness.SeedLoanScheduleAsync();

        await using (var setupDb = harness.CreateDb())
        {
            var loan = await setupDb.EmployeeLoans.SingleAsync(l => l.Id == 51);
            loan.TotalPaidAmount = 150m; // Changed after settlement
            await setupDb.SaveChangesAsync();
        }

        await using var db = harness.CreateDb();
        var service = harness.CreateService(db);
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.ReverseJournalVoucherAsync(11, DateTime.UtcNow.Date, "Payroll correction needed", "accounting-user"));
        Assert.Contains("Loan or installment has later or conflicting settlements", ex.Message);
    }

    [Fact]
    public async Task ReverseJournalVoucherFailsIfAdvanceWasModifiedAfterSettlement()
    {
        using var harness = new JournalReversalHarness();
        await harness.SeedAdvanceAsync();

        await using (var setupDb = harness.CreateDb())
        {
            var advance = await setupDb.EmployeeAdvances.SingleAsync();
            advance.DeductedAmount = 50m; // Changed after settlement
            await setupDb.SaveChangesAsync();
        }

        await using var db = harness.CreateDb();
        var service = harness.CreateService(db);
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.ReverseJournalVoucherAsync(11, DateTime.UtcNow.Date, "Payroll correction needed", "accounting-user"));
        Assert.Contains("Advance has later or conflicting settlements", ex.Message);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("ab")] // < 3 chars
    public async Task ReverseJournalVoucherFailsIfReasonIsInvalid(string reason)
    {
        using var harness = new JournalReversalHarness();
        await harness.SeedAdvanceAsync();

        await using var db = harness.CreateDb();
        var service = harness.CreateService(db);
        await Assert.ThrowsAsync<ArgumentException>(() =>
            service.ReverseJournalVoucherAsync(11, DateTime.UtcNow.Date, reason, "accounting-user"));
    }

    [Fact]
    public async Task ReverseJournalVoucherFailsIfHandoffNotPayrollSubmit()
    {
        using var harness = new JournalReversalHarness();
        await harness.SeedAdvanceAsync();

        await using (var setupDb = harness.CreateDb())
        {
            var handoff = await setupDb.HrAccountingHandoffs.SingleAsync();
            handoff.SourceType = "ADVANCE";
            handoff.Operation = "FUND";
            await setupDb.SaveChangesAsync();
        }

        await using var db = harness.CreateDb();
        var service = harness.CreateService(db);
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.ReverseJournalVoucherAsync(11, DateTime.UtcNow.Date, "Payroll correction needed", "accounting-user"));
        Assert.Contains("Only a posted payroll submission Journal Voucher can be reversed", ex.Message);
    }

    [Fact]
    public async Task ReverseJournalVoucherFailsIfReasonExceeds1000Chars()
    {
        using var harness = new JournalReversalHarness();
        await harness.SeedAdvanceAsync();

        var longReason = new string('x', 1001);
        await using var db = harness.CreateDb();
        var service = harness.CreateService(db);
        var ex = await Assert.ThrowsAsync<ArgumentException>(() =>
            service.ReverseJournalVoucherAsync(11, DateTime.UtcNow.Date, longReason, "accounting-user"));
        Assert.Contains("A journal reversal reason of 3..1000 characters is required", ex.Message);
    }

    [Fact]
    public async Task ReverseJournalVoucherFailsIfHandoffNotFound()
    {
        using var harness = new JournalReversalHarness();
        await harness.SeedAdvanceAsync();

        await using var db = harness.CreateDb();
        var service = harness.CreateService(db);
        var ex = await Assert.ThrowsAsync<KeyNotFoundException>(() =>
            service.ReverseJournalVoucherAsync(99999, DateTime.UtcNow.Date, "Payroll correction needed", "accounting-user"));
        Assert.Contains("Handoff not found in active scope", ex.Message);
    }

    [Fact]
    public async Task ReverseJournalVoucherFailsIfJournalLinkMissing()
    {
        using var harness = new JournalReversalHarness();
        await harness.SeedAdvanceAsync();

        await using (var setupDb = harness.CreateDb())
        {
            var links = await setupDb.AccountingHandoffJournalLinks.ToListAsync();
            setupDb.AccountingHandoffJournalLinks.RemoveRange(links);
            await setupDb.SaveChangesAsync();
        }

        await using var db = harness.CreateDb();
        var service = harness.CreateService(db);
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.ReverseJournalVoucherAsync(11, DateTime.UtcNow.Date, "Payroll correction needed", "accounting-user"));
        Assert.Contains("The payroll Journal Voucher link is missing", ex.Message);
    }

    [Fact]
    public async Task ReverseJournalVoucherFailsIfHandoffHasNoBranch()
    {
        using var harness = new JournalReversalHarness();
        await harness.SeedAdvanceAsync();

        await using (var setupDb = harness.CreateDb())
        {
            var handoff = await setupDb.HrAccountingHandoffs.SingleAsync();
            handoff.BranchId = null;
            await setupDb.SaveChangesAsync();
        }

        await using var db = harness.CreateDb();
        var service = harness.CreateService(db);
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.ReverseJournalVoucherAsync(11, DateTime.UtcNow.Date, "Payroll correction needed", "accounting-user"));
        Assert.Contains("Payroll handoff has no branch", ex.Message);
    }

    [Fact]
    public async Task ReverseJournalVoucherReplayFailsIfReasonDiffersFromOriginalReversal()
    {
        using var harness = new JournalReversalHarness();
        await harness.SeedAdvanceAsync();

        await using (var db = harness.CreateDb())
        {
            var service = harness.CreateService(db);
            await service.ReverseJournalVoucherAsync(11, DateTime.UtcNow.Date, "Original reason for reversal", "accounting-user");
        }

        await using (var db = harness.CreateDb())
        {
            var service = harness.CreateService(db);
            var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
                service.ReverseJournalVoucherAsync(11, DateTime.UtcNow.Date, "Different reason for reversal", "accounting-user"));
            Assert.Contains("The payroll Journal Voucher reversal is immutable", ex.Message);
        }
    }

    [Fact]
    public async Task ReverseJournalVoucherFailsIfPostingReceiptVoucherIdMismatchesLink()
    {
        using var harness = new JournalReversalHarness();
        await harness.SeedAdvanceAsync();

        await using (var setupDb = harness.CreateDb())
        {
            var receipt = await setupDb.AccountingHandoffPostingReceipts.SingleAsync();
            receipt.VoucherId = 999; // Mismatched voucher ID
            await setupDb.SaveChangesAsync();
        }

        await using var db = harness.CreateDb();
        var service = harness.CreateService(db);
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.ReverseJournalVoucherAsync(11, DateTime.UtcNow.Date, "Payroll correction needed", "accounting-user"));
        Assert.Contains("Posting receipt and Journal Voucher link do not match", ex.Message);
    }

    [Fact]
    public async Task ReverseJournalVoucherFailsIfMissingPostingReceipt()
    {
        using var harness = new JournalReversalHarness();
        await harness.SeedAdvanceAsync();

        await using (var setupDb = harness.CreateDb())
        {
            var receipts = await setupDb.AccountingHandoffPostingReceipts.ToListAsync();
            setupDb.AccountingHandoffPostingReceipts.RemoveRange(receipts);
            await setupDb.SaveChangesAsync();
        }

        await using var db = harness.CreateDb();
        var service = harness.CreateService(db);
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.ReverseJournalVoucherAsync(11, DateTime.UtcNow.Date, "Payroll correction needed", "accounting-user"));
        Assert.Contains("The payroll Journal Voucher has no posting receipt", ex.Message);
    }

    [Theory]
    [InlineData("MISSING_REASON")]
    [InlineData("MISSING_DATE")]
    [InlineData("MISSING_USER")]
    public async Task ReverseJournalVoucherFailsIfFailedPaymentHasIncompleteAuditData(string scenario)
    {
        using var harness = new JournalReversalHarness();
        await harness.SeedAdvanceAsync();

        await using (var setupDb = harness.CreateDb())
        {
            setupDb.AccountingHandoffPayments.Add(new AccountingHandoffPayment
            {
                Id = 92,
                HandoffId = 11,
                Amount = 50m,
                CurrencyCode = "USD",
                Method = "BANK",
                IdempotencyKey = "pay-failed-92",
                Status = "FAILED",
                FailureReason = scenario == "MISSING_REASON" ? null : "Bank error",
                FailedAtUtc = scenario == "MISSING_DATE" ? null : DateTime.UtcNow,
                FailedBy = scenario == "MISSING_USER" ? null : "accounting",
                CreatedAtUtc = DateTime.UtcNow,
                CreatedBy = "accounting"
            });
            await setupDb.SaveChangesAsync();
        }

        await using var db = harness.CreateDb();
        var service = harness.CreateService(db);
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.ReverseJournalVoucherAsync(11, DateTime.UtcNow.Date, "Payroll correction needed", "accounting-user"));
        Assert.Contains("has incomplete audit data", ex.Message);
    }

    [Fact]
    public async Task ReverseJournalVoucherFailsIfFailedPaymentStillHasNonVoidedPaymentVoucher()
    {
        using var harness = new JournalReversalHarness();
        await harness.SeedAdvanceAsync();

        harness.VoucherRepo.Setup(v => v.GetBySourceAsync(2, "HR_PAYMENT", 93, It.IsAny<CancellationToken>()))
            .ReturnsAsync([new GlVoucherHeader { Id = 999, BranchId = 2, Status = 1 }]); // Status 1 = Draft, not 4 = Void

        await using (var setupDb = harness.CreateDb())
        {
            setupDb.AccountingHandoffPayments.Add(new AccountingHandoffPayment
            {
                Id = 93,
                HandoffId = 11,
                Amount = 50m,
                CurrencyCode = "USD",
                Method = "BANK",
                IdempotencyKey = "pay-failed-93",
                Status = "FAILED",
                FailureReason = "Bank reject",
                FailedAtUtc = DateTime.UtcNow,
                FailedBy = "accounting",
                CreatedAtUtc = DateTime.UtcNow,
                CreatedBy = "accounting"
            });
            await setupDb.SaveChangesAsync();
        }

        await using var db = harness.CreateDb();
        var service = harness.CreateService(db);
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.ReverseJournalVoucherAsync(11, DateTime.UtcNow.Date, "Payroll correction needed", "accounting-user"));
        Assert.Contains("still has a non-voided Payment Voucher", ex.Message);
    }

    [Fact]
    public async Task ReverseJournalVoucherFailsIfSettlementSnapshotIsMalformed()
    {
        using var harness = new JournalReversalHarness();
        await harness.SeedAdvanceAsync();

        await using (var setupDb = harness.CreateDb())
        {
            var receipt = await setupDb.AccountingHandoffPostingReceipts.SingleAsync();
            receipt.SettlementSnapshotJson = "{ invalid json }";
            await setupDb.SaveChangesAsync();
        }

        await using var db = harness.CreateDb();
        var service = harness.CreateService(db);
        await Assert.ThrowsAnyAsync<Exception>(() =>
            service.ReverseJournalVoucherAsync(11, DateTime.UtcNow.Date, "Payroll correction needed", "accounting-user"));
    }

    [Fact]
    public async Task ReverseJournalVoucherFailsIfSettlementSnapshotCountMismatchesReferences()
    {
        using var harness = new JournalReversalHarness();
        await harness.SeedAdvanceAsync();

        await using (var setupDb = harness.CreateDb())
        {
            var receipt = await setupDb.AccountingHandoffPostingReceipts.SingleAsync();
            receipt.SettlementSnapshotJson = "[]";
            await setupDb.SaveChangesAsync();
        }

        await using var db = harness.CreateDb();
        var service = harness.CreateService(db);
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.ReverseJournalVoucherAsync(11, DateTime.UtcNow.Date, "Payroll correction needed", "accounting-user"));
        Assert.Contains("Payroll settlement snapshot does not match the handoff references", ex.Message);
    }

    [Fact]
    public async Task ReverseJournalVoucherReplayFailsIfReversalCannotBeVerified()
    {
        using var harness = new JournalReversalHarness();
        await harness.SeedAdvanceAsync();

        // 1. First reversal succeeds
        await using (var db = harness.CreateDb())
        {
            var service = harness.CreateService(db);
            await service.ReverseJournalVoucherAsync(11, DateTime.UtcNow.Date, "Payroll correction needed", "accounting-user");
        }

        // 2. Corrupt the reversal journal in mock
        harness.CorruptReversal();

        // 3. Replay with same reason fails because IsExactAccountingReversal returns false
        await using (var db = harness.CreateDb())
        {
            var service = harness.CreateService(db);
            var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
                service.ReverseJournalVoucherAsync(11, DateTime.UtcNow.Date, "Payroll correction needed", "accounting-user"));
            Assert.Contains("The stored Journal Voucher reversal cannot be verified", ex.Message);
        }
    }

    [Fact]
    public async Task ReverseJournalVoucherFailsIfPaymentHasMultipleLinkedPaymentVouchers()
    {
        using var harness = new JournalReversalHarness();
        await harness.SeedAdvanceAsync();

        harness.VoucherRepo.Setup(v => v.GetBySourceAsync(2, "HR_PAYMENT", 94, It.IsAny<CancellationToken>()))
            .ReturnsAsync([
                new GlVoucherHeader { Id = 101, BranchId = 2, Status = 4 },
                new GlVoucherHeader { Id = 102, BranchId = 2, Status = 4 }
            ]);

        await using (var setupDb = harness.CreateDb())
        {
            setupDb.AccountingHandoffPayments.Add(new AccountingHandoffPayment
            {
                Id = 94,
                HandoffId = 11,
                Amount = 50m,
                CurrencyCode = "USD",
                Method = "BANK",
                IdempotencyKey = "pay-multi-94",
                Status = "FAILED",
                FailureReason = "Bank reject",
                FailedAtUtc = DateTime.UtcNow,
                FailedBy = "accounting",
                CreatedAtUtc = DateTime.UtcNow,
                CreatedBy = "accounting"
            });
            await setupDb.SaveChangesAsync();
        }

        await using var db = harness.CreateDb();
        var service = harness.CreateService(db);
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.ReverseJournalVoucherAsync(11, DateTime.UtcNow.Date, "Payroll correction needed", "accounting-user"));
        Assert.Contains("has multiple linked Payment Vouchers", ex.Message);
    }

    [Fact]
    public async Task ReverseJournalVoucherFailsIfReversedPaymentIsMissingReversalVoucher()
    {
        using var harness = new JournalReversalHarness();
        await harness.SeedAdvanceAsync();

        harness.VoucherRepo.Setup(v => v.GetBySourceAsync(2, "HR_PAYMENT", 95, It.IsAny<CancellationToken>()))
            .ReturnsAsync([new GlVoucherHeader { Id = 201, BranchId = 2, Status = 3, IsReversed = true }]);

        await using (var setupDb = harness.CreateDb())
        {
            setupDb.AccountingHandoffPayments.Add(new AccountingHandoffPayment
            {
                Id = 95,
                HandoffId = 11,
                Amount = 50m,
                CurrencyCode = "USD",
                Method = "BANK",
                IdempotencyKey = "pay-rev-95",
                Status = "REVERSED",
                ReversalVoucherId = null,
                ReversalReason = "Valid reason",
                ReversalExternalReference = "EXT-REF",
                ReversalEvidenceReference = "EVID-REF",
                ReversedAtUtc = DateTime.UtcNow,
                ReversedBy = "accounting",
                CreatedAtUtc = DateTime.UtcNow,
                CreatedBy = "accounting"
            });
            await setupDb.SaveChangesAsync();
        }

        await using var db = harness.CreateDb();
        var service = harness.CreateService(db);
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.ReverseJournalVoucherAsync(11, DateTime.UtcNow.Date, "Payroll correction needed", "accounting-user"));
        Assert.Contains("is missing its original/reversal voucher or reversal audit data", ex.Message);
    }

    [Fact]
    public async Task ReverseJournalVoucherAsync_PropagatesCancellationToken()
    {
        using var harness = new JournalReversalHarness();
        await harness.SeedAdvanceAsync();
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        await using var db = harness.CreateDb();
        var service = harness.CreateService(db);
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            service.ReverseJournalVoucherAsync(11, DateTime.UtcNow.Date, "Payroll correction needed", "accounting-user", cts.Token));
    }

    [Fact]
    public async Task ReverseJournalVoucherAsync_WhenActivePayrollJournalMissingInRepo_ThrowsInvalidOperationException()
    {
        using var harness = new JournalReversalHarness();
        await harness.SeedAdvanceAsync();

        harness.VoucherRepo.Setup(v => v.GetByIdAsync(7, It.IsAny<CancellationToken>()))
            .ReturnsAsync((GlVoucherHeader?)null);

        await using var db = harness.CreateDb();
        var service = harness.CreateService(db);
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.ReverseJournalVoucherAsync(11, DateTime.UtcNow.Date, "Payroll correction needed", "accounting-user"));
        Assert.Contains("The linked payroll Journal Voucher is missing", ex.Message);
    }

    [Fact]
    public async Task ReverseJournalVoucherAsync_WhenPostingReceiptReferencesDifferentJournal_ThrowsInvalidOperationException()
    {
        using var harness = new JournalReversalHarness();
        await harness.SeedAdvanceAsync();

        var anotherJournal = new GlVoucherHeader
        {
            Id = 99,
            BranchId = 2,
            Status = 3,
            SourceSystemCode = "HR_HANDOFF",
            SourceRefId = 11,
            PostDate = DateTime.UtcNow,
            TotalAmount = 100m,
            Details = [new GlVoucherDetail { LineSer = 3, Credit = 60m }]
        };

        harness.VoucherRepo.Setup(v => v.GetByIdAsync(99, It.IsAny<CancellationToken>()))
            .ReturnsAsync(anotherJournal);

        await using (var setupDb = harness.CreateDb())
        {
            var link = await setupDb.AccountingHandoffJournalLinks.SingleAsync();
            link.VoucherId = 99;
            await setupDb.SaveChangesAsync();
        }

        await using var db = harness.CreateDb();
        var service = harness.CreateService(db);
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.ReverseJournalVoucherAsync(11, DateTime.UtcNow.Date, "Payroll correction needed", "accounting-user"));
        Assert.Contains("Posting receipt and Journal Voucher link do not match", ex.Message);
    }

    [Fact]
    public async Task ReverseJournalVoucherAsync_WhenReversalVouchersEmptyAfterReverse_ThrowsInvalidOperationException()
    {
        using var harness = new JournalReversalHarness();
        await harness.SeedAdvanceAsync();

        harness.VoucherRepo.Setup(v => v.GetBySourceAsync(2, "GL_REVERSAL", 7, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<GlVoucherHeader>());

        await using var db = harness.CreateDb();
        var service = harness.CreateService(db);
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.ReverseJournalVoucherAsync(11, DateTime.UtcNow.Date, "Payroll correction needed", "accounting-user"));
        Assert.Contains("The ERP Journal Voucher reversal was not created or does not exactly offset its source", ex.Message);
    }

    [Fact]
    public async Task ReverseJournalVoucherAsync_WhenOriginalJournalNotMarkedReversed_ThrowsInvalidOperationException()
    {
        using var harness = new JournalReversalHarness();
        await harness.SeedAdvanceAsync();

        harness.GlVouchers.Setup(v => v.ReverseVoucherAsync(7, It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new GlVoucherHeaderDto());

        await using var db = harness.CreateDb();
        var service = harness.CreateService(db);
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.ReverseJournalVoucherAsync(11, DateTime.UtcNow.Date, "Payroll correction needed", "accounting-user"));
        Assert.Contains("The ERP Journal Voucher reversal was not created or does not exactly offset its source", ex.Message);
    }

    [Theory]
    [InlineData("MISSING_REASON")]
    [InlineData("MISSING_EXT_REF")]
    [InlineData("MISSING_EVID_REF")]
    [InlineData("MISSING_REVERSED_DATE")]
    [InlineData("MISSING_REVERSED_USER")]
    public async Task ReverseJournalVoucherAsync_WhenReversedPaymentMissingAuditField_ThrowsInvalidOperationException(string missingField)
    {
        using var harness = new JournalReversalHarness();
        await harness.SeedAdvanceAsync();

        harness.VoucherRepo.Setup(v => v.GetBySourceAsync(2, "HR_PAYMENT", 96, It.IsAny<CancellationToken>()))
            .ReturnsAsync([new GlVoucherHeader { Id = 201, BranchId = 2, Status = 3, IsReversed = true }]);

        await using (var setupDb = harness.CreateDb())
        {
            setupDb.AccountingHandoffPayments.Add(new AccountingHandoffPayment
            {
                Id = 96,
                HandoffId = 11,
                Amount = 50m,
                CurrencyCode = "USD",
                Method = "BANK",
                IdempotencyKey = "pay-rev-96",
                Status = "REVERSED",
                ReversalVoucherId = 202,
                ReversalReason = missingField == "MISSING_REASON" ? null : "Reversal reason",
                ReversalExternalReference = missingField == "MISSING_EXT_REF" ? null : "EXT-96",
                ReversalEvidenceReference = missingField == "MISSING_EVID_REF" ? null : "EVID-96",
                ReversedAtUtc = missingField == "MISSING_REVERSED_DATE" ? null : DateTime.UtcNow,
                ReversedBy = missingField == "MISSING_REVERSED_USER" ? null : "accounting",
                CreatedAtUtc = DateTime.UtcNow,
                CreatedBy = "accounting"
            });
            await setupDb.SaveChangesAsync();
        }

        await using var db = harness.CreateDb();
        var service = harness.CreateService(db);
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.ReverseJournalVoucherAsync(11, DateTime.UtcNow.Date, "Payroll correction needed", "accounting-user"));
        Assert.Contains("is missing its original/reversal voucher or reversal audit data", ex.Message);
    }

    [Fact]
    public async Task ReverseJournalVoucherAsync_WhenReversedPaymentOriginalVoucherSourceSystemCodeNotHrPayment_ThrowsInvalidOperationException()
    {
        using var harness = new JournalReversalHarness();
        await harness.SeedAdvanceAsync();

        var originalPaymentVoucher = new GlVoucherHeader
        {
            Id = 201, BranchId = 2, Status = 3, IsReversed = true,
            SourceSystemCode = "GL_MANUAL",
            SourceRefId = 97, TotalAmount = 50m, TotalLocalDebit = 50m, TotalLocalCredit = 50m,
            Details = [
                new GlVoucherDetail { VoucherId = 201, LineSer = 1, BranchId = 2, CurrencyId = 9, AccountCode = "NET", Debit = 50m, LocalDebit = 50m, BaseDebit = 50m },
                new GlVoucherDetail { VoucherId = 201, LineSer = 2, BranchId = 2, CurrencyId = 9, AccountCode = "CASH", Credit = 50m, LocalCredit = 50m, BaseCredit = 50m }
            ]
        };
        var reversalPaymentVoucher = new GlVoucherHeader
        {
            Id = 202, BranchId = 2, Status = 3, IsReversed = false, PostDate = DateTime.UtcNow,
            SourceSystemCode = "GL_REVERSAL", SourceRefId = 201, TotalAmount = 50m, TotalLocalDebit = 50m, TotalLocalCredit = 50m,
            Details = [
                new GlVoucherDetail { VoucherId = 202, LineSer = 1, BranchId = 2, CurrencyId = 9, AccountCode = "NET", Credit = 50m, LocalCredit = 50m, BaseCredit = 50m },
                new GlVoucherDetail { VoucherId = 202, LineSer = 2, BranchId = 2, CurrencyId = 9, AccountCode = "CASH", Debit = 50m, LocalDebit = 50m, BaseDebit = 50m }
            ]
        };

        harness.VoucherRepo.Setup(v => v.GetBySourceAsync(2, "HR_PAYMENT", 97, It.IsAny<CancellationToken>()))
            .ReturnsAsync([originalPaymentVoucher]);
        harness.VoucherRepo.Setup(v => v.GetByIdAsync(202, It.IsAny<CancellationToken>()))
            .ReturnsAsync(reversalPaymentVoucher);
        harness.VoucherRepo.Setup(v => v.GetBySourceAsync(2, "GL_REVERSAL", 201, It.IsAny<CancellationToken>()))
            .ReturnsAsync([reversalPaymentVoucher]);

        await using (var setupDb = harness.CreateDb())
        {
            setupDb.AccountingHandoffPayments.Add(new AccountingHandoffPayment
            {
                Id = 97, HandoffId = 11, Amount = 50m, CurrencyCode = "USD", Method = "BANK",
                IdempotencyKey = "pay-rev-97", Status = "REVERSED", ReversalVoucherId = 202,
                ReversalReason = "Valid reason", ReversalExternalReference = "EXT-REF", ReversalEvidenceReference = "EVID-REF",
                ReversedAtUtc = DateTime.UtcNow, ReversedBy = "accounting", CreatedAtUtc = DateTime.UtcNow, CreatedBy = "accounting"
            });
            await setupDb.SaveChangesAsync();
        }

        await using var db = harness.CreateDb();
        var service = harness.CreateService(db);
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.ReverseJournalVoucherAsync(11, DateTime.UtcNow.Date, "Payroll correction needed", "accounting-user"));
        Assert.Contains("does not have a verifiable exact Accounting reversal", ex.Message);
    }

    [Fact]
    public async Task ReverseJournalVoucherAsync_WhenReversedPaymentOriginalVoucherNotMarkedReversed_ThrowsInvalidOperationException()
    {
        using var harness = new JournalReversalHarness();
        await harness.SeedAdvanceAsync();

        var originalPaymentVoucher = new GlVoucherHeader
        {
            Id = 201, BranchId = 2, Status = 3, IsReversed = false,
            SourceSystemCode = "HR_PAYMENT", SourceRefId = 98, TotalAmount = 50m, TotalLocalDebit = 50m, TotalLocalCredit = 50m,
            Details = [
                new GlVoucherDetail { VoucherId = 201, LineSer = 1, BranchId = 2, CurrencyId = 9, AccountCode = "NET", Debit = 50m, LocalDebit = 50m, BaseDebit = 50m },
                new GlVoucherDetail { VoucherId = 201, LineSer = 2, BranchId = 2, CurrencyId = 9, AccountCode = "CASH", Credit = 50m, LocalCredit = 50m, BaseCredit = 50m }
            ]
        };
        var reversalPaymentVoucher = new GlVoucherHeader
        {
            Id = 202, BranchId = 2, Status = 3, IsReversed = false, PostDate = DateTime.UtcNow,
            SourceSystemCode = "GL_REVERSAL", SourceRefId = 201, TotalAmount = 50m, TotalLocalDebit = 50m, TotalLocalCredit = 50m,
            Details = [
                new GlVoucherDetail { VoucherId = 202, LineSer = 1, BranchId = 2, CurrencyId = 9, AccountCode = "NET", Credit = 50m, LocalCredit = 50m, BaseCredit = 50m },
                new GlVoucherDetail { VoucherId = 202, LineSer = 2, BranchId = 2, CurrencyId = 9, AccountCode = "CASH", Debit = 50m, LocalDebit = 50m, BaseDebit = 50m }
            ]
        };

        harness.VoucherRepo.Setup(v => v.GetBySourceAsync(2, "HR_PAYMENT", 98, It.IsAny<CancellationToken>()))
            .ReturnsAsync([originalPaymentVoucher]);
        harness.VoucherRepo.Setup(v => v.GetByIdAsync(202, It.IsAny<CancellationToken>()))
            .ReturnsAsync(reversalPaymentVoucher);
        harness.VoucherRepo.Setup(v => v.GetBySourceAsync(2, "GL_REVERSAL", 201, It.IsAny<CancellationToken>()))
            .ReturnsAsync([reversalPaymentVoucher]);

        await using (var setupDb = harness.CreateDb())
        {
            setupDb.AccountingHandoffPayments.Add(new AccountingHandoffPayment
            {
                Id = 98, HandoffId = 11, Amount = 50m, CurrencyCode = "USD", Method = "BANK",
                IdempotencyKey = "pay-rev-98", Status = "REVERSED", ReversalVoucherId = 202,
                ReversalReason = "Valid reason", ReversalExternalReference = "EXT-REF", ReversalEvidenceReference = "EVID-REF",
                ReversedAtUtc = DateTime.UtcNow, ReversedBy = "accounting", CreatedAtUtc = DateTime.UtcNow, CreatedBy = "accounting"
            });
            await setupDb.SaveChangesAsync();
        }

        await using var db = harness.CreateDb();
        var service = harness.CreateService(db);
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.ReverseJournalVoucherAsync(11, DateTime.UtcNow.Date, "Payroll correction needed", "accounting-user"));
        Assert.Contains("does not have a verifiable exact Accounting reversal", ex.Message);
    }

    [Fact]
    public async Task ReverseJournalVoucherAsync_WhenReversedPaymentHasMultipleReversalsInAccounting_ThrowsInvalidOperationException()
    {
        using var harness = new JournalReversalHarness();
        await harness.SeedAdvanceAsync();

        var originalPaymentVoucher = new GlVoucherHeader
        {
            Id = 201, BranchId = 2, Status = 3, IsReversed = true,
            SourceSystemCode = "HR_PAYMENT", SourceRefId = 99, TotalAmount = 50m, TotalLocalDebit = 50m, TotalLocalCredit = 50m,
            Details = [
                new GlVoucherDetail { VoucherId = 201, LineSer = 1, BranchId = 2, CurrencyId = 9, AccountCode = "NET", Debit = 50m, LocalDebit = 50m, BaseDebit = 50m },
                new GlVoucherDetail { VoucherId = 201, LineSer = 2, BranchId = 2, CurrencyId = 9, AccountCode = "CASH", Credit = 50m, LocalCredit = 50m, BaseCredit = 50m }
            ]
        };
        var reversal1 = new GlVoucherHeader { Id = 202, BranchId = 2, Status = 3, SourceSystemCode = "GL_REVERSAL", SourceRefId = 201 };
        var reversal2 = new GlVoucherHeader { Id = 203, BranchId = 2, Status = 3, SourceSystemCode = "GL_REVERSAL", SourceRefId = 201 };

        harness.VoucherRepo.Setup(v => v.GetBySourceAsync(2, "HR_PAYMENT", 99, It.IsAny<CancellationToken>()))
            .ReturnsAsync([originalPaymentVoucher]);
        harness.VoucherRepo.Setup(v => v.GetByIdAsync(202, It.IsAny<CancellationToken>()))
            .ReturnsAsync(reversal1);
        harness.VoucherRepo.Setup(v => v.GetBySourceAsync(2, "GL_REVERSAL", 201, It.IsAny<CancellationToken>()))
            .ReturnsAsync([reversal1, reversal2]);

        await using (var setupDb = harness.CreateDb())
        {
            setupDb.AccountingHandoffPayments.Add(new AccountingHandoffPayment
            {
                Id = 99, HandoffId = 11, Amount = 50m, CurrencyCode = "USD", Method = "BANK",
                IdempotencyKey = "pay-rev-99", Status = "REVERSED", ReversalVoucherId = 202,
                ReversalReason = "Valid reason", ReversalExternalReference = "EXT-REF", ReversalEvidenceReference = "EVID-REF",
                ReversedAtUtc = DateTime.UtcNow, ReversedBy = "accounting", CreatedAtUtc = DateTime.UtcNow, CreatedBy = "accounting"
            });
            await setupDb.SaveChangesAsync();
        }

        await using var db = harness.CreateDb();
        var service = harness.CreateService(db);
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.ReverseJournalVoucherAsync(11, DateTime.UtcNow.Date, "Payroll correction needed", "accounting-user"));
        Assert.Contains("does not have a verifiable exact Accounting reversal", ex.Message);
    }

    [Fact]
    public async Task ReverseJournalVoucherAsync_WhenReversedPaymentReversalVoucherDoesNotInvertOriginalPayment_ThrowsInvalidOperationException()
    {
        using var harness = new JournalReversalHarness();
        await harness.SeedAdvanceAsync();

        var originalPaymentVoucher = new GlVoucherHeader
        {
            Id = 201, BranchId = 2, Status = 3, IsReversed = true,
            SourceSystemCode = "HR_PAYMENT", SourceRefId = 100, TotalAmount = 50m, TotalLocalDebit = 50m, TotalLocalCredit = 50m,
            Details = [
                new GlVoucherDetail { VoucherId = 201, LineSer = 1, BranchId = 2, CurrencyId = 9, AccountCode = "NET", Debit = 50m, LocalDebit = 50m, BaseDebit = 50m },
                new GlVoucherDetail { VoucherId = 201, LineSer = 2, BranchId = 2, CurrencyId = 9, AccountCode = "CASH", Credit = 50m, LocalCredit = 50m, BaseCredit = 50m }
            ]
        };
        var corruptedReversal = new GlVoucherHeader
        {
            Id = 202, BranchId = 2, Status = 3, IsReversed = false, PostDate = DateTime.UtcNow,
            SourceSystemCode = "GL_REVERSAL", SourceRefId = 201, TotalAmount = 50m, TotalLocalDebit = 50m, TotalLocalCredit = 50m,
            Details = [
                new GlVoucherDetail { VoucherId = 202, LineSer = 1, BranchId = 2, CurrencyId = 9, AccountCode = "NET", Credit = 49m, LocalCredit = 49m, BaseCredit = 49m },
                new GlVoucherDetail { VoucherId = 202, LineSer = 2, BranchId = 2, CurrencyId = 9, AccountCode = "CASH", Debit = 49m, LocalDebit = 49m, BaseDebit = 49m }
            ]
        };

        harness.VoucherRepo.Setup(v => v.GetBySourceAsync(2, "HR_PAYMENT", 100, It.IsAny<CancellationToken>()))
            .ReturnsAsync([originalPaymentVoucher]);
        harness.VoucherRepo.Setup(v => v.GetByIdAsync(202, It.IsAny<CancellationToken>()))
            .ReturnsAsync(corruptedReversal);
        harness.VoucherRepo.Setup(v => v.GetBySourceAsync(2, "GL_REVERSAL", 201, It.IsAny<CancellationToken>()))
            .ReturnsAsync([corruptedReversal]);

        await using (var setupDb = harness.CreateDb())
        {
            setupDb.AccountingHandoffPayments.Add(new AccountingHandoffPayment
            {
                Id = 100, HandoffId = 11, Amount = 50m, CurrencyCode = "USD", Method = "BANK",
                IdempotencyKey = "pay-rev-100", Status = "REVERSED", ReversalVoucherId = 202,
                ReversalReason = "Valid reason", ReversalExternalReference = "EXT-REF", ReversalEvidenceReference = "EVID-REF",
                ReversedAtUtc = DateTime.UtcNow, ReversedBy = "accounting", CreatedAtUtc = DateTime.UtcNow, CreatedBy = "accounting"
            });
            await setupDb.SaveChangesAsync();
        }

        await using var db = harness.CreateDb();
        var service = harness.CreateService(db);
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.ReverseJournalVoucherAsync(11, DateTime.UtcNow.Date, "Payroll correction needed", "accounting-user"));
        Assert.Contains("does not have a verifiable exact Accounting reversal", ex.Message);
    }

    private sealed class JournalReversalHarness : IDisposable
    {
        private readonly DbContextOptions<OracleDbContext> options = new DbContextOptionsBuilder<OracleDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;

        private readonly Mock<IGlVoucherRepository> voucherRepo = new();
        private readonly Mock<IGlVoucherService> glVouchers = new();
        private bool reversalCreated;
        public GlVoucherHeader? CreatedReversal { get; private set; }

        private readonly GlVoucherHeader originalJournal = new()
        {
            Id = 7,
            BranchId = 2,
            FiscalYearId = 1,
            VoucherType = 1,
            VoucherNo = 1001,
            SourceSystemCode = "HR_HANDOFF",
            SourceRefId = 11,
            Status = 3,
            PostDate = new DateTime(2026, 11, 7, 10, 0, 0, DateTimeKind.Utc),
            TotalAmount = 100m,
            TotalLocalDebit = 100m,
            TotalLocalCredit = 100m,
            Details =
            [
                new GlVoucherDetail { VoucherId = 7, LineSer = 1, BranchId = 2, CurrencyId = 9, AccountCode = "SALARY", Debit = 100m, LocalDebit = 100m, BaseDebit = 100m },
                new GlVoucherDetail { VoucherId = 7, LineSer = 2, BranchId = 2, CurrencyId = 9, AccountCode = "ADVANCE-GL", Credit = 40m, LocalCredit = 40m, BaseCredit = 40m },
                new GlVoucherDetail { VoucherId = 7, LineSer = 3, BranchId = 2, CurrencyId = 9, AccountCode = "NET", Credit = 60m, LocalCredit = 60m, BaseCredit = 60m }
            ]
        };

        private readonly GlVoucherHeader reversalJournal = new()
        {
            Id = 8,
            BranchId = 2,
            FiscalYearId = 1,
            VoucherType = 1,
            VoucherNo = 1002,
            SourceSystemCode = "GL_REVERSAL",
            SourceRefId = 7,
            Status = 3,
            PostDate = new DateTime(2026, 11, 7, 11, 0, 0, DateTimeKind.Utc),
            TotalAmount = 100m,
            TotalLocalDebit = 100m,
            TotalLocalCredit = 100m,
            Details =
            [
                new GlVoucherDetail { VoucherId = 8, LineSer = 1, BranchId = 2, CurrencyId = 9, AccountCode = "SALARY", Credit = 100m, LocalCredit = 100m, BaseCredit = 100m },
                new GlVoucherDetail { VoucherId = 8, LineSer = 2, BranchId = 2, CurrencyId = 9, AccountCode = "ADVANCE-GL", Debit = 40m, LocalDebit = 40m, BaseDebit = 40m },
                new GlVoucherDetail { VoucherId = 8, LineSer = 3, BranchId = 2, CurrencyId = 9, AccountCode = "NET", Debit = 60m, LocalDebit = 60m, BaseDebit = 60m }
            ]
        };

        public Mock<IGlVoucherRepository> VoucherRepo => voucherRepo;
        public Mock<IGlVoucherService> GlVouchers => glVouchers;
        public OracleDbContext CreateDb() => new(options);

        public void CorruptReversal()
        {
            reversalJournal.Details[0].Credit = 99m;
        }

        public HrAccountingHandoffService CreateService(OracleDbContext db, IGlVoucherService? voucherService = null) => new(
            db,
            voucherRepo.Object,
            Mock.Of<IHrPayrollPostingService>(),
            Mock.Of<IReceiptPaymentVoucherService>(),
            voucherService ?? glVouchers.Object);

        public IGlVoucherService CreateRealGlVoucherService()
        {
            voucherRepo.Setup(v => v.GetVoucherTypeByCodeAsync(1, It.IsAny<CancellationToken>()))
                .ReturnsAsync(new GlVoucherType { Id = 1, TypeCode = 1, TypeKey = "JV", Prefix = "JV", SerialResetPolicy = "MONTHLY" });
            voucherRepo.Setup(v => v.GenerateNextSerialNoAsync(2, It.IsAny<int>(), It.IsAny<int>(), 1, "MONTHLY", It.IsAny<CancellationToken>()))
                .ReturnsAsync(1002L);
            voucherRepo.Setup(v => v.AddVoucherAsync(It.IsAny<GlVoucherHeader>(), It.IsAny<CancellationToken>()))
                .Callback<GlVoucherHeader, CancellationToken>((voucher, _) =>
                {
                    voucher.Id = 8;
                    CreatedReversal = voucher;
                    reversalCreated = true;
                })
                .Returns(Task.CompletedTask);
            voucherRepo.Setup(v => v.SaveChangesAsync(It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);

            return new GlVoucherService(voucherRepo.Object,
                Mock.Of<IGlAccountRepository>(),
                Mock.Of<IGlCostCenterRepository>(),
                Mock.Of<IGlFiscalPeriodRepository>(),
                Mock.Of<IGlAccountBalanceRepository>(),
                Mock.Of<IArSubledgerRepository>(),
                Mock.Of<IApSubledgerRepository>(),
                Mock.Of<ICurrentTenantContext>(),
                NullLogger<GlVoucherService>.Instance);
        }

        private void SetupMocks()
        {
            voucherRepo.Setup(v => v.GetBySourceAsync(It.IsAny<long>(), "HR_PAYMENT", It.IsAny<long>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new List<GlVoucherHeader>());
            voucherRepo.Setup(v => v.GetByIdAsync(7, It.IsAny<CancellationToken>())).ReturnsAsync(originalJournal);
            voucherRepo.Setup(v => v.GetByIdAsync(8, It.IsAny<CancellationToken>())).ReturnsAsync(reversalJournal);
            voucherRepo.Setup(v => v.GetBySourceAsync(2, "HR_HANDOFF", 11, It.IsAny<CancellationToken>()))
                .ReturnsAsync([originalJournal]);
            voucherRepo.Setup(v => v.GetBySourceAsync(2, "GL_REVERSAL", 7, It.IsAny<CancellationToken>()))
                .Returns(() => Task.FromResult<IReadOnlyList<GlVoucherHeader>>(reversalCreated ? [reversalJournal] : Array.Empty<GlVoucherHeader>()));

            glVouchers.Setup(v => v.ReverseVoucherAsync(7, It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .Callback(() =>
                {
                    originalJournal.IsReversed = true;
                    reversalCreated = true;
                })
                .ReturnsAsync(new GlVoucherHeaderDto());
        }

        public async Task SeedAdvanceAsync()
        {
            SetupMocks();
            await using var db = CreateDb();
            var snapshotJson = JsonSerializer.Serialize(new[]
            {
                new
                {
                    Kind = "ADVANCE",
                    SourceId = 41L,
                    Amount = 40m,
                    PayrollRunLineId = 501L,
                    PriorStatus = "ACTIVE",
                    PriorDeductedAmount = (decimal?)0m,
                    PriorRemainingBalance = (decimal?)100m,
                    PriorPaidAmount = (decimal?)null,
                    PriorCarriedForwardAmount = (decimal?)null,
                    PriorPaidDate = (DateTime?)null,
                    PriorSchedulePayrollLineId = (long?)null,
                    PriorLoanTotalPaidAmount = (decimal?)null,
                    PriorLoanRemainingBalance = (decimal?)null,
                    PriorLoanStatus = (string?)null
                }
            });

            db.HrAccountingHandoffs.Add(new HrAccountingHandoff
            {
                Id = 11, CompanyId = 3, BranchId = 2, SourceType = "PAYROLL", SourceId = "9", SourceVersion = 1,
                Operation = "SUBMIT", PayPeriod = "2026-11", HandoffReference = "HR-PAY-3-9-1", PayloadHash = "payload",
                DeliveryStatus = "POSTED", CreatedAtUtc = DateTime.UtcNow, CreatedBy = "hr",
                Lines = [new HrAccountingHandoffLine
                {
                    LineType = "REFERENCE:ADVANCE", CurrencyCode = "USD", Amount = 40m, BranchId = 2,
                    SourceReference = "ADVANCE:41:PAYROLL_LINE:501"
                }]
            });
            db.AccountingHandoffJournalLinks.Add(new AccountingHandoffJournalLink
            {
                Id = 21, HandoffId = 11, VoucherId = 7, CreatedAtUtc = DateTime.UtcNow, CreatedBy = "accounting"
            });
            db.AccountingHandoffPostingReceipts.Add(new AccountingHandoffPostingReceipt
            {
                Id = 31, HandoffId = 11, VoucherId = 7, EventKey = "posting-event-001",
                SettlementSnapshotJson = snapshotJson, ReceivedAtUtc = DateTime.UtcNow, ReceivedBy = "accounting"
            });
            db.EmployeeAdvances.Add(new EmployeeAdvance
            {
                Id = 41, EmployeeCode = "E-501", AdvanceAmount = 100m, CurrencyCode = "USD", TargetPayPeriod = "2026-11",
                DeductedAmount = 40m, RemainingBalance = 60m, Status = "ACTIVE", ReservedByHandoffId = null
            });
            await db.SaveChangesAsync();
        }

        public async Task SeedLoanScheduleAsync()
        {
            SetupMocks();
            await using var db = CreateDb();
            var snapshotJson = JsonSerializer.Serialize(new[]
            {
                new
                {
                    Kind = "SCHEDULE",
                    SourceId = 101L,
                    Amount = 100m,
                    PayrollRunLineId = 502L,
                    PriorStatus = "PENDING",
                    PriorDeductedAmount = (decimal?)null,
                    PriorRemainingBalance = (decimal?)null,
                    PriorPaidAmount = (decimal?)0m,
                    PriorCarriedForwardAmount = (decimal?)0m,
                    PriorPaidDate = (DateTime?)null,
                    PriorSchedulePayrollLineId = (long?)null,
                    PriorLoanTotalPaidAmount = (decimal?)0m,
                    PriorLoanRemainingBalance = (decimal?)500m,
                    PriorLoanStatus = (string?)"ACTIVE"
                }
            });

            db.HrAccountingHandoffs.Add(new HrAccountingHandoff
            {
                Id = 11, CompanyId = 3, BranchId = 2, SourceType = "PAYROLL", SourceId = "9", SourceVersion = 1,
                Operation = "SUBMIT", PayPeriod = "2026-11", HandoffReference = "HR-PAY-3-9-1", PayloadHash = "payload",
                DeliveryStatus = "POSTED", CreatedAtUtc = DateTime.UtcNow, CreatedBy = "hr",
                Lines = [new HrAccountingHandoffLine
                {
                    LineType = "REFERENCE:SCHEDULE", CurrencyCode = "USD", Amount = 100m, BranchId = 2,
                    SourceReference = "SCHEDULE:101:PAYROLL_LINE:502"
                }]
            });
            db.AccountingHandoffJournalLinks.Add(new AccountingHandoffJournalLink
            {
                Id = 21, HandoffId = 11, VoucherId = 7, CreatedAtUtc = DateTime.UtcNow, CreatedBy = "accounting"
            });
            db.AccountingHandoffPostingReceipts.Add(new AccountingHandoffPostingReceipt
            {
                Id = 31, HandoffId = 11, VoucherId = 7, EventKey = "posting-event-001",
                SettlementSnapshotJson = snapshotJson, ReceivedAtUtc = DateTime.UtcNow, ReceivedBy = "accounting"
            });
            var loan = new EmployeeLoan
            {
                Id = 51, EmployeeCode = "E-502", PrincipalAmount = 500m, CurrencyCode = "USD",
                RemainingBalance = 400m, TotalPaidAmount = 100m, Status = "ACTIVE"
            };
            db.EmployeeLoans.Add(loan);
            db.LoanRepaymentSchedules.Add(new LoanRepaymentSchedule
            {
                Id = 101, EmployeeLoanId = 51, InstallmentNo = 1, PayPeriod = "2026-11",
                ScheduledAmount = 100m, PaidAmount = 100m, CarriedForwardAmount = 0m,
                Status = "PAID", PaidDate = DateTime.UtcNow, PayrollRunLineId = 502L,
                ReservedByHandoffId = null, EmployeeLoan = loan
            });
            await db.SaveChangesAsync();
        }

        public async Task SeedBothAdvanceAndLoanAsync()
        {
            SetupMocks();
            await using var db = CreateDb();
            var snapshotJson = JsonSerializer.Serialize(new[]
            {
                new
                {
                    Kind = "ADVANCE",
                    SourceId = 41L,
                    Amount = 40m,
                    PayrollRunLineId = 501L,
                    PriorStatus = "ACTIVE",
                    PriorDeductedAmount = (decimal?)0m,
                    PriorRemainingBalance = (decimal?)100m,
                    PriorPaidAmount = (decimal?)null,
                    PriorCarriedForwardAmount = (decimal?)null,
                    PriorPaidDate = (DateTime?)null,
                    PriorSchedulePayrollLineId = (long?)null,
                    PriorLoanTotalPaidAmount = (decimal?)null,
                    PriorLoanRemainingBalance = (decimal?)null,
                    PriorLoanStatus = (string?)null
                },
                new
                {
                    Kind = "SCHEDULE",
                    SourceId = 101L,
                    Amount = 60m,
                    PayrollRunLineId = 502L,
                    PriorStatus = "PENDING",
                    PriorDeductedAmount = (decimal?)null,
                    PriorRemainingBalance = (decimal?)null,
                    PriorPaidAmount = (decimal?)0m,
                    PriorCarriedForwardAmount = (decimal?)0m,
                    PriorPaidDate = (DateTime?)null,
                    PriorSchedulePayrollLineId = (long?)null,
                    PriorLoanTotalPaidAmount = (decimal?)0m,
                    PriorLoanRemainingBalance = (decimal?)500m,
                    PriorLoanStatus = (string?)"ACTIVE"
                }
            });

            db.HrAccountingHandoffs.Add(new HrAccountingHandoff
            {
                Id = 11, CompanyId = 3, BranchId = 2, SourceType = "PAYROLL", SourceId = "9", SourceVersion = 1,
                Operation = "SUBMIT", PayPeriod = "2026-11", HandoffReference = "HR-PAY-3-9-1", PayloadHash = "payload",
                DeliveryStatus = "POSTED", CreatedAtUtc = DateTime.UtcNow, CreatedBy = "hr",
                Lines =
                [
                    new HrAccountingHandoffLine
                    {
                        LineType = "REFERENCE:ADVANCE", CurrencyCode = "USD", Amount = 40m, BranchId = 2,
                        SourceReference = "ADVANCE:41:PAYROLL_LINE:501"
                    },
                    new HrAccountingHandoffLine
                    {
                        LineType = "REFERENCE:SCHEDULE", CurrencyCode = "USD", Amount = 60m, BranchId = 2,
                        SourceReference = "SCHEDULE:101:PAYROLL_LINE:502"
                    }
                ]
            });
            db.AccountingHandoffJournalLinks.Add(new AccountingHandoffJournalLink
            {
                Id = 21, HandoffId = 11, VoucherId = 7, CreatedAtUtc = DateTime.UtcNow, CreatedBy = "accounting"
            });
            db.AccountingHandoffPostingReceipts.Add(new AccountingHandoffPostingReceipt
            {
                Id = 31, HandoffId = 11, VoucherId = 7, EventKey = "posting-event-001",
                SettlementSnapshotJson = snapshotJson, ReceivedAtUtc = DateTime.UtcNow, ReceivedBy = "accounting"
            });
            db.EmployeeAdvances.Add(new EmployeeAdvance
            {
                Id = 41, EmployeeCode = "E-501", AdvanceAmount = 100m, CurrencyCode = "USD", TargetPayPeriod = "2026-11",
                DeductedAmount = 40m, RemainingBalance = 60m, Status = "ACTIVE", ReservedByHandoffId = null
            });
            var loan = new EmployeeLoan
            {
                Id = 51, EmployeeCode = "E-502", PrincipalAmount = 500m, CurrencyCode = "USD",
                RemainingBalance = 440m, TotalPaidAmount = 60m, Status = "ACTIVE"
            };
            db.EmployeeLoans.Add(loan);
            db.LoanRepaymentSchedules.Add(new LoanRepaymentSchedule
            {
                Id = 101, EmployeeLoanId = 51, InstallmentNo = 1, PayPeriod = "2026-11",
                ScheduledAmount = 60m, PaidAmount = 60m, CarriedForwardAmount = 0m,
                Status = "PAID", PaidDate = DateTime.UtcNow, PayrollRunLineId = 502L,
                ReservedByHandoffId = null, EmployeeLoan = loan
            });
            await db.SaveChangesAsync();
        }

        public void Dispose() { }
    }
}
