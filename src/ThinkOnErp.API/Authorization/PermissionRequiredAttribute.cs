using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using ThinkOnErp.Domain.Interfaces;

namespace ThinkOnErp.API.Authorization;

[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = true)]
public class PermissionRequiredAttribute : Attribute, IAsyncAuthorizationFilter
{
    public string ScreenCode { get; }
    public string FeatureCode { get; }

    public PermissionRequiredAttribute(string screenCode, string featureCode)
    {
        ScreenCode = screenCode;
        FeatureCode = featureCode;
    }

    public async Task OnAuthorizationAsync(AuthorizationFilterContext context)
    {
        var userIdClaim = context.HttpContext.User.FindFirst("userId")?.Value;
        if (string.IsNullOrEmpty(userIdClaim) || !long.TryParse(userIdClaim, out var userId))
        {
            context.Result = new UnauthorizedResult();
            return;
        }

        var permissionService = context.HttpContext.RequestServices.GetRequiredService<IPermissionService>();
        var allowed = await permissionService.CanAccessByCodeAsync(userId, ScreenCode, FeatureCode);

        if (!allowed)
        {
            context.Result = new ForbidResult();
        }
    }
}
