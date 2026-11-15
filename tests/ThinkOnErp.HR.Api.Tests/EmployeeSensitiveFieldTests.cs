using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using ThinkOnErp.Application.DTOs.Hr;
using ThinkOnErp.Application.Services.Hr;
using ThinkOnErp.Domain.Entities;
using ThinkOnErp.Domain.Entities.Hr;
using ThinkOnErp.Infrastructure.Data;
using ThinkOnErp.Infrastructure.Repositories.Hr;
using ThinkOnErp.Infrastructure.Services.Hr;
using Xunit;

namespace ThinkOnErp.HR.Api.Tests;

public sealed class EmployeeSensitiveFieldTests : IDisposable
{
    private readonly OracleDbContext db=new(new DbContextOptionsBuilder<OracleDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
    private readonly EmployeeFieldAccess fields=new();
    private readonly Mock<IHrUnitOfWork> unit=new();
    private EmployeeRepository Repository=>new(db,fields);
    private EmployeeService Service=>new(Repository,unit.Object,fields);
    private static Employee Employee(string code,long branch)
    {
        var e=new Employee { EmployeeCode=code,NameLocal=code,NameEn=code,BranchId=branch,NationalId="SECRET_"+code,Nationality="X",CitizenshipCountryCode="US",PassportNumber="PASSPORT",DateOfBirth=new(1990,1,1),HireDate=new(2020,1,1),Phone="PHONE_"+code,Email=code+"@example.test",BankIban="IBAN_"+code };
        e.SalaryStructures.Add(new SalaryStructure { BasicSalary=9876,EffectiveFrom=e.HireDate });
        e.Dependents.Add(new EmployeeDependent { NameEn="CHILD",NationalId="CHILD_ID" }); return e;
    }
    public EmployeeSensitiveFieldTests()
    {
        db.AddRange(new SysBranch { Id=7,CompanyId=1,IsActive=true },new SysBranch { Id=8,CompanyId=1,IsActive=true },new SysBranch { Id=9,CompanyId=2,IsActive=true },
            new SysUser { Id=12,CompanyId=1,RoleId=90,IsActive=true },new SysRole { Id=90,IsActive=true },new SysSystem { Id=50,IsActive=true },
            new SysScreen { Id=52,SystemId=50,ScreenCode="hr-employees",IsActive=true },new SysBranchSystem { Id=70,BranchId=7,SystemId=50 },new SysBranchSystem { Id=71,BranchId=8,SystemId=50 },
            new SysCurrency { Id=1, ShortNameEn="USD", CurrencyNameEn="US Dollar", CurrencyNameLocal="دولار أمريكي" },
            Employee("E",7),Employee("F",8));
        db.SaveChanges(); db.ChangeTracker.Clear(); db.HrScopeEnabled=true; db.HrScopeCompanyId=1; db.HrScopeBranchIds.AddRange(new long[] {7,8});
        EmployeeFieldPermissionCatalog.EnsureAsync(db,"FIXTURE").GetAwaiter().GetResult();
        unit.Setup(u=>u.ExecuteEmployeeProfileAsync(It.IsAny<string>(),It.IsAny<DateTime>(),It.IsAny<Func<Task<EmployeeDetailsDto>>>(),It.IsAny<CancellationToken>()))
            .Returns((string code,DateTime date,Func<Task<EmployeeDetailsDto>> action,CancellationToken ct)=>action());
        unit.Setup(u=>u.ExecuteEmployeeProfileAsync(It.IsAny<string>(),It.IsAny<DateTime>(),It.IsAny<Func<Task<DependentDto>>>(),It.IsAny<CancellationToken>()))
            .Returns((string code,DateTime date,Func<Task<DependentDto>> action,CancellationToken ct)=>action());
        unit.Setup(u=>u.ExecuteEmployeeProfileAsync(It.IsAny<string>(),It.IsAny<DateTime>(),It.IsAny<Func<Task<bool>>>(),It.IsAny<CancellationToken>()))
            .Returns((string code,DateTime date,Func<Task<bool>> action,CancellationToken ct)=>action());
        unit.Setup(u=>u.ExecuteForEmployeeDatesAsync(It.IsAny<string>(),It.IsAny<DateTime>(),It.IsAny<DateTime>(),It.IsAny<Func<Task<SalaryStructureDetailsDto>>>(),It.IsAny<CancellationToken>()))
            .Returns((string code,DateTime start,DateTime end,Func<Task<SalaryStructureDetailsDto>> action,CancellationToken ct)=>action());
    }
    private static UpdateEmployeeDto Full(Employee e)=>new(e.NameLocal,e.NameEn,e.NationalId,e.Nationality,e.PassportNumber,e.DateOfBirth,e.Gender,e.MaritalStatus,e.Email,e.Phone,e.HireDate,e.ProbationEndDate,e.TerminationDate,e.TerminationReason,e.EmploymentType,e.EmploymentStatus,e.DepartmentCode,e.PositionCode,e.BranchId,e.ManagerEmployeeCode,e.SscNumber,e.TaxExemptionCount,e.IsHighRiskRole,e.BankName,e.BankAccountNumber,e.BankIban,e.IsActive);
    private static UpdateEmployeeWorkDto Work(Employee e)=>new(e.NameLocal,e.NameEn,e.HireDate,e.ProbationEndDate,e.TerminationDate,e.TerminationReason,e.EmploymentType,e.EmploymentStatus,e.DepartmentCode,e.PositionCode,e.BranchId,e.ManagerEmployeeCode,e.IsActive);
    private async Task Permission(string code,long branch,bool role=false,bool allowed=true)
    {
        var id=await db.SysFeatures.Where(f=>f.FeatureCode==code).Select(f=>f.Id).SingleAsync();
        if(role) db.Add(new SysRoleScreenPermission { RoleId=90,BranchId=branch,ScreenId=52,FeatureId=id,IsGranted=allowed });
        else db.Add(new SysUserScreenPermission { UserId=12,BranchId=branch,ScreenId=52,FeatureId=id,IsGranted=allowed });
        await db.SaveChangesAsync();
    }
    private async Task Populate()=>await EmployeeFieldAuthorization.PopulateAsync(db,fields,await db.SysUsers.SingleAsync(),false,new long[] {7,8,9});
    [Fact] public async Task GeneralReadMasksPrivateDataAndDistinguishesItFromEmptyValues()
    {
        var dto=(await Service.GetEmployeeByCodeAsync("E"))!;
        Assert.Null(dto.NationalId); Assert.Null(dto.CitizenshipCountryCode); Assert.Null(dto.DateOfBirth); Assert.Empty(dto.Dependents); Assert.Null(dto.BankIban); Assert.Null(dto.ActiveSalaryStructure);
        Assert.Equal("E",dto.NameEn); Assert.False(dto.FieldVisibility!.Identity); Assert.False(dto.FieldVisibility.Bank); Assert.False(dto.FieldVisibility.Salary);
        Assert.Null(Assert.Single((await Service.GetEmployeesPagedAsync(branchId:7)).Items).BasicSalary);
    }
    [Fact] public async Task FieldGrantsAreIndependentAndIntersectTheirBranches()
    {
        await Permission(EmployeeFieldAccess.ViewIdentity,7); await Permission(EmployeeFieldAccess.ViewBank,7,true); await Permission(EmployeeFieldAccess.ViewSalary,8,true); await Populate();
        var e=(await Service.GetEmployeeByCodeAsync("E"))!; var f=(await Service.GetEmployeeByCodeAsync("F"))!;
        Assert.Equal("SECRET_E",e.NationalId); Assert.Equal("IBAN_E",e.BankIban); Assert.Null(e.ActiveSalaryStructure);
        Assert.Null(f.NationalId); Assert.Null(f.BankIban); Assert.Equal(9876,f.ActiveSalaryStructure!.BasicSalary);
        await Assert.ThrowsAsync<UnauthorizedAccessException>(()=>Service.GetActiveSalaryStructureAsync("E"));
        Assert.NotNull(await Service.GetActiveSalaryStructureAsync("F"));
    }
    [Fact] public async Task ExplicitDenyOverridesRoleAndForeignBranchCannotBeGranted()
    {
        await Permission(EmployeeFieldAccess.ViewIdentity,7,true); await Permission(EmployeeFieldAccess.ViewIdentity,7,allowed:false);
        await Permission(EmployeeFieldAccess.ViewBank,9); await Populate();
        Assert.False(fields.Can(EmployeeFieldAccess.ViewIdentity,7)); Assert.False(fields.Can(EmployeeFieldAccess.ViewBank,9));
    }
    [Fact] public async Task CatalogIsIdempotentAndNeverCreatesPermissionGrants()
    {
        await EmployeeFieldPermissionCatalog.EnsureAsync(db,"FIXTURE");
        Assert.Equal(6,await db.SysFeatures.CountAsync()); Assert.Equal(6,await db.Set<SysScreenFeature>().CountAsync());
        Assert.Empty(await db.SysUserScreenPermissions.ToListAsync()); Assert.Empty(await db.SysRoleScreenPermissions.ToListAsync());
        await Populate(); Assert.False(fields.Can(EmployeeFieldAccess.ViewIdentity,7));
    }
    [Fact] public async Task HiddenIdentityCannotBeInferredThroughSearchCounts()
    {
        Assert.Equal(0,(await Service.GetEmployeesPagedAsync(searchKeyword:"SECRET")).TotalCount);
        fields.Grant(EmployeeFieldAccess.ViewIdentity,7);
        var result=await Service.GetEmployeesPagedAsync(searchKeyword:"SECRET"); Assert.Equal("E",Assert.Single(result.Items).EmployeeCode);
        Assert.Equal(0,(await Service.GetEmployeesPagedAsync(searchKeyword:"PHONE_F")).TotalCount);
    }
    [Fact] public async Task WorkUpdatePreservesPrivateFieldsSalaryAndChildren()
    {
        var e=(await Repository.GetEmployeeByCodeAsync("E"))!;
        var result=await Service.UpdateEmployeeWorkAsync("E",Work(e) with { NameEn="Changed" },"EDITOR");
        Assert.Null(result.NationalId); Assert.Null(result.BankIban);
        var stored=await db.Employees.AsNoTracking().SingleAsync(x=>x.EmployeeCode=="E");
        Assert.Equal("Changed",stored.NameEn); Assert.Equal("SECRET_E",stored.NationalId); Assert.Equal("IBAN_E",stored.BankIban);
        Assert.Equal(9876,(await db.SalaryStructures.SingleAsync(s=>s.EmployeeCode=="E")).BasicSalary);
        Assert.Equal("CHILD_ID",(await db.EmployeeDependents.SingleAsync(d=>d.EmployeeCode=="E")).NationalId);
    }
    [Fact] public async Task FullReplacementNeedsBothEditGrantsAndDoesNotGrantRead()
    {
        var e=(await Repository.GetEmployeeByCodeAsync("E"))!; fields.Grant(EmployeeFieldAccess.EditIdentity,7);
        await Assert.ThrowsAsync<UnauthorizedAccessException>(()=>Service.UpdateEmployeeAsync("E",Full(e) with { BankIban=null },"EDITOR"));
        Assert.Equal("IBAN_E",e.BankIban);
        fields.Grant(EmployeeFieldAccess.EditBank,7);
        var result=await Service.UpdateEmployeeAsync("E",Full(e) with { BankIban="NEW" },"EDITOR");
        Assert.Equal("NEW",(await db.Employees.AsNoTracking().SingleAsync(x=>x.EmployeeCode=="E")).BankIban); Assert.Null(result.BankIban);
    }
    [Fact] public async Task FullAndWorkProfileUpdatesCannotTerminateEmployee()
    {
        fields.Grant(EmployeeFieldAccess.EditIdentity,7); fields.Grant(EmployeeFieldAccess.EditBank,7);
        var employee=(await Repository.GetEmployeeByCodeAsync("E"))!;
        var terminationDate=new DateTime(2026,11,15);
        await Assert.ThrowsAsync<InvalidOperationException>(()=>Service.UpdateEmployeeAsync("E",
            Full(employee) with { EmploymentStatus="TERMINATED", TerminationDate=terminationDate, TerminationReason="Resignation", IsActive=false },"EDITOR"));
        await Assert.ThrowsAsync<InvalidOperationException>(()=>Service.UpdateEmployeeWorkAsync("E",
            Work(employee) with { EmploymentStatus="TERMINATED", TerminationDate=terminationDate, TerminationReason="Resignation", IsActive=false },"EDITOR"));

        var stored=await db.Employees.AsNoTracking().SingleAsync(x=>x.EmployeeCode=="E");
        Assert.Equal("ACTIVE",stored.EmploymentStatus); Assert.True(stored.IsActive);
        Assert.Null(stored.TerminationDate); Assert.Null(stored.TerminationReason);
    }
    [Fact] public async Task BranchTransferRequiresAllEditGroupsOnBothSides()
    {
        var e=(await Repository.GetEmployeeByCodeAsync("E"))!;
        foreach(var feature in new[] {EmployeeFieldAccess.EditIdentity,EmployeeFieldAccess.EditBank,EmployeeFieldAccess.EditSalary}) fields.Grant(feature,7);
        await Assert.ThrowsAsync<UnauthorizedAccessException>(()=>Service.UpdateEmployeeWorkAsync("E",Work(e) with { BranchId=8 },"EDITOR"));
        Assert.Equal(7,e.BranchId);
        foreach(var feature in new[] {EmployeeFieldAccess.EditIdentity,EmployeeFieldAccess.EditBank,EmployeeFieldAccess.EditSalary}) fields.Grant(feature,8);
        await Service.UpdateEmployeeWorkAsync("E",Work(e) with { BranchId=8 },"EDITOR"); Assert.Equal(8,e.BranchId); Assert.Equal("SECRET_E",e.NationalId);
    }
    [Fact] public async Task DependentAndSalaryWritesCannotUseGeneralEditAlone()
    {
        await Assert.ThrowsAsync<UnauthorizedAccessException>(()=>Service.AddDependentAsync("E",new("Child","Child","CHILD",new(2010,1,1),"M",null,true,false),"EDITOR"));
        await Assert.ThrowsAsync<UnauthorizedAccessException>(()=>Service.AssignSalaryStructureAsync("E",new(new(2026,1,1),null,900,"USD","CASH",null),"EDITOR"));
        Assert.Equal(9876,(await db.SalaryStructures.SingleAsync(s=>s.EmployeeCode=="E")).BasicSalary);
    }
    [Fact] public async Task ExportFailsAsAWholeWithoutAllReadGroupsAcrossSelectedRows()
    {
        foreach(var feature in new[] {EmployeeFieldAccess.ViewIdentity,EmployeeFieldAccess.ViewBank,EmployeeFieldAccess.ViewSalary}) fields.Grant(feature,7);
        var writer=new Mock<IXlsxEmployeeWorkbookWriter>(); writer.Setup(w=>w.WriteExportWorkbook(It.IsAny<IReadOnlyList<EmployeeExportRowDto>>())).Returns(new byte[] {1});
        var service=new EmployeeExcelService(Repository,Mock.Of<IXlsxEmployeeWorkbookReader>(),writer.Object,Mock.Of<ILogger<EmployeeExcelService>>(),unit.Object,fields);
        await Assert.ThrowsAsync<UnauthorizedAccessException>(()=>service.ExportEmployeesAsync()); writer.Verify(w=>w.WriteExportWorkbook(It.IsAny<IReadOnlyList<EmployeeExportRowDto>>()),Times.Never);
        foreach(var feature in new[] {EmployeeFieldAccess.ViewIdentity,EmployeeFieldAccess.ViewBank,EmployeeFieldAccess.ViewSalary}) fields.Grant(feature,8);
        Assert.NotEmpty(await service.ExportEmployeesAsync());
    }
    [Fact] public async Task BankAndIdentityGroupWritersDoNotRequireOrOverwriteEachOther()
    {
        fields.Grant(EmployeeFieldAccess.EditBank,7);
        var bank=await Service.UpdateEmployeeBankAsync("E",new("New bank","ACCOUNT","NEW_IBAN"),"BANK_EDITOR");
        Assert.Null(bank.BankIban); Assert.Equal("SECRET_E",(await db.Employees.AsNoTracking().SingleAsync(e=>e.EmployeeCode=="E")).NationalId);
        fields.Reset(); fields.Grant(EmployeeFieldAccess.EditIdentity,7);
        var identity=await Service.UpdateEmployeeIdentityAsync("E",new("NEW_ID","X",null,new(1990,1,1),"M","SINGLE",null,null,null,0,false," ca "),"IDENTITY_EDITOR");
        Assert.Null(identity.NationalId);
        Assert.Null(identity.CitizenshipCountryCode);
        var stored=await db.Employees.AsNoTracking().SingleAsync(e=>e.EmployeeCode=="E"); Assert.Equal("NEW_ID",stored.NationalId); Assert.Equal("CA",stored.CitizenshipCountryCode); Assert.Equal("NEW_IBAN",stored.BankIban);
        await Service.UpdateEmployeeIdentityAsync("E",new("NEWER_ID","X",null,new(1990,1,1),"M","SINGLE",null,null,null,0,false),"LEGACY_IDENTITY_EDITOR");
        Assert.Equal("CA",(await db.Employees.AsNoTracking().SingleAsync(e=>e.EmployeeCode=="E")).CitizenshipCountryCode);
        Assert.Equal(9876,(await db.SalaryStructures.SingleAsync(s=>s.EmployeeCode=="E")).BasicSalary);
    }
    [Fact] public async Task GroupAuthorizationIsCheckedAgainInsideProfileGuard()
    {
        fields.Grant(EmployeeFieldAccess.EditBank,7);
        unit.Setup(u=>u.ExecuteEmployeeProfileAsync(It.IsAny<string>(),It.IsAny<DateTime>(),It.IsAny<Func<Task<EmployeeDetailsDto>>>(),It.IsAny<CancellationToken>()))
            .Returns((string code,DateTime date,Func<Task<EmployeeDetailsDto>> action,CancellationToken ct)=> { fields.Reset(); return action(); });
        await Assert.ThrowsAsync<UnauthorizedAccessException>(()=>Service.UpdateEmployeeBankAsync("E",new(null,null,"HACK"),"EDITOR"));
        Assert.Equal("IBAN_E",(await db.Employees.AsNoTracking().SingleAsync(e=>e.EmployeeCode=="E")).BankIban);
    }
    [Fact] public async Task GeneralWorkValidationErrorCannotExposeStoredNationalId()
    {
        (await db.Employees.SingleAsync(e=>e.EmployeeCode=="F")).NationalId="SECRET_E"; await db.SaveChangesAsync();
        var e=(await Repository.GetEmployeeByCodeAsync("E"))!;
        var error=await Assert.ThrowsAsync<InvalidOperationException>(()=>Service.UpdateEmployeeWorkAsync("E",Work(e) with { NameEn="Changed" },"EDITOR"));
        Assert.DoesNotContain("SECRET_E",error.Message);
    }

    [Fact]
    public async Task GetEmployeeByCodeAsync_PropagatesCancellationToken()
    {
        using var cts = new CancellationTokenSource();
        cts.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => Service.GetEmployeeByCodeAsync("E", cts.Token));
    }

    [Fact]
    public async Task GetEmployeesPagedAsync_PropagatesCancellationToken()
    {
        using var cts = new CancellationTokenSource();
        cts.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => Service.GetEmployeesPagedAsync(cancellationToken: cts.Token));
    }

    [Fact]
    public async Task GetActiveSalaryStructureAsync_PropagatesCancellationToken()
    {
        fields.Grant(EmployeeFieldAccess.ViewSalary, 7);
        using var cts = new CancellationTokenSource();
        cts.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => Service.GetActiveSalaryStructureAsync("E", cancellationToken: cts.Token));
    }

    [Fact]
    public async Task GetEmployeeByCodeAsync_WhenNotFound_ReturnsNull()
    {
        Assert.Null(await Service.GetEmployeeByCodeAsync("NONEXISTENT"));
    }

    [Fact]
    public async Task UpdateEmployeeWorkAsync_WhenNotFound_ThrowsKeyNotFoundException()
    {
        var e = (await Repository.GetEmployeeByCodeAsync("E"))!;
        await Assert.ThrowsAsync<KeyNotFoundException>(() => Service.UpdateEmployeeWorkAsync("NONEXISTENT", Work(e), "EDITOR"));
    }

    [Fact]
    public async Task UpdateEmployeeBankAsync_WhenNotFound_ThrowsKeyNotFoundException()
    {
        await Assert.ThrowsAsync<KeyNotFoundException>(() => Service.UpdateEmployeeBankAsync("NONEXISTENT", new("B", "A", "I"), "EDITOR"));
    }

    [Fact]
    public async Task UpdateEmployeeIdentityAsync_WhenNotFound_ThrowsKeyNotFoundException()
    {
        await Assert.ThrowsAsync<KeyNotFoundException>(() => Service.UpdateEmployeeIdentityAsync("NONEXISTENT", new("N", "N", null, new(1990, 1, 1), "M", "S", null, null, null, 0, false), "EDITOR"));
    }

    [Fact]
    public async Task AddDependentAsync_WhenNotFound_ThrowsKeyNotFoundException()
    {
        await Assert.ThrowsAsync<KeyNotFoundException>(() => Service.AddDependentAsync("NONEXISTENT", new("C", "C", "C", new(2010, 1, 1), "M", null, true, false), "EDITOR"));
    }

    [Fact]
    public async Task DeleteDependentAsync_WhenNotFound_ThrowsKeyNotFoundException()
    {
        await Assert.ThrowsAsync<KeyNotFoundException>(() => Service.DeleteDependentAsync(99999L, "EDITOR"));
    }

    [Fact]
    public async Task AssignSalaryStructureAsync_WhenNotFound_ThrowsKeyNotFoundException()
    {
        await Assert.ThrowsAsync<KeyNotFoundException>(() => Service.AssignSalaryStructureAsync("NONEXISTENT", new(new(2026, 1, 1), null, 1000, "USD", "CASH", null), "EDITOR"));
    }

    [Fact]
    public async Task GetActiveSalaryStructureAsync_WhenNotFound_ThrowsKeyNotFoundException()
    {
        await Assert.ThrowsAsync<KeyNotFoundException>(() => Service.GetActiveSalaryStructureAsync("NONEXISTENT"));
    }

    [Fact]
    public async Task DeleteDependentAsync_WhenNoEditIdentityPermission_ThrowsUnauthorizedAccessException()
    {
        var depId = (await db.EmployeeDependents.SingleAsync(d => d.EmployeeCode == "E")).Id;
        fields.Reset();
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => Service.DeleteDependentAsync(depId, "EDITOR"));
    }

    [Fact]
    public async Task DeleteDependentAsync_WhenEditIdentityGranted_DeletesSuccessfully()
    {
        var depId = (await db.EmployeeDependents.SingleAsync(d => d.EmployeeCode == "E")).Id;
        fields.Grant(EmployeeFieldAccess.EditIdentity, 7);
        await Service.DeleteDependentAsync(depId, "EDITOR");
        Assert.Empty(await db.EmployeeDependents.Where(d => d.EmployeeCode == "E").ToListAsync());
    }

    [Fact]
    public async Task UpdateEmployeeIdentityAsync_NormalizesCitizenshipCountryCodeToUppercase()
    {
        fields.Grant(EmployeeFieldAccess.EditIdentity, 7);
        await Service.UpdateEmployeeIdentityAsync("E", new("ID_123", "X", null, new(1990, 1, 1), "M", "SINGLE", null, null, null, 0, false, "  jo  "), "EDITOR");
        var stored = await db.Employees.AsNoTracking().SingleAsync(e => e.EmployeeCode == "E");
        Assert.Equal("JO", stored.CitizenshipCountryCode);
    }

    [Fact]
    public async Task UpdateEmployeeIdentityAsync_WhenCitizenshipCountryCodeEmpty_SetsNull()
    {
        fields.Grant(EmployeeFieldAccess.EditIdentity, 7);
        await Service.UpdateEmployeeIdentityAsync("E", new("ID_123", "X", null, new(1990, 1, 1), "M", "SINGLE", null, null, null, 0, false, "   "), "EDITOR");
        var stored = await db.Employees.AsNoTracking().SingleAsync(e => e.EmployeeCode == "E");
        Assert.Null(stored.CitizenshipCountryCode);
    }

    [Fact]
    public async Task AssignSalaryStructureAsync_WhenDtoNull_ThrowsArgumentNullException()
    {
        fields.Grant(EmployeeFieldAccess.EditSalary, 7);
        fields.Grant(EmployeeFieldAccess.ViewSalary, 7);
        await Assert.ThrowsAsync<ArgumentNullException>(() =>
            Service.AssignSalaryStructureAsync("E", null!, "EDITOR"));
    }

    [Fact]
    public async Task AssignSalaryStructureAsync_WhenBasicSalaryNegative_ThrowsArgumentException()
    {
        fields.Grant(EmployeeFieldAccess.EditSalary, 7);
        fields.Grant(EmployeeFieldAccess.ViewSalary, 7);
        var dto = new AssignSalaryStructureDto(new(2026, 1, 1), null, -100m, "USD", "CASH", null);
        await Assert.ThrowsAsync<ArgumentException>(() =>
            Service.AssignSalaryStructureAsync("E", dto, "EDITOR"));
    }

    [Fact]
    public async Task AssignSalaryStructureAsync_WhenEffectiveToEarlierThanEffectiveFrom_ThrowsArgumentException()
    {
        fields.Grant(EmployeeFieldAccess.EditSalary, 7);
        fields.Grant(EmployeeFieldAccess.ViewSalary, 7);
        var dto = new AssignSalaryStructureDto(new(2026, 1, 10), new(2026, 1, 5), 1000m, "USD", "CASH", null);
        await Assert.ThrowsAsync<ArgumentException>(() =>
            Service.AssignSalaryStructureAsync("E", dto, "EDITOR"));
    }

    [Fact]
    public async Task AssignSalaryStructureAsync_WhenCurrencyNotConfiguredInSysCurrency_ThrowsArgumentException()
    {
        fields.Grant(EmployeeFieldAccess.EditSalary, 7);
        fields.Grant(EmployeeFieldAccess.ViewSalary, 7);
        var dto = new AssignSalaryStructureDto(new(2026, 1, 1), null, 1000m, "UNSUPPORTED", "CASH", null);
        var ex = await Assert.ThrowsAsync<ArgumentException>(() =>
            Service.AssignSalaryStructureAsync("E", dto, "EDITOR"));
        Assert.Contains("CurrencyCode must identify exactly one configured ERP currency", ex.Message);
    }

    [Fact]
    public async Task AssignSalaryStructureAsync_WhenValid_TruncatesPrecedingStructureAndSaves()
    {
        fields.Grant(EmployeeFieldAccess.EditSalary, 7);
        fields.Grant(EmployeeFieldAccess.ViewSalary, 7);

        var newEffectiveFrom = new DateTime(2026, 6, 1);
        var dto = new AssignSalaryStructureDto(newEffectiveFrom, null, 12500m, "USD", "BANK_TRANSFER", null);

        var result = await Service.AssignSalaryStructureAsync("E", dto, "EDITOR");

        Assert.Equal(12500m, result.BasicSalary);
        Assert.Equal("USD", result.CurrencyCode);
        Assert.Equal("BANK_TRANSFER", result.PaymentMethod);

        // Preceding structure truncated to 2026-05-31
        var initial = await db.SalaryStructures.SingleAsync(s => s.EmployeeCode == "E" && s.BasicSalary == 9876m);
        Assert.Equal(new DateTime(2026, 5, 31), initial.EffectiveTo);
    }

    [Fact]
    public async Task AssignSalaryStructureAsync_WhenFutureStructureStartsOnOrAfterEffectiveFrom_ThrowsInvalidOperationException()
    {
        fields.Grant(EmployeeFieldAccess.EditSalary, 7);
        fields.Grant(EmployeeFieldAccess.ViewSalary, 7);

        // Assign structure starting 2026-09-01
        var futureDto = new AssignSalaryStructureDto(new(2026, 9, 1), null, 11000m, "USD", "CASH", null);
        await Service.AssignSalaryStructureAsync("E", futureDto, "EDITOR");

        // Try to assign structure starting on or before future date 2026-08-01
        var backdatedDto = new AssignSalaryStructureDto(new(2026, 8, 1), null, 10500m, "USD", "CASH", null);
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            Service.AssignSalaryStructureAsync("E", backdatedDto, "EDITOR"));
        Assert.Contains("already starts on or after this date", ex.Message);
    }

    [Fact]
    public async Task DeleteEmployeeAsync_WhenFound_RefusesTerminationAndPreservesEmployee()
    {
        await Assert.ThrowsAsync<InvalidOperationException>(() => Service.DeleteEmployeeAsync("E", "HR_ADMIN"));

        var stored = await db.Employees.AsNoTracking().SingleAsync(e => e.EmployeeCode == "E");
        Assert.True(stored.IsActive);
        Assert.Equal("ACTIVE", stored.EmploymentStatus);
        Assert.Null(stored.TerminationDate);
        Assert.Null(stored.TerminationReason);
    }

    [Fact]
    public async Task DeleteEmployeeAsync_WhenNotFound_ThrowsKeyNotFoundException()
    {
        await Assert.ThrowsAsync<KeyNotFoundException>(() =>
            Service.DeleteEmployeeAsync("NONEXISTENT", "HR_ADMIN"));
    }

    [Fact]
    public async Task DeleteEmployeeAsync_PropagatesCancellationToken()
    {
        using var cts = new CancellationTokenSource();
        cts.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            Service.DeleteEmployeeAsync("E", "HR_ADMIN", cts.Token));
    }

    [Fact]
    public async Task GetSalaryComponentsAsync_ReturnsComponentsList()
    {
        db.SalaryComponents.Add(new SalaryComponent
        {
            ComponentCode = "BONUS",
            NameEn = "Performance Bonus",
            NameLocal = "مكافأة أداء",
            ComponentType = "EARNING",
            CalculationType = "FIXED",
            DefaultAmount = 200m,
            IsTaxable = true,
            IsSscApplicable = true,
            IsActive = true
        });
        await db.SaveChangesAsync();

        var components = await Service.GetSalaryComponentsAsync();
        Assert.Contains(components, c => c.ComponentCode == "BONUS" && c.DefaultAmount == 200m);
    }

    [Fact]
    public async Task GetSalaryComponentsAsync_PropagatesCancellationToken()
    {
        using var cts = new CancellationTokenSource();
        cts.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            Service.GetSalaryComponentsAsync(cts.Token));
    }

    [Fact]
    public async Task CreateSalaryComponentAsync_WhenNew_CreatesAndReturnsDto()
    {
        var dto = new CreateSalaryComponentDto(
            ComponentCode: "phone_allowance",
            NameLocal: "بدل هاتف",
            NameEn: "Phone Allowance",
            ComponentType: "EARNING",
            CalculationType: "FIXED",
            DefaultAmount: 50m,
            DefaultPercent: 0m,
            IsTaxable: false,
            IsSscApplicable: false,
            GlAccountCode: null
        );

        var created = await Service.CreateSalaryComponentAsync(dto, "PAYROLL_ADMIN");
        Assert.Equal("PHONE_ALLOWANCE", created.ComponentCode);
        Assert.Equal(50m, created.DefaultAmount);

        var stored = await db.SalaryComponents.SingleAsync(c => c.ComponentCode == "PHONE_ALLOWANCE");
        Assert.Equal("Phone Allowance", stored.NameEn);
    }

    [Fact]
    public async Task CreateSalaryComponentAsync_WhenDuplicateCode_ThrowsInvalidOperationException()
    {
        db.SalaryComponents.Add(new SalaryComponent
        {
            ComponentCode = "TRANSPORT",
            NameEn = "Transport Allowance",
            NameLocal = "بدل مواصلات",
            ComponentType = "EARNING",
            CalculationType = "FIXED",
            IsActive = true
        });
        await db.SaveChangesAsync();

        var duplicateDto = new CreateSalaryComponentDto(
            ComponentCode: "transport",
            NameLocal: "بدل مواصلات آخر",
            NameEn: "Another Transport",
            ComponentType: "EARNING",
            CalculationType: "FIXED",
            DefaultAmount: 100m,
            DefaultPercent: 0m,
            IsTaxable: false,
            IsSscApplicable: false,
            GlAccountCode: null
        );

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            Service.CreateSalaryComponentAsync(duplicateDto, "PAYROLL_ADMIN"));
        Assert.Contains("already exists", ex.Message);
    }

    [Fact]
    public async Task CreateSalaryComponentAsync_PropagatesCancellationToken()
    {
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        var dto = new CreateSalaryComponentDto("STIPEND", "منحة", "Stipend", "EARNING", "FIXED", 100m, 0m, false, false, null);
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            Service.CreateSalaryComponentAsync(dto, "PAYROLL_ADMIN", cts.Token));
    }
    public void Dispose()=>db.Dispose();
}

