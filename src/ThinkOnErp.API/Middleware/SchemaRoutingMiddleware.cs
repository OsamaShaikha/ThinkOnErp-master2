using System.Data;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Oracle.ManagedDataAccess.Client;
using ThinkOnErp.API.Authorization;
using ThinkOnErp.Domain.Models;
using ThinkOnErp.Infrastructure.Data;

namespace ThinkOnErp.API.Middleware;

/// <summary>
/// Resolves an authoritative company record and switches the current Oracle
/// session to that company's schema for tenant-scoped endpoints.
/// </summary>
public class SchemaRoutingMiddleware
{
    public const string TenantSchemaContextItem = "ThinkOnErp.TenantSchema";

    private static readonly Regex OracleIdentifierPattern = new(
        "^[A-Za-z][A-Za-z0-9_$#]{0,127}$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private readonly RequestDelegate _next;
    private readonly ILogger<SchemaRoutingMiddleware> _logger;
    private readonly string _connectionString;
    private readonly string _masterSchema;

    public SchemaRoutingMiddleware(
        RequestDelegate next,
        IConfiguration configuration,
        ILogger<SchemaRoutingMiddleware> logger)
    {
        _next = next;
        _logger = logger;
        _connectionString = configuration.GetConnectionString("OracleDb")
            ?? throw new InvalidOperationException("Connection string 'OracleDb' not found.");

        var connectionBuilder = new OracleConnectionStringBuilder(_connectionString);
        _masterSchema = connectionBuilder.UserID?.Trim().Trim('"')
            ?? throw new InvalidOperationException("Oracle connection user is not configured.");

        if (!IsValidOracleIdentifier(_masterSchema))
        {
            throw new InvalidOperationException("Oracle connection user is not a valid schema identifier.");
        }
    }

    public async Task InvokeAsync(HttpContext context, OracleDbContext dbContext)
    {
        var tenantScope = context.GetEndpoint()?.Metadata.GetMetadata<TenantScopedAttribute>();
        if (tenantScope == null)
        {
            await _next(context);
            return;
        }

        var user = context.User;
        var isAuthenticated = user.Identity?.IsAuthenticated == true;
        var isSuperAdmin = string.Equals(
            user.FindFirst("isSuperAdmin")?.Value,
            "true",
            StringComparison.OrdinalIgnoreCase);

        var selectorResult = ReadCompanySelector(context.Request);
        if (!selectorResult.IsValid)
        {
            await WriteProblemAsync(
                context,
                StatusCodes.Status400BadRequest,
                "Invalid company selector",
                selectorResult.Error!);
            return;
        }

        TenantRequestContext? tenantContext;

        if (isSuperAdmin)
        {
            if (!selectorResult.CompanyId.HasValue &&
                string.IsNullOrWhiteSpace(selectorResult.CompanyCode))
            {
                if (tenantScope.SelectionRequired)
                {
                    await WriteProblemAsync(
                        context,
                        StatusCodes.Status400BadRequest,
                        "Company selection required",
                        "Select exactly one company using X-Company-Id or X-Company-Code.");
                    return;
                }

                await _next(context);
                return;
            }

            tenantContext = await ResolveTenantAsync(
                selectorResult.CompanyId,
                selectorResult.CompanyCode,
                expectedSchema: null,
                context.RequestAborted);

            if (tenantContext == null)
            {
                await WriteProblemAsync(
                    context,
                    StatusCodes.Status404NotFound,
                    "Company not found",
                    "The selected company does not exist, is inactive, or has no tenant schema.");
                return;
            }
        }
        else
        {
            if (!isAuthenticated)
            {
                // Let ASP.NET authorization produce the normal 401 response.
                await _next(context);
                return;
            }

            var companyIdClaim = user.FindFirst("companyId")?.Value;
            var companyCodeClaim = user.FindFirst("companyCode")?.Value;
            var companySchemaClaim = user.FindFirst("companySchema")?.Value;

            if (!long.TryParse(companyIdClaim, out var companyId) ||
                companyId <= 0 ||
                string.IsNullOrWhiteSpace(companySchemaClaim))
            {
                await WriteProblemAsync(
                    context,
                    StatusCodes.Status403Forbidden,
                    "Tenant context missing",
                    "The authenticated token does not contain a valid company context.");
                return;
            }

            tenantContext = await ResolveTenantAsync(
                companyId,
                companyCodeClaim,
                companySchemaClaim,
                context.RequestAborted);

            if (tenantContext == null)
            {
                await WriteProblemAsync(
                    context,
                    StatusCodes.Status403Forbidden,
                    "Tenant access denied",
                    "The token's company context is inactive or no longer valid.");
                return;
            }

            if ((selectorResult.CompanyId.HasValue &&
                 selectorResult.CompanyId.Value != tenantContext.CompanyId) ||
                (!string.IsNullOrWhiteSpace(selectorResult.CompanyCode) &&
                 !string.Equals(
                     selectorResult.CompanyCode,
                     tenantContext.CompanyCode,
                     StringComparison.OrdinalIgnoreCase)))
            {
                await WriteProblemAsync(
                    context,
                    StatusCodes.Status403Forbidden,
                    "Tenant access denied",
                    "A company user cannot override the company selected by their token.");
                return;
            }
        }

        context.Items[TenantRequestContext.HttpContextItemKey] = tenantContext;
        context.Items[TenantSchemaContextItem] = tenantContext.Schema;

        var connectionOpenedHere = dbContext.Database.GetDbConnection().State != ConnectionState.Open;

        try
        {
            if (connectionOpenedHere)
            {
                await dbContext.Database.OpenConnectionAsync(context.RequestAborted);
            }

            await SetCurrentSchemaAsync(
                dbContext,
                tenantContext.Schema,
                context.RequestAborted);

            _logger.LogDebug(
                "Tenant routing selected company {CompanyId}/{CompanyCode}",
                tenantContext.CompanyId,
                tenantContext.CompanyCode);

            await _next(context);
        }
        finally
        {
            try
            {
                if (dbContext.Database.GetDbConnection().State == ConnectionState.Open)
                {
                    await SetCurrentSchemaAsync(
                        dbContext,
                        _masterSchema,
                        CancellationToken.None);
                }
            }
            catch (Exception ex)
            {
                _logger.LogCritical(
                    ex,
                    "Failed to reset Oracle CURRENT_SCHEMA before returning the connection to the pool");
                if (dbContext.Database.GetDbConnection() is OracleConnection oracleConnection)
                {
                    OracleConnection.ClearPool(oracleConnection);
                }
            }
            finally
            {
                if (connectionOpenedHere &&
                    dbContext.Database.GetDbConnection().State == ConnectionState.Open)
                {
                    await dbContext.Database.CloseConnectionAsync();
                }
            }
        }
    }

    private async Task<TenantRequestContext?> ResolveTenantAsync(
        long? companyId,
        string? companyCode,
        string? expectedSchema,
        CancellationToken cancellationToken)
    {
        await using var connection = new OracleConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        try
        {
            await using (var resetCommand = connection.CreateCommand())
            {
                resetCommand.CommandText =
                    $"ALTER SESSION SET CURRENT_SCHEMA = \"{_masterSchema.ToUpperInvariant()}\"";
                await resetCommand.ExecuteNonQueryAsync(cancellationToken);
            }

            await using var command = connection.CreateCommand();
            command.BindByName = true;
            command.CommandText = $"""
                SELECT "Id", COMPANY_CODE, COMPANY_SCHEMA
                FROM "{_masterSchema.ToUpperInvariant()}"."SYS_COMPANY"
                WHERE (TO_CHAR(IS_ACTIVE) = '1' OR UPPER(TO_CHAR(IS_ACTIVE)) = 'Y')
                  AND COMPANY_SCHEMA IS NOT NULL
                """;

            if (companyId.HasValue)
            {
                command.CommandText += "\n  AND \"Id\" = :companyId";
                command.Parameters.Add("companyId", OracleDbType.Int64).Value = companyId.Value;
            }

            if (!string.IsNullOrWhiteSpace(companyCode))
            {
                command.CommandText += "\n  AND UPPER(COMPANY_CODE) = UPPER(:companyCode)";
                command.Parameters.Add("companyCode", OracleDbType.Varchar2).Value = companyCode.Trim();
            }

            if (!string.IsNullOrWhiteSpace(expectedSchema))
            {
                command.CommandText += "\n  AND UPPER(COMPANY_SCHEMA) = UPPER(:expectedSchema)";
                command.Parameters.Add("expectedSchema", OracleDbType.Varchar2).Value = expectedSchema.Trim();
            }

            await using var reader = await command.ExecuteReaderAsync(
                CommandBehavior.SingleRow,
                cancellationToken);

            if (!await reader.ReadAsync(cancellationToken))
            {
                return null;
            }

            var resolvedCompanyId = Convert.ToInt64(reader.GetValue(0));
            var resolvedCompanyCode = reader.GetString(1);
            var resolvedSchema = reader.GetString(2);

            if (!IsValidOracleIdentifier(resolvedSchema))
            {
                _logger.LogError(
                    "Company {CompanyId} has an invalid tenant schema identifier",
                    resolvedCompanyId);
                return null;
            }

            return new TenantRequestContext(
                resolvedCompanyId,
                resolvedCompanyCode,
                resolvedSchema.ToUpperInvariant());
        }
        finally
        {
            if (connection.State == ConnectionState.Open)
            {
                try
                {
                    await using var resetCommand = connection.CreateCommand();
                    resetCommand.CommandText =
                        $"ALTER SESSION SET CURRENT_SCHEMA = \"{_masterSchema.ToUpperInvariant()}\"";
                    await resetCommand.ExecuteNonQueryAsync(CancellationToken.None);
                }
                catch (Exception ex)
                {
                    _logger.LogCritical(
                        ex,
                        "Failed to reset the registry lookup connection to the master schema");
                    OracleConnection.ClearPool(connection);
                }
            }
        }
    }

    private static async Task SetCurrentSchemaAsync(
        OracleDbContext dbContext,
        string schema,
        CancellationToken cancellationToken)
    {
        if (!IsValidOracleIdentifier(schema))
        {
            throw new InvalidOperationException("Invalid Oracle schema identifier.");
        }

        await using var command = dbContext.Database.GetDbConnection().CreateCommand();
        command.CommandText =
            $"ALTER SESSION SET CURRENT_SCHEMA = \"{schema.ToUpperInvariant()}\"";
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static CompanySelector ReadCompanySelector(HttpRequest request)
    {
        long? companyId = null;
        string? companyCode = null;

        if (request.Headers.TryGetValue("X-Company-Id", out var companyIdValues))
        {
            if (companyIdValues.Count != 1 ||
                !long.TryParse(companyIdValues[0], out var parsedCompanyId) ||
                parsedCompanyId <= 0)
            {
                return CompanySelector.Invalid(
                    "X-Company-Id must contain one positive numeric company ID.");
            }

            companyId = parsedCompanyId;
        }

        if (request.Headers.TryGetValue("X-Company-Code", out var companyCodeValues))
        {
            if (companyCodeValues.Count != 1 ||
                string.IsNullOrWhiteSpace(companyCodeValues[0]))
            {
                return CompanySelector.Invalid(
                    "X-Company-Code must contain one non-empty company code.");
            }

            companyCode = companyCodeValues[0]!.Trim();
        }

        return CompanySelector.Valid(companyId, companyCode);
    }

    private static bool IsValidOracleIdentifier(string value) =>
        !string.IsNullOrWhiteSpace(value) && OracleIdentifierPattern.IsMatch(value);

    private static Task WriteProblemAsync(
        HttpContext context,
        int statusCode,
        string title,
        string detail)
    {
        context.Response.StatusCode = statusCode;
        return context.Response.WriteAsJsonAsync(
            new ProblemDetails
            {
                Status = statusCode,
                Title = title,
                Detail = detail,
                Instance = context.Request.Path
            },
            cancellationToken: context.RequestAborted);
    }

    private sealed record CompanySelector(
        bool IsValid,
        long? CompanyId,
        string? CompanyCode,
        string? Error)
    {
        public static CompanySelector Valid(long? companyId, string? companyCode) =>
            new(true, companyId, companyCode, null);

        public static CompanySelector Invalid(string error) =>
            new(false, null, null, error);
    }
}
