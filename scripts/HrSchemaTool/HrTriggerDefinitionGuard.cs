using Oracle.ManagedDataAccess.Client;

internal static class HrTriggerDefinitionGuard
{
    public static async Task RequireBodyAsync(OracleConnection connection, string triggerName, string expectedBody)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT TEXT FROM ALL_SOURCE WHERE OWNER='DEV_TEMPLATE' AND TYPE='TRIGGER' AND NAME=:triggerName ORDER BY LINE";
        command.Parameters.Add(new OracleParameter("triggerName", triggerName));

        var source = new System.Text.StringBuilder();
        await using (var reader = await command.ExecuteReaderAsync())
            while (await reader.ReadAsync())
                source.Append(Convert.ToString(reader.GetValue(0)));

        var text = source.ToString();
        var upper = text.ToUpperInvariant();
        var start = upper.IndexOf("BEGIN", StringComparison.Ordinal);
        var end = upper.LastIndexOf("END;", StringComparison.Ordinal);
        if (start < 0 || end < start)
            throw new InvalidOperationException($"DEV_TEMPLATE.{triggerName} source body is unavailable; replacement was refused before DDL.");
        if (upper[..start].Contains("DECLARE", StringComparison.Ordinal))
            throw new InvalidOperationException($"DEV_TEMPLATE.{triggerName} has a declaration section; replacement was refused before DDL.");

        var actualBody = text[start..(end + 4)];
        if (!Normalize(actualBody).Equals(Normalize(expectedBody), StringComparison.Ordinal))
            throw new InvalidOperationException($"DEV_TEMPLATE.{triggerName} has an unexpected source body; replacement was refused before DDL.");
    }

    private static string Normalize(string value) => new(value.Where(c => !char.IsWhiteSpace(c) && c != '"').ToArray());
}
