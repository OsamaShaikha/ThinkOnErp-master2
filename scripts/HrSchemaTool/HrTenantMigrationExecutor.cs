using System.Security.Cryptography;
using System.Text.Json;
using System.Text.RegularExpressions;
using Oracle.ManagedDataAccess.Client;

internal static class HrTenantMigrationExecutor
{
    private const string ScriptName = "hr-tenant-migrations-41-offline.sql";
    private const int ExpectedMigrationCount = 41;
    private const int ExpectedBlockCount = 127;

    private sealed record BackupManifest(string Schema, DateTime CapturedUtc, string Dump, string Log, long Bytes, string SHA256, string ExportState);

    public static async Task Apply(OracleConnection connection, string root, List<string> report)
    {
        var session = await Query(connection, "SELECT SYS_CONTEXT('USERENV','SESSION_USER'),SYS_CONTEXT('USERENV','CURRENT_SCHEMA') FROM DUAL");
        if (session.Count != 1 || session[0][1] != "DEV_TEMPLATE")
            throw new InvalidOperationException("Tenant migration application requires CURRENT_SCHEMA=DEV_TEMPLATE.");

        var backupRoot = Path.Combine(root, "artifacts", "hr-audit", "backups");
        var backupDirectory = Directory.GetDirectories(backupRoot)
            .Where(path => Regex.IsMatch(Path.GetFileName(path), @"^\d{14}$"))
            .OrderByDescending(Path.GetFileName, StringComparer.Ordinal)
            .FirstOrDefault()
            ?? throw new InvalidOperationException("A fresh DEV_TEMPLATE backup is required before migration DDL.");
        var manifestPath = Path.Combine(backupDirectory, "manifest.json");
        var manifest = JsonSerializer.Deserialize<BackupManifest>(await File.ReadAllTextAsync(manifestPath))
            ?? throw new InvalidOperationException("The approved DEV_TEMPLATE backup manifest is invalid.");
        if (manifest.Schema != "DEV_TEMPLATE" || manifest.ExportState != "COMPLETED" || manifest.Bytes <= 0)
            throw new InvalidOperationException("A completed DEV_TEMPLATE Data Pump backup is required before migration DDL.");
        var dumpPath = Path.Combine(backupDirectory, manifest.Dump);
        var logPath = Path.Combine(backupDirectory, manifest.Log);
        var dumpInfo = new FileInfo(dumpPath);
        if (!dumpInfo.Exists || dumpInfo.Length != manifest.Bytes)
            throw new InvalidOperationException("The DEV_TEMPLATE backup size or SHA-256 does not match its manifest.");
        await using (var dumpStream = File.OpenRead(dumpPath))
        {
            var actualHash = Convert.ToHexString(await SHA256.HashDataAsync(dumpStream));
            if (!string.Equals(actualHash, manifest.SHA256, StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("The DEV_TEMPLATE backup size or SHA-256 does not match its manifest.");
        }
        if (!File.Exists(logPath) || (await File.ReadAllTextAsync(logPath)).Contains("ORA-", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("The DEV_TEMPLATE backup log is missing or contains an Oracle error.");

        var scriptPath = Path.Combine(root, "artifacts", "hr-audit", ScriptName);
        var script = await File.ReadAllTextAsync(scriptPath);
        var targetIds = Regex.Matches(script, "WHERE\\s+\"MigrationId\"\\s*=\\s*N'([^']+)'", RegexOptions.IgnoreCase)
            .Select(match => match.Groups[1].Value).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray();
        var sourceMigrations = Directory.GetFiles(Path.Combine(root, "src", "ThinkOnErp.Infrastructure", "Migrations"), "20*.cs")
            .Select(path => (Path: path, Match: Regex.Match(File.ReadAllText(path), "\\[Migration\\(\"([^\"]+)\"\\)\\]")))
            .Where(item => item.Match.Success)
            .Select(item => item.Match.Groups[1].Value)
            .Where(id => id.Length >= 14 && string.CompareOrdinal(id[..14], "20261113183000") >= 0 && string.CompareOrdinal(id[..14], "20261115002400") <= 0)
            .Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray();
        if (targetIds.Length != ExpectedMigrationCount || !targetIds.SequenceEqual(sourceMigrations, StringComparer.Ordinal))
            throw new InvalidOperationException($"Migration SQL/source mismatch: SQL={targetIds.Length}, source={sourceMigrations.Length}; refusing DDL.");
        if (File.GetLastWriteTimeUtc(scriptPath) > manifest.CapturedUtc)
            throw new InvalidOperationException("The SQL script is newer than the approved backup; take a fresh backup before applying.");

        var blocks = Regex.Split(script, "(?m)^\\s*/\\s*$")
            .Select(block => Regex.Replace(block, "(?m)^\\s*--.*(?:\\r?\\n|$)", string.Empty).Trim())
            .Where(block => block.Length > 0).ToArray();
        if (blocks.Length != ExpectedBlockCount || blocks.Any(block => !Regex.IsMatch(block, "^(DECLARE|BEGIN)\\b", RegexOptions.IgnoreCase)))
            throw new InvalidOperationException($"Unexpected EF SQL block layout ({blocks.Length}); refusing DDL.");

        var centralColumns = await Query(connection, "SELECT COLUMN_NAME,DATA_TYPE,CHAR_LENGTH,NULLABLE FROM ALL_TAB_COLUMNS WHERE OWNER='THINKON_ERP' AND TABLE_NAME='__EFMigrationsHistory' ORDER BY COLUMN_ID");
        var expectedColumns = new[] { "MigrationId|NVARCHAR2|150|N", "ProductVersion|NVARCHAR2|32|N" };
        if (!centralColumns.Select(column => string.Join("|", column)).SequenceEqual(expectedColumns, StringComparer.Ordinal))
            throw new InvalidOperationException("The central EF history table does not match the expected two-column Oracle layout.");

        var targetTable = await Query(connection, "SELECT COUNT(*) FROM ALL_TABLES WHERE OWNER='DEV_TEMPLATE' AND TABLE_NAME='__EFMigrationsHistory'");
        if (targetTable[0][0] == "0")
        {
            await Execute(connection, "CREATE TABLE \"__EFMigrationsHistory\" (\"MigrationId\" NVARCHAR2(150) NOT NULL, \"ProductVersion\" NVARCHAR2(32) NOT NULL, CONSTRAINT \"PK___EFMigrationsHistory\" PRIMARY KEY (\"MigrationId\"))");
            report.Add("Created DEV_TEMPLATE-local EF migration history after validating the completed backup and central history column layout.");
        }

        var completed = new HashSet<string>((await Query(connection, "SELECT \"MigrationId\" FROM DEV_TEMPLATE.\"__EFMigrationsHistory\"")).Select(row => row[0]), StringComparer.Ordinal);
        var blockIndex = 0;
        try
        {
            foreach (var block in blocks)
            {
                blockIndex++;
                await Execute(connection, block, 300);
                if (blockIndex % 10 == 0 || blockIndex == blocks.Length)
                    Console.WriteLine($"Applied SQL block {blockIndex}/{blocks.Length}.");
            }
        }
        catch (Exception exception)
        {
            throw new InvalidOperationException($"DEV_TEMPLATE migration application stopped at SQL block {blockIndex}/{blocks.Length}; prior Oracle DDL may be committed, and the idempotent script can be reviewed before resume.", exception);
        }

        var appliedIds = (await Query(connection, "SELECT \"MigrationId\" FROM DEV_TEMPLATE.\"__EFMigrationsHistory\" ORDER BY \"MigrationId\"")).Select(row => row[0]).ToHashSet(StringComparer.Ordinal);
        var missing = targetIds.Where(id => !appliedIds.Contains(id)).ToArray();
        if (missing.Length != 0)
            throw new InvalidOperationException("Migration SQL completed but expected history rows are missing: " + string.Join(", ", missing));
        report.Add($"DEV_TEMPLATE migration batch complete: {targetIds.Length} source-matched IDs; previously recorded in target: {completed.Count(id => targetIds.Contains(id))}.");
    }

    private static async Task<List<string[]>> Query(OracleConnection connection, string sql)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        command.CommandTimeout = 60;
        await using var reader = await command.ExecuteReaderAsync();
        var rows = new List<string[]>();
        while (await reader.ReadAsync())
            rows.Add(Enumerable.Range(0, reader.FieldCount).Select(index => reader.IsDBNull(index) ? "NULL" : reader.GetValue(index).ToString()!).ToArray());
        return rows;
    }

    private static async Task Execute(OracleConnection connection, string sql, int timeoutSeconds = 60)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        command.CommandTimeout = timeoutSeconds;
        await command.ExecuteNonQueryAsync();
    }
}
