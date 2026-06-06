using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Oracle.ManagedDataAccess.Client;
using ThinkOnErp.Domain.Interfaces;
using ThinkOnErp.Infrastructure.Data;

namespace ThinkOnErp.Infrastructure.Services;

public class OracleSchemaService : IOracleSchemaService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<OracleSchemaService> _logger;
    private readonly string _masterConnectionString;

    private static readonly HashSet<string> GlobalTables = new(StringComparer.OrdinalIgnoreCase)
    {
        "SYS_SUPER_ADMIN", "SYS_COMPANY", "SYS_SYSTEM", "SYS_SCREEN",
        "SYS_CURRENCY", "SYS_SETTING", "SYS_CODE",
        "SYS_AUDIT_LOG", "SYS_AUDIT_LOG_ARCHIVE", "SYS_RETENTION_POLICIES",
        "__EFMigrationsHistory"
    };

    public OracleSchemaService(
        IServiceScopeFactory scopeFactory,
        IConfiguration configuration,
        ILogger<OracleSchemaService> logger)
    {
        _scopeFactory = scopeFactory;
        _masterConnectionString = configuration.GetConnectionString("OracleDb")
            ?? throw new InvalidOperationException("Connection string 'OracleDb' not found.");
        _logger = logger;
    }

    public async Task CreateCompanySchemaAsync(string schemaName, string password)
    {
        schemaName = schemaName.ToUpperInvariant();
        _logger.LogInformation("Creating Oracle schema: {SchemaName}", schemaName);

        await using var masterConn = new OracleConnection(_masterConnectionString);
        await masterConn.OpenAsync();

        // Enable Oracle 12c+ script mode to allow regular usernames in CDB
        await ExecuteRawAsync(masterConn, "ALTER SESSION SET \"_ORACLE_SCRIPT\" = TRUE");

        // 1. Create the Oracle user (schema)
        try
        {
            await ExecuteRawAsync(masterConn, $"CREATE USER \"{schemaName}\" IDENTIFIED BY \"{password}\"");
            _logger.LogInformation("User created: {SchemaName}", schemaName);
        }
        catch (OracleException ex) when (ex.Number == 1920)
        {
            _logger.LogWarning("Schema already exists: {SchemaName}", schemaName);
            // Schema exists — don't drop, just re-grant privileges. Tables already exist.
        }

        // 2. Grant necessary privileges
        await ExecuteRawAsync(masterConn,
            $"GRANT CONNECT, RESOURCE, CREATE SESSION, CREATE TABLE, CREATE VIEW, CREATE SEQUENCE, CREATE PROCEDURE, CREATE TRIGGER, CREATE SYNONYM TO \"{schemaName}\"");
        _logger.LogInformation("Privileges granted to: {SchemaName}", schemaName);

        // 3. Create tenant tables
        await CreateTenantTablesAsync(masterConn, schemaName);

        _logger.LogInformation("Schema creation completed: {SchemaName}", schemaName);
    }

    private async Task CreateTenantTablesAsync(OracleConnection connection, string schemaName)
    {
        using var scope = _scopeFactory.CreateScope();
        await using var context = scope.ServiceProvider.GetRequiredService<OracleDbContext>();
        var model = context.Model;

        foreach (var entityType in model.GetEntityTypes())
        {
            var tableName = entityType.GetTableName();
            if (string.IsNullOrEmpty(tableName) || GlobalTables.Contains(tableName))
                continue;

            var createSql = GenerateCreateTableSql(entityType, schemaName, tableName);
            _logger.LogDebug("Creating table {SchemaName}.{TableName}", schemaName, tableName);
            await ExecuteRawAsync(connection, createSql);
        }
    }

    private static string GenerateCreateTableSql(IEntityType entityType, string schemaName, string tableName)
    {
        var columns = new List<string>();
        var primaryKeys = new List<string>();

        foreach (var prop in entityType.GetProperties())
        {
            var storeId = StoreObjectIdentifier.Table(tableName);
            var colName = prop.GetColumnName(storeId);
            var colType = prop.GetColumnType();

            if (string.IsNullOrEmpty(colType))
                colType = GetOracleType(prop);

            var nullable = prop.IsNullable ? "NULL" : "NOT NULL";
            var colDef = $"    \"{colName}\" {colType} {nullable}";

            columns.Add(colDef);

            if (prop.IsPrimaryKey())
                primaryKeys.Add($"\"{colName}\"");
        }

        var sql = $"CREATE TABLE \"{schemaName}\".\"{tableName}\"\n(\n";
        sql += string.Join(",\n", columns);

        if (primaryKeys.Count > 0)
        {
            sql += $",\n    PRIMARY KEY ({string.Join(", ", primaryKeys)})";
        }

        sql += "\n)";
        return sql;
    }

    private static string GetOracleType(IProperty prop)
    {
        var clrType = prop.ClrType;
        var maxLength = prop.GetMaxLength();

        if (clrType == typeof(bool)) return "NUMBER(1)";
        if (clrType == typeof(byte)) return "NUMBER(3)";
        if (clrType == typeof(short)) return "NUMBER(6)";
        if (clrType == typeof(int)) return "NUMBER(10)";
        if (clrType == typeof(long)) return "NUMBER(19)";
        if (clrType == typeof(float)) return "BINARY_FLOAT";
        if (clrType == typeof(double)) return "BINARY_DOUBLE";
        if (clrType == typeof(decimal)) return "NUMBER(18,2)";
        if (clrType == typeof(DateTime)) return "DATE";
        if (clrType == typeof(DateTimeOffset)) return "TIMESTAMP WITH TIME ZONE";
        if (clrType == typeof(TimeSpan)) return "INTERVAL DAY TO SECOND";
        if (clrType == typeof(Guid)) return "RAW(16)";
        if (clrType == typeof(string))
        {
            if (maxLength.HasValue && maxLength > 0 && maxLength <= 4000)
                return $"NVARCHAR2({maxLength})";
            if (maxLength > 4000)
                return "NCLOB";
            return "NVARCHAR2(2000)";
        }
        if (clrType == typeof(byte[]))
        {
            if (maxLength.HasValue && maxLength <= 2000)
                return $"RAW({maxLength})";
            return "BLOB";
        }

        return "NVARCHAR2(255)";
    }

    private static async Task ExecuteRawAsync(OracleConnection connection, string sql)
    {
        await using var cmd = connection.CreateCommand();
        cmd.CommandText = sql;
        await cmd.ExecuteNonQueryAsync();
    }
}
