namespace ThinkOnErp.Domain.Interfaces;

/// <summary>
/// Provides audit context information for the current request.
/// Abstracts away HTTP context details from the Application layer.
/// </summary>
public interface IAuditContextProvider
{
    /// <summary>
    /// Gets the current correlation ID for the request.
    /// </summary>
    string GetCorrelationId();

    /// <summary>
    /// Gets the actor ID (user ID) for the current request.
    /// </summary>
    long GetActorId();

    /// <summary>
    /// Gets the actor type (SUPER_ADMIN, COMPANY_ADMIN, USER, SYSTEM) for the current request.
    /// </summary>
    string GetActorType();

    /// <summary>
    /// Gets the company ID for the current request.
    /// </summary>
    long? GetCompanyId();

    /// <summary>
    /// Gets the branch ID for the current request.
    /// </summary>
    long? GetBranchId();

    /// <summary>
    /// Gets the IP address for the current request.
    /// </summary>
    string? GetIpAddress();

    /// <summary>
    /// Gets the user agent for the current request.
    /// </summary>
    string? GetUserAgent();

    /// <summary>
    /// Gets the HTTP method (GET, POST, PUT, DELETE, etc.) for the current request.
    /// </summary>
    string? GetHttpMethod();

    /// <summary>
    /// Gets the endpoint path for the current request, including query string if present.
    /// </summary>
    string? GetEndpointPath();
}
