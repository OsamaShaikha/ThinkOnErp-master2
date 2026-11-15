using System.Data;
using System.Data.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Oracle.ManagedDataAccess.Client;
using ThinkOnErp.Domain.Entities.Accounting;
using ThinkOnErp.Infrastructure.Data;

namespace ThinkOnErp.Infrastructure.Repositories.Accounting;

internal static class OracleGlVoucherSerialAllocator
{
    public static async Task<long> ReserveAsync(OracleDbContext db, long branchId, int year, int month, int typeCode,
        string policy, CancellationToken ct)
    {
        var cached = db.ChangeTracker.Entries<GlVoucherSerial>()
            .Where(e => e.Entity.BranchId == branchId && e.Entity.VoucherType == typeCode).ToArray();
        if (cached.Any(e => e.State != EntityState.Unchanged))
            throw new InvalidOperationException("Journal serial allocation cannot overwrite pending serial tracker changes.");
        foreach (var entry in cached) entry.State = EntityState.Detached;

        var connection = db.Database.GetDbConnection();
        var opened = connection.State != ConnectionState.Open;
        if (opened) await db.Database.OpenConnectionAsync(ct);
        IDbContextTransaction? owned = null;
        try
        {
            if (db.Database.CurrentTransaction == null)
                owned = await db.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted, ct);
            var transaction = db.Database.CurrentTransaction!.GetDbTransaction();
            DbCommand Command(string sql, params (string Name, object Value)[] values)
            {
                var command = connection.CreateCommand();
                command.Transaction = transaction;
                command.CommandText = sql;
                if (command is OracleCommand oracle) oracle.BindByName = true;
                foreach (var (name, value) in values)
                {
                    var parameter = command.CreateParameter(); parameter.ParameterName = name; parameter.Value = value;
                    command.Parameters.Add(parameter);
                }
                return command;
            }

            // The existing type row also protects first allocation, before any serial
            // tracker exists. It is separate from HR company/employee lock anchors.
            await using (var anchor = Command("SELECT SERIAL_RESET_POLICY FROM GL_VOUCHER_TYPE WHERE TYPE_CODE=:typeCode FOR UPDATE", ("typeCode", typeCode)))
            {
                var configured = (await anchor.ExecuteScalarAsync(ct))?.ToString()?.Trim().ToUpperInvariant();
                if (configured != policy)
                    throw new InvalidOperationException("The journal type is missing or its serial policy changed. Reload the configuration before retrying.");
            }

            var continuous = policy == "CONTINUOUS";
            var yearly = policy == "YEARLY";
            var targetYear = continuous ? 0 : year;
            var targetMonth = continuous || yearly ? 0 : month;
            var scope = "BRANCH_ID=:branchId AND VOUCHER_TYPE=:typeCode";
            var values = new List<(string Name, object Value)> { ("branchId", branchId), ("typeCode", typeCode) };
            if (!continuous) { scope += " AND VOUCHER_YEAR=:year"; values.Add(("year", year)); }
            if (!continuous && !yearly) { scope += " AND VOUCHER_MONTH=:month"; values.Add(("month", month)); }
            long floor;
            await using (var maximum = Command("SELECT NVL(MAX(VOUCHER_NO),0) FROM GL_VOUCHER_HEADER WHERE " + scope, values.ToArray()))
                floor = Convert.ToInt64(await maximum.ExecuteScalarAsync(ct));
            var localMax = db.GlVoucherHeaders.Local.Where(h => h.BranchId == branchId && h.VoucherType == typeCode &&
                (continuous || h.VoucherYear == year) && (continuous || yearly || h.VoucherMonth == month))
                .Select(h => (long?)h.VoucherNo).Max() ?? 0;
            floor = Math.Max(floor, localMax);
            if (continuous)
            {
                await using var maximum = Command("SELECT NVL(MAX(LAST_SERIAL_NO),0) FROM GL_VOUCHER_SERIAL WHERE BRANCH_ID=:branchId AND VOUCHER_TYPE=:typeCode",
                    ("branchId", branchId), ("typeCode", typeCode));
                floor = Math.Max(floor, Convert.ToInt64(await maximum.ExecuteScalarAsync(ct)));
            }
            var key = new (string Name, object Value)[] { ("branchId", branchId), ("typeCode", typeCode), ("year", targetYear), ("month", targetMonth) };
            const string predicate = "BRANCH_ID=:branchId AND VOUCHER_TYPE=:typeCode AND SERIAL_YEAR=:year AND SERIAL_MONTH=:month";
            await using (var maximum = Command("SELECT NVL(MAX(LAST_SERIAL_NO),0) FROM GL_VOUCHER_SERIAL WHERE " + predicate, key))
                floor = Math.Max(floor, Convert.ToInt64(await maximum.ExecuteScalarAsync(ct)));
            var next = checked(Math.Max(floor, 0) + 1);
            var mutationValues = key.Append(("next", (object)next)).ToArray();
            int changed;
            await using (var update = Command("UPDATE GL_VOUCHER_SERIAL SET LAST_SERIAL_NO=:next WHERE " + predicate, mutationValues))
                changed = await update.ExecuteNonQueryAsync(ct);
            if (changed == 0)
            {
                await using var insert = Command("INSERT INTO GL_VOUCHER_SERIAL (BRANCH_ID,SERIAL_YEAR,SERIAL_MONTH,VOUCHER_TYPE,LAST_SERIAL_NO) VALUES (:branchId,:year,:month,:typeCode,:next)", mutationValues);
                await insert.ExecuteNonQueryAsync(ct);
            }
            // SQL writes only the counter. SaveChanges here would accidentally flush
            // unrelated pending journal/party changes in callers without a transaction.
            if (owned != null) await owned.CommitAsync(ct);
            return next;
        }
        catch
        {
            if (owned != null) await owned.RollbackAsync(CancellationToken.None);
            throw;
        }
        finally
        {
            if (owned != null) await owned.DisposeAsync();
            if (opened) await db.Database.CloseConnectionAsync();
        }
    }
}
