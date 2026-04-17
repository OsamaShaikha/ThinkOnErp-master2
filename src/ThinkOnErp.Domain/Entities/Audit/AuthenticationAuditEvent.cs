namespace ThinkOnErp.Domain.Entities.Audit;

/// <summary>
/// Audit event for authentication operations (login, logout, token refresh).
/// Tracks successful and failed authentication attempts for security monitoring.
/// </summary>
public class AuthenticationAuditEvent : AuditEvent
{
    /// <summary>
    /// Whether the authentication attempt was successful
    /// </summary>
    public bool Success { get; set; }

    /// <summary>
    /// Reason for authentication failure (null if successful)
    /// </summary>
    public string? FailureReason { get; set; }

    /// <summary>
    /// Unique identifier of the JWT or refresh token
    /// </summary>
    public string? TokenId { get; set; }

    /// <summary>
    /// Duration of the user session (for logout events)
    /// </summary>
    public TimeSpan? SessionDuration { get; set; }
}
