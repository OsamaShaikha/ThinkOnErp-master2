namespace ThinkOnErp.Application.DTOs.Audit;

/// <summary>
/// Legacy audit log DTO that matches the exact format from logs.png
/// Provides backward compatibility with existing UI components
/// </summary>
public class LegacyAuditLogDto
{
    /// <summary>
    /// Unique identifier of the audit log entry
    /// </summary>
    public long Id { get; set; }
    
    /// <summary>
    /// Human-readable error description (matches "Error Description" column in logs.png)
    /// </summary>
    public string ErrorDescription { get; set; } = null!;
    
    /// <summary>
    /// Business module (matches "Module" column in logs.png)
    /// Examples: POS, HR, Accounting, Inventory, Sales
    /// </summary>
    public string Module { get; set; } = null!;
    
    /// <summary>
    /// Company name (matches "Company" column in logs.png)
    /// </summary>
    public string Company { get; set; } = null!;
    
    /// <summary>
    /// Branch name (matches "Branch" column in logs.png)
    /// </summary>
    public string Branch { get; set; } = null!;
    
    /// <summary>
    /// User name (matches "User" column in logs.png)
    /// </summary>
    public string User { get; set; } = null!;
    
    /// <summary>
    /// Device identifier (matches "Device" column in logs.png)
    /// Examples: POS Terminal 03, Desktop-HR-02, Mobile-Sales-01
    /// </summary>
    public string Device { get; set; } = null!;
    
    /// <summary>
    /// Date and time of the event (matches "Date & Time" column in logs.png)
    /// </summary>
    public DateTime DateTime { get; set; }
    
    /// <summary>
    /// Status of the error (matches "Status" column in logs.png)
    /// Values: Unresolved, In Progress, Resolved, Critical Errors
    /// </summary>
    public string Status { get; set; } = null!;
    
    /// <summary>
    /// Whether the current user can resolve this error
    /// </summary>
    public bool CanResolve { get; set; }
    
    /// <summary>
    /// Whether the current user can delete this error
    /// </summary>
    public bool CanDelete { get; set; }
    
    /// <summary>
    /// Whether the current user can view detailed information
    /// </summary>
    public bool CanViewDetails { get; set; }
    
    /// <summary>
    /// Standardized error code for categorization
    /// Examples: DB_TIMEOUT_001, API_HR_045, AUTH_FAILED_003
    /// </summary>
    public string? ErrorCode { get; set; }
    
    /// <summary>
    /// Correlation ID for detailed tracing and debugging
    /// </summary>
    public string? CorrelationId { get; set; }
}

/// <summary>
/// Dashboard counters that match the top section of logs.png
/// Provides summary statistics for the audit log dashboard
/// </summary>
public class LegacyDashboardCountersDto
{
    /// <summary>
    /// Number of unresolved errors (red circle with count in logs.png)
    /// </summary>
    public int UnresolvedCount { get; set; }
    
    /// <summary>
    /// Number of errors in progress (orange circle with count in logs.png)
    /// </summary>
    public int InProgressCount { get; set; }
    
    /// <summary>
    /// Number of resolved errors (green circle with count in logs.png)
    /// </summary>
    public int ResolvedCount { get; set; }
    
    /// <summary>
    /// Number of critical errors (dark red circle with count in logs.png)
    /// </summary>
    public int CriticalErrorsCount { get; set; }
}

/// <summary>
/// Filter for legacy audit logs view
/// Supports all filtering options shown in logs.png interface
/// </summary>
public class LegacyAuditLogFilterDto
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
    /// Filter by status (Unresolved, In Progress, Resolved, Critical Errors)
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
    /// Search term for filtering by description, user, device, or error code
    /// </summary>
    public string? SearchTerm { get; set; }
    
    /// <summary>
    /// Page number for pagination (default: 1)
    /// </summary>
    public int Page { get; set; } = 1;
    
    /// <summary>
    /// Records per page (default: 50)
    /// </summary>
    public int PageSize { get; set; } = 50;
}
