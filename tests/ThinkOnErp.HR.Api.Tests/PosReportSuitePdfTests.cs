using System.Collections;
using System.Reflection;
using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using QuestPDF.Infrastructure;
using ThinkOnErp.API.Controllers.Pos;
using ThinkOnErp.Application.Common;
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

public class PosReportSuitePdfTests
{
    private static readonly DateTime Day = new(2026, 10, 20);
    public static IEnumerable<object[]> Cases => new (string, Type)[]
    {
        ("sales-summary", typeof(PosSalesPrintDataDto)), ("sales-by-payment-method", typeof(PosPaymentMethodReportDto)),
        ("sales-by-cashier", typeof(PosCashierSalesReportDto)), ("sales-by-item", typeof(PosItemSalesReportDto)),
        ("sales-by-category", typeof(PosCategorySalesReportDto)), ("voids-and-refunds", typeof(PosVoidsAndRefundsReportDto)),
        ("discounts", typeof(PosDiscountsReportDto)), ("cash-drawer-variance", typeof(PosCashDrawerVarianceReportDto)),
        ("cash-movements", typeof(PosCashMovementReportDto)), ("hourly-sales", typeof(PosHourlySalesReportDto)),
        ("sales-by-floor-and-table", typeof(PosFloorTableSalesReportDto)), ("sales-by-order-type", typeof(PosOrderTypeSalesReportDto)),
        ("sales-by-customer", typeof(PosCustomerSalesReportDto)), ("tax-summary", typeof(PosTaxSummaryReportDto)),
        ("open-tables-live", typeof(PosOpenTablesLiveReportDto)), ("kitchen-prep-time", typeof(PosKitchenPrepTimeReportDto))
    }.SelectMany(c => new[] { new object[] { c.Item1, c.Item2, "en" }, new object[] { c.Item1, c.Item2, "ar" } });

    private static OracleDbContext Database()
    {
        var db = new OracleDbContext(new DbContextOptionsBuilder<OracleDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
        db.SysCurrencies.Add(new() { Id = 1, ShortNameEn = "JOD" });
        db.SysCompanies.Add(new() { Id = 1, CompanyNameEn = "ThinkOn Demo", CompanyNameLocal = "شركة ثينك أون", CurrId = 1, IsActive = true });
        db.SysBranches.AddRange(new SysBranch { Id = 1, CompanyId = 1, BranchNameEn = "Amman", BranchNameLocal = "عمّان", BaseCurrencyId = 1, IsActive = true },
            new SysBranch { Id = 2, CompanyId = 1, BranchNameEn = "Irbid", BaseCurrencyId = 1, IsActive = true },
            new SysBranch { Id = 3, CompanyId = 2, BranchNameEn = "Foreign", BaseCurrencyId = 1, IsActive = true });
        db.SysUsers.Add(new() { Id = 1, UserName = "cashier", CompanyId = 1, IsActive = true });
        db.SysUserBranches.Add(new() { Id = 1, UserId = 1, BranchId = 1 });
        db.PosFloors.AddRange(new PosFloor { Id = 1, BranchId = 1, FloorName = "Main floor", IsActive = true }, new PosFloor { Id = 2, BranchId = 2, FloorName = "Other floor", IsActive = true });
        db.SaveChanges(); return db;
    }

    private static PosReportPdfService Service(OracleDbContext db, IPosReportService source, bool admin = false)
    {
        var tenant = new Mock<ICurrentTenantContext>(); tenant.Setup(t => t.GetRequiredCompanyId()).Returns(1); tenant.Setup(t => t.GetLanguageCode()).Returns("en");
        var claims = new List<Claim> { new("userId", "1"), new(ClaimTypes.Name, "Demo User") };
        if (admin) claims.Add(new("isSuperAdmin", "true"));
        var http = new HttpContextAccessor { HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(new ClaimsIdentity(claims, "test")) } };
        var sales = new PosSalesReportExportService(db, tenant.Object, http, Mock.Of<ILogoStorageService>(), NullLogger<PosSalesReportExportService>.Instance);
        return new(sales, source, db, tenant.Object);
    }

    // Fixture generation only; production mappings deliberately use explicit typed DTO fields.
    private static object Fixture(Type type, int depth = 0)
    {
        var value = Activator.CreateInstance(type)!;
        foreach (var p in type.GetProperties().Where(p => p.CanWrite))
        {
            var t = Nullable.GetUnderlyingType(p.PropertyType) ?? p.PropertyType;
            if (t == typeof(string)) p.SetValue(value, p.Name.EndsWith("Ar") || p.Name.EndsWith("Local") ? "اسم عربي" : p.Name == "Scope" ? "Order" : p.Name == "Status" ? "Open" : p.Name.Contains("Reason") || p.Name.Contains("Notes") ? "ملاحظة توضيحية / Explanation & review" : "Demo-01");
            else if (t == typeof(decimal)) p.SetValue(value, 321.123m);
            else if (t == typeof(int)) p.SetValue(value, 7);
            else if (t == typeof(long)) p.SetValue(value, 1L);
            else if (t == typeof(bool)) p.SetValue(value, true);
            else if (t == typeof(DateTime)) p.SetValue(value, Day.AddHours(12));
            else if (t.IsEnum) p.SetValue(value, Enum.GetValues(t).GetValue(0));
            else if (t.IsGenericType && t.GetGenericTypeDefinition() == typeof(List<>))
            {
                var list = (IList)Activator.CreateInstance(t)!;
                if (depth < 2) list.Add(Fixture(t.GenericTypeArguments[0], depth + 1));
                p.SetValue(value, list);
            }
            else if (t.IsClass && depth < 2) p.SetValue(value, Fixture(t, depth + 1));
        }
        return value;
    }
    private static T F<T>() => (T)Fixture(typeof(T));
    private static ApiResponse<T> Success<T>() => ApiResponse<T>.CreateSuccess(F<T>());

    private static Mock<IPosReportService> Sources()
    {
        var m = new Mock<IPosReportService>(MockBehavior.Strict);
        m.Setup(x => x.GetSalesByPaymentMethodReportAsync(It.IsAny<PosReportFilterDto>(), It.IsAny<CancellationToken>())).ReturnsAsync(Success<PosPaymentMethodReportDto>());
        m.Setup(x => x.GetSalesByCashierReportAsync(It.IsAny<PosReportFilterDto>(), It.IsAny<CancellationToken>())).ReturnsAsync(Success<PosCashierSalesReportDto>());
        m.Setup(x => x.GetSalesByItemReportAsync(It.IsAny<PosReportFilterDto>(), It.IsAny<CancellationToken>())).ReturnsAsync(Success<PosItemSalesReportDto>());
        m.Setup(x => x.GetSalesByCategoryReportAsync(It.IsAny<PosReportFilterDto>(), It.IsAny<CancellationToken>())).ReturnsAsync(Success<PosCategorySalesReportDto>());
        m.Setup(x => x.GetVoidsAndRefundsReportAsync(It.IsAny<PosReportFilterDto>(), It.IsAny<CancellationToken>())).ReturnsAsync(Success<PosVoidsAndRefundsReportDto>());
        m.Setup(x => x.GetDiscountsReportAsync(It.IsAny<PosReportFilterDto>(), It.IsAny<CancellationToken>())).ReturnsAsync(Success<PosDiscountsReportDto>());
        m.Setup(x => x.GetCashDrawerVarianceReportAsync(It.IsAny<PosReportFilterDto>(), It.IsAny<CancellationToken>())).ReturnsAsync(Success<PosCashDrawerVarianceReportDto>());
        m.Setup(x => x.GetCashMovementsReportAsync(It.IsAny<PosReportFilterDto>(), It.IsAny<CancellationToken>())).ReturnsAsync(Success<PosCashMovementReportDto>());
        m.Setup(x => x.GetHourlySalesReportAsync(It.IsAny<PosReportFilterDto>(), It.IsAny<CancellationToken>())).ReturnsAsync(Success<PosHourlySalesReportDto>());
        m.Setup(x => x.GetSalesByFloorAndTableReportAsync(It.IsAny<PosReportFilterDto>(), It.IsAny<CancellationToken>())).ReturnsAsync(Success<PosFloorTableSalesReportDto>());
        m.Setup(x => x.GetSalesByOrderTypeReportAsync(It.IsAny<PosReportFilterDto>(), It.IsAny<CancellationToken>())).ReturnsAsync(Success<PosOrderTypeSalesReportDto>());
        m.Setup(x => x.GetSalesByCustomerReportAsync(It.IsAny<PosReportFilterDto>(), It.IsAny<CancellationToken>())).ReturnsAsync(Success<PosCustomerSalesReportDto>());
        m.Setup(x => x.GetTaxSummaryReportAsync(It.IsAny<PosReportFilterDto>(), It.IsAny<CancellationToken>())).ReturnsAsync(Success<PosTaxSummaryReportDto>());
        m.Setup(x => x.GetOpenTablesLiveReportAsync(It.IsAny<long>(), It.IsAny<long?>(), It.IsAny<CancellationToken>())).ReturnsAsync(Success<PosOpenTablesLiveReportDto>());
        m.Setup(x => x.GetKitchenPrepTimeReportAsync(It.IsAny<PosReportFilterDto>(), It.IsAny<int>(), It.IsAny<CancellationToken>())).ReturnsAsync(Success<PosKitchenPrepTimeReportDto>());
        return m;
    }

    [Theory]
    [MemberData(nameof(Cases))]
    public async Task EveryReport_DispatchesAndRendersInBothLanguages(string code, Type sourceType, string language)
    {
        using var db = Database(); var source = Sources(); var service = Service(db, source.Object);
        var filter = new PosReportExportFilterDto { BranchId = 1, Language = language == "ar" ? 1 : 2 };
        if (code != "open-tables-live") { filter.StartDate = Day; filter.EndDate = Day; filter.CashierUsername = "cashier"; }
        var result = await service.GetDataAsync(code, filter);
        Assert.True(result.Success, result.Message); Assert.Equal(code, result.Data!.Code); Assert.Equal(language, result.Data.Header.Language);
        Assert.Single(result.Data.Branches); Assert.NotEmpty(result.Data.Branches[0].Metrics); Assert.NotEmpty(result.Data.Branches[0].Tables);
        if (code != "sales-summary") Assert.Single(source.Invocations);
        var section = new PosReportSuiteMapper(language, "JOD").Map(Fixture(sourceType));
        Assert.All(section.Tables, table => { Assert.NotEmpty(table.Rows); Assert.All(table.Rows, row => Assert.Equal(table.Columns.Count, row.Count)); });
        // Render populated fixtures as well as testing real dispatcher metadata above.
        result.Data.Branches[0] = section; section.BranchName = language == "ar" ? "عمّان" : "Amman";
        QuestPDF.Settings.License = LicenseType.Community;
        var pdf = await service.RenderAsync(result.Data);
        Assert.Equal("%PDF", System.Text.Encoding.ASCII.GetString(pdf, 0, 4));
        if (Environment.GetEnvironmentVariable("POS_PDF_SAMPLE_DIR") is { } output)
        {
            var folder = Path.Combine(output, "pos-suite-samples"); Directory.CreateDirectory(folder);
            File.WriteAllBytes(Path.Combine(folder, $"{code}-{language}.pdf"), pdf);
        }
    }

    [Fact]
    public async Task AllBranches_KeepRatesAndAveragesSeparate_AndNeverReadForeignBranches()
    {
        using var db = Database(); var source = Sources();
        source.Setup(x => x.GetSalesByPaymentMethodReportAsync(It.IsAny<PosReportFilterDto>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((PosReportFilterDto f, CancellationToken _) => ApiResponse<PosPaymentMethodReportDto>.CreateSuccess(new() { NetCollected = f.BranchId == 1 ? 11 : 22 }));
        var result = await Service(db, source.Object, true).GetDataAsync("sales-by-payment-method", new() { StartDate = Day, EndDate = Day });
        Assert.True(result.Success); Assert.Equal(new long[] { 1, 2 }, result.Data!.Branches.Select(b => b.BranchId));
        Assert.Equal("11.000", result.Data.Branches[0].Metrics[0].Value); Assert.Equal("22.000", result.Data.Branches[1].Metrics[0].Value);
        source.Verify(x => x.GetSalesByPaymentMethodReportAsync(It.Is<PosReportFilterDto>(f => f.BranchId == 3), It.IsAny<CancellationToken>()), Times.Never);
        Assert.Equal(403, (await Service(db, source.Object).GetDataAsync("tax-summary", new() { BranchId = 2 })).StatusCode);
    }

    [Fact]
    public async Task AliasesResolve_AndMissingOrFailedReportsNeverProducePartialSuccess()
    {
        using var db = Database(); var source = Sources(); var service = Service(db, source.Object);
        foreach (var pair in new[] { ("drawer-variance", "cash-drawer-variance"), ("cash-in-out", "cash-movements"), ("sales-by-table", "sales-by-floor-and-table"), ("open-tables", "open-tables-live") })
            Assert.Equal(pair.Item2, (await service.GetDataAsync(pair.Item1, new())).Data!.Code);
        Assert.Equal(404, (await service.GetDataAsync("unknown", new())).StatusCode);
        source.Setup(x => x.GetTaxSummaryReportAsync(It.IsAny<PosReportFilterDto>(), It.IsAny<CancellationToken>())).ReturnsAsync(ApiResponse<PosTaxSummaryReportDto>.CreateFailure("source unavailable", statusCode: 503));
        var failed = await service.GetDataAsync("tax-summary", new()); Assert.False(failed.Success); Assert.Null(failed.Data); Assert.Equal(503, failed.StatusCode);
    }

    [Fact]
    public async Task ReportSpecificFilters_AreValidated_AndFloorsAreScoped()
    {
        using var db = Database(); var source = Sources(); var service = Service(db, source.Object);
        Assert.Equal(400, (await service.GetDataAsync("open-tables-live", new() { StartDate = Day })).StatusCode);
        Assert.Equal(400, (await service.GetDataAsync("open-tables-live", new() { CashierUsername = "cashier" })).StatusCode);
        Assert.Equal(400, (await service.GetDataAsync("tax-summary", new() { FloorId = 1, BranchId = 1 })).StatusCode);
        Assert.Equal(400, (await service.GetDataAsync("kitchen-prep-time", new() { DelayedThresholdMinutes = 0 })).StatusCode);
        Assert.Equal(400, (await service.GetDataAsync("sales-by-item", new() { PaymentMethod = PosPaymentMethod.Cash })).StatusCode);
        Assert.Empty(source.Invocations);
        Assert.Equal(403, (await service.GetDataAsync("open-tables-live", new() { BranchId = 1, FloorId = 2 })).StatusCode);
        var options = await service.GetOptionsAsync("open-tables-live", new() { BranchId = 1 });
        Assert.Equal(1, Assert.Single(options.Data!.Floors).Id);
        await service.GetDataAsync("kitchen-prep-time", new() { BranchId = 1, CashierUsername = "cashier", DelayedThresholdMinutes = 23 });
        source.Verify(x => x.GetKitchenPrepTimeReportAsync(It.Is<PosReportFilterDto>(f => f.BranchId == 1 && f.CashierUsername == "cashier"), 23, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public void CatalogAndSwaggerRoutes_CoverAllReportsAndAliases()
    {
        using var db = Database(); var service = Service(db, Sources().Object);
        var en = service.GetCatalog(2).Data!; var ar = service.GetCatalog(1).Data!;
        Assert.Equal(16, en.Count); Assert.Equal(16, en.Select(x => x.Code).Distinct().Count());
        Assert.All(en.Zip(ar), pair => Assert.NotEqual(pair.First.Name, pair.Second.Name));
        var routes = typeof(PosReportExportsController).GetMethods().SelectMany(m => m.GetCustomAttributes<HttpGetAttribute>()).Select(a => a.Template).ToList();
        foreach (var entry in en.Where(e => e.Code != "sales-summary"))
            foreach (var path in new[] { entry.Code }.Concat(entry.Aliases)) Assert.Contains(path + "/pdf", routes);
        Assert.True(en.Single(x => x.Code == "open-tables-live").IsLive);
        Assert.False(en.Single(x => x.Code == "open-tables-live").SupportsCashier);
    }

    [Fact]
    public async Task Options_IncludeHistoricalCashMovementAndShiftOperatorsWithoutSales()
    {
        using var db = Database();
        db.PosShifts.Add(new() { Id = 1, BranchId = 1, ShiftNumber = "S1", OpenedAt = Day, CreationUser = "former-shift-operator" });
        db.PosShiftCashMovements.Add(new() { Id = 1, ShiftId = 1, CreationDate = Day, CreationUser = "former-cash-operator", Reason = "Test" });
        await db.SaveChangesAsync(); var service = Service(db, Sources().Object);
        Assert.Contains((await service.GetOptionsAsync("cash-movements", new() { StartDate = Day, EndDate = Day })).Data!.Cashiers, c => c.UserName == "former-cash-operator");
        Assert.Contains((await service.GetOptionsAsync("drawer-variance", new() { StartDate = Day, EndDate = Day })).Data!.Cashiers, c => c.UserName == "former-shift-operator");
        Assert.Empty((await service.GetOptionsAsync("open-tables", new())).Data!.Cashiers);
    }

    [Fact]
    public async Task KitchenSource_ActuallyAppliesCashierFilter()
    {
        using var db = Database();
        db.PosOrderHeaders.AddRange(new PosOrderHeader { Id = 1, BranchId = 1, OrderNumber = "A", CreationUser = "cashier", CreationDate = Day, IsActive = true, Lines = new List<PosOrderLine> { new() { Id = 1, ItemId = 1, ItemCode = "A", ItemName = "A", KdsSentAt = Day, KdsReadyAt = Day.AddMinutes(20) } } },
            new PosOrderHeader { Id = 2, BranchId = 1, OrderNumber = "B", CreationUser = "other", CreationDate = Day, IsActive = true, Lines = new List<PosOrderLine> { new() { Id = 2, ItemId = 2, ItemCode = "B", ItemName = "B", KdsSentAt = Day, KdsReadyAt = Day.AddMinutes(3) } } });
        await db.SaveChangesAsync();
        var report = await new PosReportService(db, NullLogger<PosReportService>.Instance).GetKitchenPrepTimeReportAsync(new() { BranchId = 1, StartDate = Day, EndDate = Day, CashierUsername = "cashier" });
        Assert.True(report.Success, report.Message); Assert.Equal(1, report.Data!.TotalItemsAnalyzed); Assert.Equal(20, report.Data.OverallAveragePrepMinutes);
    }

    [Fact]
    public void LongTablesAndEmptyReports_RenderWithoutLosingPagination()
    {
        QuestPDF.Settings.License = LicenseType.Community;
        var section = new PosReportSuiteMapper("ar", "JOD").Map(F<PosDiscountsReportDto>());
        var row = section.Tables[0].Rows[0]; section.Tables[0].Rows = Enumerable.Range(0, 120).Select(_ => row.ToList()).ToList();
        var doc = new PosReportPrintDataDto { Code = "discounts", Title = "الخصومات وتعديل الأسعار", Header = new() { Language = "ar", CompanyName = "شركة تجريبية", BranchName = "الفرع الرئيسي", Currency = "JOD", StartDate = Day, EndDate = Day, IssuedAtUtc = Day }, Branches = new() { section } };
        var pdf = PosReportSuiteDocument.Generate(doc); Assert.True(pdf.Length > 1000);
        foreach (var table in section.Tables) table.Rows.Clear();
        Assert.True(PosReportSuiteDocument.Generate(doc).Length > 1000);
    }
}
