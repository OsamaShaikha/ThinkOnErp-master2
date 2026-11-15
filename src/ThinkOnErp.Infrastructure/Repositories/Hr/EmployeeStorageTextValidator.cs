using System.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Storage;
using ThinkOnErp.Infrastructure.Data;
namespace ThinkOnErp.Infrastructure.Repositories.Hr;

internal sealed class EmployeeStorageTextValidator(OracleDbContext db)
{
    private readonly Dictionary<string, Dictionary<string, int>> limits = new(StringComparer.Ordinal);
    public async Task ValidateAsync(EntityEntry entry, CancellationToken ct)
    {
        var table = entry.Metadata.GetTableName()!;
        if (!limits.TryGetValue(table, out var caps))
        {
            caps = new(StringComparer.Ordinal);
            if (db.Database.IsOracle())
            {
                var opened = db.Database.GetDbConnection().State != ConnectionState.Open;
                if (opened) await db.Database.OpenConnectionAsync(ct);
                try
                {
                    await using var command = db.Database.GetDbConnection().CreateCommand(); command.Transaction = db.Database.CurrentTransaction?.GetDbTransaction();
                    command.CommandText = "SELECT COLUMN_NAME,CHAR_LENGTH FROM ALL_TAB_COLUMNS WHERE OWNER=SYS_CONTEXT('USERENV','CURRENT_SCHEMA') AND TABLE_NAME=:tableName AND DATA_TYPE IN ('NVARCHAR2','VARCHAR2','NCHAR','CHAR')";
                    var parameter = command.CreateParameter(); parameter.ParameterName = "tableName"; parameter.Value = table; command.Parameters.Add(parameter);
                    await using var reader = await command.ExecuteReaderAsync(ct);
                    while (await reader.ReadAsync(ct)) caps.Add(reader.GetString(0), Convert.ToInt32(reader.GetValue(1)));
                    if (caps.Count == 0) throw new InvalidOperationException("Employee storage metadata is unavailable for the current tenant.");
                }
                finally { if (opened) await db.Database.CloseConnectionAsync(); }
            }
            else
            {
                var identity = StoreObjectIdentifier.Table(table, entry.Metadata.GetSchema());
                foreach (var property in entry.Metadata.GetProperties().Where(p => p.ClrType == typeof(string) && p.GetMaxLength().HasValue))
                    caps.Add(property.GetColumnName(identity)!, property.GetMaxLength()!.Value);
            }
            limits.Add(table, caps);
        }
        var store = StoreObjectIdentifier.Table(table, entry.Metadata.GetSchema());
        foreach (var property in entry.Properties.Where(p => p.Metadata.ClrType == typeof(string)))
            if (property.CurrentValue is string value && caps.TryGetValue(property.Metadata.GetColumnName(store)!, out var max) && value.Length > max)
                throw new ArgumentException($"{property.Metadata.Name} exceeds this tenant's storage limit of {max} characters.");
    }
}
