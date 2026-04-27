namespace ThinkOnErp.Application.DTOs.Audit;

/// <summary>
/// Data transfer object for audit log entries.
/// Represents a comprehensive audit log entry with all tracking information.
/// </summary>
public class AuditLogDto
{
    /// <summary>
    /// Gets or sets the unique identifier of the audit log entry.
    /// </summary>
    public long Id { get; set; }

    /// <summary>
    /// Gets or sets the correlation ID that tracks the request through the system.
    /// </summary>
    public string CorrelationId { get; set; } = null!;

    /// <summary>
    /// Gets or sets the type of actor (SUPER_ADMIN, COMPANY_ADMIN, USER, SYSTEM).
    /// </summary>
    public string ActorType { get; set; } = null!;

    /// <summary>
    /// Gets or sets the ID of the actor who performed the action.
    /// </summary>
    public long ActorId { get; set; }

    /// <summary>
    /// Gets or sets the name of the actor who performed the action.
    /// </summary>
    public string? ActorName { get; set; }

    /// <summary>
    /// Gets or sets the company ID associated with the action.
    /// </summary>
    public long? CompanyId { get; set; }

    /// <summary>
    /// Gets or sets the branch ID associated with the action.
    /// </summary>
    public long? BranchId { get; set; }

    /// <summary>
    /// Gets or sets the action performed (INSERT, UPDATE, DELETE, LOGIN, LOGOUT, etc.).
    /// </summary>
    public string Action { get; set; } = null!;

    /// <summary>
    /// Gets or sets the type of entity affected by the action.
    /// </summary>
    public string EntityType { get; set; } = null!;

    /// <summary>
    /// Gets or sets the ID of the entity affected by the action.
    /// </summary>
    public long? EntityId { get; set; }

    /// <summary>
    /// Gets or sets the old value before the change (JSON format).
    /// </summary>
    public string? OldValue { get; set; }

    /// <summary>
    /// Gets or sets the new value after the change (JSON format).
    /// </summary>
    public string? NewValue { get; set; }

    /// <summary>
    /// Gets or sets the IP address of the actor.
    /// </summary>
    public string? IpAddress { get; set; }

    /// <summary>
    /// Gets or sets the user agent string of the actor's client.
    /// </summary>
    public string? UserAgent { get; set; }

    /// <summary>
    /// Gets or sets the HTTP method of the request (GET, POST, PUT, DELETE, etc.).
    /// </summary>
    public string? HttpMethod { get; set; }

    /// <summary>
    /// Gets or sets the endpoint path of the request.
    /// </summary>
    public string? EndpointPath { get; set; }

    /// <summary>
    /// Gets or sets the execution time of the request in milliseconds.
    /// </summary>
    public long? ExecutionTimeMs { get; set; }

    /// <summary>
    /// Gets or sets the HTTP status code of the response.
    /// </summary>
    public int? StatusCode { get; set; }

    /// <summary>
    /// Gets or sets the type of exception if an error occurred.
    /// </summary>
    public string? ExceptionType { get; set; }

    /// <summary>
    /// Gets or sets the exception message if an error occurred.
    /// </summary>
    public string? ExceptionMessage { get; set; }

    /// <summary>
    /// Gets or sets the severity level (Critical, Error, Warning, Info).
    /// </summary>
    public string Severity { get; set; } = "Info";

    /// <summary>
    /// Gets or sets the event category (DataChange, Authentication, Permission, Exception, Configuration, Request).
    /// </summary>
    public string EventCategory { get; set; } = "DataChange";

    /// <summary>
    /// Gets or sets the timestamp when the audit log entry was created.
    /// </summary>
    public DateTime Timestamp { get; set; }
}
