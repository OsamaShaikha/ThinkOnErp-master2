using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Oracle.ManagedDataAccess.Client;
using ThinkOnErp.Infrastructure.Data;
using ThinkOnErp.Infrastructure.Services.Hr;

internal static class HrEmployeeAccountLockVerification
{
    public static async Task Verify(OracleConnection connection, string connectionString, List<string> report)
    {
        static OracleDbContext Context(OracleConnection c) => new(new DbContextOptionsBuilder<OracleDbContext>().UseOracle(c,
            o=>o.UseOracleSQLCompatibility(OracleSQLCompatibility.DatabaseVersion19)).Options);
        using var firstDb=Context(connection);
        var seed=await (from e in firstDb.Employees.AsNoTracking() join b in firstDb.SysBranches on e.BranchId equals (long?)b.Id
            where b.IsActive && b.CompanyId.HasValue select new { e.EmployeeCode, CompanyId=b.CompanyId!.Value }).FirstAsync();
        firstDb.HrScopeEnabled=true; firstDb.HrScopeAllBranches=true; firstDb.HrScopeCompanyId=seed.CompanyId;
        await using var secondConnection=new OracleConnection(connectionString);
        await secondConnection.OpenAsync();
        await using (var schema=secondConnection.CreateCommand()) { schema.CommandText="ALTER SESSION SET CURRENT_SCHEMA=DEV_TEMPLATE"; await schema.ExecuteNonQueryAsync(); }
        using var secondDb=Context(secondConnection);
        secondDb.HrScopeEnabled=true; secondDb.HrScopeAllBranches=true; secondDb.HrScopeCompanyId=seed.CompanyId;
        using var deadline=new CancellationTokenSource(TimeSpan.FromSeconds(20));
        var entered=new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release=new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var secondEntered=new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var first=new HrUnitOfWork(firstDb).ExecuteEmployeeAccountAsync(seed.EmployeeCode,async ()=>
        { entered.SetResult(); await release.Task.WaitAsync(deadline.Token); return true; },deadline.Token);
        Task<bool>? second=null;
        try
        {
            await entered.Task.WaitAsync(deadline.Token);
            await using (var probe=await secondDb.Database.BeginTransactionAsync(System.Data.IsolationLevel.ReadCommitted,deadline.Token))
            {
                async Task Locked(string sql,string parameterName,object value)
                {
                    await using var command=secondConnection.CreateCommand(); command.Transaction=(OracleTransaction)probe.GetDbTransaction();
                    command.CommandText=sql; command.CommandTimeout=5;
                    command.Parameters.Add(new OracleParameter(parameterName,value));
                    try { await command.ExecuteScalarAsync(deadline.Token); }
                    catch(OracleException ex) when(ex.Number==54) { return; }
                    throw new InvalidOperationException("Expected account lock was not held by the first Oracle session.");
                }
                await Locked("SELECT \"Id\" FROM SYS_BRANCH WHERE \"Id\"=(SELECT MIN(\"Id\") FROM SYS_BRANCH WHERE COMPANY_ID=:companyId) FOR UPDATE NOWAIT","companyId",seed.CompanyId);
                await Locked("SELECT 1 FROM HR_EMPLOYEE WHERE EMPLOYEE_CODE=:code FOR UPDATE NOWAIT","code",seed.EmployeeCode);
                await probe.RollbackAsync(CancellationToken.None);
            }
            second=new HrUnitOfWork(secondDb).ExecuteEmployeeAccountAsync(seed.EmployeeCode,()=>
            { secondEntered.SetResult(); return Task.FromResult(true); },deadline.Token);
            await Task.WhenAny(secondEntered.Task,Task.Delay(250,deadline.Token));
            if(secondEntered.Task.IsCompleted || second.IsCompleted) throw new InvalidOperationException("Second account callback entered before the first transaction released its locks.");
        }
        finally
        {
            release.TrySetResult();
            try { await first.WaitAsync(TimeSpan.FromSeconds(20)); }
            finally { if(second!=null) await second.WaitAsync(TimeSpan.FromSeconds(20)); }
        }
        if(second==null || !await second.WaitAsync(TimeSpan.FromSeconds(20)) || !secondEntered.Task.IsCompleted)
            throw new InvalidOperationException("Second account callback did not resume after lock release.");
        report.Add("Oracle two-session account lock check: NOWAIT rejected company-anchor and employee-row lock acquisition while the first guarded callback held them; a second account callback waited and completed after release. Both callbacks performed no DML; no profile, account, grant or payroll data changed. This does not prove all payroll/manager concurrency scenarios or Oracle HTTP.");
    }
}
