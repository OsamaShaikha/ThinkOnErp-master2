using Oracle.ManagedDataAccess.Client;

internal static class HrLeaveClosePolicySnapshotSchema
{
    private static readonly (string Name, string Type, int? Precision, int? Scale)[] Columns =
    [
        ("TRACKS_BALANCE_SNAPSHOT", "NUMBER", 1, 0),
        ("CARRY_FORWARD_ALLOWED_SNAPSHOT", "NUMBER", 1, 0),
        ("CARRY_FORWARD_CAP_DAYS_SNAPSHOT", "NUMBER", 6, 2),
        ("YEAR_END_EXCESS_ACTION_SNAPSHOT", "NVARCHAR2", null, null)
    ];

    public static async Task Install(OracleConnection connection, List<string> report)
    {
        var expectations = Columns.Select(column => new HrSchemaColumnExpectation(
            "HR_LEAVE_YEAR_CLOSE_LINE", column.Name, column.Type, "Y",
            CharacterLength: column.Type == "NVARCHAR2" ? 20 : null,
            Precision: column.Precision, Scale: column.Scale, Additive: true)).ToArray();
        await HrSchemaColumnGuard.ValidateAsync(connection, expectations, allowMissingColumns: true);
        if (await CountAsync(connection, "SELECT COUNT(*) FROM ALL_TABLES WHERE OWNER='DEV_TEMPLATE' AND TABLE_NAME='HR_LEAVE_YEAR_CLOSE_LINE'") != 1)
            throw new InvalidOperationException("DEV_TEMPLATE.HR_LEAVE_YEAR_CLOSE_LINE is missing; policy snapshot DDL was not applied.");

        foreach (var column in Columns)
        {
            var count = await CountAsync(connection,
                $"SELECT COUNT(*) FROM ALL_TAB_COLUMNS WHERE OWNER='DEV_TEMPLATE' AND TABLE_NAME='HR_LEAVE_YEAR_CLOSE_LINE' AND COLUMN_NAME='{column.Name}'");
            if (count == 0)
            {
                var definition = column.Name switch
                {
                    "TRACKS_BALANCE_SNAPSHOT" or "CARRY_FORWARD_ALLOWED_SNAPSHOT" => "NUMBER(1)",
                    "CARRY_FORWARD_CAP_DAYS_SNAPSHOT" => "NUMBER(6,2)",
                    "YEAR_END_EXCESS_ACTION_SNAPSHOT" => "NVARCHAR2(20)",
                    _ => throw new InvalidOperationException($"Unsupported leave close policy column '{column.Name}'.")
                };
                await ExecuteAsync(connection,
                    $"ALTER TABLE DEV_TEMPLATE.HR_LEAVE_YEAR_CLOSE_LINE ADD ({column.Name} {definition} NULL)");
                report.Add($"Added nullable {column.Name} to DEV_TEMPLATE.HR_LEAVE_YEAR_CLOSE_LINE.");
            }
        }

        await HrSchemaColumnGuard.ValidateAsync(connection, expectations, allowMissingColumns: false);
        await Verify(connection, report);
    }

    public static async Task Verify(OracleConnection connection, List<string> report)
    {
        foreach (var column in Columns)
        {
            await using var command = connection.CreateCommand();
            command.CommandText = "SELECT DATA_TYPE,DATA_PRECISION,DATA_SCALE,CHAR_LENGTH,NULLABLE FROM ALL_TAB_COLUMNS WHERE OWNER='DEV_TEMPLATE' AND TABLE_NAME='HR_LEAVE_YEAR_CLOSE_LINE' AND COLUMN_NAME=:columnName";
            command.Parameters.Add("columnName", OracleDbType.Varchar2, column.Name, System.Data.ParameterDirection.Input);
            await using var reader = await command.ExecuteReaderAsync();
            if (!await reader.ReadAsync())
                throw new InvalidOperationException($"DEV_TEMPLATE.HR_LEAVE_YEAR_CLOSE_LINE.{column.Name} is missing.");
            var type = reader.GetString(0);
            var precision = reader.IsDBNull(1) ? (int?)null : Convert.ToInt32(reader.GetValue(1));
            var scale = reader.IsDBNull(2) ? (int?)null : Convert.ToInt32(reader.GetValue(2));
            var charLength = reader.IsDBNull(3) ? 0 : Convert.ToInt32(reader.GetValue(3));
            var nullable = reader.GetString(4);
            if (type != column.Type || precision != column.Precision || scale != column.Scale || nullable != "Y" ||
                column.Name == "YEAR_END_EXCESS_ACTION_SNAPSHOT" && charLength < 20)
                throw new InvalidOperationException($"DEV_TEMPLATE.HR_LEAVE_YEAR_CLOSE_LINE.{column.Name} has an unexpected type or nullability.");
        }

        await using (var mappedSelect = connection.CreateCommand())
        {
            mappedSelect.CommandText = "SELECT TRACKS_BALANCE_SNAPSHOT,CARRY_FORWARD_ALLOWED_SNAPSHOT,CARRY_FORWARD_CAP_DAYS_SNAPSHOT,YEAR_END_EXCESS_ACTION_SNAPSHOT FROM DEV_TEMPLATE.HR_LEAVE_YEAR_CLOSE_LINE WHERE 1=0";
            await using var reader = await mappedSelect.ExecuteReaderAsync();
        }
        report.Add("HR_LEAVE_YEAR_CLOSE_LINE: four nullable policy snapshot columns and mapped SELECT verified.");
    }

    private static async Task<long> CountAsync(OracleConnection connection, string sql)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        return Convert.ToInt64(await command.ExecuteScalarAsync());
    }

    private static async Task ExecuteAsync(OracleConnection connection, string sql)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        command.CommandTimeout = 30;
        await command.ExecuteNonQueryAsync();
    }
}
