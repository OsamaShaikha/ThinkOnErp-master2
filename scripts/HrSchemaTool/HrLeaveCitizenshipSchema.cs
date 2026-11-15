using Oracle.ManagedDataAccess.Client;

internal static class HrLeaveCitizenshipSchema
{
    private static readonly (string Table, string Column)[] Columns =
    {
        ("HR_EMPLOYEE", "CITIZENSHIP_COUNTRY_CODE"),
        ("HR_LEAVE_SETTINGS", "ELIGIBLE_CITIZENSHIP_COUNTRY_CODE")
    };

    private static readonly HrSchemaColumnExpectation[] Expectations = Columns.Select(column => new HrSchemaColumnExpectation(
        column.Table, column.Column, "VARCHAR2", "Y", CharacterLength: 2, Additive: true, ExactCharacterLength: true, CharacterSemantics: "C")).ToArray();

    public static async Task Install(OracleConnection connection, List<string> report)
    {
        await HrSchemaColumnGuard.ValidateAsync(connection, Expectations, allowMissingColumns: true);
        foreach (var table in Columns.Select(c => c.Table).Distinct())
        {
            await using var exists = connection.CreateCommand();
            exists.CommandText = "SELECT COUNT(*) FROM ALL_TABLES WHERE OWNER='DEV_TEMPLATE' AND TABLE_NAME=:tableName";
            exists.Parameters.Add(new OracleParameter("tableName", table));
            if (Convert.ToInt32(await exists.ExecuteScalarAsync()) != 1)
                throw new InvalidOperationException($"DEV_TEMPLATE.{table} is missing; no DDL applied.");
        }

        foreach (var (table, column) in Columns)
        {
            await using var columnExists = connection.CreateCommand();
            columnExists.CommandText = "SELECT COUNT(*) FROM ALL_TAB_COLUMNS WHERE OWNER='DEV_TEMPLATE' AND TABLE_NAME=:tableName AND COLUMN_NAME=:columnName";
            columnExists.Parameters.Add(new OracleParameter("tableName", table));
            columnExists.Parameters.Add(new OracleParameter("columnName", column));
            if (Convert.ToInt32(await columnExists.ExecuteScalarAsync()) == 0)
            {
                await using var add = connection.CreateCommand();
                add.CommandText = $"ALTER TABLE DEV_TEMPLATE.{table} ADD ({column} VARCHAR2(2 CHAR) NULL)";
                await add.ExecuteNonQueryAsync();
            }
        }
        await HrSchemaColumnGuard.ValidateAsync(connection, Expectations, allowMissingColumns: false);
        report.Add("Added nullable normalized citizenship country keys to employee and company leave settings; existing free-text nationality was not transformed.");
    }

    public static async Task Verify(OracleConnection connection, List<string> report)
    {
        await HrSchemaColumnGuard.ValidateAsync(connection, Expectations, allowMissingColumns: false);
        foreach (var (table, column) in Columns)
        {
            await using var metadata = connection.CreateCommand();
            metadata.CommandText = "SELECT DATA_TYPE,CHAR_LENGTH,CHAR_USED,NULLABLE FROM ALL_TAB_COLUMNS WHERE OWNER='DEV_TEMPLATE' AND TABLE_NAME=:tableName AND COLUMN_NAME=:columnName";
            metadata.Parameters.Add(new OracleParameter("tableName", table));
            metadata.Parameters.Add(new OracleParameter("columnName", column));
            await using var reader = await metadata.ExecuteReaderAsync();
            if (!await reader.ReadAsync() || reader.GetString(0) != "VARCHAR2" || Convert.ToInt32(reader.GetValue(1)) != 2 || reader.GetString(2) != "C" || reader.GetString(3) != "Y" || await reader.ReadAsync())
                throw new InvalidOperationException($"DEV_TEMPLATE.{table}.{column} is missing or has an unexpected definition.");
            await reader.DisposeAsync();

            await using var mappedSelect = connection.CreateCommand();
            mappedSelect.CommandText = $"SELECT {column} FROM DEV_TEMPLATE.{table} WHERE 1=0";
            await using var mappedReader = await mappedSelect.ExecuteReaderAsync();
            if (mappedReader.FieldCount != 1 || mappedReader.GetName(0) != column)
                throw new InvalidOperationException($"Mapped select for DEV_TEMPLATE.{table}.{column} failed.");
        }
        report.Add("Nullable alpha-2-shaped citizenship columns and mapped SELECT verified on DEV_TEMPLATE; no employee values were read.");
    }
}
