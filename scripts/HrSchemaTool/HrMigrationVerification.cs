using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Oracle.ManagedDataAccess.Client;

// A comparison manifest, NOT a backup: stores schema names/counts/hashes only.
internal static class HrMigrationVerification
{
    internal sealed record TableSnapshot(string Table, string[] Columns, long Rows, string Fingerprint);
    internal sealed record Manifest(string Schema, DateTime CapturedUtc, List<TableSnapshot> Tables);

    public static async Task Run(OracleConnection connection, string root, bool verify, List<string> report)
    {
        var folder = Path.Combine(root, "artifacts", "hr-audit");
        Directory.CreateDirectory(folder);
        var path = Path.Combine(folder, "hr-migration-baseline.json");
        if (!verify && File.Exists(path))
            throw new InvalidOperationException("Baseline already exists; archive it explicitly before capturing a new one.");

        // Consistent multi-table reads. Disposal rolls back this read-only transaction.
        using var transaction = connection.BeginTransaction();
        await using (var begin = connection.CreateCommand())
        {
            begin.CommandText = "SET TRANSACTION READ ONLY";
            await begin.ExecuteNonQueryAsync();
        }
        if (!verify)
        {
            var columns = new Dictionary<string, List<string>>(StringComparer.Ordinal);
            await using (var command = connection.CreateCommand())
            {
                command.CommandText = "SELECT TABLE_NAME,COLUMN_NAME FROM ALL_TAB_COLUMNS WHERE OWNER='DEV_TEMPLATE' AND TABLE_NAME LIKE 'HR\\_%' ESCAPE '\\' ORDER BY TABLE_NAME,COLUMN_ID";
                await using var reader = await command.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    var table = reader.GetString(0);
                    if (!columns.ContainsKey(table)) columns[table] = new();
                    columns[table].Add(reader.GetString(1));
                }
            }
            if (columns.Count == 0) throw new InvalidOperationException("No HR tables to capture.");
            var tables = new List<TableSnapshot>();
            foreach (var table in columns)
                tables.Add(await Capture(connection, table.Key, table.Value.ToArray()));
            var manifest = new Manifest("DEV_TEMPLATE", DateTime.UtcNow, tables);
            await using var file = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None);
            await JsonSerializer.SerializeAsync(file, manifest, new JsonSerializerOptions { WriteIndented = true });
            report.Add($"Captured comparison baseline: {tables.Count} HR tables; no personnel values stored. This is not a restorable backup.");
            return;
        }

        var baseline = JsonSerializer.Deserialize<Manifest>(await File.ReadAllTextAsync(path))
            ?? throw new InvalidOperationException("Missing migration manifest.");
        if (baseline.Schema != "DEV_TEMPLATE" || baseline.Tables.Count == 0)
            throw new InvalidOperationException("Invalid migration manifest scope.");
        var failures = 0;
        foreach (var expected in baseline.Tables)
        {
            var actual = await Capture(connection, expected.Table, expected.Columns);
            var same = actual.Rows == expected.Rows && actual.Fingerprint == expected.Fingerprint;
            if (!same) failures++;
            report.Add($"Migration preservation {expected.Table}: {(same ? "PASS" : "FAIL")}; rows before={expected.Rows}, after={actual.Rows}.");
        }
        report.Add($"Original-column preservation: {baseline.Tables.Count - failures} passed, {failures} failed. New columns/tables and constraints require separate preflight/schema review.");
        if (failures != 0) Environment.ExitCode = 1;
    }

    private static string Quote(string name)
    {
        if (!Regex.IsMatch(name, "^[A-Z][A-Z0-9_]*$"))
            throw new InvalidOperationException("Unsupported identifier in migration manifest.");
        return "\"" + name + "\"";
    }

    private static async Task<TableSnapshot> Capture(OracleConnection connection, string table, string[] columns)
    {
        if (!table.StartsWith("HR_", StringComparison.Ordinal) || columns.Length == 0)
            throw new InvalidOperationException("Invalid migration table.");
        await using var command = connection.CreateCommand();
        command.CommandText = $"SELECT {string.Join(",", columns.Select(Quote))} FROM DEV_TEMPLATE.{Quote(table)}";
        command.CommandTimeout = 60;
        await using var reader = await command.ExecuteReaderAsync();
        var hashes = new List<string>();
        while (await reader.ReadAsync())
        {
            using var buffer = new MemoryStream();
            using (var writer = new BinaryWriter(buffer, Encoding.UTF8, leaveOpen: true))
                for (var i = 0; i < reader.FieldCount; i++)
                {
                    writer.Write(!reader.IsDBNull(i));
                    if (reader.IsDBNull(i)) continue;
                    // Length-prefixed values preserve null/empty, boundaries and row multiplicity.
                    var value = reader.GetValue(i);
                    var text = value switch
                    {
                        byte[] bytes => Convert.ToHexString(bytes),
                        DateTime date => date.ToString("O", CultureInfo.InvariantCulture),
                        DateTimeOffset date => date.ToString("O", CultureInfo.InvariantCulture),
                        IFormattable formatted => formatted.ToString(null, CultureInfo.InvariantCulture),
                        string str => str,
                        _ => throw new InvalidOperationException("Unsupported baseline value type.")
                    };
                    writer.Write(text ?? string.Empty);
                }
            hashes.Add(Convert.ToHexString(SHA256.HashData(buffer.ToArray())));
        }
        hashes.Sort(StringComparer.Ordinal);
        using var aggregate = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        foreach (var hash in hashes) aggregate.AppendData(Convert.FromHexString(hash));
        return new TableSnapshot(table, columns, hashes.Count, Convert.ToHexString(aggregate.GetHashAndReset()));
    }
}
