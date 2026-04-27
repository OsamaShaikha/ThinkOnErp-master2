namespace ThinkOnErp.Application.DTOs.Audit;

/// <summary>
/// Request DTO for querying audit logs with comprehensive filtering options
/// </summary>
public class AuditQueryRequestDto
{
    /// <summary>
    /// Filter by start date (inclusive)
    /// </summary>
    public DateTime? StartDate { get; set; }
    
    /// <summary>
    /// Filter by end date (inclusive)
    /// </summary>
    public DateTime? EndDate { get; set; }
    
    /// <summary>
    /// Filter by actor ID (user who performed the action)
    /// </summary>
    public long? ActorId { get; set; }
    
    /// <summary>
    /// Filter by actor type (SUPER_ADMIN, COMPANY_ADMIN, USER, SYSTEM)
    /// </summary>
    public string? ActorType { get; set; }
    
    /// <summary>
    /// Filter by company ID
    /// </summary>
    public long? CompanyId { get; set; }
    
    /// <summary>
    /// Filter by branch ID
    /// </summary>
    public long? BranchId { get; set; }
    
    /// <summary>
    /// Filter by entity type (User, Company, Branch, Role, etc.)
    /// </summary>
    public string? EntityType { get; set; }
    
    /// <summary>
    /// Filter by entity ID
    /// </summary>
    public long? EntityId { get; set; }
    
    /// <summary>
    /// Filter by action (INSERT, UPDATE, DELETE, LOGIN, LOGOUT, etc.)
    /// </summary>
    public string? Action { get; set; }
    
    /// <summary>
    /// Filter by event category (DataChange, Authentication, Permission, Exception, Configuration, Request)
    /// </summary>
    public string? EventCategory { get; set; }
    
    /// <summary>
    /// Filter by severity (Critical, Error, Warning, Info)
    /// </summary>
    public string? Severity { get; set; }
    
    /// <summary>
    /// Filter by IP address
    /// </summary>
    public string? IpAddress { get; set; }
    
    /// <summary>
    /// Filter by correlation ID (exact match)
    /// </summary>
    public string? CorrelationId { get; set; }
    
    /// <summary>
    /// Filter by HTTP method (GET, POST, PUT, DELETE, etc.)
    /// </summary>
    public string? HttpMethod { get; set; }
    
    /// <summary>
    /// Filter by endpoint path (supports partial matching)
    /// </summary>
    public string? EndpointPath { get; set; }
    
    /// <summary>
    /// Filter by exception type (for error events)
    /// </summary>
    public string? ExceptionType { get; set; }
    
    /// <summary>
    /// Page number for pagination (default: 1)
    /// </summary>
    public int Page { get; set; } = 1;
    
    /// <summary>
    /// Records per page (default: 50, max: 100)
    /// </summary>
    public int PageSize { get; set; } = 50;
}

/// <summary>
/// Generic pagination options for API responses
/// </summary>
public class PaginationOptionsDto
{
    /// <summary>
    /// Page number (1-based)
    /// </summary>
    public int PageNumber { get; set; } = 1;
    
    /// <summary>
    /// Number of items per page
    /// </summary>
    public int PageSize { get; set; } = 50;
    
    /// <summary>
    /// Calculate the number of items to skip
    /// </summary>
    public int Skip => (PageNumber - 1) * PageSize;
}

/// <summary>
/// Generic paged result wrapper for API responses
/// </summary>
/// <typeparam name="T">Type of items in the result</typeparam>
public class PagedResultDto<T>
{
    /// <summary>
    /// Items in the current page
    /// </summary>
    public IEnumerable<T> Items { get; set; } = Enumerable.Empty<T>();
    
    /// <summary>
    /// Total number of items across all pages
    /// </summary>
    public int TotalCount { get; set; }
    
    /// <summary>
    /// Current page number
    /// </summary>
    public int PageNumber { get; set; }
    
    /// <summary>
    /// Number of items per page
    /// </summary>
    public int PageSize { get; set; }
    
    /// <summary>
    /// Total number of pages
    /// </summary>
    public int TotalPages => (int)Math.Ceiling(TotalCount / (double)PageSize);
    
    /// <summary>
    /// Whether there is a previous page
    /// </summary>
    public bool HasPreviousPage => PageNumber > 1;
    
    /// <summary>
    /// Whether there is a next page
    /// </summary>
    public bool HasNextPage => PageNumber < TotalPages;
}

/// <summary>
/// Request DTO for exporting audit logs
/// </summary>
public class AuditExportRequestDto
{
    /// <summary>
    /// Export format (CSV, JSON, PDF)
    /// </summary>
    public string Format { get; set; } = "CSV";
    
    /// <summary>
    /// Filter criteria for the export
    /// </summary>
    public AuditQueryRequestDto Filter { get; set; } = new();
    
    /// <summary>
    /// Maximum number of records to export (default: 10000)
    /// </summary>
    public int MaxRecords { get; set; } = 10000;
    
    /// <summary>
    /// Whether to include sensitive data in the export (requires special permissions)
    /// </summary>
    public bool IncludeSensitiveData { get; set; } = false;
}

/// <summary>
/// Request DTO for full-text search across audit logs
/// </summary>
public class AuditSearchRequestDto
{
    /// <summary>
    /// Search term to find in audit log fields
    /// </summary>
    public string SearchTerm { get; set; } = null!;
    
    /// <summary>
    /// Optional date range filter
    /// </summary>
    public DateTime? StartDate { get; set; }
    
    /// <summary>
    /// Optional date range filter
    /// </summary>
    public DateTime? EndDate { get; set; }
    
    /// <summary>
    /// Optional company filter
    /// </summary>
    public long? CompanyId { get; set; }
    
    /// <summary>
    /// Optional branch filter
    /// </summary>
    public long? BranchId { get; set; }
    
    /// <summary>
    /// Page number for pagination (default: 1)
    /// </summary>
    public int Page { get; set; } = 1;
    
    /// <summary>
    /// Records per page (default: 50)
    /// </summary>
    public int PageSize { get; set; } = 50;
}
