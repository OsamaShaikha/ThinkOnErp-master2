using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Moq;
using ThinkOnErp.Domain.Entities;
using ThinkOnErp.Domain.Entities.Accounting;
using ThinkOnErp.Application.DTOs.Accounting.Vouchers;
using ThinkOnErp.Application.Services.Accounting;
using ThinkOnErp.Application.Services.Hr;
using ThinkOnErp.Domain.Entities.Hr;
using ThinkOnErp.Domain.Interfaces.Accounting;
using ThinkOnErp.Infrastructure.Data;
using ThinkOnErp.Infrastructure.Services.Hr;
using Xunit;

namespace ThinkOnErp.HR.Api.Tests;

public sealed class AccountingHandoffPaymentReceiptTests
{
    [Fact]
    public async Task ReplayingConfirmedPaymentWithSameEvidenceIsIdempotent()
    {
        using var harness = new PaymentReceiptHarness();
        await harness.SeedAsync();

        await using (var db = harness.CreateDb())
        {
            var service = harness.CreateService(db);
            var replay = await service.ConfirmPaymentAsync(17, "BANK-REF-77", "TRANSFER-IMAGE-4", "accounting-user");
            Assert.Equal("CONFIRMED", replay.Status);
            Assert.Equal("TRANSFER-IMAGE-4", replay.EvidenceReference);
        }

        await using var verify = harness.CreateDb();
        Assert.Single(await verify.AccountingHandoffPayments.ToListAsync());
        Assert.Equal("POSTED", (await verify.HrAccountingHandoffs.SingleAsync()).DeliveryStatus);
    }

    [Fact]
    public async Task PreparedPaymentConfirmsOnlyAgainstMatchingPostedAccountingVoucher()
    {
        using var harness = new PaymentReceiptHarness();
        harness.EnableLinkedVoucher();
        await harness.SeedAsync(includePostedVoucherLink: true, paymentStatus: "PREPARED");

        await using (var db = harness.CreateDb())
        {
            var confirmed = await harness.CreateService(db).ConfirmPaymentAsync(
                17, "BANK-REF-77", "TRANSFER-IMAGE-4", "accounting-user");
            Assert.Equal("CONFIRMED", confirmed.Status);
            Assert.Equal(7, confirmed.PaymentVoucherId);
        }

        await using var verify = harness.CreateDb();
        var persisted = await verify.AccountingHandoffPayments.SingleAsync();
        Assert.Equal("BANK-REF-77", persisted.ExternalReference);
        Assert.Equal("TRANSFER-IMAGE-4", persisted.EvidenceReference);
        Assert.Equal("accounting-user", persisted.ConfirmedBy);
    }

    [Fact]
    public async Task ConfirmedPaymentRejectsDifferentEvidenceOnReplay()
    {
        using var harness = new PaymentReceiptHarness();
        await harness.SeedAsync();

        await using var db = harness.CreateDb();
        var service = harness.CreateService(db);
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.ConfirmPaymentAsync(17, "BANK-REF-77", "DIFFERENT-EVIDENCE", "accounting-user"));

        await using var verify = harness.CreateDb();
        var payment = await verify.AccountingHandoffPayments.SingleAsync();
        Assert.Equal("CONFIRMED", payment.Status);
        Assert.Equal("BANK-REF-77", payment.ExternalReference);
        Assert.Equal("TRANSFER-IMAGE-4", payment.EvidenceReference);
    }

    [Fact]
    public async Task ReplayingPaymentWithItsPostedVoucherStillRequiresAndAcceptsExactAccountingEvidence()
    {
        using var harness = new PaymentReceiptHarness();
        harness.EnableLinkedVoucher();
        await harness.SeedAsync(includePostedVoucherLink: true);

        await using var db = harness.CreateDb();
        var replay = await harness.CreateService(db).ConfirmPaymentAsync(17, "BANK-REF-77", "TRANSFER-IMAGE-4", "accounting-user");

        Assert.Equal("CONFIRMED", replay.Status);
        Assert.Equal("TRANSFER-IMAGE-4", replay.EvidenceReference);
    }

    [Fact]
    public async Task ReplayingPaymentWithPostedVoucherRejectsChangedEvidence()
    {
        using var harness = new PaymentReceiptHarness();
        harness.EnableLinkedVoucher();
        await harness.SeedAsync(includePostedVoucherLink: true);

        await using var db = harness.CreateDb();
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            harness.CreateService(db).ConfirmPaymentAsync(17, "BANK-REF-77", "DIFFERENT-EVIDENCE", "accounting-user"));
    }

    [Theory]
    [InlineData("", "TRANSFER-IMAGE-1")]
    [InlineData("   ", "TRANSFER-IMAGE-1")]
    [InlineData(null, "TRANSFER-IMAGE-1")]
    [InlineData("BANK-REF-1", "")]
    [InlineData("BANK-REF-1", "   ")]
    [InlineData("BANK-REF-1", null)]
    public async Task ConfirmPayment_InvalidArguments_ThrowsArgumentException(string? externalRef, string? evidenceRef)
    {
        using var harness = new PaymentReceiptHarness();
        await harness.SeedAsync();

        await using var db = harness.CreateDb();
        var service = harness.CreateService(db);

        var ex = await Assert.ThrowsAsync<ArgumentException>(() =>
            service.ConfirmPaymentAsync(17, externalRef!, evidenceRef!, "accounting-user"));
        Assert.Contains("payment reference and evidence reference are required", ex.Message);
    }

    [Fact]
    public async Task ConfirmPayment_ExceedingMaxLength_ThrowsArgumentException()
    {
        using var harness = new PaymentReceiptHarness();
        await harness.SeedAsync();

        await using var db = harness.CreateDb();
        var service = harness.CreateService(db);

        var tooLongRef = new string('x', 201);
        var tooLongEvidence = new string('y', 501);

        await Assert.ThrowsAsync<ArgumentException>(() =>
            service.ConfirmPaymentAsync(17, tooLongRef, "VALID-EVIDENCE", "accounting-user"));

        await Assert.ThrowsAsync<ArgumentException>(() =>
            service.ConfirmPaymentAsync(17, "VALID-REF", tooLongEvidence, "accounting-user"));
    }

    [Fact]
    public async Task ConfirmPayment_PaymentNotFound_ThrowsKeyNotFoundException()
    {
        using var harness = new PaymentReceiptHarness();
        await harness.SeedAsync();

        await using var db = harness.CreateDb();
        var service = harness.CreateService(db);

        await Assert.ThrowsAsync<KeyNotFoundException>(() =>
            service.ConfirmPaymentAsync(9999, "BANK-REF-1", "TRANSFER-IMAGE-1", "accounting-user"));
    }

    [Fact]
    public async Task ConfirmPayment_HandoffNotPosted_ThrowsInvalidOperationException()
    {
        using var harness = new PaymentReceiptHarness();
        await harness.SeedAsync();

        await using (var db = harness.CreateDb())
        {
            var handoff = await db.HrAccountingHandoffs.SingleAsync(h => h.Id == 11);
            handoff.DeliveryStatus = "ACCEPTED";
            await db.SaveChangesAsync();
        }

        await using var dbVerify = harness.CreateDb();
        var service = harness.CreateService(dbVerify);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.ConfirmPaymentAsync(17, "BANK-REF-1", "TRANSFER-IMAGE-1", "accounting-user"));
        Assert.Contains("Payment handoff must remain posted", ex.Message);
    }

    [Fact]
    public async Task ConfirmPayment_PaymentStatusNotPrepared_ThrowsInvalidOperationException()
    {
        using var harness = new PaymentReceiptHarness();
        harness.EnableLinkedVoucher();
        await harness.SeedAsync(includePostedVoucherLink: true, paymentStatus: "FAILED");

        await using var db = harness.CreateDb();
        var service = harness.CreateService(db);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.ConfirmPaymentAsync(17, "BANK-REF-1", "TRANSFER-IMAGE-1", "accounting-user"));
        Assert.Contains("Only a prepared payment can be confirmed", ex.Message);
    }

    [Fact]
    public async Task ConfirmPayment_MultipleLinkedVouchers_ThrowsInvalidOperationException()
    {
        using var harness = new PaymentReceiptHarness();
        harness.EnableMultipleLinkedVouchers();
        await harness.SeedAsync(includePostedVoucherLink: true, paymentStatus: "PREPARED");

        await using var db = harness.CreateDb();
        var service = harness.CreateService(db);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.ConfirmPaymentAsync(17, "BANK-REF-1", "TRANSFER-IMAGE-1", "accounting-user"));
        Assert.Contains("unique HR Payment Voucher must be linked", ex.Message);
    }

    [Fact]
    public async Task ConfirmPayment_VoucherDraftNotPosted_ThrowsInvalidOperationException()
    {
        using var harness = new PaymentReceiptHarness();
        harness.EnableDraftLinkedVoucher();
        await harness.SeedAsync(includePostedVoucherLink: true, paymentStatus: "PREPARED");

        await using var db = harness.CreateDb();
        var service = harness.CreateService(db);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.ConfirmPaymentAsync(17, "BANK-REF-1", "TRANSFER-IMAGE-1", "accounting-user"));
        Assert.Contains("posted ERP Payment Voucher matching the handoff branch", ex.Message);
    }

    [Theory]
    [InlineData("")]
    [InlineData("  ")]
    [InlineData("a")]
    [InlineData("ab")]
    [InlineData(null)]
    public async Task FailPayment_ReasonInvalid_ThrowsArgumentException(string? reason)
    {
        using var harness = new PaymentReceiptHarness();
        await harness.SeedAsync();

        await using var db = harness.CreateDb();
        var service = harness.CreateService(db);

        var ex = await Assert.ThrowsAsync<ArgumentException>(() =>
            service.FailPaymentAsync(17, reason!, "accounting-user"));
        Assert.Contains("failure or cancellation reason is required", ex.Message);
    }

    [Fact]
    public async Task FailPayment_ReasonExceeds1000Chars_ThrowsArgumentException()
    {
        using var harness = new PaymentReceiptHarness();
        await harness.SeedAsync();

        await using var db = harness.CreateDb();
        var service = harness.CreateService(db);

        var longReason = new string('r', 1001);
        var ex = await Assert.ThrowsAsync<ArgumentException>(() =>
            service.FailPaymentAsync(17, longReason, "accounting-user"));
        Assert.Contains("failure or cancellation reason is required", ex.Message);
    }

    [Fact]
    public async Task FailPayment_PaymentNotFound_ThrowsKeyNotFoundException()
    {
        using var harness = new PaymentReceiptHarness();
        await harness.SeedAsync();

        await using var db = harness.CreateDb();
        var service = harness.CreateService(db);

        await Assert.ThrowsAsync<KeyNotFoundException>(() =>
            service.FailPaymentAsync(9999, "Bank rejection: account frozen", "accounting-user"));
    }

    [Fact]
    public async Task FailPayment_StatusNotPrepared_ThrowsInvalidOperationException()
    {
        using var harness = new PaymentReceiptHarness();
        await harness.SeedAsync(paymentStatus: "CONFIRMED");

        await using var db = harness.CreateDb();
        var service = harness.CreateService(db);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.FailPaymentAsync(17, "Bank rejection: account frozen", "accounting-user"));
        Assert.Contains("Only a prepared payment can be failed", ex.Message);
    }

    [Fact]
    public async Task FailPayment_PostedVoucherLinked_ThrowsInvalidOperationException()
    {
        using var harness = new PaymentReceiptHarness();
        harness.EnableLinkedVoucher();
        await harness.SeedAsync(includePostedVoucherLink: true, paymentStatus: "PREPARED");

        await using var db = harness.CreateDb();
        var service = harness.CreateService(db);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.FailPaymentAsync(17, "Bank rejection: account frozen", "accounting-user"));
        Assert.Contains("posted Payment Voucher cannot be marked failed", ex.Message);
    }

    [Fact]
    public async Task FailPayment_DraftVoucherLinked_VoidsDraftAndSetsFailedStatus()
    {
        using var harness = new PaymentReceiptHarness();
        harness.EnableDraftLinkedVoucher();
        await harness.SeedAsync(includePostedVoucherLink: true, paymentStatus: "PREPARED");

        var beforeCall = DateTime.UtcNow;
        await using (var db = harness.CreateDb())
        {
            var service = harness.CreateService(db);
            var result = await service.FailPaymentAsync(17, "Payment processing rejected by gateway", "accounting-user");
            Assert.Equal("FAILED", result.Status);
            Assert.Equal("Payment processing rejected by gateway", result.FailureReason);
            Assert.Equal("accounting-user", result.FailedBy);
            Assert.True(result.FailedAtUtc >= beforeCall);
        }

        harness.PaymentVouchersMock.Verify(p => p.DeletePaymentVoucherAsync(7, "accounting-user", It.IsAny<CancellationToken>()), Times.Once);

        await using var verify = harness.CreateDb();
        var payment = await verify.AccountingHandoffPayments.SingleAsync();
        Assert.Equal("FAILED", payment.Status);
        Assert.Equal("Payment processing rejected by gateway", payment.FailureReason);
    }

    [Fact]
    public async Task FailPayment_NoLinkedVoucher_SucceedsAndIsIdempotent()
    {
        using var harness = new PaymentReceiptHarness();
        await harness.SeedAsync(includePostedVoucherLink: true, paymentStatus: "PREPARED");

        await using (var db = harness.CreateDb())
        {
            var service = harness.CreateService(db);
            var result = await service.FailPaymentAsync(17, "Payment instruction cancelled by operator", "accounting-user");
            Assert.Equal("FAILED", result.Status);
            Assert.Equal("Payment instruction cancelled by operator", result.FailureReason);

            // Idempotent replay with same reason
            var replay = await service.FailPaymentAsync(17, "Payment instruction cancelled by operator", "accounting-user");
            Assert.Equal("FAILED", replay.Status);
        }

        await using (var db = harness.CreateDb())
        {
            var service = harness.CreateService(db);
            // Replay with different reason throws
            var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
                service.FailPaymentAsync(17, "A completely different reason", "accounting-user"));
            Assert.Contains("failed payment instruction is immutable", ex.Message);
        }
    }

    [Theory]
    [InlineData("a", "EXT-1", "EVID-1")]
    [InlineData("Reason", "", "EVID-1")]
    [InlineData("Reason", "EXT-1", "")]
    public async Task ReversePayment_InvalidArguments_ThrowsArgumentException(string reason, string externalRef, string evidenceRef)
    {
        using var harness = new PaymentReceiptHarness();
        await harness.SeedAsync();

        await using var db = harness.CreateDb();
        var service = harness.CreateService(db);

        var ex = await Assert.ThrowsAsync<ArgumentException>(() =>
            service.ReversePaymentAsync(17, reason, externalRef, evidenceRef, "accounting-user"));
        Assert.Contains("reversal reason, external reversal reference, and evidence reference are required", ex.Message);
    }

    [Fact]
    public async Task ReversePayment_PaymentNotFound_ThrowsKeyNotFoundException()
    {
        using var harness = new PaymentReceiptHarness();
        await harness.SeedAsync();

        await using var db = harness.CreateDb();
        var service = harness.CreateService(db);

        await Assert.ThrowsAsync<KeyNotFoundException>(() =>
            service.ReversePaymentAsync(9999, "Returned check", "REV-EXT-1", "REV-EVID-1", "accounting-user"));
    }

    [Fact]
    public async Task ReversePayment_StatusNotConfirmed_ThrowsInvalidOperationException()
    {
        using var harness = new PaymentReceiptHarness();
        await harness.SeedAsync(paymentStatus: "PREPARED");

        await using var db = harness.CreateDb();
        var service = harness.CreateService(db);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.ReversePaymentAsync(17, "Returned check", "REV-EXT-1", "REV-EVID-1", "accounting-user"));
        Assert.Contains("Only a confirmed payment with external payment evidence can be reversed", ex.Message);
    }

    [Fact]
    public async Task ReversePayment_SuccessfulReversal_ExecutesGlReversalAndPersistsState()
    {
        using var harness = new PaymentReceiptHarness();
        harness.EnableLinkedVoucherWithReversal();
        await harness.SeedAsync(includePostedVoucherLink: true, paymentStatus: "CONFIRMED");

        var beforeCall = DateTime.UtcNow;
        await using (var db = harness.CreateDb())
        {
            var service = harness.CreateService(db);
            var reversed = await service.ReversePaymentAsync(
                17, "Payroll disbursement bounced by recipient bank", "REV-EXT-99", "REV-EVID-88", "accounting-user");

            Assert.Equal("REVERSED", reversed.Status);
            Assert.Equal("Payroll disbursement bounced by recipient bank", reversed.ReversalReason);
            Assert.Equal("REV-EXT-99", reversed.ReversalExternalReference);
            Assert.Equal("REV-EVID-88", reversed.ReversalEvidenceReference);
            Assert.Equal(99, reversed.ReversalVoucherId);
            Assert.Equal(7, reversed.PaymentVoucherId);
            Assert.Equal("accounting-user", reversed.ReversedBy);
            Assert.True(reversed.ReversedAtUtc >= beforeCall);
        }

        harness.GlVouchersMock.Verify(g => g.ReverseVoucherAsync(7, "accounting-user", "Payroll disbursement bounced by recipient bank", It.IsAny<CancellationToken>()), Times.Once);

        // Test Idempotent replay with exact same details
        await using (var db = harness.CreateDb())
        {
            var service = harness.CreateService(db);
            var replay = await service.ReversePaymentAsync(
                17, "Payroll disbursement bounced by recipient bank", "REV-EXT-99", "REV-EVID-88", "accounting-user");
            Assert.Equal("REVERSED", replay.Status);
        }

        // Test replay with different details throws
        await using (var db = harness.CreateDb())
        {
            var service = harness.CreateService(db);
            var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
                service.ReversePaymentAsync(17, "Different reason", "REV-EXT-99", "REV-EVID-88", "accounting-user"));
            Assert.Contains("reversed payment is immutable", ex.Message);
        }
    }

    [Fact]
    public async Task ConfirmPayment_WhenHandoffBranchMissing_ThrowsInvalidOperationException()
    {
        using var harness = new PaymentReceiptHarness();
        await harness.SeedAsync();

        await using (var db = harness.CreateDb())
        {
            var handoff = await db.HrAccountingHandoffs.SingleAsync(h => h.Id == 11);
            handoff.BranchId = null;
            await db.SaveChangesAsync();
        }

        await using var dbVerify = harness.CreateDb();
        var service = harness.CreateService(dbVerify);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.ConfirmPaymentAsync(17, "BANK-REF-1", "TRANSFER-IMAGE-1", "accounting-user"));
        Assert.Contains("Accounting handoff has no branch", ex.Message);
    }

    [Fact]
    public async Task ConfirmPayment_WhenPaymentMethodCodeNotFoundInDb_ThrowsInvalidOperationException()
    {
        using var harness = new PaymentReceiptHarness();
        harness.EnableLinkedVoucher();
        await harness.SeedAsync(includePostedVoucherLink: true, paymentStatus: "PREPARED");

        await using (var db = harness.CreateDb())
        {
            var payment = await db.AccountingHandoffPayments.SingleAsync(p => p.Id == 17);
            payment.PaymentMethodCode = "NONEXISTENT";
            await db.SaveChangesAsync();
        }

        await using var dbVerify = harness.CreateDb();
        var service = harness.CreateService(dbVerify);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.ConfirmPaymentAsync(17, "BANK-REF-77", "TRANSFER-IMAGE-4", "accounting-user"));
        Assert.Contains("missing or ambiguous", ex.Message);
    }

    [Fact]
    public async Task ConfirmPayment_WhenPaymentMethodCodeNull_AndCreditedAccountAmbiguous_ThrowsInvalidOperationException()
    {
        using var harness = new PaymentReceiptHarness();
        harness.EnableLinkedVoucher();
        await harness.SeedAsync(includePostedVoucherLink: true, paymentStatus: "PREPARED");

        await using (var db = harness.CreateDb())
        {
            var payment = await db.AccountingHandoffPayments.SingleAsync(p => p.Id == 17);
            payment.PaymentMethodCode = null;
            db.PaymentMethods.Add(new PaymentMethod { Id = 6, BranchId = 2, Code = "BANK2", MethodType = "BANK", GlAccountCode = "BANK-GL", IsActive = true });
            await db.SaveChangesAsync();
        }

        await using var dbVerify = harness.CreateDb();
        var service = harness.CreateService(dbVerify);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.ConfirmPaymentAsync(17, "BANK-REF-77", "TRANSFER-IMAGE-4", "accounting-user"));
        Assert.Contains("missing or ambiguous", ex.Message);
    }

    [Fact]
    public async Task ConfirmPayment_WhenVoucherCurrencyMismatch_ThrowsInvalidOperationException()
    {
        using var harness = new PaymentReceiptHarness();
        harness.EnableLinkedVoucher();
        await harness.SeedAsync(includePostedVoucherLink: true, paymentStatus: "PREPARED");

        await using (var db = harness.CreateDb())
        {
            var curr = await db.SysCurrencies.SingleAsync(c => c.Id == 1);
            curr.ShortNameEn = "EUR";
            await db.SaveChangesAsync();
        }

        await using var dbVerify = harness.CreateDb();
        var service = harness.CreateService(dbVerify);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.ConfirmPaymentAsync(17, "BANK-REF-77", "TRANSFER-IMAGE-4", "accounting-user"));
        Assert.Contains("requires a posted ERP Payment Voucher matching the handoff branch, currency, and amount", ex.Message);
    }

    [Fact]
    public async Task ConfirmPayment_WhenVoucherAmountMismatch_ThrowsInvalidOperationException()
    {
        using var harness = new PaymentReceiptHarness();
        var mismatchedVoucher = new GlVoucherHeader
        {
            Id = 7,
            BranchId = 2,
            SourceSystemCode = "HR_PAYMENT",
            SourceRefId = 17,
            VoucherType = 103,
            Status = 3,
            PostDate = new DateTime(2026, 11, 7, 12, 30, 0, DateTimeKind.Utc),
            TotalAmount = 50m,
            TotalLocalDebit = 50m,
            TotalLocalCredit = 50m,
            Details =
            [
                new GlVoucherDetail { VoucherId = 7, LineSer = 1, BranchId = 2, CurrencyId = 1, AccountCode = "SALARY-PAYABLE", Debit = 50m },
                new GlVoucherDetail { VoucherId = 7, LineSer = 2, BranchId = 2, CurrencyId = 1, AccountCode = "BANK-GL", Credit = 50m }
            ]
        };
        harness.SetupVouchersForPayment(2, 17, new[] { mismatchedVoucher });
        await harness.SeedAsync(includePostedVoucherLink: true, paymentStatus: "PREPARED");

        await using var db = harness.CreateDb();
        var service = harness.CreateService(db);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.ConfirmPaymentAsync(17, "BANK-REF-77", "TRANSFER-IMAGE-4", "accounting-user"));
        Assert.Contains("requires a posted ERP Payment Voucher matching the handoff branch, currency, and amount", ex.Message);
    }

    [Fact]
    public async Task ConfirmPayment_WhenVoucherAlreadyReversed_ThrowsInvalidOperationException()
    {
        using var harness = new PaymentReceiptHarness();
        var reversedVoucher = new GlVoucherHeader
        {
            Id = 7,
            BranchId = 2,
            SourceSystemCode = "HR_PAYMENT",
            SourceRefId = 17,
            VoucherType = 103,
            Status = 3,
            IsReversed = true,
            PostDate = new DateTime(2026, 11, 7, 12, 30, 0, DateTimeKind.Utc),
            TotalAmount = 100m,
            TotalLocalDebit = 100m,
            TotalLocalCredit = 100m,
            Details =
            [
                new GlVoucherDetail { VoucherId = 7, LineSer = 1, BranchId = 2, CurrencyId = 1, AccountCode = "SALARY-PAYABLE", Debit = 100m },
                new GlVoucherDetail { VoucherId = 7, LineSer = 2, BranchId = 2, CurrencyId = 1, AccountCode = "BANK-GL", Credit = 100m }
            ]
        };
        harness.SetupVouchersForPayment(2, 17, new[] { reversedVoucher });
        await harness.SeedAsync(includePostedVoucherLink: true, paymentStatus: "PREPARED");

        await using var db = harness.CreateDb();
        var service = harness.CreateService(db);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.ConfirmPaymentAsync(17, "BANK-REF-77", "TRANSFER-IMAGE-4", "accounting-user"));
        Assert.Contains("requires a posted ERP Payment Voucher matching the handoff branch, currency, and amount", ex.Message);
    }

    [Fact]
    public async Task ConfirmPayment_WhenDebitedAccountMismatchesJournalSalaryPayable_ThrowsInvalidOperationException()
    {
        using var harness = new PaymentReceiptHarness();
        var wrongDebitVoucher = new GlVoucherHeader
        {
            Id = 7,
            BranchId = 2,
            SourceSystemCode = "HR_PAYMENT",
            SourceRefId = 17,
            VoucherType = 103,
            Status = 3,
            PostDate = new DateTime(2026, 11, 7, 12, 30, 0, DateTimeKind.Utc),
            TotalAmount = 100m,
            TotalLocalDebit = 100m,
            TotalLocalCredit = 100m,
            Details =
            [
                new GlVoucherDetail { VoucherId = 7, LineSer = 1, BranchId = 2, CurrencyId = 1, AccountCode = "WRONG-ACCOUNT", Debit = 100m },
                new GlVoucherDetail { VoucherId = 7, LineSer = 2, BranchId = 2, CurrencyId = 1, AccountCode = "BANK-GL", Credit = 100m }
            ]
        };
        harness.SetupVouchersForPayment(2, 17, new[] { wrongDebitVoucher });
        await harness.SeedAsync(includePostedVoucherLink: true, paymentStatus: "PREPARED");

        await using var db = harness.CreateDb();
        var service = harness.CreateService(db);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.ConfirmPaymentAsync(17, "BANK-REF-77", "TRANSFER-IMAGE-4", "accounting-user"));
        Assert.Contains("requires a posted ERP Payment Voucher matching the handoff branch, currency, and amount", ex.Message);
    }

    [Fact]
    public async Task ConfirmPayment_WhenCreditedAccountMismatchesPaymentMethodGlAccount_ThrowsInvalidOperationException()
    {
        using var harness = new PaymentReceiptHarness();
        var wrongCreditVoucher = new GlVoucherHeader
        {
            Id = 7,
            BranchId = 2,
            SourceSystemCode = "HR_PAYMENT",
            SourceRefId = 17,
            VoucherType = 103,
            Status = 3,
            PostDate = new DateTime(2026, 11, 7, 12, 30, 0, DateTimeKind.Utc),
            TotalAmount = 100m,
            TotalLocalDebit = 100m,
            TotalLocalCredit = 100m,
            Details =
            [
                new GlVoucherDetail { VoucherId = 7, LineSer = 1, BranchId = 2, CurrencyId = 1, AccountCode = "SALARY-PAYABLE", Debit = 100m },
                new GlVoucherDetail { VoucherId = 7, LineSer = 2, BranchId = 2, CurrencyId = 1, AccountCode = "WRONG-CREDIT", Credit = 100m }
            ]
        };
        harness.SetupVouchersForPayment(2, 17, new[] { wrongCreditVoucher });
        await harness.SeedAsync(includePostedVoucherLink: true, paymentStatus: "PREPARED");

        await using var db = harness.CreateDb();
        var service = harness.CreateService(db);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.ConfirmPaymentAsync(17, "BANK-REF-77", "TRANSFER-IMAGE-4", "accounting-user"));
        Assert.Contains("requires a posted ERP Payment Voucher matching the handoff branch, currency, and amount", ex.Message);
    }

    [Fact]
    public async Task FailPayment_WhenHandoffMissingBranch_ThrowsInvalidOperationException()
    {
        using var harness = new PaymentReceiptHarness();
        await harness.SeedAsync(includePostedVoucherLink: true, paymentStatus: "PREPARED");

        await using (var db = harness.CreateDb())
        {
            var handoff = await db.HrAccountingHandoffs.SingleAsync(h => h.Id == 11);
            handoff.BranchId = null;
            await db.SaveChangesAsync();
        }

        await using var dbVerify = harness.CreateDb();
        var service = harness.CreateService(dbVerify);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.FailPaymentAsync(17, "Payment gateway timed out", "accounting-user"));
        Assert.Contains("Accounting handoff has no branch", ex.Message);
    }

    [Fact]
    public async Task FailPayment_WhenMultipleLinkedVouchers_ThrowsInvalidOperationException()
    {
        using var harness = new PaymentReceiptHarness();
        harness.EnableMultipleLinkedVouchers();
        await harness.SeedAsync(includePostedVoucherLink: true, paymentStatus: "PREPARED");

        await using var db = harness.CreateDb();
        var service = harness.CreateService(db);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.FailPaymentAsync(17, "Payment gateway timed out", "accounting-user"));
        Assert.Contains("More than one Payment Voucher is linked to this payment instruction", ex.Message);
    }

    [Fact]
    public async Task FailPayment_WhenDraftVoucherDeleteReturnsFalse_ThrowsInvalidOperationException()
    {
        using var harness = new PaymentReceiptHarness();
        harness.EnableDraftLinkedVoucher();
        harness.PaymentVouchersMock.Setup(p => p.DeletePaymentVoucherAsync(7, "accounting-user", It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        await harness.SeedAsync(includePostedVoucherLink: true, paymentStatus: "PREPARED");

        await using var db = harness.CreateDb();
        var service = harness.CreateService(db);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.FailPaymentAsync(17, "Payment rejected by bank", "accounting-user"));
        Assert.Contains("The draft Payment Voucher could not be voided", ex.Message);
    }

    [Fact]
    public async Task FailPayment_WhenVoucherAlreadyCancelled_Status4_DoesNotCallDeletePaymentVoucher()
    {
        using var harness = new PaymentReceiptHarness();
        var cancelledVoucher = new GlVoucherHeader
        {
            Id = 7,
            BranchId = 2,
            SourceSystemCode = "HR_PAYMENT",
            SourceRefId = 17,
            VoucherType = 103,
            Status = 4,
            PostDate = null
        };
        harness.SetupVouchersForPayment(2, 17, new[] { cancelledVoucher });
        await harness.SeedAsync(includePostedVoucherLink: true, paymentStatus: "PREPARED");

        await using (var db = harness.CreateDb())
        {
            var service = harness.CreateService(db);
            var result = await service.FailPaymentAsync(17, "Payment cancelled by user", "accounting-user");
            Assert.Equal("FAILED", result.Status);
            Assert.Equal(7, result.PaymentVoucherId);
        }

        harness.PaymentVouchersMock.Verify(p => p.DeletePaymentVoucherAsync(It.IsAny<long>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ReversePayment_WhenHandoffMissingBranch_ThrowsInvalidOperationException()
    {
        using var harness = new PaymentReceiptHarness();
        harness.EnableLinkedVoucherWithReversal();
        await harness.SeedAsync(includePostedVoucherLink: true, paymentStatus: "CONFIRMED");

        await using (var db = harness.CreateDb())
        {
            var handoff = await db.HrAccountingHandoffs.SingleAsync(h => h.Id == 11);
            handoff.BranchId = null;
            await db.SaveChangesAsync();
        }

        await using var dbVerify = harness.CreateDb();
        var service = harness.CreateService(dbVerify);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.ReversePaymentAsync(17, "Reversal reason", "REV-EXT-1", "REV-EVID-1", "accounting-user"));
        Assert.Contains("Accounting handoff has no branch", ex.Message);
    }

    [Fact]
    public async Task ReversePayment_WhenMultipleLinkedVouchers_ThrowsInvalidOperationException()
    {
        using var harness = new PaymentReceiptHarness();
        harness.EnableMultipleLinkedVouchers();
        await harness.SeedAsync(includePostedVoucherLink: true, paymentStatus: "CONFIRMED");

        await using var db = harness.CreateDb();
        var service = harness.CreateService(db);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.ReversePaymentAsync(17, "Reversal reason", "REV-EXT-1", "REV-EVID-1", "accounting-user"));
        Assert.Contains("unique HR Payment Voucher must be linked before reversal", ex.Message);
    }

    [Fact]
    public async Task ReversePayment_WhenOriginalVoucherNotPosted_ThrowsInvalidOperationException()
    {
        using var harness = new PaymentReceiptHarness();
        var draftVoucher = new GlVoucherHeader
        {
            Id = 7,
            BranchId = 2,
            SourceSystemCode = "HR_PAYMENT",
            SourceRefId = 17,
            Status = 1,
            PostDate = null,
            IsReversed = false
        };
        harness.SetupVouchersForPayment(2, 17, new[] { draftVoucher });
        await harness.SeedAsync(includePostedVoucherLink: true, paymentStatus: "CONFIRMED");

        await using var db = harness.CreateDb();
        var service = harness.CreateService(db);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.ReversePaymentAsync(17, "Reversal reason", "REV-EXT-1", "REV-EVID-1", "accounting-user"));
        Assert.Contains("Only a posted Payment Voucher can be reversed", ex.Message);
    }

    [Fact]
    public async Task ReversePayment_WhenReversalVoucherMismatchesOriginal_ThrowsInvalidOperationException()
    {
        using var harness = new PaymentReceiptHarness();
        var originalVoucher = new GlVoucherHeader
        {
            Id = 7,
            BranchId = 2,
            SourceSystemCode = "HR_PAYMENT",
            SourceRefId = 17,
            VoucherType = 103,
            Status = 3,
            IsReversed = false,
            PostDate = new DateTime(2026, 11, 7, 12, 0, 0, DateTimeKind.Utc)
        };
        var badReversal = new GlVoucherHeader
        {
            Id = 99,
            BranchId = 999,
            SourceSystemCode = "GL_REVERSAL",
            SourceRefId = 7,
            VoucherType = 103,
            Status = 3,
            PostDate = new DateTime(2026, 11, 7, 13, 0, 0, DateTimeKind.Utc)
        };
        harness.SetupVouchersForPayment(2, 17, new[] { originalVoucher });
        harness.GlVouchersMock.Setup(g => g.ReverseVoucherAsync(7, "accounting-user", "Bad reversal test", It.IsAny<CancellationToken>()))
            .Callback<long, string, string, CancellationToken>((_, _, _, _) => originalVoucher.IsReversed = true)
            .ReturnsAsync(new GlVoucherHeaderDto { Id = 99 });
        harness.SetupReversalVouchers(2, 7, new[] { badReversal });
        await harness.SeedAsync(includePostedVoucherLink: true, paymentStatus: "CONFIRMED");

        await using var db = harness.CreateDb();
        var service = harness.CreateService(db);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.ReversePaymentAsync(17, "Bad reversal test", "REV-EXT-1", "REV-EVID-1", "accounting-user"));
        Assert.Contains("The ERP reversal voucher was not created or verified", ex.Message);
    }

    [Fact]
    public async Task ConfirmPayment_PropagatesCancellationToken()
    {
        using var harness = new PaymentReceiptHarness();
        await harness.SeedAsync();

        using var cts = new CancellationTokenSource();
        cts.Cancel();

        await using var db = harness.CreateDb();
        var service = harness.CreateService(db);

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            service.ConfirmPaymentAsync(17, "BANK-REF-77", "TRANSFER-IMAGE-4", "accounting-user", cts.Token));
    }

    private sealed class PaymentReceiptHarness : IDisposable
    {
        private readonly string storeName = Guid.NewGuid().ToString();
        private readonly DbContextOptions<OracleDbContext> options;
        private readonly Mock<IGlVoucherRepository> vouchers = new();
        public Mock<IReceiptPaymentVoucherService> PaymentVouchersMock { get; } = new();
        public Mock<IGlVoucherService> GlVouchersMock { get; } = new();

        private readonly GlVoucherHeader journal = new()
        {
            Id = 8,
            BranchId = 2,
            SourceSystemCode = "HR_HANDOFF",
            SourceRefId = 11,
            Status = 3,
            PostDate = new DateTime(2026, 11, 7, 10, 0, 0, DateTimeKind.Utc),
            Details = [new GlVoucherDetail { VoucherId = 8, LineSer = 3, BranchId = 2, CurrencyId = 1, AccountCode = "SALARY-PAYABLE", Credit = 100m }]
        };

        private readonly GlVoucherHeader paymentVoucher = new()
        {
            Id = 7,
            BranchId = 2,
            SourceSystemCode = "HR_PAYMENT",
            SourceRefId = 17,
            VoucherType = 103,
            Status = 3,
            PostDate = new DateTime(2026, 11, 7, 12, 30, 0, DateTimeKind.Utc),
            TotalAmount = 100m,
            TotalLocalDebit = 100m,
            TotalLocalCredit = 100m,
            Details =
            [
                new GlVoucherDetail { VoucherId = 7, LineSer = 1, BranchId = 2, CurrencyId = 1, AccountCode = "SALARY-PAYABLE", Debit = 100m, LocalDebit = 100m, BaseDebit = 100m, ExchangeRate = 1m },
                new GlVoucherDetail { VoucherId = 7, LineSer = 2, BranchId = 2, CurrencyId = 1, AccountCode = "BANK-GL", Credit = 100m, LocalCredit = 100m, BaseCredit = 100m, ExchangeRate = 1m }
            ]
        };

        public PaymentReceiptHarness()
        {
            options = new DbContextOptionsBuilder<OracleDbContext>()
                .UseInMemoryDatabase(storeName)
                .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
                .Options;

            vouchers.Setup(x => x.GetBySourceAsync(2, "HR_PAYMENT", 17, It.IsAny<CancellationToken>()))
                .ReturnsAsync(Array.Empty<GlVoucherHeader>());
            vouchers.Setup(x => x.GetByIdAsync(8, It.IsAny<CancellationToken>())).ReturnsAsync(journal);
            PaymentVouchersMock.Setup(p => p.DeletePaymentVoucherAsync(It.IsAny<long>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(true);
        }

        public void EnableLinkedVoucher()
        {
            vouchers.Setup(x => x.GetBySourceAsync(2, "HR_PAYMENT", 17, It.IsAny<CancellationToken>()))
                .ReturnsAsync(new[] { paymentVoucher });
        }

        public void EnableMultipleLinkedVouchers()
        {
            var secondVoucher = new GlVoucherHeader { Id = 77, BranchId = 2, SourceSystemCode = "HR_PAYMENT", SourceRefId = 17 };
            vouchers.Setup(x => x.GetBySourceAsync(2, "HR_PAYMENT", 17, It.IsAny<CancellationToken>()))
                .ReturnsAsync(new[] { paymentVoucher, secondVoucher });
        }

        public void EnableDraftLinkedVoucher()
        {
            var draftVoucher = new GlVoucherHeader
            {
                Id = 7,
                BranchId = 2,
                SourceSystemCode = "HR_PAYMENT",
                SourceRefId = 17,
                VoucherType = 103,
                Status = 1, // Draft
                PostDate = null,
                TotalAmount = 100m,
                TotalLocalDebit = 100m,
                TotalLocalCredit = 100m,
                Details =
                [
                    new GlVoucherDetail { VoucherId = 7, LineSer = 1, BranchId = 2, CurrencyId = 1, AccountCode = "SALARY-PAYABLE", Debit = 100m },
                    new GlVoucherDetail { VoucherId = 7, LineSer = 2, BranchId = 2, CurrencyId = 1, AccountCode = "BANK-GL", Credit = 100m }
                ]
            };
            vouchers.Setup(x => x.GetBySourceAsync(2, "HR_PAYMENT", 17, It.IsAny<CancellationToken>()))
                .ReturnsAsync(new[] { draftVoucher });
        }

        public void EnableLinkedVoucherWithReversal()
        {
            var reversalVoucher = new GlVoucherHeader
            {
                Id = 99,
                BranchId = 2,
                SourceSystemCode = "GL_REVERSAL",
                SourceRefId = paymentVoucher.Id,
                VoucherType = 103,
                Status = 3,
                IsReversed = false,
                PostDate = new DateTime(2026, 11, 7, 13, 0, 0, DateTimeKind.Utc),
                TotalAmount = 100m,
                TotalLocalDebit = 100m,
                TotalLocalCredit = 100m,
                Details =
                [
                    new GlVoucherDetail { VoucherId = 99, LineSer = 1, BranchId = 2, CurrencyId = 1, AccountCode = "SALARY-PAYABLE", Credit = 100m, LocalCredit = 100m, BaseCredit = 100m, ExchangeRate = 1m },
                    new GlVoucherDetail { VoucherId = 99, LineSer = 2, BranchId = 2, CurrencyId = 1, AccountCode = "BANK-GL", Debit = 100m, LocalDebit = 100m, BaseDebit = 100m, ExchangeRate = 1m }
                ]
            };

            vouchers.Setup(x => x.GetBySourceAsync(2, "HR_PAYMENT", 17, It.IsAny<CancellationToken>()))
                .ReturnsAsync(new[] { paymentVoucher });

            GlVouchersMock.Setup(g => g.ReverseVoucherAsync(paymentVoucher.Id, It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .Callback<long, string, string, CancellationToken>((_, _, _, _) =>
                {
                    paymentVoucher.IsReversed = true;
                })
                .ReturnsAsync(new GlVoucherHeaderDto { Id = reversalVoucher.Id });

            vouchers.Setup(x => x.GetBySourceAsync(2, "GL_REVERSAL", paymentVoucher.Id, It.IsAny<CancellationToken>()))
                .ReturnsAsync(() => paymentVoucher.IsReversed ? new[] { reversalVoucher } : Array.Empty<GlVoucherHeader>());
        }

        public void SetupVouchersForPayment(long branchId, long paymentId, IReadOnlyList<GlVoucherHeader> voucherList)
        {
            vouchers.Setup(x => x.GetBySourceAsync(branchId, "HR_PAYMENT", paymentId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(voucherList);
        }

        public void SetupJournal(long journalId, GlVoucherHeader customJournal)
        {
            vouchers.Setup(x => x.GetByIdAsync(journalId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(customJournal);
        }

        public void SetupReversalVouchers(long branchId, long originalVoucherId, IReadOnlyList<GlVoucherHeader> reversalList)
        {
            vouchers.Setup(x => x.GetBySourceAsync(branchId, "GL_REVERSAL", originalVoucherId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(reversalList);
        }

        public OracleDbContext CreateDb() => new(options);

        public HrAccountingHandoffService CreateService(OracleDbContext db) => new(
            db,
            vouchers.Object,
            Mock.Of<IHrPayrollPostingService>(),
            PaymentVouchersMock.Object,
            GlVouchersMock.Object);

        public async Task SeedAsync(bool includePostedVoucherLink = false, string paymentStatus = "CONFIRMED")
        {
            await using var db = CreateDb();
            if (includePostedVoucherLink)
            {
                db.SysBranches.Add(new SysBranch { Id = 2, CompanyId = 3, IsActive = true });
                db.SysCurrencies.Add(new SysCurrency { Id = 1, ShortNameEn = "USD", CurrencyNameEn = "US Dollar" });
                db.PaymentMethods.Add(new PaymentMethod { Id = 5, BranchId = 2, Code = "BANK1", MethodType = "BANK", GlAccountCode = "BANK-GL", IsActive = true, ShowInVouchers = true });
            }
            db.HrAccountingHandoffs.Add(new HrAccountingHandoff
            {
                Id = 11,
                CompanyId = 3,
                BranchId = 2,
                SourceType = "PAYROLL",
                SourceId = "payroll-run-9",
                SourceVersion = 1,
                Operation = "SUBMIT",
                HandoffReference = "HR-HANDOFF-11",
                PayloadHash = "handoff-payload",
                DeliveryStatus = "POSTED",
                CreatedAtUtc = new DateTime(2026, 11, 7, 9, 0, 0, DateTimeKind.Utc),
                CreatedBy = "hr-user",
                Lines = includePostedVoucherLink ? [new HrAccountingHandoffLine { LineType = "CONTROL:NET_PAY", CurrencyCode = "USD", Amount = 100m, BranchId = 2 }] : []
            });
            if (includePostedVoucherLink)
            {
                db.AccountingHandoffJournalLinks.Add(new AccountingHandoffJournalLink { Id = 4, HandoffId = 11, VoucherId = 8, CreatedAtUtc = DateTime.UtcNow, CreatedBy = "accounting-user" });
                db.AccountingHandoffPostingReceipts.Add(new AccountingHandoffPostingReceipt { Id = 6, HandoffId = 11, VoucherId = 8, EventKey = "posting-event-001", PayloadHash = "posting-payload", ReceivedAtUtc = DateTime.UtcNow, ReceivedBy = "accounting-user" });
            }
            db.AccountingHandoffPayments.Add(new AccountingHandoffPayment
            {
                Id = 17,
                HandoffId = 11,
                PaymentSequence = 1,
                IdempotencyKey = "payment-instruction-17",
                CurrencyCode = "USD",
                Amount = 100m,
                Method = "BANK",
                PaymentMethodCode = includePostedVoucherLink ? "BANK1" : null,
                Status = paymentStatus,
                ExternalReference = paymentStatus == "CONFIRMED" ? "BANK-REF-77" : null,
                EvidenceReference = paymentStatus == "CONFIRMED" ? "TRANSFER-IMAGE-4" : null,
                ConfirmedAtUtc = paymentStatus == "CONFIRMED" ? new DateTime(2026, 11, 7, 12, 0, 0, DateTimeKind.Utc) : null,
                ConfirmedBy = paymentStatus == "CONFIRMED" ? "accounting-user" : null,
                CreatedAtUtc = new DateTime(2026, 11, 7, 11, 0, 0, DateTimeKind.Utc),
                CreatedBy = "accounting-user"
            });
            await db.SaveChangesAsync();
        }

        public void Dispose() { }
    }
}
