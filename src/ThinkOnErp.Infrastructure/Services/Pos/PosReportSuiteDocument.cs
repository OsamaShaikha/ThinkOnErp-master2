using QuestPDF.Fluent;
using QuestPDF.Infrastructure;
using ThinkOnErp.Application.DTOs.Pos;

namespace ThinkOnErp.Infrastructure.Services.Pos;

public static class PosReportSuiteDocument
{
    public static byte[] Generate(PosReportPrintDataDto data, byte[]? logo = null)
    {
        var text = new PosSalesReportText(data.Header.Language);
        return PosSalesReportDocument.GenerateFrame(data.Header, logo, data.Title,
            data.IsLive ? text.T("Live snapshot", "لقطة مباشرة") : text.T("POS reports", "تقارير نقاط البيع"),
            (column, t) =>
            {
                foreach (var note in data.FilterNotes) column.Item().Text(note).FontColor("#60716A").FontSize(8);
                if (data.Branches.Count > 1)
                    column.Item().Text(t.T("Each branch is shown separately; averages and percentages belong to that branch.", "تعرض نتائج كل فرع على حدة؛ المتوسطات والنسب تخص الفرع المعروض.")).FontColor("#60716A").FontSize(7);
                foreach (var branch in data.Branches)
                {
                    if (data.Branches.Count > 1) column.Item().EnsureSpace(130).PaddingTop(8).Text(branch.BranchName).FontSize(12).Bold();
                    foreach (var batch in branch.Metrics.Chunk(4))
                    {
                        column.Item().ShowEntire().Row(row =>
                        {
                            row.Spacing(8);
                            for (var i = 0; i < batch.Length; i++)
                            {
                                var metric = batch[i];
                                PosSalesReportDocument.Card(row.RelativeItem(), metric.Label, metric.Value, metric.Unit, "", t, i == 0);
                            }
                        });
                    }
                    foreach (var table in branch.Tables)
                    {
                        column.Item().EnsureSpace(65).PaddingTop(6).Text(table.Title).FontSize(10).SemiBold();
                        column.Item().Element(c => Table(c, table, t));
                    }
                    foreach (var note in branch.Notes) column.Item().Text(note).FontSize(7).FontColor("#60716A");
                }
                column.Item().PaddingTop(4).Text(t.T("Amounts use the company base currency. Dates and timestamps use UTC. Figures follow the selected POS report's calculation rules.", "المبالغ بالعملة الأساسية للشركة. التواريخ والأوقات بالتوقيت العالمي UTC. تعتمد الأرقام قواعد احتساب تقرير نقاط البيع المختار.")).FontSize(6.5f).FontColor("#60716A");
            }, live: data.IsLive, showCashier: !data.IsLive, showPayment: false);
    }

    private static void Table(IContainer container, PosReportTableDto data, PosSalesReportText t)
    {
        if (data.Columns.Count == 0) throw new InvalidOperationException("Report table columns are required.");
        if (data.Rows.Any(row => row.Count != data.Columns.Count)) throw new InvalidOperationException("Report row and column counts differ.");
        container.Table(table =>
        {
            table.ColumnsDefinition(cols => { foreach (var c in data.Columns) cols.RelativeColumn(c.Width); });
            table.Header(header =>
            {
                foreach (var c in data.Columns)
                    header.Cell().Background("#1D3B32").Padding(7).Text(c.Label).FontSize(7).Bold().FontColor("#FFFFFF");
            });
            if (data.Rows.Count == 0)
                table.Cell().ColumnSpan((uint)data.Columns.Count).Background("#FAF9F5").Padding(14)
                    .Text(t.T("No records match the selected filters.", "لا توجد سجلات تطابق الخيارات المحددة.")).FontColor("#60716A");
            for (var rowIndex = 0; rowIndex < data.Rows.Count; rowIndex++)
            {
                var row = data.Rows[rowIndex];
                for (var i = 0; i < data.Columns.Count; i++)
                {
                    var definition = data.Columns[i];
                    var cell = table.Cell().Background(rowIndex % 2 == 0 ? "#FFFFFF" : "#FAF9F5")
                        .BorderBottom(.4f).BorderColor("#D6D3C9").PaddingVertical(6).PaddingHorizontal(5);
                    if (definition.Numeric) cell = cell.ContentFromLeftToRight().AlignRight();
                    else if (definition.Code) cell = cell.ContentFromLeftToRight();
                    cell.Text(row[i]).FontSize(data.Columns.Count > 7 ? 6.5f : 7).FontColor("#1D3B32");
                }
            }
        });
    }
}
