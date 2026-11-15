using Oracle.ManagedDataAccess.Client;

internal static class HrLeaveRequestCancellationSchema
{
    private static readonly HrSchemaColumnExpectation[] Columns =
    [
        new("HR_LEAVE_REQUEST", "CANCELLED_BY", "NVARCHAR2", "Y", CharacterLength: 100, Additive: true),
        new("HR_LEAVE_REQUEST", "CANCELLATION_DATE", "TIMESTAMP(7)", "Y", Additive: true),
        new("HR_LEAVE_REQUEST", "CANCELLATION_REASON", "NVARCHAR2", "Y", CharacterLength: 500, Additive: true)
    ];

    public static async Task Install(OracleConnection connection, List<string> report)
    {
        // Validate every existing definition before the first Oracle DDL, since ALTER TABLE
        // commits implicitly and a partial run must remain safely repeatable.
        await HrSchemaColumnGuard.ValidateAsync(connection, Columns, allowMissingColumns: true);

        foreach (var column in Columns)
        {
            if (!await Exists(connection, column.Column))
            {
                await using var command = connection.CreateCommand();
                command.CommandTimeout = 30;
                command.CommandText = column.Column switch
                {
                    "CANCELLED_BY" => "ALTER TABLE \"HR_LEAVE_REQUEST\" ADD (\"CANCELLED_BY\" NVARCHAR2(100) NULL)",
                    "CANCELLATION_DATE" => "ALTER TABLE \"HR_LEAVE_REQUEST\" ADD (\"CANCELLATION_DATE\" TIMESTAMP(7) NULL)",
                    "CANCELLATION_REASON" => "ALTER TABLE \"HR_LEAVE_REQUEST\" ADD (\"CANCELLATION_REASON\" NVARCHAR2(500) NULL)",
                    _ => throw new InvalidOperationException($"Unexpected leave cancellation audit column {column.Column}.")
                };
                await command.ExecuteNonQueryAsync();
                report.Add($"Added DEV_TEMPLATE.HR_LEAVE_REQUEST.{column.Column} as nullable without default or backfill.");
            }
        }

        await HrSchemaColumnGuard.ValidateAsync(connection, Columns, allowMissingColumns: false);
        report.Add("Leave-request cancellation audit columns match the tenant migration definitions.");
    }

    private static async Task<bool> Exists(OracleConnection connection, string column)
    {
        await using var command = connection.CreateCommand();
        command.CommandTimeout = 15;
        command.CommandText = "SELECT COUNT(*) FROM ALL_TAB_COLUMNS WHERE OWNER='DEV_TEMPLATE' AND TABLE_NAME='HR_LEAVE_REQUEST' AND COLUMN_NAME=:columnName";
        command.Parameters.Add(new OracleParameter("columnName", column));
        return Convert.ToInt32(await command.ExecuteScalarAsync()) == 1;
    }
}
