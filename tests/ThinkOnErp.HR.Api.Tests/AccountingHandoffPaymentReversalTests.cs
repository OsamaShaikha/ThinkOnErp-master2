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

public sealed class AccountingHandoffPaymentReversalTests
{
    [Fact]
    public async Task AccountingGlVoucherServiceCreatesPostedExactInverseForPaymentVoucher()
    {
        var repository = new Mock<IGlVoucherRepository>();
        var original = new GlVoucherHeader
        {
            Id = 71, BranchId = 3, FiscalYearId = 4, VoucherType = 103, VoucherNo = 7001,
            Status = 3, TotalAmount = 60m, TotalLocalDebit = 60m, TotalLocalCredit = 60m,
            Details =
            [
                new GlVoucherDetail { VoucherId = 71, LineSer = 1, BranchId = 3, CurrencyId = 9, AccountCode = "NET", Debit = 60m, LocalDebit = 60m, BaseDebit = 60m, ExchangeRate = 1m },
                new GlVoucherDetail { VoucherId = 71, LineSer = 2, BranchId = 3, CurrencyId = 9, AccountCode = "BANK-GL", Credit = 60m, LocalCredit = 60m, BaseCredit = 60m, ExchangeRate = 1m }
            ]
        };
        GlVoucherHeader? created = null;
        repository.Setup(r => r.GetByIdAsync(71, It.IsAny<CancellationToken>())).ReturnsAsync(original);
        repository.Setup(r => r.GetByIdAsync(72, It.IsAny<CancellationToken>())).ReturnsAsync(() => created);
        repository.Setup(r => r.GetVoucherTypeByCodeAsync(103, It.IsAny<CancellationToken>())).ReturnsAsync(new GlVoucherType
        { TypeCode = 103, TypeKey = "PAYMENT", SerialResetPolicy = "MONTHLY" });
        repository.Setup(r => r.GenerateNextSerialNoAsync(3, It.IsAny<int>(), It.IsAny<int>(), 103, "MONTHLY", It.IsAny<CancellationToken>())).ReturnsAsync(7002);
        repository.Setup(r => r.AddVoucherAsync(It.IsAny<GlVoucherHeader>(), It.IsAny<CancellationToken>()))
            .Callback<GlVoucherHeader, CancellationToken>((voucher, _) => { voucher.Id = 72; created = voucher; })
            .Returns(Task.CompletedTask);

        var service = new GlVoucherService(repository.Object, Mock.Of<IGlAccountRepository>(), Mock.Of<IGlCostCenterRepository>(),
            Mock.Of<IGlFiscalPeriodRepository>(), Mock.Of<IGlAccountBalanceRepository>(), Mock.Of<IArSubledgerRepository>(),
            Mock.Of<IApSubledgerRepository>(), Mock.Of<ICurrentTenantContext>(), NullLogger<GlVoucherService>.Instance);

        var result = await service.ReverseVoucherAsync(71, "accounting", "Duplicate transfer");

        Assert.True(original.IsReversed);
        Assert.Equal(72, result.Id);
        Assert.NotNull(created);
        Assert.Equal(3, created!.Status);
        Assert.Equal("GL_REVERSAL", created.SourceSystemCode);
        Assert.Equal(71, created.SourceRefId);
        Assert.Equal(60m, created.TotalAmount);
        Assert.Equal(60m, created.TotalLocalDebit);
        Assert.Equal(60m, created.TotalLocalCredit);
        Assert.Collection(created.Details,
            d => { Assert.Equal("NET", d.AccountCode); Assert.Equal(0m, d.Debit); Assert.Equal(60m, d.Credit); },
            d => { Assert.Equal("BANK-GL", d.AccountCode); Assert.Equal(60m, d.Debit); Assert.Equal(0m, d.Credit); });
        repository.Verify(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ConfirmedPaymentRequiresExactAccountingReversalAndPersistsEvidenceOnce()
    {
        using var harness = new Harness();
        await harness.SeedAsync();

        await using (var db = harness.CreateDb())
        {
            var reversed = await harness.CreateService(db).ReversePaymentAsync(
                10, "Duplicate transfer", "BANK-REV-101", "REV-EVIDENCE-4", "accounting");
            Assert.Equal("REVERSED", reversed.Status);
            Assert.Equal(72, reversed.ReversalVoucherId);
        }

        await using (var db = harness.CreateDb())
        {
            var replay = await harness.CreateService(db).ReversePaymentAsync(
                10, "Duplicate transfer", "BANK-REV-101", "REV-EVIDENCE-4", "retry");
            Assert.Equal("REVERSED", replay.Status);
            Assert.Equal(71, replay.PaymentVoucherId);
        }

        await using var verify = harness.CreateDb();
        var payment = await verify.AccountingHandoffPayments.SingleAsync();
        Assert.Equal("Duplicate transfer", payment.ReversalReason);
        Assert.Equal("BANK-REV-101", payment.ReversalExternalReference);
        Assert.Equal("REV-EVIDENCE-4", payment.ReversalEvidenceReference);
        Assert.Equal("accounting", payment.ReversedBy);
        harness.Vouchers.Verify(v => v.ReverseVoucherAsync(71, "accounting", "Duplicate transfer", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task PaymentReversalUsesAccountingVoucherServiceAndPersistsItsExactVoucher()
    {
        using var harness = new Harness();
        harness.UseRealAccountingVoucherService();
        await harness.SeedAsync();

        await using (var db = harness.CreateDb())
        {
            var result = await harness.CreateService(db, useRealAccounting: true).ReversePaymentAsync(
                10, "Duplicate transfer", "BANK-REV-101", "REV-EVIDENCE-4", "accounting");

            Assert.Equal("REVERSED", result.Status);
            Assert.Equal(72, result.ReversalVoucherId);
        }

        await using (var db = harness.CreateDb())
        {
            var replay = await harness.CreateService(db, useRealAccounting: true).ReversePaymentAsync(
                10, "Duplicate transfer", "BANK-REV-101", "REV-EVIDENCE-4", "retry");

            Assert.Equal("REVERSED", replay.Status);
            Assert.Equal(71, replay.PaymentVoucherId);
            Assert.Equal(72, replay.ReversalVoucherId);
        }

        Assert.True(harness.Original.IsReversed);
        Assert.NotNull(harness.CreatedReversal);
        Assert.Equal("GL_REVERSAL", harness.CreatedReversal!.SourceSystemCode);
        Assert.Equal(71, harness.CreatedReversal.SourceRefId);
        Assert.Equal(3, harness.CreatedReversal.Status);
        Assert.Collection(harness.CreatedReversal.Details,
            d => { Assert.Equal("NET", d.AccountCode); Assert.Equal(0m, d.Debit); Assert.Equal(60m, d.Credit); },
            d => { Assert.Equal("BANK-GL", d.AccountCode); Assert.Equal(60m, d.Debit); Assert.Equal(0m, d.Credit); });

        await using var verify = harness.CreateDb();
        var payment = await verify.AccountingHandoffPayments.SingleAsync();
        Assert.Equal("REVERSED", payment.Status);
        Assert.Equal(72, payment.ReversalVoucherId);
        Assert.Equal("REV-EVIDENCE-4", payment.ReversalEvidenceReference);
        harness.VoucherRepository.Verify(v => v.AddVoucherAsync(It.IsAny<GlVoucherHeader>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task PaymentRemainsConfirmedWhenAccountingReversalDoesNotExactlyInvertTheVoucher()
    {
        using var harness = new Harness();
        harness.CorruptReversalAmount();
        await harness.SeedAsync();

        await using var db = harness.CreateDb();
        await Assert.ThrowsAsync<InvalidOperationException>(() => harness.CreateService(db).ReversePaymentAsync(
            10, "Duplicate transfer", "BANK-REV-101", "REV-EVIDENCE-4", "accounting"));

        await using var verify = harness.CreateDb();
        var payment = await verify.AccountingHandoffPayments.SingleAsync();
        Assert.Equal("CONFIRMED", payment.Status);
        Assert.Null(payment.ReversalVoucherId);
        Assert.Null(payment.ReversalReason);
        harness.Vouchers.Verify(v => v.ReverseVoucherAsync(71, "accounting", "Duplicate transfer", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Theory]
    [InlineData("ok", "EXT-1", "EV-1")] // reason < 3 chars
    [InlineData("", "EXT-1", "EV-1")]
    [InlineData(null, "EXT-1", "EV-1")]
    [InlineData("Valid Reason", "", "EV-1")] // externalReference < 1 char
    [InlineData("Valid Reason", null, "EV-1")]
    [InlineData("Valid Reason", "EXT-1", "")] // evidenceReference < 1 char
    [InlineData("Valid Reason", "EXT-1", null)]
    public async Task ReversePayment_InvalidArguments_ThrowsArgumentException(string? reason, string? extRef, string? evRef)
    {
        using var harness = new Harness();
        await harness.SeedAsync();

        await using var db = harness.CreateDb();
        var service = harness.CreateService(db);

        await Assert.ThrowsAsync<ArgumentException>(() =>
            service.ReversePaymentAsync(10, reason!, extRef!, evRef!, "accounting"));
    }

    [Fact]
    public async Task ReversePayment_PaymentNotFound_ThrowsKeyNotFoundException()
    {
        using var harness = new Harness();
        await harness.SeedAsync();

        await using var db = harness.CreateDb();
        var service = harness.CreateService(db);

        var ex = await Assert.ThrowsAsync<KeyNotFoundException>(() =>
            service.ReversePaymentAsync(999, "Duplicate transfer", "BANK-REV-101", "REV-EVIDENCE-4", "accounting"));

        Assert.Contains("Payment instruction not found", ex.Message);
    }

    [Fact]
    public async Task ReversePayment_PaymentNotConfirmed_ThrowsInvalidOperationException()
    {
        using var harness = new Harness();
        await harness.SeedAsync();
        await harness.SetPaymentStatusAsync(10, "PREPARED");

        await using var db = harness.CreateDb();
        var service = harness.CreateService(db);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.ReversePaymentAsync(10, "Duplicate transfer", "BANK-REV-101", "REV-EVIDENCE-4", "accounting"));

        Assert.Contains("Only a confirmed payment with external payment evidence can be reversed", ex.Message);
    }

    [Fact]
    public async Task ReversePayment_AlreadyReversedWithDifferentDetails_ThrowsInvalidOperationException()
    {
        using var harness = new Harness();
        await harness.SeedAsync();

        await using (var db = harness.CreateDb())
        {
            await harness.CreateService(db).ReversePaymentAsync(
                10, "Duplicate transfer", "BANK-REV-101", "REV-EVIDENCE-4", "accounting");
        }

        await using (var db = harness.CreateDb())
        {
            var service = harness.CreateService(db);
            var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
                service.ReversePaymentAsync(10, "Different Reason", "BANK-REV-101", "REV-EVIDENCE-4", "accounting"));

            Assert.Contains("A reversed payment is immutable", ex.Message);
        }
    }

    [Fact]
    public async Task ReversePayment_NoLinkedPaymentVoucher_ThrowsInvalidOperationException()
    {
        using var harness = new Harness();
        await harness.SeedAsync();
        harness.SetupLinkedVouchers(10); // 0 vouchers

        await using var db = harness.CreateDb();
        var service = harness.CreateService(db);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.ReversePaymentAsync(10, "Duplicate transfer", "BANK-REV-101", "REV-EVIDENCE-4", "accounting"));

        Assert.Contains("A unique HR Payment Voucher must be linked before reversal", ex.Message);
    }

    [Fact]
    public async Task ReversePayment_MultipleLinkedPaymentVouchers_ThrowsInvalidOperationException()
    {
        using var harness = new Harness();
        await harness.SeedAsync();
        harness.SetupLinkedVouchers(10, new GlVoucherHeader { Id = 71, BranchId = 3 }, new GlVoucherHeader { Id = 73, BranchId = 3 });

        await using var db = harness.CreateDb();
        var service = harness.CreateService(db);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.ReversePaymentAsync(10, "Duplicate transfer", "BANK-REV-101", "REV-EVIDENCE-4", "accounting"));

        Assert.Contains("A unique HR Payment Voucher must be linked before reversal", ex.Message);
    }

    [Fact]
    public async Task ReversePayment_OriginalVoucherNotPosted_ThrowsInvalidOperationException()
    {
        using var harness = new Harness();
        harness.SetupOriginalVoucherDraft();
        await harness.SeedAsync();

        await using var db = harness.CreateDb();
        var service = harness.CreateService(db);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.ReversePaymentAsync(10, "Duplicate transfer", "BANK-REV-101", "REV-EVIDENCE-4", "accounting"));

        Assert.Contains("Only a posted Payment Voucher can be reversed", ex.Message);
    }

    private sealed class Harness : IDisposable
    {
        private readonly DbContextOptions<OracleDbContext> options = new DbContextOptionsBuilder<OracleDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning)).Options;
        private readonly Mock<IGlVoucherRepository> voucherRepository = new();
        private readonly Mock<IGlVoucherService> glVoucherService = new();
        private bool reversalCreated;
        private readonly GlVoucherHeader journal = new()
        {
            Id = 70, BranchId = 3, SourceSystemCode = "HR_HANDOFF", SourceRefId = 42, Status = 3,
            PostDate = new DateTime(2026, 11, 7, 12, 0, 0, DateTimeKind.Utc),
            Details = [new GlVoucherDetail { VoucherId = 70, LineSer = 3, BranchId = 3, CurrencyId = 9, AccountCode = "NET", Credit = 60m }]
        };
        private readonly GlVoucherHeader original = new()
        {
            Id = 71, BranchId = 3, SourceSystemCode = "HR_PAYMENT", SourceRefId = 10, Status = 3,
            VoucherType = 103, PostDate = new DateTime(2026, 11, 7, 13, 0, 0, DateTimeKind.Utc),
            TotalAmount = 60m, TotalLocalDebit = 60m, TotalLocalCredit = 60m,
            Details =
            [
                new GlVoucherDetail { VoucherId = 71, LineSer = 1, BranchId = 3, CurrencyId = 9, AccountCode = "NET", Debit = 60m, LocalDebit = 60m, BaseDebit = 60m },
                new GlVoucherDetail { VoucherId = 71, LineSer = 2, BranchId = 3, CurrencyId = 9, AccountCode = "BANK-GL", Credit = 60m, LocalCredit = 60m, BaseCredit = 60m }
            ]
        };
        private GlVoucherHeader? reversal = new()
        {
            Id = 72, BranchId = 3, SourceSystemCode = "GL_REVERSAL", SourceRefId = 71, Status = 3,
            VoucherType = 103, PostDate = new DateTime(2026, 11, 7, 14, 0, 0, DateTimeKind.Utc),
            TotalAmount = 60m, TotalLocalDebit = 60m, TotalLocalCredit = 60m,
            Details =
            [
                new GlVoucherDetail { VoucherId = 72, LineSer = 1, BranchId = 3, CurrencyId = 9, AccountCode = "NET", Credit = 60m, LocalCredit = 60m, BaseCredit = 60m },
                new GlVoucherDetail { VoucherId = 72, LineSer = 2, BranchId = 3, CurrencyId = 9, AccountCode = "BANK-GL", Debit = 60m, LocalDebit = 60m, BaseDebit = 60m }
            ]
        };

        public Mock<IGlVoucherService> Vouchers => glVoucherService;
        public Mock<IGlVoucherRepository> VoucherRepository => voucherRepository;
        public GlVoucherHeader Original => original;
        public GlVoucherHeader? CreatedReversal => reversal;
        public OracleDbContext CreateDb() => new(options);

        public void CorruptReversalAmount() => reversal!.Details[0].Credit = 59m;

        public void UseRealAccountingVoucherService()
        {
            voucherRepository.Setup(v => v.GetByIdAsync(71, It.IsAny<CancellationToken>())).ReturnsAsync(original);
            voucherRepository.Setup(v => v.GetByIdAsync(72, It.IsAny<CancellationToken>())).ReturnsAsync(() => reversal);
            voucherRepository.Setup(v => v.GetVoucherTypeByCodeAsync(103, It.IsAny<CancellationToken>()))
                .ReturnsAsync(new GlVoucherType { TypeCode = 103, TypeKey = "PAYMENT", SerialResetPolicy = "MONTHLY" });
            voucherRepository.Setup(v => v.GenerateNextSerialNoAsync(3, It.IsAny<int>(), It.IsAny<int>(), 103, "MONTHLY", It.IsAny<CancellationToken>()))
                .ReturnsAsync(7002);
            voucherRepository.Setup(v => v.AddVoucherAsync(It.IsAny<GlVoucherHeader>(), It.IsAny<CancellationToken>()))
                .Callback<GlVoucherHeader, CancellationToken>((voucher, _) => { voucher.Id = 72; reversal = voucher; reversalCreated = true; })
                .Returns(Task.CompletedTask);
            voucherRepository.Setup(v => v.SaveChangesAsync(It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        }

        public void SetupLinkedVouchers(long paymentId, params GlVoucherHeader[] list)
        {
            voucherRepository.Setup(v => v.GetBySourceAsync(3, "HR_PAYMENT", paymentId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(list);
        }

        public void SetupOriginalVoucherDraft()
        {
            original.Status = 1;
            original.PostDate = null;
        }

        public async Task SetPaymentStatusAsync(long paymentId, string status)
        {
            await using var db = CreateDb();
            var p = await db.AccountingHandoffPayments.SingleAsync(x => x.Id == paymentId);
            p.Status = status;
            await db.SaveChangesAsync();
        }

        public HrAccountingHandoffService CreateService(OracleDbContext db, bool useRealAccounting = false) => new(db, voucherRepository.Object,
            Mock.Of<IHrPayrollPostingService>(), Mock.Of<IReceiptPaymentVoucherService>(), useRealAccounting
                ? new GlVoucherService(voucherRepository.Object, Mock.Of<IGlAccountRepository>(), Mock.Of<IGlCostCenterRepository>(),
                    Mock.Of<IGlFiscalPeriodRepository>(), Mock.Of<IGlAccountBalanceRepository>(), Mock.Of<IArSubledgerRepository>(),
                    Mock.Of<IApSubledgerRepository>(), Mock.Of<ICurrentTenantContext>(), NullLogger<GlVoucherService>.Instance)
                : glVoucherService.Object);

        public async Task SeedAsync()
        {
            voucherRepository.Setup(v => v.GetByIdAsync(70, It.IsAny<CancellationToken>())).ReturnsAsync(journal);
            voucherRepository.Setup(v => v.GetByIdAsync(71, It.IsAny<CancellationToken>())).ReturnsAsync(original);
            voucherRepository.Setup(v => v.GetBySourceAsync(3, "HR_PAYMENT", 10, It.IsAny<CancellationToken>())).ReturnsAsync([original]);
            voucherRepository.Setup(v => v.GetBySourceAsync(3, "GL_REVERSAL", 71, It.IsAny<CancellationToken>()))
                .Returns(() => Task.FromResult<IReadOnlyList<GlVoucherHeader>>(reversalCreated && reversal is not null ? [reversal] : Array.Empty<GlVoucherHeader>()));
            glVoucherService.Setup(s => s.ReverseVoucherAsync(71, "accounting", "Duplicate transfer", It.IsAny<CancellationToken>()))
                .Callback(() => { original.IsReversed = true; reversalCreated = true; })
                .ReturnsAsync(new GlVoucherHeaderDto());

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
                Amount = 60m, Method = "BANK", Status = "CONFIRMED", ExternalReference = "BANK-REF-60",
                EvidenceReference = "TRANSFER-EVIDENCE-60", ConfirmedAtUtc = DateTime.UtcNow, ConfirmedBy = "accounting",
                CreatedAtUtc = DateTime.UtcNow, CreatedBy = "accounting"
            });
            await db.SaveChangesAsync();
        }

        public void Dispose() { }
    }
}
