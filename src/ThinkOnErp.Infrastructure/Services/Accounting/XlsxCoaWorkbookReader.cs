using System.Globalization;
using System.IO.Compression;
using System.Xml;
using System.Xml.Linq;
using ThinkOnErp.Application.DTOs.Accounting;
using ThinkOnErp.Application.Services.Accounting;

namespace ThinkOnErp.Infrastructure.Services.Accounting;

public sealed class XlsxCoaWorkbookReader : ICoaWorkbookReader
{
    private const string PreferredWorksheetName = "شجرة الحسابات";

    private static readonly XNamespace SpreadsheetNamespace =
        "http://schemas.openxmlformats.org/spreadsheetml/2006/main";

    private static readonly XNamespace OfficeDocumentRelationshipsNamespace =
        "http://schemas.openxmlformats.org/officeDocument/2006/relationships";

    private static readonly XNamespace PackageRelationshipsNamespace =
        "http://schemas.openxmlformats.org/package/2006/relationships";

    private static readonly string[] ExpectedHeaders =
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

    private static readonly HashSet<string> ExpectedHeaderSet =
        new(ExpectedHeaders, StringComparer.OrdinalIgnoreCase);

    public async Task<CoaWorkbookReadResultDto> ReadAsync(
        Stream workbook,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(workbook);

        var result = new CoaWorkbookReadResultDto();
        if (!workbook.CanRead)
        {
            AddWorkbookError(
                result,
                "WORKBOOK_NOT_READABLE",
                "The workbook stream is not readable.");
            return result;
        }

        if (workbook.CanSeek)
        {
            workbook.Position = 0;
        }

        try
        {
            using var archive = new ZipArchive(workbook, ZipArchiveMode.Read, leaveOpen: true);
            await ReadArchiveAsync(archive, result, cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (InvalidDataException exception)
        {
            AddWorkbookError(result, "INVALID_XLSX", exception.Message);
        }
        catch (XmlException exception)
        {
            AddWorkbookError(result, "INVALID_XLSX_XML", exception.Message);
        }
        catch (IOException exception)
        {
            AddWorkbookError(result, "WORKBOOK_READ_ERROR", exception.Message);
        }

        return result;
    }

    private static async Task ReadArchiveAsync(
        ZipArchive archive,
        CoaWorkbookReadResultDto result,
        CancellationToken cancellationToken)
    {
        var workbookEntry = FindEntry(archive, "xl/workbook.xml");
        var relationshipsEntry = FindEntry(archive, "xl/_rels/workbook.xml.rels");

        if (workbookEntry is null)
        {
            AddWorkbookError(result, "MISSING_WORKBOOK_PART", "The XLSX workbook part is missing.");
            return;
        }

        if (relationshipsEntry is null)
        {
            AddWorkbookError(
                result,
                "MISSING_WORKBOOK_RELATIONSHIPS",
                "The XLSX workbook relationships part is missing.");
            return;
        }

        var workbookDocument = await LoadDocumentAsync(workbookEntry, cancellationToken)
            .ConfigureAwait(false);
        var relationshipsDocument = await LoadDocumentAsync(relationshipsEntry, cancellationToken)
            .ConfigureAwait(false);

        var sheets = workbookDocument
            .Descendants(SpreadsheetNamespace + "sheet")
            .ToList();

        if (sheets.Count == 0)
        {
            AddWorkbookError(result, "MISSING_WORKSHEET", "The workbook does not contain a worksheet.");
            return;
        }

        var selectedSheet = sheets.FirstOrDefault(sheet =>
                string.Equals(
                    (string?)sheet.Attribute("name"),
                    PreferredWorksheetName,
                    StringComparison.Ordinal))
            ?? sheets[0];

        var relationshipId =
            (string?)selectedSheet.Attribute(OfficeDocumentRelationshipsNamespace + "id");
        if (string.IsNullOrWhiteSpace(relationshipId))
        {
            AddWorkbookError(
                result,
                "INVALID_WORKSHEET_RELATIONSHIP",
                "The selected worksheet does not have a relationship identifier.");
            return;
        }

        var relationship = relationshipsDocument
            .Descendants(PackageRelationshipsNamespace + "Relationship")
            .FirstOrDefault(item => string.Equals(
                (string?)item.Attribute("Id"),
                relationshipId,
                StringComparison.Ordinal));

        var target = (string?)relationship?.Attribute("Target");
        var targetMode = (string?)relationship?.Attribute("TargetMode");
        if (relationship is null
            || string.IsNullOrWhiteSpace(target)
            || string.Equals(targetMode, "External", StringComparison.OrdinalIgnoreCase))
        {
            AddWorkbookError(
                result,
                "INVALID_WORKSHEET_RELATIONSHIP",
                "The selected worksheet relationship is missing or invalid.");
            return;
        }

        var worksheetPath = ResolveWorkbookTarget(target);
        var worksheetEntry = worksheetPath is null ? null : FindEntry(archive, worksheetPath);
        if (worksheetEntry is null)
        {
            AddWorkbookError(
                result,
                "MISSING_WORKSHEET_PART",
                "The selected worksheet XML part is missing.");
            return;
        }

        var sharedStrings = await ReadSharedStringsAsync(archive, cancellationToken)
            .ConfigureAwait(false);
        var worksheetDocument = await LoadDocumentAsync(worksheetEntry, cancellationToken)
            .ConfigureAwait(false);

        ReadWorksheet(worksheetDocument, sharedStrings, result, cancellationToken);
    }

    private static async Task<IReadOnlyList<string>> ReadSharedStringsAsync(
        ZipArchive archive,
        CancellationToken cancellationToken)
    {
        var entry = FindEntry(archive, "xl/sharedStrings.xml");
        if (entry is null)
        {
            return Array.Empty<string>();
        }

        var document = await LoadDocumentAsync(entry, cancellationToken).ConfigureAwait(false);
        return document
            .Descendants(SpreadsheetNamespace + "si")
            .Select(ReadTextContainer)
            .ToList();
    }

    private static void ReadWorksheet(
        XDocument worksheetDocument,
        IReadOnlyList<string> sharedStrings,
        CoaWorkbookReadResultDto result,
        CancellationToken cancellationToken)
    {
        var rows = worksheetDocument
            .Descendants(SpreadsheetNamespace + "row")
            .ToList();

        if (rows.Count == 0)
        {
            AddWorkbookError(result, "EMPTY_WORKSHEET", "The selected worksheet is empty.");
            return;
        }

        var headerRow = rows[0];
        var headerRowNumber = GetRowNumber(headerRow, 1);
        var errorsBeforeHeaders = result.Errors.Count;
        var headerCells = ReadRowCells(headerRow, headerRowNumber, sharedStrings, result);
        var headerMap = BuildHeaderMap(headerCells, headerRowNumber, result);

        if (result.Errors.Count > errorsBeforeHeaders)
        {
            return;
        }

        for (var index = 1; index < rows.Count; index++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var row = rows[index];
            var rowNumber = GetRowNumber(row, headerRowNumber + index);
            var cells = ReadRowCells(row, rowNumber, sharedStrings, result);

            if (ExpectedHeaders.All(header =>
                    string.IsNullOrWhiteSpace(GetCellValue(cells, headerMap[header]))))
            {
                continue;
            }

            result.Rows.Add(new CoaImportRowDto
            {
                RowNumber = rowNumber,
                AccountCode = ReadRequiredText(
                    cells,
                    headerMap,
                    "account_code",
                    rowNumber,
                    result),
                OldAccountCode = headerMap.ContainsKey("old_account_code")
                    ? ReadOptionalText(cells, headerMap, "old_account_code")
                    : headerMap.ContainsKey("legacy_account_code")
                        ? ReadOptionalText(cells, headerMap, "legacy_account_code")
                        : headerMap.ContainsKey("رقم_الحساب_القديم")
                            ? ReadOptionalText(cells, headerMap, "رقم_الحساب_القديم")
                            : null,
                ParentCode = ReadOptionalText(cells, headerMap, "parent_code"),
                AccountNameAr = ReadRequiredText(
                    cells,
                    headerMap,
                    "account_name_ar",
                    rowNumber,
                    result),
                AccountNameEn = ReadRequiredText(
                    cells,
                    headerMap,
                    "account_name_en",
                    rowNumber,
                    result),
                AccountLevel = ReadInteger(
                    cells,
                    headerMap,
                    "level",
                    rowNumber,
                    result),
                AccountType = ReadRequiredText(
                    cells,
                    headerMap,
                    "account_type",
                    rowNumber,
                    result),
                CategoryCode = ReadRequiredText(
                    cells,
                    headerMap,
                    "category_code",
                    rowNumber,
                    result),
                NormalBalance = ReadRequiredText(
                    cells,
                    headerMap,
                    "normal_balance",
                    rowNumber,
                    result),
                FinancialStatement = ReadRequiredText(
                    cells,
                    headerMap,
                    "financial_statement",
                    rowNumber,
                    result),
                IsContra = ReadBoolean(
                    cells,
                    headerMap,
                    "is_contra",
                    rowNumber,
                    result),
                IsControlAccount = ReadBoolean(
                    cells,
                    headerMap,
                    "is_control_account",
                    rowNumber,
                    result),
                ControlAccountType = ReadOptionalText(cells, headerMap, "control_account_type"),
                IsBranchSpecific = ReadBoolean(
                    cells,
                    headerMap,
                    "is_branch_specific",
                    rowNumber,
                    result),
                IsClearing = ReadBoolean(
                    cells,
                    headerMap,
                    "is_clearing",
                    rowNumber,
                    result),
                Notes = ReadOptionalText(cells, headerMap, "notes")
            });
        }
    }

    private static Dictionary<string, int> BuildHeaderMap(
        IReadOnlyDictionary<int, string> headerCells,
        int rowNumber,
        CoaWorkbookReadResultDto result)
    {
        var headerMap = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

        foreach (var cell in headerCells.OrderBy(item => item.Key))
        {
            var header = NormalizeHeader(cell.Value);
            if (string.IsNullOrEmpty(header))
            {
                continue;
            }

            var excelColumn = GetExcelColumnName(cell.Key);
            if (!ExpectedHeaderSet.Contains(header))
            {
                result.Errors.Add(new CoaImportErrorDto(
                    rowNumber,
                    excelColumn,
                    "UNEXPECTED_HEADER",
                    $"Unexpected header '{header}' in column {excelColumn}."));
                continue;
            }

            if (!headerMap.TryAdd(header, cell.Key))
            {
                result.Errors.Add(new CoaImportErrorDto(
                    rowNumber,
                    excelColumn,
                    "DUPLICATE_HEADER",
                    $"Header '{header}' appears more than once."));
            }
        }

        foreach (var expectedHeader in ExpectedHeaders)
        {
            if (!headerMap.ContainsKey(expectedHeader))
            {
                result.Errors.Add(new CoaImportErrorDto(
                    rowNumber,
                    expectedHeader,
                    "MISSING_HEADER",
                    $"Required header '{expectedHeader}' is missing."));
            }
        }

        return headerMap;
    }

    private static Dictionary<int, string> ReadRowCells(
        XElement row,
        int rowNumber,
        IReadOnlyList<string> sharedStrings,
        CoaWorkbookReadResultDto result)
    {
        var values = new Dictionary<int, string>();
        var fallbackColumnIndex = 0;

        foreach (var cell in row.Elements(SpreadsheetNamespace + "c"))
        {
            var columnIndex = GetColumnIndex((string?)cell.Attribute("r"))
                ?? fallbackColumnIndex;
            fallbackColumnIndex = columnIndex + 1;

            var columnName = GetExcelColumnName(columnIndex);
            var value = ReadCellValue(cell, sharedStrings, rowNumber, columnName, result);
            if (!values.TryAdd(columnIndex, value))
            {
                result.Errors.Add(new CoaImportErrorDto(
                    rowNumber,
                    columnName,
                    "DUPLICATE_CELL",
                    $"Row {rowNumber} contains more than one cell for column {columnName}."));
            }
        }

        return values;
    }

    private static string ReadCellValue(
        XElement cell,
        IReadOnlyList<string> sharedStrings,
        int rowNumber,
        string columnName,
        CoaWorkbookReadResultDto result)
    {
        var cellType = (string?)cell.Attribute("t");
        if (string.Equals(cellType, "inlineStr", StringComparison.Ordinal))
        {
            var inlineString = cell.Element(SpreadsheetNamespace + "is");
            return inlineString is null ? string.Empty : ReadTextContainer(inlineString);
        }

        var value = (string?)cell.Element(SpreadsheetNamespace + "v") ?? string.Empty;
        if (string.Equals(cellType, "s", StringComparison.Ordinal))
        {
            if (!int.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out var index)
                || index < 0
                || index >= sharedStrings.Count)
            {
                result.Errors.Add(new CoaImportErrorDto(
                    rowNumber,
                    columnName,
                    "INVALID_SHARED_STRING",
                    $"Cell {columnName}{rowNumber} refers to an invalid shared string."));
                return string.Empty;
            }

            return sharedStrings[index];
        }

        if (string.Equals(cellType, "e", StringComparison.Ordinal))
        {
            result.Errors.Add(new CoaImportErrorDto(
                rowNumber,
                columnName,
                "CELL_ERROR",
                $"Cell {columnName}{rowNumber} contains the Excel error '{value}'."));
            return string.Empty;
        }

        return value;
    }

    private static string ReadTextContainer(XElement container)
    {
        var directText = container.Element(SpreadsheetNamespace + "t");
        if (directText is not null)
        {
            return directText.Value;
        }

        return string.Concat(container
            .Elements(SpreadsheetNamespace + "r")
            .Select(run => (string?)run.Element(SpreadsheetNamespace + "t") ?? string.Empty));
    }

    private static string ReadRequiredText(
        IReadOnlyDictionary<int, string> cells,
        IReadOnlyDictionary<string, int> headerMap,
        string header,
        int rowNumber,
        CoaWorkbookReadResultDto result)
    {
        var value = ReadOptionalText(cells, headerMap, header);
        if (value is not null)
        {
            return value;
        }

        result.Errors.Add(new CoaImportErrorDto(
            rowNumber,
            header,
            "REQUIRED_VALUE",
            $"Column '{header}' is required."));
        return string.Empty;
    }

    private static string? ReadOptionalText(
        IReadOnlyDictionary<int, string> cells,
        IReadOnlyDictionary<string, int> headerMap,
        string header)
    {
        var value = GetCellValue(cells, headerMap[header]).Trim();
        return value.Length == 0 ? null : value;
    }

    private static int ReadInteger(
        IReadOnlyDictionary<int, string> cells,
        IReadOnlyDictionary<string, int> headerMap,
        string header,
        int rowNumber,
        CoaWorkbookReadResultDto result)
    {
        var value = GetCellValue(cells, headerMap[header]).Trim();
        if (value.Length == 0)
        {
            result.Errors.Add(new CoaImportErrorDto(
                rowNumber,
                header,
                "REQUIRED_VALUE",
                $"Column '{header}' is required."));
            return default;
        }

        if (int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed))
        {
            return parsed;
        }

        result.Errors.Add(new CoaImportErrorDto(
            rowNumber,
            header,
            "INVALID_INTEGER",
            $"Value '{value}' in column '{header}' is not a valid integer."));
        return default;
    }

    private static bool ReadBoolean(
        IReadOnlyDictionary<int, string> cells,
        IReadOnlyDictionary<string, int> headerMap,
        string header,
        int rowNumber,
        CoaWorkbookReadResultDto result)
    {
        var value = GetCellValue(cells, headerMap[header]).Trim();
        if (value.Length == 0)
        {
            result.Errors.Add(new CoaImportErrorDto(
                rowNumber,
                header,
                "REQUIRED_VALUE",
                $"Column '{header}' is required."));
            return default;
        }

        if (value.Equals("true", StringComparison.OrdinalIgnoreCase)
            || value.Equals("1", StringComparison.Ordinal)
            || value.Equals("y", StringComparison.OrdinalIgnoreCase)
            || value.Equals("yes", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (value.Equals("false", StringComparison.OrdinalIgnoreCase)
            || value.Equals("0", StringComparison.Ordinal)
            || value.Equals("n", StringComparison.OrdinalIgnoreCase)
            || value.Equals("no", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        result.Errors.Add(new CoaImportErrorDto(
            rowNumber,
            header,
            "INVALID_BOOLEAN",
            $"Value '{value}' in column '{header}' is not a valid Boolean."));
        return default;
    }

    private static string GetCellValue(IReadOnlyDictionary<int, string> cells, int columnIndex)
    {
        return cells.TryGetValue(columnIndex, out var value) ? value : string.Empty;
    }

    private static string NormalizeHeader(string value)
    {
        return value.Trim().TrimStart('\uFEFF');
    }

    private static int GetRowNumber(XElement row, int fallback)
    {
        return int.TryParse(
            (string?)row.Attribute("r"),
            NumberStyles.None,
            CultureInfo.InvariantCulture,
            out var rowNumber)
            && rowNumber > 0
                ? rowNumber
                : fallback;
    }

    private static int? GetColumnIndex(string? cellReference)
    {
        if (string.IsNullOrWhiteSpace(cellReference))
        {
            return null;
        }

        var columnNumber = 0;
        var letterCount = 0;
        foreach (var character in cellReference)
        {
            if (!char.IsLetter(character))
            {
                break;
            }

            var letter = char.ToUpperInvariant(character);
            if (letter is < 'A' or > 'Z')
            {
                return null;
            }

            try
            {
                columnNumber = checked((columnNumber * 26) + (letter - 'A' + 1));
            }
            catch (OverflowException)
            {
                return null;
            }

            letterCount++;
        }

        return letterCount == 0 ? null : columnNumber - 1;
    }

    private static string GetExcelColumnName(int zeroBasedColumnIndex)
    {
        if (zeroBasedColumnIndex < 0)
        {
            return string.Empty;
        }

        var columnName = string.Empty;
        var columnNumber = zeroBasedColumnIndex + 1;
        while (columnNumber > 0)
        {
            columnNumber--;
            columnName = (char)('A' + (columnNumber % 26)) + columnName;
            columnNumber /= 26;
        }

        return columnName;
    }

    private static string? ResolveWorkbookTarget(string target)
    {
        var normalizedTarget = target.Replace('\\', '/').Trim();
        var path = normalizedTarget.StartsWith("/", StringComparison.Ordinal)
            ? normalizedTarget.TrimStart('/')
            : normalizedTarget.StartsWith("xl/", StringComparison.OrdinalIgnoreCase)
                ? normalizedTarget
                : $"xl/{normalizedTarget}";

        var segments = new List<string>();
        foreach (var segment in path.Split('/', StringSplitOptions.RemoveEmptyEntries))
        {
            if (segment == ".")
            {
                continue;
            }

            if (segment == "..")
            {
                if (segments.Count == 0)
                {
                    return null;
                }

                segments.RemoveAt(segments.Count - 1);
                continue;
            }

            segments.Add(segment);
        }

        var resolvedPath = string.Join('/', segments);
        return resolvedPath.StartsWith("xl/", StringComparison.OrdinalIgnoreCase)
            ? resolvedPath
            : null;
    }

    private static ZipArchiveEntry? FindEntry(ZipArchive archive, string fullName)
    {
        return archive.GetEntry(fullName)
            ?? archive.Entries.FirstOrDefault(entry => string.Equals(
                entry.FullName,
                fullName,
                StringComparison.OrdinalIgnoreCase));
    }

    private static async Task<XDocument> LoadDocumentAsync(
        ZipArchiveEntry entry,
        CancellationToken cancellationToken)
    {
        await using var stream = entry.Open();
        return await XDocument.LoadAsync(stream, LoadOptions.None, cancellationToken)
            .ConfigureAwait(false);
    }

    private static void AddWorkbookError(
        CoaWorkbookReadResultDto result,
        string code,
        string message)
    {
        result.Errors.Add(new CoaImportErrorDto(null, null, code, message));
    }
}
