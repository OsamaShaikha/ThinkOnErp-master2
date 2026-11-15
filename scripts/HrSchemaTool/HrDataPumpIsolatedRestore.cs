using System.Security.Cryptography;
using System.Text.Json;
using System.Text.RegularExpressions;
using Oracle.ManagedDataAccess.Client;
using Oracle.ManagedDataAccess.Types;

internal static class HrDataPumpIsolatedRestore
{
    private const string SourceSchema = "DEV_TEMPLATE";
    private const string TargetSchema = "HR_RESTORE_20261112";
    private const string DumpName = "dev_template_20261112091921.dmp";
    private const string DirectoryName = "DATA_PUMP_DIR";

    private static readonly string[] ExcludedPaths =
    [
        "%GRANT%",
        "%TRIGGER%",
        "%VIEW%",
        "%PROCACT_SCHEMA%",
        "%SYNONYM%",
        "DEFAULT_ROLE",
        "TABLESPACE_QUOTA"
    ];

    public static async Task RestoreAsync(OracleConnection connection, string root, List<string> report)
    {
        var backupDirectory = Path.Combine(root, "artifacts", "hr-audit", "backups", "20261112091921");
        var manifestPath = Path.Combine(backupDirectory, "manifest.json");
        var dumpPath = Path.Combine(backupDirectory, DumpName);
        var exportLogPath = Path.Combine(backupDirectory, "dev_template_20261112091921.log");
        var manifest = JsonDocument.Parse(await File.ReadAllTextAsync(manifestPath)).RootElement;
        var expectedHash = manifest.GetProperty("SHA256").GetString();
        var expectedBytes = manifest.GetProperty("Bytes").GetInt64();
        if (manifest.GetProperty("Schema").GetString() != SourceSchema || manifest.GetProperty("Dump").GetString() != DumpName)
            throw new InvalidOperationException("The local Data Pump manifest does not match the fixed DEV_TEMPLATE restore source.");

        await using (var dump = File.OpenRead(dumpPath))
        {
            var hash = Convert.ToHexString(await SHA256.HashDataAsync(dump));
            if (!string.Equals(hash, expectedHash, StringComparison.OrdinalIgnoreCase) || dump.Length != expectedBytes)
                throw new InvalidOperationException("The local DEV_TEMPLATE dump does not match its recorded size and SHA-256.");
        }

        var serverDump = await ScalarAsync(connection, $"SELECT DBMS_LOB.FILEEXISTS(BFILENAME('{DirectoryName}','{DumpName}')) FROM DUAL");
        var container = await ScalarAsync(connection, "SELECT SYS_CONTEXT('USERENV','CON_NAME') FROM DUAL");
        var targetCount = await ScalarAsync(connection, $"SELECT COUNT(*) FROM DBA_USERS WHERE USERNAME='{TargetSchema}'");
        var importRole = await ScalarAsync(connection, "SELECT COUNT(*) FROM SESSION_ROLES WHERE ROLE IN ('DATAPUMP_IMP_FULL_DATABASE','DBA')");
        var tablespaces = await QueryAsync(connection, "SELECT TABLESPACE_NAME FROM DBA_SEGMENTS WHERE OWNER='DEV_TEMPLATE' GROUP BY TABLESPACE_NAME ORDER BY TABLESPACE_NAME");
        var usersOnline = await ScalarAsync(connection, "SELECT COUNT(*) FROM DBA_TABLESPACES WHERE TABLESPACE_NAME='USERS' AND CONTENTS='PERMANENT' AND STATUS='ONLINE'");
        if (container.Equals("CDB$ROOT", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Isolated restore refused before DDL: connection is to CDB$ROOT; use a dedicated PDB service so the scratch schema is a local user.");
        if (serverDump != "1" || targetCount != "0" || importRole == "0" || usersOnline != "1" ||
            tablespaces.Count != 1 || tablespaces[0][0] != "USERS")
            throw new InvalidOperationException("Restore safety preflight failed: verify server dump, absent scratch owner, Data Pump role, and source/target tablespaces before any DDL.");

        var suffix = DateTime.UtcNow.ToString("yyyyMMddHHmmss");
        var jobName = "HRI" + suffix;
        var serverLogName = "hr_restore_" + suffix + ".log";
        var restoreDirectory = Path.Combine(root, "artifacts", "hr-audit", "restores", suffix);
        Directory.CreateDirectory(restoreDirectory);

        // The generated credential is never logged or persisted. The imported scratch account stays locked.
        var oneTimePassword = Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
        await ExecuteAsync(connection,
            $"CREATE USER \"{TargetSchema}\" IDENTIFIED BY \"{oneTimePassword}\" DEFAULT TABLESPACE \"USERS\" TEMPORARY TABLESPACE \"TEMP\" QUOTA UNLIMITED ON \"USERS\" ACCOUNT LOCK");
        report.Add($"Created isolated, locked schema {TargetSchema}; no CREATE SESSION or application grants were added.");

        string jobState;
        try
        {
            await using var command = connection.CreateCommand();
            command.BindByName = true;
            command.CommandTimeout = 0;
            command.CommandText = BuildImportBlock();
            command.Parameters.Add("job_name", OracleDbType.Varchar2).Value = jobName;
            command.Parameters.Add("dump_name", OracleDbType.Varchar2).Value = DumpName;
            command.Parameters.Add("log_name", OracleDbType.Varchar2).Value = serverLogName;
            command.Parameters.Add("job_state", OracleDbType.Varchar2, 100).Direction = System.Data.ParameterDirection.Output;
            await command.ExecuteNonQueryAsync();
            jobState = command.Parameters["job_state"].Value.ToString() ?? "UNKNOWN";
        }
        catch
        {
            await TryDownloadAsync(connection, serverLogName, Path.Combine(restoreDirectory, serverLogName));
            report.Add($"Data Pump import did not return normally. The isolated schema {TargetSchema} remains locked for diagnosis; it was not dropped automatically.");
            throw;
        }

        var localLogPath = Path.Combine(restoreDirectory, serverLogName);
        await DownloadAsync(connection, serverLogName, localLogPath);
        var importLog = await File.ReadAllTextAsync(localLogPath);
        var oraErrors = Regex.Matches(importLog, @"ORA-\d{5}").Select(match => match.Value).Distinct().ToArray();
        if (jobState != "COMPLETED" || oraErrors.Length != 0)
        {
            report.Add($"Data Pump state={jobState}; Oracle diagnostics={string.Join(',', oraErrors)}; isolated schema remains locked for diagnosis.");
            throw new InvalidOperationException("The isolated restore did not pass Data Pump completion/error checks.");
        }

        var accountStatus = await ScalarAsync(connection, $"SELECT ACCOUNT_STATUS FROM DBA_USERS WHERE USERNAME='{TargetSchema}'");
        if (accountStatus is null || !accountStatus.Contains("LOCKED", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("The scratch schema is not account-locked after import; stop before verification or use.");

        var expectedHrRows = ReadHrRowCounts(await File.ReadAllTextAsync(exportLogPath), "exported", SourceSchema);
        var importedHrRows = ReadHrRowCounts(importLog, "imported", TargetSchema);
        var sourceTables = (await QueryAsync(connection, "SELECT TABLE_NAME FROM DBA_TABLES WHERE OWNER='DEV_TEMPLATE' AND TABLE_NAME LIKE 'HR\\_%' ESCAPE '\\' ORDER BY TABLE_NAME"))
            .Select(row => row[0]).ToArray();
        var targetTables = (await QueryAsync(connection, $"SELECT TABLE_NAME FROM DBA_TABLES WHERE OWNER='{TargetSchema}' AND TABLE_NAME LIKE 'HR\\_%' ESCAPE '\\' ORDER BY TABLE_NAME"))
            .Select(row => row[0]).ToArray();
        if (!sourceTables.SequenceEqual(targetTables, StringComparer.Ordinal))
            throw new InvalidOperationException($"HR table inventory differs after restore: source={sourceTables.Length}, target={targetTables.Length}.");
        if (expectedHrRows.Keys.Except(sourceTables, StringComparer.Ordinal).Any() || importedHrRows.Keys.Except(sourceTables, StringComparer.Ordinal).Any())
            throw new InvalidOperationException("A Data Pump log includes an HR table absent from the restored table inventory.");

        var rowCountMismatches = new List<string>();
        foreach (var table in sourceTables)
        {
            if (!Regex.IsMatch(table, "^HR_[A-Z0-9_$#]+$", RegexOptions.CultureInvariant))
                throw new InvalidOperationException("Unexpected table name in DBA_TABLES; refusing dynamic row-count SQL.");
            var restoredRows = await ScalarAsync(connection, $"SELECT COUNT(*) FROM \"{TargetSchema}\".\"{table}\"");
            var backupRows = expectedHrRows.GetValueOrDefault(table);
            var importedRows = importedHrRows.GetValueOrDefault(table);
            if (!long.TryParse(restoredRows, out var actualRows) || actualRows != backupRows || importedRows != backupRows)
                rowCountMismatches.Add($"{table}: backupLog={backupRows}, importLog={importedRows}, restored={restoredRows}");
        }
        if (rowCountMismatches.Count != 0)
            throw new InvalidOperationException("HR row counts differ from the Data Pump export log: " + string.Join("; ", rowCountMismatches));

        var structureMismatch = await CountHrColumnDifferencesAsync(connection);
        var sourceConstraints = await QueryAsync(connection, "SELECT CONSTRAINT_TYPE,STATUS,VALIDATED,COUNT(*) FROM DBA_CONSTRAINTS WHERE OWNER='DEV_TEMPLATE' AND TABLE_NAME LIKE 'HR\\_%' ESCAPE '\\' GROUP BY CONSTRAINT_TYPE,STATUS,VALIDATED ORDER BY CONSTRAINT_TYPE,STATUS,VALIDATED");
        var targetConstraints = await QueryAsync(connection, $"SELECT CONSTRAINT_TYPE,STATUS,VALIDATED,COUNT(*) FROM DBA_CONSTRAINTS WHERE OWNER='{TargetSchema}' AND TABLE_NAME LIKE 'HR\\_%' ESCAPE '\\' GROUP BY CONSTRAINT_TYPE,STATUS,VALIDATED ORDER BY CONSTRAINT_TYPE,STATUS,VALIDATED");
        var sourceIndexes = await QueryAsync(connection, "SELECT INDEX_TYPE,UNIQUENESS,STATUS,COUNT(*) FROM DBA_INDEXES WHERE OWNER='DEV_TEMPLATE' AND TABLE_NAME LIKE 'HR\\_%' ESCAPE '\\' GROUP BY INDEX_TYPE,UNIQUENESS,STATUS ORDER BY INDEX_TYPE,UNIQUENESS,STATUS");
        var targetIndexes = await QueryAsync(connection, $"SELECT INDEX_TYPE,UNIQUENESS,STATUS,COUNT(*) FROM DBA_INDEXES WHERE OWNER='{TargetSchema}' AND TABLE_NAME LIKE 'HR\\_%' ESCAPE '\\' GROUP BY INDEX_TYPE,UNIQUENESS,STATUS ORDER BY INDEX_TYPE,UNIQUENESS,STATUS");
        var constraintMismatch = !RowsEqual(sourceConstraints, targetConstraints);
        var indexMismatch = !RowsEqual(sourceIndexes, targetIndexes);
        if (structureMismatch != 0 || constraintMismatch || indexMismatch)
            throw new InvalidOperationException($"Restore metadata check failed: HR column differences={structureMismatch}, constraint groups match={!constraintMismatch}, index groups match={!indexMismatch}.");

        await File.WriteAllTextAsync(Path.Combine(restoreDirectory, "manifest.json"), JsonSerializer.Serialize(new
        {
            SourceSchema,
            TargetSchema,
            BackupDump = DumpName,
            BackupBytes = expectedBytes,
            BackupSha256 = expectedHash,
            ImportJob = jobName,
            ImportState = jobState,
            OracleDiagnostics = oraErrors,
            ImportedHrTableCount = sourceTables.Length,
            HrRowCountsMatchExportLog = true,
            HrColumnMetadataDifferences = structureMismatch,
            HrConstraintGroupsMatch = !constraintMismatch,
            HrIndexGroupsMatch = !indexMismatch,
            ExcludedObjectPaths = ExcludedPaths,
            ExcludedReason = "Prevent source-schema triggers/views, server grants, synonyms and replication pro-actions from escaping the locked scratch schema."
        }, new JsonSerializerOptions { WriteIndented = true }));
        report.Add($"Isolated Data Pump restore completed: {TargetSchema}; state={jobState}; HR tables={sourceTables.Length}; row counts match backup log; HR columns/constraint groups/index groups match current DEV_TEMPLATE; schema remains locked.");
        report.Add($"Import log and manifest: {restoreDirectory}; import ORA count={oraErrors.Length}; backup export log used for row-count verification.");
        report.Add("Views, triggers, synonyms, grants, quota/default-role metadata and LOGREP pro-actions were excluded to prevent cross-schema effects; this is an isolated structural/data restore, not a runnable tenant.");
    }

    private static string BuildImportBlock()
    {
        var filters = string.Join(Environment.NewLine, ExcludedPaths.Select(path =>
        {
            var predicate = path.Contains('%', StringComparison.Ordinal) ? $"LIKE '{path}'" : $"= '{path}'";
            var escapedPredicate = predicate.Replace("'", "''", StringComparison.Ordinal);
            return $"DBMS_DATAPUMP.METADATA_FILTER(h, 'EXCLUDE_PATH_EXPR', '{escapedPredicate}');";
        }));
        return $"""
            DECLARE
                h NUMBER;
                s VARCHAR2(100);
            BEGIN
                h := DBMS_DATAPUMP.OPEN(operation=>'IMPORT', job_mode=>'SCHEMA', job_name=>:job_name);
                DBMS_DATAPUMP.ADD_FILE(h, :dump_name, 'DATA_PUMP_DIR', filetype=>DBMS_DATAPUMP.KU$_FILE_TYPE_DUMP_FILE);
                DBMS_DATAPUMP.ADD_FILE(h, :log_name, 'DATA_PUMP_DIR', filetype=>DBMS_DATAPUMP.KU$_FILE_TYPE_LOG_FILE);
                DBMS_DATAPUMP.METADATA_FILTER(h, 'SCHEMA_EXPR', '= ''DEV_TEMPLATE''');
                DBMS_DATAPUMP.METADATA_REMAP(h, 'REMAP_SCHEMA', 'DEV_TEMPLATE', 'HR_RESTORE_20261112');
                {filters}
                DBMS_DATAPUMP.SET_PARAMETER(h, 'TABLE_EXISTS_ACTION', 'SKIP');
                DBMS_DATAPUMP.START_JOB(h);
                DBMS_DATAPUMP.WAIT_FOR_JOB(h, s);
                :job_state := s;
            END;
            """;
    }

    private static Dictionary<string, long> ReadHrRowCounts(string log, string operation, string owner)
    {
        var result = new Dictionary<string, long>(StringComparer.Ordinal);
        var pattern = string.Format(
            "(?m)^\\. \\. {0} \"{1}\"\\.\"(?<table>HR_[A-Z0-9_$#]+)\"\\s+.*?\\s+(?<rows>\\d+)\\s+rows\\s*$",
            Regex.Escape(operation), Regex.Escape(owner));
        foreach (Match match in Regex.Matches(log, pattern))
            result[match.Groups["table"].Value] = long.Parse(match.Groups["rows"].Value, System.Globalization.CultureInfo.InvariantCulture);
        return result;
    }

    private static async Task<int> CountHrColumnDifferencesAsync(OracleConnection connection)
    {
        const string sql = """
            WITH source_columns AS (
                SELECT TABLE_NAME,COLUMN_NAME,DATA_TYPE,DATA_LENGTH,NVL(DATA_PRECISION,-1) DATA_PRECISION,
                       NVL(DATA_SCALE,-1) DATA_SCALE,CHAR_LENGTH,NVL(CHAR_USED,'-') CHAR_USED,NULLABLE
                FROM DBA_TAB_COLUMNS c JOIN DBA_TABLES t ON t.OWNER=c.OWNER AND t.TABLE_NAME=c.TABLE_NAME
                WHERE c.OWNER='DEV_TEMPLATE' AND c.TABLE_NAME LIKE 'HR\_%' ESCAPE '\'
            ), target_columns AS (
                SELECT TABLE_NAME,COLUMN_NAME,DATA_TYPE,DATA_LENGTH,NVL(DATA_PRECISION,-1) DATA_PRECISION,
                       NVL(DATA_SCALE,-1) DATA_SCALE,CHAR_LENGTH,NVL(CHAR_USED,'-') CHAR_USED,NULLABLE
                FROM DBA_TAB_COLUMNS c JOIN DBA_TABLES t ON t.OWNER=c.OWNER AND t.TABLE_NAME=c.TABLE_NAME
                WHERE c.OWNER='HR_RESTORE_20261112' AND c.TABLE_NAME LIKE 'HR\_%' ESCAPE '\'
            )
            SELECT COUNT(*) FROM (
                SELECT * FROM source_columns MINUS SELECT * FROM target_columns
                UNION ALL
                SELECT * FROM target_columns MINUS SELECT * FROM source_columns
            )
            """;
        return int.Parse(await ScalarAsync(connection, sql), System.Globalization.CultureInfo.InvariantCulture);
    }

    private static bool RowsEqual(IReadOnlyList<string[]> left, IReadOnlyList<string[]> right) =>
        left.Count == right.Count && left.Zip(right).All(pair => pair.First.SequenceEqual(pair.Second, StringComparer.Ordinal));

    private static async Task<string> ScalarAsync(OracleConnection connection, string sql)
    {
        await using var command = connection.CreateCommand();
        command.BindByName = true;
        command.CommandTimeout = 120;
        command.CommandText = sql;
        return Convert.ToString(await command.ExecuteScalarAsync(), System.Globalization.CultureInfo.InvariantCulture) ?? "";
    }

    private static async Task<List<string[]>> QueryAsync(OracleConnection connection, string sql)
    {
        await using var command = connection.CreateCommand();
        command.BindByName = true;
        command.CommandTimeout = 120;
        command.CommandText = sql;
        await using var reader = await command.ExecuteReaderAsync();
        var rows = new List<string[]>();
        while (await reader.ReadAsync())
            rows.Add(Enumerable.Range(0, reader.FieldCount)
                .Select(index => reader.IsDBNull(index) ? "NULL" : Convert.ToString(reader.GetValue(index), System.Globalization.CultureInfo.InvariantCulture)!)
                .ToArray());
        return rows;
    }

    private static async Task ExecuteAsync(OracleConnection connection, string sql)
    {
        await using var command = connection.CreateCommand();
        command.BindByName = true;
        command.CommandTimeout = 30;
        command.CommandText = sql;
        await command.ExecuteNonQueryAsync();
    }

    private static async Task DownloadAsync(OracleConnection connection, string serverFile, string localPath)
    {
        using var source = new OracleBFile(connection, DirectoryName, serverFile);
        source.OpenFile();
        await using var destination = new FileStream(localPath, FileMode.CreateNew, FileAccess.Write);
        var buffer = new byte[1024 * 1024];
        int count;
        while ((count = source.Read(buffer, 0, buffer.Length)) > 0)
            await destination.WriteAsync(buffer.AsMemory(0, count));
        await destination.FlushAsync();
        if (destination.Length != source.Length || destination.Length == 0)
            throw new InvalidOperationException("Downloaded Data Pump import log length mismatch or empty file.");
        source.CloseFile();
    }

    private static async Task TryDownloadAsync(OracleConnection connection, string serverFile, string localPath)
    {
        try
        {
            await DownloadAsync(connection, serverFile, localPath);
        }
        catch
        {
            // Preserve the original Data Pump failure; the server-side job/log remains available for diagnosis.
        }
    }
}
