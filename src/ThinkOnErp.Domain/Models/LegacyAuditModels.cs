namespace ThinkOnErp.Domain.Models;

/// <summary>
/// Legacy audit log DTO that matches the exact format from logs.png
/// </summary>
public class LegacyAuditLogDto
{
    /// <summary>
    /// Unique identifier for the audit log entry
    /// </summary>
    public long Id { get; set; }
    
    /// <summary>
    /// Matches "Error Description" column in logs.png
    /// Human-readable description of what happened
    /// </summary>
    public string ErrorDescription { get; set; } = null!;
    
    /// <summary>
    /// Matches "Module" column in logs.png (POS, HR, Accounting, etc.)
    /// Business module where the event occurred
    /// </summary>
    public string Module { get; set; } = null!;
    
    /// <summary>
    /// Matches "Company" column in logs.png
    /// Company name where the event occurred
    /// </summary>
    public string Company { get; set; } = null!;
    
    /// <summary>
    /// Matches "Branch" column in logs.png
    /// Branch name where the event occurred
    /// </summary>
    public string Branch { get; set; } = null!;
    
    /// <summary>
    /// Matches "User" column in logs.png
    /// User who performed the action
    /// </summary>
    public string User { get; set; } = null!;
    
    /// <summary>
    /// Matches "Device" column in logs.png (POS Terminal 03, Desktop-HR-02, etc.)
    /// Device identifier extracted from User-Agent
    /// </summary>
    public string Device { get; set; } = null!;
    
    /// <summary>
    /// Matches "Date & Time" column in logs.png
    /// When the event occurred
    /// </summary>
    public DateTime DateTime { get; set; }
    
    /// <summary>
    /// Matches "Status" column in logs.png (Unresolved, In Progress, Resolved, Critical Errors)
    /// Current resolution status of the entry
    /// </summary>
    public string Status { get; set; } = null!;
    
    /// <summary>
    /// For the Actions column functionality - indicates if user can resolve this entry
    /// </summary>
    public bool CanResolve { get; set; }
    
    /// <summary>
    /// For the Actions column functionality - indicates if user can delete this entry
    /// </summary>
    public bool CanDelete { get; set; }
    
    /// <summary>
    /// For the Actions column functionality - indicates if user can view details
    /// </summary>
    public bool CanViewDetails { get; set; }
    
    /// <summary>
    /// Standardized error code for categorization (DB_TIMEOUT_001, API_HR_045, etc.)
    /// </summary>
    public string? ErrorCode { get; set; }
    
    /// <summary>
    /// Correlation ID for detailed tracing and debugging
    /// </summary>
    public string? CorrelationId { get; set; }
}

/// <summary>
/// Dashboard counters that match the top section of logs.png
/// </summary>
public class LegacyDashboardCounters
{
    /// <summary>
    /// Number of unresolved errors (Red circle with count in logs.png)
    /// </summary>
    public int UnresolvedCount { get; set; }
    
    /// <summary>
    /// Number of errors in progress (Orange circle with count in logs.png)
    /// </summary>
    public int InProgressCount { get; set; }
    
    /// <summary>
    /// Number of resolved errors (Green circle with count in logs.png)
    /// </summary>
    public int ResolvedCount { get; set; }
    
    /// <summary>
    /// Number of critical errors (Dark red circle with count in logs.png)
    /// </summary>
    public int CriticalErrorsCount { get; set; }
}

/// <summary>
/// Filter for legacy audit logs view that matches logs.png filtering options
/// </summary>
public class LegacyAuditLogFilter
{
    /// <summary>
    /// Filter by company name
    /// </summary>
    public string? Company { get; set; }
    
    /// <summary>
    /// Filter by business module (POS, HR, Accounting, etc.)
    /// </summary>
    public string? Module { get; set; }
    
    /// <summary>
    /// Filter by branch name
    /// </summary>
    public string? Branch { get; set; }
    
    /// <summary>
    /// Filter by status (Unresolved, In Progress, Resolved, Critical)
    /// </summary>
    public string? Status { get; set; }
    
    /// <summary>
    /// Filter by start date
    /// </summary>
    public DateTime? StartDate { get; set; }
    
    /// <summary>
    /// Filter by end date
    /// </summary>
    public DateTime? EndDate { get; set; }
    
    /// <summary>
    /// Search term for description, user, device, or error code
    /// </summary>
    public string? SearchTerm { get; set; }
}

/// <summary>
/// Pagination options for legacy audit logs
/// </summary>
public class PaginationOptions
{
    /// <summary>
    /// Page number (1-based, default: 1)
    /// </summary>
    public int PageNumber { get; set; } = 1;
    
    /// <summary>
    /// Number of items per page (default: 50, max: 100)
    /// </summary>
    public int PageSize { get; set; } = 50;
    
    /// <summary>
    /// Calculate skip count for database queries
    /// </summary>
    public int Skip => (PageNumber - 1) * PageSize;
}

/// <summary>
/// Generic paged result wrapper for API responses with pagination.
/// </summary>
/// <typeparam name="T">The type of items in the result</typeparam>
public class PagedResult<T>
{
    /// <summary>
    /// List of items for the current page
    /// </summary>
    public List<T> Items { get; set; } = new();

    /// <summary>
    /// Total number of items across all pages
    /// </summary>
    public int TotalCount { get; set; }

    /// <summary>
    /// Current page number (1-based)
    /// </summary>
    public int Page { get; set; }

    /// <summary>
    /// Number of items per page
    /// </summary>
    public int PageSize { get; set; }

    /// <summary>
    /// Total number of pages
    /// </summary>
    public int TotalPages => (int)Math.Ceiling((double)TotalCount / PageSize);

    /// <summary>
    /// Indicates if there is a next page
    /// </summary>
    public bool HasNextPage => Page < TotalPages;

    /// <summary>
    /// Indicates if there is a previous page
    /// </summary>
    public bool HasPreviousPage => Page > 1;
}

/// <summary>
/// Comprehensive audit log entry model (internal representation)
/// </summary>
public class AuditLogEntry
{
    public long RowId { get; set; }
    public string ActorType { get; set; } = null!;
    public long ActorId { get; set; }
    public string? ActorName { get; set; }
    public long? CompanyId { get; set; }
    public string? CompanyName { get; set; }
    public long? BranchId { get; set; }
    public string? BranchName { get; set; }
    public string Action { get; set; } = null!;
    public string EntityType { get; set; } = null!;
    public long? EntityId { get; set; }
    public string? OldValue { get; set; }
    public string? NewValue { get; set; }
    public string? IpAddress { get; set; }
    public string? UserAgent { get; set; }
    public string? CorrelationId { get; set; }
    public string? HttpMethod { get; set; }
    public string? EndpointPath { get; set; }
    public string? RequestPayload { get; set; }
    public string? ResponsePayload { get; set; }
    public long? ExecutionTimeMs { get; set; }
    public int? StatusCode { get; set; }
    public string? ExceptionType { get; set; }
    public string? ExceptionMessage { get; set; }
    public string? StackTrace { get; set; }
    public string Severity { get; set; } = "Info";
    public string EventCategory { get; set; } = "DataChange";
    public string? Metadata { get; set; }
    public string? BusinessModule { get; set; }
    public string? DeviceIdentifier { get; set; }
    public string? ErrorCode { get; set; }
    public string? BusinessDescription { get; set; }
    public DateTime CreationDate { get; set; }
}

/// <summary>
/// Comprehensive filter for querying audit logs with multiple criteria.
/// Supports filtering by date range, actor, company, branch, entity, action, and more.
/// All filter properties are optional - null values are ignored in queries.
/// </summary>
public class AuditQueryFilter
{
    /// <summary>
    /// Filter by start date (inclusive). Returns entries on or after this date.
    /// </summary>
    public DateTime? StartDate { get; set; }
    
    /// <summary>
    /// Filter by end date (inclusive). Returns entries on or before this date.
    /// </summary>
    public DateTime? EndDate { get; set; }
    
    /// <summary>
    /// Filter by actor ID (user ID or system component ID).
    /// </summary>
    public long? ActorId { get; set; }
    
    /// <summary>
    /// Filter by actor type (e.g., "User", "System", "SuperAdmin").
    /// </summary>
    public string? ActorType { get; set; }
    
    /// <summary>
    /// Filter by company ID for multi-tenant filtering.
    /// </summary>
    public long? CompanyId { get; set; }
    
    /// <summary>
    /// Filter by branch ID for multi-tenant filtering.
    /// </summary>
    public long? BranchId { get; set; }
    
    /// <summary>
    /// Filter by entity type (e.g., "SysUser", "SysCompany", "SysBranch").
    /// </summary>
    public string? EntityType { get; set; }
    
    /// <summary>
    /// Filter by entity ID to get history for a specific entity.
    /// </summary>
    public long? EntityId { get; set; }
    
    /// <summary>
    /// Filter by action type (e.g., "INSERT", "UPDATE", "DELETE", "LOGIN", "LOGOUT").
    /// </summary>
    public string? Action { get; set; }
    
    /// <summary>
    /// Filter by IP address to track actions from specific locations.
    /// </summary>
    public string? IpAddress { get; set; }
    
    /// <summary>
    /// Filter by correlation ID to trace all operations within a single request.
    /// </summary>
    public string? CorrelationId { get; set; }
    
    /// <summary>
    /// Filter by event category (e.g., "DataChange", "Authentication", "Permission", "Exception", "Configuration").
    /// </summary>
    public string? EventCategory { get; set; }
    
    /// <summary>
    /// Filter by severity level (e.g., "Critical", "Error", "Warning", "Info").
    /// </summary>
    public string? Severity { get; set; }
    
    /// <summary>
    /// Filter by HTTP method (e.g., "GET", "POST", "PUT", "DELETE").
    /// </summary>
    public string? HttpMethod { get; set; }
    
    /// <summary>
    /// Filter by endpoint path (e.g., "/api/users", "/api/companies").
    /// </summary>
    public string? EndpointPath { get; set; }
    
    /// <summary>
    /// Filter by business module (e.g., "POS", "HR", "Accounting") for legacy compatibility.
    /// </summary>
    public string? BusinessModule { get; set; }
    
    /// <summary>
    /// Filter by error code (e.g., "DB_TIMEOUT_001", "API_HR_045") for legacy compatibility.
    /// </summary>
    public string? ErrorCode { get; set; }
}

/// <summary>
/// User action replay containing a chronological sequence of user actions with full context.
/// Used for debugging, user behavior analysis, and reproducing issues.
/// </summary>
public class UserActionReplay
{
    /// <summary>
    /// The user ID for this replay.
    /// </summary>
    public long UserId { get; set; }
    
    /// <summary>
    /// The user name for this replay.
    /// </summary>
    public string UserName { get; set; } = null!;
    
    /// <summary>
    /// Start date of the replay range.
    /// </summary>
    public DateTime StartDate { get; set; }
    
    /// <summary>
    /// End date of the replay range.
    /// </summary>
    public DateTime EndDate { get; set; }
    
    /// <summary>
    /// Total number of actions performed by the user in this time range.
    /// </summary>
    public int TotalActions { get; set; }
    
    /// <summary>
    /// List of user actions in chronological order with full context.
    /// </summary>
    public List<UserAction> Actions { get; set; } = new();
    
    /// <summary>
    /// Timeline visualization data for graphical representation.
    /// </summary>
    public TimelineVisualization? Timeline { get; set; }
}

/// <summary>
/// Individual user action with full request and response context.
/// </summary>
public class UserAction
{
    /// <summary>
    /// Unique identifier for the audit log entry.
    /// </summary>
    public long AuditLogId { get; set; }
    
    /// <summary>
    /// Correlation ID for tracing this action through the system.
    /// </summary>
    public string? CorrelationId { get; set; }
    
    /// <summary>
    /// Timestamp when the action occurred.
    /// </summary>
    public DateTime Timestamp { get; set; }
    
    /// <summary>
    /// Action type (e.g., "INSERT", "UPDATE", "DELETE", "LOGIN", "LOGOUT").
    /// </summary>
    public string Action { get; set; } = null!;
    
    /// <summary>
    /// Entity type affected by this action (e.g., "SysUser", "SysCompany").
    /// </summary>
    public string EntityType { get; set; } = null!;
    
    /// <summary>
    /// Entity ID affected by this action.
    /// </summary>
    public long? EntityId { get; set; }
    
    /// <summary>
    /// HTTP method used for this action (e.g., "GET", "POST", "PUT", "DELETE").
    /// </summary>
    public string? HttpMethod { get; set; }
    
    /// <summary>
    /// Endpoint path accessed (e.g., "/api/users/123").
    /// </summary>
    public string? EndpointPath { get; set; }
    
    /// <summary>
    /// Request payload (masked for sensitive data).
    /// </summary>
    public string? RequestPayload { get; set; }
    
    /// <summary>
    /// Response payload (masked for sensitive data).
    /// </summary>
    public string? ResponsePayload { get; set; }
    
    /// <summary>
    /// HTTP status code returned (e.g., 200, 404, 500).
    /// </summary>
    public int? StatusCode { get; set; }
    
    /// <summary>
    /// Execution time in milliseconds.
    /// </summary>
    public long? ExecutionTimeMs { get; set; }
    
    /// <summary>
    /// IP address from which the action was performed.
    /// </summary>
    public string? IpAddress { get; set; }
    
    /// <summary>
    /// User agent string (browser/device information).
    /// </summary>
    public string? UserAgent { get; set; }
    
    /// <summary>
    /// Exception type if the action resulted in an error.
    /// </summary>
    public string? ExceptionType { get; set; }
    
    /// <summary>
    /// Exception message if the action resulted in an error.
    /// </summary>
    public string? ExceptionMessage { get; set; }
    
    /// <summary>
    /// Event category (e.g., "DataChange", "Authentication", "Permission").
    /// </summary>
    public string EventCategory { get; set; } = "DataChange";
    
    /// <summary>
    /// Severity level (e.g., "Critical", "Error", "Warning", "Info").
    /// </summary>
    public string Severity { get; set; } = "Info";
}

/// <summary>
/// Timeline visualization data for graphical representation of user actions.
/// Provides aggregated data for timeline charts and activity graphs.
/// </summary>
public class TimelineVisualization
{
    /// <summary>
    /// Actions grouped by hour for timeline visualization.
    /// </summary>
    public List<TimelineDataPoint> HourlyActivity { get; set; } = new();
    
    /// <summary>
    /// Actions grouped by endpoint for activity distribution.
    /// </summary>
    public Dictionary<string, int> EndpointDistribution { get; set; } = new();
    
    /// <summary>
    /// Actions grouped by action type for operation distribution.
    /// </summary>
    public Dictionary<string, int> ActionTypeDistribution { get; set; } = new();
    
    /// <summary>
    /// Actions grouped by entity type for entity access patterns.
    /// </summary>
    public Dictionary<string, int> EntityTypeDistribution { get; set; } = new();
    
    /// <summary>
    /// Peak activity hour (hour with most actions).
    /// </summary>
    public int PeakActivityHour { get; set; }
    
    /// <summary>
    /// Average execution time across all actions in milliseconds.
    /// </summary>
    public double AverageExecutionTimeMs { get; set; }
    
    /// <summary>
    /// Number of successful actions (status code 2xx).
    /// </summary>
    public int SuccessfulActions { get; set; }
    
    /// <summary>
    /// Number of failed actions (status code 4xx or 5xx).
    /// </summary>
    public int FailedActions { get; set; }
}

/// <summary>
/// Data point for timeline visualization representing activity in a specific time period.
/// </summary>
public class TimelineDataPoint
{
    /// <summary>
    /// Timestamp for this data point (typically the start of the hour).
    /// </summary>
    public DateTime Timestamp { get; set; }
    
    /// <summary>
    /// Number of actions in this time period.
    /// </summary>
    public int ActionCount { get; set; }
    
    /// <summary>
    /// Number of successful actions in this time period.
    /// </summary>
    public int SuccessCount { get; set; }
    
    /// <summary>
    /// Number of failed actions in this time period.
    /// </summary>
    public int FailureCount { get; set; }
    
    /// <summary>
    /// Average execution time for actions in this time period (milliseconds).
    /// </summary>
    public double AverageExecutionTimeMs { get; set; }
}
