using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Moq;
using ThinkOnErp.Application.DTOs.Accounting.Vouchers;
using ThinkOnErp.Application.Services.Accounting;
using ThinkOnErp.Application.Services.Hr;
using ThinkOnErp.Domain.Entities;
using ThinkOnErp.Domain.Entities.Accounting;
using ThinkOnErp.Domain.Entities.Hr;
using ThinkOnErp.Domain.Interfaces.Accounting;
using ThinkOnErp.Infrastructure.Data;
using ThinkOnErp.Infrastructure.Services.Hr;
using Xunit;

namespace ThinkOnErp.HR.Api.Tests;

public sealed class AccountingHandoffFundingReversalTests
{
    [Fact]
    public async Task FundingReversalRestoresApprovedAdvanceAndExactReplayReturnsSameVoucher()
    {
        using var harness = new Harness();
        await harness.SeedAdvanceAsync();

        long reversalId;
        await using (var db = harness.CreateDb())
        {
            var service = harness.CreateService(db);
            reversalId = await service.ReverseLoanFundingAsync(11, "Duplicate disbursement", "fund-rev-event-001",
                "BANK-REV-77", "REV-EVIDENCE-77", "accounting");
            Assert.Equal(72, reversalId);
        }

        await using (var verify = harness.CreateDb())
        {
            var handoff = await verify.HrAccountingHandoffs.SingleAsync();
            var receipt = await verify.AccountingLoanFundingReceipts.SingleAsync();
            var advance = await verify.EmployeeAdvances.SingleAsync();
            Assert.Equal("REVERSED", handoff.DeliveryStatus);
            Assert.Equal(72, receipt.ReversalVoucherId);
            Assert.Equal("fund-rev-event-001", receipt.ReversalEventKey);
            Assert.Equal("Duplicate disbursement", receipt.ReversalReason);
            Assert.Equal("BANK-REV-77", receipt.ReversalExternalReference);
            Assert.Equal("REV-EVIDENCE-77", receipt.ReversalEvidenceReference);
            Assert.Equal("accounting", receipt.ReversedBy);
            Assert.Equal("APPROVED", advance.Status);
            Assert.Equal(0m, advance.RemainingBalance);
            Assert.Single(await verify.HrAccountingHandoffTransitions.Where(t => t.ToStatus == "REVERSED").ToListAsync());
        }

        await using (var db = harness.CreateDb())
        {
            reversalId = await harness.CreateService(db).ReverseLoanFundingAsync(11, "Duplicate disbursement", "fund-rev-event-001",
                "BANK-REV-77", "REV-EVIDENCE-77", "retry");
            Assert.Equal(72, reversalId);
        }
        harness.GlVoucherService.Verify(s => s.ReverseVoucherAsync(7, "accounting", "Duplicate disbursement", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ReverseLoanFunding_ExecutesInsideEmployeeAndAffectedPayrollDateLock()
    {
        using var harness = new Harness();
        await harness.SeedAdvanceAsync();
        var unitOfWork = new Mock<IHrUnitOfWork>();
        unitOfWork.Setup(u => u.ExecuteForEmployeeDatesAsync(
                "E-41", new DateTime(2026, 11, 1), new DateTime(2026, 11, 30),
                It.IsAny<Func<Task<long>>>(), It.IsAny<CancellationToken>()))
            .Returns((string _, DateTime _, DateTime _, Func<Task<long>> action, CancellationToken _) => action());

        await using var db = harness.CreateDb();
        var reversalId = await harness.CreateService(db, unitOfWork.Object).ReverseLoanFundingAsync(
            11, "Duplicate disbursement", "fund-rev-event-001", "BANK-REV-77", "REV-EVIDENCE-77", "accounting");

        Assert.Equal(72, reversalId);
        unitOfWork.Verify(u => u.ExecuteForEmployeeDatesAsync(
            "E-41", new DateTime(2026, 11, 1), new DateTime(2026, 11, 30),
            It.IsAny<Func<Task<long>>>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task FundingCannotBeReversedWhileAdvanceHasPayrollDeductions()
    {
        using var harness = new Harness();
        await harness.SeedAdvanceAsync();
        await using (var db = harness.CreateDb())
        {
            var advance = await db.EmployeeAdvances.SingleAsync();
            advance.DeductedAmount = 10m;
            advance.RemainingBalance = 90m;
            await db.SaveChangesAsync();
        }

        await using (var db = harness.CreateDb())
        {
            await Assert.ThrowsAsync<InvalidOperationException>(() => harness.CreateService(db).ReverseLoanFundingAsync(
                11, "Duplicate disbursement", "fund-rev-event-001", "BANK-REV-77", "REV-EVIDENCE-77", "accounting"));
        }

        await using var verify = harness.CreateDb();
        Assert.Equal("POSTED", (await verify.HrAccountingHandoffs.SingleAsync()).DeliveryStatus);
        Assert.Null((await verify.AccountingLoanFundingReceipts.SingleAsync()).ReversalVoucherId);
        harness.GlVoucherService.Verify(s => s.ReverseVoucherAsync(It.IsAny<long>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task FundingReversalRestoresApprovedLoanAndSchedulesAndReplayIsIdempotent()
    {
        using var harness = new Harness(amount: 500m);
        await harness.SeedLoanAsync();

        long reversalId;
        await using (var db = harness.CreateDb())
        {
            var service = harness.CreateService(db);
            reversalId = await service.ReverseLoanFundingAsync(12, "Reversing loan funding", "loan-rev-evt-001",
                "BANK-REV-51", "REV-EVIDENCE-51", "accounting");
            Assert.Equal(72, reversalId);
        }

        await using (var verify = harness.CreateDb())
        {
            var handoff = await verify.HrAccountingHandoffs.SingleAsync(h => h.Id == 12);
            var receipt = await verify.AccountingLoanFundingReceipts.SingleAsync(r => r.HandoffId == 12);
            var loan = await verify.EmployeeLoans.SingleAsync(l => l.Id == 51);
            Assert.Equal("REVERSED", handoff.DeliveryStatus);
            Assert.Equal(72, receipt.ReversalVoucherId);
            Assert.Equal("APPROVED", loan.Status);
            Assert.Equal(0m, loan.RemainingBalance);
        }

        // Idempotent retry
        await using (var db = harness.CreateDb())
        {
            reversalId = await harness.CreateService(db).ReverseLoanFundingAsync(12, "Reversing loan funding", "loan-rev-evt-001",
                "BANK-REV-51", "REV-EVIDENCE-51", "retry");
            Assert.Equal(72, reversalId);
        }
        harness.GlVoucherService.Verify(s => s.ReverseVoucherAsync(7, "accounting", "Reversing loan funding", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task FundingCannotBeReversedWhileLoanHasRepaymentsOrReservations()
    {
        using var harness = new Harness(amount: 500m);
        await harness.SeedLoanAsync();
        await using (var db = harness.CreateDb())
        {
            var loan = await db.EmployeeLoans.Include(l => l.Schedules).SingleAsync(l => l.Id == 51);
            loan.TotalPaidAmount = 50m;
            loan.RemainingBalance = 450m;
            loan.Schedules[0].PaidAmount = 50m;
            await db.SaveChangesAsync();
        }

        await using (var db = harness.CreateDb())
        {
            var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => harness.CreateService(db).ReverseLoanFundingAsync(
                12, "Reversing loan funding", "loan-rev-evt-001", "BANK-REV-51", "REV-EVIDENCE-51", "accounting"));
            Assert.Contains("Loan has repayments, reservations, schedule changes, or balance changes", ex.Message);
        }
    }

    [Theory]
    [InlineData("ok", "event-valid-key", "ext-ref", "evid-ref")] // Reason < 3 chars
    [InlineData("Valid reason", "short", "ext-ref", "evid-ref")] // EventKey < 8 chars
    [InlineData("Valid reason", "event-valid-key", "", "evid-ref")] // Empty external ref
    [InlineData("Valid reason", "event-valid-key", "ext-ref", "")] // Empty evidence ref
    public async Task FundingReversalRequiresValidReversalReasonAndReferences(string reason, string eventKey, string extRef, string evidRef)
    {
        using var harness = new Harness();
        await harness.SeedAdvanceAsync();

        await using var db = harness.CreateDb();
        var service = harness.CreateService(db);
        await Assert.ThrowsAsync<ArgumentException>(() =>
            service.ReverseLoanFundingAsync(11, reason, eventKey, extRef, evidRef, "user"));
    }

    [Fact]
    public async Task FundingReversalThrowsIfHandoffNotPosted()
    {
        using var harness = new Harness();
        await harness.SeedAdvanceAsync();
        await using (var db = harness.CreateDb())
        {
            var handoff = await db.HrAccountingHandoffs.SingleAsync();
            handoff.DeliveryStatus = "ACCEPTED";
            await db.SaveChangesAsync();
        }

        await using (var db = harness.CreateDb())
        {
            var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => harness.CreateService(db).ReverseLoanFundingAsync(
                11, "Valid reason", "event-valid-key", "ext-ref", "evid-ref", "user"));
            Assert.Contains("Only a funding handoff with a confirmed disbursement receipt can be reversed", ex.Message);
        }
    }

    [Fact]
    public async Task FundingReversalThrowsIfLegacyReceiptHasNoFundingVoucherId()
    {
        using var harness = new Harness();
        await harness.SeedAdvanceAsync();
        await using (var db = harness.CreateDb())
        {
            var receipt = await db.AccountingLoanFundingReceipts.SingleAsync();
            receipt.FundingVoucherId = null;
            await db.SaveChangesAsync();
        }

        await using (var db = harness.CreateDb())
        {
            var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => harness.CreateService(db).ReverseLoanFundingAsync(
                11, "Valid reason", "event-valid-key", "ext-ref", "evid-ref", "user"));
            Assert.Contains("legacy funding receipt has no linked Payment Voucher", ex.Message);
        }
    }

    [Fact]
    public async Task FundingReversalThrowsIfHandoffNotFound()
    {
        using var harness = new Harness();
        await harness.SeedAdvanceAsync();

        await using var db = harness.CreateDb();
        var service = harness.CreateService(db);
        var ex = await Assert.ThrowsAsync<KeyNotFoundException>(() =>
            service.ReverseLoanFundingAsync(9999, "Valid reason", "event-valid-key", "ext-ref", "evid-ref", "user"));
        Assert.Contains("Funding handoff not found", ex.Message);
    }

    [Fact]
    public async Task FundingReversalThrowsIfSourceTypeIsNotLoanOrAdvance()
    {
        using var harness = new Harness();
        await harness.SeedAdvanceAsync();
        await using (var db = harness.CreateDb())
        {
            var handoff = await db.HrAccountingHandoffs.SingleAsync();
            handoff.SourceType = "PAYROLL";
            await db.SaveChangesAsync();
        }

        await using (var db = harness.CreateDb())
        {
            var service = harness.CreateService(db);
            var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
                service.ReverseLoanFundingAsync(11, "Valid reason", "event-valid-key", "ext-ref", "evid-ref", "user"));
            Assert.Contains("Only a loan or advance funding handoff can be reversed here", ex.Message);
        }
    }

    [Fact]
    public async Task FundingReversalThrowsIfReversalEventKeyReusedByAnotherReversal()
    {
        using var harness = new Harness();
        await harness.SeedAdvanceAsync();
        await using (var db = harness.CreateDb())
        {
            db.AccountingLoanFundingReceipts.Add(new AccountingLoanFundingReceipt
            {
                HandoffId = 99,
                SourceType = "ADVANCE",
                SourceId = "999",
                SourceVersion = 1,
                FundedAmount = 100m,
                CurrencyCode = "USD",
                FundingVoucherId = 777,
                EventKey = "another-prior-event",
                ExternalReference = "PRIOR-EXT-77",
                ReversalEventKey = "already-used-reversal-key",
                ReversalExternalReference = "PRIOR-REV-EXT-77",
                ReceivedAtUtc = DateTime.UtcNow,
                ReceivedBy = "prior-user"
            });
            await db.SaveChangesAsync();
        }

        await using (var db = harness.CreateDb())
        {
            var service = harness.CreateService(db);
            var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
                service.ReverseLoanFundingAsync(11, "Valid reason", "already-used-reversal-key", "ext-ref-unique", "evid-ref", "user"));
            Assert.Contains("reversal event key or external reference is already used by another funding reversal", ex.Message);
        }
    }

    [Fact]
    public async Task FundingReversal_AdvanceWithReservations_ThrowsInvalidOperationException()
    {
        using var harness = new Harness();
        await harness.SeedAdvanceAsync();
        await using (var db = harness.CreateDb())
        {
            var advance = await db.EmployeeAdvances.SingleAsync();
            advance.ReservedByHandoffId = 555; // reserved by another handoff
            await db.SaveChangesAsync();
        }

        await using (var db = harness.CreateDb())
        {
            var service = harness.CreateService(db);
            var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
                service.ReverseLoanFundingAsync(11, "Valid reason", "event-valid-key", "ext-ref", "evid-ref", "user"));
            Assert.Contains("Advance has deductions, reservations, or balance changes; reverse later payroll settlements first", ex.Message);
        }
    }

    [Fact]
    public async Task FundingReversal_LoanScheduleWithReservations_ThrowsInvalidOperationException()
    {
        using var harness = new Harness(amount: 500m);
        await harness.SeedLoanAsync();
        await using (var db = harness.CreateDb())
        {
            var schedule = await db.LoanRepaymentSchedules.SingleAsync(s => s.Id == 151);
            schedule.ReservedByHandoffId = 666; // reserved by a payroll handoff
            await db.SaveChangesAsync();
        }

        await using (var db = harness.CreateDb())
        {
            var service = harness.CreateService(db);
            var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
                service.ReverseLoanFundingAsync(12, "Valid reason", "event-valid-key", "ext-ref", "evid-ref", "user"));
            Assert.Contains("Loan has repayments, reservations, schedule changes, or balance changes", ex.Message);
        }
    }

    [Theory]
    [InlineData(1001, 20, 20, 20)] // Reason > 1000
    [InlineData(20, 161, 20, 20)]  // EventKey > 160
    [InlineData(20, 20, 201, 20)]  // ExternalRef > 200
    [InlineData(20, 20, 20, 501)]  // EvidenceRef > 500
    public async Task FundingReversal_ExceedingMaximumLengths_ThrowsArgumentException(int reasonLen, int eventKeyLen, int extRefLen, int evidRefLen)
    {
        using var harness = new Harness();
        await harness.SeedAdvanceAsync();

        var reason = new string('A', reasonLen);
        var eventKey = new string('B', eventKeyLen);
        var extRef = new string('C', extRefLen);
        var evidRef = new string('D', evidRefLen);

        await using var db = harness.CreateDb();
        var service = harness.CreateService(db);
        await Assert.ThrowsAsync<ArgumentException>(() =>
            service.ReverseLoanFundingAsync(11, reason, eventKey, extRef, evidRef, "user"));
    }

    [Fact]
    public async Task FundingReversal_ReplayWithDifferentReason_ThrowsInvalidOperationException()
    {
        using var harness = new Harness();
        await harness.SeedAdvanceAsync();

        await using (var db = harness.CreateDb())
        {
            var service = harness.CreateService(db);
            await service.ReverseLoanFundingAsync(11, "Original reason", "rev-evt-001", "EXT-1", "EVID-1", "user");
        }

        await using (var db = harness.CreateDb())
        {
            var service = harness.CreateService(db);
            var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
                service.ReverseLoanFundingAsync(11, "Different reason", "rev-evt-001", "EXT-1", "EVID-1", "user"));
            Assert.Contains("A reversed funding handoff is immutable", ex.Message);
        }
    }

    [Fact]
    public async Task FundingReversal_ReplayWithDifferentReferences_ThrowsInvalidOperationException()
    {
        using var harness = new Harness();
        await harness.SeedAdvanceAsync();

        await using (var db = harness.CreateDb())
        {
            var service = harness.CreateService(db);
            await service.ReverseLoanFundingAsync(11, "Original reason", "rev-evt-001", "EXT-1", "EVID-1", "user");
        }

        await using (var db = harness.CreateDb())
        {
            var service = harness.CreateService(db);
            var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
                service.ReverseLoanFundingAsync(11, "Original reason", "rev-evt-001", "EXT-DIFFERENT", "EVID-1", "user"));
            Assert.Contains("A reversed funding handoff is immutable", ex.Message);
        }
    }

    [Fact]
    public async Task FundingReversal_MissingOriginalVoucher_ThrowsKeyNotFoundException()
    {
        using var harness = new Harness();
        await harness.SeedAdvanceAsync();
        harness.SetupOriginalVoucherNull();

        await using var db = harness.CreateDb();
        var service = harness.CreateService(db);
        var ex = await Assert.ThrowsAsync<KeyNotFoundException>(() =>
            service.ReverseLoanFundingAsync(11, "Valid reason", "rev-evt-001", "EXT-1", "EVID-1", "user"));
        Assert.Contains("Linked funding Payment Voucher is missing from Accounting", ex.Message);
    }

    [Fact]
    public async Task FundingReversal_VoucherNotPosted_ThrowsInvalidOperationException()
    {
        using var harness = new Harness();
        await harness.SeedAdvanceAsync();
        harness.SetupOriginalVoucherNotPosted();

        await using var db = harness.CreateDb();
        var service = harness.CreateService(db);
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.ReverseLoanFundingAsync(11, "Valid reason", "rev-evt-001", "EXT-1", "EVID-1", "user"));
        Assert.Contains("Linked funding Payment Voucher no longer matches the posted receipt", ex.Message);
    }

    [Fact]
    public async Task FundingReversal_VoucherBranchMismatch_ThrowsInvalidOperationException()
    {
        using var harness = new Harness();
        await harness.SeedAdvanceAsync();
        harness.SetupOriginalVoucherBranchMismatch();

        await using var db = harness.CreateDb();
        var service = harness.CreateService(db);
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.ReverseLoanFundingAsync(11, "Valid reason", "rev-evt-001", "EXT-1", "EVID-1", "user"));
        Assert.Contains("Linked funding Payment Voucher no longer matches the posted receipt", ex.Message);
    }

    [Fact]
    public async Task FundingReversal_VoucherCurrencyMismatch_ThrowsInvalidOperationException()
    {
        using var harness = new Harness();
        await harness.SeedAdvanceAsync();
        harness.SetupOriginalVoucherDetailsCurrencyMismatch();

        await using var db = harness.CreateDb();
        var service = harness.CreateService(db);
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.ReverseLoanFundingAsync(11, "Valid reason", "rev-evt-001", "EXT-1", "EVID-1", "user"));
        Assert.Contains("Linked funding Payment Voucher no longer matches the posted receipt", ex.Message);
    }

    [Fact]
    public async Task FundingReversal_VoucherWithSettledBills_ThrowsInvalidOperationException()
    {
        using var harness = new Harness();
        await harness.SeedAdvanceAsync();
        harness.SetupVoucherWithSettledBills();

        await using var db = harness.CreateDb();
        var service = harness.CreateService(db);
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.ReverseLoanFundingAsync(11, "Valid reason", "rev-evt-001", "EXT-1", "EVID-1", "user"));
        Assert.Contains("Linked funding Payment Voucher no longer matches the posted receipt", ex.Message);
    }

    [Fact]
    public async Task FundingReversal_LinkedAdvanceMissing_ThrowsKeyNotFoundException()
    {
        using var harness = new Harness();
        await harness.SeedAdvanceAsync();

        await using (var db = harness.CreateDb())
        {
            var advance = await db.EmployeeAdvances.SingleAsync();
            db.EmployeeAdvances.Remove(advance);
            await db.SaveChangesAsync();
        }

        await using (var db = harness.CreateDb())
        {
            var service = harness.CreateService(db);
            var ex = await Assert.ThrowsAsync<KeyNotFoundException>(() =>
                service.ReverseLoanFundingAsync(11, "Valid reason", "rev-evt-001", "EXT-1", "EVID-1", "user"));
            Assert.Contains("Linked advance is missing", ex.Message);
        }
    }

    [Fact]
    public async Task FundingReversal_LinkedLoanMissing_ThrowsKeyNotFoundException()
    {
        using var harness = new Harness(amount: 500m);
        await harness.SeedLoanAsync();

        await using (var db = harness.CreateDb())
        {
            var loan = await db.EmployeeLoans.SingleAsync();
            db.EmployeeLoans.Remove(loan);
            await db.SaveChangesAsync();
        }

        await using (var db = harness.CreateDb())
        {
            var service = harness.CreateService(db);
            var ex = await Assert.ThrowsAsync<KeyNotFoundException>(() =>
                service.ReverseLoanFundingAsync(12, "Valid reason", "rev-evt-001", "EXT-1", "EVID-1", "user"));
            Assert.Contains("Linked loan is missing", ex.Message);
        }
    }

    [Fact]
    public async Task FundingReversal_AdvanceBalanceChanged_ThrowsInvalidOperationException()
    {
        using var harness = new Harness();
        await harness.SeedAdvanceAsync();

        await using (var db = harness.CreateDb())
        {
            var advance = await db.EmployeeAdvances.SingleAsync();
            advance.RemainingBalance = 80m;
            await db.SaveChangesAsync();
        }

        await using (var db = harness.CreateDb())
        {
            var service = harness.CreateService(db);
            var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
                service.ReverseLoanFundingAsync(11, "Valid reason", "rev-evt-001", "EXT-1", "EVID-1", "user"));
            Assert.Contains("Advance has deductions, reservations, or balance changes", ex.Message);
        }
    }

    [Fact]
    public async Task FundingReversal_LoanBalanceChanged_ThrowsInvalidOperationException()
    {
        using var harness = new Harness(amount: 500m);
        await harness.SeedLoanAsync();

        await using (var db = harness.CreateDb())
        {
            var loan = await db.EmployeeLoans.SingleAsync();
            loan.RemainingBalance = 400m;
            await db.SaveChangesAsync();
        }

        await using (var db = harness.CreateDb())
        {
            var service = harness.CreateService(db);
            var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
                service.ReverseLoanFundingAsync(12, "Valid reason", "rev-evt-001", "EXT-1", "EVID-1", "user"));
            Assert.Contains("Loan has repayments, reservations, schedule changes, or balance changes", ex.Message);
        }
    }

    [Fact]
    public async Task FundingReversal_LoanScheduleStatusNotPending_ThrowsInvalidOperationException()
    {
        using var harness = new Harness(amount: 500m);
        await harness.SeedLoanAsync();

        await using (var db = harness.CreateDb())
        {
            var schedule = await db.LoanRepaymentSchedules.SingleAsync();
            schedule.Status = "PAID";
            await db.SaveChangesAsync();
        }

        await using (var db = harness.CreateDb())
        {
            var service = harness.CreateService(db);
            var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
                service.ReverseLoanFundingAsync(12, "Valid reason", "rev-evt-001", "EXT-1", "EVID-1", "user"));
            Assert.Contains("Loan has repayments, reservations, schedule changes, or balance changes", ex.Message);
        }
    }

    [Theory]
    [InlineData("AB")] // < 3 chars
    [InlineData("")]
    [InlineData("   ")]
    public async Task FundingReversal_RejectsWhenReasonTooShort(string reason)
    {
        using var harness = new Harness();
        await harness.SeedAdvanceAsync();

        await using var db = harness.CreateDb();
        var service = harness.CreateService(db);
        var ex = await Assert.ThrowsAsync<ArgumentException>(() =>
            service.ReverseLoanFundingAsync(11, reason, "valid-event-key", "EXT-REF", "EVID-REF", "user"));
        Assert.Contains("A reversal reason, unique event key, external reversal reference, and evidence reference are required", ex.Message);
    }

    [Fact]
    public async Task FundingReversal_RejectsWhenReasonTooLong()
    {
        using var harness = new Harness();
        await harness.SeedAdvanceAsync();

        var tooLongReason = new string('A', 1001);

        await using var db = harness.CreateDb();
        var service = harness.CreateService(db);
        var ex = await Assert.ThrowsAsync<ArgumentException>(() =>
            service.ReverseLoanFundingAsync(11, tooLongReason, "valid-event-key", "EXT-REF", "EVID-REF", "user"));
        Assert.Contains("A reversal reason, unique event key, external reversal reference, and evidence reference are required", ex.Message);
    }

    [Fact]
    public async Task FundingReversal_AcceptsMinAndMaxValidReasonLengths()
    {
        using var harness = new Harness();
        await harness.SeedAdvanceAsync();

        // Min length: 3 chars
        var minReason = "REV";
        await using (var db = harness.CreateDb())
        {
            var service = harness.CreateService(db);
            var revId = await service.ReverseLoanFundingAsync(11, minReason, "valid-event-key-min", "EXT-REF-MIN", "EVID-REF-MIN", "user");
            Assert.Equal(72, revId);
        }

        await using (var verify = harness.CreateDb())
        {
            var receipt = await verify.AccountingLoanFundingReceipts.SingleAsync();
            Assert.Equal("REV", receipt.ReversalReason);
        }
    }

    [Fact]
    public async Task FundingReversal_RejectsWhenEventKeyBoundaryViolated()
    {
        using var harness = new Harness();
        await harness.SeedAdvanceAsync();

        await using var db = harness.CreateDb();
        var service = harness.CreateService(db);

        // Less than 8 chars
        await Assert.ThrowsAsync<ArgumentException>(() =>
            service.ReverseLoanFundingAsync(11, "Valid reason", "short-7", "EXT-REF", "EVID-REF", "user"));

        // Greater than 160 chars
        var tooLongKey = new string('K', 161);
        await Assert.ThrowsAsync<ArgumentException>(() =>
            service.ReverseLoanFundingAsync(11, "Valid reason", tooLongKey, "EXT-REF", "EVID-REF", "user"));
    }

    [Fact]
    public async Task FundingReversal_RejectsWhenExternalReferenceBoundaryViolated()
    {
        using var harness = new Harness();
        await harness.SeedAdvanceAsync();

        await using var db = harness.CreateDb();
        var service = harness.CreateService(db);

        // Empty / whitespace
        await Assert.ThrowsAsync<ArgumentException>(() =>
            service.ReverseLoanFundingAsync(11, "Valid reason", "valid-event-key", "", "EVID-REF", "user"));

        // Greater than 200 chars
        var tooLongRef = new string('R', 201);
        await Assert.ThrowsAsync<ArgumentException>(() =>
            service.ReverseLoanFundingAsync(11, "Valid reason", "valid-event-key", tooLongRef, "EVID-REF", "user"));
    }

    [Fact]
    public async Task FundingReversal_RejectsWhenEvidenceReferenceBoundaryViolated()
    {
        using var harness = new Harness();
        await harness.SeedAdvanceAsync();

        await using var db = harness.CreateDb();
        var service = harness.CreateService(db);

        // Empty / whitespace
        await Assert.ThrowsAsync<ArgumentException>(() =>
            service.ReverseLoanFundingAsync(11, "Valid reason", "valid-event-key", "EXT-REF", "", "user"));

        // Greater than 500 chars
        var tooLongEvid = new string('E', 501);
        await Assert.ThrowsAsync<ArgumentException>(() =>
            service.ReverseLoanFundingAsync(11, "Valid reason", "valid-event-key", "EXT-REF", tooLongEvid, "user"));
    }

    [Fact]
    public async Task FundingReversal_RejectsWhenHandoffDeliveryStatusNotPosted()
    {
        using var harness = new Harness();
        await harness.SeedAdvanceAsync();

        await using (var db = harness.CreateDb())
        {
            var handoff = await db.HrAccountingHandoffs.SingleAsync(h => h.Id == 11);
            handoff.DeliveryStatus = "ACCEPTED"; // Not POSTED
            await db.SaveChangesAsync();
        }

        await using (var db = harness.CreateDb())
        {
            var service = harness.CreateService(db);
            var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
                service.ReverseLoanFundingAsync(11, "Valid reason", "rev-event-not-posted", "EXT-1", "EVID-1", "user"));
            Assert.Contains("Only a funding handoff with a confirmed disbursement receipt can be reversed", ex.Message);
        }
    }

    [Fact]
    public async Task FundingReversal_RejectsWhenHandoffBranchIdIsNull()
    {
        using var harness = new Harness();
        await harness.SeedAdvanceAsync();

        await using (var db = harness.CreateDb())
        {
            var handoff = await db.HrAccountingHandoffs.SingleAsync(h => h.Id == 11);
            handoff.BranchId = null;
            await db.SaveChangesAsync();
        }

        await using (var db = harness.CreateDb())
        {
            var service = harness.CreateService(db);
            var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
                service.ReverseLoanFundingAsync(11, "Valid reason", "rev-event-no-branch", "EXT-1", "EVID-1", "user"));
            Assert.Contains("Funding handoff has no branch", ex.Message);
        }
    }

    [Fact]
    public async Task FundingReversal_RejectsWhenFundingReceiptMissing()
    {
        using var harness = new Harness();
        await harness.SeedAdvanceAsync();

        await using (var db = harness.CreateDb())
        {
            var receipt = await db.AccountingLoanFundingReceipts.SingleAsync();
            db.AccountingLoanFundingReceipts.Remove(receipt);
            await db.SaveChangesAsync();
        }

        await using (var db = harness.CreateDb())
        {
            var service = harness.CreateService(db);
            var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
                service.ReverseLoanFundingAsync(11, "Valid reason", "rev-event-no-receipt", "EXT-1", "EVID-1", "user"));
            Assert.Contains("Funding reversal requires a posted Accounting funding receipt", ex.Message);
        }
    }

    [Fact]
    public async Task FundingReversal_RejectsWhenFundingReceiptHasNoVoucherId()
    {
        using var harness = new Harness();
        await harness.SeedAdvanceAsync();

        await using (var db = harness.CreateDb())
        {
            var receipt = await db.AccountingLoanFundingReceipts.SingleAsync();
            receipt.FundingVoucherId = 0; // Legacy / missing voucher
            await db.SaveChangesAsync();
        }

        await using (var db = harness.CreateDb())
        {
            var service = harness.CreateService(db);
            var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
                service.ReverseLoanFundingAsync(11, "Valid reason", "rev-event-no-vouch-id", "EXT-1", "EVID-1", "user"));
            Assert.Contains("This legacy funding receipt has no linked Payment Voucher; automatic reversal is blocked", ex.Message);
        }
    }

    [Fact]
    public async Task FundingReversal_RejectsWhenReversalEventKeyAlreadyUsedByAnotherReversal()
    {
        using var harness = new Harness();
        await harness.SeedAdvanceAsync();

        await using (var db = harness.CreateDb())
        {
            db.AccountingLoanFundingReceipts.Add(new AccountingLoanFundingReceipt
            {
                HandoffId = 999,
                SourceType = "ADVANCE",
                SourceId = "999",
                SourceVersion = 1,
                FundedAmount = 100m,
                CurrencyCode = "USD",
                FundingVoucherId = 7,
                EventKey = "prior-disb-key-01",
                ExternalReference = "PRIOR-EXT-01",
                ReversalEventKey = "duplicate-rev-key-001", // Duplicate reversal event key
                ReversalExternalReference = "PRIOR-REV-EXT-01",
                ReceivedAtUtc = DateTime.UtcNow,
                ReceivedBy = "user"
            });
            await db.SaveChangesAsync();
        }

        await using (var db = harness.CreateDb())
        {
            var service = harness.CreateService(db);
            var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
                service.ReverseLoanFundingAsync(11, "Valid reason", "duplicate-rev-key-001", "NEW-EXT-REF", "EVID-1", "user"));
            Assert.Contains("The reversal event key or external reference is already used by another funding reversal", ex.Message);
        }
    }

    [Fact]
    public async Task FundingReversal_RejectsWhenReversalExternalReferenceAlreadyUsedByAnotherReversal()
    {
        using var harness = new Harness();
        await harness.SeedAdvanceAsync();

        await using (var db = harness.CreateDb())
        {
            db.AccountingLoanFundingReceipts.Add(new AccountingLoanFundingReceipt
            {
                HandoffId = 999,
                SourceType = "ADVANCE",
                SourceId = "999",
                SourceVersion = 1,
                FundedAmount = 100m,
                CurrencyCode = "USD",
                FundingVoucherId = 7,
                EventKey = "prior-disb-key-02",
                ExternalReference = "PRIOR-EXT-02",
                ReversalEventKey = "prior-rev-key-002",
                ReversalExternalReference = "duplicate-rev-ext-002", // Duplicate external ref
                ReceivedAtUtc = DateTime.UtcNow,
                ReceivedBy = "user"
            });
            await db.SaveChangesAsync();
        }

        await using (var db = harness.CreateDb())
        {
            var service = harness.CreateService(db);
            var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
                service.ReverseLoanFundingAsync(11, "Valid reason", "new-unique-rev-key", "duplicate-rev-ext-002", "EVID-1", "user"));
            Assert.Contains("The reversal event key or external reference is already used by another funding reversal", ex.Message);
        }
    }

    [Fact]
    public async Task FundingReversal_ReversedHandoff_ReplayWithDifferentReasonOrEvidence_ThrowsInvalidOperationException()
    {
        using var harness = new Harness();
        await harness.SeedAdvanceAsync();

        // First successful reversal
        await using (var db = harness.CreateDb())
        {
            var service = harness.CreateService(db);
            await service.ReverseLoanFundingAsync(11, "Original reversal reason", "rev-key-immutable-01", "EXT-IMMUT-01", "EVID-IMMUT-01", "user");
        }

        // Replay with different reason
        await using (var db = harness.CreateDb())
        {
            var service = harness.CreateService(db);
            var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
                service.ReverseLoanFundingAsync(11, "Different changed reason", "rev-key-immutable-01", "EXT-IMMUT-01", "EVID-IMMUT-01", "user"));
            Assert.Contains("A reversed funding handoff is immutable", ex.Message);
        }
    }

    [Fact]
    public async Task FundingReversal_PropagatesCancellationToken()
    {
        using var harness = new Harness();
        await harness.SeedAdvanceAsync();

        using var cts = new CancellationTokenSource();
        cts.Cancel();

        await using var db = harness.CreateDb();
        var service = harness.CreateService(db);

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            service.ReverseLoanFundingAsync(11, "Valid reason", "cancel-rev-key-01", "EXT-CAN-01", "EVID-CAN-01", "user", cts.Token));
    }

    private sealed class Harness : IDisposable
    {
        private readonly DbContextOptions<OracleDbContext> options = new DbContextOptionsBuilder<OracleDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning)).Options;
        private readonly Mock<IGlVoucherRepository> vouchers = new();
        private readonly Mock<IReceiptPaymentVoucherService> paymentVouchers = new();
        private readonly Mock<IGlVoucherService> glVouchers = new();
        private bool reversalCreated;
        private readonly decimal totalAmount;
        private readonly GlVoucherHeader original;
        private readonly GlVoucherHeader reversal;

        public Harness(decimal amount = 100m)
        {
            totalAmount = amount;
            original = new()
            {
                Id = 7, BranchId = 2, VoucherType = 103, Status = 3,
                PostDate = new DateTime(2026, 11, 7, 10, 0, 0, DateTimeKind.Utc),
                TotalAmount = amount, TotalLocalDebit = amount, TotalLocalCredit = amount,
                Details =
                [
                    new GlVoucherDetail { VoucherId = 7, LineSer = 1, BranchId = 2, CurrencyId = 1, AccountCode = "LOAN-ASSET", Debit = amount, LocalDebit = amount, BaseDebit = amount },
                    new GlVoucherDetail { VoucherId = 7, LineSer = 2, BranchId = 2, CurrencyId = 1, AccountCode = "BANK", Credit = amount, LocalCredit = amount, BaseCredit = amount }
                ]
            };
            reversal = new()
            {
                Id = 72, BranchId = 2, VoucherType = 103, Status = 3, SourceSystemCode = "GL_REVERSAL", SourceRefId = 7,
                PostDate = new DateTime(2026, 11, 7, 11, 0, 0, DateTimeKind.Utc),
                TotalAmount = amount, TotalLocalDebit = amount, TotalLocalCredit = amount,
                Details =
                [
                    new GlVoucherDetail { VoucherId = 72, LineSer = 1, BranchId = 2, CurrencyId = 1, AccountCode = "LOAN-ASSET", Credit = amount, LocalCredit = amount, BaseCredit = amount },
                    new GlVoucherDetail { VoucherId = 72, LineSer = 2, BranchId = 2, CurrencyId = 1, AccountCode = "BANK", Debit = amount, LocalDebit = amount, BaseDebit = amount }
                ]
            };

            paymentVouchers.Setup(x => x.GetPaymentVoucherByIdAsync(7, It.IsAny<CancellationToken>()))
                .ReturnsAsync(new PaymentVoucherDto { VoucherId = 7, BranchId = 2, CurrencyId = 1, Status = 3 });
            vouchers.Setup(x => x.GetByIdAsync(7, It.IsAny<CancellationToken>())).ReturnsAsync(original);
            vouchers.Setup(x => x.GetByIdAsync(72, It.IsAny<CancellationToken>())).ReturnsAsync(reversal);
            vouchers.Setup(x => x.GetBySourceAsync(2, "GL_REVERSAL", 7, It.IsAny<CancellationToken>()))
                .Returns(() => Task.FromResult<IReadOnlyList<GlVoucherHeader>>(reversalCreated ? [reversal] : Array.Empty<GlVoucherHeader>()));
            glVouchers.Setup(x => x.ReverseVoucherAsync(7, It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .Callback(() => { original.IsReversed = true; reversalCreated = true; })
                .ReturnsAsync(new GlVoucherHeaderDto());
        }

        public Mock<IGlVoucherService> GlVoucherService => glVouchers;
        public OracleDbContext CreateDb() => new(options);
        public HrAccountingHandoffService CreateService(OracleDbContext db, IHrUnitOfWork? unitOfWork = null) => new(db, vouchers.Object,
            Mock.Of<IHrPayrollPostingService>(), paymentVouchers.Object, glVouchers.Object, unitOfWork);

        public void SetupOriginalVoucherNull()
        {
            vouchers.Setup(x => x.GetByIdAsync(7, It.IsAny<CancellationToken>())).ReturnsAsync((GlVoucherHeader?)null);
        }

        public void SetupOriginalVoucherNotPosted()
        {
            original.Status = 1;
            original.PostDate = null;
        }

        public void SetupOriginalVoucherBranchMismatch()
        {
            original.BranchId = 99;
        }

        public void SetupOriginalVoucherDetailsCurrencyMismatch()
        {
            original.Details[0].CurrencyId = 99;
        }

        public void SetupVoucherWithSettledBills()
        {
            paymentVouchers.Setup(x => x.GetPaymentVoucherByIdAsync(7, It.IsAny<CancellationToken>()))
                .ReturnsAsync(new PaymentVoucherDto
                {
                    VoucherId = 7,
                    BranchId = 2,
                    CurrencyId = 1,
                    Status = 3,
                    SettledBills = [new PaymentSettledBillDto { ApplicationId = 99 }]
                });
        }

        public async Task SeedAdvanceAsync()
        {
            await using var db = CreateDb();
            db.SysCurrencies.Add(new SysCurrency { Id = 1, ShortNameEn = "USD", CurrencyNameEn = "US Dollar" });
            db.EmployeeAdvances.Add(new EmployeeAdvance
            {
                Id = 41, EmployeeCode = "E-41", AdvanceAmount = totalAmount, CurrencyCode = "USD", TargetPayPeriod = "2026-11",
                RemainingBalance = totalAmount, Status = "ACTIVE"
            });
            db.HrAccountingHandoffs.Add(new HrAccountingHandoff
            {
                Id = 11, CompanyId = 3, BranchId = 2, SourceType = "ADVANCE", SourceId = "41", SourceVersion = 1,
                Operation = "FUND", HandoffReference = "HR-HANDOFF-11", PayloadHash = "funding-payload", DeliveryStatus = "POSTED",
                CreatedAtUtc = DateTime.UtcNow, CreatedBy = "hr",
                Transitions = [new HrAccountingHandoffTransition { FromStatus = "ACCEPTED", ToStatus = "POSTED", Actor = "accounting", OccurredAtUtc = DateTime.UtcNow }]
            });
            db.AccountingLoanFundingReceipts.Add(new AccountingLoanFundingReceipt
            {
                Id = 14, HandoffId = 11, SourceType = "ADVANCE", SourceId = "41", SourceVersion = 1,
                FundedAmount = totalAmount, CurrencyCode = "USD", FundingVoucherId = 7, EventKey = "fund-event-001",
                ExternalReference = "BANK-REF-77", ReceivedAtUtc = DateTime.UtcNow, ReceivedBy = "accounting"
            });
            await db.SaveChangesAsync();
        }

        public async Task SeedLoanAsync()
        {
            await using var db = CreateDb();
            if (!await db.SysCurrencies.AnyAsync(c => c.Id == 1))
            {
                db.SysCurrencies.Add(new SysCurrency { Id = 1, ShortNameEn = "USD", CurrencyNameEn = "US Dollar" });
            }
            var loan = new EmployeeLoan
            {
                Id = 51, EmployeeCode = "E-51", PrincipalAmount = totalAmount, CurrencyCode = "USD",
                RemainingBalance = totalAmount, TotalPaidAmount = 0m, Status = "ACTIVE"
            };
            db.EmployeeLoans.Add(loan);
            db.LoanRepaymentSchedules.Add(new LoanRepaymentSchedule
            {
                Id = 151, EmployeeLoanId = 51, InstallmentNo = 1, PayPeriod = "2026-11",
                ScheduledAmount = totalAmount, PaidAmount = 0m, Status = "PENDING", EmployeeLoan = loan
            });
            db.HrAccountingHandoffs.Add(new HrAccountingHandoff
            {
                Id = 12, CompanyId = 3, BranchId = 2, SourceType = "LOAN", SourceId = "51", SourceVersion = 1,
                Operation = "FUND", HandoffReference = "HR-HANDOFF-12", PayloadHash = "funding-payload-loan", DeliveryStatus = "POSTED",
                CreatedAtUtc = DateTime.UtcNow, CreatedBy = "hr",
                Transitions = [new HrAccountingHandoffTransition { FromStatus = "ACCEPTED", ToStatus = "POSTED", Actor = "accounting", OccurredAtUtc = DateTime.UtcNow }]
            });
            db.AccountingLoanFundingReceipts.Add(new AccountingLoanFundingReceipt
            {
                Id = 15, HandoffId = 12, SourceType = "LOAN", SourceId = "51", SourceVersion = 1,
                FundedAmount = totalAmount, CurrencyCode = "USD", FundingVoucherId = 7, EventKey = "fund-event-loan-001",
                ExternalReference = "BANK-REF-LOAN-77", ReceivedAtUtc = DateTime.UtcNow, ReceivedBy = "accounting"
            });
            await db.SaveChangesAsync();
        }

        public void Dispose() { }
    }
}
