using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using ThinkOnErp.Application.Common;
using ThinkOnErp.Application.Services.Validation;
using ThinkOnErp.Domain.Models;

namespace ThinkOnErp.API.Filters;

/// <summary>
/// Global Action Filter that automatically validates all incoming request models (DTOs)
/// against the dynamic database rules in THINKON_ERP.SYS_FIELD_VALIDATION_RULE and localized SYS_CODE.
/// </summary>
public sealed class DynamicValidationActionFilter : IAsyncActionFilter
{
    private readonly IDynamicValidationEngine _validationEngine;
    private readonly ILogger<DynamicValidationActionFilter> _logger;

    public DynamicValidationActionFilter(
        IDynamicValidationEngine validationEngine,
        ILogger<DynamicValidationActionFilter> logger)
    {
        _validationEngine = validationEngine;
        _logger = logger;
    }

    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        // 1. Resolve CompanyId and CountryCode from context
        long? companyId = null;
        if (context.HttpContext.Items.TryGetValue(TenantRequestContext.HttpContextItemKey, out var val) &&
            val is TenantRequestContext tenant && tenant.CompanyId > 0)
        {
            companyId = tenant.CompanyId;
        }

        string? countryCode = null;
        if (context.HttpContext.Request.Headers.TryGetValue("X-Country-Code", out var ccVal))
        {
            countryCode = ccVal.ToString().Trim().ToUpperInvariant();
        }

        // 2. Iterate action arguments
        foreach (var (paramName, argument) in context.ActionArguments)
        {
            if (argument == null) continue;

            var argType = argument.GetType();
            // Skip primitives, value types without properties, and cancellation tokens
            if (argType.IsPrimitive || argType == typeof(string) || argType == typeof(CancellationToken) || argType == typeof(Guid))
            {
                continue;
            }

            var validationResult = await _validationEngine.ValidateAsync(argument, countryCode, companyId, context.HttpContext.RequestAborted);
            if (!validationResult.IsValid)
            {
                var errorMessages = validationResult.Errors.Select(e => e.ErrorMessage).Distinct().ToList();
                var firstError = errorMessages.FirstOrDefault() ?? "Validation failed.";

                _logger.LogWarning("Dynamic validation failed for action {ActionName} on parameter {ParamName}: {Errors}",
                    context.ActionDescriptor.DisplayName, paramName, string.Join("; ", errorMessages));

                context.Result = new BadRequestObjectResult(ApiResponse<object>.CreateFailure(
                    firstError,
                    errorMessages,
                    StatusCodes.Status400BadRequest));
                return;
            }
        }

        await next();
    }
}
