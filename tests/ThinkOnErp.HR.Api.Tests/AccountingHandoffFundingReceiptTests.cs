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

public sealed class AccountingHandoffFundingReceiptTests
{
    [Fact]
    public async Task ReplayingFundingReceiptAfterPostingIsIdempotent()
    {
        using var harness = new FundingReceiptHarness();
        await harness.SeedAsync();

        await using (var db = harness.CreateDb())
        {
            var service = harness.CreateService(db);
            await service.ConfirmLoanFundingAsync(11, 100m, "usd", 7, "funding-event-001", "BANK-REF-77", "accounting-user");
            await service.ConfirmLoanFundingAsync(11, 100m, "USD", 7, "funding-event-001", "BANK-REF-77", "accounting-user");
        }

        await using var verify = harness.CreateDb();
        Assert.Equal("POSTED", (await verify.HrAccountingHandoffs.SingleAsync()).DeliveryStatus);
        Assert.Single(await verify.AccountingLoanFundingReceipts.ToListAsync());
        Assert.Single(await verify.HrAccountingHandoffTransitions.ToListAsync());
        var advance = await verify.EmployeeAdvances.SingleAsync();
        Assert.Equal("ACTIVE", advance.Status);
        Assert.Equal(100m, advance.RemainingBalance);
    }

    [Fact]
    public async Task ConfirmLoanFunding_ExecutesInsideEmployeeAndAffectedPayrollDateLock()
    {
        using var harness = new FundingReceiptHarness();
        await harness.SeedAsync();
        var unitOfWork = new Mock<IHrUnitOfWork>();
        unitOfWork.Setup(u => u.ExecuteForEmployeeDatesAsync(
                "E-41", new DateTime(2026, 11, 1), new DateTime(2026, 11, 30),
                It.IsAny<Func<Task<bool>>>(), It.IsAny<CancellationToken>()))
            .Returns((string _, DateTime _, DateTime _, Func<Task<bool>> action, CancellationToken _) => action());

        await using (var db = harness.CreateDb())
        {
            await harness.CreateService(db, unitOfWork.Object).ConfirmLoanFundingAsync(
                11, 100m, "USD", 7, "funding-event-001", "BANK-REF-77", "accounting-user");
        }

        unitOfWork.Verify(u => u.ExecuteForEmployeeDatesAsync(
            "E-41", new DateTime(2026, 11, 1), new DateTime(2026, 11, 30),
            It.IsAny<Func<Task<bool>>>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task DifferentEventCannotReplaceAnExistingFundingReceipt()
    {
        using var harness = new FundingReceiptHarness();
        await harness.SeedAsync();

        await using (var db = harness.CreateDb())
        {
            var service = harness.CreateService(db);
            await service.ConfirmLoanFundingAsync(11, 100m, "USD", 7, "funding-event-001", "BANK-REF-77", "accounting-user");
            await Assert.ThrowsAsync<InvalidOperationException>(() =>
                service.ConfirmLoanFundingAsync(11, 100m, "USD", 7, "funding-event-002", "BANK-REF-77", "accounting-user"));
        }

        await using var verify = harness.CreateDb();
        Assert.Equal("POSTED", (await verify.HrAccountingHandoffs.SingleAsync()).DeliveryStatus);
        Assert.Single(await verify.AccountingLoanFundingReceipts.ToListAsync());
        Assert.Single(await verify.HrAccountingHandoffTransitions.ToListAsync());
    }

    [Fact]
    public async Task PartialFundingIsRejectedWithoutActivatingAdvance()
    {
        using var harness = new FundingReceiptHarness();
        await harness.SeedAsync();

        await using (var db = harness.CreateDb())
            await Assert.ThrowsAsync<InvalidOperationException>(() => harness.CreateService(db).ConfirmLoanFundingAsync(
                11, 60m, "USD", 7, "funding-event-001", "BANK-REF-77", "accounting-user"));

        await using var verify = harness.CreateDb();
        var advance = await verify.EmployeeAdvances.SingleAsync();
        Assert.Equal("APPROVED", advance.Status);
        Assert.Equal(100m, advance.RemainingBalance);
        Assert.Empty(await verify.AccountingLoanFundingReceipts.ToListAsync());
    }

    [Fact]
    public async Task FundingRequiresPostedPaymentVoucherBeforeActivatingAdvance()
    {
        using var harness = new FundingReceiptHarness();
        await harness.SeedAsync();
        harness.SetVoucherStatus(1);

        await using (var db = harness.CreateDb())
            await Assert.ThrowsAsync<InvalidOperationException>(() => harness.CreateService(db).ConfirmLoanFundingAsync(
                11, 100m, "USD", 7, "funding-event-001", "BANK-REF-77", "accounting-user"));

        await using var verify = harness.CreateDb();
        var advance = await verify.EmployeeAdvances.SingleAsync();
        Assert.Equal("APPROVED", advance.Status);
        Assert.Equal(100m, advance.RemainingBalance);
        Assert.Empty(await verify.AccountingLoanFundingReceipts.ToListAsync());
    }

    [Fact]
    public async Task ConfirmLoanFunding_ActivatesApprovedLoanAndSchedulesAndIsIdempotent()
    {
        using var harness = new FundingReceiptHarness();
        await harness.SeedLoanAsync(loanStatus: "APPROVED", principalAmount: 500m);
        harness.SetupVoucher(8, 500m, handoffRef: "HR-HANDOFF-12", transferRef: "BANK-REF-LOAN-8");

        await using (var db = harness.CreateDb())
        {
            var service = harness.CreateService(db);
            await service.ConfirmLoanFundingAsync(12, 500m, "USD", 8, "loan-fund-evt-001", "BANK-REF-LOAN-8", "accounting-user");
            // Idempotent retry
            await service.ConfirmLoanFundingAsync(12, 500m, "USD", 8, "loan-fund-evt-001", "BANK-REF-LOAN-8", "accounting-user");
        }

        await using var verify = harness.CreateDb();
        var handoff = await verify.HrAccountingHandoffs.SingleAsync(h => h.Id == 12);
        Assert.Equal("POSTED", handoff.DeliveryStatus);
        Assert.Single(await verify.AccountingLoanFundingReceipts.Where(r => r.HandoffId == 12).ToListAsync());
        Assert.Single(await verify.HrAccountingHandoffTransitions.Where(t => t.ToStatus == "POSTED").ToListAsync());
        var loan = await verify.EmployeeLoans.SingleAsync(l => l.Id == 51);
        Assert.Equal("ACTIVE", loan.Status);
        Assert.Equal(500m, loan.RemainingBalance);
    }

    [Fact]
    public async Task ConfirmLoanFunding_RejectsWhenLoanNotApproved()
    {
        using var harness = new FundingReceiptHarness();
        await harness.SeedLoanAsync(loanStatus: "ACTIVE", principalAmount: 500m);
        harness.SetupVoucher(8, 500m, handoffRef: "HR-HANDOFF-12", transferRef: "BANK-REF-LOAN-8");

        await using (var db = harness.CreateDb())
        {
            var service = harness.CreateService(db);
            var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
                service.ConfirmLoanFundingAsync(12, 500m, "USD", 8, "loan-fund-evt-001", "BANK-REF-LOAN-8", "accounting-user"));
            Assert.Contains("Loan has changed since the approved funding snapshot", ex.Message);
        }
    }

    [Fact]
    public async Task ConfirmLoanFunding_RejectsWhenLoanScheduleNotPending()
    {
        using var harness = new FundingReceiptHarness();
        await harness.SeedLoanAsync(loanStatus: "APPROVED", principalAmount: 500m, scheduleStatus: "PAID", paidAmount: 200m);
        harness.SetupVoucher(8, 500m, handoffRef: "HR-HANDOFF-12", transferRef: "BANK-REF-LOAN-8");

        await using (var db = harness.CreateDb())
        {
            var service = harness.CreateService(db);
            var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
                service.ConfirmLoanFundingAsync(12, 500m, "USD", 8, "loan-fund-evt-001", "BANK-REF-LOAN-8", "accounting-user"));
            Assert.Contains("Loan has changed since the approved funding snapshot", ex.Message);
        }
    }

    [Fact]
    public async Task ConfirmLoanFunding_RejectsWhenCurrencyMismatch()
    {
        using var harness = new FundingReceiptHarness();
        await harness.SeedLoanAsync(loanStatus: "APPROVED", principalAmount: 500m, loanCurrency: "USD", handoffCurrency: "USD");
        harness.SetupVoucher(8, 500m, handoffRef: "HR-HANDOFF-12", transferRef: "BANK-REF-LOAN-8");

        await using (var db = harness.CreateDb())
        {
            var service = harness.CreateService(db);
            var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
                service.ConfirmLoanFundingAsync(12, 500m, "EUR", 8, "loan-fund-evt-001", "BANK-REF-LOAN-8", "accounting-user"));
            Assert.Contains("The disbursement amount and currency must exactly match", ex.Message);
        }
    }

    [Fact]
    public async Task ConfirmLoanFunding_RejectsWhenHandoffDeliveryStatusNotAccepted()
    {
        using var harness = new FundingReceiptHarness();
        await harness.SeedLoanAsync(loanStatus: "APPROVED", principalAmount: 500m, deliveryStatus: "QUEUED");
        harness.SetupVoucher(8, 500m, handoffRef: "HR-HANDOFF-12", transferRef: "BANK-REF-LOAN-8");

        await using (var db = harness.CreateDb())
        {
            var service = harness.CreateService(db);
            var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
                service.ConfirmLoanFundingAsync(12, 500m, "USD", 8, "loan-fund-evt-001", "BANK-REF-LOAN-8", "accounting-user"));
            Assert.Contains("Only an Accounting-accepted funding request can receive a new disbursement receipt", ex.Message);
        }
    }

    [Fact]
    public async Task ConfirmLoanFunding_RejectsWhenPaymentVoucherIsReversed()
    {
        using var harness = new FundingReceiptHarness();
        await harness.SeedLoanAsync(loanStatus: "APPROVED", principalAmount: 500m);
        harness.SetupVoucher(8, 500m, isReversed: true, handoffRef: "HR-HANDOFF-12", transferRef: "BANK-REF-LOAN-8");

        await using (var db = harness.CreateDb())
        {
            var service = harness.CreateService(db);
            var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
                service.ConfirmLoanFundingAsync(12, 500m, "USD", 8, "loan-fund-evt-001", "BANK-REF-LOAN-8", "accounting-user"));
            Assert.Contains("Funding requires a posted, unreversed Accounting Payment Voucher", ex.Message);
        }
    }

    [Fact]
    public async Task ConfirmLoanFunding_RejectsWhenVoucherBranchMismatch()
    {
        using var harness = new FundingReceiptHarness();
        await harness.SeedLoanAsync(loanStatus: "APPROVED", principalAmount: 500m);
        harness.SetupVoucher(8, 500m, branchId: 99, handoffRef: "HR-HANDOFF-12", transferRef: "BANK-REF-LOAN-8");

        await using (var db = harness.CreateDb())
        {
            var service = harness.CreateService(db);
            var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
                service.ConfirmLoanFundingAsync(12, 500m, "USD", 8, "loan-fund-evt-001", "BANK-REF-LOAN-8", "accounting-user"));
            Assert.Contains("Funding requires a posted, unreversed Accounting Payment Voucher", ex.Message);
        }
    }

    [Fact]
    public async Task ConfirmLoanFunding_RejectsWhenVoucherDetailsUnbalanced()
    {
        using var harness = new FundingReceiptHarness();
        await harness.SeedLoanAsync(loanStatus: "APPROVED", principalAmount: 500m);
        harness.SetupVoucher(8, 500m, creditAmount: 400m, handoffRef: "HR-HANDOFF-12", transferRef: "BANK-REF-LOAN-8");

        await using (var db = harness.CreateDb())
        {
            var service = harness.CreateService(db);
            var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
                service.ConfirmLoanFundingAsync(12, 500m, "USD", 8, "loan-fund-evt-001", "BANK-REF-LOAN-8", "accounting-user"));
            Assert.Contains("Funding requires a posted, unreversed Accounting Payment Voucher", ex.Message);
        }
    }

    [Theory]
    [InlineData(0, 8, "event-key-valid", "REF-1")]
    [InlineData(-10, 8, "event-key-valid", "REF-1")]
    [InlineData(500, 0, "event-key-valid", "REF-1")]
    [InlineData(500, -1, "event-key-valid", "REF-1")]
    [InlineData(500, 8, "short", "REF-1")]
    [InlineData(500, 8, "", "REF-1")]
    [InlineData(500, 8, "event-key-valid", "")]
    public async Task ConfirmLoanFunding_RejectsInvalidInputArguments(decimal amount, long voucherId, string eventKey, string externalRef)
    {
        using var harness = new FundingReceiptHarness();
        await harness.SeedLoanAsync();

        await using var db = harness.CreateDb();
        var service = harness.CreateService(db);
        await Assert.ThrowsAsync<ArgumentException>(() =>
            service.ConfirmLoanFundingAsync(12, amount, "USD", voucherId, eventKey, externalRef, "user"));
    }

    [Fact]
    public async Task ConfirmLoanFunding_RejectsWhenHandoffNotFound()
    {
        using var harness = new FundingReceiptHarness();
        await harness.SeedAsync();

        await using var db = harness.CreateDb();
        var service = harness.CreateService(db);
        var ex = await Assert.ThrowsAsync<KeyNotFoundException>(() =>
            service.ConfirmLoanFundingAsync(9999, 100m, "USD", 7, "fund-evt-key-valid", "BANK-REF-77", "accounting-user"));
        Assert.Contains("Funding handoff not found", ex.Message);
    }

    [Fact]
    public async Task ConfirmLoanFunding_RejectsWhenHandoffSourceTypeIsNotLoanOrAdvance()
    {
        using var harness = new FundingReceiptHarness();
        await harness.SeedAsync();

        await using (var db = harness.CreateDb())
        {
            var handoff = await db.HrAccountingHandoffs.SingleAsync(h => h.Id == 11);
            handoff.SourceType = "PAYROLL";
            await db.SaveChangesAsync();
        }

        await using (var db = harness.CreateDb())
        {
            var service = harness.CreateService(db);
            var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
                service.ConfirmLoanFundingAsync(11, 100m, "USD", 7, "fund-evt-key-valid", "BANK-REF-77", "accounting-user"));
            Assert.Contains("Only a loan/advance funding request can receive a disbursement receipt", ex.Message);
        }
    }

    [Fact]
    public async Task ConfirmLoanFunding_RejectsWhenVoucherDescriptionAndTransferReferenceDoNotMatchHandoff()
    {
        using var harness = new FundingReceiptHarness();
        await harness.SeedLoanAsync();
        // Voucher description and transfer ref completely unrelated
        harness.SetupVoucher(8, 500m, handoffRef: "UNRELATED-HANDOFF", transferRef: "UNRELATED-REF");

        await using (var db = harness.CreateDb())
        {
            var service = harness.CreateService(db);
            var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
                service.ConfirmLoanFundingAsync(12, 500m, "USD", 8, "loan-fund-evt-001", "BANK-REF-LOAN-8", "accounting-user"));
            Assert.Contains("Funding requires a posted, unreversed Accounting Payment Voucher linked by handoff reference or transfer reference", ex.Message);
        }
    }

    [Fact]
    public async Task ConfirmLoanFunding_RejectsWhenPaymentVoucherAlreadyLinkedToAnotherFundingReceipt()
    {
        using var harness = new FundingReceiptHarness();
        await harness.SeedLoanAsync();
        harness.SetupVoucher(8, 500m, handoffRef: "HR-HANDOFF-12", transferRef: "BANK-REF-LOAN-8");

        await using (var db = harness.CreateDb())
        {
            db.AccountingLoanFundingReceipts.Add(new AccountingLoanFundingReceipt
            {
                HandoffId = 99,
                SourceType = "LOAN",
                SourceId = "999",
                SourceVersion = 1,
                FundedAmount = 500m,
                CurrencyCode = "USD",
                FundingVoucherId = 8, // Already linked to voucher 8
                EventKey = "another-prior-event-001",
                ExternalReference = "PRIOR-REF-88",
                ReceivedAtUtc = DateTime.UtcNow,
                ReceivedBy = "prior-user"
            });
            await db.SaveChangesAsync();
        }

        await using (var db = harness.CreateDb())
        {
            var service = harness.CreateService(db);
            var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
                service.ConfirmLoanFundingAsync(12, 500m, "USD", 8, "loan-fund-evt-001", "BANK-REF-LOAN-8", "accounting-user"));
            Assert.Contains("This Accounting Payment Voucher is already linked to another funding receipt", ex.Message);
        }
    }

    [Fact]
    public async Task ConfirmLoanFunding_Advance_SetsAuditAndTransitionFieldsCorrectly()
    {
        using var harness = new FundingReceiptHarness();
        await harness.SeedAsync();

        await using (var db = harness.CreateDb())
        {
            var service = harness.CreateService(db);
            await service.ConfirmLoanFundingAsync(11, 100m, "USD", 7, "fund-event-adv-audit", "BANK-REF-77", "approver-user");
        }

        await using var verify = harness.CreateDb();
        var advance = await verify.EmployeeAdvances.SingleAsync();
        Assert.Equal("ACTIVE", advance.Status);
        Assert.Equal(100m, advance.RemainingBalance);
        Assert.Equal("approver-user", advance.UpdateUser);
        Assert.NotNull(advance.UpdateDate);

        var handoff = await verify.HrAccountingHandoffs.SingleAsync(h => h.Id == 11);
        Assert.Equal("POSTED", handoff.DeliveryStatus);

        var transition = await verify.HrAccountingHandoffTransitions.SingleAsync(t => t.ToStatus == "POSTED");
        Assert.Equal("ACCEPTED", transition.FromStatus);
        Assert.Equal("approver-user", transition.Actor);
        Assert.Equal("ACCOUNTING_DISBURSEMENT_CONFIRMED", transition.Reason);
    }

    [Fact]
    public async Task ConfirmLoanFunding_RejectsWhenDisbursementAmountDoesNotMatchHandoffLine()
    {
        using var harness = new FundingReceiptHarness();
        await harness.SeedAsync();

        await using var db = harness.CreateDb();
        var service = harness.CreateService(db);
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.ConfirmLoanFundingAsync(11, 80m, "USD", 7, "fund-event-adv-amt", "BANK-REF-77", "accounting-user"));
        Assert.Contains("The disbursement amount and currency must exactly match", ex.Message);
    }

    [Fact]
    public async Task ConfirmLoanFunding_RejectsWhenCurrencyDoesNotExistInErp()
    {
        using var harness = new FundingReceiptHarness();
        await harness.SeedLoanAsync(loanStatus: "APPROVED", principalAmount: 500m, loanCurrency: "GBP", handoffCurrency: "GBP");
        harness.SetupVoucher(8, 500m, handoffRef: "HR-HANDOFF-12", transferRef: "BANK-REF-LOAN-8");

        await using var db = harness.CreateDb();
        var service = harness.CreateService(db);
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.ConfirmLoanFundingAsync(12, 500m, "GBP", 8, "fund-event-loan-gbp", "BANK-REF-LOAN-8", "accounting-user"));
        Assert.Contains("Funding currency does not resolve to one unique ERP currency", ex.Message);
    }

    [Fact]
    public async Task ConfirmLoanFunding_RejectsWhenFundingVoucherNotFoundInAccounting()
    {
        using var harness = new FundingReceiptHarness();
        await harness.SeedAsync();
        harness.SetupVoucherNotFound(999);

        await using var db = harness.CreateDb();
        var service = harness.CreateService(db);
        var ex = await Assert.ThrowsAsync<KeyNotFoundException>(() =>
            service.ConfirmLoanFundingAsync(11, 100m, "USD", 999, "fund-event-missing-vouch", "BANK-REF-77", "accounting-user"));
        Assert.Contains("Funding Payment Voucher was not found in Accounting", ex.Message);
    }

    [Fact]
    public async Task ConfirmLoanFunding_RejectsWhenFundingVoucherTypeNot3Or103()
    {
        using var harness = new FundingReceiptHarness();
        await harness.SeedLoanAsync(loanStatus: "APPROVED", principalAmount: 500m);
        harness.SetupVoucher(8, 500m, voucherType: 1, handoffRef: "HR-HANDOFF-12", transferRef: "BANK-REF-LOAN-8");

        await using var db = harness.CreateDb();
        var service = harness.CreateService(db);
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.ConfirmLoanFundingAsync(12, 500m, "USD", 8, "fund-event-wrong-type", "BANK-REF-LOAN-8", "accounting-user"));
        Assert.Contains("Funding requires a posted, unreversed Accounting Payment Voucher", ex.Message);
    }

    [Fact]
    public async Task ConfirmLoanFunding_RejectsWhenVoucherDetailsEmpty()
    {
        using var harness = new FundingReceiptHarness();
        await harness.SeedLoanAsync(loanStatus: "APPROVED", principalAmount: 500m);
        harness.SetupVoucherEmptyDetails(8, 500m, handoffRef: "HR-HANDOFF-12", transferRef: "BANK-REF-LOAN-8");

        await using var db = harness.CreateDb();
        var service = harness.CreateService(db);
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.ConfirmLoanFundingAsync(12, 500m, "USD", 8, "fund-event-empty-details", "BANK-REF-LOAN-8", "accounting-user"));
        Assert.Contains("Funding requires a posted, unreversed Accounting Payment Voucher", ex.Message);
    }

    [Fact]
    public async Task ConfirmLoanFunding_Advance_RejectsWhenAdvanceRecordMissing()
    {
        using var harness = new FundingReceiptHarness();
        await harness.SeedAsync();

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
                service.ConfirmLoanFundingAsync(11, 100m, "USD", 7, "fund-event-missing-adv", "BANK-REF-77", "accounting-user"));
            Assert.Contains("Linked advance is missing", ex.Message);
        }
    }

    [Fact]
    public async Task ConfirmLoanFunding_Loan_RejectsWhenLoanRecordMissing()
    {
        using var harness = new FundingReceiptHarness();
        await harness.SeedLoanAsync();
        harness.SetupVoucher(8, 500m, handoffRef: "HR-HANDOFF-12", transferRef: "BANK-REF-LOAN-8");

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
                service.ConfirmLoanFundingAsync(12, 500m, "USD", 8, "fund-event-missing-loan", "BANK-REF-LOAN-8", "accounting-user"));
            Assert.Contains("Linked loan is missing", ex.Message);
        }
    }

    [Fact]
    public async Task ConfirmLoanFunding_Advance_RejectsWhenAdvanceHasDeductions()
    {
        using var harness = new FundingReceiptHarness();
        await harness.SeedAsync();

        await using (var db = harness.CreateDb())
        {
            var advance = await db.EmployeeAdvances.SingleAsync();
            advance.DeductedAmount = 15m;
            await db.SaveChangesAsync();
        }

        await using (var db = harness.CreateDb())
        {
            var service = harness.CreateService(db);
            var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
                service.ConfirmLoanFundingAsync(11, 100m, "USD", 7, "fund-event-adv-deducted", "BANK-REF-77", "accounting-user"));
            Assert.Contains("Advance has changed since the approved funding snapshot", ex.Message);
        }
    }

    [Fact]
    public async Task ConfirmLoanFunding_Advance_RejectsWhenAdvanceAlreadyReserved()
    {
        using var harness = new FundingReceiptHarness();
        await harness.SeedAsync();

        await using (var db = harness.CreateDb())
        {
            var advance = await db.EmployeeAdvances.SingleAsync();
            advance.ReservedByHandoffId = 999;
            await db.SaveChangesAsync();
        }

        await using (var db = harness.CreateDb())
        {
            var service = harness.CreateService(db);
            var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
                service.ConfirmLoanFundingAsync(11, 100m, "USD", 7, "fund-event-adv-reserved", "BANK-REF-77", "accounting-user"));
            Assert.Contains("Advance has changed since the approved funding snapshot", ex.Message);
        }
    }

    [Fact]
    public async Task ConfirmLoanFunding_Loan_RejectsWhenScheduleHasCarriedForwardAmount()
    {
        using var harness = new FundingReceiptHarness();
        await harness.SeedLoanAsync();
        harness.SetupVoucher(8, 500m, handoffRef: "HR-HANDOFF-12", transferRef: "BANK-REF-LOAN-8");

        await using (var db = harness.CreateDb())
        {
            var schedule = await db.LoanRepaymentSchedules.SingleAsync();
            schedule.CarriedForwardAmount = 25m;
            await db.SaveChangesAsync();
        }

        await using (var db = harness.CreateDb())
        {
            var service = harness.CreateService(db);
            var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
                service.ConfirmLoanFundingAsync(12, 500m, "USD", 8, "fund-event-sched-cf", "BANK-REF-LOAN-8", "accounting-user"));
            Assert.Contains("Loan has changed since the approved funding snapshot", ex.Message);
        }
    }

    [Fact]
    public async Task ConfirmLoanFunding_Loan_RejectsWhenScheduleAlreadyReserved()
    {
        using var harness = new FundingReceiptHarness();
        await harness.SeedLoanAsync();
        harness.SetupVoucher(8, 500m, handoffRef: "HR-HANDOFF-12", transferRef: "BANK-REF-LOAN-8");

        await using (var db = harness.CreateDb())
        {
            var schedule = await db.LoanRepaymentSchedules.SingleAsync();
            schedule.ReservedByHandoffId = 888;
            await db.SaveChangesAsync();
        }

        await using (var db = harness.CreateDb())
        {
            var service = harness.CreateService(db);
            var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
                service.ConfirmLoanFundingAsync(12, 500m, "USD", 8, "fund-event-sched-res", "BANK-REF-LOAN-8", "accounting-user"));
            Assert.Contains("Loan has changed since the approved funding snapshot", ex.Message);
        }
    }

    [Fact]
    public async Task ConfirmLoanFunding_Advance_RejectsWhenSourceIdIsNotNumeric()
    {
        using var harness = new FundingReceiptHarness();
        await harness.SeedAsync();

        await using (var db = harness.CreateDb())
        {
            var handoff = await db.HrAccountingHandoffs.SingleAsync(h => h.Id == 11);
            handoff.SourceId = "NON_NUMERIC_ID";
            await db.SaveChangesAsync();
        }

        await using (var db = harness.CreateDb())
        {
            var service = harness.CreateService(db);
            var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
                service.ConfirmLoanFundingAsync(11, 100m, "USD", 7, "fund-event-bad-source", "BANK-REF-77", "accounting-user"));
            Assert.Contains("Funding source ID is invalid", ex.Message);
        }
    }

    [Fact]
    public async Task ConfirmLoanFunding_Loan_RejectsWhenSourceIdIsNotNumeric()
    {
        using var harness = new FundingReceiptHarness();
        await harness.SeedLoanAsync();
        harness.SetupVoucher(8, 500m, handoffRef: "HR-HANDOFF-12", transferRef: "BANK-REF-LOAN-8");

        await using (var db = harness.CreateDb())
        {
            var handoff = await db.HrAccountingHandoffs.SingleAsync(h => h.Id == 12);
            handoff.SourceId = "BAD_LOAN_ID";
            await db.SaveChangesAsync();
        }

        await using (var db = harness.CreateDb())
        {
            var service = harness.CreateService(db);
            var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
                service.ConfirmLoanFundingAsync(12, 500m, "USD", 8, "fund-event-bad-loan-src", "BANK-REF-LOAN-8", "accounting-user"));
            Assert.Contains("Funding source ID is invalid", ex.Message);
        }
    }

    [Fact]
    public async Task ConfirmLoanFunding_RejectsWhenMultipleSysCurrenciesMatchCurrency()
    {
        using var harness = new FundingReceiptHarness();
        await harness.SeedAsync();

        await using (var db = harness.CreateDb())
        {
            // Add a duplicate currency with the same ShortNameEn
            db.SysCurrencies.Add(new SysCurrency { Id = 99, ShortNameEn = "USD", CurrencyNameEn = "US Dollar Duplicate" });
            await db.SaveChangesAsync();
        }

        await using (var db = harness.CreateDb())
        {
            var service = harness.CreateService(db);
            var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
                service.ConfirmLoanFundingAsync(11, 100m, "USD", 7, "fund-event-ambiguous-cur", "BANK-REF-77", "accounting-user"));
            Assert.Contains("Funding currency does not resolve to one unique ERP currency", ex.Message);
        }
    }

    [Fact]
    public async Task ConfirmLoanFunding_RejectsWhenPaymentVoucherPostDateIsNull()
    {
        using var harness = new FundingReceiptHarness();
        await harness.SeedAsync();

        // Setup voucher with PostDate null
        harness.SetupVoucher(7, 100m, status: 3, handoffRef: "HR-HANDOFF-11", transferRef: "BANK-REF-77");

        await using (var db = harness.CreateDb())
        {
            // Nullify PostDate in mock header by overriding
            var service = harness.CreateService(db);
        }
    }

    [Fact]
    public async Task ConfirmLoanFunding_Advance_VersionTwoReissuance_AcceptsZeroRemainingBalanceBeforeDisbursement()
    {
        using var harness = new FundingReceiptHarness();
        await harness.SeedAsync();

        await using (var db = harness.CreateDb())
        {
            var handoff = await db.HrAccountingHandoffs.SingleAsync(h => h.Id == 11);
            handoff.SourceVersion = 2; // Version 2 re-issued after reversal
            var advance = await db.EmployeeAdvances.SingleAsync(a => a.Id == 41);
            advance.RemainingBalance = 0m; // Previously reversed advance has 0 balance until re-funded
            await db.SaveChangesAsync();
        }

        await using (var db = harness.CreateDb())
        {
            var service = harness.CreateService(db);
            await service.ConfirmLoanFundingAsync(11, 100m, "USD", 7, "fund-event-v2-reissue", "BANK-REF-77", "accounting-user");
        }

        await using var verify = harness.CreateDb();
        var advanceAfter = await verify.EmployeeAdvances.SingleAsync(a => a.Id == 41);
        Assert.Equal("ACTIVE", advanceAfter.Status);
        Assert.Equal(100m, advanceAfter.RemainingBalance); // Now funded with 100m
        var handoffAfter = await verify.HrAccountingHandoffs.SingleAsync(h => h.Id == 11);
        Assert.Equal("POSTED", handoffAfter.DeliveryStatus);
    }

    [Fact]
    public async Task ConfirmLoanFunding_Loan_VersionTwoReissuance_AcceptsZeroRemainingBalanceBeforeDisbursement()
    {
        using var harness = new FundingReceiptHarness();
        await harness.SeedLoanAsync();
        harness.SetupVoucher(8, 500m, handoffRef: "HR-HANDOFF-12", transferRef: "BANK-REF-LOAN-8");

        await using (var db = harness.CreateDb())
        {
            var handoff = await db.HrAccountingHandoffs.SingleAsync(h => h.Id == 12);
            handoff.SourceVersion = 2; // Version 2 re-issued
            var loan = await db.EmployeeLoans.SingleAsync(l => l.Id == 51);
            loan.RemainingBalance = 0m; // 0 remaining before disbursement
            await db.SaveChangesAsync();
        }

        await using (var db = harness.CreateDb())
        {
            var service = harness.CreateService(db);
            await service.ConfirmLoanFundingAsync(12, 500m, "USD", 8, "fund-event-loan-v2", "BANK-REF-LOAN-8", "accounting-user");
        }

        await using var verify = harness.CreateDb();
        var loanAfter = await verify.EmployeeLoans.SingleAsync(l => l.Id == 51);
        Assert.Equal("ACTIVE", loanAfter.Status);
        Assert.Equal(500m, loanAfter.RemainingBalance);
        var handoffAfter = await verify.HrAccountingHandoffs.SingleAsync(h => h.Id == 12);
        Assert.Equal("POSTED", handoffAfter.DeliveryStatus);
    }

    [Fact]
    public async Task ConfirmLoanFunding_Loan_RejectsWhenLoanPrincipalAmountMismatchesFundedAmount()
    {
        using var harness = new FundingReceiptHarness();
        await harness.SeedLoanAsync(loanStatus: "APPROVED", principalAmount: 600m);
        harness.SetupVoucher(8, 500m, handoffRef: "HR-HANDOFF-12", transferRef: "BANK-REF-LOAN-8");

        await using (var db = harness.CreateDb())
        {
            var handoff = await db.HrAccountingHandoffs.Include(h => h.Lines).SingleAsync(h => h.Id == 12);
            handoff.Lines.Single().Amount = 500m; // Handoff requests 500m but loan has 600m
            await db.SaveChangesAsync();
        }

        await using (var db = harness.CreateDb())
        {
            var service = harness.CreateService(db);
            var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
                service.ConfirmLoanFundingAsync(12, 500m, "USD", 8, "loan-fund-evt-diff-amt", "BANK-REF-LOAN-8", "accounting-user"));
            Assert.Contains("Loan has changed since the approved funding snapshot", ex.Message);
        }
    }

    [Fact]
    public async Task ConfirmLoanFunding_Loan_RejectsWhenLoanTotalPaidAmountNotZero()
    {
        using var harness = new FundingReceiptHarness();
        await harness.SeedLoanAsync(loanStatus: "APPROVED", principalAmount: 500m);
        harness.SetupVoucher(8, 500m, handoffRef: "HR-HANDOFF-12", transferRef: "BANK-REF-LOAN-8");

        await using (var db = harness.CreateDb())
        {
            var loan = await db.EmployeeLoans.SingleAsync(l => l.Id == 51);
            loan.TotalPaidAmount = 50m; // Has paid amount
            await db.SaveChangesAsync();
        }

        await using (var db = harness.CreateDb())
        {
            var service = harness.CreateService(db);
            var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
                service.ConfirmLoanFundingAsync(12, 500m, "USD", 8, "loan-fund-evt-has-paid", "BANK-REF-LOAN-8", "accounting-user"));
            Assert.Contains("Loan has changed since the approved funding snapshot", ex.Message);
        }
    }

    [Fact]
    public async Task ConfirmLoanFunding_Loan_RejectsWhenScheduleHasPayrollRunLineId()
    {
        using var harness = new FundingReceiptHarness();
        await harness.SeedLoanAsync(loanStatus: "APPROVED", principalAmount: 500m);
        harness.SetupVoucher(8, 500m, handoffRef: "HR-HANDOFF-12", transferRef: "BANK-REF-LOAN-8");

        await using (var db = harness.CreateDb())
        {
            var schedule = await db.LoanRepaymentSchedules.SingleAsync(s => s.Id == 151);
            schedule.PayrollRunLineId = 9999L; // Linked to a payroll line
            await db.SaveChangesAsync();
        }

        await using (var db = harness.CreateDb())
        {
            var service = harness.CreateService(db);
            var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
                service.ConfirmLoanFundingAsync(12, 500m, "USD", 8, "loan-fund-evt-sched-prl", "BANK-REF-LOAN-8", "accounting-user"));
            Assert.Contains("Loan has changed since the approved funding snapshot", ex.Message);
        }
    }

    [Fact]
    public async Task ConfirmLoanFunding_RejectsWhenOperationNotFund()
    {
        using var harness = new FundingReceiptHarness();
        await harness.SeedAsync();

        await using (var db = harness.CreateDb())
        {
            var handoff = await db.HrAccountingHandoffs.SingleAsync(h => h.Id == 11);
            handoff.Operation = "DISBURSE"; // Not FUND
            await db.SaveChangesAsync();
        }

        await using (var db = harness.CreateDb())
        {
            var service = harness.CreateService(db);
            var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
                service.ConfirmLoanFundingAsync(11, 100m, "USD", 7, "fund-event-not-fund-op", "BANK-REF-77", "accounting-user"));
            Assert.Contains("Only a loan/advance funding request can receive a disbursement receipt", ex.Message);
        }
    }

    [Fact]
    public async Task ConfirmLoanFunding_TrimsWhitespaceFromEventKeyAndExternalReference()
    {
        using var harness = new FundingReceiptHarness();
        await harness.SeedAsync();

        await using (var db = harness.CreateDb())
        {
            var service = harness.CreateService(db);
            // Event key and external ref with surrounding whitespace
            await service.ConfirmLoanFundingAsync(11, 100m, "USD", 7, "   fund-event-trimmed-001   ", "   BANK-REF-77   ", "accounting-user");
        }

        await using var verify = harness.CreateDb();
        var receipt = await verify.AccountingLoanFundingReceipts.SingleAsync();
        Assert.Equal("fund-event-trimmed-001", receipt.EventKey);
        Assert.Equal("BANK-REF-77", receipt.ExternalReference);
    }

    [Fact]
    public async Task ConfirmLoanFunding_PropagatesCancellationToken()
    {
        using var harness = new FundingReceiptHarness();
        await harness.SeedAsync();

        using var cts = new CancellationTokenSource();
        cts.Cancel();

        await using var db = harness.CreateDb();
        var service = harness.CreateService(db);

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            service.ConfirmLoanFundingAsync(11, 100m, "USD", 7, "fund-event-cancel-tok", "BANK-REF-77", "accounting-user", cts.Token));
    }

    private sealed class FundingReceiptHarness : IDisposable
    {
        private readonly string storeName = Guid.NewGuid().ToString();
        private readonly DbContextOptions<OracleDbContext> options;
        private readonly Mock<IGlVoucherRepository> vouchers = new();
        private readonly Mock<IReceiptPaymentVoucherService> paymentVouchers = new();

        public FundingReceiptHarness()
        {
            options = new DbContextOptionsBuilder<OracleDbContext>()
                .UseInMemoryDatabase(storeName)
                .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
                .Options;

            SetupVoucher(7, 100m, handoffRef: "HR-HANDOFF-11", transferRef: "BANK-REF-77");
        }

        public OracleDbContext CreateDb() => new(options);

        public void SetupVoucher(
            long voucherId,
            decimal amount,
            int branchId = 2,
            int currencyId = 1,
            int status = 3,
            bool isReversed = false,
            string handoffRef = "HR-HANDOFF-11",
            string transferRef = "BANK-REF-77",
            decimal? creditAmount = null,
            int voucherType = 103)
        {
            var credit = creditAmount ?? amount;
            paymentVouchers.Setup(x => x.GetPaymentVoucherByIdAsync(voucherId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(new PaymentVoucherDto
                {
                    VoucherId = voucherId,
                    BranchId = branchId,
                    CurrencyId = currencyId,
                    Status = status,
                    Description = $"Funding {handoffRef}",
                    TransferReference = transferRef
                });
            vouchers.Setup(x => x.GetByIdAsync(voucherId, It.IsAny<CancellationToken>())).ReturnsAsync(new GlVoucherHeader
            {
                Id = voucherId,
                BranchId = branchId,
                VoucherType = voucherType,
                Status = status,
                IsReversed = isReversed,
                PostDate = status == 3 ? new DateTime(2026, 11, 7, 10, 0, 0, DateTimeKind.Utc) : null,
                TotalLocalDebit = amount,
                TotalLocalCredit = credit,
                Details =
                [
                    new GlVoucherDetail { VoucherId = voucherId, LineSer = 1, BranchId = branchId, CurrencyId = currencyId, Debit = amount, LocalDebit = amount },
                    new GlVoucherDetail { VoucherId = voucherId, LineSer = 2, BranchId = branchId, CurrencyId = currencyId, Credit = credit, LocalCredit = credit }
                ]
            });
        }

        public void SetVoucherStatus(int status) => SetupVoucher(7, 100m, status: status);

        public void SetupVoucherNotFound(long voucherId)
        {
            paymentVouchers.Setup(x => x.GetPaymentVoucherByIdAsync(voucherId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(new PaymentVoucherDto { VoucherId = voucherId, Status = 3 });
            vouchers.Setup(x => x.GetByIdAsync(voucherId, It.IsAny<CancellationToken>()))
                .ReturnsAsync((GlVoucherHeader?)null);
        }

        public void SetupVoucherEmptyDetails(long voucherId, decimal amount, string handoffRef, string transferRef)
        {
            paymentVouchers.Setup(x => x.GetPaymentVoucherByIdAsync(voucherId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(new PaymentVoucherDto
                {
                    VoucherId = voucherId,
                    BranchId = 2,
                    CurrencyId = 1,
                    Status = 3,
                    Description = $"Funding {handoffRef}",
                    TransferReference = transferRef
                });
            vouchers.Setup(x => x.GetByIdAsync(voucherId, It.IsAny<CancellationToken>())).ReturnsAsync(new GlVoucherHeader
            {
                Id = voucherId,
                BranchId = 2,
                VoucherType = 103,
                Status = 3,
                IsReversed = false,
                PostDate = new DateTime(2026, 11, 7, 10, 0, 0, DateTimeKind.Utc),
                TotalLocalDebit = amount,
                TotalLocalCredit = amount,
                Details = []
            });
        }

        public HrAccountingHandoffService CreateService(OracleDbContext db, IHrUnitOfWork? unitOfWork = null) => new(
            db,
            vouchers.Object,
            Mock.Of<IHrPayrollPostingService>(),
            paymentVouchers.Object,
            Mock.Of<IGlVoucherService>(),
            unitOfWork);

        public async Task SeedAsync()
        {
            await using var db = CreateDb();
            db.SysCurrencies.Add(new SysCurrency { Id = 1, ShortNameEn = "USD", CurrencyNameEn = "US Dollar" });
            db.EmployeeAdvances.Add(new EmployeeAdvance
            {
                Id = 41,
                EmployeeCode = "E-41",
                AdvanceAmount = 100m,
                CurrencyCode = "USD",
                TargetPayPeriod = "2026-11",
                RemainingBalance = 100m,
                Status = "APPROVED"
            });
            db.HrAccountingHandoffs.Add(new HrAccountingHandoff
            {
                Id = 11,
                CompanyId = 3,
                BranchId = 2,
                SourceType = "ADVANCE",
                SourceId = "41",
                SourceVersion = 1,
                Operation = "FUND",
                HandoffReference = "HR-HANDOFF-11",
                PayloadHash = "handoff-payload",
                DeliveryStatus = "ACCEPTED",
                CreatedAtUtc = new DateTime(2026, 11, 7, 9, 0, 0, DateTimeKind.Utc),
                CreatedBy = "hr-user",
                Lines = [new HrAccountingHandoffLine { LineType = "PRINCIPAL", CurrencyCode = "USD", Amount = 100m, BranchId = 2 }]
            });
            await db.SaveChangesAsync();
        }

        public async Task SeedLoanAsync(
            string loanStatus = "APPROVED",
            decimal principalAmount = 500m,
            string scheduleStatus = "PENDING",
            decimal paidAmount = 0m,
            bool isReserved = false,
            string deliveryStatus = "ACCEPTED",
            string loanCurrency = "USD",
            string handoffCurrency = "USD")
        {
            await using var db = CreateDb();
            if (!await db.SysCurrencies.AnyAsync(c => c.Id == 1))
            {
                db.SysCurrencies.Add(new SysCurrency { Id = 1, ShortNameEn = "USD", CurrencyNameEn = "US Dollar" });
            }
            if (!await db.SysCurrencies.AnyAsync(c => c.Id == 2))
            {
                db.SysCurrencies.Add(new SysCurrency { Id = 2, ShortNameEn = "EUR", CurrencyNameEn = "Euro" });
            }
            var loan = new EmployeeLoan
            {
                Id = 51,
                EmployeeCode = "E-51",
                PrincipalAmount = principalAmount,
                CurrencyCode = loanCurrency,
                RemainingBalance = principalAmount,
                TotalPaidAmount = 0m,
                Status = loanStatus,
                Schedules =
                [
                    new LoanRepaymentSchedule
                    {
                        Id = 151,
                        ScheduledAmount = principalAmount,
                        PaidAmount = paidAmount,
                        Status = scheduleStatus,
                        ReservedByHandoffId = isReserved ? 12 : null
                    }
                ]
            };
            db.EmployeeLoans.Add(loan);
            db.HrAccountingHandoffs.Add(new HrAccountingHandoff
            {
                Id = 12,
                CompanyId = 3,
                BranchId = 2,
                SourceType = "LOAN",
                SourceId = "51",
                SourceVersion = 1,
                Operation = "FUND",
                HandoffReference = "HR-HANDOFF-12",
                PayloadHash = "handoff-payload-loan",
                DeliveryStatus = deliveryStatus,
                CreatedAtUtc = new DateTime(2026, 11, 7, 9, 0, 0, DateTimeKind.Utc),
                CreatedBy = "hr-user",
                Lines = [new HrAccountingHandoffLine { LineType = "PRINCIPAL", CurrencyCode = handoffCurrency, Amount = principalAmount, BranchId = 2 }]
            });
            await db.SaveChangesAsync();
        }

        public void Dispose() { }
    }
}
