using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Oracle.ManagedDataAccess.Client;
using ThinkOnErp.Infrastructure.Data;

internal static class HrStringRepair
{
    private static readonly Regex OracleStringType = new(
        @"^\s*(NVARCHAR2|VARCHAR2|NCHAR|CHAR)\s*\(\s*(\d+)\s*(?:(CHAR|BYTE))?\s*\)\s*$",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private sealed record ColumnPlan(string Table, string Column, string Definition);

    public static async Task PreflightAsync(OracleConnection connection, OracleDbContext db)
    {
        _ = await BuildPlanAsync(connection, db);
    }

    public static async Task RepairAsync(OracleConnection connection, OracleDbContext db, List<string> report)
    {
        var plan = await BuildPlanAsync(connection, db);
        foreach (var item in plan)
        {
            await using var command = connection.CreateCommand();
            command.CommandTimeout = 30;
            command.CommandText = $"ALTER TABLE {Quote(item.Table)} ADD ({Quote(item.Column)} {item.Definition})";
            await command.ExecuteNonQueryAsync();
            report.Add($"Added mapped HR string column {item.Table}.{item.Column}: {item.Definition}");
        }
    }

    private static async Task<List<ColumnPlan>> BuildPlanAsync(OracleConnection connection, OracleDbContext db)
    {
        var plan = new List<ColumnPlan>();
        var keys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var tableMetadata = new Dictionary<string, Dictionary<string, (string Type, int Length, int DataLength, string Nullable, string? CharUsed, string? VirtualColumn)>?>(StringComparer.OrdinalIgnoreCase);
        var rowCounts = new Dictionary<string, long>(StringComparer.OrdinalIgnoreCase);
        var defaults = new Dictionary<Type, object?>();

        foreach (var entity in db.Model.GetEntityTypes())
        {
            var table = entity.GetTableName();
            if (table == null || !table.StartsWith("HR_", StringComparison.OrdinalIgnoreCase)) continue;

            var properties = entity.GetProperties().Where(p => p.ClrType == typeof(string) && p.GetMaxLength() is > 0).ToArray();
            if (properties.Length == 0) continue;

            if (!tableMetadata.TryGetValue(table, out var columns))
            {
                columns = await ReadTableColumnsAsync(connection, table);
                tableMetadata.Add(table, columns);
            }

            // A missing table is created from the current EF model by schema bootstrap.
            if (columns == null) continue;

            var store = StoreObjectIdentifier.Table(table, entity.GetSchema());
            foreach (var property in properties)
            {
                var column = property.GetColumnName(store);
                if (string.IsNullOrWhiteSpace(column)) continue;
                var key = table + "." + column;
                if (!keys.Add(key)) continue;

                var typeText = property.GetColumnType() ?? $"NVARCHAR2({property.GetMaxLength()})";
                var parsed = OracleStringType.Match(typeText);
                if (!parsed.Success)
                    throw new InvalidOperationException($"Cannot safely repair {key}: unsupported mapped Oracle string type '{typeText}'.");

                var typeName = parsed.Groups[1].Value.ToUpperInvariant();
                var length = property.GetMaxLength()!.Value;
                var semantics = parsed.Groups[3].Success
                    ? parsed.Groups[3].Value.ToUpperInvariant()
                    : IsCountryCodeColumn(table, column) ? "CHAR" : null;
                // Oracle's implicit NLS_LENGTH_SEMANTICS can vary by session for
                // VARCHAR2/CHAR. Make repaired DDL deterministic and compare the
                // corresponding data-dictionary semantics instead of ignoring them.
                var expectedSemantics = semantics ?? (typeName is "VARCHAR2" or "CHAR" ? "BYTE" : "CHAR");
                var ddlSemantics = semantics ?? (typeName is "VARCHAR2" or "CHAR" ? "BYTE" : null);
                var sqlType = $"{typeName}({length}{(ddlSemantics == null ? string.Empty : " " + ddlSemantics)})";
                var definition = sqlType;

                if (columns.TryGetValue(column, out var existing))
                {
                    var expectedCharUsed = expectedSemantics == "BYTE" ? "B" : "C";
                    var storageLengthTooShort = expectedSemantics == "BYTE"
                        ? existing.DataLength < length
                        : existing.Length < length;
                    var compatibleStringType = string.Equals(existing.Type, typeName, StringComparison.OrdinalIgnoreCase) ||
                        IsCompatibleCountryCodeStorage(table, column, typeName, existing.Type);
                    if (!compatibleStringType ||
                        storageLengthTooShort ||
                        !string.Equals(existing.Nullable, property.IsNullable ? "Y" : "N", StringComparison.Ordinal) ||
                        existing.VirtualColumn != "NO" ||
                        !string.Equals(existing.CharUsed, expectedCharUsed, StringComparison.Ordinal))
                    {
                        throw new InvalidOperationException($"DEV_TEMPLATE.{key} has incompatible type, length, semantics, nullability, or virtual-column metadata (mapped={typeName}({length} {expectedSemantics})/{(property.IsNullable ? "Y" : "N")}, actual={existing.Type}({existing.Length} {existing.CharUsed})/{existing.Nullable}, virtual={existing.VirtualColumn}); no string repair DDL applied.");
                    }
                    continue;
                }

                if (property.IsNullable)
                {
                    definition += " NULL";
                }
                else
                {
                    var defaultValue = GetDefaultValue(property, entity.ClrType, defaults) as string;
                    if (!string.IsNullOrWhiteSpace(defaultValue))
                    {
                        if (defaultValue.Length > length)
                            throw new InvalidOperationException($"EF default for {key} exceeds its mapped maximum length; no string repair DDL applied.");
                        var prefix = typeName.StartsWith("N", StringComparison.Ordinal) ? "N" : string.Empty;
                        definition += $" DEFAULT {prefix}'{defaultValue.Replace("'", "''")}' NOT NULL";
                    }
                    else
                    {
                        if (!rowCounts.TryGetValue(table, out var rowCount))
                        {
                            rowCount = await CountRowsAsync(connection, table);
                            rowCounts.Add(table, rowCount);
                        }
                        if (rowCount > 0)
                            throw new InvalidOperationException($"DEV_TEMPLATE.{key} is required but missing from a populated table and has no non-empty EF default; review a backfill before DDL.");
                        definition += " NOT NULL";
                    }
                }

                plan.Add(new ColumnPlan(table, column, definition));
            }
        }

        return plan;
    }

    private static async Task<Dictionary<string, (string Type, int Length, int DataLength, string Nullable, string? CharUsed, string? VirtualColumn)>?> ReadTableColumnsAsync(
        OracleConnection connection, string table)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT COLUMN_NAME,DATA_TYPE,CHAR_LENGTH,DATA_LENGTH,NULLABLE,CHAR_USED,VIRTUAL_COLUMN FROM ALL_TAB_COLS WHERE OWNER=SYS_CONTEXT('USERENV','CURRENT_SCHEMA') AND TABLE_NAME=:tableName AND HIDDEN_COLUMN='NO'";
        command.Parameters.Add(new OracleParameter("tableName", table));
        var columns = new Dictionary<string, (string Type, int Length, int DataLength, string Nullable, string? CharUsed, string? VirtualColumn)>(StringComparer.OrdinalIgnoreCase);
        OracleDataReader reader;
        var hasVirtualColumnMetadata = true;
        try
        {
            reader = await command.ExecuteReaderAsync();
        }
        catch (OracleException ex) when (ex.Number == 904)
        {
            // Oracle versions without virtual columns also omit VIRTUAL_COLUMN from the dictionary view.
            hasVirtualColumnMetadata = false;
            command.CommandText = "SELECT COLUMN_NAME,DATA_TYPE,CHAR_LENGTH,DATA_LENGTH,NULLABLE,CHAR_USED FROM ALL_TAB_COLUMNS WHERE OWNER=SYS_CONTEXT('USERENV','CURRENT_SCHEMA') AND TABLE_NAME=:tableName";
            reader = await command.ExecuteReaderAsync();
        }
        await using (reader)
        {
            while (await reader.ReadAsync())
            {
                var name = reader.GetString(0);
                columns[name] = (
                    reader.GetString(1),
                    reader.IsDBNull(2) ? 0 : Convert.ToInt32(reader.GetValue(2)),
                    reader.IsDBNull(3) ? 0 : Convert.ToInt32(reader.GetValue(3)),
                    reader.GetString(4),
                    reader.IsDBNull(5) ? null : reader.GetString(5),
                    hasVirtualColumnMetadata && !reader.IsDBNull(6) ? reader.GetString(6) : "NO");
            }

            if (columns.Count > 0) return columns;
        }

        await using var tableCommand = connection.CreateCommand();
        tableCommand.CommandText = "SELECT COUNT(*) FROM ALL_TABLES WHERE OWNER=SYS_CONTEXT('USERENV','CURRENT_SCHEMA') AND TABLE_NAME=:tableName";
        tableCommand.Parameters.Add(new OracleParameter("tableName", table));
        return Convert.ToInt32(await tableCommand.ExecuteScalarAsync()) == 0 ? null : columns;
    }

    private static bool IsCompatibleCountryCodeStorage(string table, string column, string expected, string actual)
    {
        var isCharacterType = expected is "VARCHAR2" or "NVARCHAR2" && actual is "VARCHAR2" or "NVARCHAR2";
        return IsCountryCodeColumn(table, column) && isCharacterType;
    }

    private static bool IsCountryCodeColumn(string table, string column)
        => table == "HR_EMPLOYEE" && column == "CITIZENSHIP_COUNTRY_CODE" ||
           table == "HR_LEAVE_SETTINGS" && column == "ELIGIBLE_CITIZENSHIP_COUNTRY_CODE";

    private static async Task<long> CountRowsAsync(OracleConnection connection, string table)
    {
        await using var command = connection.CreateCommand();
        command.CommandTimeout = 30;
        command.CommandText = $"SELECT COUNT(*) FROM {Quote(table)}";
        return Convert.ToInt64(await command.ExecuteScalarAsync());
    }

    private static object? GetDefaultValue(IProperty property, Type entityType, Dictionary<Type, object?> defaults)
    {
        if (!defaults.TryGetValue(entityType, out var instance))
        {
            try { instance = Activator.CreateInstance(entityType); }
            catch { instance = null; }
            defaults.Add(entityType, instance);
        }
        return instance == null ? null : property.PropertyInfo?.GetValue(instance);
    }

    private static string Quote(string identifier) => "\"" + identifier.Replace("\"", "\"\"") + "\"";
}
