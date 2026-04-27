using Microsoft.AspNetCore.Http;
using ThinkOnErp.Domain.Interfaces;

namespace ThinkOnErp.Infrastructure.Services;

/// <summary>
/// Provides audit context information from the current HTTP request.
/// Extracts user identity, company/branch context, and request metadata from JWT claims and HTTP context.
/// </summary>
public class AuditContextProvider : IAuditContextProvider
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    public AuditContextProvider(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    /// <summary>
    /// Gets the current correlation ID for the request.
    /// Returns a new GUID if no correlation ID exists.
    /// </summary>
    public string GetCorrelationId()
    {
        return CorrelationContext.GetOrCreate();
    }

    /// <summary>
    /// Gets the actor ID (user ID) for the current request.
    /// Returns 0 if not authenticated.
    /// </summary>
    public long GetActorId()
    {
        var httpContext = _httpContextAccessor.HttpContext;
        if (httpContext == null)
        {
            return 0;
        }

        var userIdClaim = httpContext.User?.FindFirst("userId")?.Value
                          ?? httpContext.User?.FindFirst("sub")?.Value;

        return long.TryParse(userIdClaim, out var userId) ? userId : 0;
    }

    /// <summary>
    /// Gets the actor type (SUPER_ADMIN, COMPANY_ADMIN, USER, SYSTEM) for the current request.
    /// </summary>
    public string GetActorType()
    {
        var httpContext = _httpContextAccessor.HttpContext;
        if (httpContext == null)
        {
            return "SYSTEM";
        }

        var isAdmin = httpContext.User?.FindFirst("isAdmin")?.Value;
        var isSuperAdmin = httpContext.User?.FindFirst("isSuperAdmin")?.Value;

        if (bool.TryParse(isSuperAdmin, out var superAdmin) && superAdmin)
        {
            return "SUPER_ADMIN";
        }

        if (bool.TryParse(isAdmin, out var admin) && admin)
        {
            return "COMPANY_ADMIN";
        }

        return httpContext.User?.Identity?.IsAuthenticated == true ? "USER" : "SYSTEM";
    }

    /// <summary>
    /// Gets the company ID for the current request.
    /// Returns null if not available.
    /// </summary>
    public long? GetCompanyId()
    {
        var httpContext = _httpContextAccessor.HttpContext;
        if (httpContext == null)
        {
            return null;
        }

        var companyIdClaim = httpContext.User?.FindFirst("companyId")?.Value;
        return long.TryParse(companyIdClaim, out var companyId) ? companyId : null;
    }

    /// <summary>
    /// Gets the branch ID for the current request.
    /// Returns null if not available.
    /// </summary>
    public long? GetBranchId()
    {
        var httpContext = _httpContextAccessor.HttpContext;
        if (httpContext == null)
        {
            return null;
        }

        var branchIdClaim = httpContext.User?.FindFirst("branchId")?.Value;
        return long.TryParse(branchIdClaim, out var branchId) ? branchId : null;
    }

    /// <summary>
    /// Gets the IP address for the current request.
    /// Returns null if not available.
    /// </summary>
    public string? GetIpAddress()
    {
        var httpContext = _httpContextAccessor.HttpContext;
        return httpContext?.Connection.RemoteIpAddress?.ToString();
    }

    /// <summary>
    /// Gets the user agent for the current request.
    /// Returns null if not available.
    /// </summary>
    public string? GetUserAgent()
    {
        var httpContext = _httpContextAccessor.HttpContext;
        return httpContext?.Request.Headers["User-Agent"].ToString();
    }
}
