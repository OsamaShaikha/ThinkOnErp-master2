using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Routing;
using ThinkOnErp.API.Authorization;
using ThinkOnErp.Domain.Models;

namespace ThinkOnErp.SuperAdmin.Tests.Authorization;

public class PermissionRequiredAttributeTests
{
    [Fact]
    public async Task SuperAdmin_WithSelectedTenant_BypassesTenantUserPermissionLookup()
    {
        var httpContext = CreateHttpContext(
            new Claim("userId", "1"),
            new Claim("isSuperAdmin", "true"));
        httpContext.Items[TenantRequestContext.HttpContextItemKey] =
            new TenantRequestContext(341, "DEBS1", "THINKONERP_DEBS1");

        var filterContext = CreateFilterContext(httpContext);
        var attribute = new PermissionRequiredAttribute("USERS", "VIEW");

        await attribute.OnAuthorizationAsync(filterContext);

        Assert.Null(filterContext.Result);
    }

    [Fact]
    public async Task SuperAdmin_WithoutSelectedTenant_IsForbidden()
    {
        var filterContext = CreateFilterContext(CreateHttpContext(
            new Claim("userId", "1"),
            new Claim("isSuperAdmin", "true")));
        var attribute = new PermissionRequiredAttribute("USERS", "VIEW");

        await attribute.OnAuthorizationAsync(filterContext);

        Assert.IsType<ForbidResult>(filterContext.Result);
    }

    [Fact]
    public async Task AuthenticatedUser_WithoutNumericUserId_IsUnauthorized()
    {
        var filterContext = CreateFilterContext(CreateHttpContext(
            new Claim("userName", "missing-id")));
        var attribute = new PermissionRequiredAttribute("USERS", "VIEW");

        await attribute.OnAuthorizationAsync(filterContext);

        Assert.IsType<UnauthorizedResult>(filterContext.Result);
    }

    private static DefaultHttpContext CreateHttpContext(params Claim[] claims)
    {
        var context = new DefaultHttpContext();
        context.User = new ClaimsPrincipal(new ClaimsIdentity(claims, "Test"));
        return context;
    }

    private static AuthorizationFilterContext CreateFilterContext(HttpContext httpContext)
    {
        var actionContext = new ActionContext(
            httpContext,
            new RouteData(),
            new ActionDescriptor());

        return new AuthorizationFilterContext(
            actionContext,
            new List<IFilterMetadata>());
    }
}
