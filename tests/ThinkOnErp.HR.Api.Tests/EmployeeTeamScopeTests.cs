using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using ThinkOnErp.Application.Services.Hr;
using ThinkOnErp.Domain.Entities;
using ThinkOnErp.Domain.Entities.Hr;
using ThinkOnErp.Infrastructure.Data;
using ThinkOnErp.Infrastructure.Repositories.Hr;
using Xunit;

namespace ThinkOnErp.HR.Api.Tests;

public sealed class EmployeeTeamScopeTests : IDisposable
{
    private readonly OracleDbContext db=new(new DbContextOptionsBuilder<OracleDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
    private static Employee Employee(string code,long branch,string? manager)=>new() { EmployeeCode=code,NameLocal=code,NameEn=code,BranchId=branch,ManagerEmployeeCode=manager,NationalId="PRIVATE_ID",BankIban="PRIVATE_IBAN",Phone="PRIVATE_PHONE",Email="PRIVATE_EMAIL" };
    public EmployeeTeamScopeTests()
    {
        var manager=Employee("M",7,"M"); manager.UserId=12;
        var direct=Employee("A",7,"M"); direct.SalaryStructures.Add(new SalaryStructure { BasicSalary=9876m });
        var inactive=Employee("I",7,"M"); inactive.IsActive=false; inactive.EmploymentStatus="TERMINATED";
        db.AddRange(new SysBranch { Id=7,CompanyId=1,IsActive=true },new SysBranch { Id=8,CompanyId=1,IsActive=true },new SysBranch { Id=9,CompanyId=2,IsActive=true },
            new SysUser { Id=12,CompanyId=1,IsActive=true },new SysUserBranch { Id=1,UserId=12,BranchId=7 },manager,direct,inactive,
            Employee("B",8,"M"),Employee("FOREIGN",9,"M"),Employee("GRANDCHILD",7,"A"),Employee("PEER",7,"OTHER"));
        db.SaveChanges(); db.ChangeTracker.Clear(); db.HrScopeEnabled=true; db.HrScopeCompanyId=1; db.HrScopeBranchIds.Add(7);
    }
    [Fact] public async Task DirectTeamIntersectionExcludesHiddenForeignPeerGrandchildAndSelf()
    {
        var repository=new EmployeeRepository(db);
        var (items,total)=await repository.GetDirectReportsAsync(12,true,null,1,20);
        Assert.Equal(1,total); Assert.Equal("A",Assert.Single(items).EmployeeCode);
        Assert.Empty(items[0].NationalId); Assert.Null(items[0].BankIban); Assert.Empty(items[0].SalaryStructures);
        db.HrScopeAllBranches=true;
        Assert.Equal(new[] { "A","B" },(await repository.GetDirectReportsAsync(12,true,null,1,20)).Items.Select(e=>e.EmployeeCode));
        Assert.Equal(new[] { "A","B","I" },(await repository.GetDirectReportsAsync(12,false,null,1,20)).Items.Select(e=>e.EmployeeCode));
    }
    [Fact] public async Task ManagerOwnLinkCanResolveOutsideReportHrGrantWithoutBroadeningReports()
    {
        db.HrScopeBranchIds.Clear(); db.HrScopeBranchIds.Add(8);
        var result=await new EmployeeRepository(db).GetDirectReportsAsync(12,true,null,1,20);
        Assert.Equal("B",Assert.Single(result.Items).EmployeeCode);
    }
    [Theory] [InlineData("unlink")] [InlineData("inactive")] [InlineData("suspended")] [InlineData("account")] [InlineData("membership")] [InlineData("company")]
    public async Task ManagerMustHaveCurrentActiveAccountEmployeeAndOwnBranchMembership(string change)
    {
        var manager=await db.Employees.SingleAsync(e=>e.EmployeeCode=="M");
        if(change=="unlink") manager.UserId=null;
        if(change=="inactive") manager.IsActive=false;
        if(change=="suspended") manager.EmploymentStatus="SUSPENDED";
        if(change=="account") (await db.SysUsers.SingleAsync()).IsActive=false;
        if(change=="membership") db.Remove(await db.SysUserBranches.SingleAsync());
        if(change=="company") (await db.SysUsers.SingleAsync()).CompanyId=2;
        await db.SaveChangesAsync();
        await Assert.ThrowsAsync<KeyNotFoundException>(()=>new EmployeeRepository(db).GetDirectReportsAsync(12,true,null,1,20));
    }
    [Fact] public async Task SelfOrEmptyScopeDoesNotBecomeManagementPermission()
    {
        var repository=new EmployeeRepository(db); db.HrScopeEmployeeCode="M";
        await Assert.ThrowsAsync<InvalidOperationException>(()=>repository.GetDirectReportsAsync(12,true,null,1,20));
        db.HrScopeEmployeeCode=null; db.HrScopeBranchIds.Clear();
        await Assert.ThrowsAsync<InvalidOperationException>(()=>repository.GetDirectReportsAsync(12,true,null,1,20));
    }
    [Fact] public async Task TeamPageIsStableCountsOnlyVisibleRowsAndDoesNotSearchSensitiveFields()
    {
        db.AddRange(Employee("D",7,"M"),Employee("E",7,"M")); await db.SaveChangesAsync();
        var repository=new EmployeeRepository(db);
        var result=await repository.GetDirectReportsAsync(12,true,null,2,1);
        Assert.Equal(3,result.TotalCount); Assert.Equal("D",Assert.Single(result.Items).EmployeeCode);
        Assert.Empty((await repository.GetDirectReportsAsync(12,true,"PRIVATE",1,20)).Items);
        Assert.Equal(0,(await repository.GetDirectReportsAsync(12,true,"FOREIGN",1,20)).TotalCount);
    }
    [Theory] [InlineData(0,20)] [InlineData(1,0)] [InlineData(1,201)] [InlineData(int.MaxValue,200)]
    public async Task InvalidPagesCannotReachUnboundedQuery(int page,int size)
        =>await Assert.ThrowsAsync<ArgumentException>(()=>new EmployeeRepository(db).GetDirectReportsAsync(12,true,null,page,size));
    [Fact] public async Task ResponseContainsOnlyOrganizationalFields()
    {
        var (items,_)=await new EmployeeService(new EmployeeRepository(db)).GetDirectReportsAsync(12,true,null,1,20);
        var json=JsonSerializer.Serialize(Assert.Single(items));
        Assert.DoesNotContain("PRIVATE",json); Assert.DoesNotContain("9876",json);
        using var doc=JsonDocument.Parse(json);
        Assert.Equal(new[] { "BranchId","DepartmentCode","EmployeeCode","EmploymentStatus","IsActive","NameEn","NameLocal","PositionCode" },doc.RootElement.EnumerateObject().Select(p=>p.Name).OrderBy(n=>n));
    }
    [Fact] public async Task GetDirectReportsAsync_PropagatesCancellationToken()
    {
        using var cts = new CancellationTokenSource();
        var (items, total) = await new EmployeeRepository(db).GetDirectReportsAsync(12, true, null, 1, 20, cts.Token);
        Assert.Equal(1, total);
        Assert.Single(items);
    }
    [Fact] public async Task GetDirectReportsAsync_WhenSearchExceeds200Chars_ThrowsArgumentException()
    {
        var longSearch = new string('x', 201);
        await Assert.ThrowsAsync<ArgumentException>(() => new EmployeeRepository(db).GetDirectReportsAsync(12, true, longSearch, 1, 20));
    }
    [Fact] public async Task GetDirectReportsAsync_FiltersByCodeAndLocalName()
    {
        db.AddRange(Employee("CODE_X", 7, "M"), Employee("CODE_Y", 7, "M"));
        await db.SaveChangesAsync();
        var repository = new EmployeeRepository(db);
        var (items, total) = await repository.GetDirectReportsAsync(12, true, "CODE_X", 1, 20);
        Assert.Equal(1, total);
        Assert.Equal("CODE_X", Assert.Single(items).EmployeeCode);
    }
    [Fact] public async Task GetDirectReportsAsync_PagingWorksDeterministically()
    {
        db.AddRange(Employee("C1", 7, "M"), Employee("C2", 7, "M"), Employee("C3", 7, "M"));
        await db.SaveChangesAsync();
        var repository = new EmployeeRepository(db);
        var page1 = await repository.GetDirectReportsAsync(12, true, null, 1, 2);
        Assert.Equal(4, page1.TotalCount); // A, C1, C2, C3
        Assert.Equal(2, page1.Items.Count);
        Assert.Equal("A", page1.Items[0].EmployeeCode);
        Assert.Equal("C1", page1.Items[1].EmployeeCode);

        var page2 = await repository.GetDirectReportsAsync(12, true, null, 2, 2);
        Assert.Equal(4, page2.TotalCount);
        Assert.Equal(2, page2.Items.Count);
        Assert.Equal("C2", page2.Items[0].EmployeeCode);
        Assert.Equal("C3", page2.Items[1].EmployeeCode);
    }
    public void Dispose()=>db.Dispose();
}
