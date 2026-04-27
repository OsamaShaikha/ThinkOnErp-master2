namespace ThinkOnErp.Domain.Entities;

/// <summary>
/// Entity representing an audit log entry in the SYS_AUDIT_LOG table.
/// Captures comprehensive information about system events including data changes,
/// authentication events, permission changes, and exceptions.
/// </summary>
public class SysAuditLog
{
    /// <summary>
    /// Primary key - unique identifier for the audit log entry
    /// </summary>
    public long RowId { get; set; }

    /// <summary>
    /// Type of actor performing the action (SUPER_ADMIN, COMPANY_ADMIN, USER, SYSTEM)
    /// </summary>
    public string ActorType { get; set; } = null!;

    /// <summary>
    /// ID of the actor (user ID or system component ID)
    /// </summary>
    public long ActorId { get; set; }

    /// <summary>
    /// Company ID for multi-tenant context
    /// </summary>
    public long? CompanyId { get; set; }

    /// <summary>
    /// Branch ID for multi-tenant context
    /// </summary>
    public long? BranchId { get; set; }

    /// <summary>
    /// Action performed (INSERT, UPDATE, DELETE, LOGIN, LOGOUT, etc.)
    /// </summary>
    public string Action { get; set; } = null!;

    /// <summary>
    /// Type of entity affected (SysUser, SysCompany, SysBranch, etc.)
    /// </summary>
    public string EntityType { get; set; } = null!;

    /// <summary>
    /// ID of the entity affected
    /// </summary>
    public long? EntityId { get; set; }

    /// <summary>
    /// Previous value before change (JSON format for complex objects)
    /// </summary>
    public string? OldValue { get; set; }

    /// <summary>
    /// New value after change (JSON format for complex objects)
    /// </summary>
    public string? NewValue { get; set; }

    /// <summary>
    /// IP address of the client making the request
    /// </summary>
    public string? IpAddress { get; set; }

    /// <summary>
    /// User agent string from the HTTP request
    /// </summary>
    public string? UserAgent { get; set; }

    /// <summary>
    /// Unique correlation ID for tracking requests through the system
    /// </summary>
    public string? CorrelationId { get; set; }

    /// <summary>
    /// HTTP method (GET, POST, PUT, DELETE, etc.)
    /// </summary>
    public string? HttpMethod { get; set; }

    /// <summary>
    /// API endpoint path
    /// </summary>
    public string? EndpointPath { get; set; }

    /// <summary>
    /// Request payload (JSON format, may be truncated for large payloads)
    /// </summary>
    public string? RequestPayload { get; set; }

    /// <summary>
    /// Response payload (JSON format, may be truncated for large payloads)
    /// </summary>
    public string? ResponsePayload { get; set; }

    /// <summary>
    /// Request execution time in milliseconds
    /// </summary>
    public long? ExecutionTimeMs { get; set; }

    /// <summary>
    /// HTTP status code
    /// </summary>
    public int? StatusCode { get; set; }

    /// <summary>
    /// Exception type if an error occurred
    /// </summary>
    public string? ExceptionType { get; set; }

    /// <summary>
    /// Exception message if an error occurred
    /// </summary>
    public string? ExceptionMessage { get; set; }

    /// <summary>
    /// Full stack trace if an exception occurred
    /// </summary>
    public string? StackTrace { get; set; }

    /// <summary>
    /// Severity level (Critical, Error, Warning, Info)
    /// </summary>
    public string Severity { get; set; } = "Info";

    /// <summary>
    /// Event category (DataChange, Authentication, Permission, Exception, Configuration, Request)
    /// </summary>
    public string EventCategory { get; set; } = "DataChange";

    /// <summary>
    /// Additional metadata in JSON format for extensibility
    /// </summary>
    public string? Metadata { get; set; }

    /// <summary>
    /// Business module (POS, HR, Accounting, etc.) - for legacy compatibility
    /// </summary>
    public string? BusinessModule { get; set; }

    /// <summary>
    /// Device identifier (POS Terminal 03, Desktop-HR-02, etc.) - for legacy compatibility
    /// </summary>
    public string? DeviceIdentifier { get; set; }

    /// <summary>
    /// Standardized error code (DB_TIMEOUT_001, API_HR_045, etc.) - for legacy compatibility
    /// </summary>
    public string? ErrorCode { get; set; }

    /// <summary>
    /// Human-readable business description - for legacy compatibility
    /// </summary>
    public string? BusinessDescription { get; set; }

    /// <summary>
    /// Timestamp when the audit log entry was created
    /// </summary>
    public DateTime CreationDate { get; set; } = DateTime.UtcNow;
}
