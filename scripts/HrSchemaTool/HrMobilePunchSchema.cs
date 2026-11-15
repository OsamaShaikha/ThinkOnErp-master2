using Oracle.ManagedDataAccess.Client;

internal static class HrMobilePunchSchema
{
    private sealed record IndexColumn(int Position, string ColumnName, string? Expression);

    private static readonly HrSchemaColumnExpectation[] InitialColumns =
    [
        new("HR_RAW_ATTENDANCE", "EMPLOYEE_CODE", "NVARCHAR2", "N", CharacterLength: 50),
        new("HR_RAW_ATTENDANCE", "EVENT_ID", "NVARCHAR2", "Y", Additive: true),
        new("HR_RAW_ATTENDANCE", "EVENT_PAYLOAD_HASH", "NVARCHAR2", "Y", CharacterLength: 64, Additive: true),
        new("HR_RAW_ATTENDANCE", "EVENT_RESPONSE_JSON", "NCLOB", "Y", Additive: true)
    ];

    private static readonly HrSchemaColumnExpectation[] FinalColumns =
    [
        new("HR_RAW_ATTENDANCE", "EMPLOYEE_CODE", "NVARCHAR2", "N", CharacterLength: 50),
        new("HR_RAW_ATTENDANCE", "EVENT_ID", "NVARCHAR2", "Y", CharacterLength: 100),
        new("HR_RAW_ATTENDANCE", "EVENT_PAYLOAD_HASH", "NVARCHAR2", "Y", CharacterLength: 64),
        new("HR_RAW_ATTENDANCE", "EVENT_RESPONSE_JSON", "NCLOB", "Y")
    ];

    public static async Task Install(OracleConnection connection, List<string> report)
    {
        await HrSchemaColumnGuard.ValidateAsync(connection, InitialColumns, allowMissingColumns: true);
        await using var command = connection.CreateCommand();
        await ValidateIndexAsync(connection);

        command.Parameters.Clear();
        command.CommandText = "SELECT COUNT(*) FROM (SELECT EMPLOYEE_CODE,EVENT_ID FROM DEV_TEMPLATE.HR_RAW_ATTENDANCE WHERE EVENT_ID IS NOT NULL GROUP BY EMPLOYEE_CODE,EVENT_ID HAVING COUNT(*)>1)";
        if (Convert.ToInt32(await command.ExecuteScalarAsync()) != 0)
            throw new InvalidOperationException("Cannot install mobile event uniqueness while duplicate indexed employee/event keys exist, including events without an employee code.");

        command.Parameters.Clear();
        command.CommandText = "SELECT CHAR_LENGTH FROM ALL_TAB_COLUMNS WHERE OWNER='DEV_TEMPLATE' AND TABLE_NAME='HR_RAW_ATTENDANCE' AND COLUMN_NAME='EVENT_ID'";
        var existingEventIdLength = Convert.ToInt32(await command.ExecuteScalarAsync() ?? 0);
        if (existingEventIdLength is > 0 and < 100)
        {
            command.CommandText = "SELECT COUNT(*) FROM DEV_TEMPLATE.HR_RAW_ATTENDANCE WHERE EVENT_ID IS NOT NULL";
            if (Convert.ToInt32(await command.ExecuteScalarAsync()) != 0)
                throw new InvalidOperationException("Event namespace expansion requires an isolated migration: accepted event rows already exist; no mobile punch DDL was applied.");
        }

        command.Parameters.Clear();
        foreach (var column in new[] { ("EVENT_ID", "NVARCHAR2(100)"), ("EVENT_PAYLOAD_HASH", "NVARCHAR2(64)"), ("EVENT_RESPONSE_JSON", "NCLOB") })
        {
            command.CommandText = "SELECT COUNT(*) FROM ALL_TAB_COLUMNS WHERE OWNER='DEV_TEMPLATE' AND TABLE_NAME='HR_RAW_ATTENDANCE' AND COLUMN_NAME=:columnName";
            command.Parameters.Clear(); command.Parameters.Add(new OracleParameter("columnName", column.Item1));
            if (Convert.ToInt32(await command.ExecuteScalarAsync()) != 0) continue;
            command.Parameters.Clear(); command.CommandText = $"ALTER TABLE DEV_TEMPLATE.HR_RAW_ATTENDANCE ADD ({column.Item1} {column.Item2} NULL)";
            await command.ExecuteNonQueryAsync(); report.Add("Added nullable mobile punch column: " + column.Item1);
        }
        await HrSchemaColumnGuard.ValidateAsync(connection, InitialColumns, allowMissingColumns: false);
        command.Parameters.Clear(); command.CommandText = "SELECT CHAR_LENGTH FROM ALL_TAB_COLUMNS WHERE OWNER='DEV_TEMPLATE' AND TABLE_NAME='HR_RAW_ATTENDANCE' AND COLUMN_NAME='EVENT_ID'";
        if (Convert.ToInt32(await command.ExecuteScalarAsync()) < 100)
        {
            // Upgrade only the new, unused DEV_TEMPLATE event namespace. Never
            // remove its guard once accepted events exist. Oracle ORA-30556
            // requires recreating a functional index around column expansion.
            command.CommandText = "SELECT COUNT(*) FROM DEV_TEMPLATE.HR_RAW_ATTENDANCE WHERE EVENT_ID IS NOT NULL";
            if (Convert.ToInt32(await command.ExecuteScalarAsync()) != 0) throw new InvalidOperationException("Event namespace expansion requires an isolated migration: accepted event rows already exist.");
            command.CommandText = "SELECT TABLE_NAME FROM ALL_INDEXES WHERE OWNER='DEV_TEMPLATE' AND INDEX_NAME='UX_HR_MOBILE_EVENT'";
            var indexedTable = await command.ExecuteScalarAsync();
            if (indexedTable != null && indexedTable != DBNull.Value)
            {
                if (!string.Equals(Convert.ToString(indexedTable), "HR_RAW_ATTENDANCE", StringComparison.Ordinal)) throw new InvalidOperationException("Unexpected owner table for event index.");
                command.CommandText = "DROP INDEX DEV_TEMPLATE.UX_HR_MOBILE_EVENT"; await command.ExecuteNonQueryAsync();
                report.Add("Verified zero accepted events; rebuilding only the new event index around namespace expansion.");
            }
            command.CommandText = "ALTER TABLE DEV_TEMPLATE.HR_RAW_ATTENDANCE MODIFY (EVENT_ID NVARCHAR2(100))";
            await command.ExecuteNonQueryAsync(); report.Add("Expanded event namespace for stable device keys; existing event IDs retained.");
        }
        await HrSchemaColumnGuard.ValidateAsync(connection, FinalColumns, allowMissingColumns: false);
        if (!await ValidateIndexAsync(connection))
        {
            command.Parameters.Clear();
            command.CommandText = "CREATE UNIQUE INDEX DEV_TEMPLATE.UX_HR_MOBILE_EVENT ON DEV_TEMPLATE.HR_RAW_ATTENDANCE (CASE WHEN EVENT_ID IS NOT NULL THEN EMPLOYEE_CODE END, EVENT_ID)";
            await command.ExecuteNonQueryAsync();
        }
        if (!await ValidateIndexAsync(connection))
            throw new InvalidOperationException("UX_HR_MOBILE_EVENT was not installed in the expected function/column signature.");
        report.Add("Mobile event uniqueness installed; legacy NULL event IDs are excluded without rewriting or deduplicating old raw evidence.");
    }

    private static async Task<bool> ValidateIndexAsync(OracleConnection connection)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT TABLE_NAME,UNIQUENESS,STATUS,INDEX_TYPE FROM ALL_INDEXES WHERE OWNER='DEV_TEMPLATE' AND INDEX_NAME='UX_HR_MOBILE_EVENT'";
        await using (var reader = await command.ExecuteReaderAsync())
        {
            if (!await reader.ReadAsync()) return false;
            if (reader.GetString(0) != "HR_RAW_ATTENDANCE" || reader.GetString(1) != "UNIQUE" || reader.GetString(2) != "VALID" || reader.GetString(3) != "FUNCTION-BASED NORMAL" || await reader.ReadAsync())
                throw new InvalidOperationException("UX_HR_MOBILE_EVENT exists with an unexpected owner table, uniqueness, status, or index type.");
        }

        command.CommandText = "SELECT ic.COLUMN_POSITION,ic.COLUMN_NAME,e.COLUMN_EXPRESSION FROM ALL_IND_COLUMNS ic LEFT JOIN ALL_IND_EXPRESSIONS e ON e.INDEX_OWNER=ic.INDEX_OWNER AND e.INDEX_NAME=ic.INDEX_NAME AND e.COLUMN_POSITION=ic.COLUMN_POSITION WHERE ic.INDEX_OWNER='DEV_TEMPLATE' AND ic.INDEX_NAME='UX_HR_MOBILE_EVENT' ORDER BY ic.COLUMN_POSITION";
        await using var columnReader = await command.ExecuteReaderAsync();
        var columns = new List<IndexColumn>();
        while (await columnReader.ReadAsync())
            columns.Add(new IndexColumn(Convert.ToInt32(columnReader.GetValue(0)), columnReader.GetString(1), columnReader.IsDBNull(2) ? null : columnReader.GetString(2)));
        static string Normalize(string value) => new(value.Where(c => !char.IsWhiteSpace(c) && c != '"').Select(char.ToUpperInvariant).ToArray());
        var expectedShape = columns.Count == 2
            && columns[0].Position == 1 && columns[0].Expression is { } firstExpression
            && Normalize(firstExpression) == Normalize("CASE WHEN EVENT_ID IS NOT NULL THEN EMPLOYEE_CODE END")
            && columns[1].Position == 2 && columns[1].ColumnName == "EVENT_ID" && columns[1].Expression is null;
        if (!expectedShape)
            throw new InvalidOperationException("UX_HR_MOBILE_EVENT exists with an unexpected expression/column order; no mobile punch DDL should proceed.");
        return true;
    }
}
