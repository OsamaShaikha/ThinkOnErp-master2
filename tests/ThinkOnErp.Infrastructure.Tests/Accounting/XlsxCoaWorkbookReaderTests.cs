using System.IO.Compression;
using System.Xml.Linq;
using ThinkOnErp.Infrastructure.Services.Accounting;
using Xunit;

namespace ThinkOnErp.Infrastructure.Tests.Accounting;

public sealed class XlsxCoaWorkbookReaderTests
{
    private static readonly string[] Headers =
    {
        "account_code",
        "parent_code",
        "account_name_ar",
        "account_name_en",
        "level",
        "account_type",
        "category_code",
        "normal_balance",
        "financial_statement",
        "is_contra",
        "is_control_account",
        "control_account_type",
        "is_branch_specific",
        "is_clearing",
        "notes"
    };

    [Fact]
    public async Task ReadAsync_InlineStringWorksheet_MapsEveryImportField()
    {
        await using var workbook = BuildWorkbook(
            Headers,
            new[]
            {
                "111101",
                "1111",
                "الصندوق الرئيسي",
                "Main cash",
                "5",
                "DETAIL",
                "ASSETS",
                "D",
                "BALANCE_SHEET",
                "false",
                "1",
                "AR",
                "yes",
                "0",
                "حساب اختباري"
            });

        var result = await new XlsxCoaWorkbookReader().ReadAsync(workbook);

        Assert.Empty(result.Errors);
        var row = Assert.Single(result.Rows);
        Assert.Equal(2, row.RowNumber);
        Assert.Equal("111101", row.AccountCode);
        Assert.Equal("1111", row.ParentCode);
        Assert.Equal("الصندوق الرئيسي", row.AccountNameAr);
        Assert.Equal("Main cash", row.AccountNameEn);
        Assert.Equal(5, row.AccountLevel);
        Assert.Equal("DETAIL", row.AccountType);
        Assert.Equal("ASSETS", row.CategoryCode);
        Assert.Equal("D", row.NormalBalance);
        Assert.Equal("BALANCE_SHEET", row.FinancialStatement);
        Assert.False(row.IsContra);
        Assert.True(row.IsControlAccount);
        Assert.Equal("AR", row.ControlAccountType);
        Assert.True(row.IsBranchSpecific);
        Assert.False(row.IsClearing);
        Assert.Equal("حساب اختباري", row.Notes);
    }

    private static MemoryStream BuildWorkbook(params string[][] rows)
    {
        XNamespace spreadsheet = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";
        XNamespace relationships = "http://schemas.openxmlformats.org/officeDocument/2006/relationships";
        XNamespace packageRelationships = "http://schemas.openxmlformats.org/package/2006/relationships";

        var workbookDocument = new XDocument(
            new XElement(
                spreadsheet + "workbook",
                new XAttribute(XNamespace.Xmlns + "r", relationships),
                new XElement(
                    spreadsheet + "sheets",
                    new XElement(
                        spreadsheet + "sheet",
                        new XAttribute("name", "Chart of Accounts"),
                        new XAttribute("sheetId", "1"),
                        new XAttribute(relationships + "id", "rId1")))));

        var relationshipDocument = new XDocument(
            new XElement(
                packageRelationships + "Relationships",
                new XElement(
                    packageRelationships + "Relationship",
                    new XAttribute("Id", "rId1"),
                    new XAttribute(
                        "Type",
                        "http://schemas.openxmlformats.org/officeDocument/2006/relationships/worksheet"),
                    new XAttribute("Target", "worksheets/sheet1.xml"))));

        var worksheetRows = rows.Select((values, rowIndex) =>
            new XElement(
                spreadsheet + "row",
                new XAttribute("r", rowIndex + 1),
                values.Select((value, columnIndex) =>
                    new XElement(
                        spreadsheet + "c",
                        new XAttribute("r", $"{GetColumnName(columnIndex)}{rowIndex + 1}"),
                        new XAttribute("t", "inlineStr"),
                        new XElement(
                            spreadsheet + "is",
                            new XElement(spreadsheet + "t", value))))));

        var worksheetDocument = new XDocument(
            new XElement(
                spreadsheet + "worksheet",
                new XElement(spreadsheet + "sheetData", worksheetRows)));

        var stream = new MemoryStream();
        using (var archive = new ZipArchive(stream, ZipArchiveMode.Create, leaveOpen: true))
        {
            WriteXml(archive, "xl/workbook.xml", workbookDocument);
            WriteXml(archive, "xl/_rels/workbook.xml.rels", relationshipDocument);
            WriteXml(archive, "xl/worksheets/sheet1.xml", worksheetDocument);
        }

        stream.Position = 0;
        return stream;
    }

    private static void WriteXml(ZipArchive archive, string path, XDocument document)
    {
        var entry = archive.CreateEntry(path);
        using var entryStream = entry.Open();
        document.Save(entryStream);
    }

    private static string GetColumnName(int zeroBasedIndex)
    {
        var name = string.Empty;
        var number = zeroBasedIndex + 1;
        while (number > 0)
        {
            number--;
            name = (char)('A' + (number % 26)) + name;
            number /= 26;
        }

        return name;
    }
}
