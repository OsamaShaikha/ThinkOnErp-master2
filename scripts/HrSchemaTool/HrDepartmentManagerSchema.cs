using Oracle.ManagedDataAccess.Client;

internal static class HrDepartmentManagerSchema
{
    public static async Task Install(OracleConnection connection, List<string> report)
    {
        await RequireTable(connection, "HR_EMPLOYEE", report);
        await RequireTable(connection, "HR_DEPARTMENT", report);
        var employeeCode = await ReadColumn(connection, "HR_EMPLOYEE", "EMPLOYEE_CODE");
        if (employeeCode == null || !SupportedStringType(employeeCode.Value.Type) || employeeCode.Value.CharLength is <= 0 or > 2000)
            throw new InvalidOperationException("DEV_TEMPLATE.HR_EMPLOYEE.EMPLOYEE_CODE has no supported character definition; no DDL applied.");

        var managerColumn = await ReadColumn(connection, "HR_DEPARTMENT", "MANAGER_EMPLOYEE_CODE");
        if (managerColumn == null)
        {
            var type = employeeCode.Value.Type;
            var lengthAndSemantics = type is "VARCHAR2" or "CHAR"
                ? employeeCode.Value.CharUsed == "B"
                    ? $"{employeeCode.Value.DataLength} BYTE"
                    : $"{employeeCode.Value.CharLength} CHAR"
                : employeeCode.Value.CharLength.ToString(System.Globalization.CultureInfo.InvariantCulture);
            await using var add = connection.CreateCommand();
            add.CommandText = $"ALTER TABLE DEV_TEMPLATE.HR_DEPARTMENT ADD (MANAGER_EMPLOYEE_CODE {type}({lengthAndSemantics}) NULL)";
            await add.ExecuteNonQueryAsync();
            report.Add($"Added nullable HR_DEPARTMENT.MANAGER_EMPLOYEE_CODE as {type}({lengthAndSemantics}); no default or backfill was applied.");
        }

        await Verify(connection, report);
    }

    public static async Task Verify(OracleConnection connection, List<string> report)
    {
        await RequireTable(connection, "HR_EMPLOYEE", report);
        await RequireTable(connection, "HR_DEPARTMENT", report);
        var employeeCode = await ReadColumn(connection, "HR_EMPLOYEE", "EMPLOYEE_CODE");
        var managerCode = await ReadColumn(connection, "HR_DEPARTMENT", "MANAGER_EMPLOYEE_CODE");
        if (employeeCode == null || managerCode == null || !SupportedStringType(employeeCode.Value.Type) ||
            !string.Equals(employeeCode.Value.Type, managerCode.Value.Type, StringComparison.OrdinalIgnoreCase) ||
            managerCode.Value.CharLength < employeeCode.Value.CharLength ||
            !string.Equals(managerCode.Value.CharUsed, employeeCode.Value.CharUsed, StringComparison.OrdinalIgnoreCase) ||
            employeeCode.Value.CharUsed == "B" && managerCode.Value.DataLength < employeeCode.Value.DataLength ||
            managerCode.Value.Nullable != "Y" || managerCode.Value.DefaultLength != 0 || managerCode.Value.VirtualColumn != "NO")
            throw new InvalidOperationException("DEV_TEMPLATE.HR_DEPARTMENT.MANAGER_EMPLOYEE_CODE is missing, not nullable, or incompatible with HR_EMPLOYEE.EMPLOYEE_CODE.");

        report.Add($"Verified DEV_TEMPLATE.HR_DEPARTMENT.MANAGER_EMPLOYEE_CODE as nullable {managerCode.Value.Type}({managerCode.Value.CharLength} {managerCode.Value.CharUsed}); no manager values were backfilled.");
    }

    private static async Task RequireTable(OracleConnection connection, string table, List<string> report)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT COUNT(*) FROM ALL_TABLES WHERE OWNER='DEV_TEMPLATE' AND TABLE_NAME=:tableName";
        command.Parameters.Add(new OracleParameter("tableName", table));
        if (Convert.ToInt32(await command.ExecuteScalarAsync()) == 0)
            throw new InvalidOperationException($"DEV_TEMPLATE.{table} is missing; no DDL applied.");
    }

    private static async Task<(string Type, int CharLength, int DataLength, string CharUsed, string Nullable, int DefaultLength, string VirtualColumn)?> ReadColumn(OracleConnection connection, string table, string column)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT DATA_TYPE,CHAR_LENGTH,DATA_LENGTH,NVL(CHAR_USED,'-'),NULLABLE,NVL(DEFAULT_LENGTH,0),VIRTUAL_COLUMN FROM ALL_TAB_COLS WHERE OWNER='DEV_TEMPLATE' AND TABLE_NAME=:tableName AND COLUMN_NAME=:columnName";
        command.Parameters.Add(new OracleParameter("tableName", table));
        command.Parameters.Add(new OracleParameter("columnName", column));
        await using var reader = await command.ExecuteReaderAsync();
        if (!await reader.ReadAsync()) return null;
        return (reader.GetString(0).ToUpperInvariant(), Convert.ToInt32(reader.GetValue(1)), Convert.ToInt32(reader.GetValue(2)), reader.GetString(3).ToUpperInvariant(), reader.GetString(4).ToUpperInvariant(), Convert.ToInt32(reader.GetValue(5)), reader.GetString(6).ToUpperInvariant());
    }

    private static bool SupportedStringType(string type) => type is "VARCHAR2" or "NVARCHAR2" or "CHAR" or "NCHAR";
}
