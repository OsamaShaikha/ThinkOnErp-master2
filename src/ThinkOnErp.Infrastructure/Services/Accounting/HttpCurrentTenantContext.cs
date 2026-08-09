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
}
