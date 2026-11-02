using Microsoft.EntityFrameworkCore;
using ThinkOnErp.Application.Common;
using ThinkOnErp.Application.DTOs.Pos;
using ThinkOnErp.Application.Services.Pos;
using ThinkOnErp.Domain.Interfaces.Accounting;
using ThinkOnErp.Infrastructure.Data;

namespace ThinkOnErp.Infrastructure.Services.Pos;

public sealed class PosReportPdfService(PosSalesReportExportService sales, IPosReportService reports,
    OracleDbContext db, ICurrentTenantContext tenant) : IPosReportPdfService
{
    public ApiResponse<List<PosReportCatalogEntryDto>> GetCatalog(int? language)
    {
        if (language.HasValue && language is not (1 or 2)) return ApiResponse<List<PosReportCatalogEntryDto>>.CreateFailure("Language must be 1 or 2.");
        var t = sales.ReportText(new() { Language = language });
        return ApiResponse<List<PosReportCatalogEntryDto>>.CreateSuccess(PosReportCatalog.All.Select(d => PosReportCatalog.Entry(d, t)).ToList());
    }

    public async Task<ApiResponse<PosSalesReportOptionsDto>> GetOptionsAsync(string report, PosReportExportFilterDto filter, CancellationToken ct = default)
    {
        var definition = PosReportCatalog.Find(report);
        if (definition == null) return ApiResponse<PosSalesReportOptionsDto>.CreateFailure("Unknown POS report.", statusCode: 404);
        var error = Validate(definition, filter);
        if (error != null) return ApiResponse<PosSalesReportOptionsDto>.CreateFailure(error);
        var result = await sales.GetOptionsAsync(filter, ct);
        if (!result.Success || result.Data == null) return result;
        if (definition.Live) result.Data.Cashiers.Clear();
        else await AddHistoricalOperators(result.Data, definition, filter, ct);
        if (!definition.Floor || !filter.BranchId.HasValue) return result;
        var branchId = filter.BranchId.Value;
        var floors = await db.PosFloors.AsNoTracking().Where(f => f.IsActive && f.BranchId == branchId)
            .OrderBy(f => f.SortOrder).ThenBy(f => f.Id).Select(f => new { f.Id, f.FloorName }).ToListAsync(ct);
        result.Data.Floors = floors.Select(f => new PosSalesBranchOptionDto { Id = f.Id, Name = f.FloorName }).ToList();
        return result;
    }

    private async Task AddHistoricalOperators(PosSalesReportOptionsDto options, PosReportDefinition definition, PosReportExportFilterDto filter, CancellationToken ct)
    {
        var ids = options.Branches.Where(b => !filter.BranchId.HasValue || b.Id == filter.BranchId).Select(b => b.Id).ToArray();
        var (start, end) = PosSalesReportExportService.DateRange(filter);
        var until = end.AddDays(1);
        // Reconciliation and cash movements can have operators who never created a sale.
        IQueryable<string> query = definition.Code switch
        {
            "cash-drawer-variance" => db.PosShifts.AsNoTracking().Where(s => ids.Contains(s.BranchId) && s.OpenedAt >= start && s.OpenedAt < until).Select(s => s.CreationUser),
            "cash-movements" => db.PosShiftCashMovements.AsNoTracking().Where(m => m.Shift != null && ids.Contains(m.Shift.BranchId) && m.CreationDate >= start && m.CreationDate < until).Select(m => m.CreationUser),
            _ => db.PosOrderHeaders.AsNoTracking().Where(o => ids.Contains(o.BranchId) && o.IsActive && o.CreationDate >= start && o.CreationDate < until).Select(o => o.CreationUser)
        };
        var names = (await query.Distinct().ToListAsync(ct)).Where(n => !string.IsNullOrWhiteSpace(n) && !options.Cashiers.Any(c => c.UserName == n)).ToList();
        if (names.Count == 0) return;
        var companyId = tenant.GetRequiredCompanyId();
        var users = await db.SysUsers.AsNoTracking().Where(u => u.CompanyId == companyId && names.Contains(u.UserName))
            .Select(u => new { u.UserName, u.FullNameLocal, u.FullNameEn }).ToListAsync(ct);
        var text = new PosSalesReportText(options.Language);
        foreach (var name in names)
        {
            var user = users.FirstOrDefault(u => u.UserName == name);
            var display = user == null ? name : text.Name(user.FullNameLocal, user.FullNameEn);
            options.Cashiers.Add(new() { UserName = name, Name = string.IsNullOrWhiteSpace(display) ? name : display });
        }
        options.Cashiers = options.Cashiers.OrderBy(c => c.Name, StringComparer.Ordinal).ToList();
    }

    public async Task<ApiResponse<PosReportPrintDataDto>> GetDataAsync(string report, PosReportExportFilterDto filter, CancellationToken ct = default)
    {
        var definition = PosReportCatalog.Find(report);
        if (definition == null) return Fail("Unknown POS report.", 404);
        var error = Validate(definition, filter);
        if (error != null) return Fail(error);
        if (definition.Code == "sales-summary")
        {
            var summary = await sales.GetDataAsync(filter, ct);
            if (!summary.Success || summary.Data == null) return Fail(summary.Message, summary.StatusCode);
            var h = summary.Data;
            var branch = new PosReportSuiteMapper(h.Language, h.Currency).Map(h);
            branch.BranchId = filter.BranchId ?? 0; branch.BranchName = h.BranchName;
            var t = new PosSalesReportText(h.Language);
            return ApiResponse<PosReportPrintDataDto>.CreateSuccess(new() { Code = definition.Code, Title = t.T(definition.English, definition.Arabic), Header = h, Branches = new() { branch } });
        }

        var context = await sales.GetContextAsync(filter, requireCurrency: !definition.Delay, ct: ct);
        if (!context.Success || context.Data == null) return Fail(context.Message, context.StatusCode);
        var header = context.Data.Header;
        var text = new PosSalesReportText(header.Language);
        var document = new PosReportPrintDataDto { Code = definition.Code, Title = text.T(definition.English, definition.Arabic), Header = header, IsLive = definition.Live };
        if (filter.FloorId.HasValue)
        {
            var floor = await db.PosFloors.AsNoTracking().SingleOrDefaultAsync(f => f.Id == filter.FloorId && f.IsActive && f.BranchId == filter.BranchId, ct);
            if (floor == null) return Fail("Floor access denied.", 403);
            document.FilterNotes.Add(text.T("Floor", "الطابق") + ": " + floor.FloorName);
        }
        if (definition.Delay)
            document.FilterNotes.Add(text.T("Delay threshold (minutes)", "حد التأخير (دقائق)") + ": " + (filter.DelayedThresholdMinutes ?? 15));
        try
        {
            foreach (var branch in context.Data.Branches)
            {
                ct.ThrowIfCancellationRequested();
                var f = new PosReportFilterDto
                {
                    BranchId = branch.Id, StartDate = header.StartDate, EndDate = header.EndDate,
                    ShiftId = filter.ShiftId, TillId = filter.TillId, CashierUsername = filter.CashierUsername
                };
                object data = definition.Code switch
                {
                    "sales-by-payment-method" => await Source(reports.GetSalesByPaymentMethodReportAsync(f, ct)),
                    "sales-by-cashier" => await Source(reports.GetSalesByCashierReportAsync(f, ct)),
                    "sales-by-item" => await Source(reports.GetSalesByItemReportAsync(f, ct)),
                    "sales-by-category" => await Source(reports.GetSalesByCategoryReportAsync(f, ct)),
                    "voids-and-refunds" => await Source(reports.GetVoidsAndRefundsReportAsync(f, ct)),
                    "discounts" => await Source(reports.GetDiscountsReportAsync(f, ct)),
                    "cash-drawer-variance" => await Source(reports.GetCashDrawerVarianceReportAsync(f, ct)),
                    "cash-movements" => await Source(reports.GetCashMovementsReportAsync(f, ct)),
                    "hourly-sales" => await Source(reports.GetHourlySalesReportAsync(f, ct)),
                    "sales-by-floor-and-table" => await Source(reports.GetSalesByFloorAndTableReportAsync(f, ct)),
                    "sales-by-order-type" => await Source(reports.GetSalesByOrderTypeReportAsync(f, ct)),
                    "sales-by-customer" => await Source(reports.GetSalesByCustomerReportAsync(f, ct)),
                    "tax-summary" => await Source(reports.GetTaxSummaryReportAsync(f, ct)),
                    "open-tables-live" => await Source(reports.GetOpenTablesLiveReportAsync(branch.Id, filter.FloorId, ct)),
                    "kitchen-prep-time" => await Source(reports.GetKitchenPrepTimeReportAsync(f, filter.DelayedThresholdMinutes ?? 15, ct)),
                    _ => throw new NotSupportedException("Missing report dispatcher.")
                };
                ct.ThrowIfCancellationRequested();
                var section = new PosReportSuiteMapper(header.Language, header.Currency).Map(data);
                section.BranchId = branch.Id; section.BranchName = branch.Name;
                document.Branches.Add(section);
            }
        }
        catch (ReportSourceException ex) { ct.ThrowIfCancellationRequested(); return Fail(ex.Message, ex.StatusCode); }
        return ApiResponse<PosReportPrintDataDto>.CreateSuccess(document);
    }

    public async Task<byte[]> RenderAsync(PosReportPrintDataDto data, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        var logo = await sales.LoadLogoAsync(ct);
        ct.ThrowIfCancellationRequested();
        return PosReportSuiteDocument.Generate(data, logo);
    }

    private static string? Validate(PosReportDefinition definition, PosReportExportFilterDto f)
    {
        var (start, end) = PosSalesReportExportService.DateRange(f);
        var error = PosSalesReportExportService.Validate(f, start, end);
        if (error != null) return error;
        if (definition.Live && (f.StartDate.HasValue || f.EndDate.HasValue || f.ShiftId.HasValue || f.TillId.HasValue || !string.IsNullOrWhiteSpace(f.CashierUsername)))
            return "Live tables is a current snapshot; date, shift, till and cashier filters are not supported.";
        if (f.PaymentMethod.HasValue && definition.Code != "sales-summary") return "PaymentMethod filtering is supported only by the sales summary export.";
        if (f.FloorId.HasValue && (!definition.Floor || !f.BranchId.HasValue || f.FloorId <= 0)) return "Select a branch before choosing a floor for the live tables report.";
        if (f.DelayedThresholdMinutes.HasValue && (!definition.Delay || f.DelayedThresholdMinutes < 1 || f.DelayedThresholdMinutes > 1440)) return "Delay threshold must be between 1 and 1440 minutes and is supported only by kitchen preparation time.";
        return null;
    }

    private static async Task<object> Source<T>(Task<ApiResponse<T>> request)
    {
        var result = await request;
        if (!result.Success || result.Data == null) throw new ReportSourceException(result.Message, result.StatusCode >= 400 ? result.StatusCode : 400);
        return result.Data;
    }
    private sealed class ReportSourceException(string message, int statusCode) : Exception(message) { public int StatusCode { get; } = statusCode; }
    private static ApiResponse<PosReportPrintDataDto> Fail(string message, int status = 400) => ApiResponse<PosReportPrintDataDto>.CreateFailure(message, statusCode: status);
}
