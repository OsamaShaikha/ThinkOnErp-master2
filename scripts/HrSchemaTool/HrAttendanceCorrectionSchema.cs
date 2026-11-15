using Oracle.ManagedDataAccess.Client;

internal static class HrAttendanceCorrectionSchema
{
    private static readonly HrSchemaColumnExpectation[] Columns =
    [
        new("HR_ATTENDANCE_CORRECTION", "ORIGINAL_DAY_VERSION", "NVARCHAR2", "Y", CharacterLength: 64, Additive: true),
        new("HR_ATTENDANCE_CORRECTION", "APPLIED_CHECK_IN", "TIMESTAMP(7)", "Y", Additive: true),
        new("HR_ATTENDANCE_CORRECTION", "APPLIED_CHECK_OUT", "TIMESTAMP(7)", "Y", Additive: true)
    ];

    public static async Task Install(OracleConnection connection, List<string> report)
    {
        await HrSchemaColumnGuard.ValidateAsync(connection, Columns, allowMissingColumns: true);
        await using var command = connection.CreateCommand();
        foreach (var column in new[] { ("ORIGINAL_DAY_VERSION", "NVARCHAR2(64)"), ("APPLIED_CHECK_IN", "TIMESTAMP(7)"), ("APPLIED_CHECK_OUT", "TIMESTAMP(7)") })
        {
            command.CommandText = "SELECT COUNT(*) FROM ALL_TAB_COLUMNS WHERE OWNER='DEV_TEMPLATE' AND TABLE_NAME='HR_ATTENDANCE_CORRECTION' AND COLUMN_NAME=:columnName";
            command.Parameters.Clear(); command.Parameters.Add(new OracleParameter("columnName", column.Item1));
            if (Convert.ToInt32(await command.ExecuteScalarAsync()) != 0) continue;
            command.Parameters.Clear(); command.CommandText = $"ALTER TABLE DEV_TEMPLATE.HR_ATTENDANCE_CORRECTION ADD ({column.Item1} {column.Item2} NULL)";
            await command.ExecuteNonQueryAsync(); report.Add("Added nullable correction audit column: " + column.Item1);
        }
        await HrSchemaColumnGuard.ValidateAsync(connection, Columns, allowMissingColumns: false);
        report.Add("No legacy request version or applied punches inferred. Legacy pending approvals require reviewed resubmission.");
    }
}
