namespace ThinkOnErp.Domain.Entities;

/// <summary>
/// Entity representing search analytics and query logging.
/// Tracks search queries for performance optimization and analytics.
/// Requirements: 8.11, 19.9, 19.10
/// </summary>
public class SysSearchAnalytics
{
    /// <summary>
    /// Unique identifier for the search analytics record
    /// </summary>
    public Int64 RowId { get; set; }

    /// <summary>
    /// User who performed the search
    /// </summary>
    public Int64 UserId { get; set; }

    /// <summary>
    /// Search term used (if any)
    /// </summary>
    public string? SearchTerm { get; set; }

    /// <summary>
    /// Complete search criteria as JSON
    /// </summary>
    public string? SearchCriteria { get; set; }

    /// <summary>
    /// Filter logic used (AND/OR)
    /// </summary>
    public string? FilterLogic { get; set; }

    /// <summary>
    /// Number of results returned
    /// </summary>
    public int ResultCount { get; set; }

    /// <summary>
    /// Execution time in milliseconds
    /// </summary>
    public int ExecutionTimeMs { get; set; }

    /// <summary>
    /// Date and time when search was performed
    /// </summary>
    public DateTime SearchDate { get; set; }

    /// <summary>
    /// Company ID (if filtered)
    /// </summary>
    public Int64? CompanyId { get; set; }

    /// <summary>
    /// Branch ID (if filtered)
    /// </summary>
    public Int64? BranchId { get; set; }

    // Navigation properties
    public SysUser? User { get; set; }
    public SysCompany? Company { get; set; }
    public SysBranch? Branch { get; set; }
}
