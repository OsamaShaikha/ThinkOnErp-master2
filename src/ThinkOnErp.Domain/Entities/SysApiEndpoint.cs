namespace ThinkOnErp.Domain.Entities;

/// <summary>
/// Represents an API Endpoint registered in the database for Swagger documentation and API gateway routing.
/// </summary>
public class SysApiEndpoint
{
    public long Id { get; set; }
    public string CategoryCode { get; set; } = string.Empty;
    public string ControllerName { get; set; } = string.Empty;
    public string ActionName { get; set; } = string.Empty;
    public string HttpMethod { get; set; } = string.Empty;
    public string RoutePath { get; set; } = string.Empty;
    public string? DisplayName { get; set; }
    public string? Description { get; set; }
    public bool IsActive { get; set; } = true;

    // Navigation property
    public virtual SysApiCategory? Category { get; set; }
}
