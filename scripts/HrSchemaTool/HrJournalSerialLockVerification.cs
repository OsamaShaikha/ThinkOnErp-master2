using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Oracle.ManagedDataAccess.Client;
using ThinkOnErp.Domain.Entities.Accounting;
using ThinkOnErp.Infrastructure.Data;
using ThinkOnErp.Infrastructure.Repositories.Accounting;

internal static class HrJournalSerialLockVerification
{
    public static async Task Verify(OracleConnection connection, string connectionString, List<string> report)
    {
        static OracleDbContext Context(OracleConnection c) => new(new DbContextOptionsBuilder<OracleDbContext>().UseOracle(c,
            o => o.UseOracleSQLCompatibility(OracleSQLCompatibility.DatabaseVersion19)).Options);
        using var firstDb = Context(connection);
        var branch = await firstDb.SysBranches.AsNoTracking().Select(b => b.Id).FirstAsync();
        var type = await firstDb.GlVoucherTypes.AsNoTracking().Where(t => t.SerialResetPolicy == "MONTHLY" || t.SerialResetPolicy == "YEARLY" || t.SerialResetPolicy == "CONTINUOUS")
            .OrderBy(t => t.Id).FirstAsync();
        var original = await firstDb.GlVoucherSerials.AsNoTracking().Where(s => s.BranchId == branch && s.VoucherType == type.TypeCode)
            .Select(s => new { s.SerialYear, s.SerialMonth, s.LastSerialNo }).ToListAsync();
        var originalCount = await firstDb.GlVoucherHeaders.CountAsync(v => v.BranchId == branch && v.VoucherType == type.TypeCode);
        await using var secondConnection = new OracleConnection(connectionString);
        await secondConnection.OpenAsync();
        await using (var schema = secondConnection.CreateCommand())
        { schema.CommandText = "ALTER SESSION SET CURRENT_SCHEMA=DEV_TEMPLATE"; await schema.ExecuteNonQueryAsync(); }
        using var secondDb = Context(secondConnection);
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        var repository = new GlVoucherRepository(firstDb);
        await using var first = await firstDb.Database.BeginTransactionAsync(System.Data.IsolationLevel.ReadCommitted, deadline.Token);
        var released = false;
        try
        {
            firstDb.Add(new GlVoucherHeader { BranchId = branch, VoucherType = type.TypeCode, VoucherYear = 2026, VoucherMonth = 11 });
            foreach (var policy in new[] { "MONTHLY", "YEARLY", "CONTINUOUS" })
            {
                // Fixture input only; all policy/counter writes are rolled back.
                await firstDb.Database.ExecuteSqlInterpolatedAsync($"UPDATE GL_VOUCHER_TYPE SET SERIAL_RESET_POLICY={policy} WHERE TYPE_CODE={type.TypeCode}", deadline.Token);
                var one = await repository.GenerateNextSerialNoAsync(branch, 2026, 11, type.TypeCode, policy, deadline.Token);
                await firstDb.GlVoucherSerials.Where(s => s.BranchId == branch && s.VoucherType == type.TypeCode).LoadAsync(deadline.Token);
                var two = await repository.GenerateNextSerialNoAsync(branch, policy == "CONTINUOUS" ? 2027 : 2026,
                    policy == "YEARLY" ? 12 : policy == "CONTINUOUS" ? 1 : 11, type.TypeCode, policy, deadline.Token);
                if (two != one + 1) throw new InvalidOperationException("Oracle serial policy lost its high-water mark.");
                if (firstDb.ChangeTracker.Entries<GlVoucherSerial>().Any(e => e.Entity.BranchId == branch && e.Entity.VoucherType == type.TypeCode) ||
                    originalCount != await firstDb.GlVoucherHeaders.CountAsync(v => v.BranchId == branch && v.VoucherType == type.TypeCode, deadline.Token))
                    throw new InvalidOperationException("Serial allocation retained a stale counter or flushed an unrelated pending voucher.");
            }
            // Restore the requested policy while retaining the first transaction's lock.
            await firstDb.Database.ExecuteSqlInterpolatedAsync($"UPDATE GL_VOUCHER_TYPE SET SERIAL_RESET_POLICY={type.SerialResetPolicy} WHERE TYPE_CODE={type.TypeCode}", deadline.Token);
            await using (var probe = await secondDb.Database.BeginTransactionAsync(System.Data.IsolationLevel.ReadCommitted, deadline.Token))
            {
                await using var command = secondConnection.CreateCommand();
                command.Transaction = (OracleTransaction)probe.GetDbTransaction(); command.BindByName = true;
                command.CommandText = "SELECT TYPE_CODE FROM GL_VOUCHER_TYPE WHERE TYPE_CODE=:typeCode FOR UPDATE NOWAIT";
                command.Parameters.Add(new OracleParameter("typeCode", type.TypeCode));
                var locked = false;
                try { await command.ExecuteScalarAsync(deadline.Token); }
                catch (OracleException e) when (e.Number == 54) { locked = true; }
                await probe.RollbackAsync(CancellationToken.None);
                if (!locked) throw new InvalidOperationException("The serial allocation anchor was not locked.");
            }
            await using var second = await secondDb.Database.BeginTransactionAsync(System.Data.IsolationLevel.ReadCommitted, deadline.Token);
            Task<long>? waiter = null;
            try
            {
                var other = new GlVoucherRepository(secondDb);
                waiter = other.GenerateNextSerialNoAsync(branch, 2026, 11, type.TypeCode, type.SerialResetPolicy, deadline.Token);
                await Task.WhenAny(waiter, Task.Delay(300, deadline.Token));
                if (waiter.IsCompleted) throw new InvalidOperationException("A competing serial allocation passed the held type lock.");
                await first.RollbackAsync(CancellationToken.None); released = true;
                var one = await waiter.WaitAsync(deadline.Token);
                var two = await other.GenerateNextSerialNoAsync(branch, 2026, 11, type.TypeCode, type.SerialResetPolicy, deadline.Token);
                if (two != one + 1) throw new InvalidOperationException("A resumed allocator repeated a reserved number.");
            }
            finally
            {
                if (!released) { await first.RollbackAsync(CancellationToken.None); released = true; }
                if (waiter != null) { try { await waiter.WaitAsync(TimeSpan.FromSeconds(5)); } catch { } }
                await second.RollbackAsync(CancellationToken.None);
            }
        }
        finally
        {
            if (!released) await first.RollbackAsync(CancellationToken.None);
            firstDb.ChangeTracker.Clear(); secondDb.ChangeTracker.Clear();
        }
        var after = await firstDb.GlVoucherSerials.AsNoTracking().Where(s => s.BranchId == branch && s.VoucherType == type.TypeCode)
            .Select(s => new { s.SerialYear, s.SerialMonth, s.LastSerialNo }).ToListAsync();
        if (!original.OrderBy(s => s.SerialYear).ThenBy(s => s.SerialMonth).SequenceEqual(after.OrderBy(s => s.SerialYear).ThenBy(s => s.SerialMonth)) ||
            originalCount != await firstDb.GlVoucherHeaders.CountAsync(v => v.BranchId == branch && v.VoucherType == type.TypeCode) ||
            (await firstDb.GlVoucherTypes.AsNoTracking().SingleAsync(t => t.Id == type.Id)).SerialResetPolicy != type.SerialResetPolicy)
            throw new InvalidOperationException("Journal serial fixture rollback did not preserve the original counters/type/voucher count.");
        report.Add("Oracle journal serial allocation: MONTHLY/YEARLY/CONTINUOUS reserved increasing numbers inside rollback; cached counters detached and unrelated pending voucher not flushed. NOWAIT rejected the held type anchor; a second allocator waited and resumed after rollback, then reserved distinct numbers. Original counters/policy/voucher count restored; no GL voucher or committed reservation created. This is allocator locking/rollback proof, not owned-transaction committed reservation, positive payroll posting/reversal, ERP period-close coordination or all external writers.");
    }
}
