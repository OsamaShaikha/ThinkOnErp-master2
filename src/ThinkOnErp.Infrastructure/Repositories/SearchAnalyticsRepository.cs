using Microsoft.Extensions.Logging;
using Oracle.ManagedDataAccess.Client;
using System.Data;
using ThinkOnErp.Domain.Entities;
using ThinkOnErp.Domain.Interfaces;
using ThinkOnErp.Infrastructure.Data;

namespace ThinkOnErp.Infrastructure.Repositories;

/// <summary>
/// Repository implementation for search analytics operations.
/// Requirements: 8.11, 19.9, 19.10
/// </summary>
public class SearchAnalyticsRepository : ISearchAnalyticsRepository
{
    private readonly OracleDbContext _context;
    private readonly ILogger<SearchAnalyticsRepository> _logger;

    public SearchAnalyticsRepository(OracleDbContext context, ILogger<SearchAnalyticsRepository> logger)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Logs a search query for analytics
    /// </summary>
    public async Task<Int64> LogSearchAsync(SysSearchAnalytics analytics)
    {
        try
        {
            using var connection = _context.CreateConnection();
            await connection.OpenAsync();

            using var command = new OracleCommand("SP_SYS_SEARCH_ANALYTICS_INSERT", connection)
            {
                CommandType = CommandType.StoredProcedure
            };

            // Output parameter
            var newIdParam = new OracleParameter("P_NEW_ID", OracleDbType.Int64, ParameterDirection.Output);
            command.Parameters.Add(newIdParam);

            // Input parameters
            command.Parameters.Add("P_USER_ID", OracleDbType.Int64).Value = analytics.UserId;
            command.Parameters.Add("P_SEARCH_TERM", OracleDbType.NVarchar2).Value = 
                (object?)analytics.SearchTerm ?? DBNull.Value;
            command.Parameters.Add("P_SEARCH_CRITERIA", OracleDbType.NClob).Value = 
                (object?)analytics.SearchCriteria ?? DBNull.Value;
            command.Parameters.Add("P_FILTER_LOGIC", OracleDbType.NVarchar2).Value = 
                (object?)analytics.FilterLogic ?? DBNull.Value;
            command.Parameters.Add("P_RESULT_COUNT", OracleDbType.Int32).Value = analytics.ResultCount;
            command.Parameters.Add("P_EXECUTION_TIME_MS", OracleDbType.Int32).Value = analytics.ExecutionTimeMs;
            command.Parameters.Add("P_COMPANY_ID", OracleDbType.Int64).Value = 
                (object?)analytics.CompanyId ?? DBNull.Value;
            command.Parameters.Add("P_BRANCH_ID", OracleDbType.Int64).Value = 
                (object?)analytics.BranchId ?? DBNull.Value;

            await command.ExecuteNonQueryAsync();

            var newId = Convert.ToInt64(newIdParam.Value.ToString());
            _logger.LogInformation("Search analytics logged with ID {AnalyticsId}", newId);

            return newId;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error logging search analytics");
            throw;
        }
    }

    /// <summary>
    /// Retrieves most popular search terms
    /// </summary>
    public async Task<List<TopSearchResult>> GetTopSearchesAsync(int daysBack = 30, int topCount = 10)
    {
        try
        {
            using var connection = _context.CreateConnection();
            await connection.OpenAsync();

            using var command = new OracleCommand("SP_SYS_SEARCH_ANALYTICS_GET_TOP_SEARCHES", connection)
            {
                CommandType = CommandType.StoredProcedure
            };

            command.Parameters.Add("P_DAYS_BACK", OracleDbType.Int32).Value = daysBack;
            command.Parameters.Add("P_TOP_COUNT", OracleDbType.Int32).Value = topCount;

            var cursorParam = new OracleParameter("P_RESULT_CURSOR", OracleDbType.RefCursor, ParameterDirection.Output);
            command.Parameters.Add(cursorParam);

            var results = new List<TopSearchResult>();

            using var reader = await command.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                results.Add(new TopSearchResult
                {
                    SearchTerm = reader.GetString(reader.GetOrdinal("SEARCH_TERM")),
                    SearchCount = reader.GetInt32(reader.GetOrdinal("SEARCH_COUNT")),
                    AvgResults = reader.IsDBNull(reader.GetOrdinal("AVG_RESULTS")) 
                        ? 0 : Convert.ToDouble(reader.GetDecimal(reader.GetOrdinal("AVG_RESULTS"))),
                    AvgExecutionTime = reader.IsDBNull(reader.GetOrdinal("AVG_EXECUTION_TIME")) 
                        ? 0 : Convert.ToDouble(reader.GetDecimal(reader.GetOrdinal("AVG_EXECUTION_TIME")))
                });
            }

            _logger.LogInformation("Retrieved {Count} top searches", results.Count);
            return results;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving top searches");
            throw;
        }
    }

    /// <summary>
    /// Retrieves search history for a specific user
    /// </summary>
    public async Task<List<SysSearchAnalytics>> GetUserSearchHistoryAsync(Int64 userId, int daysBack = 30)
    {
        try
        {
            using var connection = _context.CreateConnection();
            await connection.OpenAsync();

            using var command = new OracleCommand("SP_SYS_SEARCH_ANALYTICS_GET_USER_HISTORY", connection)
            {
                CommandType = CommandType.StoredProcedure
            };

            command.Parameters.Add("P_USER_ID", OracleDbType.Int64).Value = userId;
            command.Parameters.Add("P_DAYS_BACK", OracleDbType.Int32).Value = daysBack;

            var cursorParam = new OracleParameter("P_RESULT_CURSOR", OracleDbType.RefCursor, ParameterDirection.Output);
            command.Parameters.Add(cursorParam);

            var results = new List<SysSearchAnalytics>();

            using var reader = await command.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                results.Add(new SysSearchAnalytics
                {
                    RowId = reader.GetInt64(reader.GetOrdinal("ROW_ID")),
                    SearchTerm = reader.IsDBNull(reader.GetOrdinal("SEARCH_TERM")) 
                        ? null : reader.GetString(reader.GetOrdinal("SEARCH_TERM")),
                    SearchCriteria = reader.IsDBNull(reader.GetOrdinal("SEARCH_CRITERIA")) 
                        ? null : reader.GetString(reader.GetOrdinal("SEARCH_CRITERIA")),
                    FilterLogic = reader.IsDBNull(reader.GetOrdinal("FILTER_LOGIC")) 
                        ? null : reader.GetString(reader.GetOrdinal("FILTER_LOGIC")),
                    ResultCount = reader.GetInt32(reader.GetOrdinal("RESULT_COUNT")),
                    ExecutionTimeMs = reader.GetInt32(reader.GetOrdinal("EXECUTION_TIME_MS")),
                    SearchDate = reader.GetDateTime(reader.GetOrdinal("SEARCH_DATE"))
                });
            }

            _logger.LogInformation("Retrieved {Count} search history records for user {UserId}", 
                results.Count, userId);
            return results;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving user search history for user {UserId}", userId);
            throw;
        }
    }

    /// <summary>
    /// Retrieves search performance metrics
    /// </summary>
    public async Task<List<SearchPerformanceMetric>> GetSearchPerformanceAsync(int daysBack = 7)
    {
        try
        {
            using var connection = _context.CreateConnection();
            await connection.OpenAsync();

            using var command = new OracleCommand("SP_SYS_SEARCH_ANALYTICS_GET_PERFORMANCE", connection)
            {
                CommandType = CommandType.StoredProcedure
            };

            command.Parameters.Add("P_DAYS_BACK", OracleDbType.Int32).Value = daysBack;

            var cursorParam = new OracleParameter("P_RESULT_CURSOR", OracleDbType.RefCursor, ParameterDirection.Output);
            command.Parameters.Add(cursorParam);

            var results = new List<SearchPerformanceMetric>();

            using var reader = await command.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                results.Add(new SearchPerformanceMetric
                {
                    SearchDay = reader.GetDateTime(reader.GetOrdinal("SEARCH_DAY")),
                    TotalSearches = reader.GetInt32(reader.GetOrdinal("TOTAL_SEARCHES")),
                    AvgResults = reader.IsDBNull(reader.GetOrdinal("AVG_RESULTS")) 
                        ? 0 : Convert.ToDouble(reader.GetDecimal(reader.GetOrdinal("AVG_RESULTS"))),
                    AvgExecutionTime = reader.IsDBNull(reader.GetOrdinal("AVG_EXECUTION_TIME")) 
                        ? 0 : Convert.ToDouble(reader.GetDecimal(reader.GetOrdinal("AVG_EXECUTION_TIME"))),
                    MaxExecutionTime = reader.GetInt32(reader.GetOrdinal("MAX_EXECUTION_TIME")),
                    MinExecutionTime = reader.GetInt32(reader.GetOrdinal("MIN_EXECUTION_TIME"))
                });
            }

            _logger.LogInformation("Retrieved {Count} search performance metrics", results.Count);
            return results;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving search performance metrics");
            throw;
        }
    }
}
