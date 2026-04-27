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
    /// Returns 0 if not authenticated.
    /// </summary>
    long GetActorId();

    /// <summary>
    /// Gets the actor type (SUPER_ADMIN, COMPANY_ADMIN, USER, SYSTEM) for the current request.
    /// </summary>
    string GetActorType();

    /// <summary>
    /// Gets the company ID for the current request.
    /// Returns null if not available.
    /// </summary>
    long? GetCompanyId();

    /// <summary>
    /// Gets the branch ID for the current request.
    /// Returns null if not available.
    /// </summary>
    long? GetBranchId();

    /// <summary>
    /// Gets the IP address for the current request.
    /// Returns null if not available.
    /// </summary>
    string? GetIpAddress();

    /// <summary>
    /// Gets the user agent for the current request.
    /// Returns null if not available.
    /// </summary>
    string? GetUserAgent();
}
