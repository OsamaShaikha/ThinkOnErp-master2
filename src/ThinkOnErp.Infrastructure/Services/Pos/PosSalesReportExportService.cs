using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using ThinkOnErp.Application.Common;
using ThinkOnErp.Application.DTOs.Pos;
using ThinkOnErp.Application.Services.Pos;
using ThinkOnErp.Domain.Entities;
using ThinkOnErp.Domain.Entities.Pos;
using ThinkOnErp.Domain.Entities.Pos.Enums;
using ThinkOnErp.Domain.Interfaces;
using ThinkOnErp.Domain.Interfaces.Accounting;
using ThinkOnErp.Infrastructure.Data;

namespace ThinkOnErp.Infrastructure.Services.Pos;

public sealed class PosSalesReportExportService(
    OracleDbContext db, ICurrentTenantContext tenant, IHttpContextAccessor http,
    ILogoStorageService logos, ILogger<PosSalesReportExportService> logger) : IPosSalesReportExportService
{
    public async Task<ApiResponse<PosSalesPrintDataDto>> GetDataAsync(PosSalesPdfFilterDto filter, CancellationToken ct = default)
    {
        var context = await GetContextAsync(filter, ct: ct);
        if (!context.Success) return Failure(context.Message, context.StatusCode);
        var data = context.Data!.Header;
        var orders = await Orders(context.Data.Branches.Select(b => b.Id).ToArray(), filter, data.StartDate, data.EndDate)
            .Include(o => o.Payments).ToListAsync(ct);
        var start = data.StartDate; var end = data.EndDate;
        var text = ReportText(filter);
        var grouped = orders.GroupBy(o => o.CreationDate.Date).ToDictionary(g => g.Key, g => Aggregate(g));
        for (var date = start; date <= end; date = date.AddDays(1))
            data.Days.Add(grouped.TryGetValue(date, out var day) ? day : new PosSalesDayDto { Date = date });
        data.Totals = Aggregate(orders);
        data.Payments = orders.SelectMany(o => o.Payments.Select(p => new
            { p.PaymentMethod, Amount = o.IsRefund ? -Math.Abs(p.Amount) : p.Amount }))
            .GroupBy(p => p.PaymentMethod).Select(g => new PosSalesTenderDto { Name = text.Payment(g.Key), Amount = g.Sum(p => p.Amount) })
            .OrderByDescending(p => p.Amount).ToList();
        var tenderTotal = data.Payments.Sum(p => p.Amount);
        foreach (var payment in data.Payments) payment.Percentage = tenderTotal > 0 ? payment.Amount / tenderTotal * 100 : 0;
        return ApiResponse<PosSalesPrintDataDto>.CreateSuccess(data);
    }

    internal async Task<ApiResponse<PosReportExportContext>> GetContextAsync(PosSalesPdfFilterDto filter, bool requireCurrency = true, CancellationToken ct = default)
    {
        var companyId = tenant.GetRequiredCompanyId();
        var (start, end) = DateRange(filter);
        var error = Validate(filter, start, end);
        if (error != null) return ContextFailure(error);
        var text = ReportText(filter);
        var scope = await GetAccessibleBranchesAsync(companyId, filter.BranchId, ct);
        if (!scope.Success) return ContextFailure(scope.Message, scope.StatusCode);
        var principal = http.HttpContext!.User;
        var allowed = scope.Data!;
        var company = await db.SysCompanies.AsNoTracking().Include(c => c.Currency).SingleOrDefaultAsync(c => c.Id == companyId, ct);
        if (company == null) return ContextFailure("Company was not found.", 404);
        // Never add values expressed in different currencies.
        var currencies = allowed.Select(b => b.BaseCurrencyId ?? company.CurrId).Distinct().ToList();
        if (requireCurrency && (currencies.Count != 1 || currencies[0] == null))
            return ContextFailure("Select branches with a single configured base currency.");
        var currency = allowed.Select(b => b.BaseCurrency).FirstOrDefault(c => c != null) ?? company.Currency;
        if (requireCurrency && (currency == null || string.IsNullOrWhiteSpace(currency.ShortNameEn)))
            return ContextFailure("Configure the report currency code before exporting.");

        var now = DateTime.UtcNow;
        var data = new PosSalesPrintDataDto
        {
            Language = text.Language,
            CompanyName = text.Name(company.CompanyNameLocal, company.CompanyNameEn),
            BranchName = filter.BranchId.HasValue ? text.Name(allowed[0].BranchNameLocal, allowed[0].BranchNameEn) : text.T("All accessible branches", "جميع الفروع المتاحة"),
            TaxNumber = allowed.Count == 1 ? allowed[0].TaxNumber : null,
            Phone = allowed.Count == 1 ? allowed[0].Phone : null,
            Currency = currencies.Count == 1 ? currency?.ShortNameEn ?? "-" : "-", StartDate = start, EndDate = end,
            Cashier = string.IsNullOrWhiteSpace(filter.CashierUsername) ? text.All : filter.CashierUsername,
            Payment = filter.PaymentMethod.HasValue ? text.Payment(filter.PaymentMethod.Value) : text.All,
            ShiftId = filter.ShiftId, TillId = filter.TillId,
            PreparedBy = principal.Identity?.Name ?? principal.FindFirst("username")?.Value ?? principal.FindFirst("userId")?.Value ?? "SuperAdmin",
            IssuedAtUtc = now, ReportNumber = $"RPT-{now:yyyyMMdd}-{Guid.NewGuid():N}"[..26]
        };
        return ApiResponse<PosReportExportContext>.CreateSuccess(new(data,
            allowed.Select(b => new PosSalesBranchOptionDto { Id = b.Id, Name = text.Name(b.BranchNameLocal, b.BranchNameEn) }).ToList()));
    }

    private static ApiResponse<PosReportExportContext> ContextFailure(string message, int status = 400) =>
        ApiResponse<PosReportExportContext>.CreateFailure(message, statusCode: status);

    public async Task<ApiResponse<PosSalesReportOptionsDto>> GetOptionsAsync(PosSalesPdfFilterDto filter, CancellationToken ct = default)
    {
        var companyId = tenant.GetRequiredCompanyId();
        var (start, end) = DateRange(filter);
        var error = Validate(filter, start, end);
        if (error != null) return ApiResponse<PosSalesReportOptionsDto>.CreateFailure(error);
        var text = ReportText(filter);
        // Return every accessible branch so choosing one never removes the other choices.
        var scope = await GetAccessibleBranchesAsync(companyId, null, ct);
        if (!scope.Success) return ApiResponse<PosSalesReportOptionsDto>.CreateFailure(scope.Message, statusCode: scope.StatusCode);
        var branches = scope.Data!;
        if (filter.BranchId.HasValue && !branches.Any(b => b.Id == filter.BranchId.Value))
            return ApiResponse<PosSalesReportOptionsDto>.CreateFailure("Report access denied.", statusCode: 403);
        var branchIds = branches.Where(b => !filter.BranchId.HasValue || b.Id == filter.BranchId).Select(b => b.Id).ToArray();
        var historicalNames = await Orders(branchIds, filter, start, end, applyCashier: false)
            .Select(o => o.CreationUser).Distinct().ToListAsync(ct);
        var assignedUsers = await db.SysUserBranches.AsNoTracking().Where(a => branchIds.Contains(a.BranchId))
            .Select(a => a.UserId).Distinct().ToListAsync(ct);
        // Include assigned active cashiers with no sales and historical operators even after deactivation.
        var users = await db.SysUsers.AsNoTracking().Where(u => u.CompanyId == companyId &&
            ((u.IsActive && assignedUsers.Contains(u.Id)) || historicalNames.Contains(u.UserName)))
            .Select(u => new { u.UserName, u.FullNameLocal, u.FullNameEn }).ToListAsync(ct);
        var names = users.GroupBy(u => u.UserName).ToDictionary(g => g.Key,
            g => text.Name(g.First().FullNameLocal, g.First().FullNameEn), StringComparer.Ordinal);
        var cashiers = users.Select(u => u.UserName).Concat(historicalNames)
            .Where(n => !string.IsNullOrWhiteSpace(n)).Distinct(StringComparer.Ordinal)
            .Select(n => new PosSalesCashierOptionDto { UserName = n, Name = names.TryGetValue(n, out var name) && !string.IsNullOrWhiteSpace(name) ? name : n })
            .OrderBy(c => c.Name, StringComparer.Ordinal).ToList();
        return ApiResponse<PosSalesReportOptionsDto>.CreateSuccess(new()
        {
            Language = text.Language, AllBranchesLabel = text.T("All branches", "جميع الفروع"),
            AllCashiersLabel = text.T("All cashiers", "جميع الكاشيرات"),
            Branches = branches.Select(b => new PosSalesBranchOptionDto { Id = b.Id, Name = text.Name(b.BranchNameLocal, b.BranchNameEn) })
                .OrderBy(b => b.Name, StringComparer.Ordinal).ToList(),
            Cashiers = cashiers
        });
    }

    internal PosSalesReportText ReportText(PosSalesPdfFilterDto filter) => new(
        filter.Language.HasValue ? (filter.Language == 1 ? "ar" : "en") : tenant.GetLanguageCode());

    internal static (DateTime Start, DateTime End) DateRange(PosSalesPdfFilterDto filter) =>
        ((filter.StartDate ?? DateTime.UtcNow.Date.AddDays(-9)).Date, (filter.EndDate ?? DateTime.UtcNow.Date).Date);

    internal static string? Validate(PosSalesPdfFilterDto filter, DateTime start, DateTime end)
    {
        if (filter.Language.HasValue && filter.Language is not (1 or 2)) return "Report language must be 1 (Arabic) or 2 (English).";
        if (end < start || end == DateTime.MaxValue.Date || (end - start).TotalDays > 365) return "Choose a valid date range of at most 366 days.";
        if (filter.BranchId <= 0 || filter.ShiftId <= 0 || filter.TillId <= 0 || filter.CashierUsername?.Length > 200 ||
            (filter.PaymentMethod.HasValue && !Enum.IsDefined(filter.PaymentMethod.Value))) return "Invalid report filter.";
        return null;
    }

    internal async Task<ApiResponse<List<SysBranch>>> GetAccessibleBranchesAsync(long companyId, long? branchId, CancellationToken ct)
    {
        var principal = http.HttpContext?.User;
        if (principal?.Identity?.IsAuthenticated != true)
            return ApiResponse<List<SysBranch>>.CreateFailure("Authentication is required.", statusCode: 401);
        var branches = db.SysBranches.AsNoTracking().Where(b => b.CompanyId == companyId && b.IsActive);
        if (principal.FindFirst("isSuperAdmin")?.Value != "true")
        {
            if (!long.TryParse(principal.FindFirst("userId")?.Value, out var userId))
                return ApiResponse<List<SysBranch>>.CreateFailure("Authentication is required.", statusCode: 401);
            var user = await db.SysUsers.AsNoTracking().SingleOrDefaultAsync(u => u.Id == userId && u.CompanyId == companyId && u.IsActive, ct);
            if (user == null) return ApiResponse<List<SysBranch>>.CreateFailure("Report access denied.", statusCode: 403);
            if (!user.IsAdmin)
            {
                // Primary and additional branch grants are persisted in SysUserBranches.
                var granted = await db.SysUserBranches.AsNoTracking().Where(a => a.UserId == userId).Select(a => a.BranchId).ToListAsync(ct);
                branches = branches.Where(b => granted.Contains(b.Id));
            }
        }
        if (branchId.HasValue) branches = branches.Where(b => b.Id == branchId);
        var allowed = await branches.Include(b => b.BaseCurrency).ToListAsync(ct);
        return allowed.Count == 0
            ? ApiResponse<List<SysBranch>>.CreateFailure("No accessible branches match this report.", statusCode: 403)
            : ApiResponse<List<SysBranch>>.CreateSuccess(allowed);
    }

    private IQueryable<PosOrderHeader> Orders(long[] branchIds, PosSalesPdfFilterDto filter, DateTime start, DateTime end, bool applyCashier = true)
    {
        var until = end.AddDays(1);
        var query = db.PosOrderHeaders.AsNoTracking().Where(o => branchIds.Contains(o.BranchId) && o.IsActive &&
            o.CreationDate >= start && o.CreationDate < until &&
            (o.Status == PosOrderStatus.Completed || o.Status == PosOrderStatus.Refunded));
        if (filter.ShiftId.HasValue) query = query.Where(o => o.ShiftId == filter.ShiftId);
        if (filter.TillId.HasValue) query = query.Where(o => o.TillId == filter.TillId);
        if (applyCashier && !string.IsNullOrWhiteSpace(filter.CashierUsername)) query = query.Where(o => o.CreationUser == filter.CashierUsername);
        // Payment selection retains complete split invoices.
        if (filter.PaymentMethod.HasValue) query = query.Where(o => o.Payments.Any(p => p.PaymentMethod == filter.PaymentMethod));
        return query;
    }

    public async Task<byte[]> RenderAsync(PosSalesPrintDataDto data, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        var logo = await LoadLogoAsync(ct);
        ct.ThrowIfCancellationRequested();
        return PosSalesReportDocument.Generate(data, logo);
    }

    internal async Task<byte[]?> LoadLogoAsync(CancellationToken ct)
    {
        var companyId = tenant.GetRequiredCompanyId();
        var company = await db.SysCompanies.AsNoTracking().SingleAsync(c => c.Id == companyId, ct);
        if (string.IsNullOrWhiteSpace(company.CompanyLogoPath)) return null;
        try { return await logos.GetLogoAsync(company.CompanyLogoPath); }
        catch (IOException ex) { logger.LogWarning(ex, "Company logo unavailable for POS PDF"); return null; }
    }

    private static ApiResponse<PosSalesPrintDataDto> Failure(string message, int status = 400) => ApiResponse<PosSalesPrintDataDto>.CreateFailure(message, statusCode: status);

    private static PosSalesDayDto Aggregate(IEnumerable<PosOrderHeader> source)
    {
        var orders = source.ToList();
        var sales = orders.Where(o => !o.IsRefund).ToList();
        var refunds = orders.Where(o => o.IsRefund).ToList();
        return new PosSalesDayDto
        {
            Date = orders.FirstOrDefault()?.CreationDate.Date ?? default,
            Invoices = sales.Count, Sales = sales.Sum(o => o.SubtotalAmount), Discount = sales.Sum(o => o.DiscountAmount),
            Returns = refunds.Sum(o => Math.Abs(o.SubtotalAmount - o.DiscountAmount)),
            Tax = sales.Sum(o => o.TaxAmount) - refunds.Sum(o => Math.Abs(o.TaxAmount)),
            Charges = sales.Sum(o => o.ServiceChargeAmount + o.DeliveryFee + o.TipAmount) - refunds.Sum(o => Math.Abs(o.ServiceChargeAmount) + Math.Abs(o.DeliveryFee) + Math.Abs(o.TipAmount)),
            Total = sales.Sum(o => o.TotalAmount) - refunds.Sum(o => Math.Abs(o.TotalAmount))
        };
    }
}

internal sealed record PosReportExportContext(PosSalesPrintDataDto Header, List<PosSalesBranchOptionDto> Branches);
