using Microsoft.Extensions.Logging;
using Moq;
using ThinkOnErp.Application.DTOs.Hr;
using ThinkOnErp.Application.Services.Hr;
using ThinkOnErp.Domain.Entities.Hr;
using ThinkOnErp.Domain.Interfaces.Hr;
using Xunit;

namespace ThinkOnErp.HR.Tests;

public sealed class EmployeeImportPreviewTests
{
    private readonly Mock<IEmployeeRepository> repository=new();
    private readonly Mock<IXlsxEmployeeWorkbookReader> reader=new();
    private readonly Mock<IHrUnitOfWork> unit=new();
    private EmployeeExcelService Service=>new(repository.Object,reader.Object,Mock.Of<IXlsxEmployeeWorkbookWriter>(),Mock.Of<ILogger<EmployeeExcelService>>(),unit.Object);
    private static EmployeeImportRowDto Row()=>new() { RowNumber=2,EmployeeCode="E1",NameLocal="Local",NameEn="English",NationalId="ID",Nationality="X",DateOfBirth=new(1990,1,1),HireDate=new(2024,1,1),BranchId=7 };
    public EmployeeImportPreviewTests()
    {
        reader.Setup(r=>r.ReadAsync(It.IsAny<Stream>(),It.IsAny<CancellationToken>())).ReturnsAsync(()=>((IReadOnlyList<EmployeeImportRowDto>)new[] { Row() },new EmployeeImportResultDto { TotalRows=1 }));
        repository.Setup(r=>r.GetImportImpactAsync(It.IsAny<string>(),It.IsAny<long?>(),It.IsAny<DateTime>(),It.IsAny<CancellationToken>())).ReturnsAsync(new EmployeeImportImpact(Array.Empty<EmployeeImportPeriodImpact>(),false));
    }
    private void Workbook(EmployeeImportRowDto row)=>reader.Setup(r=>r.ReadAsync(It.IsAny<Stream>(),It.IsAny<CancellationToken>()))
        .ReturnsAsync(((IReadOnlyList<EmployeeImportRowDto>)new[] { row },new EmployeeImportResultDto { TotalRows=1 }));
    private Employee Existing()
    {
        var e=new Employee { EmployeeCode="E1",NameLocal="Local",NameEn="English",NationalId="ID",Nationality="X",DateOfBirth=new(1990,1,1),HireDate=new(2024,1,1),BranchId=7 };
        repository.Setup(r=>r.GetEmployeeByCodeAsync("E1",It.IsAny<CancellationToken>())).ReturnsAsync(e); return e;
    }
    [Fact] public async Task NewEmployeeShowsProspectiveInsertAndSalaryDateWithoutPersistence()
    {
        var row=Row(); row.BasicSalary=600; row.CurrencyCode="USD"; row.PaymentMethod="CASH"; row.SalaryEffectiveFrom=new(2024,2,1); Workbook(row);
        using var stream=new MemoryStream(); var result=await Service.ValidateWorkbookAsync(stream); var preview=Assert.Single(result.PreviewRows);
        Assert.True(result.IsValid); Assert.Equal("INSERT",preview.Action); Assert.False(preview.RequiresUpdateExisting);
        Assert.Equal("NEW_VERSION",preview.SalaryChange); Assert.Equal(row.SalaryEffectiveFrom,preview.SalaryEffectiveFrom); Assert.Equal(row.HireDate,preview.AffectedFrom);
        Assert.NotNull(result.PreviewGeneratedAtUtc);
        repository.Verify(r=>r.SaveChangesAsync(It.IsAny<CancellationToken>()),Times.Never);
        repository.Verify(r=>r.AddEmployeeAsync(It.IsAny<Employee>(),It.IsAny<CancellationToken>()),Times.Never);
        unit.VerifyNoOtherCalls();
    }
    [Fact] public async Task ExistingPreviewUsesEarliestUndatedHistoryAndNeverClosesPreviousSalary()
    {
        var e=Existing(); var salary=new SalaryStructure { EffectiveFrom=new(2023,1,1),BasicSalary=500 }; e.SalaryStructures.Add(salary);
        var row=Row(); row.HireDate=new(2025,1,1); row.BasicSalary=700; row.SalaryEffectiveFrom=new(2026,11,1); Workbook(row);
        using var stream=new MemoryStream(); var result=await Service.ValidateWorkbookAsync(stream); var p=Assert.Single(result.PreviewRows);
        Assert.Equal("UPDATE",p.Action); Assert.True(p.RequiresUpdateExisting); Assert.Equal(new DateTime(2023,1,1),p.AffectedFrom);
        Assert.Equal(row.SalaryEffectiveFrom,p.SalaryEffectiveFrom); Assert.Equal("NEW_VERSION",p.SalaryChange);
        Assert.Equal(500,salary.BasicSalary); Assert.Null(salary.EffectiveTo); Assert.Single(e.SalaryStructures); Assert.Equal(new DateTime(2024,1,1),e.HireDate);
    }
    [Fact] public async Task ReimportingSameCurrentSalaryDoesNotPlanNewVersion()
    {
        var e=Existing(); e.SalaryStructures.Add(new SalaryStructure { EffectiveFrom=e.HireDate,BasicSalary=500 });
        var row=Row(); row.BasicSalary=500; Workbook(row);
        using var stream=new MemoryStream(); var p=Assert.Single((await Service.ValidateWorkbookAsync(stream)).PreviewRows);
        Assert.Equal("UNCHANGED",p.SalaryChange); Assert.Null(p.SalaryEffectiveFrom);
    }
    [Theory] [InlineData("CLOSED_PAYROLL_PERIOD")] [InlineData("APPROVED_PAYROLL")] [InlineData("UNMAPPED_APPROVED_PAYROLL")]
    public async Task ProtectedInputsAppearAsBlockingReasonsAndRowValidationFailure(string reason)
    {
        repository.Setup(r=>r.GetImportImpactAsync(It.IsAny<string>(),It.IsAny<long?>(),It.IsAny<DateTime>(),It.IsAny<CancellationToken>()))
            .ReturnsAsync(new EmployeeImportImpact(new[] { new EmployeeImportPeriodImpact("P",new(2026,1,1),new(2026,1,31),reason=="CLOSED_PAYROLL_PERIOD"?"CLOSED":"OPEN",reason=="APPROVED_PAYROLL") },reason=="UNMAPPED_APPROVED_PAYROLL"));
        using var stream=new MemoryStream(); var result=await Service.ValidateWorkbookAsync(stream);
        Assert.Contains(reason,Assert.Single(result.PreviewRows).BlockingReasons); Assert.False(result.IsValid); Assert.Equal(0,result.SuccessCount);
        Assert.Contains(result.Errors,e=>e.Code=="PAYROLL_INPUT_BLOCKED"); repository.Verify(r=>r.SaveChangesAsync(It.IsAny<CancellationToken>()),Times.Never);
    }
    [Fact] public async Task HiddenEmployeeDoesNotRevealStoredImpact()
    {
        repository.Setup(r=>r.ExistsByCodeAsync("E1",It.IsAny<CancellationToken>())).ReturnsAsync(true);
        using var stream=new MemoryStream(); var result=await Service.ValidateWorkbookAsync(stream);
        Assert.Empty(result.PreviewRows); Assert.Contains(result.Errors,e=>e.Code=="EMPLOYEE_NOT_EDITABLE");
        repository.Verify(r=>r.GetImportImpactAsync(It.IsAny<string>(),It.IsAny<long?>(),It.IsAny<DateTime>(),It.IsAny<CancellationToken>()),Times.Never);
    }
    [Fact] public async Task OutOfScopeBranchDoesNotReturnPeriodMetadata()
    {
        repository.Setup(r=>r.GetImportImpactAsync(It.IsAny<string>(),It.IsAny<long?>(),It.IsAny<DateTime>(),It.IsAny<CancellationToken>())).ThrowsAsync(new UnauthorizedAccessException());
        using var stream=new MemoryStream(); var result=await Service.ValidateWorkbookAsync(stream);
        Assert.Empty(result.PreviewRows); Assert.Contains(result.Errors,e=>e.Code=="IMPORT_SCOPE_UNAVAILABLE");
    }
    [Fact] public async Task NewSalaryBeforeHireIsInvalidInPreviewAsWellAsImport()
    {
        var row=Row(); row.BasicSalary=500; row.SalaryEffectiveFrom=new(2023,1,1); Workbook(row);
        using var stream=new MemoryStream(); var result=await Service.ValidateWorkbookAsync(stream);
        Assert.Equal("INVALID",Assert.Single(result.PreviewRows).SalaryChange); Assert.Contains(result.Errors,e=>e.Code=="SALARY_BEFORE_HIRE");
    }
    [Fact] public async Task ValidPreviewCannotBypassLaterTransactionRevalidation()
    {
        using var stream=new MemoryStream(); Assert.True((await Service.ValidateWorkbookAsync(stream)).IsValid);
        unit.Setup(u=>u.ExecuteEmployeeImportAsync(It.IsAny<IReadOnlyList<EmployeeImportRowDto>>(),It.IsAny<Func<Task<EmployeeImportResultDto>>>(),It.IsAny<CancellationToken>())).ThrowsAsync(new InvalidOperationException("closed after preview"));
        await Assert.ThrowsAsync<InvalidOperationException>(()=>Service.ImportEmployeesAsync(stream));
        repository.Verify(r=>r.SaveChangesAsync(It.IsAny<CancellationToken>()),Times.Never);
    }
}
