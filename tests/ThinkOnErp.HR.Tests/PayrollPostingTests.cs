using Moq;
using ThinkOnErp.Application.Services.Accounting.Legacy;
using ThinkOnErp.Application.Services.Hr;
using ThinkOnErp.Domain.Entities.Accounting;
using ThinkOnErp.Domain.Entities.Hr;
using ThinkOnErp.Domain.Interfaces.Accounting;
using ThinkOnErp.Domain.Interfaces.Hr;
using Xunit;

namespace ThinkOnErp.HR.Tests;

public class PayrollPostingTests
{
    internal sealed class Fixture
    {
        public PayrollRun Run { get; } = new()
        {
            Id = 7, BranchId = 3, PayPeriod = "2026-11", Status = "APPROVED",
            TotalGrossSalary = 1000m, TotalNetSalary = 790m, TotalEmployeeSsc = 70m,
            TotalEmployerSsc = 140m, TotalIncomeTax = 60m, TotalNationalContrib = 30m, TotalOtherDeductions = 50m
        };
        public Mock<IPayrollPeriodRepository> Repository { get; } = new();
        public Mock<IGlVoucherRepository> Gl { get; } = new();
        public Mock<IHrPayrollPostingService> Posting { get; } = new();
        public List<GlVoucherHeader> Journals { get; } = new();
        public LegacyPayrollJournalService Service { get; }
        public GlVoucherHeader? Voucher { get; private set; }
        public Fixture(string postingCurrency = "USD", IHrUnitOfWork? unitOfWork = null, string serialPolicy = "YEARLY")
        {
            Run.Lines.Add(new PayrollRunLine { EmployeeCode = "E1", BranchId = 3, CurrencyCode = "USD",
                GrossSalary = 1000m, NetPay = 790m, SscEmployeeContrib = 70m, SscEmployerContrib = 140m,
                IncomeTaxWithheld = 60m, NationalContribWithheld = 30m, OtherDeductions = 50m });
            Repository.Setup(r => r.GetPayrollRunByIdAsync(7, It.IsAny<CancellationToken>())).ReturnsAsync(Run);
            Repository.Setup(r => r.GetPeriodForRunAsync(Run, It.IsAny<CancellationToken>())).ReturnsAsync(new PayrollPeriod
            { CompanyId = 1, PeriodCode = Run.PayPeriod, EndDate = new(2026, 11, 30), PayDate = new(2026, 12, 2) });
            var posting = Posting;
            posting.Setup(p => p.ResolveAsync(3, new DateTime(2026, 12, 2), It.IsAny<CancellationToken>())).ReturnsAsync(
                new PayrollPostingContext(new PayrollPostingConfiguration { BranchId = 3, CurrencyId = 9, VoucherType = 4,
                    SalaryExpense = "SALARY", EmployerSscExpense = "EMPLOYER", SalaryPayable = "NET",
                    SscPayable = "SSC", TaxPayable = "TAX", DeductionsPayable = "DEDUCTIONS" }, 12, postingCurrency, serialPolicy));
            posting.Setup(p => p.ResolveReversalAsync(3, It.IsAny<DateTime>(), 4, It.IsAny<CancellationToken>()))
                .ReturnsAsync(new PayrollReversalContext(13, serialPolicy));
            Gl.Setup(g => g.GetBySourceAsync(It.IsAny<long>(), It.IsAny<string>(), It.IsAny<long>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((long branch, string source, long reference, CancellationToken _) =>
                    (IReadOnlyList<GlVoucherHeader>)Journals.Where(v => v.BranchId == branch && v.SourceSystemCode == source && v.SourceRefId == reference).ToArray());
            Gl.Setup(g => g.GetByIdAsync(It.IsAny<long>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((long id, CancellationToken _) => Journals.SingleOrDefault(v => v.Id == id));
            Gl.Setup(g => g.GenerateNextSerialNoAsync(3, It.IsAny<int>(), It.IsAny<int>(), 4, It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(() => 88L + Journals.Count);
            Gl.Setup(g => g.AddVoucherAsync(It.IsAny<GlVoucherHeader>(), It.IsAny<CancellationToken>()))
                .Callback<GlVoucherHeader, CancellationToken>((voucher, _) => { Voucher = voucher; voucher.Id = 99 + Journals.Count; Journals.Add(voucher); }).Returns(Task.CompletedTask);
            Service = new LegacyPayrollJournalService(Repository.Object, Gl.Object, posting.Object, unitOfWork);
        }
        public async Task RejectBeforeWriting()
        {
            await Assert.ThrowsAsync<InvalidOperationException>(() => Service.PostPayrollToGlAsync(7, "HR"));
            Gl.Verify(g => g.GenerateNextSerialNoAsync(It.IsAny<long>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
            Gl.Verify(g => g.AddVoucherAsync(It.IsAny<GlVoucherHeader>(), It.IsAny<CancellationToken>()), Times.Never);
            Gl.Verify(g => g.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
            Repository.Verify(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
            Assert.Equal("APPROVED", Run.Status);
            Assert.Null(Run.JournalVoucherId);
        }
    }

    [Theory]
    [InlineData(null)] [InlineData("")] [InlineData("EUR")]
    public async Task MissingOrForeignLineCurrencyRejectsBeforeSerialAndWrites(string? currency)
    { var f = new Fixture(); f.Run.Lines[0].CurrencyCode = currency; await f.RejectBeforeWriting(); }

    [Fact]
    public async Task MixedCurrenciesCannotPostEvenWhenStoredTotalsBalance()
    {
        var f = new Fixture(); f.Run.Lines.Add(new PayrollRunLine { BranchId = 3, CurrencyCode = "EUR" }); await f.RejectBeforeWriting();
    }

    [Theory] [InlineData(null)] [InlineData(4L)]
    public async Task MissingOrDifferentLineBranchCannotPost(long? branchId)
    { var f = new Fixture(); f.Run.Lines[0].BranchId = branchId; await f.RejectBeforeWriting(); }

    [Fact]
    public async Task NegativeDeductionsCannotBeSilentlyOmittedFromVoucher()
    {
        var f = new Fixture(); f.Run.TotalOtherDeductions = -50m; f.Run.Lines[0].OtherDeductions = -50m;
        f.Run.TotalNetSalary = 890m; f.Run.Lines[0].NetPay = 890m;
        await f.RejectBeforeWriting();
    }

    [Theory] [InlineData(false)] [InlineData(true)]
    public async Task EmptyRunOrMissingResolvedCurrencyCannotPost(bool emptyRun)
    {
        var f = new Fixture(emptyRun ? "USD" : " "); if (emptyRun) f.Run.Lines.Clear(); await f.RejectBeforeWriting();
    }

    [Theory]
    [InlineData("gross")] [InlineData("net")] [InlineData("employee")] [InlineData("employer")]
    [InlineData("tax")] [InlineData("national")] [InlineData("other")]
    public async Task EachStaleAggregateIsRejectedBeforeSerial(string aggregate)
    {
        var f = new Fixture();
        switch (aggregate)
        {
            case "gross": f.Run.TotalGrossSalary += 1; break;
            case "net": f.Run.TotalNetSalary += 1; break;
            case "employee": f.Run.TotalEmployeeSsc += 1; break;
            case "employer": f.Run.TotalEmployerSsc += 1; break;
            case "tax": f.Run.TotalIncomeTax += 1; break;
            case "national": f.Run.TotalNationalContrib += 1; break;
            case "other": f.Run.TotalOtherDeductions += 1; break;
        }
        await f.RejectBeforeWriting();
    }

    [Fact]
    public async Task BalancedButStaleTotalsCannotPost()
    {
        var f = new Fixture(); f.Run.TotalGrossSalary += 1; f.Run.TotalNetSalary += 1; await f.RejectBeforeWriting();
    }

    [Theory] [InlineData(0.0005)] [InlineData(0.001)] [InlineData(1)]
    public async Task UnbalancedStoredLinesRejectBeforeSerial(decimal difference)
    {
        var f = new Fixture(); f.Run.TotalNetSalary += difference; f.Run.Lines[0].NetPay += difference;
        await f.RejectBeforeWriting();
    }

    [Theory] [InlineData("USD")] [InlineData(" usd ")]
    public async Task MatchingCurrencyPostsConfiguredAccountsAndBalancedLocalAmounts(string currency)
    {
        var f = new Fixture(currency); f.Run.Lines[0].CurrencyCode = " usd ";
        var result = await f.Service.PostPayrollToGlAsync(7, "HR");
        Assert.True(result.IsBalanced);
        var voucher = Assert.IsType<GlVoucherHeader>(f.Voucher);
        Assert.Equal(88, voucher.VoucherNo); Assert.Equal(12, voucher.FiscalYearId);
        Assert.Equal(new DateTime(2026, 12, 2), voucher.VoucherDate);
        Assert.Equal(1140m, voucher.TotalAmount);
        Assert.Equal(1140m, voucher.Details.Sum(d => d.Debit)); Assert.Equal(1140m, voucher.Details.Sum(d => d.Credit));
        Assert.Equal(1000m, voucher.Details.Single(d => d.AccountCode == "SALARY").Debit);
        Assert.Equal(140m, voucher.Details.Single(d => d.AccountCode == "EMPLOYER").Debit);
        Assert.Equal(790m, voucher.Details.Single(d => d.AccountCode == "NET").Credit);
        Assert.Equal(210m, voucher.Details.Single(d => d.AccountCode == "SSC").Credit);
        Assert.Equal(90m, voucher.Details.Single(d => d.AccountCode == "TAX").Credit);
        Assert.Equal(50m, voucher.Details.Single(d => d.AccountCode == "DEDUCTIONS").Credit);
        Assert.All(voucher.Details, d => { Assert.Equal(9, d.CurrencyId); Assert.Equal(1m, d.ExchangeRate);
            Assert.Equal(d.Debit, d.LocalDebit); Assert.Equal(d.Credit, d.LocalCredit); Assert.Equal(3, d.BranchId); });
        Assert.Equal("POSTED_TO_GL", f.Run.Status); Assert.Equal(99, f.Run.JournalVoucherId);
        Assert.Equal(" usd ", f.Run.Lines[0].CurrencyCode);
        f.Gl.Verify(g => g.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
        f.Repository.Verify(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task PostPayrollToGlAsync_WhenRunNotFound_ThrowsKeyNotFoundException()
    {
        var f = new Fixture();
        f.Repository.Setup(r => r.GetPayrollRunByIdAsync(999, It.IsAny<CancellationToken>()))
            .ReturnsAsync((PayrollRun?)null);

        var ex = await Assert.ThrowsAsync<KeyNotFoundException>(() => f.Service.PostPayrollToGlAsync(999, "HR"));
        Assert.Contains("Payroll run 999 not found", ex.Message);
    }

    [Fact]
    public async Task PostPayrollToGlAsync_WhenPeriodNotFound_ThrowsInvalidOperationException()
    {
        var f = new Fixture();
        f.Repository.Setup(r => r.GetPeriodForRunAsync(f.Run, It.IsAny<CancellationToken>()))
            .ReturnsAsync((PayrollPeriod?)null);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => f.Service.PostPayrollToGlAsync(7, "HR"));
        Assert.Contains("Payroll period was not found", ex.Message);
    }

    [Fact]
    public async Task PostPayrollToGlAsync_WhenPostingServiceNull_ThrowsInvalidOperationException()
    {
        var f = new Fixture();
        var serviceWithoutPosting = new LegacyPayrollJournalService(f.Repository.Object, f.Gl.Object, null, null);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => serviceWithoutPosting.PostPayrollToGlAsync(7, "HR"));
        Assert.Contains("Payroll posting configuration service is required", ex.Message);
    }

    [Fact]
    public async Task PostPayrollToGlAsync_WhenTotalOtherDeductionsZero_DoesNotAddDeductionsDetailLine()
    {
        var f = new Fixture("USD");
        // Adjust run so TotalOtherDeductions = 0, NetSalary increases to 840 (so Gross + Employer = 1140, Net 840 + SSC 210 + Tax 90 = 1140)
        f.Run.TotalOtherDeductions = 0m;
        f.Run.TotalNetSalary = 840m;
        f.Run.Lines[0].OtherDeductions = 0m;
        f.Run.Lines[0].NetPay = 840m;

        var result = await f.Service.PostPayrollToGlAsync(7, "HR");
        Assert.True(result.IsBalanced);
        var voucher = Assert.IsType<GlVoucherHeader>(f.Voucher);
        Assert.DoesNotContain(voucher.Details, d => d.AccountCode == "DEDUCTIONS");
        Assert.Equal(5, voucher.Details.Count); // 2 debits + 3 credits (no deductions line)
    }

    [Fact]
    public async Task PostPayrollToGlAsync_WhenCancellationTokenCanceled_ThrowsOperationCanceledException()
    {
        var f = new Fixture();
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        f.Repository.Setup(r => r.GetPayrollRunByIdAsync(7, cts.Token))
            .ThrowsAsync(new OperationCanceledException(cts.Token));

        await Assert.ThrowsAsync<OperationCanceledException>(() =>
            f.Service.PostPayrollToGlAsync(7, "HR", cts.Token));
    }
}
