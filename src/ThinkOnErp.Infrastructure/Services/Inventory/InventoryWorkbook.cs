using System.Globalization;
using System.IO.Compression;
using System.Xml;
using System.Xml.Linq;

namespace ThinkOnErp.Infrastructure.Services.Inventory;

/// <summary>Small XLSX codec; uses the same ZIP/XML approach as the existing HR exports.</summary>
public static class InventoryWorkbook
{
    private static readonly XNamespace S = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";
    private static readonly XNamespace R = "http://schemas.openxmlformats.org/officeDocument/2006/relationships";
    private static readonly XNamespace P = "http://schemas.openxmlformats.org/package/2006/relationships";
    public const int MaxRows = 10000;
    public static readonly string[] ItemHeaders = { "ItemCode", "Sku", "ItemNameLocal", "ItemNameEn", "Category", "UomBase", "ItemType", "CostingMethod", "StandardCost", "DefaultSellingPrice", "ShowInPos", "Warehouse", "Quantity", "UnitCost", "Notes" };
    public static readonly string[] CategoryHeaders = { "CategoryCode", "CategoryNameLocal", "CategoryNameEn", "ParentCategoryCode", "ShowInPos" };
    public static readonly string[] OpeningHeaders = { "Item", "Warehouse", "Quantity", "UnitCost", "Notes", "BinId", "LotNumber", "SerialNumbers", "ExpiryDate", "UomCode", "UomFactor" };
    private static string SheetName(InventoryWorkbookKind kind) => kind switch { InventoryWorkbookKind.Items => "Items", InventoryWorkbookKind.Categories => "Categories", _ => "OpeningBalances" };
    private static string[] Headers(InventoryWorkbookKind kind) => kind switch { InventoryWorkbookKind.Items => ItemHeaders, InventoryWorkbookKind.Categories => CategoryHeaders, _ => OpeningHeaders };

    private static string Column(int index) => index < 26 ? ((char)('A' + index)).ToString() : throw new InvalidDataException("Too many columns.");
    public static byte[] Write(bool items, IEnumerable<object?[]> rows, IReadOnlyList<string> categories, IReadOnlyList<string>? warehouses = null)
        => Write(items ? InventoryWorkbookKind.Items : InventoryWorkbookKind.Categories, rows, categories, warehouses);

    public static byte[] Write(InventoryWorkbookKind kind, IEnumerable<object?[]> rows, IReadOnlyList<string> categories, IReadOnlyList<string>? warehouses = null, IReadOnlyDictionary<string, IReadOnlyList<string>>? binsByWarehouse = null)
    {
        var items = kind != InventoryWorkbookKind.Categories;
        var opening = kind == InventoryWorkbookKind.OpeningBalances;
        var primarySheet = opening ? "ItemLookup" : "CategoryLookup";
        var primaryName = opening ? "BranchItems" : "BranchCategories";
        warehouses ??= Array.Empty<string>();
        using var output = new MemoryStream();
        using (var zip = new ZipArchive(output, ZipArchiveMode.Create, true))
        {
            void Add(string path, XElement root)
            {
                using var stream = zip.CreateEntry(path).Open();
                new XDocument(root).Save(stream);
            }
            XNamespace types = "http://schemas.openxmlformats.org/package/2006/content-types";
            Add("[Content_Types].xml", new XElement(types + "Types",
                new XElement(types + "Default", new XAttribute("Extension", "rels"), new XAttribute("ContentType", "application/vnd.openxmlformats-package.relationships+xml")),
                new XElement(types + "Default", new XAttribute("Extension", "xml"), new XAttribute("ContentType", "application/xml")),
                new XElement(types + "Override", new XAttribute("PartName", "/xl/workbook.xml"), new XAttribute("ContentType", "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet.main+xml")),
                Enumerable.Range(1, opening ? 4 : items ? 3 : 2).Select(i => new XElement(types + "Override", new XAttribute("PartName", $"/xl/worksheets/sheet{i}.xml"), new XAttribute("ContentType", "application/vnd.openxmlformats-officedocument.spreadsheetml.worksheet+xml")))));
            XElement Rel(string id, string type, string target) => new(P + "Relationship", new XAttribute("Id", id), new XAttribute("Type", R.NamespaceName + "/" + type), new XAttribute("Target", target));
            Add("_rels/.rels", new XElement(P + "Relationships", Rel("rId1", "officeDocument", "xl/workbook.xml")));
            var relationships = new XElement(P + "Relationships", Rel("rId1", "worksheet", "worksheets/sheet1.xml"), Rel("rId2", "worksheet", "worksheets/sheet2.xml"));
            if (items) relationships.Add(Rel("rId3", "worksheet", "worksheets/sheet3.xml"));
            if (opening) relationships.Add(Rel("rId4", "worksheet", "worksheets/sheet4.xml"));
            Add("xl/_rels/workbook.xml.rels", relationships);
            var book = new XElement(S + "workbook", new XAttribute(XNamespace.Xmlns + "r", R),
                new XElement(S + "sheets",
                    new XElement(S + "sheet", new XAttribute("name", SheetName(kind)), new XAttribute("sheetId", 1), new XAttribute(R + "id", "rId1")),
                    new XElement(S + "sheet", new XAttribute("name", primarySheet), new XAttribute("sheetId", 2), new XAttribute("state", "hidden"), new XAttribute(R + "id", "rId2"))));
            var binRows = new List<object?[]>();
            if (items)
            {
                book.Element(S + "sheets")!.Add(new XElement(S + "sheet", new XAttribute("name", "WarehouseLookup"), new XAttribute("sheetId", 3), new XAttribute("state", "hidden"), new XAttribute(R + "id", "rId3")));
                var names = new XElement(S + "definedNames", new XElement(S + "definedName", new XAttribute("name", "BranchWarehouses"), $"'WarehouseLookup'!$A$1:$A${Math.Max(1, warehouses.Count)}"));
                if (categories.Count > 0 || opening) names.Add(new XElement(S + "definedName", new XAttribute("name", primaryName), $"'{primarySheet}'!$A$1:$A${Math.Max(1, categories.Count)}"));
                if (opening)
                {
                    book.Element(S + "sheets")!.Add(new XElement(S + "sheet", new XAttribute("name", "BinLookup"), new XAttribute("sheetId", 4), new XAttribute("state", "hidden"), new XAttribute(R + "id", "rId4")));
                    names.Add(new XElement(S + "definedName", new XAttribute("name", "WarehouseBins"), $"'BinLookup'!$A$1:$B${Math.Max(1, warehouses.Count)}"),
                        new XElement(S + "definedName", new XAttribute("name", "NoBins"), "'BinLookup'!$F$1"));
                    var allBins = new List<string>();
                    for (var index = 0; index < warehouses.Count; index++)
                    {
                        var choices = binsByWarehouse?.GetValueOrDefault(warehouses[index]) ?? Array.Empty<string>();
                        var rangeName = choices.Count > 0 ? $"WarehouseBinList_{index + 1}" : "NoBins";
                        binRows.Add(new object?[] { warehouses[index], rangeName });
                        if (choices.Count == 0) continue;
                        var first = allBins.Count + 1;
                        allBins.AddRange(choices);
                        names.Add(new XElement(S + "definedName", new XAttribute("name", rangeName), $"'BinLookup'!$D${first}:$D${allBins.Count}"));
                    }
                    for (var index = 0; index < allBins.Count; index++)
                    {
                        if (index >= binRows.Count) binRows.Add(new object?[4]);
                        var mapping = binRows[index];
                        binRows[index] = new object?[] { mapping.ElementAtOrDefault(0), mapping.ElementAtOrDefault(1), null, allBins[index] };
                    }
                }
                book.Add(names);
            }
            Add("xl/workbook.xml", book);
            XElement Cell(int number, int column, object? value)
            {
                var cell = new XElement(S + "c", new XAttribute("r", Column(column) + number));
                if (value is bool flag) value = flag ? "Yes" : "No";
                if (value is int or long or decimal) cell.Add(new XElement(S + "v", Convert.ToString(value, CultureInfo.InvariantCulture)));
                else cell.Add(new XAttribute("t", "inlineStr"), new XElement(S + "is", new XElement(S + "t", new XAttribute(XNamespace.Xml + "space", "preserve"), Convert.ToString(value, CultureInfo.InvariantCulture) ?? "")));
                return cell;
            }
            XElement Row(int number, object?[] values) => new(S + "row", new XAttribute("r", number), values.Select((value, i) => Cell(number, i, value)));
            var data = new XElement(S + "sheetData", Row(1, Headers(kind).Cast<object?>().ToArray()));
            int n = 2;
            foreach (var row in rows) data.Add(Row(n++, row));
            var sheet = new XElement(S + "worksheet",
                new XElement(S + "sheetViews", new XElement(S + "sheetView", new XAttribute("workbookViewId", 0), new XElement(S + "pane", new XAttribute("ySplit", 1), new XAttribute("topLeftCell", "A2"), new XAttribute("state", "frozen")))),
                new XElement(S + "cols", new XElement(S + "col", new XAttribute("min", 1), new XAttribute("max", Headers(kind).Length), new XAttribute("width", 26), new XAttribute("customWidth", 1))), data);
            var validations = new XElement(S + "dataValidations");
            if (items && (categories.Count > 0 || opening))
                validations.Add(new XElement(S + "dataValidation", new XAttribute("type", "list"), new XAttribute("allowBlank", 0), new XAttribute("showErrorMessage", 1), new XAttribute("errorStyle", "stop"), new XAttribute("error", opening ? "Select an item for this branch." : "Select a category for this branch."), new XAttribute("sqref", opening ? "A2:A1048576" : "E2:E1048576"), new XElement(S + "formula1", primaryName)));
            if (items)
                validations.Add(new XElement(S + "dataValidation", new XAttribute("type", "list"), new XAttribute("allowBlank", 0), new XAttribute("showErrorMessage", 1), new XAttribute("errorStyle", "stop"), new XAttribute("error", "Select a warehouse for this branch."), new XAttribute("sqref", opening ? "B2:B1048576" : "L2:L1048576"), new XElement(S + "formula1", "BranchWarehouses")));
            if (opening)
                validations.Add(new XElement(S + "dataValidation", new XAttribute("type", "list"), new XAttribute("allowBlank", 1), new XAttribute("showErrorMessage", 1), new XAttribute("errorStyle", "stop"), new XAttribute("error", "Select a bin from the selected warehouse."), new XAttribute("sqref", "F2:F1048576"), new XElement(S + "formula1", "INDIRECT(IFERROR(VLOOKUP($B2,WarehouseBins,2,FALSE),\"NoBins\"))")));
            var booleanColumn = items ? "K" : "E";
            if (!opening) validations.Add(new XElement(S + "dataValidation", new XAttribute("type", "list"), new XAttribute("allowBlank", 1), new XAttribute("showErrorMessage", 1), new XAttribute("errorStyle", "stop"), new XAttribute("error", "Select Yes or No."), new XAttribute("sqref", $"{booleanColumn}2:{booleanColumn}1048576"), new XElement(S + "formula1", "\"Yes,No\"")));
            validations.SetAttributeValue("count", validations.Elements().Count());
            sheet.Add(validations);
            Add("xl/worksheets/sheet1.xml", sheet);
            Add("xl/worksheets/sheet2.xml", new XElement(S + "worksheet", new XElement(S + "sheetData", categories.Select((c, i) => Row(i + 1, new object?[] { c })))));
            if (items) Add("xl/worksheets/sheet3.xml", new XElement(S + "worksheet", new XElement(S + "sheetData", warehouses.Select((w, i) => Row(i + 1, new object?[] { w })))));
            if (opening) Add("xl/worksheets/sheet4.xml", new XElement(S + "worksheet", new XElement(S + "sheetData", binRows.Select((values, i) => Row(i + 1, values)))));
        }
        return output.ToArray();
    }

    public static List<(int Number, Dictionary<string, string> Cells)> Read(Stream stream, bool items)
        => Read(stream, items ? InventoryWorkbookKind.Items : InventoryWorkbookKind.Categories);

    public static List<(int Number, Dictionary<string, string> Cells)> Read(Stream stream, InventoryWorkbookKind kind)
    {
        using var zip = new ZipArchive(stream, ZipArchiveMode.Read, true);
        if (zip.Entries.Sum(e => e.Length) > 40L * 1024 * 1024) throw new InvalidDataException("Uncompressed workbook exceeds 40 MB.");
        XDocument Load(string path)
        {
            using var entry = (zip.GetEntry(path) ?? throw new InvalidDataException("Missing workbook part: " + path)).Open();
            using var reader = XmlReader.Create(entry, new XmlReaderSettings { DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null, MaxCharactersInDocument = 40L * 1024 * 1024 });
            return XDocument.Load(reader);
        }
        var name = SheetName(kind);
        var sheet = Load("xl/workbook.xml").Descendants(S + "sheet").FirstOrDefault(e => (string?)e.Attribute("name") == name)
            ?? throw new InvalidDataException($"Worksheet '{name}' is required. Use the exported template.");
        var rel = Load("xl/_rels/workbook.xml.rels").Descendants(P + "Relationship").Single(e => (string?)e.Attribute("Id") == (string?)sheet.Attribute(R + "id"));
        if ((string?)rel.Attribute("TargetMode") == "External") throw new InvalidDataException("External worksheets are unsupported.");
        var target = (string?)rel.Attribute("Target") ?? "";
        var path = new Uri(new Uri("https://workbook/xl/workbook.xml"), target).AbsolutePath.TrimStart('/');
        var strings = zip.GetEntry("xl/sharedStrings.xml") == null ? new List<string>() : Load("xl/sharedStrings.xml").Descendants(S + "si").Select(e => string.Concat(e.Descendants(S + "t").Select(t => t.Value))).ToList();
        string Value(XElement cell)
        {
            if (cell.Element(S + "f") != null) throw new InvalidDataException("Formula cells are unsupported. Paste values before importing.");
            var value = cell.Element(S + "v")?.Value ?? "";
            return (string?)cell.Attribute("t") switch
            {
                "inlineStr" => string.Concat(cell.Descendants(S + "t").Select(e => e.Value)),
                "s" => int.TryParse(value, out var i) && i >= 0 && i < strings.Count ? strings[i] : throw new InvalidDataException("Invalid shared string."),
                "b" => value == "1" ? "true" : "false",
                "e" => throw new InvalidDataException("Excel error cells are unsupported."),
                _ => value
            };
        }
        var xmlRows = Load(path).Descendants(S + "sheetData").Elements(S + "row").ToList();
        if (xmlRows.Count == 0) throw new InvalidDataException("Header row is required.");
        string Ref(XElement c) => new(((string?)c.Attribute("r") ?? "").TakeWhile(char.IsLetter).ToArray());
        var headers = xmlRows[0].Elements(S + "c").ToDictionary(Ref, c =>
        {
            var header = Value(c).Trim();
            return kind == InventoryWorkbookKind.OpeningBalances && string.Equals(header, "SerialNumber", StringComparison.OrdinalIgnoreCase) ? "SerialNumbers" : header;
        });
        var expected = Headers(kind);
        if (headers.Values.Distinct(StringComparer.OrdinalIgnoreCase).Count() != headers.Count || expected.Any(h => !headers.Values.Contains(h, StringComparer.OrdinalIgnoreCase)))
            throw new InvalidDataException("Missing or duplicate headers. Use the exported template.");
        var result = new List<(int, Dictionary<string, string>)>();
        foreach (var row in xmlRows.Skip(1))
        {
            var cells = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var cell in row.Elements(S + "c"))
                if (headers.TryGetValue(Ref(cell), out var header)) cells[header] = Value(cell).Trim();
            if (cells.Values.All(string.IsNullOrWhiteSpace)) continue;
            if (result.Count >= MaxRows) throw new InvalidDataException($"Import is limited to {MaxRows} rows.");
            result.Add(((int?)row.Attribute("r") ?? result.Count + 2, cells));
        }
        return result;
    }
}

public enum InventoryWorkbookKind { Items, Categories, OpeningBalances }
