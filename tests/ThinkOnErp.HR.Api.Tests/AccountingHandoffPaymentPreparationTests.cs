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

public sealed class AccountingHandoffPaymentPreparationTests
{
    [Fact]
    public async Task PaymentPreparationUsesConfiguredBranchAndLeavesAccountingVoucherAsDraft()
    {
        using var harness = new Harness();
        await harness.SeedAsync(configureVoucherCreation: true);

        await using (var db = harness.CreateDb())
        {
            harness.ConfigureVoucherCreation(db);
            var payment = await harness.CreateService(db).PreparePaymentAsync(
                42, 40m, "usd", "bank", "payment-second-002", "accounting");
            Assert.Equal("PREPARED", payment.Status);
            Assert.Equal(2, payment.PaymentSequence);
            Assert.Equal(71, payment.PaymentVoucherId);
        }

        await using var verify = harness.CreateDb();
        var voucher = await verify.GlVoucherHeaders.SingleAsync(x => x.Id == 71);
        var paymentRow = await verify.AccountingHandoffPayments.SingleAsync(x => x.IdempotencyKey == "payment-second-002");
        Assert.Equal("HR_PAYMENT", voucher.SourceSystemCode);
        Assert.Equal(paymentRow.Id, voucher.SourceRefId);
        Assert.Equal(40m, voucher.TotalAmount);
        Assert.Equal("PREPARED", paymentRow.Status);
    }

    [Fact]
    public async Task PartialPaymentReservationCannotExceedPostedPayrollPayable()
    {
        using var harness = new Harness();
        await harness.SeedAsync();

        await using (var db = harness.CreateDb())
        {
            var service = harness.CreateService(db);
            await Assert.ThrowsAsync<InvalidOperationException>(() =>
                service.PreparePaymentAsync(42, 50m, "USD", "BANK", "payment-second-002", "accounting"));
        }

        await using var verify = harness.CreateDb();
        Assert.Single(await verify.AccountingHandoffPayments.ToListAsync());
        Assert.Equal(60m, (await verify.AccountingHandoffPayments.SingleAsync()).Amount);
    }

    [Fact]
    public async Task IdempotencyKeyCannotBeReusedForDifferentPaymentAmount()
    {
        using var harness = new Harness();
        await harness.SeedAsync();

        await using (var db = harness.CreateDb())
            await Assert.ThrowsAsync<InvalidOperationException>(() => harness.CreateService(db)
                .PreparePaymentAsync(42, 59m, "USD", "BANK", "payment-first-001", "accounting"));

        await using var verify = harness.CreateDb();
        var payment = await verify.AccountingHandoffPayments.SingleAsync();
        Assert.Equal(60m, payment.Amount);
        Assert.Equal("PREPARED", payment.Status);
        Assert.Equal("payment-first-001", payment.IdempotencyKey);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-10)]
    [InlineData(-0.001)]
    public async Task NonPositivePaymentAmountThrows(decimal invalidAmount)
    {
        using var harness = new Harness();
        await harness.SeedAsync();

        await using (var db = harness.CreateDb())
        {
            await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => harness.CreateService(db)
                .PreparePaymentAsync(42, invalidAmount, "USD", "BANK", "payment-unique-001", "accounting"));
        }
    }

    [Theory]
    [InlineData("", "BANK", "valid-key-001")]
    [InlineData("USD", "", "valid-key-001")]
    [InlineData("USD", "BANK", "short")] // less than 8 chars
    public async Task InvalidCurrencyOrMethodOrIdempotencyKeyThrows(string currency, string method, string key)
    {
        using var harness = new Harness();
        await harness.SeedAsync();

        await using (var db = harness.CreateDb())
        {
            var ex = await Assert.ThrowsAsync<ArgumentException>(() => harness.CreateService(db)
                .PreparePaymentAsync(42, 10m, currency, method, key, "accounting"));
            Assert.Contains("invalid", ex.Message, StringComparison.OrdinalIgnoreCase);
        }
    }

    [Theory]
    [InlineData("ACCEPTED")]
    [InlineData("QUEUED")]
    [InlineData("DELIVERED")]
    [InlineData("REVERSED")]
    public async Task UnpostedHandoffThrows(string status)
    {
        using var harness = new Harness();
        await harness.SeedAsync();

        await using (var db = harness.CreateDb())
        {
            var handoff = await db.HrAccountingHandoffs.SingleAsync(h => h.Id == 42);
            handoff.DeliveryStatus = status;
            await db.SaveChangesAsync();
        }

        await using (var db = harness.CreateDb())
        {
            var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => harness.CreateService(db)
                .PreparePaymentAsync(42, 10m, "USD", "BANK", "valid-key-002", "accounting"));
            Assert.Contains("journal posting receipt", ex.Message);
        }
    }

    [Fact]
    public async Task CurrencyMismatchWithHandoffThrows()
    {
        using var harness = new Harness();
        await harness.SeedAsync();

        await using (var db = harness.CreateDb())
        {
            var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => harness.CreateService(db)
                .PreparePaymentAsync(42, 10m, "EUR", "BANK", "valid-key-003", "accounting"));
            Assert.Contains("does not match the approved handoff currency", ex.Message);
        }
    }

    [Fact]
    public async Task MissingBranchOnHandoffThrows()
    {
        using var harness = new Harness();
        await harness.SeedAsync();

        await using (var db = harness.CreateDb())
        {
            var handoff = await db.HrAccountingHandoffs.SingleAsync(h => h.Id == 42);
            handoff.BranchId = null;
            await db.SaveChangesAsync();
        }

        await using (var db = harness.CreateDb())
        {
            var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => harness.CreateService(db)
                .PreparePaymentAsync(42, 10m, "USD", "BANK", "valid-key-004", "accounting"));
            Assert.Contains("Accounting handoff has no branch", ex.Message);
        }
    }

    [Fact]
    public async Task NonExistentHandoffThrows()
    {
        using var harness = new Harness();
        await harness.SeedAsync();

        await using (var db = harness.CreateDb())
        {
            await Assert.ThrowsAsync<KeyNotFoundException>(() => harness.CreateService(db)
                .PreparePaymentAsync(999999, 10m, "USD", "BANK", "valid-key-005", "accounting"));
        }
    }

    [Fact]
    public async Task ConsecutiveValidPartialPaymentsCannotExceedPayableTotal()
    {
        using var harness = new Harness();
        await harness.SeedAsync(configureVoucherCreation: true);

        // First payment of 60m is already in the seed. Remaining = 100 - 60 = 40m.
        await using (var db = harness.CreateDb())
        {
            harness.ConfigureVoucherCreation(db);
            var second = await harness.CreateService(db).PreparePaymentAsync(
                42, 40m, "USD", "BANK", "payment-second-002", "accounting");
            Assert.Equal("PREPARED", second.Status);
            Assert.Equal(40m, second.Amount);
        }

        // Third payment of even 0.01m exceeds the total
        await using (var db = harness.CreateDb())
        {
            var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => harness.CreateService(db)
                .PreparePaymentAsync(42, 0.01m, "USD", "BANK", "payment-third-003", "accounting"));
            Assert.Contains("exceeds the remaining payable amount", ex.Message);
        }
    }

    [Fact]
    public async Task IdempotentReplayReturnsExistingPaymentWithoutModifyingDatabase()
    {
        using var harness = new Harness();
        await harness.SeedAsync();

        await using (var db = harness.CreateDb())
        {
            harness.SetupLinkedVoucher(10L, 71L);
            var payment = await harness.CreateService(db)
                .PreparePaymentAsync(42, 60m, "USD", "BANK", "payment-first-001", "accounting");
            Assert.Equal("PREPARED", payment.Status);
            Assert.Equal(1, payment.PaymentSequence);
            Assert.Equal(60m, payment.Amount);
        }

        await using var verify = harness.CreateDb();
        var allPayments = await verify.AccountingHandoffPayments.ToListAsync();
        Assert.Single(allPayments);
        Assert.Equal("payment-first-001", allPayments[0].IdempotencyKey);
    }

    [Fact]
    public async Task PaymentPreparation_RejectsInactivePaymentMethod()
    {
        using var harness = new Harness();
        await harness.SeedAsync();

        await using (var db = harness.CreateDb())
        {
            var method = await db.PaymentMethods.SingleAsync(m => m.Code == "BANK");
            method.IsActive = false;
            await db.SaveChangesAsync();
        }

        await using (var db = harness.CreateDb())
        {
            var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => harness.CreateService(db)
                .PreparePaymentAsync(42, 20m, "USD", "BANK", "payment-inactive-001", "accounting"));
            Assert.Contains("No active Accounting payment method matches", ex.Message);
        }
    }

    [Fact]
    public async Task PaymentPreparation_RejectsPaymentMethodNotEnabledForVouchers()
    {
        using var harness = new Harness();
        await harness.SeedAsync();

        await using (var db = harness.CreateDb())
        {
            var method = await db.PaymentMethods.SingleAsync(m => m.Code == "BANK");
            method.ShowInVouchers = false;
            await db.SaveChangesAsync();
        }

        await using (var db = harness.CreateDb())
        {
            var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => harness.CreateService(db)
                .PreparePaymentAsync(42, 20m, "USD", "BANK", "payment-novoucher-001", "accounting"));
            Assert.Contains("No active Accounting payment method matches", ex.Message);
        }
    }

    [Fact]
    public async Task PaymentPreparation_RejectsUnknownPaymentMethodCode()
    {
        using var harness = new Harness();
        await harness.SeedAsync();

        await using (var db = harness.CreateDb())
        {
            var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => harness.CreateService(db)
                .PreparePaymentAsync(42, 20m, "USD", "CRYPTO_TOKEN", "payment-crypto-001", "accounting"));
            Assert.Contains("Select an active Accounting payment method", ex.Message);
        }
    }

    [Fact]
    public async Task PaymentPreparation_WhenPreviousPaymentIsReversed_AllowsRemainingReallocation()
    {
        using var harness = new Harness();
        await harness.SeedAsync(configureVoucherCreation: true);

        // Mark the seeded payment of 60m as REVERSED
        await using (var db = harness.CreateDb())
        {
            var prior = await db.AccountingHandoffPayments.SingleAsync(p => p.IdempotencyKey == "payment-first-001");
            prior.Status = "REVERSED";
            prior.ReversalReason = "Prior reversed payment";
            prior.ReversalExternalReference = "REF-REV-01";
            prior.ReversalEvidenceReference = "EVID-REV-01";
            await db.SaveChangesAsync();
        }

        // Full 100m payable is now available again
        await using (var db = harness.CreateDb())
        {
            harness.ConfigureVoucherCreation(db);
            var fullPayment = await harness.CreateService(db).PreparePaymentAsync(
                42, 100m, "USD", "BANK", "payment-full-reallocated-001", "accounting");
            Assert.Equal("PREPARED", fullPayment.Status);
            Assert.Equal(100m, fullPayment.Amount);
            Assert.Equal(2, fullPayment.PaymentSequence);
        }

        await using var verify = harness.CreateDb();
        var payments = await verify.AccountingHandoffPayments.OrderBy(p => p.PaymentSequence).ToListAsync();
        Assert.Equal(2, payments.Count);
        Assert.Equal("REVERSED", payments[0].Status);
        Assert.Equal("PREPARED", payments[1].Status);
        Assert.Equal(100m, payments[1].Amount);
    }

    [Fact]
    public async Task PreparePaymentAsync_PropagatesCancellationToken()
    {
        using var harness = new Harness();
        await harness.SeedAsync();

        using var cts = new CancellationTokenSource();
        cts.Cancel();

        await using var db = harness.CreateDb();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => harness.CreateService(db)
            .PreparePaymentAsync(42, 10m, "USD", "BANK", "cancelled-key-01", "accounting", cts.Token));
    }

    [Theory]
    [InlineData(100_000_000_000_000)]
    [InlineData(999_999_999_999_999.9999)]
    public async Task PaymentPreparation_AmountExceedingMaximumAllowed_ThrowsArgumentOutOfRangeException(decimal excessiveAmount)
    {
        using var harness = new Harness();
        await harness.SeedAsync();

        await using var db = harness.CreateDb();
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => harness.CreateService(db)
            .PreparePaymentAsync(42, excessiveAmount, "USD", "BANK", "payment-excess-001", "accounting"));
    }

    [Theory]
    [InlineData("CURRENCY_NAME_EXCEEDING_20_CHARS", "BANK", "valid-key-001")]
    [InlineData("USD", "METHOD_TYPE_EXCEEDING_FIFTY_CHARACTERS_LONG_WHICH_IS_TOO_LONG", "valid-key-001")]
    [InlineData("USD", "BANK", "A_VERY_LONG_IDEMPOTENCY_KEY_THAT_EXCEEDS_ONE_HUNDRED_AND_SIXTY_CHARACTERS_WHICH_IS_PROHIBITED_BY_THE_SPECIFICATION_GUARD_AND_MUST_THROW_AN_ARGUMENT_EXCEPTION_EVERY_TIME_0123456789")]
    public async Task PaymentPreparation_OversizedParameters_ThrowsArgumentException(string currency, string method, string key)
    {
        using var harness = new Harness();
        await harness.SeedAsync();

        await using var db = harness.CreateDb();
        var ex = await Assert.ThrowsAsync<ArgumentException>(() => harness.CreateService(db)
            .PreparePaymentAsync(42, 10m, currency, method, key, "accounting"));
        Assert.Contains("invalid", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task PaymentPreparation_RejectsAmbiguousPaymentMethodWhenMultipleExist()
    {
        using var harness = new Harness();
        await harness.SeedAsync();

        await using (var db = harness.CreateDb())
        {
            var existing = await db.PaymentMethods.SingleAsync(m => m.Code == "BANK");
            existing.Code = "BANK-01";

            db.PaymentMethods.Add(new PaymentMethod
            {
                Id = 6, BranchId = 3, Code = "BANK-02", MethodType = "BANK", GlAccountCode = "BANK-GL-2",
                IsActive = true, ShowInVouchers = true
            });
            await db.SaveChangesAsync();
        }

        await using (var db = harness.CreateDb())
        {
            var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => harness.CreateService(db)
                .PreparePaymentAsync(42, 10m, "USD", "BANK", "payment-ambiguous-001", "accounting"));
            Assert.Contains("More than one Accounting payment method matches", ex.Message);
        }
    }

    [Fact]
    public async Task PaymentPreparation_ResolvesPaymentMethodBySpecificCode()
    {
        using var harness = new Harness();
        await harness.SeedAsync(configureVoucherCreation: true);

        await using (var db = harness.CreateDb())
        {
            db.PaymentMethods.Add(new PaymentMethod
            {
                Id = 6, BranchId = 3, Code = "BANK-02", MethodType = "BANK", GlAccountCode = "BANK-GL-2",
                IsActive = true, ShowInVouchers = true
            });
            await db.SaveChangesAsync();
        }

        await using (var db = harness.CreateDb())
        {
            harness.ConfigureVoucherCreation(db);
            var payment = await harness.CreateService(db)
                .PreparePaymentAsync(42, 20m, "USD", "BANK-02", "payment-specific-code-001", "accounting");
            Assert.Equal("PREPARED", payment.Status);
            Assert.Equal("BANK-02", payment.PaymentMethodCode);
        }
    }

    [Theory]
    [InlineData("INACTIVE")]
    [InlineData("DIFFERENT_BRANCH")]
    [InlineData("DIFFERENT_CURRENCY")]
    [InlineData("DIFFERENT_GL")]
    public async Task PaymentPreparation_RejectsMismatchedOrInactiveBankAccount(string mismatchKind)
    {
        using var harness = new Harness();
        await harness.SeedAsync(configureVoucherCreation: true);

        await using (var db = harness.CreateDb())
        {
            var method = await db.PaymentMethods.SingleAsync(m => m.Code == "BANK");
            method.BankAccountId = 99;

            var bankAccount = new BankAccount
            {
                Id = 99,
                BranchId = mismatchKind == "DIFFERENT_BRANCH" ? 999 : 3,
                IsActive = mismatchKind != "INACTIVE",
                CurrencyId = mismatchKind == "DIFFERENT_CURRENCY" ? 888 : 9,
                GlAccountCode = mismatchKind == "DIFFERENT_GL" ? "OTHER-GL" : "BANK-GL"
            };
            db.BankAccounts.Add(bankAccount);
            await db.SaveChangesAsync();
        }

        await using (var db = harness.CreateDb())
        {
            harness.ConfigureVoucherCreation(db);
            var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => harness.CreateService(db)
                .PreparePaymentAsync(42, 20m, "USD", "BANK", "payment-bank-mismatch-001", "accounting"));
            Assert.Contains("bank account is inactive, mismatched", ex.Message);
        }
    }

    [Fact]
    public async Task PaymentPreparation_WhenBankAccountMatchesAllCriteria_Succeeds()
    {
        using var harness = new Harness();
        await harness.SeedAsync(configureVoucherCreation: true);

        await using (var db = harness.CreateDb())
        {
            var method = await db.PaymentMethods.SingleAsync(m => m.Code == "BANK");
            method.BankAccountId = 15;
            db.BankAccounts.Add(new BankAccount
            {
                Id = 15, BranchId = 3, IsActive = true, CurrencyId = 9, GlAccountCode = "BANK-GL"
            });
            await db.SaveChangesAsync();
        }

        await using (var db = harness.CreateDb())
        {
            harness.ConfigureVoucherCreation(db);
            var payment = await harness.CreateService(db)
                .PreparePaymentAsync(42, 20m, "USD", "BANK", "payment-bank-valid-001", "accounting");
            Assert.Equal("PREPARED", payment.Status);
            Assert.Equal(71, payment.PaymentVoucherId);
        }
    }

    [Theory]
    [InlineData("INACTIVE")]
    [InlineData("DIFFERENT_BRANCH")]
    [InlineData("DIFFERENT_CURRENCY")]
    [InlineData("DIFFERENT_GL")]
    public async Task PaymentPreparation_RejectsMismatchedOrInactiveCashRegister(string mismatchKind)
    {
        using var harness = new Harness();
        await harness.SeedAsync(configureVoucherCreation: true);

        await using (var db = harness.CreateDb())
        {
            db.PaymentMethods.Add(new PaymentMethod
            {
                Id = 20, BranchId = 3, Code = "CASH-MAIN", MethodType = "CASH", GlAccountCode = "CASH-GL",
                CashRegisterId = 88, IsActive = true, ShowInVouchers = true
            });
            db.CashRegisters.Add(new CashRegister
            {
                Id = 88,
                BranchId = mismatchKind == "DIFFERENT_BRANCH" ? 999 : 3,
                IsActive = mismatchKind != "INACTIVE",
                CurrencyId = mismatchKind == "DIFFERENT_CURRENCY" ? 888 : 9,
                GlAccountCode = mismatchKind == "DIFFERENT_GL" ? "OTHER-GL" : "CASH-GL"
            });
            await db.SaveChangesAsync();
        }

        await using (var db = harness.CreateDb())
        {
            harness.ConfigureVoucherCreation(db);
            var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => harness.CreateService(db)
                .PreparePaymentAsync(42, 20m, "USD", "CASH-MAIN", "payment-cash-mismatch-001", "accounting"));
            Assert.Contains("cash register is inactive, mismatched", ex.Message);
        }
    }

    [Fact]
    public async Task PaymentPreparation_WhenCashRegisterMatchesAllCriteria_Succeeds()
    {
        using var harness = new Harness();
        await harness.SeedAsync(configureVoucherCreation: true);

        await using (var db = harness.CreateDb())
        {
            db.PaymentMethods.Add(new PaymentMethod
            {
                Id = 20, BranchId = 3, Code = "CASH-MAIN", MethodType = "CASH", GlAccountCode = "CASH-GL",
                CashRegisterId = 22, IsActive = true, ShowInVouchers = true
            });
            db.CashRegisters.Add(new CashRegister
            {
                Id = 22, BranchId = 3, IsActive = true, CurrencyId = 9, GlAccountCode = "CASH-GL"
            });
            await db.SaveChangesAsync();
        }

        await using (var db = harness.CreateDb())
        {
            harness.ConfigureVoucherCreation(db);
            var payment = await harness.CreateService(db)
                .PreparePaymentAsync(42, 20m, "USD", "CASH-MAIN", "payment-cash-valid-001", "accounting");
            Assert.Equal("PREPARED", payment.Status);
            Assert.Equal("CASH", payment.Method);
            Assert.Equal(71, payment.PaymentVoucherId);
        }
    }

    [Fact]
    public async Task PaymentPreparation_RejectsMixedCurrencyHandoffLines()
    {
        using var harness = new Harness();
        await harness.SeedAsync();

        await using (var db = harness.CreateDb())
        {
            var handoff = await db.HrAccountingHandoffs.Include(h => h.Lines).SingleAsync(h => h.Id == 42);
            handoff.Lines.Add(new HrAccountingHandoffLine { LineType = "CONTROL:NET_PAY", CurrencyCode = "EUR", Amount = 50m, BranchId = 3 });
            await db.SaveChangesAsync();
        }

        await using (var db = harness.CreateDb())
        {
            var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => harness.CreateService(db)
                .PreparePaymentAsync(42, 10m, "USD", "BANK", "payment-mixed-curr-001", "accounting"));
            Assert.Contains("Payment currency does not match the approved handoff currency", ex.Message);
        }
    }

    [Fact]
    public async Task PaymentPreparation_RejectsAccountingBaseCurrencyMismatch()
    {
        using var harness = new Harness();
        await harness.SeedAsync(configureVoucherCreation: true);

        await using (var db = harness.CreateDb())
        {
            harness.ConfigureVoucherCreation(db);
            var config = new PayrollPostingConfiguration
            {
                BranchId = 3, CurrencyId = 999, VoucherType = 4, SalaryExpense = "SALARY", EmployerSscExpense = "EMPLOYER",
                SalaryPayable = "NET", SscPayable = "SSC", TaxPayable = "TAX", DeductionsPayable = "DEDUCTIONS"
            };
            harness.posting.Setup(p => p.ResolveAsync(3, It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new PayrollPostingContext(config, 12, "EUR", "MONTHLY"));

            var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => harness.CreateService(db)
                .PreparePaymentAsync(42, 20m, "USD", "BANK", "payment-base-mismatch-001", "accounting"));
            Assert.Contains("Payment currency does not match the configured Accounting base currency", ex.Message);
        }
    }

    [Fact]
    public async Task PaymentPreparation_RejectsMultipleLinkedPaymentVouchersOnEnsure()
    {
        using var harness = new Harness();
        await harness.SeedAsync(configureVoucherCreation: true);

        await using (var db = harness.CreateDb())
        {
            harness.ConfigureVoucherCreation(db);
            harness.SetupMultipleLinkedVouchers(11L, 71L, 72L);

            var payment11 = new AccountingHandoffPayment
            {
                Id = 11, HandoffId = 42, PaymentSequence = 2, IdempotencyKey = "payment-dup-vouchers-001",
                CurrencyCode = "USD", Amount = 20m, Method = "BANK", PaymentMethodCode = "BANK",
                Status = "PREPARED", CreatedAtUtc = DateTime.UtcNow, CreatedBy = "accounting"
            };
            db.AccountingHandoffPayments.Add(payment11);
            await db.SaveChangesAsync();
        }

        await using (var db = harness.CreateDb())
        {
            var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => harness.CreateService(db)
                .PreparePaymentAsync(42, 20m, "USD", "BANK", "payment-dup-vouchers-001", "accounting"));
            Assert.Contains("More than one Payment Voucher is linked to this payment instruction", ex.Message);
        }
    }

    [Theory]
    [InlineData("EUR", "BANK")]
    [InlineData("USD", "CASH")]
    public async Task PaymentPreparation_IdempotentReplayRejectsChangedCurrencyOrMethod(string changedCurrency, string changedMethod)
    {
        using var harness = new Harness();
        await harness.SeedAsync();

        await using (var db = harness.CreateDb())
        {
            db.PaymentMethods.Add(new PaymentMethod
            {
                Id = 21, BranchId = 3, Code = "CASH", MethodType = "CASH", GlAccountCode = "CASH-GL",
                IsActive = true, ShowInVouchers = true
            });
            await db.SaveChangesAsync();
        }

        await using (var db = harness.CreateDb())
        {
            var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => harness.CreateService(db)
                .PreparePaymentAsync(42, 60m, changedCurrency, changedMethod, "payment-first-001", "accounting"));
            Assert.Contains("Idempotency key was reused for a different payment instruction", ex.Message);
        }
    }

    [Fact]
    public async Task PaymentPreparation_IdempotentReplayForConfirmedPayment_ReturnsPriorWithVoucher()
    {
        using var harness = new Harness();
        await harness.SeedAsync();

        await using (var db = harness.CreateDb())
        {
            var prior = await db.AccountingHandoffPayments.SingleAsync(p => p.IdempotencyKey == "payment-first-001");
            prior.Status = "CONFIRMED";
            prior.ConfirmedAtUtc = DateTime.UtcNow;
            prior.ConfirmedBy = "accounting";
            await db.SaveChangesAsync();
        }

        await using (var db = harness.CreateDb())
        {
            harness.SetupLinkedVoucher(10L, 71L);
            var replayed = await harness.CreateService(db)
                .PreparePaymentAsync(42, 60m, "USD", "BANK", "payment-first-001", "accounting");
            Assert.Equal("CONFIRMED", replayed.Status);
            Assert.Equal(71L, replayed.PaymentVoucherId);
        }
    }

    [Fact]
    public async Task PaymentPreparation_IdempotentReplayForConfirmedPaymentWithMultipleVouchers_Throws()
    {
        using var harness = new Harness();
        await harness.SeedAsync();

        await using (var db = harness.CreateDb())
        {
            var prior = await db.AccountingHandoffPayments.SingleAsync(p => p.IdempotencyKey == "payment-first-001");
            prior.Status = "CONFIRMED";
            await db.SaveChangesAsync();
        }

        await using (var db = harness.CreateDb())
        {
            harness.SetupMultipleLinkedVouchers(10L, 71L, 72L);
            var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => harness.CreateService(db)
                .PreparePaymentAsync(42, 60m, "USD", "BANK", "payment-first-001", "accounting"));
            Assert.Contains("More than one Payment Voucher is linked to this payment instruction", ex.Message);
        }
    }

    private sealed class Harness : IDisposable
    {
        private readonly DbContextOptions<OracleDbContext> options = new DbContextOptionsBuilder<OracleDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning)).Options;

        public readonly Mock<IGlVoucherRepository> vouchers = new();
        public readonly Mock<IHrPayrollPostingService> posting = new();
        public readonly Mock<IReceiptPaymentVoucherService> paymentVouchers = new();

        public OracleDbContext CreateDb() => new(options);

        public HrAccountingHandoffService CreateService(OracleDbContext db) => new(db,
            vouchers.Object, posting.Object, paymentVouchers.Object, Mock.Of<IGlVoucherService>());

        public void ConfigureVoucherCreation(OracleDbContext db)
        {
            var configuration = new PayrollPostingConfiguration
            {
                BranchId = 3, CurrencyId = 9, VoucherType = 4, SalaryExpense = "SALARY", EmployerSscExpense = "EMPLOYER",
                SalaryPayable = "NET", SscPayable = "SSC", TaxPayable = "TAX", DeductionsPayable = "DEDUCTIONS"
            };
            posting.Setup(p => p.ResolveAsync(3, It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new PayrollPostingContext(configuration, 12, "USD", "MONTHLY"));
            vouchers.Setup(v => v.GetByIdAsync(70, It.IsAny<CancellationToken>())).ReturnsAsync(new GlVoucherHeader
            {
                Id = 70, BranchId = 3, SourceSystemCode = "HR_HANDOFF", SourceRefId = 42, Status = 3,
                PostDate = DateTime.UtcNow,
                Details = [new GlVoucherDetail { VoucherId = 70, LineSer = 3, BranchId = 3, CurrencyId = 9, AccountCode = "NET", Credit = 100m }]
            });
            vouchers.Setup(v => v.GetBySourceAsync(3, "HR_PAYMENT", It.IsAny<long>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(Array.Empty<GlVoucherHeader>());
            paymentVouchers.Setup(p => p.CreatePaymentVoucherAsync(It.IsAny<CreatePaymentVoucherDto>(), "accounting", It.IsAny<CancellationToken>()))
                .Returns(async (CreatePaymentVoucherDto dto, string user, CancellationToken cancellationToken) =>
                {
                    db.GlVoucherHeaders.Add(new GlVoucherHeader
                    {
                        Id = 71, BranchId = dto.BranchId, FiscalYearId = dto.FiscalYearId,
                        VoucherYear = dto.VoucherDate.Year, VoucherMonth = dto.VoucherDate.Month, VoucherType = 103,
                        VoucherNo = 1, VoucherDate = dto.VoucherDate, TotalAmount = dto.TotalAmount,
                        TotalLocalDebit = dto.TotalAmount, TotalLocalCredit = dto.TotalAmount, Status = dto.AutoPost ? 3 : 1,
                        CreationUser = user, CreationDate = DateTime.UtcNow
                    });
                    await db.SaveChangesAsync(cancellationToken);
                    return new PaymentVoucherDto { VoucherId = 71 };
                });
        }

        public void SetupLinkedVoucher(long paymentId, long voucherId)
        {
            vouchers.Setup(v => v.GetBySourceAsync(3, "HR_PAYMENT", paymentId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(new[] { new GlVoucherHeader { Id = voucherId, BranchId = 3, SourceSystemCode = "HR_PAYMENT", SourceRefId = paymentId } });
        }

        public void SetupMultipleLinkedVouchers(long paymentId, params long[] voucherIds)
        {
            vouchers.Setup(v => v.GetBySourceAsync(3, "HR_PAYMENT", paymentId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(voucherIds.Select(id => new GlVoucherHeader { Id = id, BranchId = 3, SourceSystemCode = "HR_PAYMENT", SourceRefId = paymentId }).ToArray());
        }

        public async Task SeedAsync(bool configureVoucherCreation = false)
        {
            await using var db = CreateDb();
            if (configureVoucherCreation)
            {
                db.SysCurrencies.Add(new SysCurrency { Id = 9, ShortNameEn = "USD" });
                db.AccountingHandoffJournalLinks.Add(new AccountingHandoffJournalLink
                { Id = 7, HandoffId = 42, VoucherId = 70, CreatedAtUtc = DateTime.UtcNow, CreatedBy = "accounting" });
            }
            db.PaymentMethods.Add(new PaymentMethod
            {
                Id = 5, BranchId = 3, Code = "BANK", MethodType = "BANK", GlAccountCode = "BANK-GL",
                IsActive = true, ShowInVouchers = true
            });
            db.HrAccountingHandoffs.Add(new HrAccountingHandoff
            {
                Id = 42, CompanyId = 1, BranchId = 3, SourceType = "PAYROLL", SourceId = "17", SourceVersion = 1,
                Operation = "SUBMIT", HandoffReference = "HR-42", PayloadHash = "hash", DeliveryStatus = "POSTED",
                CreatedAtUtc = DateTime.UtcNow, CreatedBy = "hr",
                Lines = [new HrAccountingHandoffLine { LineType = "CONTROL:NET_PAY", CurrencyCode = "USD", Amount = 100m, BranchId = 3 }]
            });
            db.AccountingHandoffPayments.Add(new AccountingHandoffPayment
            {
                Id = 10, HandoffId = 42, PaymentSequence = 1, IdempotencyKey = "payment-first-001", CurrencyCode = "USD",
                Amount = 60m, Method = "BANK", PaymentMethodCode = "BANK", Status = "PREPARED",
                CreatedAtUtc = DateTime.UtcNow, CreatedBy = "accounting"
            });
            await db.SaveChangesAsync();
        }

        public void Dispose() { }
    }
}
