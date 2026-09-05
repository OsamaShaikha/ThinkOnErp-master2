using Microsoft.AspNetCore.Http;
using ThinkOnErp.Domain.Interfaces.Accounting;
using ThinkOnErp.Domain.Models;

namespace ThinkOnErp.Infrastructure.Services.Accounting;

public sealed class HttpCurrentTenantContext : ICurrentTenantContext
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    public HttpCurrentTenantContext(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    public long GetRequiredCompanyId()
    {
        var httpContext = _httpContextAccessor.HttpContext
            ?? throw new InvalidOperationException(
                "An active HTTP tenant context is required for this operation.");

        if (!httpContext.Items.TryGetValue(
                TenantRequestContext.HttpContextItemKey,
                out var value) ||
            value is not TenantRequestContext tenantContext ||
            tenantContext.CompanyId <= 0)
        {
            throw new InvalidOperationException(
                "An authoritative tenant context has not been resolved for this request.");
        }

        return tenantContext.CompanyId;
    }

    public string GetLanguageCode()
    {
        var httpContext = _httpContextAccessor.HttpContext;
        if (httpContext == null) return "en";

        // 1. From User JWT Claims
        var langClaim = httpContext.User.FindFirst("lang")?.Value 
                     ?? httpContext.User.FindFirst("defaultLang")?.Value
                     ?? httpContext.User.FindFirst("language")?.Value;

        if (!string.IsNullOrWhiteSpace(langClaim))
        {
            return langClaim switch
            {
                "1" => "ar",
                "2" => "en",
                "3" => "fr",
                "4" => "es",
                "5" => "tr",
                "6" => "de",
                _ => langClaim.Trim().ToLowerInvariant()
            };
        }

        // 2. From Accept-Language HTTP Header
        var acceptLang = httpContext.Request.Headers["Accept-Language"].ToString();
        if (!string.IsNullOrWhiteSpace(acceptLang))
        {
            var primaryLang = acceptLang.Split(',').FirstOrDefault()?.Split(';').FirstOrDefault()?.Trim().ToLowerInvariant();
            if (!string.IsNullOrWhiteSpace(primaryLang))
            {
                return primaryLang.Length > 2 ? primaryLang.Substring(0, 2) : primaryLang;
            }
        }

        return "en";
    }
}
