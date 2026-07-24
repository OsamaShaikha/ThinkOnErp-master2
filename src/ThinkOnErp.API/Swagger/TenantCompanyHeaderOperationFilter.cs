using System.Reflection;
using Microsoft.OpenApi.Models;
using Swashbuckle.AspNetCore.SwaggerGen;
using ThinkOnErp.API.Authorization;

namespace ThinkOnErp.API.Swagger;

/// <summary>
/// Documents the per-request company selector used when a SuperAdmin calls
/// tenant-scoped APIs from Swagger UI.
/// </summary>
public sealed class TenantCompanyHeaderOperationFilter : IOperationFilter
{
    public void Apply(OpenApiOperation operation, OperationFilterContext context)
    {
        var tenantScope =
            context.MethodInfo.GetCustomAttribute<TenantScopedAttribute>(inherit: true) ??
            context.MethodInfo.DeclaringType?.GetCustomAttribute<TenantScopedAttribute>(
                inherit: true);

        if (tenantScope == null)
        {
            return;
        }

        operation.Parameters ??= new List<OpenApiParameter>();

        operation.Parameters.Add(new OpenApiParameter
        {
            Name = "X-Company-Code",
            In = ParameterLocation.Header,
            Required = false,
            Schema = new OpenApiSchema { Type = "string" },
            Description = tenantScope.SelectionRequired
                ? "Required for SuperAdmin unless X-Company-Id is supplied. Ignored as an override for company users."
                : "Optional company selector for SuperAdmin. Use either this header or X-Company-Id."
        });

        operation.Parameters.Add(new OpenApiParameter
        {
            Name = "X-Company-Id",
            In = ParameterLocation.Header,
            Required = false,
            Schema = new OpenApiSchema { Type = "integer", Format = "int64" },
            Description = tenantScope.SelectionRequired
                ? "Required for SuperAdmin unless X-Company-Code is supplied. Ignored as an override for company users."
                : "Optional company selector for SuperAdmin. Use either this header or X-Company-Code."
        });
    }
}
