using Oracle.ManagedDataAccess.Client;

internal static class HrLeaveAccrualSettingsSchema
{
    private static readonly HrSchemaColumnExpectation[] Columns =
    [
        new("HR_LEAVE_SETTINGS", "ACCRUAL_AUTO_ENABLED", "NUMBER", "N", Precision: 10, Scale: 0, Additive: true, DefaultExpression: "0"),
        new("HR_LEAVE_SETTINGS", "ACCRUAL_TIME_ZONE_ID", "NVARCHAR2", "Y", CharacterLength: 120, Additive: true)
    ];

    public static async Task Install(OracleConnection connection, List<string> report)
    {
        async Task<long> Count(string sql)
        {
            await using var command = connection.CreateCommand();
            command.CommandText = sql;
            command.CommandTimeout = 30;
            return Convert.ToInt64(await command.ExecuteScalarAsync());
        }

        async Task Run(string sql)
        {
            await using var command = connection.CreateCommand();
            command.CommandText = sql;
            command.CommandTimeout = 30;
            await command.ExecuteNonQueryAsync();
        }

        if (await Count("SELECT COUNT(*) FROM ALL_TABLES WHERE OWNER='DEV_TEMPLATE' AND TABLE_NAME='HR_LEAVE_SETTINGS'") == 0)
            throw new InvalidOperationException("DEV_TEMPLATE.HR_LEAVE_SETTINGS is missing; no DDL applied.");

        string? existingType = null;
        int? existingPrecision = null;
        int? existingScale = null;
        string? existingNullable = null;
        int existingDefaultLength = 0;
        string? existingDefault = null;
        string? existingVirtual = null;
        await using (var inspect = connection.CreateCommand())
        {
            inspect.CommandText = "SELECT DATA_TYPE,DATA_PRECISION,DATA_SCALE,NULLABLE,NVL(DEFAULT_LENGTH,0),DATA_DEFAULT,VIRTUAL_COLUMN FROM ALL_TAB_COLS WHERE OWNER='DEV_TEMPLATE' AND TABLE_NAME='HR_LEAVE_SETTINGS' AND COLUMN_NAME='ACCRUAL_AUTO_ENABLED'";
            inspect.CommandTimeout = 15;
            inspect.InitialLONGFetchSize = -1;
            await using var reader = await inspect.ExecuteReaderAsync();
            if (await reader.ReadAsync())
            {
                existingType = reader.GetString(0).ToUpperInvariant();
                existingPrecision = reader.IsDBNull(1) ? null : Convert.ToInt32(reader.GetValue(1));
                existingScale = reader.IsDBNull(2) ? null : Convert.ToInt32(reader.GetValue(2));
                existingNullable = reader.GetString(3).ToUpperInvariant();
                existingDefaultLength = Convert.ToInt32(reader.GetValue(4));
                existingDefault = reader.IsDBNull(5) ? null : reader.GetString(5);
                existingVirtual = reader.GetString(6).ToUpperInvariant();
            }
        }

        if (existingType is null)
        {
            await Run("ALTER TABLE DEV_TEMPLATE.HR_LEAVE_SETTINGS ADD (ACCRUAL_AUTO_ENABLED NUMBER(10) DEFAULT 0 NOT NULL)");
        }
        else
        {
            if (existingType != "NUMBER" || existingPrecision is not (1 or 10) || existingScale != 0 ||
                existingNullable != "N" || existingVirtual != "NO" ||
                existingDefaultLength > 0 && HrSchemaColumnGuard.NormalizeDefault(existingDefault) != "0")
                throw new InvalidOperationException($"DEV_TEMPLATE.HR_LEAVE_SETTINGS.ACCRUAL_AUTO_ENABLED has an incompatible definition (type={existingType}, precision={existingPrecision}, scale={existingScale}, nullable={existingNullable}, defaultLength={existingDefaultLength}, default={System.Text.Json.JsonSerializer.Serialize(existingDefault)}, virtual={existingVirtual}); no DDL applied.");

            if (await Count("SELECT COUNT(*) FROM DEV_TEMPLATE.HR_LEAVE_SETTINGS WHERE ACCRUAL_AUTO_ENABLED IS NULL OR ACCRUAL_AUTO_ENABLED NOT IN (0,1)") != 0)
                throw new InvalidOperationException("DEV_TEMPLATE.HR_LEAVE_SETTINGS.ACCRUAL_AUTO_ENABLED contains values outside 0/1; no DDL applied.");

            await Run("ALTER TABLE DEV_TEMPLATE.HR_LEAVE_SETTINGS MODIFY (ACCRUAL_AUTO_ENABLED NUMBER(10))");
            await Run("ALTER TABLE DEV_TEMPLATE.HR_LEAVE_SETTINGS MODIFY (ACCRUAL_AUTO_ENABLED DEFAULT 0)");
        }
        if (await Count("SELECT COUNT(*) FROM ALL_TAB_COLUMNS WHERE OWNER='DEV_TEMPLATE' AND TABLE_NAME='HR_LEAVE_SETTINGS' AND COLUMN_NAME='ACCRUAL_TIME_ZONE_ID'") == 0)
            await Run("ALTER TABLE DEV_TEMPLATE.HR_LEAVE_SETTINGS ADD (ACCRUAL_TIME_ZONE_ID NVARCHAR2(120) NULL)");
        await HrSchemaColumnGuard.ValidateAsync(connection, Columns, allowMissingColumns: false);
        report.Add("Added tenant-controlled automatic accrual settings; the feature remains disabled and no schedule runs until explicitly enabled with a time zone.");
    }

    public static async Task Verify(OracleConnection connection, List<string> report)
    {
        await HrSchemaColumnGuard.ValidateAsync(connection, Columns, allowMissingColumns: false);
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT ACCRUAL_AUTO_ENABLED,ACCRUAL_TIME_ZONE_ID FROM DEV_TEMPLATE.HR_LEAVE_SETTINGS WHERE 1=0";
        command.CommandTimeout = 15;
        await using var reader = await command.ExecuteReaderAsync();
        if (reader.FieldCount != 2)
            throw new InvalidOperationException("DEV_TEMPLATE leave accrual settings did not return the expected mapped columns.");
        report.Add("HR_LEAVE_SETTINGS automatic accrual flag/time zone columns and mapped SELECT verified.");
    }
}
