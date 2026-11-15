using System.IO.Compression;
using System.Xml.Linq;
using ThinkOnErp.Infrastructure.Services.Hr;
using Microsoft.Extensions.Logging;
using Moq;
using ThinkOnErp.Application.DTOs.Hr;
using ThinkOnErp.Application.Services.Hr;
using ThinkOnErp.Domain.Entities.Hr;
using ThinkOnErp.Domain.Interfaces.Hr;
using Xunit;
namespace ThinkOnErp.HR.Tests;
public class EmployeeInputValidationTests
{
    private static CreateEmployeeDto Input() => new(" e1 ", " Local ", " Employee ", " N1 ", " X ", null, new(1990,1,1), "m", "single", null, null,
        new(2020,1,1), null, "CUSTOM_CONTRACT", "active", null, null, 7, null, null, 0, false, null, null, null);
    [Fact] public void NormalizeDoesNotImposeCountryIdentityOrContractFormat()
    {
        var value = EmployeeInputValidation.Normalize(Input() with { NationalId = " A-123/foreign ", Nationality = " Custom nationality ", Email = " person@example.test " });
        Assert.Equal("E1", value.EmployeeCode); Assert.Equal("A-123/foreign", value.NationalId); Assert.Equal("CUSTOM_CONTRACT", value.EmploymentType);
        Assert.Equal("Custom nationality", value.Nationality); Assert.Equal("person@example.test", value.Email);
    }
    [Fact] public void CitizenshipKeyIsOptionalExplicitNormalizedAndNeverInferredFromNationality()
    {
        var value = EmployeeInputValidation.Normalize(Input() with { Nationality = "Some country name", CitizenshipCountryCode = " jo " });
        Assert.Equal("JO", value.CitizenshipCountryCode);
        Assert.Null(EmployeeInputValidation.Normalize(Input() with { Nationality = "Jordanian" }).CitizenshipCountryCode);
        Assert.Throws<ArgumentException>(() => EmployeeInputValidation.Normalize(Input() with { CitizenshipCountryCode = "J" }));
    }
    [Theory]
    [InlineData(0)] [InlineData(1)] [InlineData(2)] [InlineData(3)] [InlineData(4)] [InlineData(5)] [InlineData(6)] [InlineData(7)] [InlineData(8)] [InlineData(9)]
    public void InvalidProfileIsRejected(int scenario)
    {
        var value = scenario switch { 0 => Input() with { NationalId = " " }, 1 => Input() with { NameLocal = " " }, 2 => Input() with { HireDate = default },
            3 => Input() with { DateOfBirth = DateTime.UtcNow.AddYears(2) }, 4 => Input() with { HireDate = new(1980,1,1) },
            5 => Input() with { ProbationEndDate = new(2019,1,1) }, 6 => Input() with { EmploymentStatus = "UNKNOWN" },
            7 => Input() with { TaxExemptionCount = -1 }, 8 => Input() with { Email = "Display <person@example.test>" }, _ => Input() with { BranchId = -7 } };
        Assert.Throws<ArgumentException>(() => EmployeeInputValidation.Normalize(value));
    }
    [Fact] public void EmployeeCreationCannotBypassEndOfServiceTermination()
    {
        Assert.Throws<ArgumentException>(() => EmployeeInputValidation.Normalize(Input() with { EmploymentStatus = "TERMINATED" }));
        Assert.Throws<ArgumentException>(() => EmployeeInputValidation.Normalize(Input() with
        { EmploymentStatus = "terminated", TerminationDate = new(2022,1,1), TerminationReason = " Contract ended " }));
        Assert.Throws<ArgumentException>(() => EmployeeInputValidation.Normalize(Input() with { TerminationDate = new(2022,1,1) }));
    }
    [Fact] public void FutureHireAndLongProbationAreNotLimitedByCountryRules()
    {
        var value = EmployeeInputValidation.Normalize(Input() with { HireDate = new(2035,1,1), ProbationEndDate = new(2040,1,1) });
        Assert.Equal(2035, value.HireDate.Year);
    }
    [Fact] public void NewImportCannotInventBirthHireOrNationality()
    {
        var row = Row(); row.DateOfBirth = null; Assert.Throws<ArgumentException>(() => EmployeeInputValidation.NormalizeImportRow(row, null));
        row = Row(); row.HireDate = null; Assert.Throws<ArgumentException>(() => EmployeeInputValidation.NormalizeImportRow(row, null));
        row = Row(); row.Nationality = ""; Assert.Throws<ArgumentException>(() => EmployeeInputValidation.NormalizeImportRow(row, null));
    }
    [Fact] public void ExistingImportPreservesMissingDatesAndNationality()
    {
        var row = Row(); row.DateOfBirth = null; row.HireDate = null; row.Nationality = "";
        EmployeeInputValidation.NormalizeImportRow(row, new Employee { DateOfBirth = new(1980,1,1), HireDate = new(2010,1,1), Nationality = "OTHER", CitizenshipCountryCode = "US" });
        Assert.Equal(new DateTime(1980,1,1), row.DateOfBirth); Assert.Equal(new DateTime(2010,1,1), row.HireDate); Assert.Equal("OTHER", row.Nationality);
        Assert.Equal("US", row.CitizenshipCountryCode);
    }
    [Fact] public void ImportCannotCreateOrTerminateEmployeeOutsideEndOfService()
    {
        var create = Row(); create.EmploymentStatus = "TERMINATED"; create.TerminationDate = new(2026, 1, 1);
        Assert.Throws<ArgumentException>(() => EmployeeInputValidation.NormalizeImportRow(create, null));

        var previous = new Employee { EmployeeCode = "E1", EmploymentStatus = "ACTIVE", IsActive = true,
            NameLocal = "Local", NameEn = "Employee", NationalId = "N1", Nationality = "X", DateOfBirth = new(1990,1,1), HireDate = new(2020,1,1) };
        var update = Row(); update.EmploymentStatus = "TERMINATED"; update.TerminationDate = new(2026, 1, 1);
        Assert.Throws<ArgumentException>(() => EmployeeInputValidation.NormalizeImportRow(update, previous));
    }
    [Fact] public void MissingWorkbookColumnsDoNotReactivateOrEraseExistingProfile()
    {
        var previous = new Employee { EmployeeCode="E1", NameLocal="Local", NameEn="English", NationalId="N1", Nationality="X", DateOfBirth=new(1990,1,1), HireDate=new(2020,1,1),
            EmploymentStatus="SUSPENDED", EmploymentType="CUSTOM", IsActive=false, TaxExemptionCount=3, IsHighRiskRole=true, Email="old@example.test", BranchId=7 };
        var row = Row(); row.PresentColumns = new HashSet<string> { "EmployeeCode", "NameLocal", "NationalId", "NameEn", "IsActive", "TaxExemptionCount" };
        row.SuppliedFields = new HashSet<string> { "EmployeeCode", "NameLocal", "NationalId" };
        EmployeeInputValidation.NormalizeImportRow(row, previous);
        Assert.False(row.IsActive); Assert.Equal(3,row.TaxExemptionCount); Assert.True(row.IsHighRiskRole); Assert.Equal("SUSPENDED",row.EmploymentStatus);
        Assert.Equal("English", row.NameEn); Assert.Equal("CUSTOM",row.EmploymentType); Assert.Equal("old@example.test",row.Email); Assert.Equal(7,row.BranchId);
    }
    private static EmployeeImportRowDto Row() => new() { EmployeeCode = "E1", NameLocal = "Local", NameEn = "Employee", NationalId = "N1", Nationality = "X", DateOfBirth = new(1990,1,1), HireDate = new(2020,1,1) };
    [Fact] public async Task InvalidImportPreviewAndSaveUseSameRulesWithoutPersisting()
    {
        var row = Row(); row.DateOfBirth = null;
        var reader = new Mock<IXlsxEmployeeWorkbookReader>(); reader.Setup(r => r.ReadAsync(It.IsAny<Stream>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => ((IReadOnlyList<EmployeeImportRowDto>)new[] { row }, new EmployeeImportResultDto { TotalRows = 1 }));
        var repo = new Mock<IEmployeeRepository>(); var unit = new Mock<IHrUnitOfWork>();
        unit.Setup(u => u.ExecuteEmployeeImportAsync(It.IsAny<IReadOnlyList<EmployeeImportRowDto>>(), It.IsAny<Func<Task<EmployeeImportResultDto>>>(), It.IsAny<CancellationToken>()))
            .Returns((IReadOnlyList<EmployeeImportRowDto> rows, Func<Task<EmployeeImportResultDto>> action, CancellationToken ct) => action());
        var service = new EmployeeExcelService(repo.Object, reader.Object, Mock.Of<IXlsxEmployeeWorkbookWriter>(), Mock.Of<ILogger<EmployeeExcelService>>(), unit.Object);
        using var stream = new MemoryStream();
        Assert.Contains((await service.ValidateWorkbookAsync(stream)).Errors, e => e.Code == "INVALID_EMPLOYEE_PROFILE");
        Assert.Contains((await service.ImportEmployeesAsync(stream)).Errors, e => e.Code == "INVALID_EMPLOYEE_PROFILE");
        repo.Verify(r => r.AddEmployeeAsync(It.IsAny<Employee>(), It.IsAny<CancellationToken>()), Times.Never);
        repo.Verify(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }
    [Fact] public async Task WorkbookPreviewAndImportRejectTenantStorageLimitBeforeMutation()
    {
        var reader = new Mock<IXlsxEmployeeWorkbookReader>(); reader.Setup(r=>r.ReadAsync(It.IsAny<Stream>(),It.IsAny<CancellationToken>()))
            .ReturnsAsync(()=>((IReadOnlyList<EmployeeImportRowDto>)new[] { Row() },new EmployeeImportResultDto { TotalRows=1 }));
        var repository=new Mock<IEmployeeRepository>();
        repository.Setup(r=>r.ValidateStorageAsync(It.IsAny<Employee>(),It.IsAny<CancellationToken>())).ThrowsAsync(new ArgumentException("NameEn exceeds tenant storage limit."));
        var unit=new Mock<IHrUnitOfWork>(); unit.Setup(u=>u.ExecuteEmployeeImportAsync(It.IsAny<IReadOnlyList<EmployeeImportRowDto>>(),It.IsAny<Func<Task<EmployeeImportResultDto>>>(),It.IsAny<CancellationToken>()))
            .Returns((IReadOnlyList<EmployeeImportRowDto> rows,Func<Task<EmployeeImportResultDto>> action,CancellationToken ct)=>action());
        var service=new EmployeeExcelService(repository.Object,reader.Object,Mock.Of<IXlsxEmployeeWorkbookWriter>(),Mock.Of<ILogger<EmployeeExcelService>>(),unit.Object);
        using var workbook=new MemoryStream();
        Assert.Contains((await service.ValidateWorkbookAsync(workbook)).Errors,e=>e.Message.Contains("storage limit"));
        Assert.Contains((await service.ImportEmployeesAsync(workbook)).Errors,e=>e.Message.Contains("storage limit"));
        repository.Verify(r=>r.ValidateStorageAsync(It.IsAny<Employee>(),It.IsAny<CancellationToken>()),Times.Exactly(2));
        repository.Verify(r=>r.SaveChangesAsync(It.IsAny<CancellationToken>()),Times.Never);
    }
    [Fact] public async Task InvalidDirectInputIsRejectedBeforePayrollTransaction()
    {
        var unit = new Mock<IHrUnitOfWork>(MockBehavior.Strict);
        await Assert.ThrowsAsync<ArgumentException>(() => new EmployeeService(Mock.Of<IEmployeeRepository>(), unit.Object).CreateEmployeeAsync(Input() with { NationalId = "" }, "HR"));
        unit.VerifyNoOtherCalls();
    }
    [Fact] public async Task RealWorkbookReaderPreservesMissingFactsInsteadOfDefaults()
    {
        using var workbook = Workbook(new Dictionary<string,string?> { ["DateOfBirth"] = null, ["HireDate"] = null, ["Nationality"] = null, ["EmploymentType"] = "INTERNAL_PROGRAM" });
        var (rows, result) = await new XlsxEmployeeWorkbookReader().ReadAsync(workbook);
        Assert.Empty(result.Errors); var row = Assert.Single(rows);
        Assert.Null(row.DateOfBirth); Assert.Null(row.HireDate); Assert.Equal(string.Empty, row.Nationality); Assert.Equal("INTERNAL_PROGRAM", row.EmploymentType);
    }
    [Fact] public async Task RealWorkbookReaderCarriesExplicitCitizenshipCountryCode()
    {
        using var workbook = Workbook(new Dictionary<string,string?> { ["CitizenshipCountryCode"] = " jo " });
        var (rows, result) = await new XlsxEmployeeWorkbookReader().ReadAsync(workbook);
        Assert.Empty(result.Errors);
        var row = Assert.Single(rows);
        EmployeeInputValidation.NormalizeImportRow(row, null);
        Assert.Equal("JO", row.CitizenshipCountryCode);
    }
    [Theory]
    [InlineData("TerminationDate", "not-a-date", "INVALID_DATE")]
    [InlineData("BranchId", "not-a-number", "INVALID_BRANCH")]
    [InlineData("TaxExemptionCount", "bad", "INVALID_COUNT")]
    [InlineData("IsActive", "possibly", "INVALID_BOOLEAN")]
    public async Task WorkbookDoesNotSilentlyCoerceInvalidCells(string field, string value, string error)
    {
        using var workbook = Workbook(new Dictionary<string,string?> { [field] = value });
        var (_, result) = await new XlsxEmployeeWorkbookReader().ReadAsync(workbook);
        Assert.Contains(result.Errors, e => e.Column == field && e.Code == error);
    }
    private static MemoryStream Workbook(Dictionary<string,string?> changes)
    {
        var stream = new MemoryStream();
        var bytes = new XlsxEmployeeWorkbookWriter().WriteExportWorkbook(new[] { new EmployeeExportRowDto { EmployeeCode = "E1", NameLocal = "Local", NameEn = "Employee",
            NationalId = "N1", Nationality = "X", DateOfBirth = new(1990,1,1), HireDate = new(2020,1,1), BasicSalary = 500m, SalaryEffectiveFrom = new(2020,1,1) } });
        stream.Write(bytes); stream.Position = 0;
        using (var zip = new ZipArchive(stream, ZipArchiveMode.Update, true))
        {
            var entry = zip.GetEntry("xl/worksheets/sheet1.xml")!; XDocument document;
            using (var reader = entry.Open()) document = XDocument.Load(reader);
            XNamespace ns = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";
            var rows = document.Descendants(ns + "row").ToArray(); var applied = 0;
            foreach (var header in rows[0].Elements(ns + "c"))
            {
                var name = string.Concat(header.Descendants(ns + "t").Select(t => t.Value)).Replace("TaxExemptions", "TaxExemptionCount");
                var change = changes.FirstOrDefault(pair => name == pair.Key || name.Contains("(" + pair.Key + ")") || name.Contains("(" + pair.Key + ":"));
                if (change.Key == null) continue; applied++; var replacement = change.Value;
                var column = new string(header.Attribute("r")!.Value.TakeWhile(char.IsLetter).ToArray());
                var cell = rows[1].Elements(ns + "c").Single(c => c.Attribute("r")!.Value == column + "2");
                if (replacement == null) cell.Remove();
                else { cell.RemoveNodes(); cell.SetAttributeValue("t", "inlineStr"); cell.Add(new XElement(ns + "is", new XElement(ns + "t", replacement))); }
            }
            Assert.Equal(changes.Count, applied);
            entry.Delete(); using var writer = zip.CreateEntry("xl/worksheets/sheet1.xml").Open(); document.Save(writer);
        }
        stream.Position = 0; return stream;
    }

}
