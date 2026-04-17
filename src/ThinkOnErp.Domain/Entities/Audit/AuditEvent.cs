namespace ThinkOnErp.Domain.Entities.Audit;

/// <summary>
/// Base class for all audit events in the traceability system.
/// Captures common information about who did what, when, and where.
/// </summary>
public abstract class AuditEvent
{
    /// <summary>
    /// Unique identifier tracking this request through the entire system
    /// </summary>
    public string CorrelationId { get; set; } = string.Empty;

    /// <summary>
    /// Type of actor performing the action: SUPER_ADMIN, COMPANY_ADMIN, USER, SYSTEM
    /// </summary>
    public string ActorType { get; set; } = string.Empty;

    /// <summary>
    /// ID of the user or system component performing the action
    /// </summary>
    public long ActorId { get; set; }

    /// <summary>
    /// Company ID for multi-tenant operations (nullable for system-level actions)
    /// </summary>
    public long? CompanyId { get; set; }

    /// <summary>
    /// Branch ID for multi-tenant operations (nullable for company-level actions)
    /// </summary>
    public long? BranchId { get; set; }

    /// <summary>
    /// Action performed: INSERT, UPDATE, DELETE, LOGIN, LOGOUT, etc.
    /// </summary>
    public string Action { get; set; } = string.Empty;

    /// <summary>
    /// Type of entity affected: User, Role, Company, Branch, etc.
    /// </summary>
    public string EntityType { get; set; } = string.Empty;

    /// <summary>
    /// ID of the specific entity affected (nullable for non-entity actions)
    /// </summary>
    public long? EntityId { get; set; }

    /// <summary>
    /// IP address of the client making the request
    /// </summary>
    public string? IpAddress { get; set; }

    /// <summary>
    /// User agent string from the HTTP request
    /// </summary>
    public string? UserAgent { get; set; }

    /// <summary>
    /// Timestamp when the event occurred (UTC)
    /// </summary>
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
}
