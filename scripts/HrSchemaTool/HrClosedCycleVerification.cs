using Microsoft.EntityFrameworkCore;
using Oracle.ManagedDataAccess.Client;
using ThinkOnErp.Domain.Entities.Hr;
using ThinkOnErp.Infrastructure.Data;
using ThinkOnErp.Infrastructure.Repositories.Hr;

internal static class HrClosedCycleVerification
{
    public static async Task Verify(OracleConnection connection, List<string> report)
    {
        using var db = new OracleDbContext(new DbContextOptionsBuilder<OracleDbContext>().UseOracle(connection,
            o => o.UseOracleSQLCompatibility(OracleSQLCompatibility.DatabaseVersion19)).Options);
        await using var transaction = await db.Database.BeginTransactionAsync(System.Data.IsolationLevel.ReadCommitted);
        var repository = new LeaveRepository(db);
        var source = await db.LeaveBalances.AsNoTracking().FirstAsync();
        var start = new DateTime(9000, 9, 1);
        var end = new DateTime(9001, 8, 31);
        var close = new LeaveYearClose { CloseKey = "VERIFY_" + Guid.NewGuid().ToString("N"), FromYear = 9000, ToYear = 9001,
            LeaveTypeCode = source.LeaveTypeCode, CreationUser = "HR_VERIFY" };
        db.LeaveYearCloses.Add(close);
        await db.SaveChangesAsync();
        var closeId = close.Id;
        var blocked = 0;
        try
        {
            await repository.ExecuteForLeaveEmployeeAsync(source.EmployeeCode, async () =>
            {
                await repository.EnsureLeaveCycleOpenAsync(source.EmployeeCode, source.LeaveTypeCode, start, end);
                close.Status = "POSTED";
                await db.SaveChangesAsync();
                // Even a header without a balance line closes the requested cycle.
                try { await repository.EnsureLeaveCycleOpenAsync(source.EmployeeCode, source.LeaveTypeCode, start, end); }
                catch (InvalidOperationException ex) when (ex.Message.StartsWith("This leave cycle is closed.")) { blocked++; }
                try { await repository.ApplyAccrualTargetAsync(new LeaveBalance { EmployeeCode = source.EmployeeCode,
                    LeaveTypeCode = source.LeaveTypeCode, YearNo = 9000, CycleStartDate = start, CycleEndDate = end, AccruedDays = 10 }, "HR_VERIFY"); }
                catch (InvalidOperationException ex) when (ex.Message.StartsWith("This leave cycle is closed.")) { blocked++; }
                // A historical line must also block overlapping dates after cycle-policy changes.
                db.LeaveYearCloseLines.Add(new LeaveYearCloseLine { CloseId = close.Id, EmployeeCode = source.EmployeeCode,
                    LeaveTypeCode = source.LeaveTypeCode, SourceYear = 9000, TargetYear = 9001, SourceBalanceId = source.Id,
                    SourceCycleStartDate = start, SourceCycleEndDate = end,
                    TargetCycleStartDate = end.AddDays(1), TargetCycleEndDate = end.AddYears(1) });
                await db.SaveChangesAsync();
                try { await repository.EnsureLeaveCycleOpenAsync(source.EmployeeCode, source.LeaveTypeCode, new DateTime(9001, 1, 1), new DateTime(9001, 12, 31)); }
                catch (InvalidOperationException ex) when (ex.Message.StartsWith("This leave cycle is closed.")) { blocked++; }
                await repository.EnsureLeaveCycleOpenAsync(source.EmployeeCode, source.LeaveTypeCode, end.AddDays(1), end.AddYears(1));
                close.Status = "UNPOSTED";
                await db.SaveChangesAsync();
                await repository.EnsureLeaveCycleOpenAsync(source.EmployeeCode, source.LeaveTypeCode, start, end);
                // Wildcard headers must also close a type without a source line.
                close.Status = "POSTED";
                close.LeaveTypeCode = "*";
                await db.SaveChangesAsync();
                try { await repository.EnsureLeaveCycleOpenAsync(source.EmployeeCode, source.LeaveTypeCode, start, end); }
                catch (InvalidOperationException ex) when (ex.Message.StartsWith("This leave cycle is closed.")) { blocked++; }
                return true;
            });
            if (blocked != 4) throw new InvalidOperationException("Closed-cycle guard did not reject all expected writes.");
            report.Add("Oracle closed-cycle guard: draft/unposted and next cycle accepted; type/wildcard posted headers, accrual and historical overlap blocked.");
        }
        finally { await transaction.RollbackAsync(); db.ChangeTracker.Clear(); }
        if (await db.LeaveYearCloses.AnyAsync(c => c.Id == closeId) || await db.LeaveYearCloseLines.AnyAsync(l => l.CloseId == closeId))
            throw new InvalidOperationException("Closed-cycle test rows remained after rollback.");
        report.Add("Oracle closed-cycle test rollback verified; no test records retained. Concurrent sessions not tested.");
    }
}
