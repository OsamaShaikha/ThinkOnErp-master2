using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Oracle.ManagedDataAccess.Client;
using Oracle.ManagedDataAccess.Types;
using ThinkOnErp.Infrastructure.Data;
using ThinkOnErp.Infrastructure.Migrations;

// Write/verification modes remain deliberately limited to DEV_TEMPLATE. The separate
// --schema-details-owner mode is a read-only metadata export for an approved isolated schema.
var root = Directory.GetCurrentDirectory();
if (args.Contains("--write-hr-unicode-string-alignment-plan"))
{
    if (args.Length != 1)
    {
        Console.Error.WriteLine("Unicode alignment SQL-plan generation is offline and must run alone.");
        Environment.ExitCode = 1;
        return;
    }

    var planOperations = AlignHrUnicodeStringColumns.GetSqlOperations();
    var planPath = Path.Combine(root, "artifacts", "hr-audit", "ops16-unicode-string-alignment.sql");
    Directory.CreateDirectory(Path.GetDirectoryName(planPath)!);
    File.WriteAllText(planPath, string.Join(Environment.NewLine + "/" + Environment.NewLine,
        planOperations.Select((sql, index) => $"-- OPS-16 operation {index + 1}/{planOperations.Count}{Environment.NewLine}{sql}")));
    Console.WriteLine($"OPS-16 SQL plan generated offline ({planOperations.Count} operations): {planPath}");
    return;
}
var configurationPath = Path.Combine(root, "src", "ThinkOnErp.API", "appsettings.json");
using var configuration = JsonDocument.Parse(File.ReadAllText(configurationPath));
var configured = Environment.GetEnvironmentVariable("ConnectionStrings__OracleDb")
    ?? configuration.RootElement.GetProperty("ConnectionStrings").GetProperty("OracleDb").GetString();
var builder = new OracleConnectionStringBuilder(configured) { ConnectionTimeout = 10, Pooling = false };
var report = new List<string>();
var migrationBaseline = args.Contains("--migration-baseline");
var migrationVerify = args.Contains("--migration-verify");
var migrationHistory = args.Contains("--migration-history");
var applyHrTenantMigrations = args.Contains("--apply-hr-tenant-migrations");
var backup = args.Contains("--backup-dev-template");
var inspectDatapumpRestore = args.Contains("--inspect-datapump-restore");
var reviewDatapumpRestore = args.Contains("--review-datapump-restore");
var restoreDatapumpIsolated = args.Contains("--restore-dev-template-isolated");
var installLedger = args.Contains("--install-leave-ledger");
var installShifts = args.Contains("--install-shift-resolution");
var installTime = args.Contains("--install-attendance-time");
var installMobile = args.Contains("--install-mobile-punch-events");
var installCorrections = args.Contains("--install-attendance-corrections");
var installProcessing = args.Contains("--install-attendance-processing");
var installBranchLocation = args.Contains("--install-branch-attendance-location");
var installEosShortLeave = args.Contains("--install-eos-short-leave");
var installHandoff = args.Contains("--install-accounting-handoff");
var installPayrollCorrectionIndex = args.Contains("--install-payroll-correction-version-index");
var installPayrollAdjustmentDecision = args.Contains("--install-payroll-adjustment-decision") || args.Contains("--install-payroll-overtime-approval");
var verifyHandoff = args.Contains("--verify-accounting-handoff");
var registerHandoffPermissions = args.Contains("--register-accounting-handoff-permissions");
var installLeavePolicyEffective = args.Contains("--install-leave-policy-effective");
var installLeaveAccrualSettings = args.Contains("--install-leave-accrual-settings");
var installLeaveCitizenship = args.Contains("--install-leave-citizenship");
var installLeaveBalanceMigration = args.Contains("--install-leave-balance-migration");
var installLeaveCashoutSettings = args.Contains("--install-leave-cashout-settings");
var installLeaveCashout = args.Contains("--install-leave-cashout");
var installLeaveClosePolicySnapshots = args.Contains("--install-leave-close-policy-snapshots");
var installCurrencyPrecision = args.Contains("--install-currency-payroll-precision");
var installPayrollRecoveryAccounts = args.Contains("--install-payroll-recovery-accounts");
var verifyPayrollRecoveryAccounts = args.Contains("--verify-payroll-recovery-accounts");
var installDepartmentManager = args.Contains("--install-organization-department-manager");
var verifyDepartmentManager = args.Contains("--verify-organization-department-manager");
var installBankAccountCode = args.Contains("--install-accounting-bank-code");
var installLeaveRequestCancellationAudit = args.Contains("--install-leave-request-cancellation-audit");
var installUnicodeStringAlignment = args.Contains("--install-hr-unicode-string-alignment");
var installSchema = installLedger || installShifts || installTime || installMobile || installCorrections || installProcessing || installBranchLocation ||
    installEosShortLeave || installHandoff || installPayrollCorrectionIndex || installPayrollAdjustmentDecision || installLeavePolicyEffective ||
    installLeaveAccrualSettings || installLeaveCitizenship || installLeaveBalanceMigration || installLeaveCashoutSettings || installLeaveCashout ||
    installLeaveClosePolicySnapshots || installCurrencyPrecision || installPayrollRecoveryAccounts || installDepartmentManager || installBankAccountCode ||
    installLeaveRequestCancellationAudit || installUnicodeStringAlignment;
var verifyBankAccountCode = args.Contains("--verify-accounting-bank-code");
var verifyCurrencyPrecision = args.Contains("--verify-payroll-currency-precision");
var verifyPayrollOvertimeOracle = args.Contains("--verify-payroll-overtime-oracle");
var auditLeaveBalanceMigration = args.Contains("--audit-leave-balance-migration");
var auditLegacyPayroll = args.Contains("--audit-legacy-payroll");
var verifyLeaveBalanceMigrationWorkflow = args.Contains("--verify-leave-balance-migration-workflow");
var verifyLeaveConcurrency = args.Contains("--verify-leave-concurrency");
var verifyLeaveCloseConcurrency = args.Contains("--verify-leave-close-concurrency");
var verifyPayrollInputLock = args.Contains("--verify-payroll-input-lock");
var verifyPayrollJournalOracle = args.Contains("--verify-payroll-journal-oracle");
var verifyPayrollPaymentOracle = args.Contains("--verify-payroll-payment-oracle");
var verifyPayrollCorrectionOracle = args.Contains("--verify-payroll-correction-oracle");
var verifyPayrollPeriodConcurrency = args.Contains("--verify-payroll-period-concurrency");
var verifyAttendanceTimeSettings = args.Contains("--verify-attendance-time-settings");
var verifyAccountingOutboxOracle = args.Contains("--verify-accounting-outbox-oracle");
var verifyLeaveManualAdjustment = args.Contains("--verify-leave-manual-adjustment");
var verifyEmployeeImportRollback = args.Contains("--verify-employee-import-rollback");
var auditStringLengths = args.Contains("--audit-string-lengths");
var widenCurrencyCodeColumns = args.Contains("--widen-currency-code-columns");
var verifyLeaveWorkerDisabled = args.Contains("--verify-leave-worker-disabled");
var schemaDetailsOwnerArguments = args.Where(a => a.StartsWith("--schema-details-owner=", StringComparison.Ordinal)).ToArray();
string? schemaDetailsOwner = schemaDetailsOwnerArguments.Length == 1
    ? schemaDetailsOwnerArguments[0]["--schema-details-owner=".Length..].Trim().ToUpperInvariant()
    : null;
var schemaDetailsOwnerMode = schemaDetailsOwnerArguments.Length > 0;
if (schemaDetailsOwnerMode && (schemaDetailsOwnerArguments.Length != 1 || args.Length != 1 ||
    string.IsNullOrWhiteSpace(schemaDetailsOwner) ||
    !System.Text.RegularExpressions.Regex.IsMatch(schemaDetailsOwner, "^[A-Z][A-Z0-9_$#]{0,127}$", System.Text.RegularExpressions.RegexOptions.CultureInvariant)))
{
    Console.Error.WriteLine("--schema-details-owner=SCHEMA must be the only argument and use a valid Oracle schema identifier; this mode is read-only.");
    Environment.ExitCode = 1;
    return;
}
if ((backup || installLedger || installShifts || installTime || installMobile || installCorrections || installProcessing || installBranchLocation || installEosShortLeave || installHandoff || installPayrollCorrectionIndex || installPayrollAdjustmentDecision || registerHandoffPermissions || installLeavePolicyEffective || installLeaveAccrualSettings || installLeaveCitizenship || installLeaveBalanceMigration || installLeaveCashoutSettings || installLeaveCashout || installLeaveClosePolicySnapshots || installCurrencyPrecision || installPayrollRecoveryAccounts || installDepartmentManager || installBankAccountCode || installLeaveRequestCancellationAudit || installUnicodeStringAlignment || widenCurrencyCodeColumns) && args.Length != 1)
{
    Console.Error.WriteLine("Backup, schema installation, and permission catalog registration must each run alone.");
    Environment.ExitCode = 1;
    return;
}
if (verifyDepartmentManager && args.Length != 1)
{
    Console.Error.WriteLine("Department manager schema verification is read-only and must run alone against DEV_TEMPLATE.");
    Environment.ExitCode = 1;
    return;
}
if (verifyBankAccountCode && args.Length != 1)
{
    Console.Error.WriteLine("Accounting bank-code schema verification is read-only and must run alone against DEV_TEMPLATE.");
    Environment.ExitCode = 1;
    return;
}
if (verifyHandoff && args.Length != 1)
{
    Console.Error.WriteLine("Accounting handoff schema verification is read-only and must run alone against DEV_TEMPLATE.");
    Environment.ExitCode = 1;
    return;
}
if (inspectDatapumpRestore && args.Length != 1)
{
    Console.Error.WriteLine("Data Pump restore readiness inspection is read-only and must run alone against DEV_TEMPLATE.");
    Environment.ExitCode = 1;
    return;
}
if (reviewDatapumpRestore && args.Length != 1)
{
    Console.Error.WriteLine("Data Pump restore SQL review must run alone; it generates DDL text but executes no schema DDL or DML.");
    Environment.ExitCode = 1;
    return;
}
if (restoreDatapumpIsolated && args.Length != 1)
{
    Console.Error.WriteLine("The isolated DEV_TEMPLATE Data Pump restore must run alone; it creates only the fixed, locked HR_RESTORE_20261112 scratch schema.");
    Environment.ExitCode = 1;
    return;
}
if (verifyLeaveConcurrency && args.Length != 1)
{
    Console.Error.WriteLine("Leave concurrency verification must run alone; it opens two DEV_TEMPLATE sessions and takes a short employee row lock without DML.");
    Environment.ExitCode = 1;
    return;
}
if (verifyAttendanceTimeSettings && args.Length != 1)
{
    Console.Error.WriteLine("Attendance time-settings verification must run alone; it uses one rollback-only ReadCommitted Oracle transaction on DEV_TEMPLATE.");
    Environment.ExitCode = 1;
    return;
}
if (verifyLeaveCloseConcurrency && args.Length != 1)
{
    Console.Error.WriteLine("Leave-close concurrency verification must run alone; it uses two rolled-back DEV_TEMPLATE sessions and a reserved close key.");
    Environment.ExitCode = 1;
    return;
}
if (verifyPayrollInputLock && args.Length != 1)
{
    Console.Error.WriteLine("Payroll/input lock verification must run alone; it creates a unique temporary OPEN period on DEV_TEMPLATE, tests two-session row-lock serialization, and removes the fixture.");
    Environment.ExitCode = 1;
    return;
}
if (verifyPayrollJournalOracle && args.Length != 1)
{
    Console.Error.WriteLine("Payroll Journal Voucher lifecycle verification must run alone against DEV_TEMPLATE; it uses only a rollback-only Oracle transaction and refuses to overwrite an existing branch configuration.");
    Environment.ExitCode = 1;
    return;
}
if (verifyPayrollPaymentOracle && args.Length != 1)
{
    Console.Error.WriteLine("Payroll payment Oracle lifecycle verification must run alone; it commits temporary DEV_TEMPLATE fixtures and removes them afterward.");
    Environment.ExitCode = 1;
    return;
}
if (verifyPayrollCorrectionOracle && args.Length != 1)
{
    Console.Error.WriteLine("Payroll correction Oracle verification must run alone; it commits only marked temporary payroll fixtures, tests two-session period-lock serialization, then verifies cleanup.");
    Environment.ExitCode = 1;
    return;
}
if (verifyLeaveManualAdjustment && args.Length != 1)
{
    Console.Error.WriteLine("Manual leave-balance verification must run alone; it posts and reverses synthetic movements inside a rollback-only DEV_TEMPLATE transaction.");
    Environment.ExitCode = 1;
    return;
}
if (verifyEmployeeImportRollback && args.Length != 1)
{
    Console.Error.WriteLine("Employee import rollback verification must run alone; it imports a temporary XLSX with an invalid final row and removes any fixture employees/salaries.");
    Environment.ExitCode = 1;
    return;
}
if (verifyLeaveWorkerDisabled && args.Length != 1)
{
    Console.Error.WriteLine("Disabled worker verification must run alone; it processes DEV_TEMPLATE only and refuses if its accrual opt-in is enabled.");
    Environment.ExitCode = 1;
    return;
}
if (auditLeaveBalanceMigration && args.Length != 1)
{
    Console.Error.WriteLine("Leave-balance migration audit must run alone. It reads aggregate DEV_TEMPLATE data in a read-only transaction.");
    Environment.ExitCode = 1;
    return;
}
if (auditLegacyPayroll && args.Length != 1)
{
    Console.Error.WriteLine("Legacy payroll migration audit must run alone. It reads aggregate DEV_TEMPLATE data in a read-only transaction.");
    Environment.ExitCode = 1;
    return;
}
if (verifyLeaveBalanceMigrationWorkflow && args.Length != 1)
{
    Console.Error.WriteLine("Leave-balance migration workflow verification must run alone. It posts and reverses synthetic allocations in an outer rollback transaction on DEV_TEMPLATE.");
    Environment.ExitCode = 1;
    return;
}
if ((args.Contains("--schema-details") || migrationBaseline || migrationVerify) && args.Any(a => a is "--apply" or "--repair")
    || migrationBaseline && migrationVerify)
{
    Console.Error.WriteLine("Read-only schema/baseline modes cannot be combined with writes; choose a single migration mode.");
    Environment.ExitCode = 1;
    return;
}
if (migrationHistory && args.Length != 1)
{
    Console.Error.WriteLine("Migration history inspection is read-only and must run alone against DEV_TEMPLATE.");
    Environment.ExitCode = 1;
    return;
}
if (applyHrTenantMigrations && args.Length != 1)
{
    Console.Error.WriteLine("HR tenant migration application is a standalone DEV_TEMPLATE operation and requires its verified backup and fixed offline script.");
    Environment.ExitCode = 1;
    return;
}
if (auditStringLengths && (args.Length != 1 || args.Any(a => a is "--apply" or "--repair")))
{
    Console.Error.WriteLine("HR string-length audit is read-only and must run alone against DEV_TEMPLATE.");
    Environment.ExitCode = 1;
    return;
}
if (verifyCurrencyPrecision && args.Length != 1)
{
    Console.Error.WriteLine("Payroll currency precision verification must run alone against DEV_TEMPLATE.");
    Environment.ExitCode = 1;
    return;
}
if (verifyPayrollOvertimeOracle && args.Length != 1)
{
    Console.Error.WriteLine("PAY-17 Oracle overtime verification must run alone against DEV_TEMPLATE; it creates temporary payroll, attendance, rule, and adjustment fixtures then cleans them up.");
    Environment.ExitCode = 1;
    return;
}
try
{
    await using var connection = new OracleConnection(builder.ConnectionString);
        await connection.OpenAsync();

    async Task<List<string[]>> Query(string sql)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        command.CommandTimeout = 15;
        command.InitialLONGFetchSize = -1;
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

    if (migrationHistory)
    {
        await Execute("ALTER SESSION SET CURRENT_SCHEMA=DEV_TEMPLATE");
        await Execute("SET TRANSACTION READ ONLY");
        var session = await Query("SELECT SYS_CONTEXT('USERENV','SESSION_USER'),SYS_CONTEXT('USERENV','CURRENT_SCHEMA') FROM DUAL");
        Console.WriteLine($"Oracle session user={session[0][0]}; current schema={session[0][1]}.");
        var historyTable = await Query("SELECT COUNT(*) FROM ALL_TABLES WHERE OWNER='DEV_TEMPLATE' AND TABLE_NAME='__EFMigrationsHistory'");
        if (historyTable.Count != 1 || historyTable[0][0] != "1")
        {
            var candidates = await Query("SELECT OWNER,TABLE_NAME FROM ALL_TABLES WHERE UPPER(TABLE_NAME) LIKE '%MIGRATION%HISTORY%' ORDER BY OWNER,TABLE_NAME");
            Console.WriteLine("DEV_TEMPLATE.__EFMigrationsHistory is absent from ALL_TABLES. Migration-history candidates visible to this account:");
            foreach (var candidate in candidates)
                Console.WriteLine($"{candidate[0]}.{candidate[1]}");
            if (candidates.Any(candidate => candidate[0] == "THINKON_ERP" && candidate[1] == "__EFMigrationsHistory"))
            {
                var centralHistory = await Query("SELECT \"MigrationId\",\"ProductVersion\" FROM THINKON_ERP.\"__EFMigrationsHistory\" ORDER BY \"MigrationId\"");
                Console.WriteLine($"Central THINKON_ERP history ({centralHistory.Count} rows; not DEV_TEMPLATE tenant state):");
                foreach (var migration in centralHistory)
                    Console.WriteLine($"{migration[0]} | {migration[1]}");
            }
            Console.WriteLine("No migration state was inferred; do not apply migrations until a trusted baseline is established.");
            return;
        }

        var appliedMigrations = await Query("SELECT \"MigrationId\",\"ProductVersion\" FROM DEV_TEMPLATE.\"__EFMigrationsHistory\" ORDER BY \"MigrationId\"");
        Console.WriteLine($"DEV_TEMPLATE EF migration history ({appliedMigrations.Count} rows):");
        foreach (var migration in appliedMigrations)
            Console.WriteLine($"{migration[0]} | {migration[1]}");
        var pendingParentColumns = await Query("SELECT TABLE_NAME,COLUMN_NAME,DATA_TYPE,CHAR_LENGTH,DATA_LENGTH,CHAR_USED,NULLABLE,NVL(DEFAULT_LENGTH,0),VIRTUAL_COLUMN,HIDDEN_COLUMN FROM ALL_TAB_COLS WHERE OWNER='DEV_TEMPLATE' AND ((TABLE_NAME='HR_SALARY_COMPONENT' AND COLUMN_NAME='COMPONENT_CODE') OR (TABLE_NAME='HR_LEAVE_CASHOUT' AND COLUMN_NAME='BASIS_COMPONENT_CODE')) ORDER BY TABLE_NAME,COLUMN_NAME");
        Console.WriteLine("Cashout migration parent/source column metadata:");
        foreach (var column in pendingParentColumns)
            Console.WriteLine(string.Join(" | ", column));
        var leaveMigrationColumns = await Query("SELECT TABLE_NAME,COLUMN_NAME,DATA_TYPE,DATA_PRECISION,DATA_SCALE,CHAR_LENGTH,DATA_LENGTH,CHAR_USED,NULLABLE,NVL(DEFAULT_LENGTH,0),VIRTUAL_COLUMN FROM ALL_TAB_COLS WHERE OWNER='DEV_TEMPLATE' AND TABLE_NAME IN ('HR_LEAVE_BALANCE_MIGRATION','HR_LEAVE_BALANCE_MIGRATION_LINE') ORDER BY TABLE_NAME,COLUMN_ID");
        Console.WriteLine("Leave balance migration table metadata:");
        foreach (var column in leaveMigrationColumns)
            Console.WriteLine(string.Join(" | ", column));
        return;
    }

    if (applyHrTenantMigrations)
    {
        await Execute("ALTER SESSION SET CURRENT_SCHEMA=DEV_TEMPLATE");
        await HrTenantMigrationExecutor.Apply(connection, root, report);
        Directory.CreateDirectory(Path.Combine(root, "artifacts", "hr-audit"));
        File.WriteAllLines(Path.Combine(root, "artifacts", "hr-audit", "dev-template-schema.txt"), report);
        foreach (var line in report) Console.WriteLine(line);
        return;
    }

    if (schemaDetailsOwnerMode)
    {
        var snapshotId = DateTime.UtcNow.ToString("yyyyMMddHHmmssfff", System.Globalization.CultureInfo.InvariantCulture);
        var outputPath = Path.Combine(root, "artifacts", "hr-audit", $"{schemaDetailsOwner}-schema-details-{snapshotId}.txt");
        await HrTenantSchemaSnapshot.WriteAsync(connection, schemaDetailsOwner!, outputPath);
        Console.WriteLine($"Read-only HR schema metadata written to {outputPath} and {Path.ChangeExtension(outputPath, ".json")}. No row data or counts were selected.");
        return;
    }

    if (inspectDatapumpRestore)
    {
        const string dumpName = "dev_template_20261112091921.dmp";
        const string restoreSchema = "HR_RESTORE_20261112";
        var fileExists = await Query($"SELECT DBMS_LOB.FILEEXISTS(BFILENAME('DATA_PUMP_DIR','{dumpName}')) FROM DUAL");
        var directoryExists = await Query("SELECT COUNT(*) FROM ALL_DIRECTORIES WHERE DIRECTORY_NAME='DATA_PUMP_DIR'");
        var targetExists = await Query($"SELECT COUNT(*) FROM ALL_USERS WHERE USERNAME='{restoreSchema}'");
        var privileges = await Query("SELECT PRIVILEGE FROM SESSION_PRIVS WHERE PRIVILEGE IN ('CREATE USER','IMP_FULL_DATABASE','CREATE ANY DIRECTORY') ORDER BY PRIVILEGE");
        var roles = await Query("SELECT ROLE FROM SESSION_ROLES WHERE ROLE IN ('DATAPUMP_IMP_FULL_DATABASE','DBA') ORDER BY ROLE");
        var defaultTablespace = await Query("SELECT DEFAULT_TABLESPACE FROM USER_USERS");
        var sourceTablespaces = await Query("SELECT TABLESPACE_NAME,CEIL(SUM(BYTES)/1024/1024) FROM DBA_SEGMENTS WHERE OWNER='DEV_TEMPLATE' GROUP BY TABLESPACE_NAME ORDER BY TABLESPACE_NAME");
        var targetTablespace = await Query("SELECT COUNT(*) FROM DBA_TABLESPACES WHERE TABLESPACE_NAME='USERS' AND CONTENTS='PERMANENT' AND STATUS='ONLINE'");
        var container = await Query("SELECT SYS_CONTEXT('USERENV','CON_NAME'),SYS_CONTEXT('USERENV','CON_ID') FROM DUAL");
        var sourceUser = await Query("SELECT USERNAME,COMMON,ACCOUNT_STATUS FROM DBA_USERS WHERE USERNAME='DEV_TEMPLATE'");
        var pdbs = await Query("SELECT PDB_NAME,STATUS FROM DBA_PDBS ORDER BY PDB_NAME");
        var sensitivePaths = await Query("SELECT OBJECT_PATH FROM SCHEMA_EXPORT_OBJECTS WHERE OBJECT_PATH LIKE '%GRANT%' OR OBJECT_PATH LIKE '%PROCACT_SCHEMA%' OR OBJECT_PATH LIKE '%TRIGGER%' OR OBJECT_PATH LIKE '%VIEW%' ORDER BY OBJECT_PATH");
        report.Add("DEV_TEMPLATE Data Pump restore readiness: read-only inspection; no DDL/DML executed.");
        report.Add($"DATA_PUMP_DIR visible: {directoryExists.Single()[0]}; backup dump present on server: {fileExists.Single()[0]} (expected 1).");
        report.Add($"Isolated target {restoreSchema} already exists: {targetExists.Single()[0]}.");
        report.Add("Available relevant session privileges: " + (privileges.Count == 0 ? "none reported" : string.Join(", ", privileges.Select(row => row[0]))));
        report.Add("Available relevant session roles: " + (roles.Count == 0 ? "none reported" : string.Join(", ", roles.Select(row => row[0]))));
        report.Add("Connected account default tablespace: " + defaultTablespace.Single()[0] + ".");
        report.Add("DEV_TEMPLATE source segment tablespaces (MB): " + (sourceTablespaces.Count == 0 ? "none" : string.Join(", ", sourceTablespaces.Select(row => $"{row[0]}={row[1]}"))));
        report.Add($"USERS permanent tablespace online: {targetTablespace.Single()[0]} (expected 1).");
        report.Add($"Connected container: {container.Single()[0]} (CON_ID={container.Single()[1]}); DEV_TEMPLATE user metadata: " + (sourceUser.Count == 0 ? "absent" : string.Join(" | ", sourceUser.Select(row => string.Join(",", row)) )));
        report.Add("Available PDBs: " + (pdbs.Count == 0 ? "none" : string.Join(" | ", pdbs.Select(row => string.Join(":", row)))));
        report.Add("Relevant Data Pump metadata paths: " + (sensitivePaths.Count == 0 ? "none" : string.Join(" | ", sensitivePaths.Select(row => row[0]))));
        report.Add("The connected database is CDB$ROOT with no PDB listed; isolated restore requires a dedicated PDB service. No restore DDL was attempted by this readiness check.");
        Directory.CreateDirectory(Path.Combine(root, "artifacts", "hr-audit"));
        File.WriteAllLines(Path.Combine(root, "artifacts", "hr-audit", "dev-template-datapump-restore-readiness.txt"), report);
        foreach (var line in report) Console.WriteLine(line);
        return;
    }

    if (reviewDatapumpRestore)
    {
        const string dumpName = "dev_template_20261112091921.dmp";
        var suffix = DateTime.UtcNow.ToString("yyyyMMddHHmmss");
        var jobName = "HRR" + suffix;
        var sqlName = "hr_restore_review_" + suffix + ".sql";
        var logName = "hr_restore_review_" + suffix + ".log";
        var fileExists = await Query($"SELECT DBMS_LOB.FILEEXISTS(BFILENAME('DATA_PUMP_DIR','{dumpName}')) FROM DUAL");
        if (fileExists.Single()[0] != "1")
            throw new InvalidOperationException("The server-side DEV_TEMPLATE Data Pump dump is not available in DATA_PUMP_DIR.");

        await using (var command = connection.CreateCommand())
        {
            command.BindByName = true;
            command.CommandTimeout = 0;
            command.CommandText = """
                DECLARE
                    h NUMBER;
                    s VARCHAR2(100);
                BEGIN
                    h := DBMS_DATAPUMP.OPEN(operation=>'SQL_FILE', job_mode=>'SCHEMA', job_name=>:job_name);
                    DBMS_DATAPUMP.ADD_FILE(h, :dump_name, 'DATA_PUMP_DIR', filetype=>DBMS_DATAPUMP.KU$_FILE_TYPE_DUMP_FILE);
                    DBMS_DATAPUMP.ADD_FILE(h, :sql_name, 'DATA_PUMP_DIR', filetype=>DBMS_DATAPUMP.KU$_FILE_TYPE_SQL_FILE);
                    DBMS_DATAPUMP.ADD_FILE(h, :log_name, 'DATA_PUMP_DIR', filetype=>DBMS_DATAPUMP.KU$_FILE_TYPE_LOG_FILE);
                    DBMS_DATAPUMP.METADATA_FILTER(h, 'SCHEMA_EXPR', '= ''DEV_TEMPLATE''');
                    DBMS_DATAPUMP.METADATA_REMAP(h, 'REMAP_SCHEMA', 'DEV_TEMPLATE', 'HR_RESTORE_20261112');
                    DBMS_DATAPUMP.METADATA_FILTER(h, 'EXCLUDE_PATH_EXPR', 'LIKE ''%GRANT%''');
                    DBMS_DATAPUMP.METADATA_FILTER(h, 'EXCLUDE_PATH_EXPR', 'LIKE ''%TRIGGER%''');
                    DBMS_DATAPUMP.METADATA_FILTER(h, 'EXCLUDE_PATH_EXPR', 'LIKE ''%VIEW%''');
                    DBMS_DATAPUMP.METADATA_FILTER(h, 'EXCLUDE_PATH_EXPR', 'LIKE ''%PROCACT_SCHEMA%''');
                    DBMS_DATAPUMP.METADATA_FILTER(h, 'EXCLUDE_PATH_EXPR', 'LIKE ''%SYNONYM%''');
                    DBMS_DATAPUMP.METADATA_FILTER(h, 'EXCLUDE_PATH_EXPR', '= ''DEFAULT_ROLE''');
                    DBMS_DATAPUMP.METADATA_FILTER(h, 'EXCLUDE_PATH_EXPR', '= ''TABLESPACE_QUOTA''');
                    DBMS_DATAPUMP.START_JOB(h);
                    DBMS_DATAPUMP.WAIT_FOR_JOB(h, s);
                    :job_state := s;
                END;
                """;
            command.Parameters.Add("job_name", OracleDbType.Varchar2).Value = jobName;
            command.Parameters.Add("dump_name", OracleDbType.Varchar2).Value = dumpName;
            command.Parameters.Add("sql_name", OracleDbType.Varchar2).Value = sqlName;
            command.Parameters.Add("log_name", OracleDbType.Varchar2).Value = logName;
            command.Parameters.Add("job_state", OracleDbType.Varchar2, 100).Direction = System.Data.ParameterDirection.Output;
            await command.ExecuteNonQueryAsync();
            var state = command.Parameters["job_state"].Value.ToString();
            if (state != "COMPLETED")
                throw new InvalidOperationException($"Data Pump SQL_FILE review ended in state {state}; inspect server log {logName}.");
        }

        var reviewFolder = Path.Combine(root, "artifacts", "hr-audit", "restore-review", suffix);
        Directory.CreateDirectory(reviewFolder);
        foreach (var serverFile in new[] { sqlName, logName })
        {
            using var source = new OracleBFile(connection, "DATA_PUMP_DIR", serverFile);
            source.OpenFile();
            await using var destination = new FileStream(Path.Combine(reviewFolder, serverFile), FileMode.CreateNew, FileAccess.Write);
            var buffer = new byte[1024 * 1024];
            int count;
            while ((count = source.Read(buffer, 0, buffer.Length)) > 0)
                await destination.WriteAsync(buffer.AsMemory(0, count));
            await destination.FlushAsync();
            if (destination.Length != source.Length || destination.Length == 0)
                throw new InvalidOperationException("Downloaded Data Pump review artifact length mismatch or empty file.");
            source.CloseFile();
        }

        var generatedSql = await File.ReadAllTextAsync(Path.Combine(reviewFolder, sqlName));
        var generatedLog = await File.ReadAllTextAsync(Path.Combine(reviewFolder, logName));
        var oraErrors = System.Text.RegularExpressions.Regex.Matches(generatedLog, @"ORA-\d{5}").Count;
        var grantStatements = System.Text.RegularExpressions.Regex.Matches(generatedSql, @"(?im)^\s*GRANT\s").Count;
        var sourceSchemaReferences = System.Text.RegularExpressions.Regex.Matches(generatedSql, @"(?i)(?:""DEV_TEMPLATE""|\bDEV_TEMPLATE)\s*\.").Count;
        var createUserStatements = System.Text.RegularExpressions.Regex.Matches(generatedSql, @"(?im)^\s*CREATE\s+USER\b").Count;
        report.Add("DEV_TEMPLATE Data Pump SQL_FILE review completed; no schema DDL/DML executed.");
        report.Add($"Job state=COMPLETED; local artifacts={reviewFolder}; log ORA count={oraErrors}; GRANT statements={grantStatements}; CREATE USER statements={createUserStatements}; remaining DEV_TEMPLATE-qualified references={sourceSchemaReferences}.");
        report.Add("Review the generated SQL before any isolated restore. This mode never imports objects or creates the target schema.");
        File.WriteAllLines(Path.Combine(root, "artifacts", "hr-audit", "dev-template-schema.txt"), report);
        foreach (var line in report) Console.WriteLine(line);
        return;
    }

    if (restoreDatapumpIsolated)
    {
        await HrDataPumpIsolatedRestore.RestoreAsync(connection, root, report);
        Directory.CreateDirectory(Path.Combine(root, "artifacts", "hr-audit"));
        File.WriteAllLines(Path.Combine(root, "artifacts", "hr-audit", "dev-template-schema.txt"), report);
        foreach (var line in report) Console.WriteLine(line);
        return;
    }


    var columns = await Query("SELECT TABLE_NAME,COLUMN_NAME,DATA_TYPE,NULLABLE FROM ALL_TAB_COLUMNS WHERE OWNER='DEV_TEMPLATE' AND (TABLE_NAME LIKE 'HR_%' OR TABLE_NAME='SYS_USERS') ORDER BY TABLE_NAME,COLUMN_ID");
    if (!columns.Any(c => c[0] == "HR_EMPLOYEE") || !columns.Any(c => c[0] == "SYS_USERS" && c[1] == "Id"))
        throw new InvalidOperationException("Required DEV_TEMPLATE employee table or SYS_USERS Id column is missing; no DDL applied.");
    report.Add("Connected; target schema DEV_TEMPLATE verified.");
    if (installSchema)
    {
        await Execute("ALTER SESSION SET CURRENT_SCHEMA=DEV_TEMPLATE");
        if (installLeaveRequestCancellationAudit)
        {
            report.Add("Leave-request audit installer will validate only its three nullable target columns before DDL; unrelated legacy string metadata is not part of this change.");
        }
        else if (installUnicodeStringAlignment)
        {
            report.Add("Unicode string alignment has its own all-column data/metadata preflight; HR-wide string compatibility will be rechecked after conversion and before other installer DDL.");
        }
        else
        {
            using var preflightDb = new OracleDbContext(new DbContextOptionsBuilder<OracleDbContext>().UseOracle(connection,
                options => options.UseOracleSQLCompatibility(OracleSQLCompatibility.DatabaseVersion19)).Options);
            await HrStringRepair.PreflightAsync(connection, preflightDb);
            report.Add("HR bounded-string schema preflight passed before installer DDL.");
        }
    }
    if (backup) await HrSchemaBackup.Export(connection, root, report);
    if (installLeaveRequestCancellationAudit)
    {
        await HrLeaveRequestCancellationSchema.Install(connection, report);
        columns = await Query("SELECT TABLE_NAME,COLUMN_NAME,DATA_TYPE,NULLABLE FROM ALL_TAB_COLUMNS WHERE OWNER='DEV_TEMPLATE' AND (TABLE_NAME LIKE 'HR_%' OR TABLE_NAME='SYS_USERS') ORDER BY TABLE_NAME,COLUMN_ID");
    }
    if (installUnicodeStringAlignment)
    {
        var unicodeSql = AlignHrUnicodeStringColumns.GetSqlOperations();
        var unicodePlanPath = Path.Combine(root, "artifacts", "hr-audit", "ops16-unicode-string-alignment.sql");
        Directory.CreateDirectory(Path.GetDirectoryName(unicodePlanPath)!);
        File.WriteAllText(unicodePlanPath, string.Join(Environment.NewLine + "/" + Environment.NewLine,
            unicodeSql.Select((sql, index) => $"-- OPS-16 operation {index + 1}/{unicodeSql.Count}{Environment.NewLine}{sql}")));
        report.Add($"OPS-16 SQL plan saved for review: {unicodePlanPath}");
        for (var index = 0; index < unicodeSql.Count; index++)
        {
            await Execute(unicodeSql[index]);
        }
        report.Add("Applied the OPS-16 Unicode conversion operations after their complete source-column, data, constraint, and index preflight.");
        using var unicodePreflightDb = new OracleDbContext(new DbContextOptionsBuilder<OracleDbContext>().UseOracle(connection,
            options => options.UseOracleSQLCompatibility(OracleSQLCompatibility.DatabaseVersion19)).Options);
        await HrStringRepair.PreflightAsync(connection, unicodePreflightDb);
        report.Add("HR bounded-string preflight passed after Unicode conversion and before any subsequent installer DDL.");
        columns = await Query("SELECT TABLE_NAME,COLUMN_NAME,DATA_TYPE,NULLABLE FROM ALL_TAB_COLUMNS WHERE OWNER='DEV_TEMPLATE' AND (TABLE_NAME LIKE 'HR_%' OR TABLE_NAME='SYS_USERS') ORDER BY TABLE_NAME,COLUMN_ID");
    }
    if (installLedger)
    {
        await HrLedgerSchema.Install(connection, report);
        columns = await Query("SELECT TABLE_NAME,COLUMN_NAME,DATA_TYPE,NULLABLE FROM ALL_TAB_COLUMNS WHERE OWNER='DEV_TEMPLATE' AND (TABLE_NAME LIKE 'HR_%' OR TABLE_NAME='SYS_USERS') ORDER BY TABLE_NAME,COLUMN_ID");
    }
    if (installShifts)
    {
        await HrShiftSchema.Install(connection, report);
        columns = await Query("SELECT TABLE_NAME,COLUMN_NAME,DATA_TYPE,NULLABLE FROM ALL_TAB_COLUMNS WHERE OWNER='DEV_TEMPLATE' AND (TABLE_NAME LIKE 'HR_%' OR TABLE_NAME='SYS_USERS') ORDER BY TABLE_NAME,COLUMN_ID");
    }
    if (installTime)
    {
        await HrAttendanceTimeSchema.Install(connection, report);
        columns = await Query("SELECT TABLE_NAME,COLUMN_NAME,DATA_TYPE,NULLABLE FROM ALL_TAB_COLUMNS WHERE OWNER='DEV_TEMPLATE' AND (TABLE_NAME LIKE 'HR_%' OR TABLE_NAME='SYS_USERS') ORDER BY TABLE_NAME,COLUMN_ID");
    }
    if (installMobile)
    {
        await HrMobilePunchSchema.Install(connection, report);
        columns = await Query("SELECT TABLE_NAME,COLUMN_NAME,DATA_TYPE,NULLABLE FROM ALL_TAB_COLUMNS WHERE OWNER='DEV_TEMPLATE' AND (TABLE_NAME LIKE 'HR_%' OR TABLE_NAME='SYS_USERS') ORDER BY TABLE_NAME,COLUMN_ID");
    }
    if (installCorrections)
    {
        await HrAttendanceCorrectionSchema.Install(connection, report);
        columns = await Query("SELECT TABLE_NAME,COLUMN_NAME,DATA_TYPE,NULLABLE FROM ALL_TAB_COLUMNS WHERE OWNER='DEV_TEMPLATE' AND (TABLE_NAME LIKE 'HR_%' OR TABLE_NAME='SYS_USERS') ORDER BY TABLE_NAME,COLUMN_ID");
    }
    if (installBranchLocation)
    {
        await HrBranchLocationSchema.Install(connection, report);
        columns = await Query("SELECT TABLE_NAME,COLUMN_NAME,DATA_TYPE,NULLABLE FROM ALL_TAB_COLUMNS WHERE OWNER='DEV_TEMPLATE' AND (TABLE_NAME LIKE 'HR_%' OR TABLE_NAME='SYS_USERS') ORDER BY TABLE_NAME,COLUMN_ID");
    }
    if (installDepartmentManager)
    {
        await HrDepartmentManagerSchema.Install(connection, report);
        columns = await Query("SELECT TABLE_NAME,COLUMN_NAME,DATA_TYPE,NULLABLE FROM ALL_TAB_COLUMNS WHERE OWNER='DEV_TEMPLATE' AND (TABLE_NAME LIKE 'HR_%' OR TABLE_NAME='SYS_USERS') ORDER BY TABLE_NAME,COLUMN_ID");
    }
    if (verifyDepartmentManager)
        await HrDepartmentManagerSchema.Verify(connection, report);
    if (installBankAccountCode)
    {
        await HrBankAccountSchema.Install(connection, report);
        columns = await Query("SELECT TABLE_NAME,COLUMN_NAME,DATA_TYPE,NULLABLE FROM ALL_TAB_COLUMNS WHERE OWNER='DEV_TEMPLATE' AND (TABLE_NAME LIKE 'HR_%' OR TABLE_NAME='SYS_USERS') ORDER BY TABLE_NAME,COLUMN_ID");
    }
    if (verifyBankAccountCode)
        await HrBankAccountSchema.Verify(connection, report);
    if (installEosShortLeave)
    {
        await HrEosShortLeaveSchema.Install(connection, report);
        columns = await Query("SELECT TABLE_NAME,COLUMN_NAME,DATA_TYPE,NULLABLE FROM ALL_TAB_COLUMNS WHERE OWNER='DEV_TEMPLATE' AND (TABLE_NAME LIKE 'HR_%' OR TABLE_NAME='SYS_USERS') ORDER BY TABLE_NAME,COLUMN_ID");
    }
    if (installHandoff)
    {
        await HrAccountingHandoffSchema.Install(connection, report);
    }
    if (installPayrollCorrectionIndex)
        await HrPayrollCorrectionIndexSchema.Install(connection, report);
    if (installCurrencyPrecision)
    {
        await HrCurrencyPrecisionSchema.Install(connection, report);
        columns = await Query("SELECT TABLE_NAME,COLUMN_NAME,DATA_TYPE,NULLABLE FROM ALL_TAB_COLUMNS WHERE OWNER='DEV_TEMPLATE' AND (TABLE_NAME LIKE 'HR_%' OR TABLE_NAME='SYS_USERS') ORDER BY TABLE_NAME,COLUMN_ID");
    }
    if (installPayrollRecoveryAccounts)
        await HrPayrollRecoveryAccountsSchema.Install(connection, report);
    if (verifyPayrollRecoveryAccounts)
    {
        if (args.Length != 1)
        {
            Console.Error.WriteLine("Payroll recovery account schema verification is read-only and must run alone against DEV_TEMPLATE.");
            Environment.ExitCode = 1;
            return;
        }
        await HrPayrollRecoveryAccountsSchema.Verify(connection, report);
    }
    if (verifyCurrencyPrecision)
        await HrCurrencyPrecisionVerification.Verify(connection, builder.ConnectionString, report);
    if (verifyPayrollOvertimeOracle)
        await HrPayrollOvertimeOracleVerification.Verify(builder.ConnectionString, report);
    if (installPayrollAdjustmentDecision)
    {
        await HrPayrollAdjustmentDecisionSchema.Install(connection, report);
        columns = await Query("SELECT TABLE_NAME,COLUMN_NAME,DATA_TYPE,NULLABLE FROM ALL_TAB_COLUMNS WHERE OWNER='DEV_TEMPLATE' AND (TABLE_NAME LIKE 'HR_%' OR TABLE_NAME='SYS_USERS') ORDER BY TABLE_NAME,COLUMN_ID");
    }
    if (installLeavePolicyEffective)
    {
        await HrLeavePolicySchema.Install(connection, report);
        columns = await Query("SELECT TABLE_NAME,COLUMN_NAME,DATA_TYPE,NULLABLE FROM ALL_TAB_COLUMNS WHERE OWNER='DEV_TEMPLATE' AND (TABLE_NAME LIKE 'HR_%' OR TABLE_NAME='SYS_USERS') ORDER BY TABLE_NAME,COLUMN_ID");
    }
    if (installLeaveAccrualSettings)
    {
        await HrLeaveAccrualSettingsSchema.Install(connection, report);
        columns = await Query("SELECT TABLE_NAME,COLUMN_NAME,DATA_TYPE,NULLABLE FROM ALL_TAB_COLUMNS WHERE OWNER='DEV_TEMPLATE' AND (TABLE_NAME LIKE 'HR_%' OR TABLE_NAME='SYS_USERS') ORDER BY TABLE_NAME,COLUMN_ID");
    }
    if (installLeaveCitizenship)
    {
        await HrLeaveCitizenshipSchema.Install(connection, report);
        columns = await Query("SELECT TABLE_NAME,COLUMN_NAME,DATA_TYPE,NULLABLE FROM ALL_TAB_COLUMNS WHERE OWNER='DEV_TEMPLATE' AND (TABLE_NAME LIKE 'HR_%' OR TABLE_NAME='SYS_USERS') ORDER BY TABLE_NAME,COLUMN_ID");
    }
    if (installLeaveBalanceMigration)
    {
        await HrLeaveBalanceMigrationSchema.Install(connection, report);
        columns = await Query("SELECT TABLE_NAME,COLUMN_NAME,DATA_TYPE,NULLABLE FROM ALL_TAB_COLUMNS WHERE OWNER='DEV_TEMPLATE' AND (TABLE_NAME LIKE 'HR_%' OR TABLE_NAME='SYS_USERS') ORDER BY TABLE_NAME,COLUMN_ID");
    }
    if (installLeaveCashoutSettings)
    {
        await HrLeaveCashoutSettingsSchema.Install(connection, report);
        columns = await Query("SELECT TABLE_NAME,COLUMN_NAME,DATA_TYPE,NULLABLE FROM ALL_TAB_COLUMNS WHERE OWNER='DEV_TEMPLATE' AND (TABLE_NAME LIKE 'HR_%' OR TABLE_NAME='SYS_USERS') ORDER BY TABLE_NAME,COLUMN_ID");
    }
    if (installLeaveCashout)
    {
        await HrLeaveCashoutSchema.Install(connection, report);
        columns = await Query("SELECT TABLE_NAME,COLUMN_NAME,DATA_TYPE,NULLABLE FROM ALL_TAB_COLUMNS WHERE OWNER='DEV_TEMPLATE' AND (TABLE_NAME LIKE 'HR_%' OR TABLE_NAME='SYS_USERS') ORDER BY TABLE_NAME,COLUMN_ID");
    }
    if (verifyHandoff)
    {
        foreach (var table in new[] { "HR_ACCOUNTING_HANDOFF", "HR_ACCOUNTING_HANDOFF_LINE", "HR_ACCOUNTING_HANDOFF_TRANSITION", "HR_ACCOUNTING_OUTBOX", "ACC_HANDOFF_INBOX", "ACC_HANDOFF_JOURNAL_LINK", "ACC_HANDOFF_POSTING_RECEIPT", "ACC_HANDOFF_PAYMENT", "ACC_LOAN_FUNDING_RECEIPT" })
        {
            var exists = await Query($"SELECT TABLE_NAME FROM ALL_TABLES WHERE OWNER='DEV_TEMPLATE' AND TABLE_NAME='{table}'");
            if (exists.Count != 1) throw new InvalidOperationException($"Required handoff table {table} is missing.");
            report.Add($"Handoff table {table}: present.");
        }
        foreach (var column in new[] { "PAYMENT_METHOD_CODE", "FAILURE_REASON", "FAILED_AT_UTC", "FAILED_BY", "REVERSAL_REASON", "REVERSAL_EXTERNAL_REFERENCE", "REVERSAL_EVIDENCE_REFERENCE", "REVERSED_AT_UTC", "REVERSED_BY", "REVERSAL_VOUCHER_ID" })
        {
            var exists = await Query($"SELECT COLUMN_NAME FROM ALL_TAB_COLUMNS WHERE OWNER='DEV_TEMPLATE' AND TABLE_NAME='ACC_HANDOFF_PAYMENT' AND COLUMN_NAME='{column}'");
            if (exists.Count != 1) throw new InvalidOperationException($"ACC_HANDOFF_PAYMENT.{column} is missing.");
            report.Add($"Handoff payment column {column}: present.");
        }
        if ((await Query("SELECT COLUMN_NAME FROM ALL_TAB_COLUMNS WHERE OWNER='DEV_TEMPLATE' AND TABLE_NAME='ACC_HANDOFF_POSTING_RECEIPT' AND COLUMN_NAME='SETTLEMENT_SNAPSHOT_JSON'")).Count != 1)
            throw new InvalidOperationException("ACC_HANDOFF_POSTING_RECEIPT.SETTLEMENT_SNAPSHOT_JSON is missing.");
        report.Add("Posting receipt settlement snapshot column: present.");
        if ((await Query("SELECT COLUMN_NAME FROM ALL_TAB_COLUMNS WHERE OWNER='DEV_TEMPLATE' AND TABLE_NAME='ACC_LOAN_FUNDING_RECEIPT' AND COLUMN_NAME='FUNDING_VOUCHER_ID'")).Count != 1)
            throw new InvalidOperationException("ACC_LOAN_FUNDING_RECEIPT.FUNDING_VOUCHER_ID is missing.");
        report.Add("Funding Payment Voucher link column: present.");
        foreach (var column in new[] { "SOURCE_VERSION", "REVERSAL_VOUCHER_ID", "REVERSAL_EVENT_KEY", "REVERSAL_REASON", "REVERSAL_EXTERNAL_REFERENCE", "REVERSAL_EVIDENCE_REFERENCE", "REVERSED_AT_UTC", "REVERSED_BY" })
            if ((await Query($"SELECT COLUMN_NAME FROM ALL_TAB_COLUMNS WHERE OWNER='DEV_TEMPLATE' AND TABLE_NAME='ACC_LOAN_FUNDING_RECEIPT' AND COLUMN_NAME='{column}'")).Count != 1)
                throw new InvalidOperationException($"ACC_LOAN_FUNDING_RECEIPT.{column} is missing.");
        report.Add("Funding reversal receipt and version columns: present.");
        foreach (var column in new[] { "CORRECTION_OF_RUN_ID", "CORRECTION_ROOT_RUN_ID", "CORRECTION_VERSION", "CORRECTION_REASON" })
            if ((await Query($"SELECT COLUMN_NAME FROM ALL_TAB_COLUMNS WHERE OWNER='DEV_TEMPLATE' AND TABLE_NAME='HR_PAYROLL_RUN' AND COLUMN_NAME='{column}'")).Count != 1)
                throw new InvalidOperationException($"HR_PAYROLL_RUN.{column} is missing.");
        report.Add("Payroll correction lineage columns: present.");
        var constraints = await Query("SELECT CONSTRAINT_NAME,CONSTRAINT_TYPE,STATUS,VALIDATED,DELETE_RULE FROM ALL_CONSTRAINTS WHERE OWNER='DEV_TEMPLATE' AND CONSTRAINT_NAME IN ('UX_HR_HANDOFF_SOURCE','UX_HR_HANDOFF_REF','FK_HR_HANDOFF_LINE','FK_HR_HANDOFF_TRANS','FK_HR_HANDOFF_OUTBOX','FK_ACC_HANDOFF_INBOX','FK_ACC_HANDOFF_JOURNAL','FK_ACC_HANDOFF_JV','FK_ACC_HANDOFF_RECEIPT','FK_ACC_RECEIPT_JV','FK_ACC_HANDOFF_PAYMENT','UX_ACC_HANDOFF_PAY_KEY','UX_ACC_HANDOFF_PAY_SEQ','UX_ACC_HANDOFF_PAY_REF','FK_ACC_LOAN_FUND_RECEIPT','FK_ACC_FUNDING_GL_VOUCHER','FK_ACC_FUND_REV_GL_VCH','UX_ACC_FUND_HANDOFF','UX_ACC_FUND_EVENT','UX_ACC_FUND_SRCVER','UX_ACC_FUND_REF','UX_ACC_FUND_VOUCHER','UX_ACC_FUND_REV_EVENT','UX_ACC_FUND_REV_REF','UX_ACC_FUND_REV_VCH','FK_HR_PAYRUN_CORR_OF','FK_HR_PAYRUN_CORR_ROOT') ORDER BY CONSTRAINT_NAME");
        if (constraints.Count != 27 || constraints.Any(c => c[2] != "ENABLED" || c[3] != "VALIDATED" || (c[1] == "R" && c[4] != "NO ACTION")))
            throw new InvalidOperationException("Handoff unique/foreign-key constraints are missing, disabled, unvalidated, or allow cascading deletes.");
        report.Add("Handoff source/reference uniqueness and restrictive foreign keys: enabled and validated.");
        var fundingVoucherLink = await Query("SELECT cc.COLUMN_NAME,r.TABLE_NAME,rc.COLUMN_NAME FROM ALL_CONSTRAINTS c JOIN ALL_CONS_COLUMNS cc ON cc.OWNER=c.OWNER AND cc.CONSTRAINT_NAME=c.CONSTRAINT_NAME JOIN ALL_CONSTRAINTS r ON r.OWNER=c.R_OWNER AND r.CONSTRAINT_NAME=c.R_CONSTRAINT_NAME JOIN ALL_CONS_COLUMNS rc ON rc.OWNER=r.OWNER AND rc.CONSTRAINT_NAME=r.CONSTRAINT_NAME AND rc.POSITION=cc.POSITION WHERE c.OWNER='DEV_TEMPLATE' AND c.CONSTRAINT_NAME='FK_ACC_FUNDING_GL_VOUCHER'");
        if (fundingVoucherLink.Count != 1 || fundingVoucherLink[0][0] != "FUNDING_VOUCHER_ID" || fundingVoucherLink[0][1] != "GL_VOUCHER_HEADER" || fundingVoucherLink[0][2] != "ID")
            throw new InvalidOperationException("Funding voucher foreign key does not reference GL_VOUCHER_HEADER.ID.");
        var uniqueFundingVoucher = await Query("SELECT COLUMN_NAME FROM ALL_CONS_COLUMNS WHERE OWNER='DEV_TEMPLATE' AND CONSTRAINT_NAME='UX_ACC_FUND_VOUCHER' ORDER BY POSITION");
        if (uniqueFundingVoucher.Count != 1 || uniqueFundingVoucher[0][0] != "FUNDING_VOUCHER_ID")
            throw new InvalidOperationException("Funding voucher uniqueness is not defined on FUNDING_VOUCHER_ID.");
        var fundingSourceVersionColumns = await Query("SELECT COLUMN_NAME FROM ALL_CONS_COLUMNS WHERE OWNER='DEV_TEMPLATE' AND CONSTRAINT_NAME='UX_ACC_FUND_SRCVER' ORDER BY POSITION");
        if (fundingSourceVersionColumns.Count != 3 || fundingSourceVersionColumns[0][0] != "SOURCE_TYPE" || fundingSourceVersionColumns[1][0] != "SOURCE_ID" || fundingSourceVersionColumns[2][0] != "SOURCE_VERSION")
            throw new InvalidOperationException("Funding source version uniqueness is not defined on the expected columns.");
        var reversalVoucherLink = await Query("SELECT cc.COLUMN_NAME,r.TABLE_NAME,rc.COLUMN_NAME FROM ALL_CONSTRAINTS c JOIN ALL_CONS_COLUMNS cc ON cc.OWNER=c.OWNER AND cc.CONSTRAINT_NAME=c.CONSTRAINT_NAME JOIN ALL_CONSTRAINTS r ON r.OWNER=c.R_OWNER AND r.CONSTRAINT_NAME=c.R_CONSTRAINT_NAME JOIN ALL_CONS_COLUMNS rc ON rc.OWNER=r.OWNER AND rc.CONSTRAINT_NAME=r.CONSTRAINT_NAME AND rc.POSITION=cc.POSITION WHERE c.OWNER='DEV_TEMPLATE' AND c.CONSTRAINT_NAME='FK_ACC_FUND_REV_GL_VCH'");
        if (reversalVoucherLink.Count != 1 || reversalVoucherLink[0][0] != "REVERSAL_VOUCHER_ID" || reversalVoucherLink[0][1] != "GL_VOUCHER_HEADER" || reversalVoucherLink[0][2] != "ID")
            throw new InvalidOperationException("Funding reversal voucher foreign key does not reference GL_VOUCHER_HEADER.ID.");
        var reversalUniqueColumns = await Query("SELECT c.CONSTRAINT_NAME,cc.COLUMN_NAME FROM ALL_CONSTRAINTS c JOIN ALL_CONS_COLUMNS cc ON cc.OWNER=c.OWNER AND cc.CONSTRAINT_NAME=c.CONSTRAINT_NAME WHERE c.OWNER='DEV_TEMPLATE' AND c.CONSTRAINT_NAME IN ('UX_ACC_FUND_REV_EVENT','UX_ACC_FUND_REV_REF','UX_ACC_FUND_REV_VCH') ORDER BY c.CONSTRAINT_NAME,cc.POSITION");
        var expectedReversalUniqueColumns = new Dictionary<string, string> { ["UX_ACC_FUND_REV_EVENT"] = "REVERSAL_EVENT_KEY", ["UX_ACC_FUND_REV_REF"] = "REVERSAL_EXTERNAL_REFERENCE", ["UX_ACC_FUND_REV_VCH"] = "REVERSAL_VOUCHER_ID" };
        if (reversalUniqueColumns.Count != expectedReversalUniqueColumns.Count || reversalUniqueColumns.Any(c => !expectedReversalUniqueColumns.TryGetValue(c[0], out var column) || c[1] != column))
            throw new InvalidOperationException("Funding reversal event/reference/voucher uniqueness does not target the expected columns.");
        report.Add("Funding/reversal voucher FKs, source version uniqueness, and idempotency uniqueness target the expected columns.");
        var correctionForeignKeys = await Query("SELECT c.CONSTRAINT_NAME,cc.COLUMN_NAME,r.TABLE_NAME,rc.COLUMN_NAME FROM ALL_CONSTRAINTS c JOIN ALL_CONS_COLUMNS cc ON cc.OWNER=c.OWNER AND cc.CONSTRAINT_NAME=c.CONSTRAINT_NAME JOIN ALL_CONSTRAINTS r ON r.OWNER=c.R_OWNER AND r.CONSTRAINT_NAME=c.R_CONSTRAINT_NAME JOIN ALL_CONS_COLUMNS rc ON rc.OWNER=r.OWNER AND rc.CONSTRAINT_NAME=r.CONSTRAINT_NAME AND rc.POSITION=cc.POSITION WHERE c.OWNER='DEV_TEMPLATE' AND c.CONSTRAINT_NAME IN ('FK_HR_PAYRUN_CORR_OF','FK_HR_PAYRUN_CORR_ROOT') ORDER BY c.CONSTRAINT_NAME");
        if (correctionForeignKeys.Count != 2 || correctionForeignKeys.Any(c => c[2] != "HR_PAYROLL_RUN" || c[3] != "ID") || !correctionForeignKeys.Any(c => c[1] == "CORRECTION_OF_RUN_ID") || !correctionForeignKeys.Any(c => c[1] == "CORRECTION_ROOT_RUN_ID"))
            throw new InvalidOperationException("Payroll correction lineage foreign keys do not reference HR_PAYROLL_RUN.ID.");
        var correctionIndex = await Query("SELECT INDEX_TYPE,UNIQUENESS,STATUS FROM ALL_INDEXES WHERE OWNER='DEV_TEMPLATE' AND INDEX_NAME='UX_HR_PAYRUN_CORR_VER' AND TABLE_NAME='HR_PAYROLL_RUN'");
        var correctionExpressions = await Query("SELECT COLUMN_EXPRESSION FROM ALL_IND_EXPRESSIONS WHERE INDEX_OWNER='DEV_TEMPLATE' AND INDEX_NAME='UX_HR_PAYRUN_CORR_VER' ORDER BY COLUMN_POSITION");
        var expectedCorrectionExpressions = new[]
        {
            "CASE WHEN CORRECTION_ROOT_RUN_ID IS NOT NULL THEN CORRECTION_ROOT_RUN_ID END",
            "CASE WHEN CORRECTION_ROOT_RUN_ID IS NOT NULL THEN CORRECTION_VERSION END"
        };
        var actualCorrectionExpressions = correctionExpressions.Select(row => new string(row[0].Where(c => !char.IsWhiteSpace(c) && c != '"').Select(char.ToUpperInvariant).ToArray())).ToArray();
        var normalizedExpectedCorrectionExpressions = expectedCorrectionExpressions.Select(value => new string(value.Where(c => !char.IsWhiteSpace(c) && c != '"').Select(char.ToUpperInvariant).ToArray())).ToArray();
        if (correctionIndex.Count != 1 || correctionIndex[0][0] != "FUNCTION-BASED NORMAL" || correctionIndex[0][1] != "UNIQUE" || correctionIndex[0][2] != "VALID" || !actualCorrectionExpressions.SequenceEqual(normalizedExpectedCorrectionExpressions, StringComparer.Ordinal))
            throw new InvalidOperationException("Payroll correction root/version function-based unique index is missing, invalid, or malformed.");
        report.Add("Payroll correction index permits multiple root runs and uniquely constrains versions under each correction root.");
        var indexes = await Query("SELECT TABLE_NAME,COUNT(*) FROM ALL_CONSTRAINTS WHERE OWNER='DEV_TEMPLATE' AND TABLE_NAME IN ('HR_ACCOUNTING_HANDOFF','HR_ACCOUNTING_OUTBOX','ACC_HANDOFF_INBOX','ACC_HANDOFF_JOURNAL_LINK','ACC_HANDOFF_POSTING_RECEIPT','ACC_HANDOFF_PAYMENT','ACC_LOAN_FUNDING_RECEIPT','HR_PAYROLL_RUN') AND CONSTRAINT_TYPE='U' AND STATUS='ENABLED' AND VALIDATED='VALIDATED' GROUP BY TABLE_NAME");
        var requiredUnique = new Dictionary<string, int> { ["HR_ACCOUNTING_HANDOFF"] = 2, ["HR_ACCOUNTING_OUTBOX"] = 1, ["ACC_HANDOFF_INBOX"] = 1, ["ACC_HANDOFF_JOURNAL_LINK"] = 2, ["ACC_HANDOFF_POSTING_RECEIPT"] = 2, ["ACC_HANDOFF_PAYMENT"] = 3, ["ACC_LOAN_FUNDING_RECEIPT"] = 8 };
        foreach (var (table, count) in requiredUnique)
        {
            var actual = Convert.ToInt32(indexes.SingleOrDefault(i => i[0] == table)?[1] ?? "0");
            report.Add($"Unique indexes {table}: {actual}/{count}");
            if (actual != count) throw new InvalidOperationException($"A required unique index is missing on {table}.");
        }
        var handoffIndexes = await Query("""
            SELECT i.INDEX_NAME,i.TABLE_NAME,i.UNIQUENESS,i.STATUS,
                   LISTAGG(c.COLUMN_NAME, ',') WITHIN GROUP (ORDER BY c.COLUMN_POSITION)
            FROM ALL_INDEXES i
            JOIN ALL_IND_COLUMNS c ON c.INDEX_OWNER=i.OWNER AND c.INDEX_NAME=i.INDEX_NAME
            WHERE i.OWNER='DEV_TEMPLATE'
              AND i.INDEX_NAME IN ('IX_HR_HANDOFF_LINE','IX_HR_HANDOFF_TRANS','IX_HR_HANDOFF_OUTBOX','IX_ACC_HANDOFF_INBOX')
            GROUP BY i.INDEX_NAME,i.TABLE_NAME,i.UNIQUENESS,i.STATUS
            ORDER BY i.INDEX_NAME
            """);
        var expectedHandoffIndexes = new Dictionary<string, (string Table, string Columns)>
        {
            ["IX_HR_HANDOFF_LINE"] = ("HR_ACCOUNTING_HANDOFF_LINE", "HANDOFF_ID"),
            ["IX_HR_HANDOFF_TRANS"] = ("HR_ACCOUNTING_HANDOFF_TRANSITION", "HANDOFF_ID"),
            ["IX_HR_HANDOFF_OUTBOX"] = ("HR_ACCOUNTING_OUTBOX", "STATUS,CREATED_AT_UTC"),
            ["IX_ACC_HANDOFF_INBOX"] = ("ACC_HANDOFF_INBOX", "HANDOFF_ID,RECEIVED_AT_UTC")
        };
        if (handoffIndexes.Count != expectedHandoffIndexes.Count || handoffIndexes.Any(index =>
                !expectedHandoffIndexes.TryGetValue(index[0], out var expected) ||
                index[1] != expected.Table || index[2] != "NONUNIQUE" || index[3] != "VALID" || index[4] != expected.Columns))
            throw new InvalidOperationException("A handoff EF index is missing, invalid, or has incompatible columns.");
        report.Add("Handoff supporting indexes: all four EF indexes are valid with expected table and columns.");
    }
    if (installProcessing)
    {
        await HrAttendanceProcessingSchema.Install(connection, report);
        columns = await Query("SELECT TABLE_NAME,COLUMN_NAME,DATA_TYPE,NULLABLE FROM ALL_TAB_COLUMNS WHERE OWNER='DEV_TEMPLATE' AND (TABLE_NAME LIKE 'HR_%' OR TABLE_NAME='SYS_USERS') ORDER BY TABLE_NAME,COLUMN_ID");
    }
    if (migrationBaseline || migrationVerify)
    {
        await HrMigrationVerification.Run(connection, root, migrationVerify, report);
    }
    if (args.Contains("--schema-details"))
    {
        var unmappedHrTables = new[]
        {
            "HR_ASSET_ASSIGNMENT", "HR_ATTENDANCE_RECORD", "HR_CANDIDATE", "HR_CANDIDATE_APP",
            "HR_CANDIDATE_APPLICATION", "HR_EMPLOYEE_DOCUMENT", "HR_EMPLOYMENT_CONTRACT", "HR_EMPLOYMENT_EVENT",
            "HR_EOS_PROVISION_ACCRUAL", "HR_EOS_SETTLEMENT", "HR_EXPENSE_CLAIM", "HR_EXPENSE_CLAIM_LINE",
            "HR_FINAL_SETTLEMENT", "HR_JOB_GRADE", "HR_JOB_REQUISITION", "HR_ONBOARDING_TASK",
            "HR_OVERTIME_RECORD", "HR_PAYROLL_LINE_COMP", "HR_SALARY_REVISION", "HR_STATUTORY_RULE",
            "HR_EMP_SALARY_STRUCTURE", "HR_EMP_SALARY_STRUCT_LINE", "HR_EMP_SHIFT_ASSIGNMENT"
        };
        var availableHrTables = columns.Select(c => c[0]).ToHashSet(StringComparer.Ordinal);
        var missingUnmappedTables = unmappedHrTables.Where(table => !availableHrTables.Contains(table)).ToArray();
        if (missingUnmappedTables.Length > 0)
            throw new InvalidOperationException("Cannot audit row counts; unmapped HR table(s) missing in DEV_TEMPLATE: " + string.Join(", ", missingUnmappedTables));

        foreach (var row in await Query("SELECT SYS_CONTEXT('USERENV','SESSION_USER'),SYS_CONTEXT('USERENV','CURRENT_SCHEMA') FROM DUAL"))
            report.Add("Oracle session [user,current-schema]: " + string.Join(" | ", row));
        foreach (var row in await Query("SELECT TABLE_NAME,COLUMN_NAME,DATA_TYPE,DATA_LENGTH,DATA_PRECISION,DATA_SCALE,CHAR_LENGTH,CHAR_USED,NULLABLE,DATA_DEFAULT FROM ALL_TAB_COLUMNS WHERE OWNER='DEV_TEMPLATE' AND TABLE_NAME LIKE 'HR\\_%' ESCAPE '\\' ORDER BY TABLE_NAME,COLUMN_ID"))
            report.Add("Column [table,column,type,bytes,precision,scale,chars,char-used,nullable,default]: " + string.Join(" | ", row));
        foreach (var row in await Query("SELECT c.TABLE_NAME,c.CONSTRAINT_NAME,c.CONSTRAINT_TYPE,c.STATUS,c.VALIDATED,c.DELETE_RULE,x.COLUMN_NAME,x.POSITION,c.R_OWNER,c.R_CONSTRAINT_NAME FROM ALL_CONSTRAINTS c LEFT JOIN ALL_CONS_COLUMNS x ON x.OWNER=c.OWNER AND x.CONSTRAINT_NAME=c.CONSTRAINT_NAME WHERE c.OWNER='DEV_TEMPLATE' AND c.TABLE_NAME LIKE 'HR\\_%' ESCAPE '\\' ORDER BY c.TABLE_NAME,c.CONSTRAINT_NAME,x.POSITION"))
            report.Add("Constraint [table,name,type,status,validated,delete,column,position,ref-owner,ref-key]: " + string.Join(" | ", row));
        foreach (var row in await Query("SELECT i.TABLE_NAME,i.INDEX_NAME,i.UNIQUENESS,i.STATUS,x.COLUMN_NAME,x.COLUMN_POSITION FROM ALL_INDEXES i JOIN ALL_IND_COLUMNS x ON x.INDEX_OWNER=i.OWNER AND x.INDEX_NAME=i.INDEX_NAME WHERE i.TABLE_OWNER='DEV_TEMPLATE' AND i.TABLE_NAME LIKE 'HR\\_%' ESCAPE '\\' ORDER BY i.TABLE_NAME,i.INDEX_NAME,x.COLUMN_POSITION"))
            report.Add("Index [table,name,uniqueness,status,column,position]: " + string.Join(" | ", row));
        foreach (var row in await Query("SELECT TABLE_NAME,TRIGGER_NAME,STATUS,TRIGGERING_EVENT,TRIGGER_TYPE FROM ALL_TRIGGERS WHERE OWNER='DEV_TEMPLATE' AND TABLE_NAME LIKE 'HR\\_%' ESCAPE '\\' ORDER BY TABLE_NAME,TRIGGER_NAME"))
            report.Add("Trigger [table,name,status,event,type]: " + string.Join(" | ", row));
        foreach (var row in await Query("SELECT NAME,TYPE,LINE,POSITION,TEXT FROM ALL_ERRORS WHERE OWNER='DEV_TEMPLATE' AND TYPE='TRIGGER' AND NAME IN (SELECT TRIGGER_NAME FROM ALL_TRIGGERS WHERE OWNER='DEV_TEMPLATE' AND TABLE_NAME LIKE 'HR\\_%' ESCAPE '\\') ORDER BY NAME,SEQUENCE"))
            report.Add("Trigger error [name,type,line,position,text]: " + string.Join(" | ", row));
        foreach (var row in await Query("SELECT OBJECT_NAME,OBJECT_TYPE,STATUS FROM ALL_OBJECTS WHERE OWNER='DEV_TEMPLATE' AND OBJECT_TYPE='TRIGGER' AND OBJECT_NAME IN (SELECT TRIGGER_NAME FROM ALL_TRIGGERS WHERE OWNER='DEV_TEMPLATE' AND TABLE_NAME LIKE 'HR\\_%' ESCAPE '\\') ORDER BY OBJECT_NAME"))
            report.Add("Trigger object [name,type,status]: " + string.Join(" | ", row));
        foreach (var row in await Query("SELECT NAME,LINE,TEXT FROM ALL_SOURCE WHERE OWNER='DEV_TEMPLATE' AND TYPE='TRIGGER' AND NAME IN (SELECT TRIGGER_NAME FROM ALL_TRIGGERS WHERE OWNER='DEV_TEMPLATE' AND TABLE_NAME LIKE 'HR\\_%' ESCAPE '\\') ORDER BY NAME,LINE"))
            report.Add("Trigger source [name,line,text]: " + string.Join(" | ", row));
        foreach (var row in await Query("SELECT OWNER,TABLE_NAME,TRIGGER_NAME,STATUS,TRIGGERING_EVENT,TRIGGER_TYPE FROM ALL_TRIGGERS WHERE TRIGGER_NAME='TR_HR_LEAVE_MOVEMENT_IMM' OR TABLE_NAME='HR_LEAVE_MOVEMENT' ORDER BY OWNER,TRIGGER_NAME"))
            report.Add("Ledger trigger candidate [owner,table,name,status,event,type]: " + string.Join(" | ", row));
        foreach (var row in await Query("SELECT OWNER,NAME,TYPE,LINE,POSITION,TEXT FROM ALL_ERRORS WHERE TYPE='TRIGGER' AND NAME='TR_HR_LEAVE_MOVEMENT_IMM' ORDER BY OWNER,SEQUENCE"))
            report.Add("Ledger trigger compile error [owner,name,type,line,position,text]: " + string.Join(" | ", row));
        foreach (var row in await Query("SELECT OWNER,NAME,LINE,TEXT FROM ALL_SOURCE WHERE TYPE='TRIGGER' AND NAME='TR_HR_LEAVE_MOVEMENT_IMM' ORDER BY OWNER,LINE"))
            report.Add("Ledger trigger source by owner [owner,name,line,text]: " + string.Join(" | ", row));
        foreach (var row in await Query("SELECT OWNER,TABLE_OWNER,TABLE_NAME,TRIGGER_NAME,STATUS,TRIGGERING_EVENT FROM ALL_TRIGGERS WHERE TABLE_OWNER='DEV_TEMPLATE' AND TABLE_NAME LIKE 'HR\\_%' ESCAPE '\\' AND OWNER<>'DEV_TEMPLATE' ORDER BY TABLE_NAME,OWNER,TRIGGER_NAME"))
            report.Add("Foreign trigger attached to DEV_TEMPLATE [owner,table-owner,table,trigger,status,event]: " + string.Join(" | ", row));
        foreach (var row in await Query("SELECT OWNER,NAME,TYPE,LINE,POSITION,TEXT FROM ALL_ERRORS WHERE TYPE='TRIGGER' AND (OWNER,NAME) IN (SELECT OWNER,TRIGGER_NAME FROM ALL_TRIGGERS WHERE TABLE_OWNER='DEV_TEMPLATE' AND TABLE_NAME LIKE 'HR\\_%' ESCAPE '\\' AND OWNER<>'DEV_TEMPLATE') ORDER BY OWNER,NAME,SEQUENCE"))
            report.Add("Foreign trigger compile error [owner,name,type,line,position,text]: " + string.Join(" | ", row));
        report.Add("Unmapped HR table row counts: aggregate COUNT(*) only; no row values selected.");
        foreach (var table in unmappedHrTables)
        {
            var count = await Query($"SELECT COUNT(*) FROM DEV_TEMPLATE.{table}");
            report.Add($"Unmapped table [table,rows]: {table} | {count.Single()[0]}");
        }
        var unmappedTableList = string.Join(",", unmappedHrTables.Select(table => $"'{table}'"));
        report.Add("Unmapped HR static dependency scan: completed within DEV_TEMPLATE only; dynamic SQL and other schema owners are not included.");
        foreach (var row in await Query($"SELECT REFERENCED_NAME,OWNER,NAME,TYPE,DEPENDENCY_TYPE FROM ALL_DEPENDENCIES WHERE OWNER='DEV_TEMPLATE' AND REFERENCED_OWNER='DEV_TEMPLATE' AND REFERENCED_NAME IN ({unmappedTableList}) ORDER BY REFERENCED_NAME,OWNER,TYPE,NAME"))
            report.Add("Unmapped table static dependency [table,owner,name,type,dependency]: " + string.Join(" | ", row));
        foreach (var row in await Query("SELECT COUNT(*) FROM (SELECT EMPLOYEE_CODE,LEAVE_TYPE_CODE,YEAR_NO FROM DEV_TEMPLATE.HR_LEAVE_BALANCE GROUP BY EMPLOYEE_CODE,LEAVE_TYPE_CODE,YEAR_NO HAVING COUNT(*)>1)"))
            report.Add("Legacy balance duplicate employee/type/year groups: " + row[0]);
        foreach (var row in await Query("SELECT COUNT(*) FROM DEV_TEMPLATE.HR_LEAVE_BALANCE WHERE YEAR_NO<1 OR YEAR_NO>9998 OR YEAR_NO IS NULL"))
            report.Add("Legacy balance rows with year outside migration range 1..9998: " + row[0]);
        foreach (var row in await Query("SELECT COUNT(*) FROM DEV_TEMPLATE.HR_LEAVE_BALANCE WHERE ABS(ACCRUED_DAYS)>=10000 OR ABS(USED_DAYS)>=10000 OR ABS(CARRIED_FORWARD_DAYS)>=10000"))
            report.Add("Balance rows exceeding NUMBER(8,4) range: " + row[0]);
        Directory.CreateDirectory(Path.Combine(root, "artifacts", "hr-audit"));
        File.WriteAllLines(Path.Combine(root, "artifacts", "hr-audit", "dev-template-schema-details.txt"), report);
    }
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
    if (args.Contains("--verify-cycle-key"))
        await HrCycleVerification.Verify(connection, report);
    if (args.Contains("--verify-leave-policy-effective"))
        await HrLeavePolicySchema.Verify(connection, report);
    if (args.Contains("--verify-leave-accrual-settings"))
        await HrLeaveAccrualSettingsSchema.Verify(connection, report);
    if (args.Contains("--verify-leave-citizenship"))
        await HrLeaveCitizenshipSchema.Verify(connection, report);
    if (args.Contains("--verify-leave-balance-migration"))
        await HrLeaveBalanceMigrationSchema.Verify(connection, report);
    if (args.Contains("--verify-leave-cashout-settings"))
        await HrLeaveCashoutSettingsSchema.Verify(connection, report);
    if (args.Contains("--verify-leave-cashout"))
        await HrLeaveCashoutSchema.Verify(connection, report);
    if (installLeaveClosePolicySnapshots)
        await HrLeaveClosePolicySnapshotSchema.Install(connection, report);
    if (args.Contains("--verify-eos-short-leave"))
        await HrEosShortLeaveSchema.Verify(connection, report);
    if (args.Contains("--verify-leave-close-policy-snapshots"))
        await HrLeaveClosePolicySnapshotSchema.Verify(connection, report);
    if (auditLeaveBalanceMigration)
        await HrLeaveBalanceMigrationAudit.Run(connection, report);
    if (auditLegacyPayroll)
        await HrLegacyPayrollMigrationAudit.Run(connection, report);
    if (verifyLeaveBalanceMigrationWorkflow)
        await HrLeaveBalanceMigrationVerification.Verify(builder.ConnectionString, report);
    if (verifyLeaveConcurrency)
        await HrLeaveConcurrencyVerification.Verify(builder.ConnectionString, report);
    if (verifyAttendanceTimeSettings)
    {
        await Execute("ALTER SESSION SET CURRENT_SCHEMA=DEV_TEMPLATE");
        await HrAttendanceTimeSettingsVerification.Verify(connection, report);
    }
    if (verifyLeaveCloseConcurrency)
        await HrLeaveCloseConcurrencyVerification.Verify(builder.ConnectionString, report);
    if (verifyPayrollInputLock)
        await HrPayrollInputLockVerification.Verify(connection, builder.ConnectionString, report);
    if (verifyPayrollJournalOracle)
    {
        await Execute("ALTER SESSION SET CURRENT_SCHEMA=DEV_TEMPLATE");
        await HrPayrollJournalOracleVerification.Verify(connection, report);
    }
    if (verifyPayrollPaymentOracle)
    {
        await Execute("ALTER SESSION SET CURRENT_SCHEMA=DEV_TEMPLATE");
        await HrPayrollPaymentOracleVerification.Verify(connection, report);
    }
    if (verifyPayrollCorrectionOracle)
        await HrPayrollCorrectionOracleVerification.Verify(connection, builder.ConnectionString, report);
    if (verifyPayrollPeriodConcurrency)
    {
        await Execute("ALTER SESSION SET CURRENT_SCHEMA=DEV_TEMPLATE");
        await HrPayrollPeriodConcurrencyVerification.Verify(connection, builder.ConnectionString, report);
    }
    if (verifyAccountingOutboxOracle)
    {
        await Execute("ALTER SESSION SET CURRENT_SCHEMA=DEV_TEMPLATE");
        await HrAccountingOutboxOracleVerification.Verify(connection, builder.ConnectionString, report);
    }
    if (verifyLeaveManualAdjustment)
    {
        await Execute("ALTER SESSION SET CURRENT_SCHEMA=DEV_TEMPLATE");
        await HrLeaveManualAdjustmentVerification.Verify(connection, builder.ConnectionString, report);
    }
    if (verifyEmployeeImportRollback)
    {
        await Execute("ALTER SESSION SET CURRENT_SCHEMA=DEV_TEMPLATE");
        await HrEmployeeImportRollbackVerification.Verify(connection, report);
    }
    if (verifyLeaveWorkerDisabled)
        await HrLeaveWorkerVerification.VerifyDisabledTenantAsync(builder.ConnectionString, report);
    if (args.Contains("--verify-closed-cycle"))
    {
        await Execute("ALTER SESSION SET CURRENT_SCHEMA=DEV_TEMPLATE");
        await HrClosedCycleVerification.Verify(connection, report);
    }
    if (args.Contains("--verify-leave-ledger"))
    {
        await Execute("ALTER SESSION SET CURRENT_SCHEMA=DEV_TEMPLATE");
        await HrLedgerVerification.Verify(connection, report);
    }
    if (args.Contains("--verify-shift-resolution"))
    {
        await Execute("ALTER SESSION SET CURRENT_SCHEMA=DEV_TEMPLATE");
        await HrShiftVerification.Verify(connection, report);
    }
    if (args.Contains("--verify-shift-definitions"))
    {
        await Execute("ALTER SESSION SET CURRENT_SCHEMA=DEV_TEMPLATE");
        await HrShiftDefinitionVerification.Verify(connection, report);
    }
    if (args.Contains("--verify-shift-assignments"))
    {
        await Execute("ALTER SESSION SET CURRENT_SCHEMA=DEV_TEMPLATE");
        await HrShiftAssignmentVerification.Verify(connection, report);
    }
    if (args.Contains("--verify-shift-days"))
    {
        await Execute("ALTER SESSION SET CURRENT_SCHEMA=DEV_TEMPLATE");
        await HrShiftDayVerification.Verify(connection, report);
    }
    if (args.Contains("--verify-shift-generation"))
    {
        await Execute("ALTER SESSION SET CURRENT_SCHEMA=DEV_TEMPLATE");
        await HrShiftGenerationVerification.Verify(connection, report);
    }
    if (args.Contains("--verify-shift-timing"))
    {
        await Execute("ALTER SESSION SET CURRENT_SCHEMA=DEV_TEMPLATE");
        await HrShiftTimingVerification.Verify(connection, report);
    }
    if (args.Contains("--verify-scheduled-attendance") || args.Contains("--verify-mobile-punch-events"))
    {
        await Execute("ALTER SESSION SET CURRENT_SCHEMA=DEV_TEMPLATE");
        await HrScheduledAttendanceVerification.Verify(connection, report);
    }
    if (args.Contains("--verify-attendance-status"))
    {
        await Execute("ALTER SESSION SET CURRENT_SCHEMA=DEV_TEMPLATE");
        await HrAttendanceStatusVerification.Verify(connection, report);
    }
    if (args.Contains("--verify-attendance-corrections"))
    {
        await Execute("ALTER SESSION SET CURRENT_SCHEMA=DEV_TEMPLATE");
        await HrAttendanceCorrectionVerification.Verify(connection, report);
    }
    if (args.Contains("--verify-organization-catalog"))
    {
        await Execute("ALTER SESSION SET CURRENT_SCHEMA=DEV_TEMPLATE");
        await HrOrganizationCatalogVerification.Verify(connection, report);
    }
    if (args.Contains("--verify-organization"))
    {
        await Execute("ALTER SESSION SET CURRENT_SCHEMA=DEV_TEMPLATE");
        await HrOrganizationVerification.Verify(connection, report);
    }
    if (args.Contains("--verify-employee-profile"))
    {
        await Execute("ALTER SESSION SET CURRENT_SCHEMA=DEV_TEMPLATE");
        await HrEmployeeProfileVerification.Verify(connection, report);
    }
    if (args.Contains("--verify-employee-account"))
    {
        await Execute("ALTER SESSION SET CURRENT_SCHEMA=DEV_TEMPLATE");
        await HrEmployeeAccountVerification.Verify(connection, report);
    }
    if (args.Contains("--verify-employee-account-locks"))
    {
        await Execute("ALTER SESSION SET CURRENT_SCHEMA=DEV_TEMPLATE");
        await HrEmployeeAccountLockVerification.Verify(connection, builder.ConnectionString, report);
    }
    if (args.Contains("--verify-employee-team"))
    {
        await Execute("ALTER SESSION SET CURRENT_SCHEMA=DEV_TEMPLATE");
        await HrEmployeeTeamVerification.Verify(connection, report);
    }
    if (args.Contains("--verify-employee-import-preview"))
    {
        await Execute("ALTER SESSION SET CURRENT_SCHEMA=DEV_TEMPLATE");
        await HrEmployeeImportPreviewVerification.Verify(connection, report);
    }
    if (args.Contains("--verify-employee-fields"))
    {
        await Execute("ALTER SESSION SET CURRENT_SCHEMA=DEV_TEMPLATE");
        await HrEmployeeFieldVerification.Verify(connection, report);
    }
    if (args.Contains("--verify-accounting-boundary"))
    {
        await Execute("ALTER SESSION SET CURRENT_SCHEMA=DEV_TEMPLATE");
        await HrAccountingBoundaryVerification.Verify(connection, report);
    }
    if (args.Contains("--verify-journal-serial-locks"))
    {
        await Execute("ALTER SESSION SET CURRENT_SCHEMA=DEV_TEMPLATE");
        await HrJournalSerialLockVerification.Verify(connection, builder.ConnectionString, report);
    }
    if (args.Contains("--verify-payroll-financial-guards"))
    {
        await Execute("ALTER SESSION SET CURRENT_SCHEMA=DEV_TEMPLATE");
        await HrPayrollFinancialGuardVerification.Verify(connection, report);
    }
    if (args.Contains("--verify-payroll-posting"))
    {
        await Execute("ALTER SESSION SET CURRENT_SCHEMA=DEV_TEMPLATE");
        await HrPayrollPostingVerification.Verify(connection, report);
    }
    if (args.Contains("--verify-salary-metadata"))
    {
        await Execute("ALTER SESSION SET CURRENT_SCHEMA=DEV_TEMPLATE");
        await HrSalaryMetadataVerification.Verify(connection, report);
    }
    if (args.Contains("--verify-branch-attendance-location"))
    {
        await Execute("ALTER SESSION SET CURRENT_SCHEMA=DEV_TEMPLATE");
        await HrBranchLocationVerification.Verify(connection, report);
    }
    if (args.Contains("--verify-attendance-processing"))
    {
        await Execute("ALTER SESSION SET CURRENT_SCHEMA=DEV_TEMPLATE");
        await HrRawProcessingVerification.Verify(connection, report);
    }
    if (args.Contains("--inventory"))
    {
        foreach (var row in await Query("SELECT TABLE_NAME,COLUMN_NAME,DATA_TYPE,DATA_DEFAULT FROM ALL_TAB_COLUMNS WHERE OWNER='DEV_TEMPLATE' AND (TABLE_NAME IN ('HR_EMP_SALARY_STRUCTURE','HR_EMP_SALARY_STRUCT_LINE','HR_EMP_SHIFT_ASSIGNMENT','HR_SALARY_STRUCTURE','HR_SALARY_STRUCTURE_LINE','HR_EMPLOYEE_SHIFT_ASSIGNMENT','SYS_SCREEN','SYS_FEATURE','SYS_USERS','SYS_SYSTEM','SYS_SCREEN_FEATURE','SYS_FISCAL_YEAR','SYS_SETTING')) ORDER BY TABLE_NAME,COLUMN_ID"))
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
    if (verifyHandoff)
    {
        var handoffTables = new[]
        {
            "HR_ACCOUNTING_HANDOFF", "HR_ACCOUNTING_HANDOFF_LINE", "HR_ACCOUNTING_HANDOFF_TRANSITION",
            "HR_ACCOUNTING_OUTBOX", "ACC_HANDOFF_INBOX", "ACC_HANDOFF_JOURNAL_LINK",
            "ACC_HANDOFF_POSTING_RECEIPT", "ACC_HANDOFF_PAYMENT", "ACC_LOAN_FUNDING_RECEIPT"
        };
        foreach (var table in handoffTables)
        {
            var entity = context.Model.GetEntityTypes().SingleOrDefault(e => e.GetTableName() == table)
                ?? throw new InvalidOperationException($"No EF entity mapping exists for required handoff table {table}.");
            var store = StoreObjectIdentifier.Table(table, entity.GetSchema());
            var mappedColumns = entity.GetProperties()
                .Select(property => property.GetColumnName(store))
                .Where(column => !string.IsNullOrWhiteSpace(column))
                .Distinct(StringComparer.Ordinal)
                .ToArray();
            if (mappedColumns.Length == 0)
                throw new InvalidOperationException($"No mapped columns exist for required handoff table {table}.");

            // Ask Oracle to parse the full EF projection while reading no tenant rows.
            await Query($"SELECT {string.Join(",", mappedColumns.Select(column => "\"" + column + "\""))} FROM DEV_TEMPLATE.\"{table}\" WHERE 1=0");
            report.Add($"Handoff mapped SELECT accepted: {table} ({mappedColumns.Length} columns; no rows read).");
        }
    }
    if (registerHandoffPermissions)
        await HrAccountingHandoffPermissionCatalog.EnsureAsync(context, report);
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
    if (auditStringLengths)
    {
        var actualTextColumns = await Query("SELECT TABLE_NAME,COLUMN_NAME,DATA_TYPE,DATA_LENGTH,CHAR_LENGTH,CHAR_USED FROM ALL_TAB_COLUMNS WHERE OWNER='DEV_TEMPLATE' AND TABLE_NAME LIKE 'HR\\_%' ESCAPE '\\' AND DATA_TYPE IN ('CHAR','NCHAR','VARCHAR2','NVARCHAR2') ORDER BY TABLE_NAME,COLUMN_NAME");
        var actualByColumn = actualTextColumns.ToDictionary(r => (r[0], r[1]));
        var matches = 0;
        var mismatches = 0;
        var missing = 0;
        var unmapped = 0;
        foreach (var entity in hrEntities)
        {
            var table = entity.GetTableName()!;
            var identifier = StoreObjectIdentifier.Table(table, entity.GetSchema());
            foreach (var property in entity.GetProperties().Where(p => p.ClrType == typeof(string) && p.GetMaxLength().HasValue))
            {
                var column = property.GetColumnName(identifier);
                if (column is null) continue;
                if (!actualByColumn.TryGetValue((table, column), out var actual))
                {
                    missing++;
                    report.Add($"String length [EF/Oracle]: {table}.{column} | EF={property.GetMaxLength()} | Oracle=MISSING_OR_NON_TEXT");
                    continue;
                }
                var efLength = property.GetMaxLength()!.Value;
                var oracleChars = int.Parse(actual[4], System.Globalization.CultureInfo.InvariantCulture);
                var status = efLength == oracleChars ? "MATCH" : "MISMATCH";
                if (status == "MATCH") matches++; else mismatches++;
                report.Add($"String length [{status}]: {table}.{column} | EF={efLength} | OracleChars={oracleChars} | OracleType={actual[2]} | Bytes={actual[3]} | CharUsed={actual[5]}");
            }
        }
        foreach (var actual in actualTextColumns.Where(r => !hrEntities.Any(e => e.GetTableName() == r[0] && e.GetProperties().Any(p => p.ClrType == typeof(string) && p.GetColumnName(StoreObjectIdentifier.Table(r[0], e.GetSchema())) == r[1]))))
        {
            unmapped++;
            report.Add($"String length [UNMAPPED]: {actual[0]}.{actual[1]} | OracleChars={actual[4]} | OracleType={actual[2]} | Bytes={actual[3]} | CharUsed={actual[5]}");
        }
        var unmappedTables = actualTextColumns.GroupBy(r => r[0])
            .Where(group => !hrEntities.Any(entity => entity.GetTableName() == group.Key)).ToList();
        foreach (var table in unmappedTables)
            report.Add($"String length [UNMAPPED_TABLE]: {table.Key} | OracleTextColumns={table.Count()} | EF_TABLE_MAPPING=NONE");
        report.Add($"HR string-length audit summary: MATCH={matches}; MISMATCH={mismatches}; MISSING_OR_NON_TEXT={missing}; ORACLE_TEXT_UNMAPPED={unmapped}; UNMAPPED_TABLES={unmappedTables.Count}; target=DEV_TEMPLATE; read-only.");
        File.WriteAllLines(Path.Combine(root, "artifacts", "hr-audit", "dev-template-hr-string-lengths.txt"), report.Where(line => line.StartsWith("String length [") || line.StartsWith("HR string-length audit summary:")));
    }
    if (widenCurrencyCodeColumns)
    {
        var narrowCodes = await Query("SELECT TABLE_NAME,CHAR_LENGTH FROM ALL_TAB_COLUMNS WHERE OWNER='DEV_TEMPLATE' AND TABLE_NAME LIKE 'HR\\_%' ESCAPE '\\' AND COLUMN_NAME='CURRENCY_CODE' AND DATA_TYPE='NVARCHAR2' AND CHAR_LENGTH<20 ORDER BY TABLE_NAME");
        foreach (var row in narrowCodes)
        {
            if (!System.Text.RegularExpressions.Regex.IsMatch(row[0], "^[A-Z0-9_]+$"))
                throw new InvalidOperationException("Unexpected HR table name in currency-code widening inventory.");
            await Execute($"ALTER TABLE DEV_TEMPLATE.{row[0]} MODIFY (CURRENCY_CODE NVARCHAR2(20))");
            report.Add($"Widened DEV_TEMPLATE.{row[0]}.CURRENCY_CODE from {row[1]} to 20 characters; data retained.");
        }
        var verify = await Query("SELECT TABLE_NAME,CHAR_LENGTH FROM ALL_TAB_COLUMNS WHERE OWNER='DEV_TEMPLATE' AND TABLE_NAME LIKE 'HR\\_%' ESCAPE '\\' AND COLUMN_NAME='CURRENCY_CODE' AND DATA_TYPE='NVARCHAR2' AND CHAR_LENGTH<20");
        if (verify.Count > 0)
            throw new InvalidOperationException("One or more HR currency-code columns remain below 20 characters.");
        report.Add($"Currency-code widening verified; changed {narrowCodes.Count} column(s), target=DEV_TEMPLATE; other schemas untouched.");
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
    report.Add("Schema verification failed: " + ex.GetType().Name + ": " + ex.Message);
    if (!string.Equals(ex.Message, ex.GetBaseException().Message, StringComparison.Ordinal))
        report.Add("Root cause: " + ex.GetBaseException().Message);
    Environment.ExitCode = 1;
}
Directory.CreateDirectory(Path.Combine(root, "artifacts", "hr-audit"));
File.WriteAllLines(Path.Combine(root, "artifacts", "hr-audit", "dev-template-schema.txt"), report);
foreach (var line in report) Console.WriteLine(line);
