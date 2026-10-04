using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Xml;
using System.Xml.Linq;
using ThinkOnErp.Application.DTOs.Hr;
using ThinkOnErp.Application.Services.Hr;

namespace ThinkOnErp.Infrastructure.Services.Hr;

public sealed class XlsxEmployeeWorkbookReader : IXlsxEmployeeWorkbookReader
{
    private static readonly XNamespace SpreadsheetNamespace =
        "http://schemas.openxmlformats.org/spreadsheetml/2006/main";

    private static readonly XNamespace OfficeDocumentRelationshipsNamespace =
        "http://schemas.openxmlformats.org/officeDocument/2006/relationships";

    private static readonly XNamespace PackageRelationshipsNamespace =
        "http://schemas.openxmlformats.org/package/2006/relationships";

    private static readonly string[] PreferredSheetNames =
    {
        "الموظفون",
        "الموظفين",
        "Employees",
        "Sheet1"
    };

    public async Task<(IReadOnlyList<EmployeeImportRowDto> Rows, EmployeeImportResultDto Result)> ReadAsync(
        Stream workbook,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(workbook);

        var result = new EmployeeImportResultDto();
        var rows = new List<EmployeeImportRowDto>();

        if (!workbook.CanRead)
        {
            result.Errors.Add(new EmployeeImportErrorDto(0, null, "STREAM_UNREADABLE", "Cannot read workbook stream."));
            return (rows, result);
        }

        if (workbook.CanSeek)
        {
            workbook.Position = 0;
        }

        try
        {
            using var archive = new ZipArchive(workbook, ZipArchiveMode.Read, leaveOpen: true);
            var workbookDoc = await LoadXmlEntryAsync(archive, "xl/workbook.xml", cancellationToken);
            var relsDoc = await LoadXmlEntryAsync(archive, "xl/_rels/workbook.xml.rels", cancellationToken);

            if (workbookDoc == null || relsDoc == null)
            {
                result.Errors.Add(new EmployeeImportErrorDto(0, null, "INVALID_XLSX", "Workbook structure parts are missing."));
                return (rows, result);
            }

            var sheets = workbookDoc.Descendants(SpreadsheetNamespace + "sheet").ToList();
            if (sheets.Count == 0)
            {
                result.Errors.Add(new EmployeeImportErrorDto(0, null, "NO_SHEETS", "No worksheets found in workbook."));
                return (rows, result);
            }

            // Find preferred sheet or first sheet
            var targetSheet = sheets.FirstOrDefault(s =>
                PreferredSheetNames.Any(p => string.Equals((string?)s.Attribute("name"), p, StringComparison.OrdinalIgnoreCase)))
                ?? sheets[0];

            var relId = (string?)targetSheet.Attribute(OfficeDocumentRelationshipsNamespace + "id");
            var rel = relsDoc.Descendants(PackageRelationshipsNamespace + "Relationship")
                .FirstOrDefault(r => string.Equals((string?)r.Attribute("Id"), relId, StringComparison.Ordinal));

            var targetUri = (string?)rel?.Attribute("Target");
            if (string.IsNullOrWhiteSpace(targetUri))
            {
                result.Errors.Add(new EmployeeImportErrorDto(0, null, "SHEET_REL_MISSING", "Worksheet target relationship missing."));
                return (rows, result);
            }

            var sheetPath = targetUri.StartsWith("/") ? targetUri.TrimStart('/') : "xl/" + targetUri.Replace("../", string.Empty);
            var sheetDoc = await LoadXmlEntryAsync(archive, sheetPath, cancellationToken);
            if (sheetDoc == null)
            {
                result.Errors.Add(new EmployeeImportErrorDto(0, null, "SHEET_NOT_FOUND", $"Worksheet part '{sheetPath}' not found."));
                return (rows, result);
            }

            // Load shared strings if available
            var sharedStrings = await LoadSharedStringsAsync(archive, cancellationToken);

            // Read rows
            var sheetData = sheetDoc.Descendants(SpreadsheetNamespace + "sheetData").FirstOrDefault();
            if (sheetData == null)
            {
                return (rows, result);
            }

            var xmlRows = sheetData.Elements(SpreadsheetNamespace + "row").ToList();
            if (xmlRows.Count == 0)
            {
                return (rows, result);
            }

            // Parse Header Row (Row 1)
            var headerRowElem = xmlRows[0];
            var headerMap = ParseHeaders(headerRowElem, sharedStrings);

            if (!headerMap.ContainsKey("EmployeeCode") && !headerMap.ContainsKey("NationalId") && !headerMap.ContainsKey("NameLocal"))
            {
                result.Errors.Add(new EmployeeImportErrorDto(1, null, "INVALID_HEADERS", "Required column headers (EmployeeCode, NameLocal, NationalId) not detected."));
                return (rows, result);
            }

            // Parse Data Rows (Row 2 onwards)
            for (var i = 1; i < xmlRows.Count; i++)
            {
                var rowElem = xmlRows[i];
                var rowNumberStr = (string?)rowElem.Attribute("r");
                var rowNumber = int.TryParse(rowNumberStr, out var rNum) ? rNum : (i + 1);

                var cellValues = ParseRowCells(rowElem, sharedStrings);
                if (cellValues.Count == 0 || cellValues.Values.All(string.IsNullOrWhiteSpace))
                {
                    // Empty row, skip safely
                    continue;
                }

                var rowDto = ParseEmployeeRow(cellValues, headerMap, rowNumber, result);
                if (rowDto != null)
                {
                    rows.Add(rowDto);
                }
            }
        }
        catch (InvalidDataException ex)
        {
            result.Errors.Add(new EmployeeImportErrorDto(0, null, "INVALID_XLSX_ZIP", ex.Message));
        }
        catch (XmlException ex)
        {
            result.Errors.Add(new EmployeeImportErrorDto(0, null, "INVALID_XML", ex.Message));
        }
        catch (Exception ex)
        {
            result.Errors.Add(new EmployeeImportErrorDto(0, null, "READ_ERROR", ex.Message));
        }

        result.TotalRows = rows.Count;
        return (rows, result);
    }

    private static Dictionary<string, int> ParseHeaders(XElement headerRow, List<string> sharedStrings)
    {
        var headers = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        var cells = headerRow.Elements(SpreadsheetNamespace + "c");

        foreach (var c in cells)
        {
            var cellRef = (string?)c.Attribute("r") ?? string.Empty;
            var colIndex = ColumnNameToIndex(ExtractColumnLetters(cellRef));
            var text = GetCellValue(c, sharedStrings).Trim();

            if (string.IsNullOrWhiteSpace(text)) continue;

            var canonical = NormalizeHeader(text);
            if (!string.IsNullOrEmpty(canonical) && !headers.ContainsKey(canonical))
            {
                headers[canonical] = colIndex;
            }
        }

        return headers;
    }

    private static string NormalizeHeader(string header)
    {
        if (string.IsNullOrWhiteSpace(header)) return string.Empty;

        // Normalize by stripping all punctuation and special characters
        var clean = Regex.Replace(header.ToLowerInvariant().Trim(), @"[^a-z0-9\u0600-\u06FF]+", "");

        if (clean.Contains("employeecode") || clean.Contains("كودالموظف") || clean.Contains("رمزالموظف") || clean.Contains("رقمالموظف"))
            return "EmployeeCode";
        if (clean.Contains("namelocal") || clean.Contains("الاسمبالعربي") || clean.Contains("الاسمعربي") || clean.Contains("اسمالموظفبالعربي") || clean.Contains("الاسمالمحلي") || clean == "الاسم")
            return "NameLocal";
        if (clean.Contains("nameen") || clean.Contains("الاسمبالإنجليزي") || clean.Contains("الاسمبالانجليزي") || clean.Contains("اسمالموظفبالإنجليزي") || clean.Contains("الاسمإنجليزي") || clean.Contains("الاسمانجليزي"))
            return "NameEn";
        if (clean.Contains("nationalid") || clean.Contains("رقمالهوية") || clean.Contains("الرقمالوطني") || clean.Contains("الرقمالقومي") || clean.Contains("رقمالوطني") || clean.Contains("الهويةالوطنية") || clean == "الهوية")
            return "NationalId";
        if (clean.Contains("nationality") || clean.Contains("الجنسية"))
            return "Nationality";
        if (clean.Contains("passportnumber") || clean.Contains("رقمالجواز") || clean.Contains("جوازالسفر"))
            return "PassportNumber";
        if (clean.Contains("dateofbirth") || clean.Contains("تاريخالميلاد") || clean == "الميلاد")
            return "DateOfBirth";
        if (clean.Contains("gender") || clean.Contains("الجنس") || clean == "النوع")
            return "Gender";
        if (clean.Contains("maritalstatus") || clean.Contains("الحالةالاجتماعية"))
            return "MaritalStatus";
        if (clean.Contains("email") || clean.Contains("البريدالإلكتروني") || clean.Contains("البريد") || clean.Contains("الايميل"))
            return "Email";
        if (clean.Contains("phone") || clean.Contains("الهاتف") || clean.Contains("رقمالهاتف") || clean.Contains("الجوال") || clean.Contains("الموبايل"))
            return "Phone";
        if (clean.Contains("hiredate") || clean.Contains("تاريخالتعيين") || clean.Contains("تاريخالمباشرة") || clean.Contains("التعيين"))
            return "HireDate";
        if (clean.Contains("probationenddate") || clean.Contains("انتهاءالتجربة") || clean.Contains("فترةالتجربة") || clean.Contains("نهايةالتجربة"))
            return "ProbationEndDate";
        if (clean.Contains("terminationdate") || clean.Contains("إنهاءالخدمة") || clean.Contains("انهاءالخدمة"))
            return "TerminationDate";
        if (clean.Contains("terminationreason") || clean.Contains("سببإنهاءالخدمة") || clean.Contains("سببانهاءالخدمة"))
            return "TerminationReason";
        if (clean.Contains("employmenttype") || clean.Contains("نوعالتوظيف") || clean.Contains("نوعالدوام"))
            return "EmploymentType";
        if (clean.Contains("employmentstatus") || clean.Contains("حالةالعمل") || clean.Contains("حالةالموظف") || clean.Contains("الحالة"))
            return "EmploymentStatus";
        if (clean.Contains("departmentcode") || clean.Contains("كودالقسم") || clean.Contains("رمزالقسم") || clean == "القسم")
            return "DepartmentCode";
        if (clean.Contains("positioncode") || clean.Contains("كودالوظيفة") || clean.Contains("المسمىالوظيفي") || clean.Contains("الوظيفة"))
            return "PositionCode";
        if (clean.Contains("branchid") || clean.Contains("معرفالفرع") || clean.Contains("رقمالفرع") || clean == "الفرع")
            return "BranchId";
        if (clean.Contains("manageremployeecode") || clean.Contains("managercode") || clean.Contains("المديرالمباشر") || clean.Contains("كودالمدير"))
            return "ManagerEmployeeCode";
        if (clean.Contains("sscnumber") || clean.Contains("رقمالضمان") || clean.Contains("الضمانالاجتماعي") || clean == "الضمان")
            return "SscNumber";
        if (clean.Contains("taxexemption") || clean.Contains("الإعفاءات") || clean.Contains("الاعفاءات"))
            return "TaxExemptionCount";
        if (clean.Contains("highrisk") || clean.Contains("مهنةخطرة") || clean.Contains("وظيفةخطرة"))
            return "IsHighRiskRole";
        if (clean.Contains("bankname") || clean.Contains("اسمالبنك") || clean == "البنك")
            return "BankName";
        if (clean.Contains("bankaccountnumber") || clean.Contains("رقمالحساب"))
            return "BankAccountNumber";
        if (clean.Contains("bankiban") || clean.Contains("iban") || clean.Contains("الآيبان") || clean.Contains("الايبان"))
            return "BankIban";
        if (clean.Contains("basicsalary") || clean.Contains("الراتبالأساسي") || clean.Contains("الراتبالاساسي") || clean == "الراتب")
            return "BasicSalary";
        if (clean.Contains("isactive") || clean == "نشط" || clean.Contains("الحالةالنشطة"))
            return "IsActive";

        return string.Empty;
    }

    private static Dictionary<int, string> ParseRowCells(XElement row, List<string> sharedStrings)
    {
        var dict = new Dictionary<int, string>();
        foreach (var c in row.Elements(SpreadsheetNamespace + "c"))
        {
            var cellRef = (string?)c.Attribute("r") ?? string.Empty;
            var colIndex = ColumnNameToIndex(ExtractColumnLetters(cellRef));
            var val = GetCellValue(c, sharedStrings);
            dict[colIndex] = val;
        }
        return dict;
    }

    private static EmployeeImportRowDto? ParseEmployeeRow(
        Dictionary<int, string> cells,
        Dictionary<string, int> headers,
        int rowNumber,
        EmployeeImportResultDto result)
    {
        string GetVal(string headerKey)
        {
            if (headers.TryGetValue(headerKey, out var colIdx) && cells.TryGetValue(colIdx, out var val))
            {
                return val.Trim();
            }
            return string.Empty;
        }

        var empCode = GetVal("EmployeeCode");
        var nameLocal = GetVal("NameLocal");
        var nameEn = GetVal("NameEn");
        var nationalId = GetVal("NationalId");

        var hasError = false;

        if (string.IsNullOrWhiteSpace(empCode))
        {
            result.Errors.Add(new EmployeeImportErrorDto(rowNumber, "EmployeeCode", "REQUIRED", "EmployeeCode is required."));
            hasError = true;
        }

        if (string.IsNullOrWhiteSpace(nameLocal))
        {
            result.Errors.Add(new EmployeeImportErrorDto(rowNumber, "NameLocal", "REQUIRED", "NameLocal (Arabic name) is required."));
            hasError = true;
        }

        if (string.IsNullOrWhiteSpace(nationalId))
        {
            result.Errors.Add(new EmployeeImportErrorDto(rowNumber, "NationalId", "REQUIRED", "NationalId is required."));
            hasError = true;
        }

        // Dates
        var dobStr = GetVal("DateOfBirth");
        DateTime? dob = null;
        if (!string.IsNullOrWhiteSpace(dobStr))
        {
            if (TryParseDate(dobStr, out var d)) dob = d;
            else
            {
                result.Errors.Add(new EmployeeImportErrorDto(rowNumber, "DateOfBirth", "INVALID_DATE", $"Invalid DateOfBirth format: '{dobStr}'."));
                hasError = true;
            }
        }
        else
        {
            dob = new DateTime(1990, 1, 1);
        }

        var hireDateStr = GetVal("HireDate");
        DateTime? hireDate = null;
        if (!string.IsNullOrWhiteSpace(hireDateStr))
        {
            if (TryParseDate(hireDateStr, out var d)) hireDate = d;
            else
            {
                result.Errors.Add(new EmployeeImportErrorDto(rowNumber, "HireDate", "INVALID_DATE", $"Invalid HireDate format: '{hireDateStr}'."));
                hasError = true;
            }
        }
        else
        {
            hireDate = DateTime.UtcNow.Date;
        }

        var probEndStr = GetVal("ProbationEndDate");
        DateTime? probEnd = null;
        if (!string.IsNullOrWhiteSpace(probEndStr) && TryParseDate(probEndStr, out var pe))
        {
            probEnd = pe;
        }

        var termDateStr = GetVal("TerminationDate");
        DateTime? termDate = null;
        if (!string.IsNullOrWhiteSpace(termDateStr) && TryParseDate(termDateStr, out var td))
        {
            termDate = td;
        }

        // Gender & Marital Status
        var genderStr = GetVal("Gender");
        var gender = ParseGender(genderStr);

        var maritalStr = GetVal("MaritalStatus");
        var maritalStatus = ParseMaritalStatus(maritalStr);

        // Employment type and status
        var empTypeStr = GetVal("EmploymentType");
        var empType = string.IsNullOrWhiteSpace(empTypeStr) ? "FULL_TIME" : NormalizeEmploymentType(empTypeStr);

        var empStatusStr = GetVal("EmploymentStatus");
        var empStatus = string.IsNullOrWhiteSpace(empStatusStr) ? "ACTIVE" : NormalizeEmploymentStatus(empStatusStr);

        // BranchId
        var branchStr = GetVal("BranchId");
        long? branchId = null;
        if (!string.IsNullOrWhiteSpace(branchStr) && long.TryParse(branchStr, out var bId))
        {
            branchId = bId;
        }

        // Tax exemption count
        var taxExStr = GetVal("TaxExemptionCount");
        var taxCount = int.TryParse(taxExStr, out var tc) ? tc : 0;

        // IsHighRiskRole
        var isHighRiskStr = GetVal("IsHighRiskRole");
        var isHighRisk = ParseBool(isHighRiskStr, defaultValue: false);

        // Basic Salary
        var salaryStr = GetVal("BasicSalary");
        decimal? basicSalary = null;
        if (!string.IsNullOrWhiteSpace(salaryStr) && decimal.TryParse(salaryStr, NumberStyles.Any, CultureInfo.InvariantCulture, out var sal))
        {
            basicSalary = sal;
        }

        // IsActive
        var activeStr = GetVal("IsActive");
        var isActive = ParseBool(activeStr, defaultValue: true);

        if (hasError)
        {
            return null;
        }

        return new EmployeeImportRowDto
        {
            RowNumber = rowNumber,
            EmployeeCode = empCode.Trim().ToUpperInvariant(),
            NameLocal = nameLocal.Trim(),
            NameEn = string.IsNullOrWhiteSpace(nameEn) ? nameLocal.Trim() : nameEn.Trim(),
            NationalId = nationalId.Trim(),
            Nationality = string.IsNullOrWhiteSpace(GetVal("Nationality")) ? "Jordanian" : GetVal("Nationality"),
            PassportNumber = string.IsNullOrWhiteSpace(GetVal("PassportNumber")) ? null : GetVal("PassportNumber"),
            DateOfBirth = dob,
            Gender = gender,
            MaritalStatus = maritalStatus,
            Email = string.IsNullOrWhiteSpace(GetVal("Email")) ? null : GetVal("Email"),
            Phone = string.IsNullOrWhiteSpace(GetVal("Phone")) ? null : GetVal("Phone"),
            HireDate = hireDate,
            ProbationEndDate = probEnd,
            TerminationDate = termDate,
            TerminationReason = string.IsNullOrWhiteSpace(GetVal("TerminationReason")) ? null : GetVal("TerminationReason"),
            EmploymentType = empType,
            EmploymentStatus = empStatus,
            DepartmentCode = string.IsNullOrWhiteSpace(GetVal("DepartmentCode")) ? null : GetVal("DepartmentCode"),
            PositionCode = string.IsNullOrWhiteSpace(GetVal("PositionCode")) ? null : GetVal("PositionCode"),
            BranchId = branchId,
            ManagerEmployeeCode = string.IsNullOrWhiteSpace(GetVal("ManagerEmployeeCode")) ? null : GetVal("ManagerEmployeeCode"),
            SscNumber = string.IsNullOrWhiteSpace(GetVal("SscNumber")) ? null : GetVal("SscNumber"),
            TaxExemptionCount = taxCount,
            IsHighRiskRole = isHighRisk,
            BankName = string.IsNullOrWhiteSpace(GetVal("BankName")) ? null : GetVal("BankName"),
            BankAccountNumber = string.IsNullOrWhiteSpace(GetVal("BankAccountNumber")) ? null : GetVal("BankAccountNumber"),
            BankIban = string.IsNullOrWhiteSpace(GetVal("BankIban")) ? null : GetVal("BankIban"),
            BasicSalary = basicSalary,
            IsActive = isActive
        };
    }

    private static string ParseGender(string raw)
    {
        var clean = raw.Trim().ToUpperInvariant();
        if (clean == "M" || clean == "ذكر" || clean == "MALE" || clean == "1") return "M";
        if (clean == "F" || clean == "أنثى" || clean == "انثى" || clean == "FEMALE" || clean == "2") return "F";
        return "M";
    }

    private static string ParseMaritalStatus(string raw)
    {
        var clean = raw.Trim().ToUpperInvariant();
        if (clean == "MARRIED" || clean == "متزوج" || clean == "متزوجة") return "MARRIED";
        if (clean == "DIVORCED" || clean == "مطلق" || clean == "مطلقة") return "DIVORCED";
        if (clean == "WIDOWED" || clean == "أرمل" || clean == "ارمل" || clean == "أرملة") return "WIDOWED";
        return "SINGLE";
    }

    private static string NormalizeEmploymentType(string raw)
    {
        var clean = raw.Trim().ToUpperInvariant();
        if (clean.Contains("PART") || clean.Contains("جزئي")) return "PART_TIME";
        if (clean.Contains("CONTRACT") || clean.Contains("عقد")) return "CONTRACT";
        if (clean.Contains("INTERN") || clean.Contains("تدريب")) return "INTERN";
        return "FULL_TIME";
    }

    private static string NormalizeEmploymentStatus(string raw)
    {
        var clean = raw.Trim().ToUpperInvariant();
        if (clean.Contains("SUSPEND") || clean.Contains("معلق") || clean.Contains("موقوف")) return "SUSPENDED";
        if (clean.Contains("TERMINAT") || clean.Contains("منهي") || clean.Contains("مفصول")) return "TERMINATED";
        return "ACTIVE";
    }

    private static bool ParseBool(string raw, bool defaultValue)
    {
        if (string.IsNullOrWhiteSpace(raw)) return defaultValue;
        var clean = raw.Trim().ToLowerInvariant();
        if (clean is "1" or "true" or "yes" or "y" or "نعم" or "صح") return true;
        if (clean is "0" or "false" or "no" or "n" or "لا" or "خطأ") return false;
        return defaultValue;
    }

    private static bool TryParseDate(string val, out DateTime date)
    {
        date = default;
        if (string.IsNullOrWhiteSpace(val)) return false;

        // Check if numeric serial (Excel OA date)
        if (double.TryParse(val, NumberStyles.Any, CultureInfo.InvariantCulture, out var oaDate) && oaDate > 1000 && oaDate < 100000)
        {
            try
            {
                date = DateTime.FromOADate(oaDate);
                return true;
            }
            catch
            {
                // Fallback to text parsing
            }
        }

        string[] formats =
        {
            "yyyy-MM-dd",
            "yyyy/MM/dd",
            "dd-MM-yyyy",
            "dd/MM/yyyy",
            "d/M/yyyy",
            "d-M-yyyy",
            "yyyy-MM-ddTHH:mm:ss",
            "yyyy-MM-ddTHH:mm:ss.fffZ",
            "MM/dd/yyyy"
        };

        if (DateTime.TryParseExact(val.Trim(), formats, CultureInfo.InvariantCulture, DateTimeStyles.None, out date))
        {
            return true;
        }

        return DateTime.TryParse(val.Trim(), CultureInfo.CurrentCulture, DateTimeStyles.None, out date);
    }

    private static string GetCellValue(XElement cell, List<string> sharedStrings)
    {
        var type = (string?)cell.Attribute("t");
        var valElem = cell.Element(SpreadsheetNamespace + "v");
        var isElem = cell.Element(SpreadsheetNamespace + "is");

        // Inline string
        if (isElem != null)
        {
            return isElem.Element(SpreadsheetNamespace + "t")?.Value ?? string.Empty;
        }

        var val = valElem?.Value ?? string.Empty;

        // Shared string
        if (string.Equals(type, "s", StringComparison.OrdinalIgnoreCase) &&
            int.TryParse(val, out var strIdx) &&
            strIdx >= 0 && strIdx < sharedStrings.Count)
        {
            return sharedStrings[strIdx];
        }

        return val;
    }

    private static string ExtractColumnLetters(string cellRef)
    {
        var match = Regex.Match(cellRef, @"^[A-Za-z]+");
        return match.Success ? match.Value.ToUpperInvariant() : "A";
    }

    private static int ColumnNameToIndex(string columnName)
    {
        var index = 0;
        foreach (var c in columnName)
        {
            index = (index * 26) + (c - 'A' + 1);
        }
        return index;
    }

    private static async Task<XDocument?> LoadXmlEntryAsync(ZipArchive archive, string entryPath, CancellationToken cancellationToken)
    {
        var entry = archive.GetEntry(entryPath) ?? archive.Entries.FirstOrDefault(e => string.Equals(e.FullName, entryPath, StringComparison.OrdinalIgnoreCase));
        if (entry == null) return null;

        await using var stream = entry.Open();
        return await XDocument.LoadAsync(stream, LoadOptions.None, cancellationToken);
    }

    private static async Task<List<string>> LoadSharedStringsAsync(ZipArchive archive, CancellationToken cancellationToken)
    {
        var list = new List<string>();
        var entry = archive.GetEntry("xl/sharedStrings.xml") ?? archive.Entries.FirstOrDefault(e => string.Equals(e.FullName, "xl/sharedStrings.xml", StringComparison.OrdinalIgnoreCase));
        if (entry == null) return list;

        await using var stream = entry.Open();
        var doc = await XDocument.LoadAsync(stream, LoadOptions.None, cancellationToken);
        var siElements = doc.Descendants(SpreadsheetNamespace + "si");

        foreach (var si in siElements)
        {
            var t = si.Element(SpreadsheetNamespace + "t");
            if (t != null)
            {
                list.Add(t.Value);
            }
            else
            {
                // Rich text runs
                var fullText = string.Concat(si.Elements(SpreadsheetNamespace + "r")
                    .Select(r => r.Element(SpreadsheetNamespace + "t")?.Value ?? string.Empty));
                list.Add(fullText);
            }
        }

        return list;
    }
}
