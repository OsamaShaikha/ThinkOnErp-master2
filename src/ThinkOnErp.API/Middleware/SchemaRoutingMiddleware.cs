using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using ThinkOnErp.API.Authorization;
using ThinkOnErp.Domain.Models;
using ThinkOnErp.Infrastructure.Data;

namespace ThinkOnErp.API.Middleware;

/// <summary>
/// Resolves the authenticated company from the central registry, then runs a
/// tenant-scoped request inside that company's Oracle schema.
/// </summary>
public sealed class SchemaRoutingMiddleware
{
    public const string TenantSchemaContextItem = "ThinkOnErp.TenantSchema";

    private readonly RequestDelegate _next;
    private readonly ITenantSchemaResolver _tenantResolver;
    private readonly ITenantSchemaSession _tenantSession;

    public SchemaRoutingMiddleware(
        RequestDelegate next,
        ITenantSchemaResolver tenantResolver,
        ITenantSchemaSession tenantSession)
    {
        _next = next;
        _tenantResolver = tenantResolver;
        _tenantSession = tenantSession;
    }

    public async Task InvokeAsync(HttpContext context)
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

        var selector = ReadCompanySelector(context.Request);
        if (!selector.IsValid)
        {
            await WriteProblemAsync(context, StatusCodes.Status400BadRequest,
                "Invalid company selector", selector.Error!);
            return;
        }

        TenantRequestContext? tenantContext;
        if (isSuperAdmin)
        {
            if (!selector.CompanyId.HasValue && string.IsNullOrWhiteSpace(selector.CompanyCode))
            {
                if (tenantScope.SelectionRequired)
                {
                    await WriteProblemAsync(context, StatusCodes.Status400BadRequest,
                        "Company selection required", "Select exactly one company using X-Company-Id or X-Company-Code.");
                    return;
                }

                await _next(context);
                return;
            }

            tenantContext = await _tenantResolver.ResolveAsync(
                selector.CompanyId, selector.CompanyCode, expectedSchema: null, context.RequestAborted);
            if (tenantContext == null)
            {
                await WriteProblemAsync(context, StatusCodes.Status404NotFound,
                    "Company not found", "The selected company does not exist, is inactive, or has no tenant schema.");
                return;
            }

            if (!MatchesResolvedTenant(tenantContext, selector.CompanyId, selector.CompanyCode, expectedSchema: null))
            {
                await WriteProblemAsync(context, StatusCodes.Status404NotFound,
                    "Company not found", "The registry did not resolve the explicitly selected company.");
                return;
            }
        }
        else
        {
            if (!isAuthenticated)
            {
                await _next(context);
                return;
            }

            var companyIdClaim = user.FindFirst("companyId")?.Value;
            var companyCodeClaim = user.FindFirst("companyCode")?.Value;
            var companySchemaClaim = user.FindFirst("companySchema")?.Value;
            if (!long.TryParse(companyIdClaim, out var companyId) || companyId <= 0 ||
                string.IsNullOrWhiteSpace(companySchemaClaim))
            {
                await WriteProblemAsync(context, StatusCodes.Status403Forbidden,
                    "Tenant context missing", "The authenticated token does not contain a valid company context.");
                return;
            }

            tenantContext = await _tenantResolver.ResolveAsync(
                companyId, companyCodeClaim, companySchemaClaim, context.RequestAborted);
            if (tenantContext == null)
            {
                await WriteProblemAsync(context, StatusCodes.Status403Forbidden,
                    "Tenant access denied", "The token's company context is inactive or no longer valid.");
                return;
            }

            if (!MatchesResolvedTenant(tenantContext, companyId, companyCodeClaim, companySchemaClaim))
            {
                await WriteProblemAsync(context, StatusCodes.Status403Forbidden,
                    "Tenant access denied", "The registry resolution does not match the authenticated company context.");
                return;
            }

            if ((selector.CompanyId.HasValue && selector.CompanyId.Value != tenantContext.CompanyId) ||
                (!string.IsNullOrWhiteSpace(selector.CompanyCode) &&
                 !string.Equals(selector.CompanyCode, tenantContext.CompanyCode, StringComparison.OrdinalIgnoreCase)))
            {
                await WriteProblemAsync(context, StatusCodes.Status403Forbidden,
                    "Tenant access denied", "A company user cannot override the company selected by their token.");
                return;
            }
        }

        context.Items[TenantRequestContext.HttpContextItemKey] = tenantContext;
        context.Items[TenantSchemaContextItem] = tenantContext.Schema;
        var dbContext = context.RequestServices.GetRequiredService<OracleDbContext>();
        await _tenantSession.RunAsync(dbContext, tenantContext.Schema, context.RequestAborted, () => _next(context));
    }

    private static CompanySelector ReadCompanySelector(HttpRequest request)
    {
        if (request.Headers.ContainsKey("X-Company-Id") && request.Headers.ContainsKey("X-Company-Code"))
            return CompanySelector.Invalid("Specify only one company selector: X-Company-Id or X-Company-Code.");

        long? companyId = null;
        string? companyCode = null;
        if (request.Headers.TryGetValue("X-Company-Id", out var companyIdValues))
        {
            if (companyIdValues.Count != 1 || !long.TryParse(companyIdValues[0], out var parsedCompanyId) || parsedCompanyId <= 0)
                return CompanySelector.Invalid("X-Company-Id must contain one positive numeric company ID.");
            companyId = parsedCompanyId;
        }

        if (request.Headers.TryGetValue("X-Company-Code", out var companyCodeValues))
        {
            if (companyCodeValues.Count != 1 || string.IsNullOrWhiteSpace(companyCodeValues[0]))
                return CompanySelector.Invalid("X-Company-Code must contain one non-empty company code.");
            companyCode = companyCodeValues[0]!.Trim();
        }

        return CompanySelector.Valid(companyId, companyCode);
    }

    private static bool MatchesResolvedTenant(
        TenantRequestContext resolved,
        long? expectedCompanyId,
        string? expectedCompanyCode,
        string? expectedSchema) =>
        OracleSchemaIdentifier.IsValid(resolved.Schema) &&
        (!expectedCompanyId.HasValue || resolved.CompanyId == expectedCompanyId.Value) &&
        (string.IsNullOrWhiteSpace(expectedCompanyCode) || string.Equals(
            resolved.CompanyCode,
            expectedCompanyCode.Trim(),
            StringComparison.OrdinalIgnoreCase)) &&
        (string.IsNullOrWhiteSpace(expectedSchema) || string.Equals(
            resolved.Schema,
            expectedSchema.Trim(),
            StringComparison.OrdinalIgnoreCase));

    private static Task WriteProblemAsync(HttpContext context, int statusCode, string title, string detail)
    {
        context.Response.StatusCode = statusCode;
        return context.Response.WriteAsJsonAsync(new ProblemDetails
        {
            Status = statusCode,
            Title = title,
            Detail = detail,
            Instance = context.Request.Path
        }, cancellationToken: context.RequestAborted);
    }

    private sealed record CompanySelector(bool IsValid, long? CompanyId, string? CompanyCode, string? Error)
    {
        public static CompanySelector Valid(long? companyId, string? companyCode) => new(true, companyId, companyCode, null);
        public static CompanySelector Invalid(string error) => new(false, null, null, error);
    }
}
