namespace ThinkOnErp.Domain.Models;

/// <summary>
/// The authoritative tenant selected for the current HTTP request.
/// The schema value is resolved from the central company registry and must never
/// be populated directly from request input.
/// </summary>
public sealed record TenantRequestContext(
    long CompanyId,
    string CompanyCode,
    string Schema)
{
    public const string HttpContextItemKey = "ThinkOnErp.TenantRequestContext";
}
