using Microsoft.EntityFrameworkCore;
using ThinkOnErp.Domain.Entities;
using ThinkOnErp.Domain.Entities.Accounting;
using ThinkOnErp.Domain.Entities.Hr;
using ThinkOnErp.Infrastructure.Data;
using ThinkOnErp.Infrastructure.Services.Hr;
using ThinkOnErp.Infrastructure.Repositories.Hr;
using ThinkOnErp.Infrastructure.Repositories.Accounting;
using ThinkOnErp.Application.Services.Hr;
using ThinkOnErp.Application.DTOs.Hr;
using ThinkOnErp.Domain.Interfaces.Hr;
using Moq;
using ThinkOnErp.Application.Services.Accounting.Legacy;
using Xunit;

namespace ThinkOnErp.HR.Api.Tests;

public sealed class PayrollPostingConfigurationTests : IDisposable
{
    private readonly OracleDbContext db = new(new DbContextOptionsBuilder<OracleDbContext>()
        .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
    private readonly DateTime date = new(2026, 11, 30);
    private static PayrollPostingConfiguration Configuration() => new()
    {
        BranchId = 3, CurrencyId = 9, VoucherType = 4, SalaryExpense = "SALARY", EmployerSscExpense = "EMPLOYER",
        SalaryPayable = "NET", SscPayable = "SSC", TaxPayable = "TAX", DeductionsPayable = "DEDUCTIONS"
    };
    public PayrollPostingConfigurationTests()
    {
        db.Add(new SysBranch { Id = 3, CompanyId = 1, BaseCurrencyId = 9, IsActive = true });
        db.Add(new SysCurrency { Id = 9, ShortNameEn = " usd " });
        db.Add(new GlVoucherType { Id = 4, TypeCode = 4 });
        foreach (var code in new[] { "SALARY", "EMPLOYER", "NET", "SSC", "TAX", "DEDUCTIONS" })
            db.Add(new GlAccount { AccountCode = code, AccountType = "DETAIL" });
        db.Add(new SysFiscalYear { Id = 12, BranchId = 3, StartDate = new(2026, 1, 1), EndDate = new(2026, 12, 31), IsActive = true });
        db.Add(Configuration()); db.SaveChanges(); db.ChangeTracker.Clear();
    }

    [Fact]
    public async Task ResolutionUsesUniqueCatalogCodeAndOpenBranchYearWithoutChangingSource()
    {
        var context = await new HrPayrollPostingService(db).ResolveAsync(3, date);
        Assert.Equal("USD", context.CurrencyCode); Assert.Equal(9, context.Configuration.CurrencyId);
        Assert.Equal(12, context.FiscalYearId); Assert.False(db.ChangeTracker.HasChanges());
        Assert.Equal(" usd ", (await db.SysCurrencies.SingleAsync()).ShortNameEn);
    }

    [Theory] [InlineData(null)] [InlineData("")] [InlineData(" ")]
    public async Task EmptyCatalogCodeCannotResolveOrSave(string? code)
    {
        (await db.SysCurrencies.SingleAsync()).ShortNameEn = code!; await db.SaveChangesAsync(); db.ChangeTracker.Clear();
        var service = new HrPayrollPostingService(db);
        await Assert.ThrowsAsync<ArgumentException>(() => service.ResolveAsync(3, date));
        var change = Configuration(); change.SalaryExpense = "NET";
        await Assert.ThrowsAsync<ArgumentException>(() => service.SaveAsync(change));
        Assert.Equal("SALARY", (await service.GetAsync(3))!.SalaryExpense);
        Assert.False(db.ChangeTracker.HasChanges());
    }

    [Fact]
    public async Task DuplicateNormalizedCatalogCodesAreRejected()
    {
        db.Add(new SysCurrency { Id = 10, ShortNameEn = "USD" }); await db.SaveChangesAsync();
        await Assert.ThrowsAsync<ArgumentException>(() => new HrPayrollPostingService(db).ResolveAsync(3, date));
    }

    [Fact]
    public async Task CustomCompanyCodeDoesNotRequireACountryOrThreeLetters()
    {
        (await db.SysCurrencies.SingleAsync()).ShortNameEn = " client_unit "; await db.SaveChangesAsync();
        Assert.Equal("CLIENT_UNIT", (await new HrPayrollPostingService(db).ResolveAsync(3, date)).CurrencyCode);
    }

    [Fact]
    public async Task ChangedBranchBaseCurrencyCannotResolveOldConfiguration()
    {
        (await db.SysBranches.SingleAsync()).BaseCurrencyId = 10; await db.SaveChangesAsync();
        await Assert.ThrowsAsync<ArgumentException>(() => new HrPayrollPostingService(db).ResolveAsync(3, date));
    }

    [Theory] [InlineData("inactive")] [InlineData("group")] [InlineData("branch")]
    public async Task ConfiguredAccountMustRemainPostableAndAvailableToBranch(string reason)
    {
        var account = await db.GlAccounts.SingleAsync(a => a.AccountCode == "SALARY");
        if (reason == "inactive") account.IsActive = false;
        if (reason == "group") account.AccountType = "GROUP";
        if (reason == "branch") account.IsBranchSpecific = true;
        await db.SaveChangesAsync();
        await Assert.ThrowsAsync<ArgumentException>(() => new HrPayrollPostingService(db).ResolveAsync(3, date));
    }

    [Fact]
    public async Task ClosedYearCannotResolveForPosting()
    {
        (await db.SysFiscalYears.SingleAsync()).IsClosed = true; await db.SaveChangesAsync();
        await Assert.ThrowsAsync<InvalidOperationException>(() => new HrPayrollPostingService(db).ResolveAsync(3, date));
    }

    [Theory] [InlineData("SOFT_CLOSE")] [InlineData("HARD_CLOSE")] [InlineData("UNKNOWN")]
    public async Task ConfiguredNonOpenFiscalPeriodCannotBeBypassed(string status)
    {
        db.Add(new GlFiscalPeriod { Id = 1, FiscalYearId = 12, StartDate = new(2026, 11, 1), EndDate = date, Status = status });
        await db.SaveChangesAsync();
        await Assert.ThrowsAsync<InvalidOperationException>(() => new HrPayrollPostingService(db).ResolveAsync(3, date));
    }

    [Fact]
    public async Task OpenPeriodIsNotBlockedByAClosedPeriodForAnotherDateOrYear()
    {
        db.Add(new GlFiscalPeriod { Id = 1, FiscalYearId = 12, StartDate = new(2026, 11, 1), EndDate = date });
        db.Add(new GlFiscalPeriod { Id = 2, FiscalYearId = 12, StartDate = new(2026, 10, 1), EndDate = new(2026, 10, 31), Status = "HARD_CLOSE" });
        db.Add(new GlFiscalPeriod { Id = 3, FiscalYearId = 13, StartDate = new(2026, 11, 1), EndDate = date, Status = "HARD_CLOSE" });
        await db.SaveChangesAsync();
        Assert.Equal(12, (await new HrPayrollPostingService(db).ResolveAsync(3, date)).FiscalYearId);
    }

    [Fact]
    public async Task OverlappingOpenPeriodCannotHideAClosedPeriod()
    {
        db.Add(new GlFiscalPeriod { Id = 1, FiscalYearId = 12, StartDate = new(2026, 11, 1), EndDate = date });
        db.Add(new GlFiscalPeriod { Id = 2, FiscalYearId = 12, StartDate = new(2026, 11, 1), EndDate = date, Status = "HARD_CLOSE" });
        await db.SaveChangesAsync();
        await Assert.ThrowsAsync<InvalidOperationException>(() => new HrPayrollPostingService(db).ResolveAsync(3, date));
    }

    [Theory] [InlineData(12)] [InlineData(23)]
    public async Task TimeOnLastDayCannotBypassClosedCalendarPeriod(int hour)
    {
        db.Add(new GlFiscalPeriod { Id = 1, FiscalYearId = 12, StartDate = new(2026, 11, 1), EndDate = date, Status = "HARD_CLOSE" });
        await db.SaveChangesAsync();
        await Assert.ThrowsAsync<InvalidOperationException>(() => new HrPayrollPostingService(db).ResolveAsync(3, date.AddHours(hour)));
    }

    [Theory] [InlineData("MONTHLY")] [InlineData("YEARLY")] [InlineData("CONTINUOUS")]
    public async Task PostingAndReversalRespectJournalSerialPolicy(string policy)
    {
        (await db.GlVoucherTypes.SingleAsync()).SerialResetPolicy = policy; await db.SaveChangesAsync();
        var service = new HrPayrollPostingService(db);
        Assert.Equal(policy, (await service.ResolveAsync(3, date)).SerialResetPolicy);
        Assert.Equal(policy, (await service.ResolveReversalAsync(3, date, 4)).SerialResetPolicy);
    }

    [Theory] [InlineData("")] [InlineData("GUESS")]
    public async Task InvalidSerialPolicyCannotPostOrReverse(string policy)
    {
        (await db.GlVoucherTypes.SingleAsync()).SerialResetPolicy = policy; await db.SaveChangesAsync();
        var service = new HrPayrollPostingService(db);
        await Assert.ThrowsAsync<ArgumentException>(() => service.ResolveAsync(3, date));
        await Assert.ThrowsAsync<ArgumentException>(() => service.ResolveReversalAsync(3, date, 4));
    }

    [Fact]
    public async Task ReversalResolvesItsOwnYearWithoutCurrentSalaryAccountOrCurrencySettings()
    {
        db.Remove(await db.Set<PayrollPostingConfiguration>().SingleAsync());
        (await db.GlAccounts.FirstAsync()).IsActive = false;
        (await db.SysBranches.SingleAsync()).BaseCurrencyId = null;
        db.Add(new SysFiscalYear { Id = 13, BranchId = 3, StartDate = new(2027, 1, 1), EndDate = new(2027, 12, 31), IsActive = true });
        await db.SaveChangesAsync();
        var context = await new HrPayrollPostingService(db).ResolveReversalAsync(3, new DateTime(2027, 1, 5), 4);
        Assert.Equal(13, context.FiscalYearId);
    }

    [Theory] [InlineData("SOFT_CLOSE")] [InlineData("HARD_CLOSE")]
    public async Task ReversalCannotBypassClosedTargetPeriod(string status)
    {
        db.Add(new GlFiscalPeriod { Id = 1, FiscalYearId = 12, StartDate = date, EndDate = date, Status = status });
        await db.SaveChangesAsync();
        await Assert.ThrowsAsync<InvalidOperationException>(() => new HrPayrollPostingService(db).ResolveReversalAsync(3, date.AddHours(12), 4));
    }

    [Fact]
    public async Task ReversalCannotUseAnInactiveBranchOrClosedYear()
    {
        (await db.SysBranches.SingleAsync()).IsActive = false; await db.SaveChangesAsync();
        await Assert.ThrowsAsync<ArgumentException>(() => new HrPayrollPostingService(db).ResolveReversalAsync(3, date, 4));
        (await db.SysBranches.SingleAsync()).IsActive = true;
        (await db.SysFiscalYears.SingleAsync()).IsClosed = true; await db.SaveChangesAsync();
        await Assert.ThrowsAsync<InvalidOperationException>(() => new HrPayrollPostingService(db).ResolveReversalAsync(3, date, 4));
    }

    [Theory] [InlineData("MONTHLY")] [InlineData("YEARLY")] [InlineData("CONTINUOUS")]
    public async Task RealRepositoriesPersistInverseAuditRetryAndRepostingCycle(string policy)
    {
        (await db.GlVoucherTypes.SingleAsync()).SerialResetPolicy = policy;
        db.Add(new SysFiscalYear { Id = 13, BranchId = 3, StartDate = new(2027, 1, 1), EndDate = new(2027, 12, 31), IsActive = true });
        db.Add(new PayrollPeriod { Id = 1, CompanyId = 1, PeriodCode = "P26", StartDate = new(2026, 11, 1), EndDate = date });
        var run = new PayrollRun { Id = 7, BranchId = 3, PayPeriod = "P26", Status = "APPROVED", TotalGrossSalary = 100, TotalNetSalary = 100 };
        run.Lines.Add(new PayrollRunLine { BranchId = 3, EmployeeCode = "E1", CurrencyCode = "USD", GrossSalary = 100, NetPay = 100 });
        db.Add(run); await db.SaveChangesAsync(); db.ChangeTracker.Clear();
        var service = new LegacyPayrollJournalService(new PayrollPeriodRepository(db), new GlVoucherRepository(db), new HrPayrollPostingService(db));
        var posted = await service.PostPayrollToGlAsync(7, "HR"); db.ChangeTracker.Clear();
        Assert.Equal(posted.JournalVoucherId, (await service.PostPayrollToGlAsync(7, "RETRY")).JournalVoucherId);
        var request = new ReversePayrollGlDto(new DateTime(2027, 1, 5), "Correction");
        await service.ReversePayrollGlVoucherAsync(7, request, "HR"); db.ChangeTracker.Clear();
        await service.ReversePayrollGlVoucherAsync(7, request, "RETRY"); db.ChangeTracker.Clear();
        var journals = await db.GlVoucherHeaders.AsNoTracking().Include(v => v.Details).ToListAsync();
        Assert.Equal(2, journals.Count); Assert.All(journals, v => Assert.Equal(3, v.Status));
        Assert.True(journals.Single(v => v.Id == posted.JournalVoucherId).IsReversed);
        Assert.All(journals.SelectMany(v => v.Details).GroupBy(d => d.AccountCode),
            group => Assert.Equal(group.Sum(d => d.LocalDebit), group.Sum(d => d.LocalCredit)));
        Assert.Equal(posted.JournalVoucherId, (await db.PayrollRuns.AsNoTracking().SingleAsync()).JournalVoucherId);
        var reposted = await service.PostPayrollToGlAsync(7, "HR"); db.ChangeTracker.Clear();
        Assert.NotEqual(posted.JournalVoucherId, reposted.JournalVoucherId);
        Assert.Equal(3, await db.GlVoucherHeaders.CountAsync());
        if (policy == "CONTINUOUS")
        {
            Assert.Equal(3L, (await db.GlVoucherSerials.SingleAsync(s => s.SerialYear == 0 && s.SerialMonth == 0)).LastSerialNo);
        }
    }

    [Fact]
    public async Task ContinuousSerialPreservesHighWaterAcrossYearsBranchesTypesAndLegacyTrackers()
    {
        db.Add(new GlVoucherHeader { BranchId = 3, VoucherYear = 2025, VoucherMonth = 10, VoucherType = 4, VoucherNo = 40 });
        db.Add(new GlVoucherHeader { BranchId = 4, VoucherYear = 2025, VoucherMonth = 10, VoucherType = 4, VoucherNo = 900 });
        db.Add(new GlVoucherHeader { BranchId = 3, VoucherYear = 2025, VoucherMonth = 10, VoucherType = 5, VoucherNo = 800 });
        db.Add(new GlVoucherSerial { BranchId = 3, SerialYear = 2025, SerialMonth = 10, VoucherType = 4, LastSerialNo = 55 });
        await db.SaveChangesAsync(); db.ChangeTracker.Clear();
        var repository = new GlVoucherRepository(db);
        Assert.Equal(56, await repository.GenerateNextSerialNoAsync(3, 2026, 12, 4, "CONTINUOUS"));
        await db.SaveChangesAsync(); db.ChangeTracker.Clear();
        Assert.Equal(57, await repository.GenerateNextSerialNoAsync(3, 2027, 1, 4, "CONTINUOUS"));
    }

    [Theory] [InlineData("MONTHLY", 1)] [InlineData("YEARLY", 41)]
    public async Task MonthlyAndYearlySerialBoundariesRemainUnchanged(string policy, long expected)
    {
        db.Add(new GlVoucherHeader { BranchId = 3, VoucherYear = 2026, VoucherMonth = 11, VoucherType = 4, VoucherNo = 40 });
        await db.SaveChangesAsync();
        var repository = new GlVoucherRepository(db);
        Assert.Equal(expected, await repository.GenerateNextSerialNoAsync(3, 2026, 12, 4, policy));
        Assert.Equal(1, await repository.GenerateNextSerialNoAsync(3, 2027, 1, 4, policy));
    }

    [Fact]
    public async Task SourceHistoryIsRestrictedToBranchSourceAndReference()
    {
        db.Add(new GlVoucherHeader { Id = 1, BranchId = 3, SourceSystemCode = "HR_PAYROLL", SourceRefId = 7 });
        db.Add(new GlVoucherHeader { Id = 2, BranchId = 4, SourceSystemCode = "HR_PAYROLL", SourceRefId = 7 });
        db.Add(new GlVoucherHeader { Id = 3, BranchId = 3, SourceSystemCode = "HR_PAYROLL_REVERSAL", SourceRefId = 7 });
        db.Add(new GlVoucherHeader { Id = 4, BranchId = 3, SourceSystemCode = "HR_PAYROLL", SourceRefId = 8 });
        await db.SaveChangesAsync(); db.ChangeTracker.Clear();
        var history = await new GlVoucherRepository(db).GetBySourceAsync(3, "HR_PAYROLL", 7);
        Assert.Equal(1, Assert.Single(history).Id);
    }

    [Theory] [InlineData(null)] [InlineData("")] [InlineData("GUESS")]
    public async Task UnknownSerialPolicyCannotCreateOrChangeATracker(string? policy)
    {
        var repository = new GlVoucherRepository(db);
        await Assert.ThrowsAsync<ArgumentException>(() => repository.GenerateNextSerialNoAsync(3, 2026, 11, 4, policy!));
        Assert.False(db.ChangeTracker.HasChanges());
        Assert.Empty(await db.GlVoucherSerials.ToListAsync());
    }

    [Fact]
    public async Task SerialPolicyComparisonNormalizesWhitespaceAndCase()
    {
        Assert.Equal(1, await new GlVoucherRepository(db).GenerateNextSerialNoAsync(3, 2026, 11, 4, " monthly "));
    }

    [Fact]
    public async Task GetAsync_PropagatesCancellationToken()
    {
        using var cts = new CancellationTokenSource();
        cts.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => new HrPayrollPostingService(db).GetAsync(3, cts.Token));
    }

    [Fact]
    public async Task SaveAsync_PropagatesCancellationToken()
    {
        using var cts = new CancellationTokenSource();
        cts.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => new HrPayrollPostingService(db).SaveAsync(Configuration(), cts.Token));
    }

    [Fact]
    public async Task ResolveAsync_PropagatesCancellationToken()
    {
        using var cts = new CancellationTokenSource();
        cts.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => new HrPayrollPostingService(db).ResolveAsync(3, date, cts.Token));
    }

    [Fact]
    public async Task ResolveReversalAsync_PropagatesCancellationToken()
    {
        using var cts = new CancellationTokenSource();
        cts.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => new HrPayrollPostingService(db).ResolveReversalAsync(3, date, 4, cts.Token));
    }

    [Fact]
    public async Task SaveAsync_WhenBranchDoesNotExist_ThrowsArgumentException()
    {
        var cfg = Configuration();
        cfg.BranchId = 999;
        var ex = await Assert.ThrowsAsync<ArgumentException>(() => new HrPayrollPostingService(db).SaveAsync(cfg));
        Assert.Contains("Branch does not exist or is inactive", ex.Message);
    }

    [Fact]
    public async Task SaveAsync_WhenBranchIsInactive_ThrowsArgumentException()
    {
        (await db.SysBranches.SingleAsync()).IsActive = false;
        await db.SaveChangesAsync();
        var ex = await Assert.ThrowsAsync<ArgumentException>(() => new HrPayrollPostingService(db).SaveAsync(Configuration()));
        Assert.Contains("Branch does not exist or is inactive", ex.Message);
    }

    [Fact]
    public async Task SaveAsync_WhenCurrencyDoesNotExist_ThrowsArgumentException()
    {
        var cfg = Configuration();
        cfg.CurrencyId = 888;
        var ex = await Assert.ThrowsAsync<ArgumentException>(() => new HrPayrollPostingService(db).SaveAsync(cfg));
        Assert.Contains("Currency does not exist", ex.Message);
    }

    [Fact]
    public async Task SaveAsync_WhenBranchBaseCurrencyMismatch_ThrowsArgumentException()
    {
        db.Add(new SysCurrency { Id = 15, ShortNameEn = "EUR" });
        await db.SaveChangesAsync();
        var cfg = Configuration();
        cfg.CurrencyId = 15;
        var ex = await Assert.ThrowsAsync<ArgumentException>(() => new HrPayrollPostingService(db).SaveAsync(cfg));
        Assert.Contains("Payroll posting currency must match the branch base currency", ex.Message);
    }

    [Fact]
    public async Task SaveAsync_WhenVoucherTypeInactive_ThrowsArgumentException()
    {
        (await db.GlVoucherTypes.SingleAsync()).IsActive = false;
        await db.SaveChangesAsync();
        var ex = await Assert.ThrowsAsync<ArgumentException>(() => new HrPayrollPostingService(db).SaveAsync(Configuration()));
        Assert.Contains("An active journal voucher type is required", ex.Message);
    }

    [Fact]
    public async Task SaveAsync_WhenVoucherTypeNotJournalCategory_ThrowsArgumentException()
    {
        (await db.GlVoucherTypes.SingleAsync()).Category = "PAYMENT";
        await db.SaveChangesAsync();
        var ex = await Assert.ThrowsAsync<ArgumentException>(() => new HrPayrollPostingService(db).SaveAsync(Configuration()));
        Assert.Contains("An active journal voucher type is required", ex.Message);
    }

    [Fact]
    public async Task SaveAsync_WhenAccountIsControlAccount_ThrowsArgumentException()
    {
        var account = await db.GlAccounts.SingleAsync(a => a.AccountCode == "TAX");
        account.IsControlAccount = true;
        await db.SaveChangesAsync();
        var ex = await Assert.ThrowsAsync<ArgumentException>(() => new HrPayrollPostingService(db).SaveAsync(Configuration()));
        Assert.Contains("is a control account and cannot receive an aggregated payroll line", ex.Message);
    }

    [Fact]
    public async Task SaveAsync_WhenAccountIsBranchSpecific_AndNotAssignedToBranch_ThrowsArgumentException()
    {
        var account = await db.GlAccounts.SingleAsync(a => a.AccountCode == "SSC");
        account.IsBranchSpecific = true;
        await db.SaveChangesAsync();
        var ex = await Assert.ThrowsAsync<ArgumentException>(() => new HrPayrollPostingService(db).SaveAsync(Configuration()));
        Assert.Contains("is not assigned to this branch", ex.Message);
    }

    [Fact]
    public async Task SaveAsync_WhenAccountIsBranchSpecific_AndAssignedToBranch_Succeeds()
    {
        var account = await db.GlAccounts.SingleAsync(a => a.AccountCode == "SSC");
        account.IsBranchSpecific = true;
        db.Add(new GlAccountBranch { AccountCode = "SSC", BranchId = 3, IsActive = true });
        await db.SaveChangesAsync();
        var cfg = Configuration();
        await new HrPayrollPostingService(db).SaveAsync(cfg);
        var saved = await new HrPayrollPostingService(db).GetAsync(3);
        Assert.NotNull(saved);
    }

    [Fact]
    public async Task ResolveAsync_WhenNoConfigurationForBranch_ThrowsInvalidOperationException()
    {
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => new HrPayrollPostingService(db).ResolveAsync(999, date));
        Assert.Contains("Configure payroll posting accounts for this branch before posting", ex.Message);
    }

    [Fact]
    public async Task ResolveAsync_WhenDateOutsideFiscalYear_ThrowsInvalidOperationException()
    {
        var futureDate = new DateTime(2028, 5, 1);
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => new HrPayrollPostingService(db).ResolveAsync(3, futureDate));
        Assert.Contains("An open fiscal year covering the payroll posting date is required", ex.Message);
    }

    [Fact]
    public async Task ResolveReversalAsync_WhenBranchDoesNotExist_ThrowsArgumentException()
    {
        var ex = await Assert.ThrowsAsync<ArgumentException>(() => new HrPayrollPostingService(db).ResolveReversalAsync(999, date, 4));
        Assert.Contains("Branch does not exist or is inactive", ex.Message);
    }

    [Fact]
    public async Task SaveAsync_WhenExistingConfigurationPresent_UpdatesExistingRecord()
    {
        var service = new HrPayrollPostingService(db);
        var cfg = Configuration();
        cfg.SalaryExpense = "NET";
        await service.SaveAsync(cfg);
        var fetched = await service.GetAsync(3);
        Assert.NotNull(fetched);
        Assert.Equal("NET", fetched.SalaryExpense);
        Assert.Equal(1, await db.Set<PayrollPostingConfiguration>().CountAsync(c => c.BranchId == 3));
    }

    [Fact]
    public async Task GetAsync_WhenConfigurationDoesNotExist_ReturnsNull()
    {
        var service = new HrPayrollPostingService(db);
        var result = await service.GetAsync(9999);
        Assert.Null(result);
    }

    [Fact]
    public async Task SaveAsync_WhenVoucherTypeDoesNotExist_ThrowsArgumentException()
    {
        var cfg = Configuration();
        cfg.VoucherType = 9999;
        var ex = await Assert.ThrowsAsync<ArgumentException>(() => new HrPayrollPostingService(db).SaveAsync(cfg));
        Assert.Contains("An active journal voucher type is required", ex.Message);
    }

    [Fact]
    public async Task SaveAsync_WhenSerialResetPolicyUnsupported_ThrowsArgumentException()
    {
        (await db.GlVoucherTypes.SingleAsync()).SerialResetPolicy = "WEEKLY";
        await db.SaveChangesAsync();
        var ex = await Assert.ThrowsAsync<ArgumentException>(() => new HrPayrollPostingService(db).SaveAsync(Configuration()));
        Assert.Contains("Configure a supported serial reset policy for the journal voucher type", ex.Message);
    }

    [Fact]
    public async Task SaveAsync_WhenAccountNotFoundInGlAccounts_ThrowsArgumentException()
    {
        var cfg = Configuration();
        cfg.SalaryExpense = "NON_EXISTENT_ACC";
        var ex = await Assert.ThrowsAsync<ArgumentException>(() => new HrPayrollPostingService(db).SaveAsync(cfg));
        Assert.Contains("is not an active posting account", ex.Message);
    }

    [Fact]
    public async Task SaveAsync_WhenAccountIsInactive_ThrowsArgumentException()
    {
        var account = await db.GlAccounts.SingleAsync(a => a.AccountCode == "NET");
        account.IsActive = false;
        await db.SaveChangesAsync();
        var ex = await Assert.ThrowsAsync<ArgumentException>(() => new HrPayrollPostingService(db).SaveAsync(Configuration()));
        Assert.Contains("is not an active posting account", ex.Message);
    }

    [Fact]
    public async Task SaveAsync_WhenAccountTypeNotDetail_ThrowsArgumentException()
    {
        var account = await db.GlAccounts.SingleAsync(a => a.AccountCode == "SSC");
        account.AccountType = "HEADER";
        await db.SaveChangesAsync();
        var ex = await Assert.ThrowsAsync<ArgumentException>(() => new HrPayrollPostingService(db).SaveAsync(Configuration()));
        Assert.Contains("is not an active posting account", ex.Message);
    }

    [Fact]
    public async Task SaveAsync_WhenBranchBaseCurrencyNull_ThrowsArgumentException()
    {
        (await db.SysBranches.SingleAsync()).BaseCurrencyId = null;
        await db.SaveChangesAsync();
        var ex = await Assert.ThrowsAsync<ArgumentException>(() => new HrPayrollPostingService(db).SaveAsync(Configuration()));
        Assert.Contains("Payroll posting currency must match the branch base currency", ex.Message);
    }

    [Fact]
    public async Task ResolveReversalAsync_WhenVoucherTypeDoesNotExist_ThrowsArgumentException()
    {
        var ex = await Assert.ThrowsAsync<ArgumentException>(() => new HrPayrollPostingService(db).ResolveReversalAsync(3, date, 9999));
        Assert.Contains("An active journal voucher type is required", ex.Message);
    }

    [Fact]
    public async Task ResolveReversalAsync_WhenVoucherTypeNotJournalCategory_ThrowsArgumentException()
    {
        (await db.GlVoucherTypes.SingleAsync()).Category = "RECEIPT";
        await db.SaveChangesAsync();
        var ex = await Assert.ThrowsAsync<ArgumentException>(() => new HrPayrollPostingService(db).ResolveReversalAsync(3, date, 4));
        Assert.Contains("An active journal voucher type is required", ex.Message);
    }

    [Fact]
    public async Task ResolveReversalAsync_WhenFiscalPeriodClosed_ThrowsInvalidOperationException()
    {
        db.Add(new GlFiscalPeriod { Id = 1, FiscalYearId = 12, StartDate = new(2026, 11, 1), EndDate = date, Status = "CLOSED" });
        await db.SaveChangesAsync();
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => new HrPayrollPostingService(db).ResolveReversalAsync(3, date, 4));
        Assert.Contains("The ERP fiscal period is not open for payroll posting", ex.Message);
    }

    [Fact]
    public async Task ResolveAsync_WhenFiscalPeriodCoveringDateIsOpen_Succeeds()
    {
        db.Add(new GlFiscalPeriod { Id = 1, FiscalYearId = 12, StartDate = new(2026, 11, 1), EndDate = date, Status = "OPEN" });
        await db.SaveChangesAsync();
        var context = await new HrPayrollPostingService(db).ResolveAsync(3, date);
        Assert.Equal(12, context.FiscalYearId);
        Assert.Equal("USD", context.CurrencyCode);
    }

    [Fact]
    public async Task ResolveReversalAsync_WhenFiscalPeriodCoveringDateIsOpen_Succeeds()
    {
        db.Add(new GlFiscalPeriod { Id = 1, FiscalYearId = 12, StartDate = new(2026, 11, 1), EndDate = date, Status = "OPEN" });
        await db.SaveChangesAsync();
        var context = await new HrPayrollPostingService(db).ResolveReversalAsync(3, date, 4);
        Assert.Equal(12, context.FiscalYearId);
        Assert.Equal("MONTHLY", context.SerialResetPolicy);
    }

    [Fact]
    public async Task ResolveAsync_WhenNoFiscalPeriodExistsForFiscalYear_SucceedsUsingFiscalYear()
    {
        var context = await new HrPayrollPostingService(db).ResolveAsync(3, date);
        Assert.Equal(12, context.FiscalYearId);
        Assert.Equal("USD", context.CurrencyCode);
    }

    public void Dispose() => db.Dispose();
}
