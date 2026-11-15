using Microsoft.EntityFrameworkCore;
using ThinkOnErp.Domain.Entities;
using ThinkOnErp.Domain.Entities.Hr;
using ThinkOnErp.Infrastructure.Data;
using ThinkOnErp.Infrastructure.Repositories.Hr;
using Xunit;
namespace ThinkOnErp.HR.Api.Tests;
public sealed class OrganizationScopeTests : IDisposable
{
    private readonly OracleDbContext db = new(new DbContextOptionsBuilder<OracleDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
    private static Employee Employee(string code) => new() { EmployeeCode=code, NameLocal=code, NameEn=code, NationalId=code, Nationality="X", DateOfBirth=new(1990,1,1), HireDate=new(2020,1,1), BranchId=7 };
    public OrganizationScopeTests()
    {
        db.AddRange(new SysBranch { Id=7, CompanyId=1, IsActive=true }, new SysBranch { Id=8, CompanyId=1, IsActive=true }, new SysBranch { Id=9, CompanyId=2, IsActive=true },
            new HrDepartment { DepartmentCode="D1", BranchId=7, NameLocal="D1", NameEn="D1", IsActive=true },
            new HrDepartment { DepartmentCode="D2", BranchId=8, NameLocal="D2", NameEn="D2", IsActive=true },
            new HrDepartment { DepartmentCode="FOREIGN", BranchId=9, NameLocal="F", NameEn="F", IsActive=true },
            new HrPosition { PositionCode="P1", DepartmentCode="D1", TitleLocal="P1", TitleEn="P1", IsActive=true },
            new HrPosition { PositionCode="P2", DepartmentCode="D2", TitleLocal="P2", TitleEn="P2", IsActive=true },
            new HrPosition { PositionCode="PF", DepartmentCode="FOREIGN", TitleLocal="PF", TitleEn="PF", IsActive=true }, Employee("A"), Employee("B"));
        db.SaveChanges(); db.ChangeTracker.Clear(); db.HrScopeEnabled=true; db.HrScopeCompanyId=1; db.HrScopeBranchIds.Add(7);
    }
    [Fact] public async Task CatalogReadsRespectCompanyAndBranchIncludingPositionParents()
    {
        var repository = new OrganizationRepository(db);
        Assert.Equal("D1", Assert.Single(await repository.DepartmentsAsync()).DepartmentCode);
        Assert.Equal("P1", Assert.Single(await repository.PositionsAsync(null)).PositionCode);
        Assert.Empty(await repository.PositionsAsync("D2"));
        db.HrScopeAllBranches=true;
        Assert.Equal(new[] { "D1", "D2" }, (await repository.DepartmentsAsync()).Select(x=>x.DepartmentCode));
        Assert.DoesNotContain(await repository.PositionsAsync(null), p=>p.PositionCode=="PF");
    }
    [Theory] [InlineData("D2", null)] [InlineData("FOREIGN", null)] [InlineData("D1", "P2")]
    public async Task InconsistentEmployeeOrganizationIsRejectedBeforeSave(string department, string? position)
    {
        db.HrScopeAllBranches=true;
        var repository=new EmployeeRepository(db); var employee=await repository.GetEmployeeByCodeAsync("A");
        employee!.DepartmentCode=department; employee.PositionCode=position;
        await repository.UpdateEmployeeAsync(employee);
        await Assert.ThrowsAsync<ArgumentException>(()=>repository.SaveChangesAsync());
        Assert.Null((await db.Employees.AsNoTracking().SingleAsync(e=>e.EmployeeCode=="A")).DepartmentCode);
    }
    [Fact] public async Task SecondEdgeCannotCloseManagerCycle()
    {
        var repository=new EmployeeRepository(db); var a=await repository.GetEmployeeByCodeAsync("A"); a!.ManagerEmployeeCode="B";
        await repository.UpdateEmployeeAsync(a); await repository.SaveChangesAsync();
        var b=await repository.GetEmployeeByCodeAsync("B"); b!.ManagerEmployeeCode="A"; await repository.UpdateEmployeeAsync(b);
        await Assert.ThrowsAsync<ArgumentException>(()=>repository.SaveChangesAsync());
        Assert.Null((await db.Employees.AsNoTracking().SingleAsync(e=>e.EmployeeCode=="B")).ManagerEmployeeCode);
    }
    private static DepartmentWrite Department(string? parent = null, long? branch = 7, bool active = true) => new("Local", "English", branch, parent, active);
    private static PositionWrite Position(string department = "D1", string? parent = null, bool active = true) => new(department, "Local", "English", parent, 2, active);

    [Fact] public async Task CatalogCreateUpdatePreservesCreationAuditAndRejectsStaleVersion()
    {
        var repository = new OrganizationRepository(db);
        var created = await repository.WriteDepartmentAsync(" new ", Department(), null, "CREATOR");
        Assert.Equal("NEW", created.DepartmentCode);
        var version = created.Version; var date = created.CreationDate;
        var updated = await repository.WriteDepartmentAsync("NEW", Department() with { NameEn="Changed" }, version, "EDITOR");
        Assert.Equal("CREATOR", updated.CreationUser); Assert.Equal(date, updated.CreationDate);
        Assert.Equal("EDITOR", updated.UpdateUser); Assert.NotEqual(version, updated.Version);
        await Assert.ThrowsAsync<OrganizationVersionConflictException>(() => repository.WriteDepartmentAsync("NEW", Department(), version, "STALE"));
        Assert.Equal("Changed", (await repository.DepartmentAsync("NEW"))!.NameEn);
    }
    [Fact] public async Task DepartmentCycleRejectedWithoutPersistingSecondEdge()
    {
        var repository = new OrganizationRepository(db);
        var a = await repository.WriteDepartmentAsync("A1", Department(), null, "TEST");
        await repository.WriteDepartmentAsync("B1", Department("A1"), null, "TEST");
        await Assert.ThrowsAsync<ArgumentException>(() => repository.WriteDepartmentAsync("A1", Department("B1"), a.Version, "TEST"));
        Assert.Null((await repository.DepartmentAsync("A1"))!.ParentDepartmentCode);
    }
    [Fact] public async Task PositionCycleAndCrossBranchParentAreRejected()
    {
        var repository = new OrganizationRepository(db);
        var a = await repository.WritePositionAsync("A1", Position(), null, "TEST");
        await repository.WritePositionAsync("B1", Position(parent:"A1"), null, "TEST");
        await Assert.ThrowsAsync<ArgumentException>(() => repository.WritePositionAsync("A1", Position(parent:"B1"), a.Version, "TEST"));
        db.HrScopeAllBranches = true;
        await Assert.ThrowsAsync<ArgumentException>(() => repository.WritePositionAsync("C1", Position(parent:"P2"), null, "TEST"));
    }
    [Fact] public async Task ReferencedCatalogCannotBeMovedOrDeactivatedEvenWhenReferencesAreInactive()
    {
        var repository = new OrganizationRepository(db); db.HrScopeAllBranches=true;
        var d = (await repository.DepartmentAsync("D1"))!;
        await Assert.ThrowsAsync<InvalidOperationException>(() => repository.WriteDepartmentAsync("D1", Department(active:false), d.Version, "TEST"));
        await Assert.ThrowsAsync<InvalidOperationException>(() => repository.WriteDepartmentAsync("D1", Department(branch:8), d.Version, "TEST"));
        var e = await db.Employees.SingleAsync(e=>e.EmployeeCode=="A"); e.PositionCode="P1"; e.IsActive=false;
        await db.SaveChangesAsync(); db.ChangeTracker.Clear();
        var p=(await repository.PositionAsync("P1"))!;
        await Assert.ThrowsAsync<InvalidOperationException>(()=>repository.WritePositionAsync("P1", Position(active:false), p.Version, "TEST"));
        await Assert.ThrowsAsync<InvalidOperationException>(()=>repository.WritePositionAsync("P1", Position("D2"), p.Version, "TEST"));
    }
    [Fact] public async Task UnusedCatalogCanBeDeactivatedAndReadForReactivation()
    {
        var repository=new OrganizationRepository(db);
        var d=await repository.WriteDepartmentAsync("EMPTY", Department(), null, "TEST");
        var disabled=await repository.WriteDepartmentAsync("EMPTY", Department(active:false), d.Version, "TEST");
        Assert.DoesNotContain(await repository.DepartmentsAsync(),x=>x.DepartmentCode=="EMPTY");
        Assert.False((await repository.DepartmentAsync("EMPTY"))!.IsActive);
        await repository.WriteDepartmentAsync("EMPTY", Department(), disabled.Version, "TEST");
        Assert.Contains(await repository.DepartmentsAsync(),x=>x.DepartmentCode=="EMPTY");
    }
    [Fact] public async Task SharedForeignAndHiddenCatalogWritesAreDenied()
    {
        var repository=new OrganizationRepository(db);
        await Assert.ThrowsAsync<InvalidOperationException>(()=>repository.WriteDepartmentAsync("SHARED", Department(branch:null), null, "TEST"));
        await Assert.ThrowsAsync<ArgumentException>(()=>repository.WriteDepartmentAsync("NEW", Department(branch:8), null, "TEST"));
        await Assert.ThrowsAsync<InvalidOperationException>(()=>repository.WriteDepartmentAsync("D2", Department(), null, "TEST"));
        db.HrScopeAllBranches=true;
        await Assert.ThrowsAsync<ArgumentException>(()=>repository.WriteDepartmentAsync("NEW", Department(branch:9), null, "TEST"));
        db.HrScopeEmployeeCode="A";
        await Assert.ThrowsAsync<InvalidOperationException>(()=>repository.WritePositionAsync("NEW", Position(), null, "TEST"));
    }
    [Fact] public async Task InvalidInputMissingParentAndMissingVersionsAreRejected()
    {
        var repository=new OrganizationRepository(db);
        await Assert.ThrowsAsync<ArgumentException>(()=>repository.WriteDepartmentAsync("NEW", Department("UNKNOWN"), null, "TEST"));
        await Assert.ThrowsAsync<ArgumentException>(()=>repository.WritePositionAsync("NEW", Position() with { Headcount=-1 }, null, "TEST"));
        await Assert.ThrowsAsync<ArgumentException>(()=>repository.WriteDepartmentAsync("NEW", Department() with { NameEn=" " }, null, "TEST"));
        await Assert.ThrowsAsync<ArgumentException>(()=>repository.WriteDepartmentAsync("D1", Department(), "*", "TEST"));
        await Assert.ThrowsAsync<InvalidOperationException>(()=>repository.WriteDepartmentAsync("D1", Department(), null, "TEST"));
    }
    [Fact] public void CatalogVersionIgnoresDateTimeKindButIncludesRealChanges()
    {
        var d=new HrDepartment { DepartmentCode="D", NameEn="English", CreationDate=new DateTime(2026,11,6,0,0,0,DateTimeKind.Utc) };
        var version=d.Version; d.CreationDate=DateTime.SpecifyKind(d.CreationDate, DateTimeKind.Unspecified);
        Assert.Equal(version,d.Version); d.NameEn="Changed"; Assert.NotEqual(version,d.Version);
        var p=new HrPosition { PositionCode="P", DepartmentCode="D", UpdateDate=new DateTime(2026,11,6,0,0,0,DateTimeKind.Utc) };
        version=p.Version; p.UpdateDate=DateTime.SpecifyKind(p.UpdateDate.Value,DateTimeKind.Unspecified);
        Assert.Equal(version,p.Version); p.Headcount=3; Assert.NotEqual(version,p.Version);
    }
    [Fact] public void PositionResponsePreservesButDoesNotExposePayrollGrade()
    {
        var position=new HrPosition { PositionCode="P", JobGradeCode="PRIVATE_GRADE" };
        var version=position.Version;
        Assert.DoesNotContain("PRIVATE_GRADE", System.Text.Json.JsonSerializer.Serialize(position));
        position.JobGradeCode="CHANGED_GRADE"; Assert.NotEqual(version,position.Version);
    }
    [Fact] public async Task HistoricalPayrollAndHiddenChildrenStillProtectCatalog()
    {
        var repository=new OrganizationRepository(db);
        var d=await repository.WriteDepartmentAsync("HISTORY", Department(), null, "TEST");
        db.Add(new PayrollRunLine { Id=999, PayrollRunId=888, EmployeeCode="HIDDEN", DepartmentCode="HISTORY" });
        await db.SaveChangesAsync(); db.ChangeTracker.Clear();
        await Assert.ThrowsAsync<InvalidOperationException>(()=>repository.WriteDepartmentAsync("HISTORY", Department(active:false), d.Version, "TEST"));
        db.HrScopeAllBranches=true;
        var shared=await repository.WriteDepartmentAsync("SHARED", Department(branch:null), null, "TEST");
        await repository.WriteDepartmentAsync("HIDDENCHILD", Department("SHARED", branch:8), null, "TEST");
        await Assert.ThrowsAsync<InvalidOperationException>(()=>repository.WriteDepartmentAsync("SHARED", Department(branch:null,active:false), shared.Version, "TEST"));
        db.HrScopeAllBranches=false;
        await Assert.ThrowsAsync<InvalidOperationException>(()=>repository.WriteDepartmentAsync("SHARED", Department(), shared.Version, "TEST"));
    }
    [Theory] [InlineData(false)] [InlineData(true)]
    public async Task VisibleParentCannotBringHiddenLegacyAncestorIntoNewRelationship(bool position)
    {
        var d=await db.Set<HrDepartment>().SingleAsync(x=>x.DepartmentCode=="D1"); d.ParentDepartmentCode="D2";
        var p=await db.Set<HrPosition>().SingleAsync(x=>x.PositionCode=="P1"); p.ReportsToPositionCode="P2";
        await db.SaveChangesAsync(); db.ChangeTracker.Clear();
        var repository=new OrganizationRepository(db);
        if (position) await Assert.ThrowsAsync<ArgumentException>(()=>repository.WritePositionAsync("NEW", Position(parent:"P1"), null, "TEST"));
        else await Assert.ThrowsAsync<ArgumentException>(()=>repository.WriteDepartmentAsync("NEW", Department("D1"), null, "TEST"));
        Assert.Null(await repository.DepartmentAsync("NEW")); Assert.Null(await repository.PositionAsync("NEW"));
    }

    [Fact]
    public async Task EmployeeOrganizationValidator_CompanyScopeZeroOrNegative_ThrowsInvalidOperationException()
    {
        var repository = new EmployeeRepository(db);
        var employee = await repository.GetEmployeeByCodeAsync("A");
        employee!.DepartmentCode = "D1";

        db.HrScopeEnabled = false;
        db.HrScopeCompanyId = 0;
        await repository.UpdateEmployeeAsync(employee);
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => repository.SaveChangesAsync());
        Assert.Contains("Trusted company scope is required", ex.Message);
    }

    [Fact]
    public async Task EmployeeOrganizationValidator_InactiveManager_IsRejectedBeforeSave()
    {
        db.HrScopeAllBranches = true;
        var b = await db.Employees.SingleAsync(e => e.EmployeeCode == "B");
        b.IsActive = false;
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();

        var repository = new EmployeeRepository(db);
        var a = await repository.GetEmployeeByCodeAsync("A");
        a!.ManagerEmployeeCode = "B";
        await repository.UpdateEmployeeAsync(a);

        var ex = await Assert.ThrowsAsync<ArgumentException>(() => repository.SaveChangesAsync());
        Assert.Contains("Manager is unavailable in the current employee/branch scope", ex.Message);
    }

    [Fact]
    public async Task EmployeeOrganizationValidator_TerminatedManager_IsRejectedBeforeSave()
    {
        db.HrScopeAllBranches = true;
        var b = await db.Employees.SingleAsync(e => e.EmployeeCode == "B");
        b.EmploymentStatus = "TERMINATED";
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();

        var repository = new EmployeeRepository(db);
        var a = await repository.GetEmployeeByCodeAsync("A");
        a!.ManagerEmployeeCode = "B";
        await repository.UpdateEmployeeAsync(a);

        var ex = await Assert.ThrowsAsync<ArgumentException>(() => repository.SaveChangesAsync());
        Assert.Contains("Manager is unavailable in the current employee/branch scope", ex.Message);
    }

    [Fact]
    public async Task EmployeeOrganizationValidator_InactiveDepartment_IsRejectedBeforeSave()
    {
        db.HrScopeAllBranches = true;
        var d1 = await db.Set<HrDepartment>().SingleAsync(d => d.DepartmentCode == "D1");
        d1.IsActive = false;
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();

        var repository = new EmployeeRepository(db);
        var a = await repository.GetEmployeeByCodeAsync("A");
        a!.DepartmentCode = "D1";
        await repository.UpdateEmployeeAsync(a);

        var ex = await Assert.ThrowsAsync<ArgumentException>(() => repository.SaveChangesAsync());
        Assert.Contains("Department is unavailable or does not belong to the employee branch", ex.Message);
    }

    [Fact]
    public async Task EmployeeOrganizationValidator_InactivePosition_IsRejectedBeforeSave()
    {
        db.HrScopeAllBranches = true;
        var p1 = await db.Set<HrPosition>().SingleAsync(p => p.PositionCode == "P1");
        p1.IsActive = false;
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();

        var repository = new EmployeeRepository(db);
        var a = await repository.GetEmployeeByCodeAsync("A");
        a!.DepartmentCode = "D1";
        a.PositionCode = "P1";
        await repository.UpdateEmployeeAsync(a);

        var ex = await Assert.ThrowsAsync<ArgumentException>(() => repository.SaveChangesAsync());
        Assert.Contains("Position is unavailable or does not belong to the selected department", ex.Message);
    }

    [Fact]
    public async Task EmployeeOrganizationValidator_NonOrgFieldChange_SkipsOrganizationValidation()
    {
        // When org fields are not touched, saving contact details does not trigger org validator
        var repository = new EmployeeRepository(db);
        var a = await repository.GetEmployeeByCodeAsync("A");
        a!.NationalId = "NEW_NID_123";
        await repository.UpdateEmployeeAsync(a);
        await repository.SaveChangesAsync();

        var reloaded = await db.Employees.AsNoTracking().SingleAsync(e => e.EmployeeCode == "A");
        Assert.Equal("NEW_NID_123", reloaded.NationalId);
    }

    public void Dispose()=>db.Dispose();
}

