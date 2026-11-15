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

public sealed class AccountingHandoffPaymentFailureTests
{
    [Fact]
    public async Task PreparedPaymentCanBeFailedWithRequiredReasonAndExactReplay()
    {
        using var harness = new Harness("PREPARED", postedPaymentVoucher: false);
        await harness.SeedAsync();

        await using (var db = harness.CreateDb())
        {
            var first = await harness.CreateService(db).FailPaymentAsync(10, "Bank instruction cancelled", "accounting");
            Assert.Equal("FAILED", first.Status);
            Assert.Equal("Bank instruction cancelled", first.FailureReason);
        }
        await using (var db = harness.CreateDb())
        {
            var replay = await harness.CreateService(db).FailPaymentAsync(10, "Bank instruction cancelled", "retry");
            Assert.Equal("FAILED", replay.Status);
        }

        await using var verify = harness.CreateDb();
        var persisted = await verify.AccountingHandoffPayments.SingleAsync();
        Assert.Equal("accounting", persisted.FailedBy);
        Assert.Single(await verify.AccountingHandoffPayments.ToListAsync());
    }

    [Fact]
    public async Task PreparedPaymentIsMarkedFailedOnlyAfterLinkedDraftIsVoided()
    {
        using var harness = new Harness("PREPARED", postedPaymentVoucher: false);
        await harness.SeedAsync();
        harness.LinkDraftPaymentVoucher(deleteSucceeds: true);

        await using (var db = harness.CreateDb())
        {
            var failed = await harness.CreateService(db).FailPaymentAsync(10, "Transfer cancelled", "accounting");
            Assert.Equal("FAILED", failed.Status);
            Assert.Equal(71, failed.PaymentVoucherId);
        }

        harness.PaymentVouchers.Verify(v => v.DeletePaymentVoucherAsync(71, "accounting", It.IsAny<CancellationToken>()), Times.Once);
        await using var verify = harness.CreateDb();
        var persisted = await verify.AccountingHandoffPayments.SingleAsync();
        Assert.Equal("FAILED", persisted.Status);
        Assert.Equal("Transfer cancelled", persisted.FailureReason);
    }

    [Fact]
    public async Task PreparedPaymentStaysPreparedWhenLinkedDraftCannotBeVoided()
    {
        using var harness = new Harness("PREPARED", postedPaymentVoucher: false);
        await harness.SeedAsync();
        harness.LinkDraftPaymentVoucher(deleteSucceeds: false);

        await using var db = harness.CreateDb();
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            harness.CreateService(db).FailPaymentAsync(10, "Transfer cancelled", "accounting"));

        await using var verify = harness.CreateDb();
        var persisted = await verify.AccountingHandoffPayments.SingleAsync();
        Assert.Equal("PREPARED", persisted.Status);
        Assert.Null(persisted.FailureReason);
    }

    [Theory]
    [InlineData("CONFIRMED", false)]
    [InlineData("PREPARED", true)]
    public async Task ConfirmedOrAlreadyPostedPaymentCannotBeFailed(string paymentStatus, bool postedPaymentVoucher)
    {
        using var harness = new Harness(paymentStatus, postedPaymentVoucher);
        await harness.SeedAsync();

        await using var db = harness.CreateDb();
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            harness.CreateService(db).FailPaymentAsync(10, "Bank instruction cancelled", "accounting"));

        await using var verify = harness.CreateDb();
        Assert.Equal(paymentStatus, (await verify.AccountingHandoffPayments.SingleAsync()).Status);
    }

    [Theory]
    [InlineData("ok")] // < 3 chars
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public async Task FailPayment_ReasonValidation_ThrowsArgumentException(string? reason)
    {
        using var harness = new Harness("PREPARED", postedPaymentVoucher: false);
        await harness.SeedAsync();

        await using var db = harness.CreateDb();
        await Assert.ThrowsAsync<ArgumentException>(() =>
            harness.CreateService(db).FailPaymentAsync(10, reason!, "accounting"));
    }

    [Fact]
    public async Task FailPayment_PaymentNotFound_ThrowsKeyNotFoundException()
    {
        using var harness = new Harness("PREPARED", postedPaymentVoucher: false);
        await harness.SeedAsync();

        await using var db = harness.CreateDb();
        var ex = await Assert.ThrowsAsync<KeyNotFoundException>(() =>
            harness.CreateService(db).FailPaymentAsync(999, "Bank cancelled", "accounting"));

        Assert.Contains("Payment instruction not found", ex.Message);
    }

    [Fact]
    public async Task FailPayment_DifferentReasonAfterFailed_ThrowsInvalidOperationException()
    {
        using var harness = new Harness("PREPARED", postedPaymentVoucher: false);
        await harness.SeedAsync();

        await using (var db = harness.CreateDb())
        {
            await harness.CreateService(db).FailPaymentAsync(10, "Initial reason", "accounting");
        }

        await using (var db = harness.CreateDb())
        {
            var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
                harness.CreateService(db).FailPaymentAsync(10, "Different reason", "accounting"));

            Assert.Contains("A failed payment instruction is immutable", ex.Message);
        }
    }

    [Fact]
    public async Task FailPayment_MoreThanOneLinkedPaymentVoucher_ThrowsInvalidOperationException()
    {
        using var harness = new Harness("PREPARED", postedPaymentVoucher: false);
        await harness.SeedAsync();
        harness.SetupMultipleLinkedVouchers();

        await using var db = harness.CreateDb();
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            harness.CreateService(db).FailPaymentAsync(10, "Bank cancelled", "accounting"));

        Assert.Contains("More than one Payment Voucher is linked", ex.Message);
    }

    [Fact]
    public async Task FailPayment_LinkedVoucherAlreadyCancelled_MarksFailedWithoutDeleting()
    {
        using var harness = new Harness("PREPARED", postedPaymentVoucher: false);
        await harness.SeedAsync();
        harness.LinkCancelledPaymentVoucher();

        await using (var db = harness.CreateDb())
        {
            var failed = await harness.CreateService(db).FailPaymentAsync(10, "Transfer already cancelled", "accounting");
            Assert.Equal("FAILED", failed.Status);
            Assert.Equal(71, failed.PaymentVoucherId);
        }

        harness.PaymentVouchers.Verify(v => v.DeletePaymentVoucherAsync(It.IsAny<long>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
        await using var verify = harness.CreateDb();
        var persisted = await verify.AccountingHandoffPayments.SingleAsync();
        Assert.Equal("FAILED", persisted.Status);
    }

    [Fact]
    public async Task FailPayment_MissingHandoff_ThrowsKeyNotFoundException()
    {
        using var harness = new Harness("PREPARED", postedPaymentVoucher: false);
        await harness.SeedAsync();

        await using (var db = harness.CreateDb())
        {
            var handoff = await db.HrAccountingHandoffs.SingleAsync(h => h.Id == 42);
            db.HrAccountingHandoffs.Remove(handoff);
            await db.SaveChangesAsync();
        }

        await using (var db = harness.CreateDb())
        {
            var ex = await Assert.ThrowsAsync<KeyNotFoundException>(() =>
                harness.CreateService(db).FailPaymentAsync(10, "Bank cancelled", "accounting"));

            Assert.Contains("Payment handoff not found", ex.Message);
        }
    }

    private sealed class Harness(string paymentStatus, bool postedPaymentVoucher) : IDisposable
    {
        private readonly DbContextOptions<OracleDbContext> options = new DbContextOptionsBuilder<OracleDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning)).Options;
        private readonly Mock<IGlVoucherRepository> vouchers = new();
        private readonly Mock<IReceiptPaymentVoucherService> paymentVouchers = new();
        private static readonly GlVoucherHeader journal = new()
        {
            Id = 70, BranchId = 3, SourceSystemCode = "HR_HANDOFF", SourceRefId = 42, Status = 3,
            PostDate = new DateTime(2026, 11, 7, 12, 0, 0, DateTimeKind.Utc),
            Details = [new GlVoucherDetail { VoucherId = 70, LineSer = 3, BranchId = 3, CurrencyId = 9, AccountCode = "NET", Credit = 60m }]
        };
        private static readonly GlVoucherHeader paymentVoucher = new()
        {
            Id = 71, BranchId = 3, SourceSystemCode = "HR_PAYMENT", SourceRefId = 10, Status = 3,
            VoucherType = 103, PostDate = new DateTime(2026, 11, 7, 13, 0, 0, DateTimeKind.Utc), TotalAmount = 60m,
            Details = [new GlVoucherDetail { VoucherId = 71, LineSer = 1, BranchId = 3, CurrencyId = 9, AccountCode = "NET", Debit = 60m }]
        };

        public OracleDbContext CreateDb() => new(options);

        public Mock<IReceiptPaymentVoucherService> PaymentVouchers => paymentVouchers;

        public HrAccountingHandoffService CreateService(OracleDbContext db) => new(db, vouchers.Object,
            Mock.Of<IHrPayrollPostingService>(), paymentVouchers.Object, Mock.Of<IGlVoucherService>());

        public void SetupMultipleLinkedVouchers()
        {
            vouchers.Setup(v => v.GetBySourceAsync(3, "HR_PAYMENT", 10, It.IsAny<CancellationToken>()))
                .ReturnsAsync([
                    new GlVoucherHeader { Id = 71, BranchId = 3, Status = 1 },
                    new GlVoucherHeader { Id = 72, BranchId = 3, Status = 1 }
                ]);
        }

        public void LinkCancelledPaymentVoucher()
        {
            vouchers.Setup(v => v.GetBySourceAsync(3, "HR_PAYMENT", 10, It.IsAny<CancellationToken>()))
                .ReturnsAsync([new GlVoucherHeader { Id = 71, BranchId = 3, SourceSystemCode = "HR_PAYMENT", SourceRefId = 10, Status = 4 }]);
        }

        public void LinkDraftPaymentVoucher(bool deleteSucceeds)
        {
            vouchers.Setup(v => v.GetBySourceAsync(3, "HR_PAYMENT", 10, It.IsAny<CancellationToken>()))
                .ReturnsAsync([new GlVoucherHeader { Id = 71, BranchId = 3, SourceSystemCode = "HR_PAYMENT", SourceRefId = 10, Status = 1 }]);
            paymentVouchers.Setup(v => v.DeletePaymentVoucherAsync(71, "accounting", It.IsAny<CancellationToken>()))
                .ReturnsAsync(deleteSucceeds);
        }

        public async Task SeedAsync()
        {
            vouchers.Setup(v => v.GetByIdAsync(70, It.IsAny<CancellationToken>())).ReturnsAsync(journal);
            vouchers.Setup(v => v.GetBySourceAsync(3, "HR_PAYMENT", 10, It.IsAny<CancellationToken>()))
                .ReturnsAsync(postedPaymentVoucher ? [paymentVoucher] : Array.Empty<GlVoucherHeader>());
            await using var db = CreateDb();
            db.HrAccountingHandoffs.Add(new HrAccountingHandoff
            {
                Id = 42, CompanyId = 1, BranchId = 3, SourceType = "PAYROLL", SourceId = "17", Operation = "SUBMIT",
                HandoffReference = "HR-42", PayloadHash = "hash", DeliveryStatus = "POSTED",
                CreatedAtUtc = DateTime.UtcNow, CreatedBy = "hr"
            });
            db.AccountingHandoffJournalLinks.Add(new AccountingHandoffJournalLink
            { Id = 7, HandoffId = 42, VoucherId = 70, CreatedAtUtc = DateTime.UtcNow, CreatedBy = "accounting" });
            db.AccountingHandoffPayments.Add(new AccountingHandoffPayment
            {
                Id = 10, HandoffId = 42, PaymentSequence = 1, IdempotencyKey = "payment-key-001", CurrencyCode = "USD",
                Amount = 60m, Method = "BANK", Status = paymentStatus, CreatedAtUtc = DateTime.UtcNow, CreatedBy = "accounting"
            });
            await db.SaveChangesAsync();
        }

        public void Dispose() { }
    }
}
