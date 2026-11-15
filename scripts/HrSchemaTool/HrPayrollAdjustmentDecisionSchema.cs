using Oracle.ManagedDataAccess.Client;

internal static class HrPayrollAdjustmentDecisionSchema
{
    private static readonly HrSchemaColumnExpectation[] Expectations =
    [
        new("HR_PAYROLL_ADJUSTMENT", "DECISION_REASON", "NVARCHAR2", "Y", CharacterLength: 1000, Additive: true),
        new("HR_PAYROLL_ADJUSTMENT", "SOURCE_TYPE", "NVARCHAR2", "Y", CharacterLength: 40, Additive: true),
        new("HR_PAYROLL_ADJUSTMENT", "SOURCE_ID", "NVARCHAR2", "Y", CharacterLength: 100, Additive: true),
        new("HR_PAYROLL_ADJUSTMENT", "SOURCE_BRANCH_ID", "NUMBER", "Y", Precision: 19, Scale: 0, Additive: true),
        new("HR_PAYROLL_ADJUSTMENT", "SOURCE_ATTENDANCE_DATE", "DATE", "Y", Additive: true),
        new("HR_PAYROLL_ADJUSTMENT", "SOURCE_OVERTIME_HOURS", "NUMBER", "Y", Precision: 12, Scale: 4, Additive: true),
        new("HR_PAYROLL_ADJUSTMENT", "SOURCE_BASE_SALARY", "NUMBER", "Y", Precision: 18, Scale: 4, Additive: true),
        new("HR_PAYROLL_ADJUSTMENT", "SOURCE_DAY_TYPE", "NVARCHAR2", "Y", CharacterLength: 50, Additive: true),
        new("HR_PAYROLL_ADJUSTMENT", "SOURCE_CURRENCY_CODE", "NVARCHAR2", "Y", CharacterLength: 20, Additive: true)
    ];

    public static async Task Install(OracleConnection connection, List<string> report)
    {
        await HrSchemaColumnGuard.ValidateAsync(connection, Expectations, allowMissingColumns: true);
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT COUNT(*) FROM ALL_TABLES WHERE OWNER='DEV_TEMPLATE' AND TABLE_NAME='HR_PAYROLL_ADJUSTMENT'";
        if (Convert.ToInt32(await command.ExecuteScalarAsync()) == 0)
            throw new InvalidOperationException("DEV_TEMPLATE.HR_PAYROLL_ADJUSTMENT is missing; no DDL applied.");

        await HrSchemaColumnGuard.ValidateAsync(connection, Expectations, allowMissingColumns: true);
        await PreflightSourceIndexAndData(connection);

        var columns = new (string Name, string Type, int Length, int Precision, int Scale)[]
        {
            ("DECISION_REASON", "NVARCHAR2", 1000, 0, 0),
            ("SOURCE_TYPE", "NVARCHAR2", 40, 0, 0),
            ("SOURCE_ID", "NVARCHAR2", 100, 0, 0),
            ("SOURCE_BRANCH_ID", "NUMBER", 0, 19, 0),
            ("SOURCE_ATTENDANCE_DATE", "DATE", 0, 0, 0),
            ("SOURCE_OVERTIME_HOURS", "NUMBER", 0, 12, 4),
            ("SOURCE_BASE_SALARY", "NUMBER", 0, 18, 4),
            ("SOURCE_DAY_TYPE", "NVARCHAR2", 50, 0, 0),
            ("SOURCE_CURRENCY_CODE", "NVARCHAR2", 20, 0, 0)
        };
        foreach (var column in columns)
        {
            command.CommandText = "SELECT DATA_TYPE,CHAR_LENGTH,DATA_PRECISION,DATA_SCALE,NULLABLE FROM ALL_TAB_COLUMNS WHERE OWNER='DEV_TEMPLATE' AND TABLE_NAME='HR_PAYROLL_ADJUSTMENT' AND COLUMN_NAME=:columnName";
            command.Parameters.Clear();
            command.Parameters.Add(new OracleParameter("columnName", column.Name));
            await using var reader = await command.ExecuteReaderAsync();
            var exists = await reader.ReadAsync();
            var valid = exists
                && string.Equals(reader.GetString(0), column.Type, StringComparison.OrdinalIgnoreCase)
                && string.Equals(reader.GetString(4), "Y", StringComparison.OrdinalIgnoreCase)
                && (column.Length == 0 || reader.GetInt32(1) >= column.Length)
                && (column.Precision == 0 || reader.GetInt32(2) == column.Precision && reader.GetInt32(3) == column.Scale);
            await reader.DisposeAsync();

            if (!exists)
            {
                command.Parameters.Clear();
                command.CommandText = column.Type == "NVARCHAR2"
                    ? $"ALTER TABLE DEV_TEMPLATE.HR_PAYROLL_ADJUSTMENT ADD ({column.Name} NVARCHAR2({column.Length}) NULL)"
                    : column.Type == "NUMBER"
                        ? $"ALTER TABLE DEV_TEMPLATE.HR_PAYROLL_ADJUSTMENT ADD ({column.Name} NUMBER({column.Precision},{column.Scale}) NULL)"
                        : $"ALTER TABLE DEV_TEMPLATE.HR_PAYROLL_ADJUSTMENT ADD ({column.Name} DATE NULL)";
                await command.ExecuteNonQueryAsync();
                report.Add("Added nullable payroll adjustment column: " + column.Name);
            }
            else if (!valid)
            {
                throw new InvalidOperationException($"Existing DEV_TEMPLATE.HR_PAYROLL_ADJUSTMENT.{column.Name} has an incompatible Oracle definition.");
            }
        }

        await HrSchemaColumnGuard.ValidateAsync(connection, Expectations, allowMissingColumns: false);

        command.CommandText = "SELECT COUNT(*) FROM ALL_INDEXES WHERE OWNER='DEV_TEMPLATE' AND INDEX_NAME='UX_HR_ADJ_SOURCE'";
        if (Convert.ToInt32(await command.ExecuteScalarAsync()) == 0)
        {
            command.CommandText = "CREATE UNIQUE INDEX DEV_TEMPLATE.UX_HR_ADJ_SOURCE ON DEV_TEMPLATE.HR_PAYROLL_ADJUSTMENT (SOURCE_TYPE,SOURCE_ID)";
            await command.ExecuteNonQueryAsync();
            report.Add("Added unique source link for schema-scoped adjustment idempotency.");
        }
        else
        {
            command.CommandText = "SELECT UNIQUENESS FROM ALL_INDEXES WHERE OWNER='DEV_TEMPLATE' AND INDEX_NAME='UX_HR_ADJ_SOURCE' AND TABLE_NAME='HR_PAYROLL_ADJUSTMENT'";
            if (!string.Equals(Convert.ToString(await command.ExecuteScalarAsync()), "UNIQUE", StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("Existing DEV_TEMPLATE.UX_HR_ADJ_SOURCE is not a unique index on HR_PAYROLL_ADJUSTMENT.");
            command.CommandText = "SELECT LISTAGG(COLUMN_NAME,',') WITHIN GROUP (ORDER BY COLUMN_POSITION) FROM ALL_IND_COLUMNS WHERE INDEX_OWNER='DEV_TEMPLATE' AND INDEX_NAME='UX_HR_ADJ_SOURCE'";
            if (!string.Equals(Convert.ToString(await command.ExecuteScalarAsync()), "SOURCE_TYPE,SOURCE_ID", StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("Existing DEV_TEMPLATE.UX_HR_ADJ_SOURCE has an incompatible column definition.");
        }
        await VerifySourceIndex(connection);
        report.Add("Payroll adjustment decision and source columns/index verified.");
    }

    private static async Task PreflightSourceIndexAndData(OracleConnection connection)
    {
        await using (var index = connection.CreateCommand())
        {
            index.CommandText = "SELECT TABLE_NAME,UNIQUENESS,STATUS FROM ALL_INDEXES WHERE OWNER='DEV_TEMPLATE' AND INDEX_NAME='UX_HR_ADJ_SOURCE'";
            await using var reader = await index.ExecuteReaderAsync();
            if (await reader.ReadAsync())
            {
                var table = reader.GetString(0);
                var uniqueness = reader.GetString(1);
                var status = reader.GetString(2);
                if (await reader.ReadAsync())
                    throw new InvalidOperationException("UX_HR_ADJ_SOURCE has ambiguous metadata; payroll adjustment repair stopped before DDL.");
                await reader.DisposeAsync();
                if (table != "HR_PAYROLL_ADJUSTMENT" || uniqueness != "UNIQUE" || status != "VALID" ||
                    await ReadSourceIndexColumns(connection) != "SOURCE_TYPE,SOURCE_ID")
                    throw new InvalidOperationException("UX_HR_ADJ_SOURCE is reserved by another object or has an incompatible definition; payroll adjustment repair stopped before DDL.");
            }
        }

        await using var columns = connection.CreateCommand();
        columns.CommandText = "SELECT COUNT(*) FROM ALL_TAB_COLUMNS WHERE OWNER='DEV_TEMPLATE' AND TABLE_NAME='HR_PAYROLL_ADJUSTMENT' AND COLUMN_NAME IN ('SOURCE_TYPE','SOURCE_ID')";
        if (Convert.ToInt32(await columns.ExecuteScalarAsync()) == 2)
        {
            await using var duplicates = connection.CreateCommand();
              duplicates.CommandText = "SELECT COUNT(*) FROM (SELECT SOURCE_TYPE,SOURCE_ID FROM DEV_TEMPLATE.HR_PAYROLL_ADJUSTMENT WHERE SOURCE_TYPE IS NOT NULL OR SOURCE_ID IS NOT NULL GROUP BY SOURCE_TYPE,SOURCE_ID HAVING COUNT(*)>1)";
            if (Convert.ToInt64(await duplicates.ExecuteScalarAsync()) != 0)
                throw new InvalidOperationException("Duplicate payroll adjustment source links exist; review them before DDL.");
        }
    }

    private static async Task VerifySourceIndex(OracleConnection connection)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT COUNT(*) FROM ALL_INDEXES WHERE OWNER='DEV_TEMPLATE' AND INDEX_NAME='UX_HR_ADJ_SOURCE' AND TABLE_NAME='HR_PAYROLL_ADJUSTMENT' AND UNIQUENESS='UNIQUE' AND STATUS='VALID'";
        if (Convert.ToInt64(await command.ExecuteScalarAsync()) != 1 ||
            await ReadSourceIndexColumns(connection) != "SOURCE_TYPE,SOURCE_ID")
            throw new InvalidOperationException("UX_HR_ADJ_SOURCE is missing or does not match its expected valid unique definition.");
    }

    private static async Task<string> ReadSourceIndexColumns(OracleConnection connection)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT LISTAGG(COLUMN_NAME,',') WITHIN GROUP (ORDER BY COLUMN_POSITION) FROM ALL_IND_COLUMNS WHERE INDEX_OWNER='DEV_TEMPLATE' AND INDEX_NAME='UX_HR_ADJ_SOURCE'";
        return Convert.ToString(await command.ExecuteScalarAsync()) ?? string.Empty;
    }
}
