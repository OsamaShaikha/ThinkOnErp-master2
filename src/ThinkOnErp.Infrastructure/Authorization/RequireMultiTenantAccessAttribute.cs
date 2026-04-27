using Microsoft.AspNetCore.Authorization;

namespace ThinkOnErp.Infrastructure.Authorization;

/// <summary>
/// Authorization attribute that enforces multi-tenant access control.
/// Ensures users can only access resources within their assigned company and branch.
/// Unauthorized access attempts are logged as security threats.
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = false)]
public class RequireMultiTenantAccessAttribute : AuthorizeAttribute
{
    public const string PolicyName = "MultiTenantAccess";

    public RequireMultiTenantAccessAttribute()
    {
        Policy = PolicyName;
    }
}
