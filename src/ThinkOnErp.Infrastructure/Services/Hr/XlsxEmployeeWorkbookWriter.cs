using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Security;
using System.Text;
using ThinkOnErp.Application.DTOs.Hr;
using ThinkOnErp.Application.Services.Hr;

namespace ThinkOnErp.Infrastructure.Services.Hr;

public sealed class XlsxEmployeeWorkbookWriter : IXlsxEmployeeWorkbookWriter
{
    private static readonly (string Key, string HeaderText, int Width)[] Columns =
    {
        ("EmployeeCode", "كود الموظف (EmployeeCode)", 18),
        ("NameLocal", "الاسم بالعربي (NameLocal)", 24),
        ("NameEn", "الاسم بالإنجليزي (NameEn)", 24),
        ("NationalId", "رقم الهوية / الوطني (NationalId)", 20),
        ("Nationality", "الجنسية (Nationality)", 16),
        ("PassportNumber", "رقم الجواز (PassportNumber)", 18),
        ("DateOfBirth", "تاريخ الميلاد (DateOfBirth)", 16),
        ("Gender", "الجنس (Gender: M/F)", 14),
        ("MaritalStatus", "الحالة الاجتماعية (MaritalStatus)", 18),
        ("Email", "البريد الإلكتروني (Email)", 26),
        ("Phone", "رقم الهاتف (Phone)", 18),
        ("HireDate", "تاريخ التعيين (HireDate)", 16),
        ("ProbationEndDate", "نهاية التجربة (ProbationEndDate)", 16),
        ("TerminationDate", "تاريخ إنهاء الخدمة (TerminationDate)", 16),
        ("TerminationReason", "سبب إنهاء الخدمة (TerminationReason)", 22),
        ("EmploymentType", "نوع التوظيف (EmploymentType)", 18),
        ("EmploymentStatus", "حالة العمل (EmploymentStatus)", 16),
        ("DepartmentCode", "كود القسم (DepartmentCode)", 16),
        ("PositionCode", "كود الوظيفة (PositionCode)", 16),
        ("BranchId", "معرف الفرع (BranchId)", 14),
        ("ManagerEmployeeCode", "كود المدير (ManagerCode)", 16),
        ("SscNumber", "رقم الضمان (SscNumber)", 18),
        ("TaxExemptionCount", "عدد الإعفاءات (TaxExemptions)", 15),
        ("IsHighRiskRole", "مهنة خطرة (IsHighRisk: true/false)", 16),
        ("BankName", "اسم البنك (BankName)", 18),
        ("BankAccountNumber", "رقم الحساب (BankAccountNumber)", 20),
        ("BankIban", "الآيبان (BankIban)", 30),
        ("BasicSalary", "الراتب الأساسي (BasicSalary)", 16),
        ("IsActive", "نشط (IsActive: true/false)", 14)
    };

    public byte[] WriteExportWorkbook(IReadOnlyList<EmployeeExportRowDto> employees)
    {
        using var memoryStream = new MemoryStream();
        using (var archive = new ZipArchive(memoryStream, ZipArchiveMode.Create, leaveOpen: true))
        {
            AddContentTypes(archive);
            AddRels(archive);
            AddWorkbookRels(archive);
            AddWorkbook(archive);
            AddStyles(archive);
            AddExportSheet(archive, employees);
        }

        return memoryStream.ToArray();
    }

    public byte[] WriteTemplateWorkbook()
    {
        using var memoryStream = new MemoryStream();
        using (var archive = new ZipArchive(memoryStream, ZipArchiveMode.Create, leaveOpen: true))
        {
            AddContentTypes(archive);
            AddRels(archive);
            AddWorkbookRels(archive);
            AddWorkbook(archive);
            AddStyles(archive);
            AddTemplateSheet(archive);
        }

        return memoryStream.ToArray();
    }

    private static void AddContentTypes(ZipArchive archive)
    {
        var entry = archive.CreateEntry("[Content_Types].xml", CompressionLevel.Fastest);
        using var writer = new StreamWriter(entry.Open(), Encoding.UTF8);
        writer.Write(@"<?xml version=""1.0"" encoding=""UTF-8"" standalone=""yes""?>
<Types xmlns=""http://schemas.openxmlformats.org/package/2006/content-types"">
  <Default Extension=""rels"" ContentType=""application/vnd.openxmlformats-package.relationships+xml""/>
  <Default Extension=""xml"" ContentType=""application/xml""/>
  <Override PartName=""/xl/workbook.xml"" ContentType=""application/vnd.openxmlformats-officedocument.spreadsheetml.sheet.main+xml""/>
  <Override PartName=""/xl/worksheets/sheet1.xml"" ContentType=""application/vnd.openxmlformats-officedocument.spreadsheetml.worksheet+xml""/>
  <Override PartName=""/xl/styles.xml"" ContentType=""application/vnd.openxmlformats-officedocument.spreadsheetml.styles+xml""/>
</Types>");
    }

    private static void AddRels(ZipArchive archive)
    {
        var entry = archive.CreateEntry("_rels/.rels", CompressionLevel.Fastest);
        using var writer = new StreamWriter(entry.Open(), Encoding.UTF8);
        writer.Write(@"<?xml version=""1.0"" encoding=""UTF-8"" standalone=""yes""?>
<Relationships xmlns=""http://schemas.openxmlformats.org/package/2006/relationships"">
  <Relationship Id=""rId1"" Type=""http://schemas.openxmlformats.org/officeDocument/2006/relationships/officeDocument"" Target=""xl/workbook.xml""/>
</Relationships>");
    }

    private static void AddWorkbookRels(ZipArchive archive)
    {
        var entry = archive.CreateEntry("xl/_rels/workbook.xml.rels", CompressionLevel.Fastest);
        using var writer = new StreamWriter(entry.Open(), Encoding.UTF8);
        writer.Write(@"<?xml version=""1.0"" encoding=""UTF-8"" standalone=""yes""?>
<Relationships xmlns=""http://schemas.openxmlformats.org/package/2006/relationships"">
  <Relationship Id=""rId1"" Type=""http://schemas.openxmlformats.org/officeDocument/2006/relationships/worksheet"" Target=""worksheets/sheet1.xml""/>
  <Relationship Id=""rId2"" Type=""http://schemas.openxmlformats.org/officeDocument/2006/relationships/styles"" Target=""styles.xml""/>
</Relationships>");
    }

    private static void AddWorkbook(ZipArchive archive)
    {
        var entry = archive.CreateEntry("xl/workbook.xml", CompressionLevel.Fastest);
        using var writer = new StreamWriter(entry.Open(), Encoding.UTF8);
        writer.Write(@"<?xml version=""1.0"" encoding=""UTF-8"" standalone=""yes""?>
<workbook xmlns=""http://schemas.openxmlformats.org/spreadsheetml/2006/main"" xmlns:r=""http://schemas.openxmlformats.org/officeDocument/2006/relationships"">
  <sheets>
    <sheet name=""الموظفون"" sheetId=""1"" r:id=""rId1""/>
  </sheets>
</workbook>");
    }

    private static void AddStyles(ZipArchive archive)
    {
        var entry = archive.CreateEntry("xl/styles.xml", CompressionLevel.Fastest);
        using var writer = new StreamWriter(entry.Open(), Encoding.UTF8);
        // Style 0: Regular
        // Style 1: Header (Navy #1E293B, Bold White Text)
        // Style 2: Sample/Guide Row (Light Blue #F0F9FF, Italic Gray Text)
        writer.Write(@"<?xml version=""1.0"" encoding=""UTF-8"" standalone=""yes""?>
<styleSheet xmlns=""http://schemas.openxmlformats.org/spreadsheetml/2006/main"">
  <fonts count=""3"">
    <font><name val=""Segoe UI""/><sz val=""10""/></font>
    <font><b/><color rgb=""FFFFFFFF""/><name val=""Segoe UI""/><sz val=""11""/></font>
    <font><i/><color rgb=""FF475569""/><name val=""Segoe UI""/><sz val=""10""/></font>
  </fonts>
  <fills count=""4"">
    <fill><patternFill patternType=""none""/></fill>
    <fill><patternFill patternType=""gray125""/></fill>
    <fill><patternFill patternType=""solid""><fgColor rgb=""FF1E293B""/></patternFill></fill>
    <fill><patternFill patternType=""solid""><fgColor rgb=""FFF0F9FF""/></patternFill></fill>
  </fills>
  <borders count=""2"">
    <border><left/><right/><top/><bottom/></border>
    <border>
      <left style=""thin""><color rgb=""FFCBD5E1""/></left>
      <right style=""thin""><color rgb=""FFCBD5E1""/></right>
      <top style=""thin""><color rgb=""FFCBD5E1""/></top>
      <bottom style=""thin""><color rgb=""FFCBD5E1""/></bottom>
    </border>
  </borders>
  <cellStyleXfs count=""1"">
    <xf numFmtId=""0"" fontId=""0"" fillId=""0"" borderId=""0""/>
  </cellStyleXfs>
  <cellXfs count=""3"">
    <xf numFmtId=""0"" fontId=""0"" fillId=""0"" borderId=""1"" xfId=""0"" applyBorder=""1""/>
    <xf numFmtId=""0"" fontId=""1"" fillId=""2"" borderId=""1"" xfId=""0"" applyFont=""1"" applyFill=""1"" applyBorder=""1""/>
    <xf numFmtId=""0"" fontId=""2"" fillId=""3"" borderId=""1"" xfId=""0"" applyFont=""1"" applyFill=""1"" applyBorder=""1""/>
  </cellXfs>
</styleSheet>");
    }

    private static void AddExportSheet(ZipArchive archive, IReadOnlyList<EmployeeExportRowDto> employees)
    {
        var entry = archive.CreateEntry("xl/worksheets/sheet1.xml", CompressionLevel.Fastest);
        using var writer = new StreamWriter(entry.Open(), Encoding.UTF8);

        writer.Write(@"<?xml version=""1.0"" encoding=""UTF-8"" standalone=""yes""?>
<worksheet xmlns=""http://schemas.openxmlformats.org/spreadsheetml/2006/main"">");

        // Columns width
        writer.Write("<cols>");
        for (var i = 0; i < Columns.Length; i++)
        {
            var colNum = i + 1;
            writer.Write($@"<col min=""{colNum}"" max=""{colNum}"" width=""{Columns[i].Width}"" customWidth=""1""/>");
        }
        writer.Write("</cols>");

        writer.Write("<sheetData>");

        // Header Row (Row 1)
        writer.Write(@"<row r=""1"" ht=""28"" customHeight=""1"">");
        for (var i = 0; i < Columns.Length; i++)
        {
            var cellRef = GetCellReference(i, 1);
            var headerVal = SecurityElement.Escape(Columns[i].HeaderText);
            writer.Write($@"<c r=""{cellRef}"" s=""1"" t=""inlineStr""><is><t>{headerVal}</t></is></c>");
        }
        writer.Write("</row>");

        // Data Rows
        var rowIdx = 2;
        foreach (var emp in employees)
        {
            writer.Write($@"<row r=""{rowIdx}"" ht=""20"" customHeight=""1"">");

            var values = new[]
            {
                emp.EmployeeCode,
                emp.NameLocal,
                emp.NameEn,
                emp.NationalId,
                emp.Nationality,
                emp.PassportNumber ?? string.Empty,
                emp.DateOfBirth.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                emp.Gender,
                emp.MaritalStatus,
                emp.Email ?? string.Empty,
                emp.Phone ?? string.Empty,
                emp.HireDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                emp.ProbationEndDate?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) ?? string.Empty,
                emp.TerminationDate?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) ?? string.Empty,
                emp.TerminationReason ?? string.Empty,
                emp.EmploymentType,
                emp.EmploymentStatus,
                emp.DepartmentCode ?? string.Empty,
                emp.PositionCode ?? string.Empty,
                emp.BranchId?.ToString(CultureInfo.InvariantCulture) ?? string.Empty,
                emp.ManagerEmployeeCode ?? string.Empty,
                emp.SscNumber ?? string.Empty,
                emp.TaxExemptionCount.ToString(CultureInfo.InvariantCulture),
                emp.IsHighRiskRole ? "true" : "false",
                emp.BankName ?? string.Empty,
                emp.BankAccountNumber ?? string.Empty,
                emp.BankIban ?? string.Empty,
                emp.BasicSalary?.ToString("F2", CultureInfo.InvariantCulture) ?? string.Empty,
                emp.IsActive ? "true" : "false"
            };

            for (var c = 0; c < values.Length; c++)
            {
                var cellRef = GetCellReference(c, rowIdx);
                var cellVal = SecurityElement.Escape(values[c]);
                writer.Write($@"<c r=""{cellRef}"" s=""0"" t=""inlineStr""><is><t>{cellVal}</t></is></c>");
            }

            writer.Write("</row>");
            rowIdx++;
        }

        writer.Write("</sheetData></worksheet>");
    }

    private static void AddTemplateSheet(ZipArchive archive)
    {
        var entry = archive.CreateEntry("xl/worksheets/sheet1.xml", CompressionLevel.Fastest);
        using var writer = new StreamWriter(entry.Open(), Encoding.UTF8);

        writer.Write(@"<?xml version=""1.0"" encoding=""UTF-8"" standalone=""yes""?>
<worksheet xmlns=""http://schemas.openxmlformats.org/spreadsheetml/2006/main"">");

        // Columns width
        writer.Write("<cols>");
        for (var i = 0; i < Columns.Length; i++)
        {
            var colNum = i + 1;
            writer.Write($@"<col min=""{colNum}"" max=""{colNum}"" width=""{Columns[i].Width}"" customWidth=""1""/>");
        }
        writer.Write("</cols>");

        writer.Write("<sheetData>");

        // Header Row (Row 1)
        writer.Write(@"<row r=""1"" ht=""28"" customHeight=""1"">");
        for (var i = 0; i < Columns.Length; i++)
        {
            var cellRef = GetCellReference(i, 1);
            var headerVal = SecurityElement.Escape(Columns[i].HeaderText);
            writer.Write($@"<c r=""{cellRef}"" s=""1"" t=""inlineStr""><is><t>{headerVal}</t></is></c>");
        }
        writer.Write("</row>");

        // Sample Row (Row 2) - Guide for user
        writer.Write(@"<row r=""2"" ht=""22"" customHeight=""1"">");
        var sampleValues = new[]
        {
            "EMP001",
            "محمد أحمد خليل",
            "Mohammad Ahmad Khalil",
            "9901020304",
            "Jordanian",
            "P1234567",
            "1990-05-15",
            "M",
            "SINGLE",
            "mohammad.ahmad@example.com",
            "+962791234567",
            "2024-01-01",
            "2024-04-01",
            "",
            "",
            "FULL_TIME",
            "ACTIVE",
            "HR",
            "HR_SPEC",
            "1",
            "",
            "987654321",
            "0",
            "false",
            "Arab Bank",
            "123456789",
            "JO94ARAB1234567890123456789012",
            "750.00",
            "true"
        };

        for (var c = 0; c < sampleValues.Length; c++)
        {
            var cellRef = GetCellReference(c, 2);
            var cellVal = SecurityElement.Escape(sampleValues[c]);
            writer.Write($@"<c r=""{cellRef}"" s=""2"" t=""inlineStr""><is><t>{cellVal}</t></is></c>");
        }
        writer.Write("</row>");

        writer.Write("</sheetData></worksheet>");
    }

    private static string GetCellReference(int zeroBasedColIndex, int rowNumber)
    {
        var colLetters = string.Empty;
        var temp = zeroBasedColIndex;
        while (temp >= 0)
        {
            colLetters = (char)('A' + (temp % 26)) + colLetters;
            temp = (temp / 26) - 1;
        }
        return $"{colLetters}{rowNumber}";
    }
}
