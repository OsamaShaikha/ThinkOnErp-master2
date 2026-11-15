using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Oracle.ManagedDataAccess.Client;
using ThinkOnErp.Domain.Entities.Hr;
using ThinkOnErp.Infrastructure.Data;

namespace ThinkOnErp.Infrastructure.Repositories.Hr;

internal static class HrLeaveSettingsMutationLock
{
    public static async Task LockAsync(OracleDbContext context, string actor, CancellationToken cancellationToken)
    {
        var transaction = context.Database.CurrentTransaction
            ?? throw new InvalidOperationException("Leave settings must be locked inside an active transaction.");

        foreach (var entry in context.ChangeTracker.Entries<HrLeaveSettings>().Where(e => e.Entity.Id == 1).ToArray())
            entry.State = EntityState.Detached;

        var connection = context.Database.GetDbConnection();
        try
        {
            await using var insert = connection.CreateCommand();
            insert.Transaction = transaction.GetDbTransaction();
            insert.CommandText = "INSERT INTO HR_LEAVE_SETTINGS (ID, LEAVE_YEAR_START_MONTH, LEAVE_YEAR_START_DAY, BALANCE_DISPLAY_FORMAT, ACCRUAL_AUTO_ENABLED, CREATION_USER, CREATION_DATE) VALUES (1, 1, 1, 'DECIMAL', 0, :actor, :createdAt)";
            var actorParameter = insert.CreateParameter(); actorParameter.ParameterName = "actor"; actorParameter.Value = string.IsNullOrWhiteSpace(actor) ? "SYSTEM" : actor;
            var dateParameter = insert.CreateParameter(); dateParameter.ParameterName = "createdAt"; dateParameter.Value = DateTime.UtcNow;
            insert.Parameters.Add(actorParameter); insert.Parameters.Add(dateParameter);
            await insert.ExecuteNonQueryAsync(cancellationToken);
        }
        catch (OracleException ex) when (ex.Number == 1)
        {
            // Another transaction already created the singleton row; lock the row below.
        }

        await using var select = connection.CreateCommand();
        select.Transaction = transaction.GetDbTransaction();
        select.CommandText = "SELECT ID FROM HR_LEAVE_SETTINGS WHERE ID=1 FOR UPDATE";
        if (await select.ExecuteScalarAsync(cancellationToken) == null)
            throw new InvalidOperationException("The HR leave settings singleton could not be initialized or locked.");
    }
}
