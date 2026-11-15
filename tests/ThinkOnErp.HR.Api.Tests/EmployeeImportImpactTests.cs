using Microsoft.EntityFrameworkCore;
using ThinkOnErp.Domain.Entities;
using ThinkOnErp.Domain.Entities.Hr;
using ThinkOnErp.Infrastructure.Data;
using ThinkOnErp.Infrastructure.Repositories.Hr;
using Xunit;

namespace ThinkOnErp.HR.Api.Tests;

public sealed class EmployeeImportImpactTests : IDisposable
{
    private readonly OracleDbContext db=new(new DbContextOptionsBuilder<OracleDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
    public EmployeeImportImpactTests()
    {
        db.AddRange(new SysBranch { Id=7,CompanyId=1,IsActive=true },new SysBranch { Id=8,CompanyId=1,IsActive=true },new SysBranch { Id=9,CompanyId=2,IsActive=true },
            new Employee { EmployeeCode="E",BranchId=7 },
            new PayrollPeriod { Id=1,CompanyId=1,PeriodCode="OLD",StartDate=new(2020,1,1),EndDate=new(2020,1,31) },
            new PayrollPeriod { Id=2,CompanyId=1,PeriodCode="OPEN",Status="OPEN",StartDate=new(2026,1,1),EndDate=new(2026,1,31) },
            new PayrollPeriod { Id=3,CompanyId=1,PeriodCode="CLOSED",Status="CLOSED",StartDate=new(2027,1,1),EndDate=new(2027,1,31) },
            new PayrollPeriod { Id=4,CompanyId=2,PeriodCode="FOREIGN",Status="CLOSED",StartDate=new(2026,1,1),EndDate=new(2026,1,31) },
            new PayrollRun { Id=1,BranchId=7,PayPeriod="OPEN",Status="APPROVED" },new PayrollRun { Id=2,BranchId=8,PayPeriod="CLOSED",Status="PAID" },
            new PayrollRun { Id=3,BranchId=7,PayPeriod="ORPHAN",Status="APPROVED" },new PayrollRun { Id=4,BranchId=9,PayPeriod="FOREIGN",Status="APPROVED" });
        db.SaveChanges(); db.ChangeTracker.Clear(); db.HrScopeEnabled=true; db.HrScopeCompanyId=1; db.HrScopeBranchIds.Add(7);
    }
    [Fact] public async Task PreviewPeriodMetadataIsCompanyScopedAndApprovalIsBranchScoped()
    {
        var impact=await new EmployeeRepository(db).GetImportImpactAsync("E",7,new(2026,1,1));
        Assert.Equal(new[] { "OPEN","CLOSED" },impact.Periods.Select(p=>p.PeriodCode));
        Assert.True(impact.Periods[0].HasApprovedPayroll); Assert.False(impact.Periods[1].HasApprovedPayroll); Assert.True(impact.HasUnmappedApprovedPayroll);
        Assert.False(db.ChangeTracker.HasChanges());
    }
    [Theory] [InlineData(8L)] [InlineData(9L)] [InlineData(null)]
    public async Task UnauthorizedTargetCannotReadImpact(long? branch)
        =>await Assert.ThrowsAsync<UnauthorizedAccessException>(()=>new EmployeeRepository(db).GetImportImpactAsync("E",branch,new(2026,1,1)));
    [Fact] public async Task HiddenExistingAndSelfScopeCannotReadImpact()
    {
        var repository=new EmployeeRepository(db); db.HrScopeBranchIds.Clear(); db.HrScopeBranchIds.Add(8);
        await Assert.ThrowsAsync<UnauthorizedAccessException>(()=>repository.GetImportImpactAsync("E",8,new(2026,1,1)));
        db.HrScopeEmployeeCode="E";
        await Assert.ThrowsAsync<UnauthorizedAccessException>(()=>repository.GetImportImpactAsync("E",8,new(2026,1,1)));
    }
    [Fact] public async Task AuthorizedBranchMoveIncludesBothBranchApprovalInputs()
    {
        db.HrScopeAllBranches=true;
        var impact=await new EmployeeRepository(db).GetImportImpactAsync("E",8,new(2026,1,1));
        Assert.All(impact.Periods,p=>Assert.True(p.HasApprovedPayroll)); Assert.DoesNotContain(impact.Periods,p=>p.PeriodCode=="FOREIGN");
    }
    [Fact] public async Task HiddenHistoricalRunProtectsOnlyItsRelatedVisibleEmployee()
    {
        db.Remove(await db.PayrollRuns.SingleAsync(r=>r.Id==3));
        var hidden=new PayrollRun { Id=5,BranchId=8,PayPeriod="HIDDEN_ORPHAN",Status="APPROVED" };
        hidden.Lines.Add(new PayrollRunLine { Id=6,EmployeeCode="E",BranchId=8 }); db.Add(hidden); await db.SaveChangesAsync();
        var repository=new EmployeeRepository(db);
        Assert.True((await repository.GetImportImpactAsync("E",7,new(2026,1,1))).HasUnmappedApprovedPayroll);
        Assert.False((await repository.GetImportImpactAsync("NEW",7,new(2026,1,1))).HasUnmappedApprovedPayroll);
    }
    public void Dispose()=>db.Dispose();
}
