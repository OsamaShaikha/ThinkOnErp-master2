using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using ThinkOnErp.Domain.Entities;
using ThinkOnErp.Domain.Interfaces;
using ThinkOnErp.Infrastructure.Data;
using ThinkOnErp.Infrastructure.Exceptions;

namespace ThinkOnErp.Infrastructure.Repositories.EfCore;

/// <summary>
/// EF Core repository implementation for SysSearchAnalytics entity using LINQ queries.
/// Implements ISearchAnalyticsRepository interface from the Domain layer.
/// Uses ThinkOnErpDbContext for database operations with complex analytics queries using GroupBy, Select, and OrderBy.
/// Requirements: 8.11, 19.9, 19.10
/// </summary>
public class SearchAnalyticsRepository : ISearchAnalyticsRepository
{
    private readonly ThinkOnErpDbContext _context;
    private readonly ILogger<SearchAnalyticsRepository> _logger;

    /// <summary>
    /// Initializes a new instance of the SearchAnalyticsRepository class.
    /// </summary>
    /// <param name="context">The EF Core database context for entity management.</param>
    /// <param name="logger">Logger for recording operations and errors.</param>
    /// <exception cref="ArgumentNullException">Thrown when context or logger is null.</exception>
    public SearchAnalyticsRepository(
        ThinkOnErpDbContext context,
        ILogger<SearchAnalyticsRepository> logger)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Logs a search query for analytics.
    /// Uses EF Core Add() and SaveChangesAsync() for insertion.
    /// </summary>
    /// <param name="analytics">The search analytics entity to log</param>
    /// <returns>The generated RowId from the sequence</returns>
    public async Task<long> LogSearchAsync(SysSearchAnalytics analytics)
    {
        return await RepositoryExceptionHandler.ExecuteWithExceptionHandlingAsync(
            async () =>
            {
                _logger.LogDebug("Logging search analytics for user {UserId}", analytics.UserId);

                // Set search date if not already set
                if (analytics.SearchDate == default)
                {
                    analytics.SearchDate = DateTime.Now;
                }

                // Add the entity to the context
                _context.SearchAnalytics.Add(analytics);

                // Save changes - EF Core will automatically retrieve the generated ID from the sequence
                await _context.SaveChangesAsync();

                _logger.LogDebug("Logged search analytics with ID: {RowId}", analytics.RowId);

                return analytics.RowId;
            },
            "LogSearchAnalytics",
            _logger);
    }

    /// <summary>
    /// Retrieves most popular search terms using complex LINQ analytics query.
    /// Uses GroupBy() to aggregate by search term, Select() to project results,
    /// and OrderBy() to sort by search count descending.
    /// Implements date range filtering and pagination using Skip() and Take().
    /// </summary>
    /// <param name="daysBack">Number of days to look back (default 30)</param>
    /// <param name="topCount">Number of top results to return (default 10)</param>
    /// <returns>List of top search results with aggregated metrics</returns>
    public async Task<List<TopSearchResult>> GetTopSearchesAsync(int daysBack = 30, int topCount = 10)
    {
        return await RepositoryExceptionHandler.ExecuteWithExceptionHandlingAsync(
            async () =>
            {
                _logger.LogDebug("Retrieving top {TopCount} searches for last {DaysBack} days", 
                    topCount, daysBack);

                // Calculate the start date for the date range filter
                var startDate = DateTime.Now.AddDays(-daysBack);

                // Complex analytics query using GroupBy, Select, and OrderBy
                var topSearches = await _context.SearchAnalytics
                    .AsNoTracking()
                    .Where(a => a.SearchDate >= startDate && !string.IsNullOrEmpty(a.SearchTerm))
                    .GroupBy(a => a.SearchTerm)
                    .Select(g => new TopSearchResult
                    {
                        SearchTerm = g.Key!,
                        SearchCount = g.Count(),
                        AvgResults = g.Average(a => a.ResultCount),
                        AvgExecutionTime = g.Average(a => a.ExecutionTimeMs)
                    })
                    .OrderByDescending(r => r.SearchCount)
                    .Take(topCount)
                    .ToListAsync();

                _logger.LogDebug("Retrieved {Count} top searches", topSearches.Count);

                return topSearches;
            },
            "GetTopSearches",
            _logger);
    }

    /// <summary>
    /// Retrieves search history for a specific user.
    /// Uses LINQ Where() clause with date range filtering and pagination.
    /// Includes eager loading of related entities (User, Company, Branch).
    /// </summary>
    /// <param name="userId">The user ID to retrieve search history for</param>
    /// <param name="daysBack">Number of days to look back (default 30)</param>
    /// <returns>List of search analytics entries for the user</returns>
    public async Task<List<SysSearchAnalytics>> GetUserSearchHistoryAsync(long userId, int daysBack = 30)
    {
        return await RepositoryExceptionHandler.ExecuteWithExceptionHandlingAsync(
            async () =>
            {
                _logger.LogDebug("Retrieving search history for user {UserId} for last {DaysBack} days", 
                    userId, daysBack);

                // Calculate the start date for the date range filter
                var startDate = DateTime.Now.AddDays(-daysBack);

                // Query with date range filtering and eager loading
                var searchHistory = await _context.SearchAnalytics
                    .AsNoTracking()
                    .Include(a => a.User)
                    .Include(a => a.Company)
                    .Include(a => a.Branch)
                    .Where(a => a.UserId == userId && a.SearchDate >= startDate)
                    .OrderByDescending(a => a.SearchDate)
                    .ToListAsync();

                _logger.LogDebug("Retrieved {Count} search history entries for user {UserId}", 
                    searchHistory.Count, userId);

                return searchHistory;
            },
            "GetUserSearchHistory",
            _logger);
    }

    /// <summary>
    /// Retrieves search performance metrics using complex LINQ analytics query.
    /// Uses GroupBy() to aggregate by date, Select() to project performance metrics,
    /// and OrderBy() to sort chronologically.
    /// Implements date range filtering using Where() clause.
    /// </summary>
    /// <param name="daysBack">Number of days to look back (default 7)</param>
    /// <returns>List of search performance metrics aggregated by day</returns>
    public async Task<List<SearchPerformanceMetric>> GetSearchPerformanceAsync(int daysBack = 7)
    {
        return await RepositoryExceptionHandler.ExecuteWithExceptionHandlingAsync(
            async () =>
            {
                _logger.LogDebug("Retrieving search performance metrics for last {DaysBack} days", daysBack);

                // Calculate the start date for the date range filter
                var startDate = DateTime.Now.AddDays(-daysBack);

                // Complex analytics query using GroupBy, Select, and OrderBy
                // Group by date (truncated to day) to aggregate daily metrics
                var performanceMetrics = await _context.SearchAnalytics
                    .AsNoTracking()
                    .Where(a => a.SearchDate >= startDate)
                    .GroupBy(a => a.SearchDate.Date)
                    .Select(g => new SearchPerformanceMetric
                    {
                        SearchDay = g.Key,
                        TotalSearches = g.Count(),
                        AvgResults = g.Average(a => a.ResultCount),
                        AvgExecutionTime = g.Average(a => a.ExecutionTimeMs),
                        MaxExecutionTime = g.Max(a => a.ExecutionTimeMs),
                        MinExecutionTime = g.Min(a => a.ExecutionTimeMs)
                    })
                    .OrderBy(m => m.SearchDay)
                    .ToListAsync();

                _logger.LogDebug("Retrieved {Count} days of search performance metrics", 
                    performanceMetrics.Count);

                return performanceMetrics;
            },
            "GetSearchPerformance",
            _logger);
    }
}
