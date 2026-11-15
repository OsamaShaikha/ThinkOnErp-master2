using Oracle.ManagedDataAccess.Client;

internal static class HrBankAccountSchema
{
    private static readonly HrSchemaColumnExpectation[] Expectations =
    [
        new("BANK_ACCOUNT", "BANK_CODE", "NVARCHAR2", "Y", CharacterLength: 50, ExactCharacterLength: true, Additive: true)
    ];

    public static async Task Install(OracleConnection connection, List<string> report)
    {
        await HrSchemaColumnGuard.ValidateAsync(connection, Expectations, allowMissingColumns: true);
        await RequireBankAccountTable(connection);
        var column = await ReadBankCodeColumn(connection);
        if (column is null)
        {
            await Execute(connection, "ALTER TABLE DEV_TEMPLATE.BANK_ACCOUNT ADD (BANK_CODE NVARCHAR2(50) NULL)");
            report.Add("Added nullable DEV_TEMPLATE.BANK_ACCOUNT.BANK_CODE NVARCHAR2(50); no default or backfill was applied.");
        }
        else if (column.Value.Type != "NVARCHAR2" || column.Value.Length != 50 || column.Value.Nullable != "Y")
        {
            throw new InvalidOperationException("DEV_TEMPLATE.BANK_ACCOUNT.BANK_CODE exists with an incompatible definition; no change was made.");
        }

        await HrSchemaColumnGuard.ValidateAsync(connection, Expectations, allowMissingColumns: false);
        await Verify(connection, report);
    }

    public static async Task Verify(OracleConnection connection, List<string> report)
    {
        await HrSchemaColumnGuard.ValidateAsync(connection, Expectations, allowMissingColumns: false);
        await RequireBankAccountTable(connection);
        var column = await ReadBankCodeColumn(connection);
        if (column is null || column.Value.Type != "NVARCHAR2" || column.Value.Length != 50 || column.Value.Nullable != "Y")
            throw new InvalidOperationException("DEV_TEMPLATE.BANK_ACCOUNT.BANK_CODE must be nullable NVARCHAR2(50).");

        await using var mappedSelect = connection.CreateCommand();
        mappedSelect.CommandText = "SELECT BANK_CODE FROM DEV_TEMPLATE.BANK_ACCOUNT WHERE 1=0";
        mappedSelect.CommandTimeout = 15;
        await using (await mappedSelect.ExecuteReaderAsync()) { }

        report.Add("Verified DEV_TEMPLATE.BANK_ACCOUNT.BANK_CODE as nullable NVARCHAR2(50); mapped SELECT succeeded and no bank values were read or changed.");
    }

    private static async Task RequireBankAccountTable(OracleConnection connection)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT COUNT(*) FROM ALL_TABLES WHERE OWNER='DEV_TEMPLATE' AND TABLE_NAME='BANK_ACCOUNT'";
        command.CommandTimeout = 15;
        if (Convert.ToInt32(await command.ExecuteScalarAsync()) != 1)
            throw new InvalidOperationException("DEV_TEMPLATE.BANK_ACCOUNT is missing; no DDL was applied.");
    }

    private static async Task<(string Type, int Length, string Nullable)?> ReadBankCodeColumn(OracleConnection connection)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT DATA_TYPE,CHAR_LENGTH,NULLABLE FROM ALL_TAB_COLUMNS WHERE OWNER='DEV_TEMPLATE' AND TABLE_NAME='BANK_ACCOUNT' AND COLUMN_NAME='BANK_CODE'";
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
