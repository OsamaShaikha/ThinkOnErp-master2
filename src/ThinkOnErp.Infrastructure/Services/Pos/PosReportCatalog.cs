using ThinkOnErp.Application.DTOs.Pos;

namespace ThinkOnErp.Infrastructure.Services.Pos;

internal sealed record PosReportDefinition(string Code, string English, string Arabic, bool Live = false, bool Floor = false, bool Delay = false, params string[] Aliases);

internal static class PosReportCatalog
{
    internal static readonly PosReportDefinition[] All =
    {
        new("sales-summary", "Sales report", "تقرير المبيعات"),
        new("sales-by-payment-method", "Sales by payment method", "المبيعات حسب طريقة الدفع"),
        new("sales-by-cashier", "Sales by cashier", "المبيعات حسب الكاشير"),
        new("sales-by-item", "Item sales & profitability", "مبيعات الأصناف والربحية"),
        new("sales-by-category", "Sales by category", "المبيعات حسب الفئة"),
        new("voids-and-refunds", "Voids & refunds", "الإلغاءات والمرتجعات"),
        new("discounts", "Discounts & price adjustments", "الخصومات وتعديل الأسعار"),
        new("cash-drawer-variance", "Cash drawer variance", "فروقات الصندوق", Aliases: new[] { "drawer-variance" }),
        new("cash-movements", "Cash in / cash out", "الإيداعات والسحوبات النقدية", Aliases: new[] { "cash-in-out" }),
        new("hourly-sales", "Hourly sales", "المبيعات حسب الساعة"),
        new("sales-by-floor-and-table", "Sales by floor & table", "المبيعات حسب الطابق والطاولة", Aliases: new[] { "sales-by-table" }),
        new("sales-by-order-type", "Sales by order type", "المبيعات حسب نوع الطلب"),
        new("sales-by-customer", "Sales by customer", "المبيعات حسب العميل"),
        new("tax-summary", "Tax summary", "ملخص الضريبة"),
        new("open-tables-live", "Live tables report", "تقرير الطاولات المباشر", Live: true, Floor: true, Aliases: new[] { "open-tables" }),
        new("kitchen-prep-time", "Kitchen preparation time", "زمن تحضير المطبخ", Delay: true)
    };

    internal static PosReportDefinition? Find(string code) => All.FirstOrDefault(d => d.Code.Equals(code, StringComparison.OrdinalIgnoreCase) || d.Aliases.Contains(code, StringComparer.OrdinalIgnoreCase));
    internal static PosReportCatalogEntryDto Entry(PosReportDefinition d, PosSalesReportText t) => new()
    {
        Code = d.Code, Name = t.T(d.English, d.Arabic), Aliases = d.Aliases.ToList(), IsLive = d.Live,
        SupportsCashier = !d.Live, SupportsFloor = d.Floor, SupportsDelayThreshold = d.Delay,
        PdfUrl = $"/api/pos/reports/{d.Code}/pdf"
    };
}
