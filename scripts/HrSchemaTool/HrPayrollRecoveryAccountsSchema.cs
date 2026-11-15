using Oracle.ManagedDataAccess.Client;

internal static class HrPayrollRecoveryAccountsSchema
{
    private static readonly string[] Columns = ["LOAN_RECEIVABLE_ACCOUNT", "ADVANCE_RECEIVABLE_ACCOUNT"];
    private static readonly HrSchemaColumnExpectation[] Expectations = Columns.Select(column => new HrSchemaColumnExpectation(
        "HR_GL_POSTING_CONFIG", column, "NVARCHAR2", "Y", CharacterLength: 100, Additive: true, ExactCharacterLength: true)).ToArray();

    public static async Task Install(OracleConnection connection, List<string> report)
    {
        await HrSchemaColumnGuard.ValidateAsync(connection, Expectations, allowMissingColumns: true);
        await RequireTable(connection);
        var definitions = new Dictionary<string, (string Type, int Length, string Nullable)>(StringComparer.Ordinal);
        foreach (var column in Columns)
        {
            var definition = await ReadColumn(connection, column);
            if (definition is not null && (definition.Value.Type != "NVARCHAR2" || definition.Value.Length != 100 || definition.Value.Nullable != "Y"))
                throw new InvalidOperationException($"DEV_TEMPLATE.HR_GL_POSTING_CONFIG.{column} exists with an incompatible definition; no DDL was applied.");
            if (definition is not null) definitions[column] = definition.Value;
        }

        foreach (var column in Columns.Where(column => !definitions.ContainsKey(column)))
        {
            await Execute(connection, $"ALTER TABLE DEV_TEMPLATE.HR_GL_POSTING_CONFIG ADD ({column} NVARCHAR2(100) NULL)");
            report.Add($"Added nullable DEV_TEMPLATE.HR_GL_POSTING_CONFIG.{column} NVARCHAR2(100); no default or backfill was applied.");
        }
        await HrSchemaColumnGuard.ValidateAsync(connection, Expectations, allowMissingColumns: false);
        await Verify(connection, report);
    }

    public static async Task Verify(OracleConnection connection, List<string> report)
    {
        await HrSchemaColumnGuard.ValidateAsync(connection, Expectations, allowMissingColumns: false);
        await RequireTable(connection);
        foreach (var column in Columns)
        {
            var definition = await ReadColumn(connection, column);
            if (definition is null || definition.Value.Type != "NVARCHAR2" || definition.Value.Length != 100 || definition.Value.Nullable != "Y")
                throw new InvalidOperationException($"DEV_TEMPLATE.HR_GL_POSTING_CONFIG.{column} must be nullable NVARCHAR2(100).");
        }
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT LOAN_RECEIVABLE_ACCOUNT,ADVANCE_RECEIVABLE_ACCOUNT FROM DEV_TEMPLATE.HR_GL_POSTING_CONFIG WHERE 1=0";
        command.CommandTimeout = 15;
        await using (await command.ExecuteReaderAsync()) { }
        report.Add("Verified nullable loan/advance recovery account mappings on DEV_TEMPLATE.HR_GL_POSTING_CONFIG; mapped SELECT succeeded and no configuration values were read or changed.");
    }

    private static async Task RequireTable(OracleConnection connection)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT COUNT(*) FROM ALL_TABLES WHERE OWNER='DEV_TEMPLATE' AND TABLE_NAME='HR_GL_POSTING_CONFIG'";
        command.CommandTimeout = 15;
        if (Convert.ToInt32(await command.ExecuteScalarAsync()) != 1)
            throw new InvalidOperationException("DEV_TEMPLATE.HR_GL_POSTING_CONFIG is missing; no DDL was applied.");
    }

    private static async Task<(string Type, int Length, string Nullable)?> ReadColumn(OracleConnection connection, string column)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT DATA_TYPE,CHAR_LENGTH,NULLABLE FROM ALL_TAB_COLUMNS WHERE OWNER='DEV_TEMPLATE' AND TABLE_NAME='HR_GL_POSTING_CONFIG' AND COLUMN_NAME=:columnName";
        command.Parameters.Add(new OracleParameter("columnName", column));
        command.CommandTimeout = 15;
        await using var reader = await command.ExecuteReaderAsync();
        if (!await reader.ReadAsync()) return null;
        return (reader.GetString(0).ToUpperInvariant(), Convert.ToInt32(reader.GetValue(1)), reader.GetString(2).ToUpperInvariant());
    }

    private static async Task Execute(OracleConnection connection, string sql)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        command.CommandTimeout = 15;
        await command.ExecuteNonQueryAsync();
    }
}
