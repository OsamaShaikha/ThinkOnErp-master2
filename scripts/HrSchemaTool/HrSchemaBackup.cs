using System.Security.Cryptography;
using System.Text.Json;
using Oracle.ManagedDataAccess.Client;
using Oracle.ManagedDataAccess.Types;
using System.Data;

internal static class HrSchemaBackup
{
    public static async Task Export(OracleConnection connection, string root, List<string> report)
    {
        var suffix = DateTime.UtcNow.ToString("yyyyMMddHHmmss");
        var job = "HR_BACKUP_" + suffix;
        var dump = "dev_template_" + suffix + ".dmp";
        var log = "dev_template_" + suffix + ".log";
        var folder = Path.Combine(root, "artifacts", "hr-audit", "backups", suffix);
        Directory.CreateDirectory(folder);
        await using var command = connection.CreateCommand();
        command.BindByName = true;
        command.CommandTimeout = 0;
        command.CommandText = """
            DECLARE h NUMBER; s VARCHAR2(100); scn NUMBER;
            BEGIN
              scn := DBMS_FLASHBACK.GET_SYSTEM_CHANGE_NUMBER;
              h := DBMS_DATAPUMP.OPEN(operation=>'EXPORT',job_mode=>'SCHEMA',job_name=>:job);
              DBMS_DATAPUMP.ADD_FILE(h,:dump,'DATA_PUMP_DIR',filetype=>DBMS_DATAPUMP.KU$_FILE_TYPE_DUMP_FILE);
              DBMS_DATAPUMP.ADD_FILE(h,:log,'DATA_PUMP_DIR',filetype=>DBMS_DATAPUMP.KU$_FILE_TYPE_LOG_FILE);
              DBMS_DATAPUMP.METADATA_FILTER(h,'SCHEMA_EXPR','= ''DEV_TEMPLATE''');
              DBMS_DATAPUMP.SET_PARAMETER(h,'FLASHBACK_SCN',scn);
              DBMS_DATAPUMP.START_JOB(h);
              DBMS_DATAPUMP.WAIT_FOR_JOB(h,s);
              :state := s;
            END;
            """;
        command.Parameters.Add("job", OracleDbType.Varchar2).Value = job;
        command.Parameters.Add("dump", OracleDbType.Varchar2).Value = dump;
        command.Parameters.Add("log", OracleDbType.Varchar2).Value = log;
        command.Parameters.Add("state", OracleDbType.Varchar2, 100).Direction = ParameterDirection.Output;
        await command.ExecuteNonQueryAsync();
        var state = command.Parameters["state"].Value.ToString();
        report.Add("DEV_TEMPLATE Data Pump export state: " + state);
        if (state != "COMPLETED")
            throw new InvalidOperationException("Data Pump export did not complete; do not apply repair.");

        // BFILE reads copy the server export to the user's PC through this connection.
        foreach (var name in new[] { dump, log })
        {
            using var source = new OracleBFile(connection, "DATA_PUMP_DIR", name);
            source.OpenFile();
            await using var destination = new FileStream(Path.Combine(folder, name), FileMode.CreateNew, FileAccess.Write);
            var buffer = new byte[1024 * 1024];
            int count;
            while ((count = source.Read(buffer, 0, buffer.Length)) > 0)
                await destination.WriteAsync(buffer.AsMemory(0, count));
            await destination.FlushAsync();
            if (destination.Length != source.Length || destination.Length == 0)
                throw new InvalidOperationException("Downloaded backup length mismatch or empty file.");
            source.CloseFile();
        }
        var exportLog = await File.ReadAllTextAsync(Path.Combine(folder, log));
        if (System.Text.RegularExpressions.Regex.IsMatch(exportLog, @"ORA-\d{5}"))
            throw new InvalidOperationException("Export log contains Oracle diagnostics; review it before applying repair.");
        await using var file = File.OpenRead(Path.Combine(folder, dump));
        var sha256 = Convert.ToHexString(await SHA256.HashDataAsync(file));
        await File.WriteAllTextAsync(Path.Combine(folder, "manifest.json"), JsonSerializer.Serialize(new
        {
            Schema = "DEV_TEMPLATE", CapturedUtc = DateTime.UtcNow, Dump = dump, Log = log,
            Bytes = file.Length, SHA256 = sha256, ExportState = state,
            Restore = "Oracle Data Pump IMPORT (DBMS_DATAPUMP or impdp), preferably into an isolated schema; review the export log before applying HR repair."
        }, new JsonSerializerOptions { WriteIndented = true }));
        report.Add("Local DEV_TEMPLATE backup saved: " + folder);
        report.Add("Dump length and SHA-256 recorded; isolated restore rehearsal is still required.");
    }
}
