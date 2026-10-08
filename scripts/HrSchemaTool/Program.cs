using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Oracle.ManagedDataAccess.Client;
using ThinkOnErp.Infrastructure.Data;

// This utility never accepts a schema argument: it is deliberately limited to DEV_TEMPLATE.
var root = Directory.GetCurrentDirectory();
var configurationPath = Path.Combine(root, "src", "ThinkOnErp.API", "appsettings.json");
using var configuration = JsonDocument.Parse(File.ReadAllText(configurationPath));
var configured = Environment.GetEnvironmentVariable("ConnectionStrings__OracleDb")
    ?? configuration.RootElement.GetProperty("ConnectionStrings").GetProperty("OracleDb").GetString();
var builder = new OracleConnectionStringBuilder(configured) { ConnectionTimeout = 10, Pooling = false };
var report = new List<string>();
try
{
    await using var connection = new OracleConnection(builder.ConnectionString);
    await connection.OpenAsync();
    async Task<List<string[]>> Query(string sql)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        command.CommandTimeout = 15;
        await using var reader = await command.ExecuteReaderAsync();
        var rows = new List<string[]>();
        while (await reader.ReadAsync())
            rows.Add(Enumerable.Range(0, reader.FieldCount).Select(i => reader.IsDBNull(i) ? "NULL" : reader.GetValue(i).ToString()!).ToArray());
        return rows;
    }
    async Task Execute(string sql)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        command.CommandTimeout = 15;
        await command.ExecuteNonQueryAsync();
    }
    var columns = await Query("SELECT TABLE_NAME,COLUMN_NAME,DATA_TYPE,NULLABLE FROM ALL_TAB_COLUMNS WHERE OWNER='DEV_TEMPLATE' AND (TABLE_NAME LIKE 'HR_%' OR TABLE_NAME='SYS_USERS') ORDER BY TABLE_NAME,COLUMN_ID");
    if (!columns.Any(c => c[0] == "HR_EMPLOYEE") || !columns.Any(c => c[0] == "SYS_USERS" && c[1] == "Id"))
        throw new InvalidOperationException("Required DEV_TEMPLATE employee table or SYS_USERS Id column is missing; no DDL applied.");
    report.Add("Connected; target schema DEV_TEMPLATE verified.");
    if (args.Contains("--repair"))
    {
        await HrRepair.Apply(connection, report);
        columns = await Query("SELECT TABLE_NAME,COLUMN_NAME,DATA_TYPE,NULLABLE FROM ALL_TAB_COLUMNS WHERE OWNER='DEV_TEMPLATE' AND (TABLE_NAME LIKE 'HR_%' OR TABLE_NAME='SYS_USERS') ORDER BY TABLE_NAME,COLUMN_ID");
        File.WriteAllLines(Path.Combine(root, "artifacts", "hr-audit", "hr-repair-applied.txt"), report);
    }
    if (args.Contains("--verify-scopes"))
    {
        await Execute("ALTER SESSION SET CURRENT_SCHEMA=DEV_TEMPLATE");
        await HrReadVerification.Verify(connection, report);
    }
    if (args.Contains("--inventory"))
    {
        foreach (var row in await Query("SELECT TABLE_NAME,COLUMN_NAME,DATA_TYPE,DATA_DEFAULT FROM ALL_TAB_COLUMNS WHERE OWNER='DEV_TEMPLATE' AND (TABLE_NAME IN ('HR_EMP_SALARY_STRUCTURE','HR_EMP_SALARY_STRUCT_LINE','HR_EMP_SHIFT_ASSIGNMENT','HR_SALARY_STRUCTURE','HR_SALARY_STRUCTURE_LINE','HR_EMPLOYEE_SHIFT_ASSIGNMENT','SYS_SCREEN','SYS_FEATURE','SYS_SYSTEM','SYS_SCREEN_FEATURE','SYS_FISCAL_YEAR','SYS_SETTING')) ORDER BY TABLE_NAME,COLUMN_ID"))
            report.Add("Inventory: " + string.Join(" | ", row));
        foreach (var row in await Query("SELECT SCREEN_CODE,SCREEN_NAME_E FROM DEV_TEMPLATE.SYS_SCREEN WHERE LOWER(SCREEN_CODE) LIKE '%hr%'"))
            report.Add("HR screen: " + string.Join(" | ", row));
        foreach (var row in await Query("SELECT FEATURE_CODE FROM DEV_TEMPLATE.SYS_FEATURE")) report.Add("Feature: " + row[0]);
        File.WriteAllLines(Path.Combine(root, "artifacts", "hr-audit", "hr-repair-inventory.txt"), report);
    }
    if (args.Contains("--apply"))
    {
        if (!columns.Any(c => c[0] == "HR_EMPLOYEE" && c[1] == "USER_ID"))
        {
            await Execute("ALTER TABLE DEV_TEMPLATE.HR_EMPLOYEE ADD (USER_ID NUMBER(19) NULL)");
            report.Add("Added nullable HR_EMPLOYEE.USER_ID.");
        }
        var constraints = await Query("SELECT CONSTRAINT_NAME FROM ALL_CONSTRAINTS WHERE OWNER='DEV_TEMPLATE' AND TABLE_NAME='HR_EMPLOYEE'");
        if (!constraints.Any(c => c[0] == "UX_HR_EMPLOYEE_USER_ID"))
            await Execute("ALTER TABLE DEV_TEMPLATE.HR_EMPLOYEE ADD CONSTRAINT UX_HR_EMPLOYEE_USER_ID UNIQUE (USER_ID)");
        if (!constraints.Any(c => c[0] == "FK_HR_EMPLOYEE_USER"))
            await Execute("ALTER TABLE DEV_TEMPLATE.HR_EMPLOYEE ADD CONSTRAINT FK_HR_EMPLOYEE_USER FOREIGN KEY (USER_ID) REFERENCES DEV_TEMPLATE.SYS_USERS (\"Id\")");
        report.Add("Unique account constraint and non-cascading foreign key ensured.");
        columns = await Query("SELECT TABLE_NAME,COLUMN_NAME,DATA_TYPE,NULLABLE FROM ALL_TAB_COLUMNS WHERE OWNER='DEV_TEMPLATE' AND (TABLE_NAME LIKE 'HR_%' OR TABLE_NAME='SYS_USERS') ORDER BY TABLE_NAME,COLUMN_ID");
    }
    var options = new DbContextOptionsBuilder<OracleDbContext>().UseOracle(builder.ConnectionString,
        o => o.UseOracleSQLCompatibility(OracleSQLCompatibility.DatabaseVersion19)).Options;
    using var context = new OracleDbContext(options);
    var hrEntities = context.Model.GetEntityTypes().Where(e => e.GetTableName()?.StartsWith("HR_") == true).ToList();
    report.Add($"HR entity mappings reviewed: {hrEntities.Count}; actual HR tables: {columns.Select(c => c[0]).Distinct().Count(t => t.StartsWith("HR_"))}.");
    foreach (var entity in hrEntities)
    {
        var table = entity.GetTableName()!;
        var identifier = StoreObjectIdentifier.Table(table, entity.GetSchema());
        var expected = entity.GetProperties().Select(p => p.GetColumnName(identifier)).Where(c => c != null).ToList();
        var actual = columns.Where(c => c[0] == table).Select(c => c[1]).ToHashSet();
        var missing = expected.Where(c => !actual.Contains(c!)).ToArray();
        report.Add($"{table}: " + (actual.Count == 0 ? "MISSING TABLE" : missing.Length == 0 ? "all mapped columns present" : "MISSING COLUMNS: " + string.Join(", ", missing)));
        if (actual.Count > 0 && missing.Length == 0)
        {
            // Validate the actual Oracle SELECT shape without reading personnel data.
            try
            {
                await Query($"SELECT {string.Join(",", expected.Select(c => "\"" + c + "\""))} FROM DEV_TEMPLATE.\"{table}\" WHERE 1=0");
                report.Add($"{table}: mapped SELECT accepted by Oracle.");
            }
            catch (OracleException ex)
            {
                report.Add($"{table}: mapped SELECT failed with ORA-{ex.Number:D5}.");
            }
        }
    }
    foreach (var row in await Query("SELECT CONSTRAINT_NAME,CONSTRAINT_TYPE,STATUS,VALIDATED,DELETE_RULE FROM ALL_CONSTRAINTS WHERE OWNER='DEV_TEMPLATE' AND TABLE_NAME='HR_EMPLOYEE' AND CONSTRAINT_NAME IN ('UX_HR_EMPLOYEE_USER_ID','FK_HR_EMPLOYEE_USER')"))
        report.Add("Link constraint: " + string.Join(" | ", row));
    foreach (var table in columns.Select(c => c[0]).Distinct().Where(t => t.StartsWith("HR_") && System.Text.RegularExpressions.Regex.IsMatch(t, "^[A-Z0-9_]+$")))
    {
        var count = await Query($"SELECT COUNT(*) FROM DEV_TEMPLATE.{table}");
        report.Add($"{table}: rows={count[0][0]}");
    }
    if (columns.Any(c => c[0] == "HR_EMPLOYEE" && c[1] == "USER_ID"))
        report.Add("Linked employee count: " + (await Query("SELECT COUNT(*) FROM DEV_TEMPLATE.HR_EMPLOYEE WHERE USER_ID IS NOT NULL"))[0][0]);
}
catch (OracleException ex)
{
    // Print only the code, never credentials, connection details, or employee data.
    report.Add($"Oracle operation interrupted: ORA-{ex.Number:D5}. Earlier DDL may have committed; review the recorded steps before retrying.");
    Environment.ExitCode = 1;
}
catch (Exception ex)
{
    report.Add("Schema verification failed: " + ex.GetType().Name);
    Environment.ExitCode = 1;
}
Directory.CreateDirectory(Path.Combine(root, "artifacts", "hr-audit"));
File.WriteAllLines(Path.Combine(root, "artifacts", "hr-audit", "dev-template-schema.txt"), report);
foreach (var line in report) Console.WriteLine(line);
