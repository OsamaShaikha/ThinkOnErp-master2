using Microsoft.EntityFrameworkCore;
using ThinkOnErp.Application.Services.Hr;
using ThinkOnErp.Domain.Entities;
using ThinkOnErp.Domain.Entities.Hr;
using ThinkOnErp.Infrastructure.Data;
using ThinkOnErp.Infrastructure.Repositories.Hr;
using ThinkOnErp.Infrastructure.Services.Hr;
using Xunit;

namespace ThinkOnErp.HR.Api.Tests;

public sealed class EmployeeAccountScopeTests : IDisposable
{
    private readonly DbContextOptions<OracleDbContext> options = new DbContextOptionsBuilder<OracleDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options;
    private readonly OracleDbContext db;
    private EmployeeService Service => new(new EmployeeRepository(db), new HrUnitOfWork(db));
    public EmployeeAccountScopeTests()
    {
        db = new(options);
        db.AddRange(new SysBranch { Id=7, CompanyId=1, IsActive=true }, new SysBranch { Id=8, CompanyId=1, IsActive=true }, new SysBranch { Id=9, CompanyId=2, IsActive=true },
            new SysUser { Id=12, CompanyId=1, IsActive=true }, new SysUser { Id=13, CompanyId=2, IsActive=true }, new SysUser { Id=14, CompanyId=1, IsActive=false }, new SysUser { Id=15, CompanyId=1, IsActive=true },
            new SysUserBranch { Id=1, UserId=12, BranchId=7 }, new SysUserBranch { Id=2, UserId=13, BranchId=7 }, new SysUserBranch { Id=3, UserId=14, BranchId=7 },
            new Employee { EmployeeCode="A", BranchId=7, NameLocal="Local", NameEn="English", NationalId="A", Nationality="X", DateOfBirth=new(1990,1,1), HireDate=new(2020,1,1) });
        db.SaveChanges(); db.ChangeTracker.Clear(); db.HrScopeEnabled=true; db.HrScopeCompanyId=1; db.HrScopeBranchIds.Add(7);
    }
    [Theory] [InlineData(13)] [InlineData(14)] [InlineData(15)]
    public async Task ForeignInactiveOrUnassignedUserCannotBeLinked(long id)
    {
        await Assert.ThrowsAsync<InvalidOperationException>(()=>Service.LinkEmployeeUserAsync("A",id,"ADMIN"));
        Assert.Null((await db.Employees.AsNoTracking().SingleAsync()).UserId);
    }
    [Fact] public async Task AccountGuardReloadsRootAfterExternalProfileChange()
    {
        var tracked=await db.Employees.SingleAsync();
        using (var external=new OracleDbContext(options))
        {
            var e=await external.Employees.SingleAsync(); e.NameEn="Fresh profile"; await external.SaveChangesAsync();
        }
        await Service.LinkEmployeeUserAsync(" a ",12,"ADMIN");
        Assert.Equal("Fresh profile",tracked.NameEn);
        Assert.Equal("Fresh profile",(await db.Employees.AsNoTracking().SingleAsync()).NameEn);
        Assert.Equal(12,(await db.Employees.AsNoTracking().SingleAsync()).UserId);
    }
    [Fact] public async Task TargetedAccountSaveCannotRewriteDetachedStaleProfileOrChild()
    {
        var stale=await db.Employees.AsNoTracking().SingleAsync(); stale.NameEn="STALE";
        stale.UserId=12; stale.UpdateUser="ADMIN"; stale.UpdateDate=DateTime.UtcNow;
        stale.Dependents.Add(new EmployeeDependent { EmployeeCode="A", NameEn="STALE CHILD" });
        var repository=new EmployeeRepository(db); await repository.UpdateEmployeeUserLinkAsync(stale); await repository.SaveChangesAsync();
        using var read=new OracleDbContext(options);
        Assert.Equal("English",(await read.Employees.SingleAsync()).NameEn);
        Assert.Empty(await read.EmployeeDependents.ToListAsync());
    }
    [Fact] public async Task UnlinkAllowsClosedPeriodAndTerminatedLegacyEmployeeWithoutDisablingUser()
    {
        var e=await db.Employees.SingleAsync(); e.UserId=12; e.IsActive=false; e.EmploymentStatus="TERMINATED"; e.NameEn="";
        db.PayrollPeriods.Add(new PayrollPeriod { Id=1, CompanyId=1, PeriodCode="CLOSED", Status="CLOSED" });
        await db.SaveChangesAsync(); db.ChangeTracker.Clear();
        Assert.Null((await Service.LinkEmployeeUserAsync("A",null,"ADMIN")).UserId);
        Assert.True((await db.SysUsers.SingleAsync(u=>u.Id==12)).IsActive);
        Assert.Equal("TERMINATED",(await db.Employees.AsNoTracking().SingleAsync()).EmploymentStatus);
    }
    [Fact] public async Task MissingCompanySelfScopeAndPendingWritesCannotEnterAccountMutation()
    {
        db.HrScopeCompanyId=0;
        await Assert.ThrowsAsync<InvalidOperationException>(()=>Service.LinkEmployeeUserAsync("A",12,"ADMIN"));
        db.HrScopeCompanyId=1; db.HrScopeEmployeeCode="A";
        await Assert.ThrowsAsync<InvalidOperationException>(()=>Service.LinkEmployeeUserAsync("A",12,"ADMIN"));
        db.HrScopeEmployeeCode=null;
        (await db.Employees.SingleAsync()).NameEn="Pending";
        await Assert.ThrowsAsync<InvalidOperationException>(()=>Service.LinkEmployeeUserAsync("A",12,"ADMIN"));
        Assert.Null((await db.Employees.AsNoTracking().SingleAsync()).UserId);
    }
    [Fact] public async Task LinkChangesExistingFingerprintButIdempotentLinkDoesNot()
    {
        var period=new PayrollPeriod { Id=1, CompanyId=1, PeriodCode="OPEN", Status="OPEN", StartDate=new(2026,11,1), EndDate=new(2026,11,30) };
        db.Add(period); await db.SaveChangesAsync(); db.ChangeTracker.Clear();
        var snapshots=new PayrollInputSnapshotService(db);
        var before=await snapshots.CaptureAsync(period,7);
        await Service.LinkEmployeeUserAsync("A",12,"ADMIN");
        var after=await snapshots.CaptureAsync(period,7); Assert.NotEqual(before,after);
        await Service.LinkEmployeeUserAsync("A",12,"OTHER_ADMIN");
        Assert.Equal(after,await snapshots.CaptureAsync(period,7));
    }
    [Fact] public async Task UnchangedTrackedTerminatedEmployeeCannotDisableAnUnrelatedAccount()
    {
        db.AddRange(new SysUser { Id=16,CompanyId=1,IsActive=true },new Employee { EmployeeCode="LEGACY",BranchId=8,UserId=16,IsActive=false,EmploymentStatus="TERMINATED" });
        await db.SaveChangesAsync(); db.ChangeTracker.Clear();
        await db.Employees.IgnoreQueryFilters().SingleAsync(e=>e.EmployeeCode=="LEGACY");
        await Service.LinkEmployeeUserAsync("A",12,"ADMIN");
        Assert.True((await db.SysUsers.SingleAsync(u=>u.Id==16)).IsActive);
    }
    [Theory] [InlineData(0)] [InlineData(-5)]
    public async Task LinkEmployeeUserAsync_WhenUserZeroOrNegative_ThrowsArgumentException(long userId)
    {
        await Assert.ThrowsAsync<ArgumentException>(() => Service.LinkEmployeeUserAsync("A", userId, "ADMIN"));
    }
    [Theory] [InlineData("")] [InlineData("   ")]
    public async Task LinkEmployeeUserAsync_WhenEmployeeCodeWhitespace_ThrowsArgumentException(string code)
    {
        await Assert.ThrowsAsync<ArgumentException>(() => Service.LinkEmployeeUserAsync(code, 12, "ADMIN"));
    }
    [Fact] public async Task LinkEmployeeUserAsync_WhenUserAlreadyLinkedToAnotherEmployee_ThrowsInvalidOperationException()
    {
        var other = new Employee { EmployeeCode="OTHER", BranchId=7, NameLocal="Other", NameEn="Other", NationalId="O", Nationality="X", DateOfBirth=new(1990,1,1), HireDate=new(2020,1,1), UserId=12 };
        db.Add(other); await db.SaveChangesAsync(); db.ChangeTracker.Clear();
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => Service.LinkEmployeeUserAsync("A", 12, "ADMIN"));
        Assert.Contains("already linked to another employee", ex.Message);
    }
    [Fact] public async Task LinkEmployeeUserAsync_WhenEmployeeInactive_ThrowsInvalidOperationException()
    {
        var e = await db.Employees.SingleAsync(x => x.EmployeeCode == "A");
        e.IsActive = false; await db.SaveChangesAsync(); db.ChangeTracker.Clear();
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => Service.LinkEmployeeUserAsync("A", 12, "ADMIN"));
        Assert.Contains("Only active employees can be linked", ex.Message);
    }
    [Fact] public async Task LinkEmployeeUserAsync_WhenAlreadyLinked_ReturnsIdempotently()
    {
        var result1 = await Service.LinkEmployeeUserAsync("A", 12, "ADMIN");
        Assert.Equal(12, result1.UserId);
        var result2 = await Service.LinkEmployeeUserAsync("A", 12, "ADMIN");
        Assert.Equal(12, result2.UserId);
    }
    [Fact] public async Task LinkEmployeeUserAsync_PropagatesCancellationToken()
    {
        using var cts = new CancellationTokenSource();
        var result = await Service.LinkEmployeeUserAsync("A", 12, "ADMIN", cts.Token);
        Assert.Equal(12, result.UserId);
    }
    public void Dispose()=>db.Dispose();
}
