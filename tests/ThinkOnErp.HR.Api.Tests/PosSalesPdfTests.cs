using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using QuestPDF.Infrastructure;
using ThinkOnErp.API.Controllers.Pos;
using ThinkOnErp.Application.DTOs.Pos;
using ThinkOnErp.Application.Services.Pos;
using ThinkOnErp.Domain.Entities;
using ThinkOnErp.Domain.Entities.Pos;
using ThinkOnErp.Domain.Entities.Pos.Enums;
using ThinkOnErp.Domain.Interfaces;
using ThinkOnErp.Domain.Interfaces.Accounting;
using ThinkOnErp.Infrastructure.Data;
using ThinkOnErp.Infrastructure.Services.Pos;
using Xunit;

namespace ThinkOnErp.HR.Api.Tests;

public class PosSalesPdfTests
{
    private static readonly DateTime Day = new(2026, 10, 1);

    private static OracleDbContext Database()
    {
        var db = new OracleDbContext(new DbContextOptionsBuilder<OracleDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
        db.SysCurrencies.Add(new SysCurrency { Id = 1, ShortNameEn = "JOD" });
        db.SysCompanies.Add(new SysCompany { Id = 1, CompanyNameEn = "ThinkOn Demo Company", CompanyNameLocal = "شركة ثينك أون", CurrId = 1, IsActive = true });
        db.SysBranches.AddRange(new SysBranch { Id = 1, CompanyId = 1, BranchNameEn = "Amman", BranchNameLocal = "عمّان", BaseCurrencyId = 1, IsActive = true },
            new SysBranch { Id = 2, CompanyId = 1, BranchNameEn = "Irbid", BaseCurrencyId = 1, IsActive = true },
            new SysBranch { Id = 3, CompanyId = 2, BranchNameEn = "Other tenant", BaseCurrencyId = 1, IsActive = true });
        db.SysUsers.Add(new SysUser { Id = 7, UserName = "cashier", CompanyId = 1, BranchId = 1, IsActive = true });
        db.SysUserBranches.Add(new SysUserBranch { Id = 1, UserId = 7, BranchId = 1, IsPrimary = true });
        db.SaveChanges();
        return db;
    }

    private static PosSalesReportExportService Service(OracleDbContext db, bool admin = false, string sessionLanguage = "en")
    {
        var tenant = new Mock<ICurrentTenantContext>(); tenant.Setup(t => t.GetRequiredCompanyId()).Returns(1);
        tenant.Setup(t => t.GetLanguageCode()).Returns(sessionLanguage);
        var claims = new List<Claim> { new("userId", "7"), new(ClaimTypes.Name, "Test User") };
        if (admin) claims.Add(new Claim("isSuperAdmin", "true"));
        var http = new HttpContextAccessor { HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(new ClaimsIdentity(claims, "test")) } };
        return new PosSalesReportExportService(db, tenant.Object, http, Mock.Of<ILogoStorageService>(), NullLogger<PosSalesReportExportService>.Instance);
    }

    private static PosOrderHeader Order(long id, long branch, decimal subtotal = 100, bool refund = false) => new()
    {
        Id = id, BranchId = branch, OrderNumber = $"O-{id}", ClientUuid = $"uuid-{id}", CreationDate = Day.AddHours(12), CreationUser = "cashier",
        Status = refund ? PosOrderStatus.Refunded : PosOrderStatus.Completed, IsActive = true, IsRefund = refund,
        SubtotalAmount = subtotal, DiscountAmount = refund ? 0 : 10, TaxAmount = refund ? -3.2m : 14.4m,
        TotalAmount = refund ? -23.2m : 104.4m,
        Payments = new List<PosOrderPayment> { new() { Id = id, PaymentMethod = PosPaymentMethod.Cash, Amount = refund ? -23.2m : 104.4m } }
    };

    [Fact]
    public async Task Snapshot_ReconcilesRefundsDailyRowsAndPayments_ExcludesUnfinalizedAndOtherBranches()
    {
        using var db = Database();
        var draft = Order(3, 1); draft.Status = PosOrderStatus.Draft;
        var voided = Order(4, 1); voided.Status = PosOrderStatus.Voided;
        db.PosOrderHeaders.AddRange(Order(1, 1), Order(2, 1, -20, true), draft, voided, Order(5, 2), Order(6, 3));
        await db.SaveChangesAsync();
        var result = await Service(db).GetDataAsync(new() { StartDate = Day, EndDate = Day.AddDays(1) });
        Assert.True(result.Success, result.Message);
        var d = result.Data!;
        Assert.Equal(1, d.Totals.Invoices); Assert.Equal(70m, d.Totals.Net);
        Assert.Equal(11.2m, d.Totals.Tax); Assert.Equal(81.2m, d.Totals.Total);
        Assert.Equal(d.Totals.Total, d.Days.Sum(x => x.Total));
        Assert.Equal(d.Totals.Total, d.Payments.Sum(x => x.Amount));
        Assert.Equal(2, d.Days.Count); Assert.Equal(0, d.Days[1].Total);
    }

    [Fact]
    public async Task AccessAndValidation_RejectForeignBranchAndInvalidDates()
    {
        using var db = Database(); var service = Service(db);
        Assert.Equal(403, (await service.GetDataAsync(new() { BranchId = 2 })).StatusCode);
        Assert.Equal(403, (await Service(db, true).GetDataAsync(new() { BranchId = 3 })).StatusCode);
        Assert.Equal(400, (await service.GetDataAsync(new() { StartDate = Day, EndDate = Day.AddDays(-1) })).StatusCode);
        Assert.Equal(400, (await service.GetDataAsync(new() { StartDate = Day, EndDate = Day.AddDays(366) })).StatusCode);
    }

    [Fact]
    public async Task PaymentFilter_SelectsWholeSplitInvoice_AndInclusiveEndDate()
    {
        using var db = Database(); var order = Order(1, 1);
        order.CreationDate = Day.AddDays(1).AddTicks(-1);
        order.Payments.First().Amount = 40;
        order.Payments.Add(new() { Id = 20, PaymentMethod = PosPaymentMethod.Card, Amount = 64.4m });
        db.PosOrderHeaders.AddRange(order, Order(2, 1)); await db.SaveChangesAsync();
        var d = (await Service(db).GetDataAsync(new() { StartDate = Day, EndDate = Day, PaymentMethod = PosPaymentMethod.Card })).Data!;
        Assert.Equal(1, d.Totals.Invoices); Assert.Equal(2, d.Payments.Count); Assert.Equal(104.4m, d.Payments.Sum(p => p.Amount));
    }

    [Fact]
    public async Task AllBranches_RespectsAssignments_RejectsMixedCurrencies()
    {
        using var db = Database();
        db.SysUserBranches.Add(new SysUserBranch { Id = 2, UserId = 7, BranchId = 2 });
        db.PosOrderHeaders.AddRange(Order(1, 1), Order(2, 2), Order(3, 3)); await db.SaveChangesAsync();
        var filter = new PosSalesPdfFilterDto { StartDate = Day, EndDate = Day };
        var accessible = await Service(db).GetDataAsync(filter);
        Assert.True(accessible.Success, accessible.Message); Assert.Equal(2, accessible.Data!.Totals.Invoices);
        db.SysCurrencies.Add(new SysCurrency { Id = 2, ShortNameEn = "USD" });
        (await db.SysBranches.SingleAsync(b => b.Id == 2)).BaseCurrencyId = 2; await db.SaveChangesAsync();
        Assert.Equal(400, (await Service(db).GetDataAsync(filter)).StatusCode);
    }

    [Fact]
    public async Task Options_ReturnOnlyAccessibleBranchesAndRelevantCashiers_IncludingHistoricalAndZeroSales()
    {
        using var db = Database();
        db.SysUsers.AddRange(new SysUser { Id = 8, CompanyId = 1, UserName = "zero-sales", FullNameLocal = "بدون مبيعات", IsActive = true },
            new SysUser { Id = 9, CompanyId = 1, UserName = "other-branch", IsActive = true },
            new SysUser { Id = 10, CompanyId = 1, UserName = "former", FullNameLocal = "كاشير سابق", IsActive = false });
        db.SysUserBranches.AddRange(new SysUserBranch { Id = 8, UserId = 8, BranchId = 1 }, new SysUserBranch { Id = 9, UserId = 9, BranchId = 2 });
        var historical = Order(2, 1); historical.CreationUser = "former";
        var other = Order(3, 2); other.CreationUser = "other-branch";
        var foreign = Order(4, 3); foreign.CreationUser = "foreign-tenant";
        var deleted = Order(5, 1); deleted.CreationUser = "deleted-cashier";
        var draft = Order(6, 1); draft.CreationUser = "draft-only"; draft.Status = PosOrderStatus.Draft;
        db.PosOrderHeaders.AddRange(Order(1, 1), historical, other, foreign, deleted, draft); await db.SaveChangesAsync();
        var filter = new PosSalesPdfFilterDto { StartDate = Day, EndDate = Day, Language = 1, CashierUsername = "cashier" };
        var service = Service(db);
        var result = await service.GetOptionsAsync(filter);
        Assert.True(result.Success, result.Message);
        Assert.Equal(1, Assert.Single(result.Data!.Branches).Id);
        Assert.Equal("عمّان", result.Data.Branches[0].Name);
        Assert.Equal("جميع الفروع", result.Data.AllBranchesLabel);
        Assert.Equal("جميع الكاشيرات", result.Data.AllCashiersLabel);
        Assert.Equal(new[] { "cashier", "deleted-cashier", "former", "zero-sales" }, result.Data.Cashiers.Select(c => c.UserName).OrderBy(n => n));
        Assert.Equal("كاشير سابق", result.Data.Cashiers.Single(c => c.UserName == "former").Name);
        Assert.Equal("deleted-cashier", result.Data.Cashiers.Single(c => c.UserName == "deleted-cashier").Name);
        filter.BranchId = 2; Assert.Equal(403, (await service.GetOptionsAsync(filter)).StatusCode);
        var adminOptions = await Service(db, true).GetOptionsAsync(new() { BranchId = 1, StartDate = Day, EndDate = Day });
        Assert.Equal(new long[] { 1, 2 }, adminOptions.Data!.Branches.Select(b => b.Id).OrderBy(id => id));
        Assert.DoesNotContain(adminOptions.Data.Cashiers, c => c.UserName == "other-branch");
        Assert.Equal(403, (await Service(db, true).GetOptionsAsync(new() { BranchId = 3 })).StatusCode);
    }

    [Fact]
    public async Task BranchAndCashierFilters_CombineAndCanIndependentlySelectAll()
    {
        using var db = Database();
        var secondCashier = Order(2, 1); secondCashier.CreationUser = "second";
        db.PosOrderHeaders.AddRange(Order(1, 1), secondCashier, Order(3, 2), Order(4, 3)); await db.SaveChangesAsync();
        var service = Service(db, true);
        var filter = new PosSalesPdfFilterDto { StartDate = Day, EndDate = Day, BranchId = 1, CashierUsername = "cashier" };
        Assert.Equal(1, (await service.GetDataAsync(filter)).Data!.Totals.Invoices);
        filter.CashierUsername = null;
        Assert.Equal(2, (await service.GetDataAsync(filter)).Data!.Totals.Invoices);
        filter.BranchId = null; filter.CashierUsername = "cashier";
        Assert.Equal(2, (await service.GetDataAsync(filter)).Data!.Totals.Invoices);
        filter.CashierUsername = null;
        Assert.Equal(3, (await service.GetDataAsync(filter)).Data!.Totals.Invoices);
        filter.CashierUsername = "missing";
        Assert.Equal(0, (await service.GetDataAsync(filter)).Data!.Totals.Invoices);
    }

    [Fact]
    public async Task Options_ExcludeOutOfPeriodHistoricalCashiers_AndValidateDatesAndLanguage()
    {
        using var db = Database(); var old = Order(1, 1); old.CreationDate = Day.AddDays(-1); old.CreationUser = "old-only";
        db.PosOrderHeaders.Add(old); await db.SaveChangesAsync(); var service = Service(db);
        var result = await service.GetOptionsAsync(new() { StartDate = Day, EndDate = Day });
        Assert.DoesNotContain(result.Data!.Cashiers, c => c.UserName == "old-only");
        Assert.Contains(result.Data.Cashiers, c => c.UserName == "cashier");
        Assert.Equal(400, (await service.GetOptionsAsync(new() { StartDate = Day, EndDate = Day.AddDays(-1) })).StatusCode);
        Assert.Equal(400, (await service.GetOptionsAsync(new() { Language = 9 })).StatusCode);
        var controller = new PosReportsController(Mock.Of<IPosReportService>()) { ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() } };
        var response = Assert.IsType<ObjectResult>(await controller.GetSalesReportOptions(new() { Language = 1 }, service, default));
        Assert.Equal(200, response.StatusCode); Assert.Equal("ar", controller.Response.Headers.ContentLanguage.ToString());
        Assert.Equal("no-store", controller.Response.Headers.CacheControl.ToString());
    }

    [Fact]
    public async Task PdfEndpoint_ReturnsDownloadAndPropagatesAccessFailure()
    {
        using var db = Database(); var service = Service(db);
        QuestPDF.Settings.License = LicenseType.Community;
        var controller = new PosReportsController(Mock.Of<IPosReportService>()) { ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() } };
        var file = Assert.IsType<FileContentResult>(await controller.GetSalesPdf(new() { BranchId = 1, StartDate = Day, EndDate = Day }, service, default));
        Assert.Equal("application/pdf", file.ContentType); Assert.EndsWith(".pdf", file.FileDownloadName);
        Assert.EndsWith("-en.pdf", file.FileDownloadName);
        Assert.Equal("en", controller.Response.Headers.ContentLanguage.ToString());
        Assert.Equal("no-store", controller.Response.Headers.CacheControl.ToString());
        var denied = Assert.IsType<ObjectResult>(await controller.GetSalesPdf(new() { BranchId = 3 }, service, default));
        Assert.Equal(403, denied.StatusCode);
    }

    [Theory]
    [InlineData("ar", null, "ar", "شركة ثينك أون", "عمّان", "نقدًا")]
    [InlineData("en", null, "en", "ThinkOn Demo Company", "Amman", "Cash")]
    [InlineData("ar-JO", null, "ar", "شركة ثينك أون", "عمّان", "نقدًا")]
    [InlineData("en", 1, "ar", "شركة ثينك أون", "عمّان", "نقدًا")]
    [InlineData("ar", 2, "en", "ThinkOn Demo Company", "Amman", "Cash")]
    [InlineData("fr", null, "en", "ThinkOn Demo Company", "Amman", "Cash")]
    public async Task Language_UsesSessionOrExplicitOverride_AndLocalizesNamesAndTenders(string session, int? language, string expected, string company, string branch, string tender)
    {
        using var db = Database(); db.PosOrderHeaders.Add(Order(1, 1)); await db.SaveChangesAsync();
        var result = await Service(db, sessionLanguage: session).GetDataAsync(new() { BranchId = 1, StartDate = Day, EndDate = Day, Language = language });
        Assert.True(result.Success, result.Message);
        var data = result.Data!;
        Assert.Equal(expected, data.Language); Assert.Equal(expected == "ar", data.IsRightToLeft);
        Assert.Equal(company, data.CompanyName); Assert.Equal(branch, data.BranchName);
        Assert.Equal(tender, Assert.Single(data.Payments).Name);
        Assert.Equal(expected == "ar" ? "الكل" : "All", data.Cashier);
        Assert.Equal(90, data.Totals.Net); Assert.Equal(104.4m, data.Totals.Total);
    }

    [Fact]
    public async Task Language_TranslatesFiltersAndFallsBackWhenLocalNameIsMissing()
    {
        using var db = Database();
        (await db.SysCompanies.SingleAsync()).CompanyNameLocal = "";
        (await db.SysBranches.SingleAsync(b => b.Id == 1)).BranchNameLocal = "";
        db.PosOrderHeaders.Add(Order(1, 1)); await db.SaveChangesAsync();
        var service = Service(db, sessionLanguage: "ar");
        var data = (await service.GetDataAsync(new() { BranchId = 1, StartDate = Day, EndDate = Day, PaymentMethod = PosPaymentMethod.Cash })).Data!;
        Assert.Equal("ThinkOn Demo Company", data.CompanyName); Assert.Equal("Amman", data.BranchName);
        Assert.Equal("نقدًا", data.Payment);
        Assert.Equal("جميع الفروع المتاحة", (await service.GetDataAsync(new())).Data!.BranchName);
        Assert.Equal(400, (await service.GetDataAsync(new() { Language = 3 })).StatusCode);
    }

    [Theory]
    [InlineData("1", "2", "en", "ar")]
    [InlineData("2", "1", "ar", "en")]
    [InlineData(null, "2", "ar", "en")]
    [InlineData(null, null, "ar-JO,en;q=0.8", "ar")]
    public void ReportSessionLanguage_UsesJwtClaimBeforeDefaultAndHeader(string? active, string? saved, string header, string expected)
    {
        var claims = new List<Claim>();
        if (active != null) claims.Add(new("lang", active));
        if (saved != null) claims.Add(new("defaultLang", saved));
        var context = new DefaultHttpContext { User = new ClaimsPrincipal(new ClaimsIdentity(claims, "test")) };
        context.Request.Headers.AcceptLanguage = header;
        var tenant = new ThinkOnErp.Infrastructure.Services.Accounting.HttpCurrentTenantContext(new HttpContextAccessor { HttpContext = context });
        Assert.Equal(expected, tenant.GetLanguageCode());
    }

    [Theory]
    [InlineData(0)] [InlineData(10)] [InlineData(90)] [InlineData(366)]
    public void ArabicDocument_RendersWithEmbeddedFontAndLocalScriptNames(int count)
    {
        QuestPDF.Settings.License = LicenseType.Community;
        var data = Sample(count);
        data.Language = "ar"; data.CompanyName = "شركة ثينك أون للتجارة"; data.BranchName = "جميع الفروع المتاحة";
        data.Cashier = "الكل"; data.Payment = "الكل"; data.PreparedBy = "أحمد محمد";
        data.Payments[0].Name = "نقدًا"; data.Payments[1].Name = "بطاقة"; data.Payments[2].Name = "ذمم العملاء";
        var pdf = PosSalesReportDocument.Generate(data);
        Assert.True(pdf.Length > 1000); Assert.Equal("%PDF", System.Text.Encoding.ASCII.GetString(pdf, 0, 4));
        if (Environment.GetEnvironmentVariable("POS_PDF_SAMPLE_DIR") is { } output && count is 10 or 90)
        { Directory.CreateDirectory(output); File.WriteAllBytes(Path.Combine(output, $"pos-sales-report-ar-{count}days.pdf"), pdf); }
    }

    [Fact]
    public void Document_RendersRefundOnlyAndLargeChargeValues()
    {
        QuestPDF.Settings.License = LicenseType.Community;
        var data = Sample(10);
        foreach (var day in data.Days) { day.Sales = 0; day.Returns = 99999999.999m; day.Tax = -16000000m; day.Charges = -5; day.Total = day.Net + day.Tax + day.Charges; }
        var bytes = PosSalesReportDocument.Generate(data);
        Assert.True(bytes.Length > 1000);
    }

    [Theory]
    [InlineData(0)] [InlineData(10)] [InlineData(90)] [InlineData(366)]
    public void Document_RendersEmptyNormalAndMultiPageReports(int count)
    {
        QuestPDF.Settings.License = LicenseType.Community;
        var data = Sample(count);
        var pdf = PosSalesReportDocument.Generate(data);
        Assert.True(pdf.Length > 1000); Assert.Equal("%PDF", System.Text.Encoding.ASCII.GetString(pdf, 0, 4));
        if (count == 10 && Environment.GetEnvironmentVariable("POS_PDF_SAMPLE_DIR") is { } output)
        { Directory.CreateDirectory(output); File.WriteAllBytes(Path.Combine(output, "pos-sales-report-sample.pdf"), pdf); }
        if (count == 90 && Environment.GetEnvironmentVariable("POS_PDF_SAMPLE_DIR") is { } multi)
        { Directory.CreateDirectory(multi); File.WriteAllBytes(Path.Combine(multi, "pos-sales-report-multipage.pdf"), pdf); }
    }

    private static PosSalesPrintDataDto Sample(int count)
    {
        var d = new PosSalesPrintDataDto { CompanyName = "ThinkOn Demo Company", BranchName = "All accessible branches", Currency = "JOD", TaxNumber = "123456789", Phone = "0790000000", StartDate = Day, EndDate = Day.AddDays(Math.Max(0, count - 1)), PreparedBy = "Demo User", ReportNumber = "RPT-20261026-DEMO", IssuedAtUtc = new DateTime(2026, 10, 26, 16, 15, 0) };
        for (var i = 0; i < count; i++)
        {
            var sales = 600m + (i * 137 % 750); var discount = sales * .02m; var returns = i * 1.2m;
            var net = sales - discount - returns;
            d.Days.Add(new() { Date = Day.AddDays(i), Invoices = 60 + i * 3, Sales = sales, Discount = discount, Returns = returns, Tax = net * .16m, Total = net * 1.16m });
        }
        d.Totals = new() { Invoices = d.Days.Sum(x => x.Invoices), Sales = d.Days.Sum(x => x.Sales), Discount = d.Days.Sum(x => x.Discount), Returns = d.Days.Sum(x => x.Returns), Tax = d.Days.Sum(x => x.Tax), Total = d.Days.Sum(x => x.Total) };
        d.Payments = new() { new() { Name = "Cash", Amount = d.Totals.Total * .46m, Percentage = 46 }, new() { Name = "Card", Amount = d.Totals.Total * .41m, Percentage = 41 }, new() { Name = "Credit (AR)", Amount = d.Totals.Total * .13m, Percentage = 13 } };
        return d;
    }
}
