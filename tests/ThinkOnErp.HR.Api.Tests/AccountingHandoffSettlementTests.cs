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

public sealed class AccountingHandoffSettlementTests
{
    [Fact]
    public async Task PostedJournalReceiptSettlesReservedAdvanceExactlyOnce()
    {
        using var harness = new Harness();
        await harness.SeedAdvanceAsync(reserved: true);

        await using (var db = harness.CreateDb())
        {
            var service = harness.CreateService(db);
            await service.ConfirmJournalPostedAsync(11, 7, "posting-event-001", "accounting");
            await service.ConfirmJournalPostedAsync(11, 7, "posting-event-001", "accounting");
        }

        await using var verify = harness.CreateDb();
        var advance = await verify.EmployeeAdvances.SingleAsync();
        Assert.Equal(40m, advance.DeductedAmount);
        Assert.Equal(60m, advance.RemainingBalance);
        Assert.Equal("ACTIVE", advance.Status);
        Assert.Null(advance.ReservedByHandoffId);
        Assert.Equal("POSTED", (await verify.HrAccountingHandoffs.SingleAsync()).DeliveryStatus);
        var receipt = await verify.AccountingHandoffPostingReceipts.SingleAsync();
        Assert.Equal(7, receipt.VoucherId);
        Assert.Contains("ADVANCE", receipt.SettlementSnapshotJson);
        Assert.Single(await verify.HrAccountingHandoffTransitions.Where(t => t.ToStatus == "POSTED").ToListAsync());
    }

    [Fact]
    public async Task PostedJournalReceiptIsRejectedWhenHandoffReservationIsMissing()
    {
        using var harness = new Harness();
        await harness.SeedAdvanceAsync(reserved: false);

        await using (var db = harness.CreateDb())
            await Assert.ThrowsAsync<InvalidOperationException>(() => harness.CreateService(db).ConfirmJournalPostedAsync(
                11, 7, "posting-event-001", "accounting"));

        await using var verify = harness.CreateDb();
        Assert.Equal("ACCEPTED", (await verify.HrAccountingHandoffs.SingleAsync()).DeliveryStatus);
        Assert.Empty(await verify.AccountingHandoffPostingReceipts.ToListAsync());
        var advance = await verify.EmployeeAdvances.SingleAsync();
        Assert.Equal(0m, advance.DeductedAmount);
        Assert.Equal(100m, advance.RemainingBalance);
    }

    [Fact]
    public async Task PostedJournalReceiptSettlesReservedLoanScheduleExactlyOnce()
    {
        using var harness = new Harness();
        await harness.SeedLoanScheduleAsync(reserved: true, scheduledAmount: 100m, deductionAmount: 100m, loanRemainingBalance: 500m);

        await using (var db = harness.CreateDb())
        {
            var service = harness.CreateService(db);
            await service.ConfirmJournalPostedAsync(11, 7, "posting-event-loan-001", "accounting");
            // Idempotent retry
            await service.ConfirmJournalPostedAsync(11, 7, "posting-event-loan-001", "accounting");
        }

        await using var verify = harness.CreateDb();
        var schedule = await verify.LoanRepaymentSchedules.SingleAsync(s => s.Id == 101);
        Assert.Equal("PAID", schedule.Status);
        Assert.Equal(100m, schedule.PaidAmount);
        Assert.Equal(0m, schedule.CarriedForwardAmount);
        Assert.Null(schedule.ReservedByHandoffId);
        Assert.Equal(501, schedule.PayrollRunLineId);
        Assert.NotNull(schedule.PaidDate);

        var loan = await verify.EmployeeLoans.SingleAsync(l => l.Id == 51);
        Assert.Equal(100m, loan.TotalPaidAmount);
        Assert.Equal(400m, loan.RemainingBalance);
        Assert.Equal("ACTIVE", loan.Status);

        Assert.Equal("POSTED", (await verify.HrAccountingHandoffs.SingleAsync()).DeliveryStatus);
        var receipt = await verify.AccountingHandoffPostingReceipts.SingleAsync();
        Assert.Contains("SCHEDULE", receipt.SettlementSnapshotJson);
        Assert.Single(await verify.HrAccountingHandoffTransitions.Where(t => t.ToStatus == "POSTED").ToListAsync());
    }

    [Fact]
    public async Task PostedJournalReceiptSettlesLastLoanScheduleAndMarksLoanCompleted()
    {
        using var harness = new Harness();
        await harness.SeedLoanScheduleAsync(reserved: true, scheduledAmount: 100m, deductionAmount: 100m, loanRemainingBalance: 100m, loanTotalPaid: 400m);

        await using (var db = harness.CreateDb())
        {
            var service = harness.CreateService(db);
            await service.ConfirmJournalPostedAsync(11, 7, "posting-event-loan-final", "accounting");
        }

        await using var verify = harness.CreateDb();
        var schedule = await verify.LoanRepaymentSchedules.SingleAsync(s => s.Id == 101);
        Assert.Equal("PAID", schedule.Status);
        Assert.Equal(100m, schedule.PaidAmount);

        var loan = await verify.EmployeeLoans.SingleAsync(l => l.Id == 51);
        Assert.Equal(500m, loan.TotalPaidAmount);
        Assert.Equal(0m, loan.RemainingBalance);
        Assert.Equal("COMPLETED", loan.Status);
    }

    [Fact]
    public async Task PostedJournalReceiptSettlesPartialLoanScheduleWhenAmountIsLess()
    {
        using var harness = new Harness();
        await harness.SeedLoanScheduleAsync(reserved: true, scheduledAmount: 100m, deductionAmount: 60m, loanRemainingBalance: 500m);

        await using (var db = harness.CreateDb())
        {
            var service = harness.CreateService(db);
            await service.ConfirmJournalPostedAsync(11, 7, "posting-event-loan-partial", "accounting");
        }

        await using var verify = harness.CreateDb();
        var schedule = await verify.LoanRepaymentSchedules.SingleAsync(s => s.Id == 101);
        Assert.Equal("PARTIAL", schedule.Status);
        Assert.Equal(60m, schedule.PaidAmount);
        Assert.Equal(40m, schedule.CarriedForwardAmount);
        Assert.Null(schedule.ReservedByHandoffId);

        var loan = await verify.EmployeeLoans.SingleAsync(l => l.Id == 51);
        Assert.Equal(60m, loan.TotalPaidAmount);
        Assert.Equal(440m, loan.RemainingBalance);
        Assert.Equal("ACTIVE", loan.Status);
    }

    [Fact]
    public async Task PostedJournalReceiptSettlesBothAdvanceAndLoanScheduleInSameHandoff()
    {
        using var harness = new Harness();
        await harness.SeedBothAdvanceAndLoanAsync(reserved: true);

        await using (var db = harness.CreateDb())
        {
            var service = harness.CreateService(db);
            await service.ConfirmJournalPostedAsync(11, 7, "posting-event-mixed-001", "accounting");
        }

        await using var verify = harness.CreateDb();
        var advance = await verify.EmployeeAdvances.SingleAsync();
        Assert.Equal(40m, advance.DeductedAmount);
        Assert.Equal(60m, advance.RemainingBalance);
        Assert.Null(advance.ReservedByHandoffId);

        var schedule = await verify.LoanRepaymentSchedules.SingleAsync();
        Assert.Equal("PAID", schedule.Status);
        Assert.Equal(100m, schedule.PaidAmount);
        Assert.Null(schedule.ReservedByHandoffId);

        var loan = await verify.EmployeeLoans.SingleAsync();
        Assert.Equal(100m, loan.TotalPaidAmount);
        Assert.Equal(400m, loan.RemainingBalance);

        var receipt = await verify.AccountingHandoffPostingReceipts.SingleAsync();
        Assert.Contains("ADVANCE", receipt.SettlementSnapshotJson);
        Assert.Contains("SCHEDULE", receipt.SettlementSnapshotJson);
    }

    [Fact]
    public async Task PostedJournalReceiptIsRejectedWhenLoanScheduleReservationMissing()
    {
        using var harness = new Harness();
        await harness.SeedLoanScheduleAsync(reserved: false, scheduledAmount: 100m, deductionAmount: 100m);

        await using (var db = harness.CreateDb())
        {
            var service = harness.CreateService(db);
            var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
                service.ConfirmJournalPostedAsync(11, 7, "posting-event-loan-001", "accounting"));
            Assert.Contains("A payroll reservation is missing or no longer belongs to this handoff", ex.Message);
        }
    }

    [Fact]
    public async Task PostedJournalReceiptIsRejectedWhenLoanNotActive()
    {
        using var harness = new Harness();
        await harness.SeedLoanScheduleAsync(reserved: true, scheduledAmount: 100m, deductionAmount: 100m, loanStatus: "APPROVED");

        await using (var db = harness.CreateDb())
        {
            var service = harness.CreateService(db);
            var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
                service.ConfirmJournalPostedAsync(11, 7, "posting-event-loan-001", "accounting"));
            Assert.Contains("Reserved loan schedule 101 changed before posting receipt", ex.Message);
        }
    }

    [Fact]
    public async Task PostedJournalReceiptIsRejectedWhenLoanCurrencyMismatch()
    {
        using var harness = new Harness();
        await harness.SeedLoanScheduleAsync(reserved: true, scheduledAmount: 100m, deductionAmount: 100m, loanCurrency: "EUR", handoffCurrency: "USD");

        await using (var db = harness.CreateDb())
        {
            var service = harness.CreateService(db);
            var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
                service.ConfirmJournalPostedAsync(11, 7, "posting-event-loan-001", "accounting"));
            Assert.Contains("Reserved loan schedule 101 changed before posting receipt", ex.Message);
        }
    }

    [Fact]
    public async Task PostedJournalReceiptIsRejectedWhenReferenceMalformed()
    {
        using var harness = new Harness();
        await harness.SeedLoanScheduleAsync(reserved: true, sourceReference: "SCHEDULE:101:INVALID");

        await using (var db = harness.CreateDb())
        {
            var service = harness.CreateService(db);
            var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
                service.ConfirmJournalPostedAsync(11, 7, "posting-event-loan-001", "accounting"));
            Assert.Contains("Payroll reservation reference is malformed", ex.Message);
        }
    }

    [Fact]
    public async Task PostedJournalReceiptIsRejectedWhenVoucherNotPosted()
    {
        using var harness = new Harness();
        await harness.SeedAdvanceAsync(reserved: true);
        harness.SetVoucherStatus(1, hasPostDate: false);

        await using (var db = harness.CreateDb())
        {
            var service = harness.CreateService(db);
            var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
                service.ConfirmJournalPostedAsync(11, 7, "posting-event-001", "accounting"));
            Assert.Contains("Only an ERP-posted journal voucher can produce a posting receipt", ex.Message);
        }
    }

    [Fact]
    public async Task PostedJournalReceiptIsRejectedWhenHandoffNotAccepted()
    {
        using var harness = new Harness();
        await harness.SeedAdvanceAsync(reserved: true, deliveryStatus: "DELIVERED");

        await using (var db = harness.CreateDb())
        {
            var service = harness.CreateService(db);
            var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
                service.ConfirmJournalPostedAsync(11, 7, "posting-event-001", "accounting"));
            Assert.Contains("Only an accepted handoff can receive its posting receipt", ex.Message);
        }
    }

    [Fact]
    public async Task PostedJournalReceiptSettlesAdvanceFullyAndMarksRecovered()
    {
        using var harness = new Harness();
        // Advance with 100m balance, fully deducted by 100m deduction amount
        await harness.SeedAdvanceAsync(reserved: true, advanceAmount: 100m, remainingBalance: 100m, deductionAmount: 100m);

        await using (var db = harness.CreateDb())
        {
            var service = harness.CreateService(db);
            await service.ConfirmJournalPostedAsync(11, 7, "posting-event-full-adv", "accounting");
        }

        await using var verify = harness.CreateDb();
        var advance = await verify.EmployeeAdvances.SingleAsync();
        Assert.Equal(100m, advance.DeductedAmount);
        Assert.Equal(0m, advance.RemainingBalance);
        Assert.Equal("RECOVERED", advance.Status);
        Assert.Null(advance.ReservedByHandoffId);
        Assert.Equal("accounting", advance.UpdateUser);
        Assert.NotNull(advance.UpdateDate);
    }

    [Fact]
    public async Task PostedJournalReceiptIsRejectedWhenAdvanceCurrencyMismatch()
    {
        using var harness = new Harness();
        await harness.SeedAdvanceAsync(reserved: true, advanceCurrency: "EUR", handoffCurrency: "USD");

        await using (var db = harness.CreateDb())
        {
            var service = harness.CreateService(db);
            var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
                service.ConfirmJournalPostedAsync(11, 7, "posting-event-cur-mismatch", "accounting"));
            Assert.Contains("Reserved advance 41 changed before posting receipt", ex.Message);
        }
    }

    [Fact]
    public async Task PostedJournalReceiptIsRejectedWhenAdvanceBalanceLessThanDeduction()
    {
        using var harness = new Harness();
        // Remaining balance 30m is less than deduction amount 40m
        await harness.SeedAdvanceAsync(reserved: true, remainingBalance: 30m, deductionAmount: 40m);

        await using (var db = harness.CreateDb())
        {
            var service = harness.CreateService(db);
            var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
                service.ConfirmJournalPostedAsync(11, 7, "posting-event-low-bal", "accounting"));
            Assert.Contains("Reserved advance 41 changed before posting receipt", ex.Message);
        }
    }

    [Fact]
    public async Task PostedJournalReceiptIsRejectedWhenAdvanceStatusIsNotActive()
    {
        using var harness = new Harness();
        await harness.SeedAdvanceAsync(reserved: true, advanceStatus: "RECOVERED");

        await using (var db = harness.CreateDb())
        {
            var service = harness.CreateService(db);
            var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
                service.ConfirmJournalPostedAsync(11, 7, "posting-event-not-active", "accounting"));
            Assert.Contains("Reserved advance 41 changed before posting receipt", ex.Message);
        }
    }

    [Fact]
    public async Task PostedJournalReceiptIsRejectedWhenHandoffNotFound()
    {
        using var harness = new Harness();
        await harness.SeedAdvanceAsync(reserved: true);

        await using (var db = harness.CreateDb())
        {
            var service = harness.CreateService(db);
            var ex = await Assert.ThrowsAsync<KeyNotFoundException>(() =>
                service.ConfirmJournalPostedAsync(9999, 7, "posting-event-001", "accounting"));
            Assert.Contains("Handoff not found in active scope", ex.Message);
        }
    }

    [Fact]
    public async Task PostedJournalReceiptIsRejectedWhenJournalVoucherNotFound()
    {
        using var harness = new Harness();
        await harness.SeedAdvanceAsync(reserved: true);

        await using (var db = harness.CreateDb())
        {
            var service = harness.CreateService(db);
            var ex = await Assert.ThrowsAsync<KeyNotFoundException>(() =>
                service.ConfirmJournalPostedAsync(11, 9999, "posting-event-001", "accounting"));
            Assert.Contains("Journal voucher was not found", ex.Message);
        }
    }

    [Fact]
    public async Task PostedJournalReceiptIsRejectedWhenJournalVoucherBranchMismatch()
    {
        using var harness = new Harness();
        await harness.SeedAdvanceAsync(reserved: true);
        harness.SetVoucherStatus(3, hasPostDate: true, branchId: 99); // branch 99 vs handoff branch 2

        await using (var db = harness.CreateDb())
        {
            var service = harness.CreateService(db);
            var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
                service.ConfirmJournalPostedAsync(11, 7, "posting-event-001", "accounting"));
            Assert.Contains("Journal voucher is not linked to this handoff", ex.Message);
        }
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("short")] // length < 8
    public async Task PostedJournalReceiptIsRejectedWhenEventKeyIsInvalid(string invalidEventKey)
    {
        using var harness = new Harness();
        await harness.SeedAdvanceAsync(reserved: true);

        await using (var db = harness.CreateDb())
        {
            var service = harness.CreateService(db);
            await Assert.ThrowsAsync<ArgumentException>(() =>
                service.ConfirmJournalPostedAsync(11, 7, invalidEventKey, "accounting"));
        }
    }

    [Fact]
    public async Task PostedJournalReceipt_EventKeyExceedingMaxLength_ThrowsArgumentException()
    {
        using var harness = new Harness();
        await harness.SeedAdvanceAsync(reserved: true);
        var longKey = new string('K', 161);

        await using var db = harness.CreateDb();
        var service = harness.CreateService(db);
        await Assert.ThrowsAsync<ArgumentException>(() =>
            service.ConfirmJournalPostedAsync(11, 7, longKey, "accounting"));
    }

    [Fact]
    public async Task PostedJournalReceipt_ConflictingPostingReceipt_ThrowsInvalidOperationException()
    {
        using var harness = new Harness();
        await harness.SeedAdvanceAsync(reserved: true);

        await using (var db = harness.CreateDb())
        {
            db.AccountingHandoffPostingReceipts.Add(new AccountingHandoffPostingReceipt
            {
                HandoffId = 11,
                VoucherId = 88,
                EventKey = "prior-different-event-key",
                PayloadHash = "prior-different-hash",
                ReceivedAtUtc = DateTime.UtcNow,
                ReceivedBy = "prior-user"
            });
            await db.SaveChangesAsync();
        }

        await using (var db = harness.CreateDb())
        {
            var service = harness.CreateService(db);
            var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
                service.ConfirmJournalPostedAsync(11, 7, "posting-event-001", "accounting"));
            Assert.Contains("A conflicting posting receipt already exists", ex.Message);
        }
    }

    [Fact]
    public async Task PostedJournalReceipt_HandoffSourceIdInvalid_ThrowsInvalidOperationException()
    {
        using var harness = new Harness();
        await harness.SeedAdvanceAsync(reserved: true);

        await using (var db = harness.CreateDb())
        {
            var handoff = await db.HrAccountingHandoffs.SingleAsync(h => h.Id == 11);
            handoff.SourceId = "NON_NUMERIC_SOURCE";
            await db.SaveChangesAsync();
        }

        await using (var db = harness.CreateDb())
        {
            var service = harness.CreateService(db);
            var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
                service.ConfirmJournalPostedAsync(11, 7, "posting-event-001", "accounting"));
            Assert.Contains("Payroll handoff source ID is invalid", ex.Message);
        }
    }

    [Fact]
    public async Task PostedJournalReceipt_VoucherSourceSystemCodeMismatch_ThrowsInvalidOperationException()
    {
        using var harness = new Harness();
        await harness.SeedAdvanceAsync(reserved: true);
        harness.SetVoucherDetails("ERP_MANUAL", 11);

        await using (var db = harness.CreateDb())
        {
            var service = harness.CreateService(db);
            var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
                service.ConfirmJournalPostedAsync(11, 7, "posting-event-001", "accounting"));
            Assert.Contains("Journal voucher is not linked to this handoff", ex.Message);
        }
    }

    [Fact]
    public async Task PostedJournalReceipt_VoucherSourceRefIdMismatch_ThrowsInvalidOperationException()
    {
        using var harness = new Harness();
        await harness.SeedAdvanceAsync(reserved: true);
        harness.SetVoucherDetails("HR_HANDOFF", 9999);

        await using (var db = harness.CreateDb())
        {
            var service = harness.CreateService(db);
            var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
                service.ConfirmJournalPostedAsync(11, 7, "posting-event-001", "accounting"));
            Assert.Contains("Journal voucher is not linked to this handoff", ex.Message);
        }
    }

    [Fact]
    public async Task PostedJournalReceipt_ScheduleDueLessThanAmount_ThrowsInvalidOperationException()
    {
        using var harness = new Harness();
        await harness.SeedLoanScheduleAsync(reserved: true, scheduledAmount: 50m, deductionAmount: 40m);

        await using (var db = harness.CreateDb())
        {
            var schedule = await db.LoanRepaymentSchedules.SingleAsync(s => s.Id == 101);
            schedule.PaidAmount = 30m; // due is 50 - 30 = 20, but deduction line is 40
            await db.SaveChangesAsync();
        }

        await using (var db = harness.CreateDb())
        {
            var service = harness.CreateService(db);
            var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
                service.ConfirmJournalPostedAsync(11, 7, "posting-event-sched-due", "accounting"));
            Assert.Contains("Reserved loan schedule 101 changed before posting receipt", ex.Message);
        }
    }

    [Fact]
    public async Task PostedJournalReceipt_ScheduleReservedByDifferentHandoff_ThrowsInvalidOperationException()
    {
        using var harness = new Harness();
        await harness.SeedLoanScheduleAsync(reserved: true, scheduledAmount: 100m, deductionAmount: 100m);

        await using (var db = harness.CreateDb())
        {
            var schedule = await db.LoanRepaymentSchedules.SingleAsync(s => s.Id == 101);
            schedule.ReservedByHandoffId = 9999;
            await db.SaveChangesAsync();
        }

        await using (var db = harness.CreateDb())
        {
            var service = harness.CreateService(db);
            var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
                service.ConfirmJournalPostedAsync(11, 7, "posting-event-sched-res", "accounting"));
            Assert.Contains("A payroll reservation is missing or no longer belongs to this handoff", ex.Message);
        }
    }

    [Fact]
    public async Task PostedJournalReceipt_AdvanceReservedByDifferentHandoff_ThrowsInvalidOperationException()
    {
        using var harness = new Harness();
        await harness.SeedAdvanceAsync(reserved: true);

        await using (var db = harness.CreateDb())
        {
            var advance = await db.EmployeeAdvances.SingleAsync();
            advance.ReservedByHandoffId = 9999;
            await db.SaveChangesAsync();
        }

        await using (var db = harness.CreateDb())
        {
            var service = harness.CreateService(db);
            var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
                service.ConfirmJournalPostedAsync(11, 7, "posting-event-adv-res", "accounting"));
            Assert.Contains("A payroll reservation is missing or no longer belongs to this handoff", ex.Message);
        }
    }

    [Fact]
    public async Task PostedJournalReceipt_AdvanceMissing_ThrowsInvalidOperationException()
    {
        using var harness = new Harness();
        await harness.SeedAdvanceAsync(reserved: true);

        await using (var db = harness.CreateDb())
        {
            var advance = await db.EmployeeAdvances.SingleAsync();
            db.EmployeeAdvances.Remove(advance);
            await db.SaveChangesAsync();
        }

        await using (var db = harness.CreateDb())
        {
            var service = harness.CreateService(db);
            var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
                service.ConfirmJournalPostedAsync(11, 7, "posting-event-adv-missing", "accounting"));
            Assert.Contains("A payroll reservation is missing or no longer belongs to this handoff", ex.Message);
        }
    }

    [Fact]
    public async Task PostedJournalReceipt_ScheduleMissing_ThrowsInvalidOperationException()
    {
        using var harness = new Harness();
        await harness.SeedLoanScheduleAsync(reserved: true, scheduledAmount: 100m, deductionAmount: 100m);

        await using (var db = harness.CreateDb())
        {
            var schedule = await db.LoanRepaymentSchedules.SingleAsync(s => s.Id == 101);
            db.LoanRepaymentSchedules.Remove(schedule);
            await db.SaveChangesAsync();
        }

        await using (var db = harness.CreateDb())
        {
            var service = harness.CreateService(db);
            var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
                service.ConfirmJournalPostedAsync(11, 7, "posting-event-sched-missing", "accounting"));
            Assert.Contains("A payroll reservation is missing or no longer belongs to this handoff", ex.Message);
        }
    }

    [Fact]
    public async Task PostedJournalReceipt_WhenMultipleAdvancesInSameHandoff_SettlesAllAtomically()
    {
        using var harness = new Harness();
        await harness.SeedMultipleAdvancesAsync(reserved: true);

        await using (var db = harness.CreateDb())
        {
            var service = harness.CreateService(db);
            await service.ConfirmJournalPostedAsync(11, 7, "posting-event-multi-adv-001", "accounting");
        }

        await using var verify = harness.CreateDb();
        var adv1 = await verify.EmployeeAdvances.SingleAsync(a => a.Id == 41);
        Assert.Equal(40m, adv1.DeductedAmount);
        Assert.Equal(60m, adv1.RemainingBalance);
        Assert.Equal("ACTIVE", adv1.Status);
        Assert.Null(adv1.ReservedByHandoffId);

        var adv2 = await verify.EmployeeAdvances.SingleAsync(a => a.Id == 42);
        Assert.Equal(50m, adv2.DeductedAmount);
        Assert.Equal(0m, adv2.RemainingBalance);
        Assert.Equal("RECOVERED", adv2.Status);
        Assert.Null(adv2.ReservedByHandoffId);

        var receipt = await verify.AccountingHandoffPostingReceipts.SingleAsync();
        Assert.Contains("ADVANCE", receipt.SettlementSnapshotJson);
        Assert.Equal("POSTED", (await verify.HrAccountingHandoffs.SingleAsync()).DeliveryStatus);
    }

    [Fact]
    public async Task PostedJournalReceipt_WhenMultipleLoanSchedulesInSameHandoff_SettlesAllAtomically()
    {
        using var harness = new Harness();
        await harness.SeedMultipleLoanSchedulesAsync(reserved: true);

        await using (var db = harness.CreateDb())
        {
            var service = harness.CreateService(db);
            await service.ConfirmJournalPostedAsync(11, 7, "posting-event-multi-sched-001", "accounting");
        }

        await using var verify = harness.CreateDb();
        var s1 = await verify.LoanRepaymentSchedules.SingleAsync(s => s.Id == 101);
        Assert.Equal("PAID", s1.Status);
        Assert.Equal(100m, s1.PaidAmount);
        Assert.Null(s1.ReservedByHandoffId);

        var s2 = await verify.LoanRepaymentSchedules.SingleAsync(s => s.Id == 102);
        Assert.Equal("PARTIAL", s2.Status);
        Assert.Equal(60m, s2.PaidAmount);
        Assert.Equal(40m, s2.CarriedForwardAmount);
        Assert.Null(s2.ReservedByHandoffId);

        var loan1 = await verify.EmployeeLoans.SingleAsync(l => l.Id == 51);
        Assert.Equal(100m, loan1.TotalPaidAmount);
        Assert.Equal(400m, loan1.RemainingBalance);

        var loan2 = await verify.EmployeeLoans.SingleAsync(l => l.Id == 52);
        Assert.Equal(60m, loan2.TotalPaidAmount);
        Assert.Equal(240m, loan2.RemainingBalance);

        var receipt = await verify.AccountingHandoffPostingReceipts.SingleAsync();
        Assert.Contains("SCHEDULE", receipt.SettlementSnapshotJson);
    }

    [Fact]
    public async Task PostedJournalReceipt_PartialAdvanceSettlement_UpdatesDeductedAndRemainingBalance_KeepsActive()
    {
        using var harness = new Harness();
        await harness.SeedAdvanceAsync(reserved: true, advanceAmount: 100m, remainingBalance: 100m, deductionAmount: 35m);

        await using (var db = harness.CreateDb())
        {
            var service = harness.CreateService(db);
            await service.ConfirmJournalPostedAsync(11, 7, "posting-event-partial-adv", "accounting");
        }

        await using var verify = harness.CreateDb();
        var advance = await verify.EmployeeAdvances.SingleAsync();
        Assert.Equal(35m, advance.DeductedAmount);
        Assert.Equal(65m, advance.RemainingBalance);
        Assert.Equal("ACTIVE", advance.Status);
        Assert.Null(advance.ReservedByHandoffId);
    }

    [Fact]
    public async Task PostedJournalReceipt_WhenUserPassed_SavesReceivedByAndTransitionActor()
    {
        using var harness = new Harness();
        await harness.SeedAdvanceAsync(reserved: true);

        await using (var db = harness.CreateDb())
        {
            var service = harness.CreateService(db);
            await service.ConfirmJournalPostedAsync(11, 7, "posting-event-user-audit", "cfo_auditor");
        }

        await using var verify = harness.CreateDb();
        var receipt = await verify.AccountingHandoffPostingReceipts.SingleAsync();
        Assert.Equal("cfo_auditor", receipt.ReceivedBy);

        var transition = await verify.HrAccountingHandoffTransitions.SingleAsync(t => t.ToStatus == "POSTED");
        Assert.Equal("cfo_auditor", transition.Actor);
    }

    [Fact]
    public async Task PostedJournalReceipt_WhenAdvanceDeductionMatchesRemainingBalanceExactly_MarksRecovered()
    {
        using var harness = new Harness();
        await harness.SeedAdvanceAsync(reserved: true, advanceAmount: 200m, remainingBalance: 75m, deductionAmount: 75m);

        await using (var db = harness.CreateDb())
        {
            var service = harness.CreateService(db);
            await service.ConfirmJournalPostedAsync(11, 7, "posting-event-exact-bal", "accounting");
        }

        await using var verify = harness.CreateDb();
        var advance = await verify.EmployeeAdvances.SingleAsync();
        Assert.Equal(200m, advance.DeductedAmount);
        Assert.Equal(0m, advance.RemainingBalance);
        Assert.Equal("RECOVERED", advance.Status);
    }

    [Fact]
    public async Task PostedJournalReceipt_WhenLoanRemainingMatchesDeduction_MarksLoanCompleted()
    {
        using var harness = new Harness();
        await harness.SeedLoanScheduleAsync(reserved: true, scheduledAmount: 150m, deductionAmount: 150m, loanRemainingBalance: 150m, loanTotalPaid: 350m);

        await using (var db = harness.CreateDb())
        {
            var service = harness.CreateService(db);
            await service.ConfirmJournalPostedAsync(11, 7, "posting-event-loan-complete", "accounting");
        }

        await using var verify = harness.CreateDb();
        var loan = await verify.EmployeeLoans.SingleAsync(l => l.Id == 51);
        Assert.Equal(0m, loan.RemainingBalance);
        Assert.Equal(500m, loan.TotalPaidAmount);
        Assert.Equal("COMPLETED", loan.Status);
    }

    [Fact]
    public async Task PostedJournalReceipt_WhenLoanScheduleBranchMismatch_ThrowsInvalidOperationException()
    {
        using var harness = new Harness();
        await harness.SeedLoanScheduleAsync(reserved: true, scheduledAmount: 100m, deductionAmount: 100m);

        // Mutate voucher branch to 99 to simulate branch mismatch with handoff branch 2
        harness.SetVoucherStatus(3, hasPostDate: true, branchId: 99);

        await using (var db = harness.CreateDb())
        {
            var service = harness.CreateService(db);
            var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
                service.ConfirmJournalPostedAsync(11, 7, "posting-event-branch-mismatch", "accounting"));
            Assert.Contains("Journal voucher is not linked to this handoff", ex.Message);
        }
    }

    [Fact]
    public async Task ConfirmJournalPostedAsync_PropagatesCancellationToken()
    {
        using var harness = new Harness();
        await harness.SeedAdvanceAsync(reserved: true);

        using var cts = new CancellationTokenSource();
        var ct = cts.Token;

        await using (var db = harness.CreateDb())
        {
            var service = harness.CreateService(db);
            await service.ConfirmJournalPostedAsync(11, 7, "posting-event-ct", "accounting", ct);
        }

        await using var verify = harness.CreateDb();
        var advance = await verify.EmployeeAdvances.SingleAsync();
        Assert.Equal(40m, advance.DeductedAmount);
        Assert.Equal(60m, advance.RemainingBalance);
    }

    [Fact]
    public async Task ConfirmJournalPostedAsync_WhenHandoffDeliveryStatusCancelled_ThrowsInvalidOperationException()
    {
        using var harness = new Harness();
        await harness.SeedAdvanceAsync(reserved: true, deliveryStatus: "CANCELLED");

        await using (var db = harness.CreateDb())
        {
            var service = harness.CreateService(db);
            var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
                service.ConfirmJournalPostedAsync(11, 7, "posting-event-cancelled", "accounting"));
            Assert.Contains("Only an accepted handoff can receive its posting receipt", ex.Message);
        }
    }

    [Fact]
    public async Task ConfirmJournalPostedAsync_WhenHandoffDeliveryStatusReturned_ThrowsInvalidOperationException()
    {
        using var harness = new Harness();
        await harness.SeedAdvanceAsync(reserved: true, deliveryStatus: "RETURNED");

        await using (var db = harness.CreateDb())
        {
            var service = harness.CreateService(db);
            var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
                service.ConfirmJournalPostedAsync(11, 7, "posting-event-returned", "accounting"));
            Assert.Contains("Only an accepted handoff can receive its posting receipt", ex.Message);
        }
    }

    [Fact]
    public async Task ConfirmJournalPostedAsync_WhenHandoffNotFound_ThrowsKeyNotFoundException()
    {
        using var harness = new Harness();
        await harness.SeedAdvanceAsync(reserved: true);

        await using (var db = harness.CreateDb())
        {
            var service = harness.CreateService(db);
            await Assert.ThrowsAsync<KeyNotFoundException>(() =>
                service.ConfirmJournalPostedAsync(999, 7, "posting-event-not-found", "accounting"));
        }
    }

    [Fact]
    public async Task PostedJournalReceipt_WhenBothAdvanceAndLoanInSameHandoff_SettlesBothAtomically()
    {
        using var harness = new Harness();
        await harness.SeedBothAdvanceAndLoanAsync(reserved: true);

        await using (var db = harness.CreateDb())
        {
            var service = harness.CreateService(db);
            await service.ConfirmJournalPostedAsync(11, 7, "posting-event-both-001", "cfo");
        }

        await using var verify = harness.CreateDb();
        var advance = await verify.EmployeeAdvances.SingleAsync(a => a.Id == 41);
        Assert.Equal(40m, advance.DeductedAmount);
        Assert.Equal(60m, advance.RemainingBalance);
        Assert.Equal("ACTIVE", advance.Status);
        Assert.Null(advance.ReservedByHandoffId);

        var schedule = await verify.LoanRepaymentSchedules.SingleAsync(s => s.Id == 101);
        Assert.Equal("PAID", schedule.Status);
        Assert.Equal(100m, schedule.PaidAmount);
        Assert.Null(schedule.ReservedByHandoffId);

        var loan = await verify.EmployeeLoans.SingleAsync(l => l.Id == 51);
        Assert.Equal(100m, loan.TotalPaidAmount);
        Assert.Equal(400m, loan.RemainingBalance);
        Assert.Equal("ACTIVE", loan.Status);

        var handoff = await verify.HrAccountingHandoffs.SingleAsync(h => h.Id == 11);
        Assert.Equal("POSTED", handoff.DeliveryStatus);

        var receipt = await verify.AccountingHandoffPostingReceipts.SingleAsync();
        Assert.Contains("ADVANCE", receipt.SettlementSnapshotJson);
        Assert.Contains("SCHEDULE", receipt.SettlementSnapshotJson);
    }

    [Fact]
    public async Task PostedJournalReceipt_WhenScheduleInstallmentGreaterThanOne_SettlesSpecificInstallmentCleanly()
    {
        using var harness = new Harness();
        await using (var db = harness.CreateDb())
        {
            var loan = new EmployeeLoan
            {
                Id = 77,
                EmployeeCode = "E-INSTALLMENT",
                PrincipalAmount = 1200m,
                CurrencyCode = "USD",
                RemainingBalance = 800m,
                TotalPaidAmount = 400m,
                Status = "ACTIVE"
            };
            db.EmployeeLoans.Add(loan);
            db.LoanRepaymentSchedules.Add(new LoanRepaymentSchedule
            {
                Id = 205,
                EmployeeLoanId = 77,
                InstallmentNo = 5,
                PayPeriod = "2026-11",
                ScheduledAmount = 100m,
                PaidAmount = 0m,
                Status = "PENDING",
                ReservedByHandoffId = 11,
                EmployeeLoan = loan
            });
            db.HrAccountingHandoffs.Add(new HrAccountingHandoff
            {
                Id = 11, CompanyId = 3, BranchId = 2, SourceType = "PAYROLL", SourceId = "9", SourceVersion = 1,
                Operation = "SUBMIT", PayPeriod = "2026-11", HandoffReference = "HR-PAY-3-9-1", PayloadHash = "payload",
                DeliveryStatus = "ACCEPTED", CreatedAtUtc = DateTime.UtcNow, CreatedBy = "hr",
                Lines = [new HrAccountingHandoffLine
                {
                    LineType = "REFERENCE:SCHEDULE", CurrencyCode = "USD", Amount = 100m, BranchId = 2,
                    SourceReference = "SCHEDULE:205:PAYROLL_LINE:505"
                }]
            });
            await db.SaveChangesAsync();
        }

        await using (var db = harness.CreateDb())
        {
            var service = harness.CreateService(db);
            await service.ConfirmJournalPostedAsync(11, 7, "posting-event-installment-5", "accounting");
        }

        await using var verify = harness.CreateDb();
        var schedule = await verify.LoanRepaymentSchedules.SingleAsync(s => s.Id == 205);
        Assert.Equal(5, schedule.InstallmentNo);
        Assert.Equal("PAID", schedule.Status);
        Assert.Equal(100m, schedule.PaidAmount);
        Assert.Null(schedule.ReservedByHandoffId);

        var loanVerify = await verify.EmployeeLoans.SingleAsync(l => l.Id == 77);
        Assert.Equal(500m, loanVerify.TotalPaidAmount);
        Assert.Equal(700m, loanVerify.RemainingBalance);
        Assert.Equal("ACTIVE", loanVerify.Status);
    }

    [Fact]
    public async Task PostedJournalReceipt_WhenNoLoanOrAdvanceReferences_ConfirmsPostedWithoutSettlementRecords()
    {
        using var harness = new Harness();
        await using (var db = harness.CreateDb())
        {
            db.HrAccountingHandoffs.Add(new HrAccountingHandoff
            {
                Id = 11, CompanyId = 3, BranchId = 2, SourceType = "PAYROLL", SourceId = "9", SourceVersion = 1,
                Operation = "SUBMIT", PayPeriod = "2026-11", HandoffReference = "HR-PAY-3-9-1", PayloadHash = "payload",
                DeliveryStatus = "ACCEPTED", CreatedAtUtc = DateTime.UtcNow, CreatedBy = "hr",
                Lines = [new HrAccountingHandoffLine
                {
                    LineType = "NET_PAY", CurrencyCode = "USD", Amount = 5000m, BranchId = 2,
                    SourceReference = "NET_PAY:PAYROLL_RUN:9"
                }]
            });
            await db.SaveChangesAsync();
        }

        await using (var db = harness.CreateDb())
        {
            var service = harness.CreateService(db);
            await service.ConfirmJournalPostedAsync(11, 7, "posting-event-no-settlement", "accounting");
        }

        await using var verify = harness.CreateDb();
        var handoff = await verify.HrAccountingHandoffs.SingleAsync(h => h.Id == 11);
        Assert.Equal("POSTED", handoff.DeliveryStatus);

        var receipt = await verify.AccountingHandoffPostingReceipts.SingleAsync();
        Assert.Equal(7, receipt.VoucherId);
        Assert.Equal("[]", receipt.SettlementSnapshotJson);
        Assert.Empty(await verify.EmployeeAdvances.ToListAsync());
        Assert.Empty(await verify.LoanRepaymentSchedules.ToListAsync());
    }

    [Fact]
    public async Task PostedJournalReceipt_WhenEventKeyHasSurroundingWhitespace_TrimsKeyInReceipt()
    {
        using var harness = new Harness();
        await harness.SeedAdvanceAsync(reserved: true);

        await using (var db = harness.CreateDb())
        {
            var service = harness.CreateService(db);
            await service.ConfirmJournalPostedAsync(11, 7, "   posting-event-trimmed-key   ", "accounting");
        }

        await using var verify = harness.CreateDb();
        var receipt = await verify.AccountingHandoffPostingReceipts.SingleAsync();
        Assert.Equal("posting-event-trimmed-key", receipt.EventKey);
    }

    [Fact]
    public async Task ConfirmJournalPostedAsync_WhenHandoffDeliveryStatusDraft_ThrowsInvalidOperationException()
    {
        using var harness = new Harness();
        await harness.SeedAdvanceAsync(reserved: true, deliveryStatus: "DRAFT");

        await using (var db = harness.CreateDb())
        {
            var service = harness.CreateService(db);
            var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
                service.ConfirmJournalPostedAsync(11, 7, "posting-event-draft", "accounting"));
            Assert.Contains("Only an accepted handoff can receive its posting receipt", ex.Message);
        }
    }

    [Fact]
    public async Task ConfirmJournalPostedAsync_WhenHandoffDeliveryStatusSubmitted_ThrowsInvalidOperationException()
    {
        using var harness = new Harness();
        await harness.SeedAdvanceAsync(reserved: true, deliveryStatus: "SUBMITTED");

        await using (var db = harness.CreateDb())
        {
            var service = harness.CreateService(db);
            var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
                service.ConfirmJournalPostedAsync(11, 7, "posting-event-submitted", "accounting"));
            Assert.Contains("Only an accepted handoff can receive its posting receipt", ex.Message);
        }
    }

    [Fact]
    public async Task PostedJournalReceipt_WhenBothLoanAndAdvanceFullySettle_MarksLoanCompletedAndAdvanceRecovered()
    {
        using var harness = new Harness();
        await using (var db = harness.CreateDb())
        {
            db.HrAccountingHandoffs.Add(new HrAccountingHandoff
            {
                Id = 11, CompanyId = 3, BranchId = 2, SourceType = "PAYROLL", SourceId = "9", SourceVersion = 1,
                Operation = "SUBMIT", PayPeriod = "2026-11", HandoffReference = "HR-PAY-3-9-1", PayloadHash = "payload",
                DeliveryStatus = "ACCEPTED", CreatedAtUtc = DateTime.UtcNow, CreatedBy = "hr",
                Lines =
                [
                    new HrAccountingHandoffLine
                    {
                        LineType = "REFERENCE:ADVANCE", CurrencyCode = "USD", Amount = 75m, BranchId = 2,
                        SourceReference = "ADVANCE:41:PAYROLL_LINE:501"
                    },
                    new HrAccountingHandoffLine
                    {
                        LineType = "REFERENCE:SCHEDULE", CurrencyCode = "USD", Amount = 150m, BranchId = 2,
                        SourceReference = "SCHEDULE:101:PAYROLL_LINE:502"
                    }
                ]
            });
            db.EmployeeAdvances.Add(new EmployeeAdvance
            {
                Id = 41, EmployeeCode = "E-501", AdvanceAmount = 200m, CurrencyCode = "USD", TargetPayPeriod = "2026-11",
                DeductedAmount = 125m, RemainingBalance = 75m, Status = "ACTIVE", ReservedByHandoffId = 11
            });
            var loan = new EmployeeLoan
            {
                Id = 51, EmployeeCode = "E-502", PrincipalAmount = 600m, CurrencyCode = "USD",
                RemainingBalance = 150m, TotalPaidAmount = 450m, Status = "ACTIVE"
            };
            db.EmployeeLoans.Add(loan);
            db.LoanRepaymentSchedules.Add(new LoanRepaymentSchedule
            {
                Id = 101, EmployeeLoanId = 51, InstallmentNo = 4, PayPeriod = "2026-11",
                ScheduledAmount = 150m, PaidAmount = 0m, Status = "PENDING",
                ReservedByHandoffId = 11, EmployeeLoan = loan
            });
            await db.SaveChangesAsync();
        }

        await using (var db = harness.CreateDb())
        {
            var service = harness.CreateService(db);
            await service.ConfirmJournalPostedAsync(11, 7, "posting-event-full-both", "super_admin");
        }

        await using var verify = harness.CreateDb();
        var advance = await verify.EmployeeAdvances.SingleAsync(a => a.Id == 41);
        Assert.Equal(200m, advance.DeductedAmount);
        Assert.Equal(0m, advance.RemainingBalance);
        Assert.Equal("RECOVERED", advance.Status);

        var schedule = await verify.LoanRepaymentSchedules.SingleAsync(s => s.Id == 101);
        Assert.Equal("PAID", schedule.Status);
        Assert.Equal(150m, schedule.PaidAmount);

        var loanVerify = await verify.EmployeeLoans.SingleAsync(l => l.Id == 51);
        Assert.Equal(600m, loanVerify.TotalPaidAmount);
        Assert.Equal(0m, loanVerify.RemainingBalance);
        Assert.Equal("COMPLETED", loanVerify.Status);
    }

    private sealed class Harness : IDisposable
    {
        private readonly DbContextOptions<OracleDbContext> options = new DbContextOptionsBuilder<OracleDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning)).Options;
        private readonly Mock<IGlVoucherRepository> vouchers = new();

        public Harness()
        {
            SetVoucherStatus(3, hasPostDate: true);
        }

        public void SetVoucherStatus(int status, bool hasPostDate = true, int branchId = 2)
        {
            vouchers.Setup(v => v.GetByIdAsync(7, It.IsAny<CancellationToken>())).ReturnsAsync(new GlVoucherHeader
            {
                Id = 7,
                BranchId = branchId,
                SourceSystemCode = "HR_HANDOFF",
                SourceRefId = 11,
                Status = status,
                PostDate = hasPostDate ? new DateTime(2026, 11, 7, 10, 0, 0, DateTimeKind.Utc) : null,
                TotalLocalDebit = 100m,
                TotalLocalCredit = 100m
            });
        }

        public void SetVoucherDetails(string sourceSystemCode, long sourceRefId)
        {
            vouchers.Setup(v => v.GetByIdAsync(7, It.IsAny<CancellationToken>())).ReturnsAsync(new GlVoucherHeader
            {
                Id = 7,
                BranchId = 2,
                SourceSystemCode = sourceSystemCode,
                SourceRefId = sourceRefId,
                Status = 3,
                PostDate = new DateTime(2026, 11, 7, 10, 0, 0, DateTimeKind.Utc),
                TotalLocalDebit = 100m,
                TotalLocalCredit = 100m
            });
        }

        public OracleDbContext CreateDb() => new(options);
        public HrAccountingHandoffService CreateService(OracleDbContext db)
        {
            // These receipt scenarios start after an approved payroll run and Accounting JV were created.
            var handoff = db.HrAccountingHandoffs.Include(h => h.Lines).SingleOrDefault(h => h.Id == 11);
            if (handoff?.SourceType == "PAYROLL" && long.TryParse(handoff.SourceId, out var runId)
                && !db.PayrollRuns.Any(run => run.Id == runId))
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
                    var payrollLine = new PayrollRunLine
                    {
                        Id = group.Key, PayrollRunId = runId, EmployeeCode = employeeCode ?? "EMP-TEST",
                        BranchId = handoff.BranchId, CurrencyCode = group.First().Line.CurrencyCode,
                        Components = [new PayrollRunLineComponent
                        {
                            ComponentCode = "LOAN_DEDUCTION", ComponentNameEn = "Loan deduction",
                            ComponentType = "DEDUCTION", Amount = group.Sum(item => item.Line.Amount)
                        }]
                    };
                    lines.Add(payrollLine);
                }
                db.PayrollRuns.Add(new PayrollRun
                {
                    Id = runId, PayPeriod = handoff.PayPeriod!, BranchId = handoff.BranchId, Status = "APPROVED",
                    CorrectionVersion = handoff.SourceVersion, ApprovalDate = handoff.SourceApprovedAtUtc,
                    Lines = lines
                });
            }
            if (handoff != null && !db.AccountingHandoffJournalLinks.Any(link => link.HandoffId == handoff.Id))
                db.AccountingHandoffJournalLinks.Add(new AccountingHandoffJournalLink
                {
                    HandoffId = handoff.Id, VoucherId = 7, CreatedAtUtc = DateTime.UtcNow, CreatedBy = "accounting"
                });
            db.SaveChanges();
            return new HrAccountingHandoffService(db, vouchers.Object,
                Mock.Of<IHrPayrollPostingService>(), Mock.Of<IReceiptPaymentVoucherService>(), Mock.Of<IGlVoucherService>());
        }

        public async Task SeedAdvanceAsync(
            bool reserved,
            string deliveryStatus = "ACCEPTED",
            decimal advanceAmount = 100m,
            decimal remainingBalance = 100m,
            decimal deductionAmount = 40m,
            string advanceStatus = "ACTIVE",
            string advanceCurrency = "USD",
            string handoffCurrency = "USD")
        {
            await using var db = CreateDb();
            db.HrAccountingHandoffs.Add(new HrAccountingHandoff
            {
                Id = 11, CompanyId = 3, BranchId = 2, SourceType = "PAYROLL", SourceId = "9", SourceVersion = 1,
                Operation = "SUBMIT", PayPeriod = "2026-11", HandoffReference = "HR-PAY-3-9-1", PayloadHash = "payload",
                DeliveryStatus = deliveryStatus, CreatedAtUtc = DateTime.UtcNow, CreatedBy = "hr",
                Lines = [new HrAccountingHandoffLine
                {
                    LineType = "REFERENCE:ADVANCE", CurrencyCode = handoffCurrency, Amount = deductionAmount, BranchId = 2,
                    SourceReference = "ADVANCE:41:PAYROLL_LINE:501"
                }]
            });
            db.EmployeeAdvances.Add(new EmployeeAdvance
            {
                Id = 41, EmployeeCode = "E-501", AdvanceAmount = advanceAmount, CurrencyCode = advanceCurrency, TargetPayPeriod = "2026-11",
                DeductedAmount = advanceAmount - remainingBalance, RemainingBalance = remainingBalance, Status = advanceStatus, ReservedByHandoffId = reserved ? 11 : null
            });
            await db.SaveChangesAsync();
        }

        public async Task SeedLoanScheduleAsync(
            bool reserved,
            decimal scheduledAmount = 100m,
            decimal deductionAmount = 100m,
            decimal loanRemainingBalance = 500m,
            decimal loanTotalPaid = 0m,
            string loanStatus = "ACTIVE",
            string loanCurrency = "USD",
            string handoffCurrency = "USD",
            string? sourceReference = null,
            string deliveryStatus = "ACCEPTED")
        {
            await using var db = CreateDb();
            db.HrAccountingHandoffs.Add(new HrAccountingHandoff
            {
                Id = 11, CompanyId = 3, BranchId = 2, SourceType = "PAYROLL", SourceId = "9", SourceVersion = 1,
                Operation = "SUBMIT", PayPeriod = "2026-11", HandoffReference = "HR-PAY-3-9-1", PayloadHash = "payload",
                DeliveryStatus = deliveryStatus, CreatedAtUtc = DateTime.UtcNow, CreatedBy = "hr",
                Lines = [new HrAccountingHandoffLine
                {
                    LineType = "REFERENCE:SCHEDULE", CurrencyCode = handoffCurrency, Amount = deductionAmount, BranchId = 2,
                    SourceReference = sourceReference ?? "SCHEDULE:101:PAYROLL_LINE:501"
                }]
            });
            var loan = new EmployeeLoan
            {
                Id = 51,
                EmployeeCode = "E-501",
                PrincipalAmount = 500m,
                CurrencyCode = loanCurrency,
                RemainingBalance = loanRemainingBalance,
                TotalPaidAmount = loanTotalPaid,
                Status = loanStatus
            };
            db.EmployeeLoans.Add(loan);
            db.LoanRepaymentSchedules.Add(new LoanRepaymentSchedule
            {
                Id = 101,
                EmployeeLoanId = 51,
                InstallmentNo = 1,
                PayPeriod = "2026-11",
                ScheduledAmount = scheduledAmount,
                PaidAmount = 0m,
                Status = "PENDING",
                ReservedByHandoffId = reserved ? 11 : null,
                EmployeeLoan = loan
            });
            await db.SaveChangesAsync();
        }

        public async Task SeedBothAdvanceAndLoanAsync(bool reserved)
        {
            await using var db = CreateDb();
            db.HrAccountingHandoffs.Add(new HrAccountingHandoff
            {
                Id = 11, CompanyId = 3, BranchId = 2, SourceType = "PAYROLL", SourceId = "9", SourceVersion = 1,
                Operation = "SUBMIT", PayPeriod = "2026-11", HandoffReference = "HR-PAY-3-9-1", PayloadHash = "payload",
                DeliveryStatus = "ACCEPTED", CreatedAtUtc = DateTime.UtcNow, CreatedBy = "hr",
                Lines =
                [
                    new HrAccountingHandoffLine
                    {
                        LineType = "REFERENCE:ADVANCE", CurrencyCode = "USD", Amount = 40m, BranchId = 2,
                        SourceReference = "ADVANCE:41:PAYROLL_LINE:501"
                    },
                    new HrAccountingHandoffLine
                    {
                        LineType = "REFERENCE:SCHEDULE", CurrencyCode = "USD", Amount = 100m, BranchId = 2,
                        SourceReference = "SCHEDULE:101:PAYROLL_LINE:502"
                    }
                ]
            });
            db.EmployeeAdvances.Add(new EmployeeAdvance
            {
                Id = 41, EmployeeCode = "E-501", AdvanceAmount = 100m, CurrencyCode = "USD", TargetPayPeriod = "2026-11",
                DeductedAmount = 0m, RemainingBalance = 100m, Status = "ACTIVE", ReservedByHandoffId = reserved ? 11 : null
            });
            var loan = new EmployeeLoan
            {
                Id = 51, EmployeeCode = "E-502", PrincipalAmount = 500m, CurrencyCode = "USD",
                RemainingBalance = 500m, TotalPaidAmount = 0m, Status = "ACTIVE"
            };
            db.EmployeeLoans.Add(loan);
            db.LoanRepaymentSchedules.Add(new LoanRepaymentSchedule
            {
                Id = 101, EmployeeLoanId = 51, InstallmentNo = 1, PayPeriod = "2026-11",
                ScheduledAmount = 100m, PaidAmount = 0m, Status = "PENDING",
                ReservedByHandoffId = reserved ? 11 : null, EmployeeLoan = loan
            });
            await db.SaveChangesAsync();
        }

        public async Task SeedMultipleAdvancesAsync(bool reserved)
        {
            await using var db = CreateDb();
            db.HrAccountingHandoffs.Add(new HrAccountingHandoff
            {
                Id = 11, CompanyId = 3, BranchId = 2, SourceType = "PAYROLL", SourceId = "9", SourceVersion = 1,
                Operation = "SUBMIT", PayPeriod = "2026-11", HandoffReference = "HR-PAY-3-9-1", PayloadHash = "payload",
                DeliveryStatus = "ACCEPTED", CreatedAtUtc = DateTime.UtcNow, CreatedBy = "hr",
                Lines =
                [
                    new HrAccountingHandoffLine
                    {
                        LineType = "REFERENCE:ADVANCE", CurrencyCode = "USD", Amount = 40m, BranchId = 2,
                        SourceReference = "ADVANCE:41:PAYROLL_LINE:501"
                    },
                    new HrAccountingHandoffLine
                    {
                        LineType = "REFERENCE:ADVANCE", CurrencyCode = "USD", Amount = 50m, BranchId = 2,
                        SourceReference = "ADVANCE:42:PAYROLL_LINE:502"
                    }
                ]
            });
            db.EmployeeAdvances.AddRange(
                new EmployeeAdvance
                {
                    Id = 41, EmployeeCode = "E-501", AdvanceAmount = 100m, CurrencyCode = "USD", TargetPayPeriod = "2026-11",
                    DeductedAmount = 0m, RemainingBalance = 100m, Status = "ACTIVE", ReservedByHandoffId = reserved ? 11 : null
                },
                new EmployeeAdvance
                {
                    Id = 42, EmployeeCode = "E-502", AdvanceAmount = 50m, CurrencyCode = "USD", TargetPayPeriod = "2026-11",
                    DeductedAmount = 0m, RemainingBalance = 50m, Status = "ACTIVE", ReservedByHandoffId = reserved ? 11 : null
                }
            );
            await db.SaveChangesAsync();
        }

        public async Task SeedMultipleLoanSchedulesAsync(bool reserved)
        {
            await using var db = CreateDb();
            db.HrAccountingHandoffs.Add(new HrAccountingHandoff
            {
                Id = 11, CompanyId = 3, BranchId = 2, SourceType = "PAYROLL", SourceId = "9", SourceVersion = 1,
                Operation = "SUBMIT", PayPeriod = "2026-11", HandoffReference = "HR-PAY-3-9-1", PayloadHash = "payload",
                DeliveryStatus = "ACCEPTED", CreatedAtUtc = DateTime.UtcNow, CreatedBy = "hr",
                Lines =
                [
                    new HrAccountingHandoffLine
                    {
                        LineType = "REFERENCE:SCHEDULE", CurrencyCode = "USD", Amount = 100m, BranchId = 2,
                        SourceReference = "SCHEDULE:101:PAYROLL_LINE:501"
                    },
                    new HrAccountingHandoffLine
                    {
                        LineType = "REFERENCE:SCHEDULE", CurrencyCode = "USD", Amount = 60m, BranchId = 2,
                        SourceReference = "SCHEDULE:102:PAYROLL_LINE:502"
                    }
                ]
            });
            var loan1 = new EmployeeLoan
            {
                Id = 51, EmployeeCode = "E-501", PrincipalAmount = 500m, CurrencyCode = "USD",
                RemainingBalance = 500m, TotalPaidAmount = 0m, Status = "ACTIVE"
            };
            var loan2 = new EmployeeLoan
            {
                Id = 52, EmployeeCode = "E-502", PrincipalAmount = 300m, CurrencyCode = "USD",
                RemainingBalance = 300m, TotalPaidAmount = 0m, Status = "ACTIVE"
            };
            db.EmployeeLoans.AddRange(loan1, loan2);
            db.LoanRepaymentSchedules.AddRange(
                new LoanRepaymentSchedule
                {
                    Id = 101, EmployeeLoanId = 51, InstallmentNo = 1, PayPeriod = "2026-11",
                    ScheduledAmount = 100m, PaidAmount = 0m, Status = "PENDING",
                    ReservedByHandoffId = reserved ? 11 : null, EmployeeLoan = loan1
                },
                new LoanRepaymentSchedule
                {
                    Id = 102, EmployeeLoanId = 52, InstallmentNo = 1, PayPeriod = "2026-11",
                    ScheduledAmount = 100m, PaidAmount = 0m, Status = "PENDING",
                    ReservedByHandoffId = reserved ? 11 : null, EmployeeLoan = loan2
                }
            );
            await db.SaveChangesAsync();
        }

        public void Dispose() { }
    }
}
