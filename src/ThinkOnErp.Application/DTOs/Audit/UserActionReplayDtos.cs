namespace ThinkOnErp.Application.DTOs.Audit;

/// <summary>
/// DTO for user action replay functionality
/// Provides a chronological view of all actions performed by a user
/// </summary>
public class UserActionReplayDto
{
    /// <summary>
    /// User ID whose actions are being replayed
    /// </summary>
    public long UserId { get; set; }
    
    /// <summary>
    /// Username
    /// </summary>
    public string Username { get; set; } = null!;
    
    /// <summary>
    /// User's full name
    /// </summary>
    public string? FullName { get; set; }
    
    /// <summary>
    /// User's email
    /// </summary>
    public string? Email { get; set; }
    
    /// <summary>
    /// Start of the replay period
    /// </summary>
    public DateTime PeriodStart { get; set; }
    
    /// <summary>
    /// End of the replay period
    /// </summary>
    public DateTime PeriodEnd { get; set; }
    
    /// <summary>
    /// Total number of actions in the replay
    /// </summary>
    public int TotalActions { get; set; }
    
    /// <summary>
    /// Chronological list of all user actions
    /// </summary>
    public List<UserActionDto> Actions { get; set; } = new();
    
    /// <summary>
    /// Summary statistics by action type
    /// </summary>
    public Dictionary<string, int> ActionsByType { get; set; } = new();
    
    /// <summary>
    /// Summary statistics by entity type
    /// </summary>
    public Dictionary<string, int> ActionsByEntityType { get; set; } = new();
    
    /// <summary>
    /// Summary statistics by endpoint
    /// </summary>
    public Dictionary<string, int> ActionsByEndpoint { get; set; } = new();
    
    /// <summary>
    /// Number of successful actions
    /// </summary>
    public int SuccessfulActions { get; set; }
    
    /// <summary>
    /// Number of failed actions (errors/exceptions)
    /// </summary>
    public int FailedActions { get; set; }
    
    /// <summary>
    /// When this replay was generated
    /// </summary>
    public DateTime GeneratedAt { get; set; }
}

/// <summary>
/// DTO for a single user action in the replay
/// </summary>
public class UserActionDto
{
    /// <summary>
    /// Audit log entry ID
    /// </summary>
    public long AuditLogId { get; set; }
    
    /// <summary>
    /// Correlation ID for the request
    /// </summary>
    public string CorrelationId { get; set; } = null!;
    
    /// <summary>
    /// When the action occurred
    /// </summary>
    public DateTime Timestamp { get; set; }
    
    /// <summary>
    /// Action type (INSERT, UPDATE, DELETE, LOGIN, LOGOUT, etc.)
    /// </summary>
    public string Action { get; set; } = null!;
    
    /// <summary>
    /// Entity type affected by the action
    /// </summary>
    public string EntityType { get; set; } = null!;
    
    /// <summary>
    /// Entity ID affected by the action
    /// </summary>
    public long? EntityId { get; set; }
    
    /// <summary>
    /// HTTP method used (GET, POST, PUT, DELETE, etc.)
    /// </summary>
    public string? HttpMethod { get; set; }
    
    /// <summary>
    /// Endpoint path accessed
    /// </summary>
    public string? EndpointPath { get; set; }
    
    /// <summary>
    /// Request payload (may be truncated or masked)
    /// </summary>
    public string? RequestPayload { get; set; }
    
    /// <summary>
    /// Response payload (may be truncated or masked)
    /// </summary>
    public string? ResponsePayload { get; set; }
    
    /// <summary>
    /// HTTP status code returned
    /// </summary>
    public int? StatusCode { get; set; }
    
    /// <summary>
    /// Execution time in milliseconds
    /// </summary>
    public long? ExecutionTimeMs { get; set; }
    
    /// <summary>
    /// Old value before the change (for UPDATE/DELETE actions)
    /// </summary>
    public string? OldValue { get; set; }
    
    /// <summary>
    /// New value after the change (for INSERT/UPDATE actions)
    /// </summary>
    public string? NewValue { get; set; }
    
    /// <summary>
    /// IP address from which the action was performed
    /// </summary>
    public string? IpAddress { get; set; }
    
    /// <summary>
    /// User agent string
    /// </summary>
    public string? UserAgent { get; set; }
    
    /// <summary>
    /// Company ID associated with the action
    /// </summary>
    public long? CompanyId { get; set; }
    
    /// <summary>
    /// Company name
    /// </summary>
    public string? CompanyName { get; set; }
    
    /// <summary>
    /// Branch ID associated with the action
    /// </summary>
    public long? BranchId { get; set; }
    
    /// <summary>
    /// Branch name
    /// </summary>
    public string? BranchName { get; set; }
    
    /// <summary>
    /// Event category (DataChange, Authentication, Permission, Exception, Configuration, Request)
    /// </summary>
    public string EventCategory { get; set; } = "DataChange";
    
    /// <summary>
    /// Severity level (Critical, Error, Warning, Info)
    /// </summary>
    public string Severity { get; set; } = "Info";
    
    /// <summary>
    /// Exception type if an error occurred
    /// </summary>
    public string? ExceptionType { get; set; }
    
    /// <summary>
    /// Exception message if an error occurred
    /// </summary>
    public string? ExceptionMessage { get; set; }
    
    /// <summary>
    /// Whether this action was successful
    /// </summary>
    public bool IsSuccess { get; set; }
    
    /// <summary>
    /// Human-readable description of the action
    /// </summary>
    public string? Description { get; set; }
}

/// <summary>
/// Request DTO for generating a user action replay
/// </summary>
public class UserActionReplayRequestDto
{
    /// <summary>
    /// User ID to replay actions for
    /// </summary>
    public long UserId { get; set; }
    
    /// <summary>
    /// Start date for the replay period
    /// </summary>
    public DateTime StartDate { get; set; }
    
    /// <summary>
    /// End date for the replay period
    /// </summary>
    public DateTime EndDate { get; set; }
    
    /// <summary>
    /// Optional filter by endpoint path
    /// </summary>
    public string? EndpointPath { get; set; }
    
    /// <summary>
    /// Optional filter by action type
    /// </summary>
    public string? ActionType { get; set; }
    
    /// <summary>
    /// Optional filter by entity type
    /// </summary>
    public string? EntityType { get; set; }
    
    /// <summary>
    /// Whether to include request/response payloads (may be large)
    /// </summary>
    public bool IncludePayloads { get; set; } = false;
    
    /// <summary>
    /// Whether to include only failed actions
    /// </summary>
    public bool FailedActionsOnly { get; set; } = false;
    
    /// <summary>
    /// Maximum number of actions to return (default: 1000)
    /// </summary>
    public int MaxActions { get; set; } = 1000;
}

/// <summary>
/// DTO for entity history (all changes to a specific entity)
/// </summary>
public class EntityHistoryDto
{
    /// <summary>
    /// Entity type
    /// </summary>
    public string EntityType { get; set; } = null!;
    
    /// <summary>
    /// Entity ID
    /// </summary>
    public long EntityId { get; set; }
    
    /// <summary>
    /// Total number of changes to this entity
    /// </summary>
    public int TotalChanges { get; set; }
    
    /// <summary>
    /// When the entity was created
    /// </summary>
    public DateTime? CreatedAt { get; set; }
    
    /// <summary>
    /// User who created the entity
    /// </summary>
    public string? CreatedBy { get; set; }
    
    /// <summary>
    /// When the entity was last modified
    /// </summary>
    public DateTime? LastModifiedAt { get; set; }
    
    /// <summary>
    /// User who last modified the entity
    /// </summary>
    public string? LastModifiedBy { get; set; }
    
    /// <summary>
    /// Chronological list of all changes to the entity
    /// </summary>
    public List<EntityChangeDto> Changes { get; set; } = new();
    
    /// <summary>
    /// Summary of changes by user
    /// </summary>
    public Dictionary<string, int> ChangesByUser { get; set; } = new();
}

/// <summary>
/// DTO for a single entity change
/// </summary>
public class EntityChangeDto
{
    /// <summary>
    /// Audit log entry ID
    /// </summary>
    public long AuditLogId { get; set; }
    
    /// <summary>
    /// When the change occurred
    /// </summary>
    public DateTime Timestamp { get; set; }
    
    /// <summary>
    /// Action type (INSERT, UPDATE, DELETE)
    /// </summary>
    public string Action { get; set; } = null!;
    
    /// <summary>
    /// User ID who made the change
    /// </summary>
    public long ActorId { get; set; }
    
    /// <summary>
    /// Username who made the change
    /// </summary>
    public string ActorName { get; set; } = null!;
    
    /// <summary>
    /// Old value before the change (JSON format)
    /// </summary>
    public string? OldValue { get; set; }
    
    /// <summary>
    /// New value after the change (JSON format)
    /// </summary>
    public string? NewValue { get; set; }
    
    /// <summary>
    /// List of fields that were changed (for UPDATE operations)
    /// </summary>
    public List<string>? ChangedFields { get; set; }
    
    /// <summary>
    /// IP address from which the change was made
    /// </summary>
    public string? IpAddress { get; set; }
    
    /// <summary>
    /// Correlation ID for request tracing
    /// </summary>
    public string? CorrelationId { get; set; }
    
    /// <summary>
    /// Human-readable description of the change
    /// </summary>
    public string? Description { get; set; }
}
