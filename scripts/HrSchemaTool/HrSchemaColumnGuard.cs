using Oracle.ManagedDataAccess.Client;

internal sealed record HrSchemaColumnExpectation(
    string Table,
    string Column,
    string DataType,
    string Nullable,
    int? CharacterLength = null,
    int? Precision = null,
    int? Scale = null,
    bool Additive = false,
    string? DefaultExpression = null,
    bool ExactCharacterLength = false,
    int? DataLength = null,
    string? CharacterSemantics = null);

internal static class HrSchemaColumnGuard
{
    public static async Task ValidateAsync(
        OracleConnection connection,
        IReadOnlyCollection<HrSchemaColumnExpectation> expectations,
        bool allowMissingColumns,
        IReadOnlySet<string>? allowMissingTables = null)
    {
        var missingTables = new HashSet<string>(StringComparer.Ordinal);
        foreach (var table in expectations.Select(x => x.Table).Distinct(StringComparer.Ordinal))
        {
            await using var tableCommand = connection.CreateCommand();
            tableCommand.CommandText = "SELECT COUNT(*) FROM ALL_TABLES WHERE OWNER='DEV_TEMPLATE' AND TABLE_NAME=:tableName";
            tableCommand.Parameters.Add(new OracleParameter("tableName", table));
            tableCommand.CommandTimeout = 15;
            if (Convert.ToInt32(await tableCommand.ExecuteScalarAsync()) != 1)
            {
                if (allowMissingTables?.Contains(table) == true)
                {
                    missingTables.Add(table);
                    continue;
                }
                throw new InvalidOperationException($"DEV_TEMPLATE.{table} is missing; no installer DDL was applied.");
            }
        }

        foreach (var expectation in expectations)
        {
            if (missingTables.Contains(expectation.Table)) continue;
            await using var command = connection.CreateCommand();
            command.CommandText = """
                SELECT DATA_TYPE, CHAR_LENGTH, DATA_LENGTH, CHAR_USED, DATA_PRECISION, DATA_SCALE, NULLABLE,
                       NVL(DEFAULT_LENGTH, 0), VIRTUAL_COLUMN, DATA_DEFAULT
                FROM ALL_TAB_COLS
                WHERE OWNER='DEV_TEMPLATE' AND TABLE_NAME=:tableName AND COLUMN_NAME=:columnName
                """;
            command.Parameters.Add(new OracleParameter("tableName", expectation.Table));
            command.Parameters.Add(new OracleParameter("columnName", expectation.Column));
            command.CommandTimeout = 15;
            command.InitialLONGFetchSize = -1;
            OracleDataReader reader;
            try
            {
                reader = await command.ExecuteReaderAsync();
            }
            catch (OracleException ex) when (ex.Number == 904)
            {
                // Oracle releases predating virtual columns do not expose VIRTUAL_COLUMN.
                // On those releases a column cannot be virtual, so keep all other metadata checks.
                command.CommandText = """
                    SELECT DATA_TYPE, CHAR_LENGTH, DATA_LENGTH, CHAR_USED, DATA_PRECISION, DATA_SCALE, NULLABLE,
                           NVL(DEFAULT_LENGTH, 0), 'NO' AS VIRTUAL_COLUMN, DATA_DEFAULT
                    FROM ALL_TAB_COLUMNS
                    WHERE OWNER='DEV_TEMPLATE' AND TABLE_NAME=:tableName AND COLUMN_NAME=:columnName
                    """;
                reader = await command.ExecuteReaderAsync();
            }
            await using (reader)
            {
                if (!await reader.ReadAsync())
                {
                    if (allowMissingColumns && expectation.Additive) continue;
                    throw new InvalidOperationException($"DEV_TEMPLATE.{expectation.Table}.{expectation.Column} is missing after installer DDL.");
                }

                var type = reader.GetString(0).ToUpperInvariant();
                var charLength = reader.IsDBNull(1) ? (int?)null : Convert.ToInt32(reader.GetValue(1));
                var dataLength = reader.IsDBNull(2) ? (int?)null : Convert.ToInt32(reader.GetValue(2));
                var characterSemantics = reader.IsDBNull(3) ? null : reader.GetString(3).ToUpperInvariant();
                var precision = reader.IsDBNull(4) ? (int?)null : Convert.ToInt32(reader.GetValue(4));
                var scale = reader.IsDBNull(5) ? (int?)null : Convert.ToInt32(reader.GetValue(5));
                var nullable = reader.GetString(6).ToUpperInvariant();
                var defaultLength = Convert.ToInt32(reader.GetValue(7));
                var virtualColumn = reader.GetString(8).ToUpperInvariant();
                var defaultExpression = reader.IsDBNull(9) ? null : reader.GetString(9);
                var defaultMatches = expectation.DefaultExpression is null
                    ? defaultLength == 0
                    : defaultLength > 0 && NormalizeDefault(defaultExpression) == NormalizeDefault(expectation.DefaultExpression);

                if (!string.Equals(type, expectation.DataType, StringComparison.OrdinalIgnoreCase) ||
                    expectation.CharacterLength.HasValue && (!charLength.HasValue ||
                        (expectation.ExactCharacterLength ? charLength.Value != expectation.CharacterLength.Value : charLength.Value < expectation.CharacterLength.Value)) ||
                    expectation.DataLength.HasValue && (!dataLength.HasValue || dataLength.Value < expectation.DataLength.Value) ||
                    expectation.CharacterSemantics is not null && !string.Equals(characterSemantics, expectation.CharacterSemantics, StringComparison.OrdinalIgnoreCase) ||
                    expectation.Precision.HasValue && precision != expectation.Precision ||
                    expectation.Scale.HasValue && scale != expectation.Scale ||
                    expectation.Nullable != "*" && !string.Equals(nullable, expectation.Nullable, StringComparison.OrdinalIgnoreCase) ||
                    !defaultMatches || virtualColumn != "NO")
                    throw new InvalidOperationException($"DEV_TEMPLATE.{expectation.Table}.{expectation.Column} has an incompatible definition; no further installer DDL should run.");
            }
        }
    }

    internal static string NormalizeDefault(string? value)
        => new((value ?? string.Empty).Where(char.IsLetterOrDigit).Select(char.ToUpperInvariant).ToArray());
}
