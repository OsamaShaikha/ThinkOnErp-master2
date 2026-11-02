using System.Globalization;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using QuestPDF.Elements;
using ThinkOnErp.Application.DTOs.Pos;

namespace ThinkOnErp.Infrastructure.Services.Pos;

/// <summary>Shared A4 sales report layout. All values come from the print-data snapshot.</summary>
public static class PosSalesReportDocument
{
    private const string Green = "#1D3B32", Cream = "#EFECDF", Gold = "#A18045", Muted = "#60716A", Line = "#D6D3C9";
    private static string Money(decimal value) => value.ToString("N3", CultureInfo.InvariantCulture);

    public static byte[] Generate(PosSalesPrintDataDto data, byte[]? logo = null) =>
        GenerateFrame(data, logo, null, null, (column, t) =>
        {
                column.Item().Row(row =>
                {
                    row.Spacing(8);
                    Card(row.RelativeItem(), t.T("Net sales", "صافي المبيعات"), Money(data.Totals.Net), data.Currency, t.T("After discounts & returns", "بعد الخصومات والمرتجعات"), t, true);
                    Card(row.RelativeItem(), t.T("Total incl. tax", "الإجمالي شامل الضريبة"), Money(data.Totals.Total), data.Currency, $"{t.T("Tax", "الضريبة")} {Ltr(Money(data.Totals.Tax))}", t);
                    Card(row.RelativeItem(), t.T("Invoices", "الفواتير"), data.Totals.Invoices.ToString("N0", CultureInfo.InvariantCulture), "", t.T($"Over {data.Days.Count} days", $"خلال {data.Days.Count} يومًا"), t);
                    Card(row.RelativeItem(), t.T("Average invoice", "متوسط الفاتورة"), Money(data.AverageInvoice), data.Currency, t.T("Net total / sales invoices", "الإجمالي ÷ فواتير المبيعات"), t);
                });
                column.Item().Row(row =>
                {
                    row.RelativeItem().Text(t.T("Sales by day", "المبيعات حسب اليوم")).FontSize(10).SemiBold();
                    Trailing(row.RelativeItem(), t).Text(t.T($"Amounts in {data.Currency} - actual recorded tax", $"المبالغ بعملة {data.Currency} - الضريبة الفعلية المسجلة")).FontSize(7).FontColor(Muted);
                });
                column.Item().Element(c => SalesTable(c, data, t));
                column.Item().Text(t.T("Returns exclude tax. Total includes net tax, service charges, delivery fees and tips. Dates and issue time are UTC.", "المرتجعات غير شاملة للضريبة. يشمل الإجمالي صافي الضريبة ورسوم الخدمة والتوصيل والإكراميات. التواريخ ووقت الإصدار بالتوقيت العالمي UTC.")).FontSize(6.5f).FontColor(Muted);
        });

    internal static byte[] GenerateFrame(PosSalesPrintDataDto data, byte[]? logo, string? title, string? subtitle,
        Action<ColumnDescriptor, PosSalesReportText> content, bool live = false, bool showCashier = true, bool showPayment = true)
    {
        PosSalesReportFonts.EnsureRegistered();
        var t = new PosSalesReportText(data.Language);
        return Document.Create(document => document.Page(page =>
        {
            page.Size(PageSizes.A4);
            page.MarginHorizontal(30);
            page.MarginVertical(22);
            if (t.Arabic) page.ContentFromRightToLeft();
            else page.ContentFromLeftToRight();
            page.DefaultTextStyle(s => s.FontFamily(t.Arabic ? new[] { "ThinkOnArabic", "ThinkOnLatin" } : new[] { "ThinkOnLatin", "ThinkOnArabic" }).FontSize(8).FontColor(Green));
            page.Header().Element(c => Header(c, data, logo, t, title, subtitle));
            page.Content().PaddingTop(12).Column(column =>
            {
                column.Spacing(8);
                column.Item().Element(c => Panel(c, Cream, Cream)).Padding(12).Row(row =>
                {
                    Filter(row.RelativeItem(1.7f), live ? t.T("Snapshot (UTC)", "لحظة التقرير (UTC)") : t.T("Period (UTC)", "الفترة (UTC)"), Ltr(live ? $"{data.IssuedAtUtc:dd/MM/yyyy HH:mm}" : $"{data.StartDate:dd/MM/yyyy} - {data.EndDate:dd/MM/yyyy}"));
                    Filter(row.RelativeItem(1.4f), t.T("Branch", "الفرع"), data.BranchName);
                    if (showCashier) Filter(row.RelativeItem(), t.T("Cashier", "الكاشير"), data.Cashier);
                    if (showPayment) Filter(row.RelativeItem(), t.T("Payment", "الدفع"), data.Payment);
                    Filter(row.RelativeItem(.7f), t.T("Currency", "العملة"), data.Currency);
                });
                if (data.ShiftId.HasValue || data.TillId.HasValue)
                    column.Item().Text($"{t.T("Shift", "الوردية")}: {data.ShiftId?.ToString() ?? t.All}     {t.T("Till", "الصندوق")}: {data.TillId?.ToString() ?? t.All}").FontColor(Muted);
                content(column, t);
            });
            page.Footer().Column(footer =>
            {
                // Reserve the same height on every page so the last-page predicate cannot change pagination.
                footer.Item().Dynamic(new LastPageSignatures(data.PreparedBy, t));
                footer.Item().PaddingTop(10).BorderTop(.6f).BorderColor(Line).PaddingTop(8).Row(row =>
                {
                    row.RelativeItem(.8f).Text(text => { text.Span(t.T("Page ", "صفحة ")); text.CurrentPageNumber(); text.Span(t.T(" of ", " من ")); text.TotalPages(); });
                    row.RelativeItem(2.5f).AlignCenter().Text($"{t.T("Printed by", "طُبع بواسطة")} {data.PreparedBy} - {Ltr($"{data.IssuedAtUtc:dd/MM/yyyy HH:mm} UTC")}").FontSize(6.5f).FontColor(Muted);
                    Trailing(row.RelativeItem(), t).ContentFromLeftToRight().Text(text => { text.Span(t.T("Powered by ", "بدعم من ")).FontSize(7).FontColor(Muted); text.Span("think").Bold(); text.Span("on").Bold().FontColor(Gold); });
                });
            });
        })).GeneratePdf();
    }

    private sealed class LastPageSignatures(string preparedBy, PosSalesReportText t) : IDynamicComponent
    {
        public DynamicComponentComposeResult Compose(DynamicContext context) => new()
        {
            HasMoreContent = false,
            Content = context.CreateElement(c => c.Height(42).ShowIf(context.PageNumber == context.TotalPages).PaddingTop(7).Row(row =>
            {
                row.Spacing(18);
                Signature(row.RelativeItem(), $"{t.T("Prepared by", "إعداد")}: {preparedBy}");
                Signature(row.RelativeItem(), t.T("Reviewed by", "مراجعة"));
                Signature(row.RelativeItem(), t.T("Approved by", "اعتماد"));
            }))
        };
    }

    private static void Header(IContainer container, PosSalesPrintDataDto data, byte[]? logo, PosSalesReportText t, string? title, string? subtitle)
    {
        container.Column(column =>
        {
            column.Item().Height(4).Row(r => { r.RelativeItem(9).Background(Green); r.RelativeItem().Background(Gold); });
            column.Item().PaddingTop(18).PaddingBottom(13).BorderBottom(.6f).BorderColor(Line).PaddingBottom(12).Row(row =>
            {
                row.Spacing(10);
                var logoBox = row.ConstantItem(44).Height(44);
                if (logo is { Length: > 0 }) logoBox.Image(logo).FitArea();
                else logoBox.Background(Cream).AlignCenter().AlignMiddle().Text(t.T("Company", "الشركة")).FontSize(7).FontColor(Muted);
                row.RelativeItem().Column(info =>
                {
                    info.Spacing(5);
                    info.Item().Text(data.CompanyName).FontSize(12).Bold();
                    info.Item().Text($"{t.T("Branch", "الفرع")}: {data.BranchName}").FontColor(Muted);
                    if (!string.IsNullOrWhiteSpace(data.TaxNumber) || !string.IsNullOrWhiteSpace(data.Phone))
                        info.Item().Text($"{t.T("Tax no.", "الرقم الضريبي")}: {Ltr(data.TaxNumber ?? "-")} - {t.T("Phone", "الهاتف")}: {Ltr(data.Phone ?? "-")}").FontSize(7).FontColor(Muted);
                });
                Trailing(row.ConstantItem(185), t).Column(info =>
                {
                    info.Spacing(5);
                    Trailing(info.Item(), t).Text(title ?? t.T("Sales Report", "تقرير المبيعات")).FontSize(title == null ? 20 : title.Length > 26 ? 13 : 16).Bold();
                    Trailing(info.Item(), t).Text(subtitle ?? t.T("Daily - by date", "يومي - حسب التاريخ")).FontColor(Muted);
                    Trailing(info.Item(), t).Text($"{t.T("Report no.", "رقم التقرير")} {Ltr(data.ReportNumber)}").FontSize(7);
                    Trailing(info.Item(), t).Text($"{t.T("Issued", "صدر في")} {Ltr($"{data.IssuedAtUtc:dd/MM/yyyy HH:mm} UTC")}").FontSize(7);
                });
            });
        });
    }

    private static void Filter(IContainer c, string label, string value) => c.PaddingHorizontal(3).Column(x =>
    { x.Item().Text(label).FontSize(7).FontColor(Muted); x.Item().PaddingTop(5).Text(value).FontSize(8).SemiBold(); });

    internal static void Card(IContainer c, string label, string value, string currency, string caption, PosSalesReportText t, bool highlight = false)
    {
        Panel(c, highlight ? Green : "#FFFFFF", highlight ? Green : Line).Padding(10).MinHeight(55).Column(x =>
        {
            var color = highlight ? "#FFFFFF" : Green;
            x.Item().Text(label).FontSize(7.5f).FontColor(highlight ? Cream : Muted);
            var amount = x.Item().PaddingTop(6).ContentFromLeftToRight();
            amount = t.Arabic ? amount.AlignRight() : amount.AlignLeft();
            amount.Text(value).FontSize(value.Length > 12 ? 12 : 16).Bold().FontColor(color);
            x.Item().PaddingTop(2).Text(currency).FontSize(6).FontColor(highlight ? Gold : Muted);
            x.Item().PaddingTop(5).Text(caption).FontSize(6.5f).FontColor(highlight ? Cream : Muted);
        });
    }

    private static void Signature(IContainer c, string caption) => c.Column(x =>
    { x.Item().Text(caption).FontSize(7).FontColor(Muted); x.Item().Height(16).BorderBottom(.6f).BorderColor(Line); });

    private static IContainer Trailing(IContainer c, PosSalesReportText t) => t.Arabic ? c.AlignLeft() : c.AlignRight();
    private static string Ltr(string value) => "\u2066" + value + "\u2069";

    private static IContainer Panel(IContainer container, string fill, string stroke)
    {
        IContainer content = null!;
        container.Layers(layers =>
        {
            layers.Layer().Svg(size => FormattableString.Invariant($"<svg xmlns='http://www.w3.org/2000/svg' width='{size.Width}' height='{size.Height}' viewBox='0 0 {size.Width} {size.Height}'><rect x='0.5' y='0.5' width='{Math.Max(0, size.Width - 1)}' height='{Math.Max(0, size.Height - 1)}' rx='8' fill='{fill}' stroke='{stroke}' stroke-width='0.6'/></svg>"));
            content = layers.PrimaryLayer();
        });
        return content;
    }

    private static void SalesTable(IContainer container, PosSalesPrintDataDto data, PosSalesReportText t)
    {
        var charges = data.Days.Any(d => d.Charges != 0);
        var verticalPadding = data.Days.Count > 20 ? 2.5f : 5.5f;
        container.Table(table =>
        {
            table.ColumnsDefinition(c =>
            {
                c.ConstantColumn(20); c.ConstantColumn(76); c.ConstantColumn(35);
                for (var i = 0; i < (charges ? 7 : 6); i++) c.RelativeColumn();
            });
            var headers = new List<string> { "#", t.T("Date", "التاريخ"), t.T("Invoices", "الفواتير"), t.T("Sales", "المبيعات"), t.T("Discount", "الخصم"), t.T("Returns", "المرتجعات"), t.T("Net", "الصافي"), t.T("Tax", "الضريبة") };
            if (charges) headers.Add(t.T("Charges", "الرسوم"));
            headers.Add(t.T("Total", "الإجمالي"));
            table.Header(h => { foreach (var text in headers) h.Cell().Background(Green).PaddingVertical(10).PaddingHorizontal(4).Text(text).FontSize(7).Bold().FontColor("#FFFFFF"); });
            for (var i = 0; i < data.Days.Count; i++) WriteRow(table, data.Days[i], i + 1, charges, false, t, verticalPadding);
            WriteRow(table, data.Totals, 0, charges, true, t, verticalPadding);
        });
    }

    private static void WriteRow(TableDescriptor table, PosSalesDayDto day, int index, bool charges, bool total, PosSalesReportText t, float verticalPadding)
    {
        var date = t.Arabic ? $"{Ltr(day.Date.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture))} {day.Date.ToString("ddd", CultureInfo.GetCultureInfo("ar-JO"))}" : day.Date.ToString("dd/MM/yyyy ddd", CultureInfo.InvariantCulture);
        var cells = new List<string> { total ? t.T("Total", "الإجمالي") : index.ToString(CultureInfo.InvariantCulture), total ? "" : date, day.Invoices.ToString(CultureInfo.InvariantCulture), Money(day.Sales), Money(day.Discount), Money(day.Returns), Money(day.Net), Money(day.Tax) };
        if (charges) cells.Add(Money(day.Charges));
        cells.Add(Money(day.Total));
        for (var i = 0; i < cells.Count; i++)
        {
            if (total && i == 1) continue;
            var cell = table.Cell();
            if (total && i == 0) cell = cell.ColumnSpan(2);
            var c = cell.Background(total ? Cream : index % 2 == 0 ? "#FAF9F5" : "#FFFFFF");
            if (total) c = c.BorderTop(1.4f).BorderColor(Green);
            c = c.BorderBottom(.4f).BorderColor(Line).PaddingVertical(verticalPadding).PaddingHorizontal(4);
            if (i > 1) c = c.ContentFromLeftToRight().AlignRight();
            var text = c.Text(cells[i]).FontSize(charges ? 6 : 7);
            if (total || i == 6 || i == cells.Count - 1) text.SemiBold();
        }
    }

}
