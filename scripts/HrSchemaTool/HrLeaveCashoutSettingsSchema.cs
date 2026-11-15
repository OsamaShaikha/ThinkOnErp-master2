using Oracle.ManagedDataAccess.Client;

internal static class HrLeaveCashoutSettingsSchema
{
    private static readonly (string Name, string Sql, string Type, int? Length, int? Precision, int? Scale)[] Columns =
    [
        ("LEAVE_CASHOUT_WAGE_BASIS_CODE", "NVARCHAR2(30) NULL", "NVARCHAR2", 30, null, null),
        ("LEAVE_CASHOUT_BASIS_COMPONENT_CODE", "NVARCHAR2(100) NULL", "NVARCHAR2", 100, null, null),
        ("LEAVE_CASHOUT_DAY_DIVISOR", "NUMBER(10,4) NULL", "NUMBER", null, 10, 4),
        ("LEAVE_CASHOUT_PAY_COMPONENT_CODE", "NVARCHAR2(100) NULL", "NVARCHAR2", 100, null, null)
    ];

    private static readonly HrSchemaColumnExpectation[] Expectations = Columns.Select(column => new HrSchemaColumnExpectation(
        "HR_LEAVE_SETTINGS", column.Name, column.Type, "Y", CharacterLength: column.Length,
        Precision: column.Precision, Scale: column.Scale, Additive: true, ExactCharacterLength: true)).ToArray();

    public static async Task Install(OracleConnection connection, List<string> report)
    {
        await HrSchemaColumnGuard.ValidateAsync(connection, Expectations, allowMissingColumns: true);
        await using (var table = connection.CreateCommand())
        {
            table.CommandText = "SELECT COUNT(*) FROM ALL_TABLES WHERE OWNER='DEV_TEMPLATE' AND TABLE_NAME='HR_LEAVE_SETTINGS'";
            if (Convert.ToInt32(await table.ExecuteScalarAsync()) != 1)
                throw new InvalidOperationException("DEV_TEMPLATE.HR_LEAVE_SETTINGS is missing; no DDL applied.");
        }

        foreach (var (name, sql, _, _, _, _) in Columns)
        {
            await using var exists = connection.CreateCommand();
            exists.CommandText = "SELECT COUNT(*) FROM ALL_TAB_COLUMNS WHERE OWNER='DEV_TEMPLATE' AND TABLE_NAME='HR_LEAVE_SETTINGS' AND COLUMN_NAME=:columnName";
            exists.Parameters.Add(new OracleParameter("columnName", name));
            if (Convert.ToInt32(await exists.ExecuteScalarAsync()) == 0)
            {
                await using var add = connection.CreateCommand();
                add.CommandText = $"ALTER TABLE DEV_TEMPLATE.HR_LEAVE_SETTINGS ADD ({name} {sql})";
                await add.ExecuteNonQueryAsync();
            }
        }
        await HrSchemaColumnGuard.ValidateAsync(connection, Expectations, allowMissingColumns: false);
        report.Add("Added nullable company-configured leave cashout wage-basis, divisor and payroll-component fields; no defaults or employee data were written.");
        await Verify(connection, report);
    }

    public static async Task Verify(OracleConnection connection, List<string> report)
    {
        await HrSchemaColumnGuard.ValidateAsync(connection, Expectations, allowMissingColumns: false);
        foreach (var (name, _, type, length, precision, scale) in Columns)
        {
            await using var metadata = connection.CreateCommand();
            metadata.CommandText = "SELECT DATA_TYPE,CHAR_LENGTH,DATA_PRECISION,DATA_SCALE,NULLABLE FROM ALL_TAB_COLUMNS WHERE OWNER='DEV_TEMPLATE' AND TABLE_NAME='HR_LEAVE_SETTINGS' AND COLUMN_NAME=:columnName";
            metadata.Parameters.Add(new OracleParameter("columnName", name));
            await using var reader = await metadata.ExecuteReaderAsync();
            if (!await reader.ReadAsync() || reader.GetString(0) != type ||
                (length.HasValue && Convert.ToInt32(reader.GetValue(1)) != length.Value) ||
                (precision.HasValue && (reader.IsDBNull(2) || Convert.ToInt32(reader.GetValue(2)) != precision.Value)) ||
                (scale.HasValue && (reader.IsDBNull(3) || Convert.ToInt32(reader.GetValue(3)) != scale.Value)) ||
                reader.GetString(4) != "Y" || await reader.ReadAsync())
                throw new InvalidOperationException($"DEV_TEMPLATE.HR_LEAVE_SETTINGS.{name} is missing or has an unexpected type, length, precision, scale, or nullability.");
        }

        await using var mapped = connection.CreateCommand();
        mapped.CommandText = $"SELECT {string.Join(",", Columns.Select(c => c.Name))} FROM DEV_TEMPLATE.HR_LEAVE_SETTINGS WHERE 1=0";
        await using var mappedReader = await mapped.ExecuteReaderAsync();
        if (mappedReader.FieldCount != Columns.Length)
            throw new InvalidOperationException("DEV_TEMPLATE leave cashout configuration mapping returned an unexpected column count.");
        report.Add("Leave cashout settings columns and mapped Oracle SELECT verified; existing settings remain unconfigured.");
    }
}
