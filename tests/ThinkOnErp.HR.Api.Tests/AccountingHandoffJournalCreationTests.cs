using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Moq;
using ThinkOnErp.Application.Services.Accounting;
using ThinkOnErp.Application.Services.Hr;
using ThinkOnErp.Domain.Entities.Accounting;
using ThinkOnErp.Domain.Entities;
using ThinkOnErp.Domain.Entities.Hr;
using ThinkOnErp.Domain.Interfaces.Accounting;
using ThinkOnErp.Infrastructure.Data;
using ThinkOnErp.Infrastructure.Repositories.Accounting;
using ThinkOnErp.Infrastructure.Services.Hr;
using ThinkOnErp.Infrastructure.Repositories.Hr;
using Xunit;

namespace ThinkOnErp.HR.Api.Tests;

public sealed class AccountingHandoffJournalCreationTests
{
    [Fact]
    public async Task ExistingVoucherSourceRecoversOneStableHandoffLink()
    {
        using var harness = new Harness();
        await harness.SeedAcceptedHandoffAsync();
        await using (var seed = harness.CreateDb())
        {
            seed.GlVoucherHeaders.Add(new GlVoucherHeader
            {
                Id = 70, BranchId = 3, VoucherYear = 2026, VoucherMonth = 11, VoucherType = 4,
                VoucherNo = 91, SourceSystemCode = "HR_HANDOFF", SourceRefId = 42
            });
            await seed.SaveChangesAsync();
        }

        long first;
        await using (var db = harness.CreateDb())
            first = await harness.CreateService(db).CreateJournalVoucherAsync(42, "accounting");
        await using (var db = harness.CreateDb())
            Assert.Equal(first, await harness.CreateService(db).CreateJournalVoucherAsync(42, "retry"));

        await using var verify = harness.CreateDb();
        Assert.Equal(70, first);
        Assert.Single(await verify.AccountingHandoffJournalLinks.ToListAsync());
        Assert.Equal("ACCEPTED", (await verify.HrAccountingHandoffs.SingleAsync()).DeliveryStatus);
    }

    [Fact]
    public async Task UnacceptedHandoffCannotResolvePostingOrCreateVoucher()
    {
        using var harness = new Harness();
        await harness.SeedAcceptedHandoffAsync(status: "DELIVERED");
        var posting = new Mock<IHrPayrollPostingService>();
        await using (var db = harness.CreateDb())
        {
            var service = new HrAccountingHandoffService(db, new GlVoucherRepository(db), posting.Object,
                Mock.Of<IReceiptPaymentVoucherService>(), Mock.Of<IGlVoucherService>());
            await Assert.ThrowsAsync<InvalidOperationException>(() => service.CreateJournalVoucherAsync(42, "accounting"));
        }
        posting.Verify(p => p.ResolveAsync(It.IsAny<long>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()), Times.Never);
        await using var verify = harness.CreateDb();
        Assert.Empty(await verify.GlVoucherHeaders.ToListAsync());
        Assert.Empty(await verify.AccountingHandoffJournalLinks.ToListAsync());
    }

    [Fact]
    public async Task AcceptedPayrollCreatesBalancedDraftUsingResolvedBranchAccountingConfiguration()
    {
        using var harness = new Harness();
        await harness.SeedAcceptedHandoffAsync(includePostingSetup: true);
        long first;
        await using (var db = harness.CreateDb())
            first = await harness.CreateService(db).CreateJournalVoucherAsync(42, "accounting");
        await using (var db = harness.CreateDb())
            Assert.Equal(first, await harness.CreateService(db).CreateJournalVoucherAsync(42, "retry"));

        await using var verify = harness.CreateDb();
        var voucher = await verify.GlVoucherHeaders.Include(x => x.Details).SingleAsync();
        Assert.Equal(first, voucher.Id);
        Assert.Equal("HR_HANDOFF", voucher.SourceSystemCode);
        Assert.Equal(42, voucher.SourceRefId);
        Assert.Equal(3, voucher.BranchId);
        Assert.Equal(9, voucher.Details.First().CurrencyId);
        Assert.Equal(100m, voucher.TotalAmount);
        Assert.Equal(voucher.Details.Sum(x => x.Debit), voucher.Details.Sum(x => x.Credit));
        Assert.Equal(1, voucher.Status);
        Assert.Equal("ACCEPTED", (await verify.HrAccountingHandoffs.SingleAsync()).DeliveryStatus);
        Assert.Single(await verify.AccountingHandoffJournalLinks.ToListAsync());
    }

    [Fact]
    public async Task CurrencyMismatchBetweenHandoffAndAccountingConfigThrows()
    {
        using var harness = new Harness();
        await harness.SeedAcceptedHandoffAsync(includePostingSetup: true);

        await using (var db = harness.CreateDb())
        {
            var handoff = await db.HrAccountingHandoffs.Include(h => h.Lines).SingleAsync(h => h.Id == 42);
            foreach (var line in handoff.Lines)
                line.CurrencyCode = "EUR";
            await db.SaveChangesAsync();
        }

        await using (var db = harness.CreateDb())
        {
            var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
                harness.CreateService(db).CreateJournalVoucherAsync(42, "accounting"));
            Assert.Contains("Handoff currency does not match Accounting configuration", ex.Message);
        }
    }

    [Fact]
    public async Task MixedCurrencyHandoffLinesThrows()
    {
        using var harness = new Harness();
        await harness.SeedAcceptedHandoffAsync(includePostingSetup: true);

        await using (var db = harness.CreateDb())
        {
            var handoff = await db.HrAccountingHandoffs.Include(h => h.Lines).SingleAsync(h => h.Id == 42);
            handoff.Lines.Last().CurrencyCode = "EUR";
            await db.SaveChangesAsync();
        }

        await using (var db = harness.CreateDb())
        {
            var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
                harness.CreateService(db).CreateJournalVoucherAsync(42, "accounting"));
            Assert.Contains("Mixed-currency handoff cannot be posted", ex.Message);
        }
    }

    [Fact]
    public async Task UnbalancedPayrollSnapshotThrows()
    {
        using var harness = new Harness();
        await harness.SeedAcceptedHandoffAsync(includePostingSetup: true);

        await using (var db = harness.CreateDb())
        {
            var handoff = await db.HrAccountingHandoffs.Include(h => h.Lines).SingleAsync(h => h.Id == 42);
            // Gross = 100, Net = 80, no deductions or tax: unbalance of 20
            var netLine = handoff.Lines.Single(l => l.LineType == "CONTROL:NET_PAY");
            netLine.Amount = 80m;
            await db.SaveChangesAsync();
        }

        await using (var db = harness.CreateDb())
        {
            var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
                harness.CreateService(db).CreateJournalVoucherAsync(42, "accounting"));
            Assert.Contains("Payroll snapshot does not balance to net pay", ex.Message);
        }
    }

    [Fact]
    public async Task MissingBranchThrows()
    {
        using var harness = new Harness();
        await harness.SeedAcceptedHandoffAsync(includePostingSetup: true);

        await using (var db = harness.CreateDb())
        {
            var handoff = await db.HrAccountingHandoffs.SingleAsync(h => h.Id == 42);
            handoff.BranchId = null;
            await db.SaveChangesAsync();
        }

        await using (var db = harness.CreateDb())
        {
            var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
                harness.CreateService(db).CreateJournalVoucherAsync(42, "accounting"));
            Assert.Contains("Accounting handoff has no branch", ex.Message);
        }
    }

    [Fact]
    public async Task NonExistentHandoffIdThrows()
    {
        using var harness = new Harness();
        await harness.SeedAcceptedHandoffAsync(includePostingSetup: true);

        await using (var db = harness.CreateDb())
        {
            await Assert.ThrowsAsync<KeyNotFoundException>(() =>
                harness.CreateService(db).CreateJournalVoucherAsync(999999, "accounting"));
        }
    }

    [Fact]
    public async Task FullPayrollBreakdownProducesAllBalancedJournalVoucherLines()
    {
        using var harness = new Harness();
        await harness.SeedAcceptedHandoffAsync(includePostingSetup: true);

        await using (var db = harness.CreateDb())
        {
            db.HrAccountingHandoffs.Add(new HrAccountingHandoff
            {
                Id = 43, BranchId = 3, SourceType = "PAYROLL", SourceId = "18", Operation = "SUBMIT",
                HandoffReference = "HR-43", PayloadHash = "hash-43", DeliveryStatus = "ACCEPTED",
                CreatedAtUtc = new DateTime(2026, 11, 7, 0, 0, 0, DateTimeKind.Utc), CreatedBy = "hr",
                Lines =
                [
                    // Gross: 1000, EmployerSSC: 140, EmployeeSSC: 75, IncomeTax: 50, OtherDeductions: 25, NetPay: 850
                    // Balance check: 1000 - 75 - 50 - 25 = 850 (exact match)
                    new HrAccountingHandoffLine { LineType = "CONTROL:GROSS", CurrencyCode = "USD", Amount = 1000m, BranchId = 3 },
                    new HrAccountingHandoffLine { LineType = "CONTROL:EMPLOYER_SSC", CurrencyCode = "USD", Amount = 140m, BranchId = 3 },
                    new HrAccountingHandoffLine { LineType = "CONTROL:EMPLOYEE_SSC", CurrencyCode = "USD", Amount = 75m, BranchId = 3 },
                    new HrAccountingHandoffLine { LineType = "CONTROL:INCOME_TAX", CurrencyCode = "USD", Amount = 50m, BranchId = 3 },
                    new HrAccountingHandoffLine { LineType = "CONTROL:OTHER_DEDUCTIONS", CurrencyCode = "USD", Amount = 25m, BranchId = 3 },
                    new HrAccountingHandoffLine { LineType = "CONTROL:NET_PAY", CurrencyCode = "USD", Amount = 850m, BranchId = 3 }
                ]
            });
            await db.SaveChangesAsync();
        }

        long voucherId;
        await using (var db = harness.CreateDb())
        {
            voucherId = await harness.CreateService(db).CreateJournalVoucherAsync(43, "accounting");
        }

        await using var verify = harness.CreateDb();
        var voucher = await verify.GlVoucherHeaders.Include(x => x.Details).SingleAsync(v => v.Id == voucherId);

        // Debit Total = Gross (1000) + EmployerSSC (140) = 1140
        Assert.Equal(1140m, voucher.TotalAmount);
        Assert.Equal(1140m, voucher.Details.Sum(d => d.Debit));
        Assert.Equal(1140m, voucher.Details.Sum(d => d.Credit));

        // Line 1: SalaryExpense (SALARY) debit 1000
        var line1 = voucher.Details.Single(d => d.LineSer == 1);
        Assert.Equal("SALARY", line1.AccountCode);
        Assert.Equal(1000m, line1.Debit);
        Assert.Equal(0m, line1.Credit);

        // Line 2: EmployerSscExpense (EMPLOYER) debit 140
        var line2 = voucher.Details.Single(d => d.LineSer == 2);
        Assert.Equal("EMPLOYER", line2.AccountCode);
        Assert.Equal(140m, line2.Debit);
        Assert.Equal(0m, line2.Credit);

        // Line 3: SalaryPayable (NET) credit 850
        var line3 = voucher.Details.Single(d => d.LineSer == 3);
        Assert.Equal("NET", line3.AccountCode);
        Assert.Equal(0m, line3.Debit);
        Assert.Equal(850m, line3.Credit);

        // Line 4: SscPayable (SSC) credit 75 + 140 = 215
        var line4 = voucher.Details.Single(d => d.LineSer == 4);
        Assert.Equal("SSC", line4.AccountCode);
        Assert.Equal(0m, line4.Debit);
        Assert.Equal(215m, line4.Credit);

        // Line 5: TaxPayable (TAX) credit 50
        var line5 = voucher.Details.Single(d => d.LineSer == 5);
        Assert.Equal("TAX", line5.AccountCode);
        Assert.Equal(0m, line5.Debit);
        Assert.Equal(50m, line5.Credit);

        // Line 6: DeductionsPayable (DEDUCTIONS) credit 25
        var line6 = voucher.Details.Single(d => d.LineSer == 6);
        Assert.Equal("DEDUCTIONS", line6.AccountCode);
        Assert.Equal(0m, line6.Debit);
        Assert.Equal(25m, line6.Credit);
    }

    [Fact]
    public async Task PayrollWithNationalContributionAggregatesToTaxPayable()
    {
        using var harness = new Harness();
        await harness.SeedAcceptedHandoffAsync(includePostingSetup: true);

        await using (var db = harness.CreateDb())
        {
            db.HrAccountingHandoffs.Add(new HrAccountingHandoff
            {
                Id = 44, BranchId = 3, SourceType = "PAYROLL", SourceId = "19", Operation = "SUBMIT",
                HandoffReference = "HR-44", PayloadHash = "hash-44", DeliveryStatus = "ACCEPTED",
                CreatedAtUtc = new DateTime(2026, 11, 7, 0, 0, 0, DateTimeKind.Utc), CreatedBy = "hr",
                Lines =
                [
                    // Gross: 1000, EmployerSSC: 100, EmployeeSSC: 60, IncomeTax: 40, NationalContribution: 15, OtherDeductions: 35, NetPay: 850
                    // Balance: 1000 - 60 - (40 + 15) - 35 = 1000 - 60 - 55 - 35 = 850 (exact)
                    new HrAccountingHandoffLine { LineType = "CONTROL:GROSS", CurrencyCode = "USD", Amount = 1000m, BranchId = 3 },
                    new HrAccountingHandoffLine { LineType = "CONTROL:EMPLOYER_SSC", CurrencyCode = "USD", Amount = 100m, BranchId = 3 },
                    new HrAccountingHandoffLine { LineType = "CONTROL:EMPLOYEE_SSC", CurrencyCode = "USD", Amount = 60m, BranchId = 3 },
                    new HrAccountingHandoffLine { LineType = "CONTROL:INCOME_TAX", CurrencyCode = "USD", Amount = 40m, BranchId = 3 },
                    new HrAccountingHandoffLine { LineType = "CONTROL:NATIONAL_CONTRIBUTION", CurrencyCode = "USD", Amount = 15m, BranchId = 3 },
                    new HrAccountingHandoffLine { LineType = "CONTROL:OTHER_DEDUCTIONS", CurrencyCode = "USD", Amount = 35m, BranchId = 3 },
                    new HrAccountingHandoffLine { LineType = "CONTROL:NET_PAY", CurrencyCode = "USD", Amount = 850m, BranchId = 3 }
                ]
            });
            await db.SaveChangesAsync();
        }

        long voucherId;
        await using (var db = harness.CreateDb())
        {
            voucherId = await harness.CreateService(db).CreateJournalVoucherAsync(44, "accounting");
        }

        await using var verify = harness.CreateDb();
        var voucher = await verify.GlVoucherHeaders.Include(x => x.Details).SingleAsync(v => v.Id == voucherId);

        // Line 5: TaxPayable should be 40 + 15 = 55
        var taxLine = voucher.Details.Single(d => d.LineSer == 5);
        Assert.Equal("TAX", taxLine.AccountCode);
        Assert.Equal(55m, taxLine.Credit);
        Assert.Equal(0m, taxLine.Debit);

        // Balanced debits and credits: Gross (1000) + EmployerSSC (100) = 1100
        Assert.Equal(1100m, voucher.TotalAmount);
        Assert.Equal(1100m, voucher.Details.Sum(d => d.Debit));
        Assert.Equal(1100m, voucher.Details.Sum(d => d.Credit));
    }

    [Fact]
    public async Task ZeroEmployerSscOmitsEmployerSscExpenseLine()
    {
        using var harness = new Harness();
        await harness.SeedAcceptedHandoffAsync(includePostingSetup: true);

        await using (var db = harness.CreateDb())
        {
            db.HrAccountingHandoffs.Add(new HrAccountingHandoff
            {
                Id = 45, BranchId = 3, SourceType = "PAYROLL", SourceId = "20", Operation = "SUBMIT",
                HandoffReference = "HR-45", PayloadHash = "hash-45", DeliveryStatus = "ACCEPTED",
                CreatedAtUtc = new DateTime(2026, 11, 7, 0, 0, 0, DateTimeKind.Utc), CreatedBy = "hr",
                Lines =
                [
                    // Gross: 1000, EmployerSSC: 0, EmployeeSSC: 75, IncomeTax: 50, OtherDeductions: 25, NetPay: 850
                    new HrAccountingHandoffLine { LineType = "CONTROL:GROSS", CurrencyCode = "USD", Amount = 1000m, BranchId = 3 },
                    new HrAccountingHandoffLine { LineType = "CONTROL:EMPLOYEE_SSC", CurrencyCode = "USD", Amount = 75m, BranchId = 3 },
                    new HrAccountingHandoffLine { LineType = "CONTROL:INCOME_TAX", CurrencyCode = "USD", Amount = 50m, BranchId = 3 },
                    new HrAccountingHandoffLine { LineType = "CONTROL:OTHER_DEDUCTIONS", CurrencyCode = "USD", Amount = 25m, BranchId = 3 },
                    new HrAccountingHandoffLine { LineType = "CONTROL:NET_PAY", CurrencyCode = "USD", Amount = 850m, BranchId = 3 }
                ]
            });
            await db.SaveChangesAsync();
        }

        long voucherId;
        await using (var db = harness.CreateDb())
        {
            voucherId = await harness.CreateService(db).CreateJournalVoucherAsync(45, "accounting");
        }

        await using var verify = harness.CreateDb();
        var voucher = await verify.GlVoucherHeaders.Include(x => x.Details).SingleAsync(v => v.Id == voucherId);

        // LineSer 2 (EmployerSscExpense) must be omitted since debit = 0 and credit = 0
        Assert.DoesNotContain(voucher.Details, d => d.LineSer == 2);

        // Line 4 (SSC Payable) should only credit EmployeeSSC (75)
        var sscLine = voucher.Details.Single(d => d.LineSer == 4);
        Assert.Equal("SSC", sscLine.AccountCode);
        Assert.Equal(75m, sscLine.Credit);

        // Total debits = 1000, credits = 850 + 75 + 50 + 25 = 1000
        Assert.Equal(1000m, voucher.TotalAmount);
        Assert.Equal(1000m, voucher.Details.Sum(d => d.Debit));
        Assert.Equal(1000m, voucher.Details.Sum(d => d.Credit));
    }

    [Theory]
    [InlineData("QUEUED")]
    [InlineData("DELIVERED")]
    [InlineData("POSTED")]
    [InlineData("WITHDRAWN")]
    [InlineData("REVERSED")]
    public async Task UnacceptedHandoffStatusTheory_RejectsStatusIfNotAccepted(string status)
    {
        using var harness = new Harness();
        await harness.SeedAcceptedHandoffAsync(status: status, includePostingSetup: true);

        await using var db = harness.CreateDb();
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            harness.CreateService(db).CreateJournalVoucherAsync(42, "accounting"));
        Assert.Equal("Only an Accounting-accepted handoff can create a journal voucher.", ex.Message);
    }

    [Fact]
    public async Task ExistingJournalLinkReturnsVoucherIdImmediatelyWithoutCallingPostingService()
    {
        using var harness = new Harness();
        await harness.SeedAcceptedHandoffAsync(includePostingSetup: false); // No posting config in db!

        await using (var db = harness.CreateDb())
        {
            db.AccountingHandoffJournalLinks.Add(new AccountingHandoffJournalLink
            {
                HandoffId = 42,
                VoucherId = 9999,
                CreatedAtUtc = DateTime.UtcNow,
                CreatedBy = "seed"
            });
            await db.SaveChangesAsync();
        }

        var postingMock = new Mock<IHrPayrollPostingService>();
        await using (var db = harness.CreateDb())
        {
            var service = new HrAccountingHandoffService(db, new GlVoucherRepository(db), postingMock.Object,
                Mock.Of<IReceiptPaymentVoucherService>(), Mock.Of<IGlVoucherService>());

            var voucherId = await service.CreateJournalVoucherAsync(42, "user");
            Assert.Equal(9999, voucherId);
        }

        postingMock.Verify(p => p.ResolveAsync(It.IsAny<long>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task CreateJournalVoucher_RejectsWhenBranchIsInactive()
    {
        using var harness = new Harness();
        await harness.SeedAcceptedHandoffAsync(includePostingSetup: true);

        await using (var db = harness.CreateDb())
        {
            var branch = await db.SysBranches.SingleAsync(b => b.Id == 3);
            branch.IsActive = false;
            await db.SaveChangesAsync();
        }

        await using (var db = harness.CreateDb())
        {
            var ex = await Assert.ThrowsAsync<ArgumentException>(() =>
                harness.CreateService(db).CreateJournalVoucherAsync(42, "accounting"));
            Assert.Contains("Branch does not exist or is inactive", ex.Message);
        }
    }

    [Fact]
    public async Task CreateJournalVoucher_RejectsWhenForeignCurrencyMismatchWithBranchBaseCurrency()
    {
        using var harness = new Harness();
        await harness.SeedAcceptedHandoffAsync(includePostingSetup: true);

        await using (var db = harness.CreateDb())
        {
            var branch = await db.SysBranches.SingleAsync(b => b.Id == 3);
            branch.BaseCurrencyId = 99; // Different from config.CurrencyId (9)
            await db.SaveChangesAsync();
        }

        await using (var db = harness.CreateDb())
        {
            var ex = await Assert.ThrowsAsync<ArgumentException>(() =>
                harness.CreateService(db).CreateJournalVoucherAsync(42, "accounting"));
            Assert.Contains("Payroll posting currency must match the branch base currency", ex.Message);
        }
    }

    [Fact]
    public async Task CreateJournalVoucher_RejectsWhenPostingAccountIsControlAccount()
    {
        using var harness = new Harness();
        await harness.SeedAcceptedHandoffAsync(includePostingSetup: true);

        await using (var db = harness.CreateDb())
        {
            var account = await db.GlAccounts.SingleAsync(a => a.AccountCode == "TAX");
            account.IsControlAccount = true;
            await db.SaveChangesAsync();
        }

        await using (var db = harness.CreateDb())
        {
            var ex = await Assert.ThrowsAsync<ArgumentException>(() =>
                harness.CreateService(db).CreateJournalVoucherAsync(42, "accounting"));
            Assert.Contains("is a control account and cannot receive an aggregated payroll line", ex.Message);
        }
    }

    [Theory]
    [InlineData("INACTIVE")]
    [InlineData("HEADER")]
    public async Task CreateJournalVoucher_RejectsWhenPostingAccountIsInactiveOrHeader(string scenario)
    {
        using var harness = new Harness();
        await harness.SeedAcceptedHandoffAsync(includePostingSetup: true);

        await using (var db = harness.CreateDb())
        {
            var account = await db.GlAccounts.SingleAsync(a => a.AccountCode == "SALARY");
            if (scenario == "INACTIVE")
                account.IsActive = false;
            else
                account.AccountType = "HEADER";
            await db.SaveChangesAsync();
        }

        await using (var db = harness.CreateDb())
        {
            var ex = await Assert.ThrowsAsync<ArgumentException>(() =>
                harness.CreateService(db).CreateJournalVoucherAsync(42, "accounting"));
            Assert.Contains("is not an active posting account", ex.Message);
        }
    }

    [Fact]
    public async Task CreateJournalVoucher_RejectsWhenAccountIsBranchSpecificButNotAssignedToBranch()
    {
        using var harness = new Harness();
        await harness.SeedAcceptedHandoffAsync(includePostingSetup: true);

        await using (var db = harness.CreateDb())
        {
            var account = await db.GlAccounts.SingleAsync(a => a.AccountCode == "DEDUCTIONS");
            account.IsBranchSpecific = true;
            await db.SaveChangesAsync();
        }

        await using (var db = harness.CreateDb())
        {
            var ex = await Assert.ThrowsAsync<ArgumentException>(() =>
                harness.CreateService(db).CreateJournalVoucherAsync(42, "accounting"));
            Assert.Contains("is not assigned to this branch", ex.Message);
        }
    }

    [Theory]
    [InlineData("CLOSED")]
    [InlineData("INACTIVE")]
    public async Task CreateJournalVoucher_RejectsWhenFiscalYearClosedOrInactive(string state)
    {
        using var harness = new Harness();
        await harness.SeedAcceptedHandoffAsync(includePostingSetup: true);

        await using (var db = harness.CreateDb())
        {
            var year = await db.SysFiscalYears.SingleAsync(y => y.Id == 12);
            if (state == "CLOSED")
                year.IsClosed = true;
            else
                year.IsActive = false;
            await db.SaveChangesAsync();
        }

        await using (var db = harness.CreateDb())
        {
            var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
                harness.CreateService(db).CreateJournalVoucherAsync(42, "accounting"));
            Assert.Contains("An open fiscal year covering the payroll posting date is required", ex.Message);
        }
    }

    [Fact]
    public async Task CreateJournalVoucher_RejectsWhenFiscalPeriodIsClosed()
    {
        using var harness = new Harness();
        await harness.SeedAcceptedHandoffAsync(includePostingSetup: true);

        await using (var db = harness.CreateDb())
        {
            db.GlFiscalPeriods.Add(new GlFiscalPeriod
            {
                Id = 101,
                FiscalYearId = 12,
                StartDate = new DateTime(2026, 11, 1),
                EndDate = new DateTime(2026, 11, 30),
                Status = "CLOSED"
            });
            await db.SaveChangesAsync();
        }

        await using (var db = harness.CreateDb())
        {
            var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
                harness.CreateService(db).CreateJournalVoucherAsync(42, "accounting"));
            Assert.Contains("The ERP fiscal period is not open for payroll posting", ex.Message);
        }
    }

    [Fact]
    public async Task CreateJournalVoucher_RejectsWhenVoucherTypeIsNotJournalCategory()
    {
        using var harness = new Harness();
        await harness.SeedAcceptedHandoffAsync(includePostingSetup: true);

        await using (var db = harness.CreateDb())
        {
            var vType = await db.GlVoucherTypes.SingleAsync(t => t.Id == 4);
            vType.Category = "PAYMENT";
            await db.SaveChangesAsync();
        }

        await using (var db = harness.CreateDb())
        {
            var ex = await Assert.ThrowsAsync<ArgumentException>(() =>
                harness.CreateService(db).CreateJournalVoucherAsync(42, "accounting"));
            Assert.Contains("An active journal voucher type is required", ex.Message);
        }
    }

    [Fact]
    public async Task CreateJournalVoucher_RejectsWhenSerialResetPolicyIsUnsupported()
    {
        using var harness = new Harness();
        await harness.SeedAcceptedHandoffAsync(includePostingSetup: true);

        await using (var db = harness.CreateDb())
        {
            var vType = await db.GlVoucherTypes.SingleAsync(t => t.Id == 4);
            vType.SerialResetPolicy = "DAILY";
            await db.SaveChangesAsync();
        }

        await using (var db = harness.CreateDb())
        {
            var ex = await Assert.ThrowsAsync<ArgumentException>(() =>
                harness.CreateService(db).CreateJournalVoucherAsync(42, "accounting"));
            Assert.Contains("Configure a supported serial reset policy", ex.Message);
        }
    }

    [Fact]
    public async Task CreateJournalVoucher_BranchSpecificAccountAssignedToBranch_Succeeds()
    {
        using var harness = new Harness();
        await harness.SeedAcceptedHandoffAsync(includePostingSetup: true);

        await using (var db = harness.CreateDb())
        {
            var account = await db.GlAccounts.SingleAsync(a => a.AccountCode == "SALARY");
            account.IsBranchSpecific = true;
            db.GlAccountBranches.Add(new GlAccountBranch
            {
                AccountCode = "SALARY",
                BranchId = 3,
                IsActive = true
            });
            await db.SaveChangesAsync();
        }

        long voucherId;
        await using (var db = harness.CreateDb())
        {
            voucherId = await harness.CreateService(db).CreateJournalVoucherAsync(42, "accounting");
        }
        Assert.True(voucherId > 0);
    }

    [Fact]
    public async Task CreateJournalVoucher_ReplayWhenExistingVoucherInAccounting_CreatesLinkAndReturnsId()
    {
        using var harness = new Harness();
        await harness.SeedAcceptedHandoffAsync(includePostingSetup: true);

        await using (var db = harness.CreateDb())
        {
            db.GlVoucherHeaders.Add(new GlVoucherHeader
            {
                Id = 888,
                BranchId = 3,
                VoucherYear = 2026,
                VoucherMonth = 11,
                VoucherType = 4,
                VoucherNo = 1234,
                SourceSystemCode = "HR_HANDOFF",
                SourceRefId = 42
            });
            await db.SaveChangesAsync();
        }

        await using (var db = harness.CreateDb())
        {
            var voucherId = await harness.CreateService(db).CreateJournalVoucherAsync(42, "accounting");
            Assert.Equal(888, voucherId);
        }

        await using var verify = harness.CreateDb();
        var link = await verify.AccountingHandoffJournalLinks.SingleOrDefaultAsync(l => l.HandoffId == 42);
        Assert.NotNull(link);
        Assert.Equal(888, link.VoucherId);
    }

    [Fact]
    public async Task CreateJournalVoucherAsync_PropagatesCancellationToken()
    {
        using var harness = new Harness();
        await harness.SeedAcceptedHandoffAsync(includePostingSetup: true);
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        await using var db = harness.CreateDb();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            harness.CreateService(db).CreateJournalVoucherAsync(42, "accounting", cts.Token));
    }

    [Fact]
    public async Task ConfirmJournalPostedAsync_PropagatesCancellationToken()
    {
        using var harness = new Harness();
        await harness.SeedAcceptedHandoffAsync();
        await harness.SeedPostedVoucherAsync();
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        await using var db = harness.CreateDb();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            harness.CreateService(db).ConfirmJournalPostedAsync(42, 701, "event-key-12345", "accounting", cts.Token));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("short")]
    public async Task ConfirmJournalPostedAsync_WhenEventKeyInvalid_ThrowsArgumentException(string? eventKey)
    {
        using var harness = new Harness();
        await harness.SeedAcceptedHandoffAsync();

        await using var db = harness.CreateDb();
        var ex = await Assert.ThrowsAsync<ArgumentException>(() =>
            harness.CreateService(db).ConfirmJournalPostedAsync(42, 701, eventKey!, "accounting"));
        Assert.Contains("A unique posting receipt event key is required", ex.Message);
    }

    [Fact]
    public async Task ConfirmJournalPostedAsync_WhenEventKeyExceeds160Chars_ThrowsArgumentException()
    {
        using var harness = new Harness();
        await harness.SeedAcceptedHandoffAsync();

        var longKey = new string('k', 161);
        await using var db = harness.CreateDb();
        var ex = await Assert.ThrowsAsync<ArgumentException>(() =>
            harness.CreateService(db).ConfirmJournalPostedAsync(42, 701, longKey, "accounting"));
        Assert.Contains("A unique posting receipt event key is required", ex.Message);
    }

    [Fact]
    public async Task ConfirmJournalPostedAsync_WhenHandoffNotFound_ThrowsKeyNotFoundException()
    {
        using var harness = new Harness();
        await harness.SeedAcceptedHandoffAsync();

        await using var db = harness.CreateDb();
        await Assert.ThrowsAsync<KeyNotFoundException>(() =>
            harness.CreateService(db).ConfirmJournalPostedAsync(999999, 701, "event-key-12345", "accounting"));
    }

    [Fact]
    public async Task ConfirmJournalPostedAsync_WhenVoucherNotFound_ThrowsKeyNotFoundException()
    {
        using var harness = new Harness();
        await harness.SeedAcceptedHandoffAsync();

        await using var db = harness.CreateDb();
        var ex = await Assert.ThrowsAsync<KeyNotFoundException>(() =>
            harness.CreateService(db).ConfirmJournalPostedAsync(42, 999999, "event-key-12345", "accounting"));
        Assert.Contains("Journal voucher was not found", ex.Message);
    }

    [Fact]
    public async Task ConfirmJournalPostedAsync_WhenVoucherSourceSystemCodeNotHrHandoff_ThrowsInvalidOperationException()
    {
        using var harness = new Harness();
        await harness.SeedAcceptedHandoffAsync();
        await harness.SeedPostedVoucherAsync(sourceCode: "GL_MANUAL");

        await using var db = harness.CreateDb();
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            harness.CreateService(db).ConfirmJournalPostedAsync(42, 701, "event-key-12345", "accounting"));
        Assert.Contains("Journal voucher is not linked to this handoff", ex.Message);
    }

    [Fact]
    public async Task ConfirmJournalPostedAsync_WhenVoucherSourceRefIdDoesNotMatchHandoff_ThrowsInvalidOperationException()
    {
        using var harness = new Harness();
        await harness.SeedAcceptedHandoffAsync();
        await harness.SeedPostedVoucherAsync(sourceRefId: 999);

        await using var db = harness.CreateDb();
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            harness.CreateService(db).ConfirmJournalPostedAsync(42, 701, "event-key-12345", "accounting"));
        Assert.Contains("Journal voucher is not linked to this handoff", ex.Message);
    }

    [Fact]
    public async Task ConfirmJournalPostedAsync_WhenVoucherBranchIdDoesNotMatchHandoff_ThrowsInvalidOperationException()
    {
        using var harness = new Harness();
        await harness.SeedAcceptedHandoffAsync();
        await harness.SeedPostedVoucherAsync(branchId: 99);

        await using var db = harness.CreateDb();
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            harness.CreateService(db).ConfirmJournalPostedAsync(42, 701, "event-key-12345", "accounting"));
        Assert.Contains("Journal voucher is not linked to this handoff", ex.Message);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(4)]
    public async Task ConfirmJournalPostedAsync_WhenVoucherStatusNotPosted_ThrowsInvalidOperationException(int nonPostedStatus)
    {
        using var harness = new Harness();
        await harness.SeedAcceptedHandoffAsync();
        await harness.SeedPostedVoucherAsync(status: nonPostedStatus);

        await using var db = harness.CreateDb();
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            harness.CreateService(db).ConfirmJournalPostedAsync(42, 701, "event-key-12345", "accounting"));
        Assert.Contains("Only an ERP-posted journal voucher can produce a posting receipt", ex.Message);
    }

    [Fact]
    public async Task ConfirmJournalPostedAsync_WhenVoucherStatusPostedButPostDateNull_ThrowsInvalidOperationException()
    {
        using var harness = new Harness();
        await harness.SeedAcceptedHandoffAsync();
        await harness.SeedPostedVoucherAsync(status: 3);

        await using (var db = harness.CreateDb())
        {
            var voucher = await db.GlVoucherHeaders.SingleAsync(v => v.Id == 701);
            voucher.PostDate = null;
            await db.SaveChangesAsync();
        }

        await using (var db = harness.CreateDb())
        {
            var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
                harness.CreateService(db).ConfirmJournalPostedAsync(42, 701, "event-key-12345", "accounting"));
            Assert.Contains("Only an ERP-posted journal voucher can produce a posting receipt", ex.Message);
        }
    }

    [Theory]
    [InlineData("QUEUED")]
    [InlineData("DELIVERED")]
    [InlineData("POSTED")]
    [InlineData("WITHDRAWN")]
    [InlineData("REVERSED")]
    public async Task ConfirmJournalPostedAsync_WhenHandoffDeliveryStatusNotAccepted_ThrowsInvalidOperationException(string status)
    {
        using var harness = new Harness();
        await harness.SeedAcceptedHandoffAsync(status: status);
        await harness.SeedPostedVoucherAsync();

        await using var db = harness.CreateDb();
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            harness.CreateService(db).ConfirmJournalPostedAsync(42, 701, "event-key-12345", "accounting"));
        Assert.Contains("Only an accepted handoff can receive its posting receipt", ex.Message);
    }

    [Fact]
    public async Task ConfirmJournalPostedAsync_WhenValid_TransitionsHandoffToPostedAndRecordsReceipt()
    {
        using var harness = new Harness();
        await harness.SeedAcceptedHandoffAsync();
        await harness.SeedPostedVoucherAsync();

        await using (var db = harness.CreateDb())
        {
            await harness.CreateService(db).ConfirmJournalPostedAsync(42, 701, "event-key-12345", "accounting");
        }

        await using var verify = harness.CreateDb();
        var handoff = await verify.HrAccountingHandoffs.Include(h => h.Transitions).SingleAsync(h => h.Id == 42);
        Assert.Equal("POSTED", handoff.DeliveryStatus);
        var transition = Assert.Single(handoff.Transitions.Where(t => t.ToStatus == "POSTED"));
        Assert.Equal("ACCEPTED", transition.FromStatus);
        Assert.Equal("accounting", transition.Actor);

        var receipt = await verify.AccountingHandoffPostingReceipts.SingleAsync(r => r.HandoffId == 42);
        Assert.Equal(701, receipt.VoucherId);
        Assert.Equal("event-key-12345", receipt.EventKey);
        Assert.NotNull(receipt.PayloadHash);
        Assert.Equal("accounting", receipt.ReceivedBy);
    }

    [Fact]
    public async Task ConfirmJournalPostedAsync_WhenIdempotentReplayWithSameEventKeyAndHash_SucceedsWithoutDuplicateReceipt()
    {
        using var harness = new Harness();
        await harness.SeedAcceptedHandoffAsync();
        await harness.SeedPostedVoucherAsync();

        await using (var db = harness.CreateDb())
        {
            await harness.CreateService(db).ConfirmJournalPostedAsync(42, 701, "event-key-12345", "accounting");
        }

        await using (var db = harness.CreateDb())
        {
            await harness.CreateService(db).ConfirmJournalPostedAsync(42, 701, "event-key-12345", "retry-user");
        }

        await using var verify = harness.CreateDb();
        Assert.Single(await verify.AccountingHandoffPostingReceipts.ToListAsync());
    }

    [Fact]
    public async Task ConfirmJournalPostedAsync_WhenConflictingPostingReceiptExistsForSameEventKey_ThrowsInvalidOperationException()
    {
        using var harness = new Harness();
        await harness.SeedAcceptedHandoffAsync();
        await harness.SeedPostedVoucherAsync();

        await using (var db = harness.CreateDb())
        {
            db.AccountingHandoffPostingReceipts.Add(new AccountingHandoffPostingReceipt
            {
                HandoffId = 999,
                VoucherId = 888,
                EventKey = "event-key-12345",
                PayloadHash = "hash-999",
                ReceivedAtUtc = DateTime.UtcNow,
                ReceivedBy = "accounting"
            });
            await db.SaveChangesAsync();
        }

        await using (var db = harness.CreateDb())
        {
            var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
                harness.CreateService(db).ConfirmJournalPostedAsync(42, 701, "event-key-12345", "accounting"));
            Assert.Contains("A conflicting posting receipt already exists", ex.Message);
        }
    }

    [Fact]
    public async Task ConfirmJournalPostedAsync_WhenConflictingPostingReceiptExistsForSameHandoff_ThrowsInvalidOperationException()
    {
        using var harness = new Harness();
        await harness.SeedAcceptedHandoffAsync();
        await harness.SeedPostedVoucherAsync();

        await using (var db = harness.CreateDb())
        {
            db.AccountingHandoffPostingReceipts.Add(new AccountingHandoffPostingReceipt
            {
                HandoffId = 42,
                VoucherId = 701,
                EventKey = "prior-event-key",
                PayloadHash = "prior-hash",
                ReceivedAtUtc = DateTime.UtcNow,
                ReceivedBy = "accounting"
            });
            await db.SaveChangesAsync();
        }

        await using (var db = harness.CreateDb())
        {
            var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
                harness.CreateService(db).ConfirmJournalPostedAsync(42, 701, "new-event-key", "accounting"));
            Assert.Contains("A conflicting posting receipt already exists", ex.Message);
        }
    }

    [Fact]
    public async Task CreateJournalVoucher_WhenCurrencyCaseInsensitiveMatches_Succeeds()
    {
        using var harness = new Harness();
        await harness.SeedAcceptedHandoffAsync(includePostingSetup: true);

        await using (var db = harness.CreateDb())
        {
            var currency = await db.SysCurrencies.SingleAsync(c => c.Id == 9);
            currency.ShortNameEn = "usd";
            await db.SaveChangesAsync();
        }

        long voucherId;
        await using (var db = harness.CreateDb())
        {
            voucherId = await harness.CreateService(db).CreateJournalVoucherAsync(42, "accounting");
        }
        Assert.True(voucherId > 0);
    }

    private sealed class Harness : IDisposable
    {
        private readonly DbContextOptions<OracleDbContext> options = new DbContextOptionsBuilder<OracleDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning)).Options;

        public OracleDbContext CreateDb() => new(options);

        public HrAccountingHandoffService CreateService(OracleDbContext db) => new(db,
            new GlVoucherRepository(db), new HrPayrollPostingService(db), Mock.Of<IReceiptPaymentVoucherService>(), Mock.Of<IGlVoucherService>());

        public async Task SeedAcceptedHandoffAsync(string status = "ACCEPTED", bool includePostingSetup = false)
        {
            await using var db = CreateDb();
            if (includePostingSetup)
            {
                db.Add(new SysBranch { Id = 3, CompanyId = 1, BaseCurrencyId = 9, IsActive = true });
                db.Add(new SysCurrency { Id = 9, ShortNameEn = "USD" });
                db.Add(new GlVoucherType { Id = 4, TypeCode = 4, SerialResetPolicy = "MONTHLY" });
                foreach (var code in new[] { "SALARY", "EMPLOYER", "NET", "SSC", "TAX", "DEDUCTIONS" })
                    db.Add(new GlAccount { AccountCode = code, AccountType = "DETAIL", IsActive = true });
                db.Add(new SysFiscalYear { Id = 12, BranchId = 3, StartDate = new DateTime(2026, 1, 1), EndDate = new DateTime(2026, 12, 31), IsActive = true });
                db.Add(new PayrollPostingConfiguration
                {
                    BranchId = 3, CurrencyId = 9, VoucherType = 4, SalaryExpense = "SALARY", EmployerSscExpense = "EMPLOYER",
                    SalaryPayable = "NET", SscPayable = "SSC", TaxPayable = "TAX", DeductionsPayable = "DEDUCTIONS"
                });
            }
            db.HrAccountingHandoffs.Add(new HrAccountingHandoff
            {
                Id = 42, BranchId = 3, SourceType = "PAYROLL", SourceId = "17", Operation = "SUBMIT",
                HandoffReference = "HR-42", PayloadHash = "hash", DeliveryStatus = status,
                CreatedAtUtc = new DateTime(2026, 11, 7, 0, 0, 0, DateTimeKind.Utc), CreatedBy = "hr",
                Lines =
                [
                    new HrAccountingHandoffLine { LineType = "CONTROL:GROSS", CurrencyCode = "USD", Amount = 100m, BranchId = 3 },
                    new HrAccountingHandoffLine { LineType = "CONTROL:NET_PAY", CurrencyCode = "USD", Amount = 100m, BranchId = 3 }
                ]
            });
            await db.SaveChangesAsync();
        }

        public async Task SeedPostedVoucherAsync(long voucherId = 701, long handoffId = 42, long branchId = 3, int status = 3, DateTime? postDate = null, string sourceCode = "HR_HANDOFF", long? sourceRefId = null)
        {
            await using var db = CreateDb();
            db.GlVoucherHeaders.Add(new GlVoucherHeader
            {
                Id = voucherId,
                BranchId = branchId,
                FiscalYearId = 12,
                VoucherYear = 2026,
                VoucherMonth = 11,
                VoucherType = 4,
                VoucherNo = 101,
                VoucherDate = new DateTime(2026, 11, 7),
                SourceSystemCode = sourceCode,
                SourceRefId = sourceRefId ?? handoffId,
                Status = status,
                PostDate = postDate ?? (status == 3 ? new DateTime(2026, 11, 7, 10, 0, 0, DateTimeKind.Utc) : null),
                TotalAmount = 100m,
                TotalLocalDebit = 100m,
                TotalLocalCredit = 100m
            });
            await db.SaveChangesAsync();
        }

        public void Dispose() { }
    }
}
