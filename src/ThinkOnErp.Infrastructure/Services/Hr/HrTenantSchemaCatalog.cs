using System.Text.RegularExpressions;
using System.Data.Common;
using Oracle.ManagedDataAccess.Client;

namespace ThinkOnErp.Infrastructure.Services.Hr;

internal sealed class HrTenantSchemaCatalog(string connectionString)
{
    private static readonly Regex OracleIdentifier = new("^[A-Za-z][A-Za-z0-9_$#]{0,127}$", RegexOptions.CultureInvariant | RegexOptions.Compiled);

    public string ConnectionString { get; } = connectionString;

    public string GetMasterSchema()
    {
        var schema = new OracleConnectionStringBuilder(ConnectionString).UserID?.Trim().Trim('"');
        if (string.IsNullOrWhiteSpace(schema) || !OracleIdentifier.IsMatch(schema))
            throw new InvalidOperationException("Oracle connection user is not a valid master schema identifier.");
        return schema;
    }

    public static bool IsValidSchema(string schema) => OracleIdentifier.IsMatch(schema);

    public async Task<IReadOnlyList<(long CompanyId, string Schema)>> GetActiveTenantSchemasAsync(CancellationToken cancellationToken)
    {
        var masterSchema = GetMasterSchema().ToUpperInvariant();
        var tenants = new List<(long CompanyId, string Schema)>();
        var schemaOwners = new List<(long CompanyId, string Schema)>();
        await using var connection = new OracleConnection(ConnectionString);
        await connection.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.BindByName = true;
        command.CommandText = $"SELECT \"Id\", COMPANY_SCHEMA, TO_CHAR(IS_ACTIVE) FROM \"{masterSchema}\".\"SYS_COMPANY\" ORDER BY \"Id\"";
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            var companyId = Convert.ToInt64(reader.GetValue(0));
            var activeValue = reader.IsDBNull(2)
                ? string.Empty
                : Convert.ToString(reader.GetValue(2), System.Globalization.CultureInfo.InvariantCulture)?.Trim() ?? string.Empty;
            var active = activeValue == "1" || string.Equals(activeValue, "Y", StringComparison.OrdinalIgnoreCase);
            if (reader.IsDBNull(1) || string.IsNullOrWhiteSpace(reader.GetString(1)))
            {
                if (active)
                    throw new InvalidOperationException($"Active company {companyId} has no tenant schema; scheduled HR processing was stopped.");
                continue;
            }

            var normalizedSchema = reader.GetString(1).Trim().Trim('"').ToUpperInvariant();
            schemaOwners.Add((companyId, normalizedSchema));
            if (active) tenants.Add((companyId, normalizedSchema));
        }

        if (tenants.Any(x => string.Equals(x.Schema, masterSchema, StringComparison.OrdinalIgnoreCase)))
            throw new InvalidOperationException("An active company maps to the central registry schema; scheduled HR processing was stopped.");

        var activeSchemas = tenants.Select(tenant => tenant.Schema).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var duplicateSchema = schemaOwners.GroupBy(x => x.Schema, StringComparer.OrdinalIgnoreCase)
            .FirstOrDefault(group => activeSchemas.Contains(group.Key) && group.Count() > 1);
        if (duplicateSchema != null)
            throw new InvalidOperationException("Multiple company registry records map to one tenant schema; scheduled HR processing was stopped.");

        return tenants.ToArray();
    }

    public static async Task SetCurrentSchemaAsync(DbConnection connection, string schema, CancellationToken cancellationToken)
    {
        if (!OracleIdentifier.IsMatch(schema)) throw new InvalidOperationException("Invalid Oracle schema identifier.");
        await using var command = connection.CreateCommand();
        command.CommandText = $"ALTER SESSION SET CURRENT_SCHEMA = \"{schema.ToUpperInvariant()}\"";
        await command.ExecuteNonQueryAsync(cancellationToken);
    }
}
