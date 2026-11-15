using System.IO.Compression;
using System.Xml.Linq;
using Microsoft.Extensions.Logging;
using Moq;
using ThinkOnErp.Application.DTOs.Hr;
using ThinkOnErp.Application.Services.Hr;
using ThinkOnErp.Domain.Entities.Hr;
using ThinkOnErp.Domain.Interfaces.Hr;
using ThinkOnErp.Infrastructure.Services.Hr;
using Xunit;

namespace ThinkOnErp.HR.Tests;

public sealed class EmployeeSalaryMetadataTests
{
    private readonly Mock<IEmployeeRepository> repository=new();
    private readonly Mock<IHrUnitOfWork> unit=new();
    private static EmployeeImportRowDto Row()=>new() { RowNumber=2,EmployeeCode="E",NameLocal="Local",NameEn="English",NationalId="ID",Nationality="X",DateOfBirth=new(1990,1,1),HireDate=new(2020,1,1),BranchId=7,BasicSalary=700,SalaryEffectiveFrom=new(2026,12,1) };
    private Employee Existing(bool salary=true)
    {
        var e=new Employee { EmployeeCode="E",NameLocal="Local",NameEn="English",NationalId="ID",Nationality="X",DateOfBirth=new(1990,1,1),HireDate=new(2020,1,1),BranchId=7 };
        if(salary) e.SalaryStructures.Add(new SalaryStructure { BasicSalary=500,EffectiveFrom=e.HireDate,CurrencyCode="USD",PaymentMethod="CASH" });
        repository.Setup(r=>r.GetEmployeeByCodeAsync("E",It.IsAny<CancellationToken>())).ReturnsAsync(e); return e;
    }
    public EmployeeSalaryMetadataTests()
    {
        unit.Setup(u=>u.ExecuteEmployeeImportAsync(It.IsAny<IReadOnlyList<EmployeeImportRowDto>>(),It.IsAny<Func<Task<EmployeeImportResultDto>>>(),It.IsAny<CancellationToken>()))
            .Returns((IReadOnlyList<EmployeeImportRowDto> rows,Func<Task<EmployeeImportResultDto>> action,CancellationToken ct)=>action());
        repository.Setup(r=>r.GetImportImpactAsync(It.IsAny<string>(),It.IsAny<long?>(),It.IsAny<DateTime>(),It.IsAny<CancellationToken>())).ReturnsAsync(new EmployeeImportImpact(Array.Empty<EmployeeImportPeriodImpact>(),false));
    }
    private EmployeeExcelService Service(EmployeeImportRowDto row)
    {
        var reader=new Mock<IXlsxEmployeeWorkbookReader>(); reader.Setup(r=>r.ReadAsync(It.IsAny<Stream>(),It.IsAny<CancellationToken>()))
            .ReturnsAsync(()=>((IReadOnlyList<EmployeeImportRowDto>)new[] {row},new EmployeeImportResultDto { TotalRows=1 }));
        return new(repository.Object,reader.Object,Mock.Of<IXlsxEmployeeWorkbookWriter>(),Mock.Of<ILogger<EmployeeExcelService>>(),unit.Object);
    }
    [Theory] [InlineData(null,null)] [InlineData("USD",null)] [InlineData(null,"CASH")]
    public async Task FirstSalaryCannotInventMissingMetadata(string? currency,string? method)
    {
        var row=Row(); row.CurrencyCode=currency; row.PaymentMethod=method; var service=Service(row);
        using var stream=new MemoryStream();
        Assert.Contains((await service.ValidateWorkbookAsync(stream)).Errors,e=>e.Code=="SALARY_METADATA_REQUIRED");
        Assert.Contains((await service.ImportEmployeesAsync(stream)).Errors,e=>e.Code=="SALARY_METADATA_REQUIRED");
        repository.Verify(r=>r.SaveChangesAsync(It.IsAny<CancellationToken>()),Times.Never);
    }
    [Theory] [InlineData("cash")] [InlineData("bank_transfer")] [InlineData("cheque")]
    public async Task FirstSalaryUsesExplicitNormalizedMetadataAndHireDateWhenNoSalaryDate(string method)
    {
        var row=Row(); row.CurrencyCode=" usd "; row.PaymentMethod=" "+method+" "; row.SalaryEffectiveFrom=null;
        Employee? added=null; repository.Setup(r=>r.AddEmployeeAsync(It.IsAny<Employee>(),It.IsAny<CancellationToken>())).Callback((Employee e,CancellationToken ct)=>added=e).Returns(Task.CompletedTask);
        using var stream=new MemoryStream(); var result=await Service(row).ImportEmployeesAsync(stream);
        Assert.Empty(result.Errors); var salary=Assert.Single(added!.SalaryStructures); Assert.Equal("USD",salary.CurrencyCode); Assert.Equal(method.ToUpperInvariant(),salary.PaymentMethod); Assert.Equal(added.HireDate,salary.EffectiveFrom);
        repository.Verify(r=>r.ValidateSalaryMetadataAsync("USD",method.ToUpperInvariant(),It.IsAny<CancellationToken>()),Times.Once);
    }
    [Fact] public async Task ExistingEmployeeWithoutSalaryAlsoNeedsFirstStructureMetadata()
    {
        var e=Existing(false); using var stream=new MemoryStream(); var result=await Service(Row()).ImportEmployeesAsync(stream,true);
        Assert.Contains(result.Errors,r=>r.Code=="SALARY_METADATA_REQUIRED"); Assert.Empty(e.SalaryStructures);
    }
    [Theory] [InlineData("EUR","CASH",false)] [InlineData("USD","CHEQUE",false)] [InlineData("EUR","CASH",true)] [InlineData("USD","CHEQUE",true)]
    public async Task MetadataChangeCannotBeSilentlyIgnoredWhenAmountIsUnchanged(string currency,string method,bool sameStart)
    {
        var e=Existing(); var row=Row(); row.BasicSalary=500; row.CurrencyCode=currency; row.PaymentMethod=method; row.SalaryEffectiveFrom=sameStart?e.HireDate:null;
        using var stream=new MemoryStream(); var result=await Service(row).ImportEmployeesAsync(stream,true);
        Assert.Contains(result.Errors,r=>r.Code=="SALARY_METADATA_CHANGE_REQUIRES_ASSIGNMENT");
        Assert.Null(Assert.Single(e.SalaryStructures).EffectiveTo); Assert.Equal("USD",e.SalaryStructures[0].CurrencyCode);
        repository.Verify(r=>r.SaveChangesAsync(It.IsAny<CancellationToken>()),Times.Never);
    }
    [Fact] public async Task CurrencyValidationFailureCannotClosePreviousSalary()
    {
        var e=Existing(); repository.Setup(r=>r.ValidateSalaryMetadataAsync("USD","CASH",It.IsAny<CancellationToken>())).ThrowsAsync(new ArgumentException("Currency is not configured."));
        using var stream=new MemoryStream(); var result=await Service(Row()).ImportEmployeesAsync(stream,true);
        Assert.Contains(result.Errors,r=>r.Code=="INVALID_SALARY_METADATA"); Assert.Null(Assert.Single(e.SalaryStructures).EffectiveTo); Assert.Equal(500,e.SalaryStructures[0].BasicSalary);
    }
    [Fact] public async Task UnsupportedPaymentMethodIsRejectedBeforeRepositoryValidation()
    {
        var row=Row(); row.CurrencyCode="USD"; row.PaymentMethod="CARD";
        using var stream=new MemoryStream(); Assert.Contains((await Service(row).ImportEmployeesAsync(stream)).Errors,e=>e.Code=="INVALID_SALARY_METADATA");
        repository.Verify(r=>r.ValidateSalaryMetadataAsync(It.IsAny<string>(),It.IsAny<string>(),It.IsAny<CancellationToken>()),Times.Never);
    }
    [Fact] public async Task MetadataWithoutSalaryCannotBeIgnored()
    {
        var row=Row(); row.BasicSalary=null; row.SalaryEffectiveFrom=null; row.CurrencyCode="USD";
        using var stream=new MemoryStream(); Assert.Contains((await Service(row).ImportEmployeesAsync(stream)).Errors,e=>e.Code=="SALARY_REQUIRED");
    }
    [Fact] public async Task RealWorkbookRoundTripKeepsMetadataPrecisionAndActiveColumn()
    {
        var row=new EmployeeExportRowDto { EmployeeCode="E",NameLocal="Local",NameEn="English",NationalId="ID",Nationality="X",DateOfBirth=new(1990,1,1),HireDate=new(2020,1,1),BasicSalary=700.123m,SalaryEffectiveFrom=new(2026,12,1),CurrencyCode="USD",PaymentMethod="CHEQUE",IsActive=false };
        using var file=new MemoryStream(new XlsxEmployeeWorkbookWriter().WriteExportWorkbook(new[] {row}));
        var (rows,result)=await new XlsxEmployeeWorkbookReader().ReadAsync(file); Assert.Empty(result.Errors);
        var parsed=Assert.Single(rows); Assert.Equal("USD",parsed.CurrencyCode); Assert.Equal("CHEQUE",parsed.PaymentMethod); Assert.Equal(700.123m,parsed.BasicSalary); Assert.False(parsed.IsActive);
        using var template=new MemoryStream(new XlsxEmployeeWorkbookWriter().WriteTemplateWorkbook());
        var (samples,_)=await new XlsxEmployeeWorkbookReader().ReadAsync(template); var sample=Assert.Single(samples);
        Assert.Null(sample.CurrencyCode); Assert.Null(sample.PaymentMethod);
    }
    [Theory] [InlineData(true)] [InlineData(false)]
    public async Task OldWorkbookInheritsExistingMetadataButCannotInitializeNewSalary(bool hasPrevious)
    {
        Employee? existing=hasPrevious?Existing():null;
        var row=new EmployeeExportRowDto { EmployeeCode="E",NameLocal="Local",NameEn="English",NationalId="ID",Nationality="X",DateOfBirth=new(1990,1,1),HireDate=new(2020,1,1),BranchId=7,BasicSalary=700,SalaryEffectiveFrom=new(2026,12,1) };
        using var stream=new MemoryStream(); stream.Write(new XlsxEmployeeWorkbookWriter().WriteExportWorkbook(new[] {row}));
        using(var zip=new ZipArchive(stream,ZipArchiveMode.Update,true))
        {
            var entry=zip.GetEntry("xl/worksheets/sheet1.xml")!; XDocument doc; using(var input=entry.Open()) doc=XDocument.Load(input);
            XNamespace ns="http://schemas.openxmlformats.org/spreadsheetml/2006/main";
            var header=doc.Descendants(ns+"row").First();
            var columns=header.Elements(ns+"c").Where(c=>c.Value.Contains("CurrencyCode") || c.Value.Contains("PaymentMethod")).Select(c=>new string(c.Attribute("r")!.Value.TakeWhile(char.IsLetter).ToArray())).ToHashSet();
            Assert.Equal(2,columns.Count);
            foreach(var cell in doc.Descendants(ns+"c").Where(c=>columns.Contains(new string(c.Attribute("r")!.Value.TakeWhile(char.IsLetter).ToArray()))).ToList()) cell.Remove();
            entry.Delete(); using var output=zip.CreateEntry("xl/worksheets/sheet1.xml").Open(); doc.Save(output);
        }
        stream.Position=0;
        var service=new EmployeeExcelService(repository.Object,new XlsxEmployeeWorkbookReader(),Mock.Of<IXlsxEmployeeWorkbookWriter>(),Mock.Of<ILogger<EmployeeExcelService>>(),unit.Object);
        var result=await service.ImportEmployeesAsync(stream,true);
        if(hasPrevious) { Assert.Empty(result.Errors); Assert.Equal("USD",existing!.SalaryStructures.Last().CurrencyCode); Assert.Equal("CASH",existing.SalaryStructures.Last().PaymentMethod); }
        else Assert.Contains(result.Errors,e=>e.Code=="SALARY_METADATA_REQUIRED");
    }
    [Fact] public async Task DirectAssignmentValidatesMetadataBeforeClosingPreviousStructure()
    {
        var e=Existing();
        unit.Setup(u=>u.ExecuteForEmployeeDatesAsync(It.IsAny<string>(),It.IsAny<DateTime>(),It.IsAny<DateTime>(),It.IsAny<Func<Task<SalaryStructureDetailsDto>>>(),It.IsAny<CancellationToken>()))
            .Returns((string code,DateTime from,DateTime to,Func<Task<SalaryStructureDetailsDto>> action,CancellationToken ct)=>action());
        repository.Setup(r=>r.ValidateSalaryMetadataAsync("USD","CASH",It.IsAny<CancellationToken>())).ThrowsAsync(new ArgumentException("Currency not configured."));
        await Assert.ThrowsAsync<ArgumentException>(()=>new EmployeeService(repository.Object,unit.Object).AssignSalaryStructureAsync("E",new(new(2026,12,1),null,700," usd "," cash ",null),"HR"));
        Assert.Null(Assert.Single(e.SalaryStructures).EffectiveTo); repository.Verify(r=>r.SaveChangesAsync(It.IsAny<CancellationToken>()),Times.Never);
    }
    [Fact] public async Task PreviewAndSaveShareConfiguredCurrencyValidationWithoutAddingRows()
    {
        var row=Row(); row.CurrencyCode="USD"; row.PaymentMethod="CASH";
        repository.Setup(r=>r.ValidateSalaryMetadataAsync("USD","CASH",It.IsAny<CancellationToken>())).ThrowsAsync(new ArgumentException("Currency not configured."));
        var service=Service(row); using var stream=new MemoryStream();
        Assert.Contains((await service.ValidateWorkbookAsync(stream)).Errors,e=>e.Code=="INVALID_SALARY_METADATA");
        Assert.Contains((await service.ImportEmployeesAsync(stream)).Errors,e=>e.Code=="INVALID_SALARY_METADATA");
        repository.Verify(r=>r.AddEmployeeAsync(It.IsAny<Employee>(),It.IsAny<CancellationToken>()),Times.Never);
    }
    [Fact] public async Task RealReaderRejectsMetadataWithoutSalary()
    {
        var row=new EmployeeExportRowDto { EmployeeCode="E",NameLocal="Local",NameEn="English",NationalId="ID",Nationality="X",DateOfBirth=new(1990,1,1),HireDate=new(2020,1,1),CurrencyCode="USD",PaymentMethod="CASH" };
        using var stream=new MemoryStream(new XlsxEmployeeWorkbookWriter().WriteExportWorkbook(new[] { row }));
        var (_,result)=await new XlsxEmployeeWorkbookReader().ReadAsync(stream); Assert.Contains(result.Errors,e=>e.Code=="SALARY_REQUIRED");
    }
}
