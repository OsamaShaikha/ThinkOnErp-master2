using System.Data;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Oracle.ManagedDataAccess.Client;
using ThinkOnErp.Domain.Models;
using ThinkOnErp.Infrastructure.Data;

namespace ThinkOnErp.API.Middleware;

public interface ITenantSchemaResolver
{
    Task<TenantRequestContext?> ResolveAsync(
        long? companyId,
        string? companyCode,
        string? expectedSchema,
        CancellationToken cancellationToken);
}

public interface ITenantSchemaSession
{
    Task RunAsync(
        OracleDbContext dbContext,
        string schema,
        CancellationToken cancellationToken,
        Func<Task> next);
}

public static class OracleSchemaIdentifier
{
    private static readonly Regex Pattern = new(
        "^[A-Za-z][A-Za-z0-9_$#]{0,127}$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    public static bool IsValid(string? value) =>
        !string.IsNullOrWhiteSpace(value) && Pattern.IsMatch(value);
}

public sealed class OracleTenantSchemaResolver : ITenantSchemaResolver
{
    private readonly string _connectionString;
    private readonly string _masterSchema;
    private readonly ILogger<OracleTenantSchemaResolver> _logger;

    public OracleTenantSchemaResolver(IConfiguration configuration, ILogger<OracleTenantSchemaResolver> logger)
    {
        _connectionString = configuration.GetConnectionString("OracleDb")
            ?? throw new InvalidOperationException("Connection string 'OracleDb' not found.");

        var connectionBuilder = new OracleConnectionStringBuilder(_connectionString);
        _masterSchema = connectionBuilder.UserID?.Trim().Trim('"')
            ?? throw new InvalidOperationException("Oracle connection user is not configured.");

        if (!OracleSchemaIdentifier.IsValid(_masterSchema))
            throw new InvalidOperationException("Oracle connection user is not a valid schema identifier.");

        _logger = logger;
    }

    public async Task<TenantRequestContext?> ResolveAsync(
        long? companyId,
        string? companyCode,
        string? expectedSchema,
        CancellationToken cancellationToken)
    {
        if (companyId is <= 0 || !companyId.HasValue && string.IsNullOrWhiteSpace(companyCode))
            return null;

        await using var connection = new OracleConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        try
        {
            await SetCurrentSchemaAsync(connection, _masterSchema, cancellationToken);

            await using var command = connection.CreateCommand();
            command.BindByName = true;
            command.CommandText = $"""
                SELECT c."Id", c.COMPANY_CODE, c.COMPANY_SCHEMA,
                       (SELECT COUNT(1)
                        FROM "{_masterSchema.ToUpperInvariant()}"."SYS_COMPANY" registry
                        WHERE UPPER(TRIM(registry.COMPANY_SCHEMA)) = UPPER(TRIM(c.COMPANY_SCHEMA))) AS SCHEMA_OWNER_COUNT
                FROM "{_masterSchema.ToUpperInvariant()}"."SYS_COMPANY" c
                WHERE (TO_CHAR(c.IS_ACTIVE) = '1' OR UPPER(TO_CHAR(c.IS_ACTIVE)) = 'Y')
                  AND c.COMPANY_SCHEMA IS NOT NULL
                """;

            if (companyId.HasValue)
            {
                command.CommandText += "\n  AND \"Id\" = :companyId";
                command.Parameters.Add("companyId", OracleDbType.Int64).Value = companyId.Value;
            }

            if (!string.IsNullOrWhiteSpace(companyCode))
            {
                command.CommandText += "\n  AND UPPER(TRIM(COMPANY_CODE)) = UPPER(:companyCode)";
                command.Parameters.Add("companyCode", OracleDbType.Varchar2).Value = companyCode.Trim();
            }

            if (!string.IsNullOrWhiteSpace(expectedSchema))
            {
                command.CommandText += "\n  AND UPPER(TRIM(COMPANY_SCHEMA)) = UPPER(:expectedSchema)";
                command.Parameters.Add("expectedSchema", OracleDbType.Varchar2).Value = expectedSchema.Trim();
            }

            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            if (!await reader.ReadAsync(cancellationToken))
                return null;

            var resolvedCompanyId = Convert.ToInt64(reader.GetValue(0));
            if (Convert.ToInt32(reader.GetValue(3)) != 1)
            {
                _logger.LogError("Company {CompanyId} tenant schema is assigned to multiple registry records; refusing tenant routing", resolvedCompanyId);
                return null;
            }

            if (reader.IsDBNull(1))
            {
                _logger.LogError("Active company tenant schema is missing a company code; refusing tenant routing");
                return null;
            }

            var resolvedCompanyCode = reader.GetString(1).Trim();
            if (resolvedCompanyCode.Length == 0)
            {
                _logger.LogError("Active company tenant schema has an empty company code; refusing tenant routing");
                return null;
            }

            var resolvedSchema = reader.GetString(2).Trim().Trim('"');
            if (await reader.ReadAsync(cancellationToken))
            {
                _logger.LogError("Company selector resolved to multiple active tenant schemas; refusing tenant routing");
                return null;
            }

            if (!OracleSchemaIdentifier.IsValid(resolvedSchema))
            {
                _logger.LogError("Company {CompanyId} has an invalid tenant schema identifier", resolvedCompanyId);
                return null;
            }

            if (string.Equals(resolvedSchema.Trim(), _masterSchema, StringComparison.OrdinalIgnoreCase))
            {
                _logger.LogError("Company {CompanyId} resolves to the central registry schema; refusing tenant routing", resolvedCompanyId);
                return null;
            }

            return new TenantRequestContext(resolvedCompanyId, resolvedCompanyCode, resolvedSchema.ToUpperInvariant());
        }
        finally
        {
            if (connection.State == ConnectionState.Open)
            {
                try
                {
                    await SetCurrentSchemaAsync(connection, _masterSchema, CancellationToken.None);
                }
                catch (Exception ex)
                {
                    _logger.LogCritical(ex, "Failed to reset the registry lookup connection to the master schema");
                    OracleConnection.ClearPool(connection);
                }
            }
        }
    }

    private static async Task SetCurrentSchemaAsync(
        OracleConnection connection,
        string schema,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = OracleTenantSchemaSession.BuildSetCurrentSchemaStatement(schema);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }
}

public sealed class OracleTenantSchemaSession : ITenantSchemaSession
{
    private readonly string _masterSchema;
    private readonly ILogger<OracleTenantSchemaSession> _logger;
    private readonly Func<System.Data.Common.DbConnection, string, CancellationToken, Task> _setCurrentSchema;

    public OracleTenantSchemaSession(
        IConfiguration configuration,
        ILogger<OracleTenantSchemaSession> logger,
        Func<System.Data.Common.DbConnection, string, CancellationToken, Task>? setCurrentSchema = null)
    {
        var connectionString = configuration.GetConnectionString("OracleDb")
            ?? throw new InvalidOperationException("Connection string 'OracleDb' not found.");
        var connectionBuilder = new OracleConnectionStringBuilder(connectionString);
        _masterSchema = connectionBuilder.UserID?.Trim().Trim('"')
            ?? throw new InvalidOperationException("Oracle connection user is not configured.");
        if (!OracleSchemaIdentifier.IsValid(_masterSchema))
            throw new InvalidOperationException("Oracle connection user is not a valid schema identifier.");
        _logger = logger;
        _setCurrentSchema = setCurrentSchema ?? SetCurrentSchemaAsync;
    }

    public static string BuildSetCurrentSchemaStatement(string schema)
    {
        if (!OracleSchemaIdentifier.IsValid(schema))
            throw new InvalidOperationException("Invalid Oracle schema identifier.");

        return $"ALTER SESSION SET CURRENT_SCHEMA = \"{schema.ToUpperInvariant()}\"";
    }

    public async Task RunAsync(
        OracleDbContext dbContext,
        string schema,
        CancellationToken cancellationToken,
        Func<Task> next)
    {
        var connection = dbContext.Database.GetDbConnection();
        var connectionOpenedHere = connection.State != ConnectionState.Open;
        Exception? operationError = null;
        try
        {
            if (connectionOpenedHere)
                await dbContext.Database.OpenConnectionAsync(cancellationToken);

            await _setCurrentSchema(connection, schema, cancellationToken);
            await next();
        }
        catch (Exception ex)
        {
            operationError = ex;
            throw;
        }
        finally
        {
            Exception? cleanupError = null;
            try
            {
                if (connection.State == ConnectionState.Open)
                    await _setCurrentSchema(connection, _masterSchema, CancellationToken.None);
            }
            catch (Exception ex)
            {
                _logger.LogCritical(ex, "Failed to reset Oracle CURRENT_SCHEMA before returning the connection to the pool");
                if (connection is OracleConnection oracleConnection)
                {
                    try
                    {
                        OracleConnection.ClearPool(oracleConnection);
                    }
                    catch (Exception clearPoolError)
                    {
                        cleanupError = new AggregateException("Failed to reset Oracle CURRENT_SCHEMA and clear its connection pool.", ex, clearPoolError);
                    }
                }

                cleanupError ??= ex;
            }

            if (connectionOpenedHere && connection.State == ConnectionState.Open)
            {
                try
                {
                    await dbContext.Database.CloseConnectionAsync();
                }
                catch (Exception closeError)
                {
                    cleanupError = cleanupError == null
                        ? closeError
                        : new AggregateException("Oracle tenant session cleanup failed.", cleanupError, closeError);
                }
            }

            // A session whose schema could not be restored must never remain open
            // for a later request, even when its connection was opened by a caller.
            if (cleanupError != null && connection.State == ConnectionState.Open)
            {
                try
                {
                    await connection.CloseAsync();
                }
                catch (Exception closeError)
                {
                    cleanupError = new AggregateException("Oracle tenant connection could not be closed after schema restoration failed.", cleanupError, closeError);
                }
            }

            if (cleanupError != null)
            {
                if (operationError != null)
                    throw new AggregateException("The tenant operation and Oracle schema-session cleanup both failed.", operationError, cleanupError);

                throw new InvalidOperationException("Oracle CURRENT_SCHEMA could not be safely restored; the tenant request was rejected.", cleanupError);
            }
        }
    }

    private static async Task SetCurrentSchemaAsync(
        System.Data.Common.DbConnection connection,
        string schema,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = BuildSetCurrentSchemaStatement(schema);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }
}
