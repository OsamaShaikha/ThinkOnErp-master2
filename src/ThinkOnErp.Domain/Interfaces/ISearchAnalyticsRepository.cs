using ThinkOnErp.Domain.Entities;

namespace ThinkOnErp.Domain.Interfaces;

/// <summary>
/// Repository interface for search analytics operations.
/// Requirements: 8.11, 19.9, 19.10
/// </summary>
public interface ISearchAnalyticsRepository
{
    /// <summary>
    /// Logs a search query for analytics
    /// </summary>
    Task<Int64> LogSearchAsync(SysSearchAnalytics analytics);

    /// <summary>
    /// Retrieves most popular search terms
    /// </summary>
    Task<List<TopSearchResult>> GetTopSearchesAsync(int daysBack = 30, int topCount = 10);

    /// <summary>
    /// Retrieves search history for a specific user
    /// </summary>
    Task<List<SysSearchAnalytics>> GetUserSearchHistoryAsync(Int64 userId, int daysBack = 30);

    /// <summary>
    /// Retrieves search performance metrics
    /// </summary>
    Task<List<SearchPerformanceMetric>> GetSearchPerformanceAsync(int daysBack = 7);
}

/// <summary>
/// Result model for top searches
/// </summary>
public class TopSearchResult
{
    public string SearchTerm { get; set; } = string.Empty;
    public int SearchCount { get; set; }
    public double AvgResults { get; set; }
    public double AvgExecutionTime { get; set; }
}

/// <summary>
/// Result model for search performance metrics
/// </summary>
public class SearchPerformanceMetric
{
    public DateTime SearchDay { get; set; }
    public int TotalSearches { get; set; }
    public double AvgResults { get; set; }
    public double AvgExecutionTime { get; set; }
    public int MaxExecutionTime { get; set; }
    public int MinExecutionTime { get; set; }
}
