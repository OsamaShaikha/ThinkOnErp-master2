using System.Text;
using System.Text.Json;
using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Options;
using ThinkOnErp.Domain.Entities;
using ThinkOnErp.Domain.Interfaces;
using ThinkOnErp.Domain.Models;
using ThinkOnErp.Infrastructure.Data;
using ThinkOnErp.Infrastructure.Configuration;
using System.Security.Cryptography;

namespace ThinkOnErp.Infrastructure.Services;

/// <summary>
/// Service for querying and filtering audit log data with comprehensive filtering and pagination support.
/// Implements IAuditQueryService to provide efficient audit data access for compliance reporting,
/// debugging, and user activity analysis.
/// Includes Redis-based caching for improved query performance.
/// Enforces role-based access control (RBAC) to ensure users only see audit data they're authorized to access.
/// </summary>
public class AuditQueryService : IAuditQueryService
{
    private readonly IAuditRepository _auditRepository;
    private readonly OracleDbContext _dbContext;
    private readonly IServiceScopeFactory _serviceScopeFactory;
    private readonly ILogger<AuditQueryService> _logger;
    private readonly IDistributedCache? _cache;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private bool? _isOracleTextAvailable;
    private readonly SemaphoreSlim _oracleTextCheckLock = new SemaphoreSlim(1, 1);
    
    // Cache configuration
    private readonly TimeSpan _cacheDuration;
    private readonly bool _cachingEnabled;
    
    // Parallel query configuration
    private readonly bool _parallelQueriesEnabled;
    private readonly int _parallelQueryThresholdDays;
    private readonly int _parallelQueryChunkSizeDays;
    private readonly int _maxParallelQueries;
    
    public AuditQueryService(
        IAuditRepository auditRepository,
        OracleDbContext dbContext,
        IServiceScopeFactory serviceScopeFactory,
        ILogger<AuditQueryService> logger,
        IHttpContextAccessor httpContextAccessor,
        IOptions<AuditQueryCachingOptions> cachingOptions,
        IDistributedCache? cache = null)
    {
        _auditRepository = auditRepository ?? throw new ArgumentNullException(nameof(auditRepository));
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
        _serviceScopeFactory = serviceScopeFactory ?? throw new ArgumentNullException(nameof(serviceScopeFactory));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _httpContextAccessor = httpContextAccessor ?? throw new ArgumentNullException(nameof(httpContextAccessor));
        
        var options = cachingOptions?.Value ?? new AuditQueryCachingOptions();
        _cachingEnabled = options.Enabled && cache != null;
        _cacheDuration = options.CacheDuration;
        _cache = cache;
        
        // Initialize parallel query configuration
        _parallelQueriesEnabled = options.ParallelQueriesEnabled;
        _parallelQueryThresholdDays = options.ParallelQueryThresholdDays;
        _parallelQueryChunkSizeDays = options.ParallelQueryChunkSizeDays;
        _maxParallelQueries = options.MaxParallelQueries;
        
        if (_cachingEnabled)
        {
            _logger.LogInformation("AuditQueryService initialized with Redis caching enabled (TTL: {CacheDuration})", _cacheDuration);
        }
        else
        {
            _logger.LogInformation("AuditQueryService initialized without caching");
        }
        
        if (_parallelQueriesEnabled)
        {
            _logger.LogInformation("AuditQueryService initialized with parallel queries enabled " +
                "(Threshold: {ThresholdDays} days, Chunk Size: {ChunkSizeDays} days, Max Parallel: {MaxParallel})",
                _parallelQueryThresholdDays, _parallelQueryChunkSizeDays, _maxParallelQueries);
        }
        else
        {
            _logger.LogInformation("AuditQueryService initialized with parallel queries disabled");
        }
    }

    /// <summary>
    /// Query audit logs with comprehensive filtering and pagination.
    /// Supports filtering by date range, actor, company, branch, entity type, action type, and more.
    /// Results are cached in Redis for improved performance.
    /// Uses parallel query execution for large date ranges to improve performance.
    /// </summary>
    public async Task<PagedResult<AuditLogEntry>> QueryAsync(
        AuditQueryFilter filter,
        PaginationOptions pagination,
        CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogDebug("Querying audit logs with filter: {@Filter}, pagination: {@Pagination}", filter, pagination);

            // Try to get from cache first (cache-aside pattern)
            if (_cachingEnabled)
            {
                var cacheKey = GenerateCacheKey("query", filter, pagination);
                var cachedResult = await GetFromCacheAsync<PagedResult<AuditLogEntry>>(cacheKey, cancellationToken);
                
                if (cachedResult != null)
                {
                    _logger.LogDebug("Cache HIT for audit query: {CacheKey}", cacheKey);
                    return cachedResult;
                }
                
                _logger.LogDebug("Cache MISS for audit query: {CacheKey}", cacheKey);
            }

            // Check if parallel query execution should be used
            var shouldUseParallelQuery = ShouldUseParallelQuery(filter);

            PagedResult<AuditLogEntry> result;

            if (shouldUseParallelQuery)
            {
                _logger.LogDebug("Using parallel query execution for large date range");
                result = await ExecuteParallelQueryAsync(filter, pagination, cancellationToken);
            }
            else
            {
                // Cache miss or caching disabled - query database using single query
                result = await ExecuteSingleQueryAsync(filter, pagination, cancellationToken);
            }

            // Store in cache for future requests
            if (_cachingEnabled)
            {
                var cacheKey = GenerateCacheKey("query", filter, pagination);
                await SetInCacheAsync(cacheKey, result, _cacheDuration, cancellationToken);
            }

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error querying audit logs with filter: {@Filter}", filter);
            throw;
        }
    }

    /// <summary>
    /// Get all audit log entries associated with a specific correlation ID.
    /// Used for request tracing to track all operations within a single API request.
    /// </summary>
    public async Task<IEnumerable<AuditLogEntry>> GetByCorrelationIdAsync(
        string correlationId,
        CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogDebug("Getting audit logs by correlation ID: {CorrelationId}", correlationId);

            var sysAuditLogs = await _auditRepository.GetByCorrelationIdAsync(correlationId, cancellationToken);
            return sysAuditLogs.Select(MapToAuditLogEntry);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting audit logs by correlation ID: {CorrelationId}", correlationId);
            throw;
        }
    }

    /// <summary>
    /// Get the complete audit history for a specific entity.
    /// Returns all modifications (INSERT, UPDATE, DELETE) for the entity in chronological order.
    /// </summary>
    public async Task<IEnumerable<AuditLogEntry>> GetByEntityAsync(
        string entityType,
        long entityId,
        CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogDebug("Getting audit logs by entity: {EntityType} {EntityId}", entityType, entityId);

            var sysAuditLogs = await _auditRepository.GetByEntityAsync(entityType, entityId, cancellationToken);
            return sysAuditLogs.Select(MapToAuditLogEntry);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting audit logs by entity: {EntityType} {EntityId}", entityType, entityId);
            throw;
        }
    }

    /// <summary>
    /// Get all actions performed by a specific actor within a date range.
    /// Returns entries in chronological order for user activity analysis.
    /// Uses parallel query execution for large date ranges to improve performance.
    /// </summary>
    public async Task<IEnumerable<AuditLogEntry>> GetByActorAsync(
        long actorId,
        DateTime startDate,
        DateTime endDate,
        CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogDebug("Getting audit logs by actor: {ActorId} from {StartDate} to {EndDate}", 
                actorId, startDate, endDate);

            // Check if parallel query execution should be used
            var dateRangeDays = (endDate - startDate).TotalDays;
            
            if (_parallelQueriesEnabled && dateRangeDays >= _parallelQueryThresholdDays)
            {
                _logger.LogDebug("Using parallel query execution for large date range: {DateRangeDays} days", dateRangeDays);
                return await ExecuteParallelActorQueryAsync(actorId, startDate, endDate, cancellationToken);
            }

            // Use single query for small date ranges
            _logger.LogDebug("Using single query execution for date range: {DateRangeDays} days", dateRangeDays);
            return await ExecuteSingleActorQueryAsync(actorId, startDate, endDate, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting audit logs by actor: {ActorId}", actorId);
            throw;
        }
    }

    /// <summary>
    /// Perform full-text search across all audit log fields.
    /// Uses Oracle Text CONTAINS operator if available, otherwise falls back to LIKE queries.
    /// Supports advanced search features: phrase search ("exact phrase"), boolean operators (AND, OR, NOT),
    /// wildcards (%), fuzzy matching (fuzzy(term)), and proximity search (NEAR).
    /// Results are cached in Redis for improved performance.
    /// </summary>
    /// <param name="searchTerm">Search term or expression. Examples:
    /// - Simple: "error"
    /// - Phrase: "database timeout"
    /// - Boolean: "error AND database"
    /// - Wildcard: "data%"
    /// - Fuzzy: "fuzzy(error)"
    /// </param>
    /// <param name="pagination">Pagination options</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Paged search results</returns>
    public async Task<PagedResult<AuditLogEntry>> SearchAsync(
        string searchTerm,
        PaginationOptions pagination,
        CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogDebug("Searching audit logs with term: {SearchTerm}, pagination: {@Pagination}", 
                searchTerm, pagination);

            // Try to get from cache first (cache-aside pattern)
            if (_cachingEnabled)
            {
                var cacheKey = GenerateCacheKey("search", searchTerm, pagination);
                var cachedResult = await GetFromCacheAsync<PagedResult<AuditLogEntry>>(cacheKey, cancellationToken);
                
                if (cachedResult != null)
                {
                    _logger.LogDebug("Cache HIT for audit search: {CacheKey}", cacheKey);
                    return cachedResult;
                }
                
                _logger.LogDebug("Cache MISS for audit search: {CacheKey}", cacheKey);
            }

            // Cache miss or caching disabled - query database using EF Core LINQ
            // Check if Oracle Text is available
            var isOracleTextAvailable = await IsOracleTextAvailableAsync(cancellationToken);

            // Apply role-based filtering first
            var userContext = GetUserAccessContext();

            IQueryable<SysAuditLog> searchQuery;

            if (isOracleTextAvailable)
            {
                _logger.LogDebug("Using Oracle Text CONTAINS for search");

                var oracleTextQuery = TransformSearchTermForOracleText(searchTerm);

                searchQuery = _dbContext.SysAuditLogs
                    .FromSqlRaw($@"SELECT * FROM SYS_AUDIT_LOG WHERE CONTAINS(BUSINESS_DESCRIPTION, {{0}}) > 0", oracleTextQuery);
            }
            else
            {
                _logger.LogDebug("Oracle Text not available, falling back to LIKE queries");

                var searchPattern = $"%{searchTerm.ToUpper()}%";

                searchQuery = _dbContext.SysAuditLogs.Where(a =>
                    a.BusinessDescription!.ToUpper().Contains(searchPattern) ||
                    a.ExceptionMessage!.ToUpper().Contains(searchPattern) ||
                    a.EntityType.ToUpper().Contains(searchPattern) ||
                    a.Action.ToUpper().Contains(searchPattern) ||
                    a.ActorType.ToUpper().Contains(searchPattern) ||
                    a.ErrorCode!.ToUpper().Contains(searchPattern) ||
                    a.BusinessModule!.ToUpper().Contains(searchPattern) ||
                    a.EndpointPath!.ToUpper().Contains(searchPattern) ||
                    a.CorrelationId!.ToUpper().Contains(searchPattern));
            }

            // Apply role-based filtering
            searchQuery = ApplyRoleBasedFiltering(searchQuery, userContext);

            // Get total count
            var totalCount = await searchQuery.CountAsync(cancellationToken);

            // Get paged results
            var items = await searchQuery
                .OrderByDescending(a => a.CreationDate)
                .Skip(pagination.Skip)
                .Take(pagination.PageSize)
                .ToListAsync(cancellationToken);

            var result = new PagedResult<AuditLogEntry>
            {
                Items = items.Select(MapToAuditLogEntry).ToList(),
                TotalCount = totalCount,
                Page = pagination.PageNumber,
                PageSize = pagination.PageSize
            };

            // Store in cache for future requests
            if (_cachingEnabled)
            {
                var cacheKey = GenerateCacheKey("search", searchTerm, pagination);
                await SetInCacheAsync(cacheKey, result, _cacheDuration, cancellationToken);
            }

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error searching audit logs with term: {SearchTerm}", searchTerm);
            throw;
        }
    }

    /// <summary>
    /// Get a complete replay of all actions performed by a user within a date range.
    /// Returns actions in chronological order with full request context for debugging and analysis.
    /// </summary>
    public async Task<UserActionReplay> GetUserActionReplayAsync(
        long userId,
        DateTime startDate,
        DateTime endDate,
        CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogDebug("Getting user action replay for user: {UserId} from {StartDate} to {EndDate}", 
                userId, startDate, endDate);

            var auditLogs = await GetByActorAsync(userId, startDate, endDate, cancellationToken);
            var auditLogList = auditLogs.ToList();

            var userActions = auditLogList.Select(log => new UserAction
            {
                AuditLogId = log.Id,
                CorrelationId = log.CorrelationId,
                Timestamp = log.CreationDate,
                Action = log.Action,
                EntityType = log.EntityType,
                EntityId = log.EntityId,
                HttpMethod = log.HttpMethod,
                EndpointPath = log.EndpointPath,
                RequestPayload = log.RequestPayload,
                ResponsePayload = log.ResponsePayload,
                StatusCode = log.StatusCode,
                ExecutionTimeMs = log.ExecutionTimeMs,
                IpAddress = log.IpAddress,
                UserAgent = log.UserAgent,
                ExceptionType = log.ExceptionType,
                ExceptionMessage = log.ExceptionMessage,
                EventCategory = log.EventCategory,
                Severity = log.Severity
            }).ToList();

            // Build timeline visualization
            var timeline = BuildTimelineVisualization(userActions);

            return new UserActionReplay
            {
                UserId = userId,
                UserName = auditLogList.FirstOrDefault()?.ActorName ?? "Unknown",
                StartDate = startDate,
                EndDate = endDate,
                TotalActions = userActions.Count,
                Actions = userActions,
                Timeline = timeline
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting user action replay for user: {UserId}", userId);
            throw;
        }
    }

    /// <summary>
    /// Export audit logs to CSV format based on filter criteria.
    /// </summary>
    public async Task<byte[]> ExportToCsvAsync(
        AuditQueryFilter filter,
        CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogDebug("Exporting audit logs to CSV with filter: {@Filter}", filter);

            // Query all matching records (without pagination)
            var allRecords = await QueryAllAsync(filter, cancellationToken);

            var csv = new StringBuilder();
            
            // CSV Header
            csv.AppendLine("ID,Date,Actor Type,Actor ID,Company ID,Branch ID,Action,Entity Type,Entity ID," +
                          "Event Category,Severity,HTTP Method,Endpoint,Status Code,Execution Time (ms)," +
                          "IP Address,Correlation ID,Business Module,Error Code,Description");

            // CSV Rows
            foreach (var record in allRecords)
            {
                csv.AppendLine($"{record.Id}," +
                              $"\"{record.CreationDate:yyyy-MM-dd HH:mm:ss}\"," +
                              $"\"{EscapeCsv(record.ActorType)}\"," +
                              $"{record.ActorId}," +
                              $"{record.CompanyId?.ToString() ?? ""}," +
                              $"{record.BranchId?.ToString() ?? ""}," +
                              $"\"{EscapeCsv(record.Action)}\"," +
                              $"\"{EscapeCsv(record.EntityType)}\"," +
                              $"{record.EntityId?.ToString() ?? ""}," +
                              $"\"{EscapeCsv(record.EventCategory)}\"," +
                              $"\"{EscapeCsv(record.Severity)}\"," +
                              $"\"{EscapeCsv(record.HttpMethod)}\"," +
                              $"\"{EscapeCsv(record.EndpointPath)}\"," +
                              $"{record.StatusCode?.ToString() ?? ""}," +
                              $"{record.ExecutionTimeMs?.ToString() ?? ""}," +
                              $"\"{EscapeCsv(record.IpAddress)}\"," +
                              $"\"{EscapeCsv(record.CorrelationId)}\"," +
                              $"\"{EscapeCsv(record.BusinessModule)}\"," +
                              $"\"{EscapeCsv(record.ErrorCode)}\"," +
                              $"\"{EscapeCsv(record.BusinessDescription)}\"");
            }

            return Encoding.UTF8.GetBytes(csv.ToString());
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error exporting audit logs to CSV");
            throw;
        }
    }

    /// <summary>
    /// Export audit logs to JSON format based on filter criteria.
    /// </summary>
    public async Task<string> ExportToJsonAsync(
        AuditQueryFilter filter,
        CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogDebug("Exporting audit logs to JSON with filter: {@Filter}", filter);

            // Query all matching records (without pagination)
            var allRecords = await QueryAllAsync(filter, cancellationToken);

            return JsonSerializer.Serialize(allRecords, new JsonSerializerOptions
            {
                WriteIndented = true,
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error exporting audit logs to JSON");
            throw;
        }
    }

    #region Private Helper Methods

    /// <summary>
    /// Executes a single query for actor audit logs (used for small date ranges).
    /// </summary>
    private async Task<List<AuditLogEntry>> ExecuteSingleActorQueryAsync(
        long actorId,
        DateTime startDate,
        DateTime endDate,
        CancellationToken cancellationToken)
    {
        var results = await _dbContext.SysAuditLogs
            .AsNoTracking()
            .Where(a => a.ActorId == actorId
                     && a.CreationDate >= startDate
                     && a.CreationDate <= endDate)
            .OrderBy(a => a.CreationDate)
            .ToListAsync(cancellationToken);

        return results.Select(MapToAuditLogEntry).ToList();
    }

    /// <summary>
    /// Executes parallel queries for actor audit logs (used for large date ranges).
    /// Splits the date range into chunks and queries them in parallel for improved performance.
    /// </summary>
    private async Task<List<AuditLogEntry>> ExecuteParallelActorQueryAsync(
        long actorId,
        DateTime startDate,
        DateTime endDate,
        CancellationToken cancellationToken)
    {
        var dateChunks = SplitDateRangeIntoChunks(startDate, endDate, _parallelQueryChunkSizeDays);

        _logger.LogDebug("Split date range into {ChunkCount} chunks for parallel execution", dateChunks.Count);

        var allResults = new List<AuditLogEntry>();
        var semaphore = new SemaphoreSlim(_maxParallelQueries);
        var tasks = new List<Task<List<AuditLogEntry>>>();

        foreach (var chunk in dateChunks)
        {
            await semaphore.WaitAsync(cancellationToken);

            var task = Task.Run(async () =>
            {
                try
                {
                    _logger.LogTrace("Executing parallel query chunk: {ChunkStart} to {ChunkEnd}",
                        chunk.StartDate, chunk.EndDate);

                    using var scope = _serviceScopeFactory.CreateScope();
                    var context = scope.ServiceProvider.GetRequiredService<OracleDbContext>();

                    var results = await context.SysAuditLogs
                        .AsNoTracking()
                        .Where(a => a.ActorId == actorId
                                 && a.CreationDate >= chunk.StartDate
                                 && a.CreationDate <= chunk.EndDate)
                        .OrderBy(a => a.CreationDate)
                        .ToListAsync(cancellationToken);

                    return results.Select(MapToAuditLogEntry).ToList();
                }
                finally
                {
                    semaphore.Release();
                }
            }, cancellationToken);

            tasks.Add(task);
        }

        var chunkResults = await Task.WhenAll(tasks);

        foreach (var chunkResult in chunkResults)
        {
            allResults.AddRange(chunkResult);
        }

        allResults.Sort((a, b) => a.CreationDate.CompareTo(b.CreationDate));

        _logger.LogDebug("Parallel query execution completed. Total results: {ResultCount}", allResults.Count);

        return allResults;
    }

    /// <summary>
    /// Splits a date range into smaller chunks for parallel query execution.
    /// </summary>
    /// <param name="startDate">Start date of the range</param>
    /// <param name="endDate">End date of the range</param>
    /// <param name="chunkSizeDays">Size of each chunk in days</param>
    /// <returns>List of date range chunks</returns>
    private List<DateRangeChunk> SplitDateRangeIntoChunks(
        DateTime startDate, 
        DateTime endDate, 
        int chunkSizeDays)
    {
        var chunks = new List<DateRangeChunk>();
        var currentStart = startDate;

        while (currentStart < endDate)
        {
            var currentEnd = currentStart.AddDays(chunkSizeDays);
            
            // Ensure the last chunk doesn't exceed the end date
            if (currentEnd > endDate)
            {
                currentEnd = endDate;
            }

            chunks.Add(new DateRangeChunk
            {
                StartDate = currentStart,
                EndDate = currentEnd
            });

            currentStart = currentEnd;
        }

        return chunks;
    }

    /// <summary>
    /// Represents a date range chunk for parallel query execution.
    /// </summary>
    private class DateRangeChunk
    {
        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }
    }

    /// <summary>
    /// Determines if parallel query execution should be used based on the filter criteria.
    /// </summary>
    private bool ShouldUseParallelQuery(AuditQueryFilter filter)
    {
        if (!_parallelQueriesEnabled)
        {
            return false;
        }

        // Parallel queries only make sense when filtering by date range
        if (!filter.StartDate.HasValue || !filter.EndDate.HasValue)
        {
            return false;
        }

        var dateRangeDays = (filter.EndDate.Value - filter.StartDate.Value).TotalDays;
        return dateRangeDays >= _parallelQueryThresholdDays;
    }

    /// <summary>
    /// Executes a single query with filtering and pagination (used for small date ranges).
    /// </summary>
    private async Task<PagedResult<AuditLogEntry>> ExecuteSingleQueryAsync(
        AuditQueryFilter filter,
        PaginationOptions pagination,
        CancellationToken cancellationToken)
    {
        var query = ApplyQueryFilters(_dbContext.SysAuditLogs.AsQueryable(), filter);

        var totalCount = await query.CountAsync(cancellationToken);

        var items = await query
            .OrderByDescending(a => a.CreationDate)
            .Skip(pagination.Skip)
            .Take(pagination.PageSize)
            .ToListAsync(cancellationToken);

        return new PagedResult<AuditLogEntry>
        {
            Items = items.Select(MapToAuditLogEntry).ToList(),
            TotalCount = totalCount,
            Page = pagination.PageNumber,
            PageSize = pagination.PageSize
        };
    }

    /// <summary>
    /// Executes parallel queries with filtering and pagination (used for large date ranges).
    /// Splits the date range into chunks and queries them in parallel for improved performance.
    /// </summary>
    private async Task<PagedResult<AuditLogEntry>> ExecuteParallelQueryAsync(
        AuditQueryFilter filter,
        PaginationOptions pagination,
        CancellationToken cancellationToken)
    {
        if (!filter.StartDate.HasValue || !filter.EndDate.HasValue)
        {
            throw new InvalidOperationException("Parallel query execution requires both StartDate and EndDate in the filter");
        }

        var dateChunks = SplitDateRangeIntoChunks(
            filter.StartDate.Value,
            filter.EndDate.Value,
            _parallelQueryChunkSizeDays);

        _logger.LogDebug("Split date range into {ChunkCount} chunks for parallel execution", dateChunks.Count);

        var semaphore = new SemaphoreSlim(_maxParallelQueries);
        var countTasks = new List<Task<int>>();

        foreach (var chunk in dateChunks)
        {
            await semaphore.WaitAsync(cancellationToken);

            var chunkFilter = CloneFilterWithDateRange(filter, chunk.StartDate, chunk.EndDate);

            var countTask = Task.Run(async () =>
            {
                try
                {
                    using var scope = _serviceScopeFactory.CreateScope();
                    var context = scope.ServiceProvider.GetRequiredService<OracleDbContext>();
                    var query = ApplyQueryFilters(context.SysAuditLogs.AsQueryable(), chunkFilter);
                    return await query.CountAsync(cancellationToken);
                }
                finally
                {
                    semaphore.Release();
                }
            }, cancellationToken);

            countTasks.Add(countTask);
        }

        var chunkCounts = await Task.WhenAll(countTasks);
        var totalCount = chunkCounts.Sum();

        _logger.LogDebug("Parallel count queries completed. Total count: {TotalCount}", totalCount);

        var dataTasks = new List<Task<List<AuditLogEntry>>>();

        foreach (var chunk in dateChunks)
        {
            await semaphore.WaitAsync(cancellationToken);

            var chunkFilter = CloneFilterWithDateRange(filter, chunk.StartDate, chunk.EndDate);

            var dataTask = Task.Run(async () =>
            {
                try
                {
                    _logger.LogTrace("Executing parallel data query chunk: {ChunkStart} to {ChunkEnd}",
                        chunk.StartDate, chunk.EndDate);

                    using var scope = _serviceScopeFactory.CreateScope();
                    var context = scope.ServiceProvider.GetRequiredService<OracleDbContext>();
                    var query = ApplyQueryFilters(context.SysAuditLogs.AsQueryable(), chunkFilter);

                    var results = await query
                        .OrderByDescending(a => a.CreationDate)
                        .ToListAsync(cancellationToken);

                    return results.Select(MapToAuditLogEntry).ToList();
                }
                finally
                {
                    semaphore.Release();
                }
            }, cancellationToken);

            dataTasks.Add(dataTask);
        }

        var chunkResults = await Task.WhenAll(dataTasks);

        var allResults = new List<AuditLogEntry>();
        foreach (var chunkResult in chunkResults)
        {
            allResults.AddRange(chunkResult);
        }

        allResults.Sort((a, b) => b.CreationDate.CompareTo(a.CreationDate));

        var pagedResults = allResults
            .Skip(pagination.Skip)
            .Take(pagination.PageSize)
            .ToList();

        _logger.LogDebug("Parallel query execution completed. Total results: {TotalCount}, Page results: {PageCount}",
            totalCount, pagedResults.Count);

        return new PagedResult<AuditLogEntry>
        {
            Items = pagedResults,
            TotalCount = totalCount,
            Page = pagination.PageNumber,
            PageSize = pagination.PageSize
        };
    }

    /// <summary>
    /// Clones a filter and updates the date range.
    /// </summary>
    private AuditQueryFilter CloneFilterWithDateRange(AuditQueryFilter filter, DateTime startDate, DateTime endDate)
    {
        return new AuditQueryFilter
        {
            StartDate = startDate,
            EndDate = endDate,
            ActorId = filter.ActorId,
            ActorType = filter.ActorType,
            CompanyId = filter.CompanyId,
            BranchId = filter.BranchId,
            EntityType = filter.EntityType,
            EntityId = filter.EntityId,
            Action = filter.Action,
            IpAddress = filter.IpAddress,
            CorrelationId = filter.CorrelationId,
            EventCategory = filter.EventCategory,
            Severity = filter.Severity,
            HttpMethod = filter.HttpMethod,
            EndpointPath = filter.EndpointPath,
            BusinessModule = filter.BusinessModule,
            ErrorCode = filter.ErrorCode
        };
    }

    #endregion

    #region Private Helper Methods

    /// <summary>
    /// Extracts user context from HTTP context for role-based filtering.
    /// Returns null if user is not authenticated or context is unavailable.
    /// </summary>
    private UserAccessContext? GetUserAccessContext()
    {
        try
        {
            var httpContext = _httpContextAccessor.HttpContext;
            if (httpContext == null)
            {
                _logger.LogWarning("HttpContext is null, cannot extract user context for audit filtering");
                return null;
            }

            var user = httpContext.User;
            if (user == null || !user.Identity?.IsAuthenticated == true)
            {
                _logger.LogWarning("User is not authenticated, cannot extract user context for audit filtering");
                return null;
            }

            // Extract user claims
            var userIdClaim = user.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            var isAdminClaim = user.FindFirst("isAdmin")?.Value;
            var roleClaim = user.FindFirst("role")?.Value;
            var userCompanyIdClaim = user.FindFirst("CompanyId")?.Value;
            var userBranchIdClaim = user.FindFirst("BranchId")?.Value;

            if (string.IsNullOrEmpty(userIdClaim) || !long.TryParse(userIdClaim, out var userId))
            {
                _logger.LogWarning("Invalid or missing user ID claim for audit filtering");
                return null;
            }

            var context = new UserAccessContext
            {
                UserId = userId,
                IsSuperAdmin = isAdminClaim == "true",
                Role = roleClaim ?? "USER",
                CompanyId = long.TryParse(userCompanyIdClaim, out var companyId) ? companyId : (long?)null,
                BranchId = long.TryParse(userBranchIdClaim, out var branchId) ? branchId : (long?)null
            };

            _logger.LogDebug(
                "Extracted user context for audit filtering: UserId={UserId}, IsSuperAdmin={IsSuperAdmin}, Role={Role}, CompanyId={CompanyId}",
                context.UserId, context.IsSuperAdmin, context.Role, context.CompanyId);

            return context;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error extracting user context for audit filtering");
            return null;
        }
    }

    /// <summary>
    /// Applies role-based filtering to the WHERE clause conditions.
    /// - SuperAdmins: No filtering (can see all audit data)
    /// - CompanyAdmins: Filter by CompanyId (can see all data for their company)
    /// - Regular Users: Filter by ActorId (can only see their own audit data)
    /// </summary>
    private IQueryable<SysAuditLog> ApplyRoleBasedFiltering(
        IQueryable<SysAuditLog> query,
        UserAccessContext? userContext)
    {
        if (userContext == null)
        {
            _logger.LogWarning("No user context available, denying audit data access");
            return query.Where(a => false);
        }

        if (userContext.IsSuperAdmin)
        {
            _logger.LogDebug("SuperAdmin user {UserId} - no filtering applied", userContext.UserId);
            return query;
        }

        if (userContext.Role == "COMPANY_ADMIN")
        {
            if (!userContext.CompanyId.HasValue)
            {
                _logger.LogWarning(
                    "CompanyAdmin user {UserId} has no CompanyId, denying audit data access",
                    userContext.UserId);
                return query.Where(a => false);
            }

            _logger.LogDebug(
                "CompanyAdmin user {UserId} - filtering by CompanyId={CompanyId}",
                userContext.UserId, userContext.CompanyId.Value);
            return query.Where(a => a.CompanyId == userContext.CompanyId.Value);
        }

        _logger.LogDebug(
            "Regular user {UserId} - filtering by ActorId (self-access only)",
            userContext.UserId);
        return query.Where(a => a.ActorId == userContext.UserId);
    }

    /// <summary>
    /// Builds the WHERE clause for SQL query based on filter criteria.
    /// Automatically applies role-based filtering based on the current user's access level.
    /// </summary>
    private IQueryable<SysAuditLog> ApplyQueryFilters(
        IQueryable<SysAuditLog> query,
        AuditQueryFilter filter)
    {
        var userContext = GetUserAccessContext();
        query = ApplyRoleBasedFiltering(query, userContext);

        if (filter.StartDate.HasValue)
            query = query.Where(a => a.CreationDate >= filter.StartDate.Value);

        if (filter.EndDate.HasValue)
            query = query.Where(a => a.CreationDate <= filter.EndDate.Value);

        if (filter.ActorId.HasValue)
            query = query.Where(a => a.ActorId == filter.ActorId.Value);

        if (!string.IsNullOrWhiteSpace(filter.ActorType))
            query = query.Where(a => a.ActorType == filter.ActorType);

        if (filter.CompanyId.HasValue)
            query = query.Where(a => a.CompanyId == filter.CompanyId.Value);

        if (filter.BranchId.HasValue)
            query = query.Where(a => a.BranchId == filter.BranchId.Value);

        if (!string.IsNullOrWhiteSpace(filter.EntityType))
            query = query.Where(a => a.EntityType == filter.EntityType);

        if (filter.EntityId.HasValue)
            query = query.Where(a => a.EntityId == filter.EntityId.Value);

        if (!string.IsNullOrWhiteSpace(filter.Action))
            query = query.Where(a => a.Action == filter.Action);

        if (!string.IsNullOrWhiteSpace(filter.IpAddress))
            query = query.Where(a => a.IpAddress == filter.IpAddress);

        if (!string.IsNullOrWhiteSpace(filter.CorrelationId))
            query = query.Where(a => a.CorrelationId == filter.CorrelationId);

        if (!string.IsNullOrWhiteSpace(filter.EventCategory))
            query = query.Where(a => a.EventCategory == filter.EventCategory);

        if (!string.IsNullOrWhiteSpace(filter.Severity))
            query = query.Where(a => a.Severity == filter.Severity);

        if (!string.IsNullOrWhiteSpace(filter.HttpMethod))
            query = query.Where(a => a.HttpMethod == filter.HttpMethod);

        if (!string.IsNullOrWhiteSpace(filter.EndpointPath))
            query = query.Where(a => a.EndpointPath == filter.EndpointPath);

        if (!string.IsNullOrWhiteSpace(filter.BusinessModule))
            query = query.Where(a => a.BusinessModule == filter.BusinessModule);

        if (!string.IsNullOrWhiteSpace(filter.ErrorCode))
            query = query.Where(a => a.ErrorCode == filter.ErrorCode);

        return query;
    }

    /// <summary>
    /// Query all records matching the filter (for export operations).
    /// </summary>
    private async Task<List<AuditLogEntry>> QueryAllAsync(
        AuditQueryFilter filter,
        CancellationToken cancellationToken)
    {
        var query = ApplyQueryFilters(_dbContext.SysAuditLogs.AsQueryable(), filter);

        var results = await query
            .OrderByDescending(a => a.CreationDate)
            .ToListAsync(cancellationToken);

        return results.Select(MapToAuditLogEntry).ToList();
    }

    /// <summary>
    /// Maps SysAuditLog entity to AuditLogEntry model.
    /// </summary>
    private AuditLogEntry MapToAuditLogEntry(Domain.Entities.SysAuditLog sysAuditLog)
    {
        var entry = new AuditLogEntry
        {
            Id = sysAuditLog.Id,
            ActorType = sysAuditLog.ActorType,
            ActorId = sysAuditLog.ActorId,
            CompanyId = sysAuditLog.CompanyId,
            BranchId = sysAuditLog.BranchId,
            Action = sysAuditLog.Action,
            EntityType = sysAuditLog.EntityType,
            EntityId = sysAuditLog.EntityId,
            OldValue = sysAuditLog.OldValue,
            NewValue = sysAuditLog.NewValue,
            IpAddress = sysAuditLog.IpAddress,
            UserAgent = sysAuditLog.UserAgent,
            CorrelationId = sysAuditLog.CorrelationId,
            HttpMethod = sysAuditLog.HttpMethod,
            EndpointPath = sysAuditLog.EndpointPath,
            RequestPayload = sysAuditLog.RequestPayload,
            ResponsePayload = sysAuditLog.ResponsePayload,
            ExecutionTimeMs = sysAuditLog.ExecutionTimeMs,
            StatusCode = sysAuditLog.StatusCode,
            ExceptionType = sysAuditLog.ExceptionType,
            ExceptionMessage = sysAuditLog.ExceptionMessage,
            StackTrace = sysAuditLog.StackTrace,
            Severity = sysAuditLog.Severity,
            EventCategory = sysAuditLog.EventCategory,
            Metadata = sysAuditLog.Metadata,
            BusinessModule = sysAuditLog.BusinessModule,
            DeviceIdentifier = sysAuditLog.DeviceIdentifier,
            ErrorCode = sysAuditLog.ErrorCode,
            BusinessDescription = sysAuditLog.BusinessDescription,
            CreationDate = sysAuditLog.CreationDate
        };

        PopulateDisplayNames(entry);
        return entry;
    }

    private void PopulateDisplayNames(AuditLogEntry entry)
    {
        try
        {
            if (entry.CompanyId.HasValue)
            {
                entry.CompanyName = _dbContext.SysCompanies
                    .Where(c => c.Id == entry.CompanyId.Value)
                    .Select(c => c.CompanyNameEn ?? c.CompanyNameAr)
                    .FirstOrDefault();
            }

            if (entry.BranchId.HasValue)
            {
                entry.BranchName = _dbContext.SysBranches
                    .Where(b => b.Id == entry.BranchId.Value)
                    .Select(b => b.BranchNameEn ?? b.BranchNameAr)
                    .FirstOrDefault();
            }

            if (entry.ActorId > 0)
            {
                entry.ActorName = string.Equals(entry.ActorType, "SUPER_ADMIN", StringComparison.OrdinalIgnoreCase)
                    ? _dbContext.SysSuperAdmins
                        .Where(u => u.Id == entry.ActorId)
                        .Select(u => u.NameEn ?? u.NameAr ?? u.UserName)
                        .FirstOrDefault()
                    : _dbContext.SysUsers
                        .Where(u => u.Id == entry.ActorId)
                        .Select(u => u.FullNameEn ?? u.FullNameAr ?? u.UserName)
                        .FirstOrDefault();
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to resolve audit display names for audit log {AuditLogId}", entry.Id);
        }
    }

    /// <summary>
    /// Builds timeline visualization from user actions.
    /// </summary>
    private TimelineVisualization BuildTimelineVisualization(List<UserAction> actions)
    {
        if (!actions.Any())
        {
            return new TimelineVisualization();
        }

        // Group by hour for hourly activity
        var hourlyActivity = actions
            .GroupBy(a => new DateTime(a.Timestamp.Year, a.Timestamp.Month, a.Timestamp.Day, a.Timestamp.Hour, 0, 0))
            .Select(g => new TimelineDataPoint
            {
                Timestamp = g.Key,
                ActionCount = g.Count(),
                SuccessCount = g.Count(a => a.StatusCode >= 200 && a.StatusCode < 300),
                FailureCount = g.Count(a => a.StatusCode >= 400),
                AverageExecutionTimeMs = g.Where(a => a.ExecutionTimeMs.HasValue)
                                          .Average(a => (double?)a.ExecutionTimeMs) ?? 0
            })
            .OrderBy(d => d.Timestamp)
            .ToList();

        // Endpoint distribution
        var endpointDistribution = actions
            .Where(a => !string.IsNullOrWhiteSpace(a.EndpointPath))
            .GroupBy(a => a.EndpointPath!)
            .ToDictionary(g => g.Key, g => g.Count());

        // Action type distribution
        var actionTypeDistribution = actions
            .GroupBy(a => a.Action)
            .ToDictionary(g => g.Key, g => g.Count());

        // Entity type distribution
        var entityTypeDistribution = actions
            .GroupBy(a => a.EntityType)
            .ToDictionary(g => g.Key, g => g.Count());

        // Peak activity hour
        var peakActivityHour = hourlyActivity
            .OrderByDescending(h => h.ActionCount)
            .FirstOrDefault()?.Timestamp.Hour ?? 0;

        // Average execution time
        var averageExecutionTimeMs = actions
            .Where(a => a.ExecutionTimeMs.HasValue)
            .Average(a => (double?)a.ExecutionTimeMs) ?? 0;

        // Success/failure counts
        var successfulActions = actions.Count(a => a.StatusCode >= 200 && a.StatusCode < 300);
        var failedActions = actions.Count(a => a.StatusCode >= 400);

        return new TimelineVisualization
        {
            HourlyActivity = hourlyActivity,
            EndpointDistribution = endpointDistribution,
            ActionTypeDistribution = actionTypeDistribution,
            EntityTypeDistribution = entityTypeDistribution,
            PeakActivityHour = peakActivityHour,
            AverageExecutionTimeMs = averageExecutionTimeMs,
            SuccessfulActions = successfulActions,
            FailedActions = failedActions
        };
    }

    /// <summary>
    /// Escapes CSV special characters.
    /// </summary>
    private string EscapeCsv(string? value)
    {
        if (string.IsNullOrEmpty(value))
            return string.Empty;

        // Escape double quotes by doubling them
        return value.Replace("\"", "\"\"");
    }

    /// <summary>
    /// Checks if Oracle Text is available and the full-text index exists.
    /// This check is cached after the first call to avoid repeated database queries.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>True if Oracle Text is available and configured, false otherwise</returns>
    private async Task<bool> IsOracleTextAvailableAsync(
        CancellationToken cancellationToken)
    {
        // Return cached result if available
        if (_isOracleTextAvailable.HasValue)
        {
            return _isOracleTextAvailable.Value;
        }

        // Use lock to prevent multiple concurrent checks
        await _oracleTextCheckLock.WaitAsync(cancellationToken);
        try
        {
            // Double-check after acquiring lock
            if (_isOracleTextAvailable.HasValue)
            {
                return _isOracleTextAvailable.Value;
            }

            _logger.LogDebug("Checking if Oracle Text is available");

            var count = await _dbContext.Database
                .SqlQueryRaw<int>(@"
                    SELECT COUNT(*) 
                    FROM USER_INDEXES 
                    WHERE INDEX_NAME = 'IDX_AUDIT_LOG_FULLTEXT' 
                      AND INDEX_TYPE = 'DOMAIN'")
                .SingleAsync(cancellationToken);

            var indexExists = count > 0;

            if (indexExists)
            {
                _logger.LogInformation("Oracle Text index IDX_AUDIT_LOG_FULLTEXT found and available");
                _isOracleTextAvailable = true;
            }
            else
            {
                _logger.LogWarning("Oracle Text index IDX_AUDIT_LOG_FULLTEXT not found. Falling back to LIKE queries. " +
                    "To enable Oracle Text search, run Database/Scripts/56_Create_Oracle_Text_Index_For_Audit_Search.sql");
                _isOracleTextAvailable = false;
            }

            return _isOracleTextAvailable.Value;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error checking Oracle Text availability. Falling back to LIKE queries.");
            _isOracleTextAvailable = false;
            return false;
        }
        finally
        {
            _oracleTextCheckLock.Release();
        }
    }

    /// <summary>
    /// Transforms a user search term into Oracle Text query syntax.
    /// Handles phrase search, boolean operators, wildcards, and special characters.
    /// </summary>
    /// <param name="searchTerm">User-provided search term</param>
    /// <returns>Oracle Text compatible query string</returns>
    private string TransformSearchTermForOracleText(string searchTerm)
    {
        if (string.IsNullOrWhiteSpace(searchTerm))
        {
            return "%"; // Match all
        }

        searchTerm = searchTerm.Trim();

        // If the search term already contains Oracle Text operators, use it as-is
        // Check for common Oracle Text syntax patterns
        if (searchTerm.Contains("NEAR(") || 
            searchTerm.Contains("fuzzy(") || 
            searchTerm.Contains("WITHIN") ||
            searchTerm.Contains("ACCUM") ||
            searchTerm.Contains("EQUIV"))
        {
            _logger.LogDebug("Search term contains Oracle Text operators, using as-is: {SearchTerm}", searchTerm);
            return searchTerm;
        }

        // If the search term is already quoted (phrase search), use it as-is
        if (searchTerm.StartsWith("\"") && searchTerm.EndsWith("\""))
        {
            _logger.LogDebug("Search term is a quoted phrase: {SearchTerm}", searchTerm);
            return searchTerm;
        }

        // If the search term contains boolean operators (AND, OR, NOT), use it as-is
        if (searchTerm.Contains(" AND ", StringComparison.OrdinalIgnoreCase) ||
            searchTerm.Contains(" OR ", StringComparison.OrdinalIgnoreCase) ||
            searchTerm.Contains(" NOT ", StringComparison.OrdinalIgnoreCase))
        {
            _logger.LogDebug("Search term contains boolean operators: {SearchTerm}", searchTerm);
            return searchTerm.ToUpper(); // Oracle Text operators are case-sensitive
        }

        // If the search term contains wildcards (%), use it as-is
        if (searchTerm.Contains("%") || searchTerm.Contains("_"))
        {
            _logger.LogDebug("Search term contains wildcards: {SearchTerm}", searchTerm);
            return searchTerm;
        }

        // For simple single-word or multi-word searches, treat as phrase if multiple words
        if (searchTerm.Contains(" "))
        {
            // Multi-word search - treat as phrase search
            _logger.LogDebug("Converting multi-word search to phrase: {SearchTerm}", searchTerm);
            return $"\"{searchTerm}\"";
        }

        // Single word search - use as-is
        _logger.LogDebug("Using simple word search: {SearchTerm}", searchTerm);
        return searchTerm;
    }

    #region Cache Helper Methods

    /// <summary>
    /// Generates a deterministic cache key based on the query parameters.
    /// Uses SHA256 hash to create a compact, collision-resistant key.
    /// </summary>
    /// <param name="prefix">Cache key prefix (e.g., "query", "search")</param>
    /// <param name="parameters">Query parameters to include in the key</param>
    /// <returns>Cache key string</returns>
    private string GenerateCacheKey(string prefix, params object[] parameters)
    {
        try
        {
            // Serialize parameters to JSON for consistent hashing
            var json = JsonSerializer.Serialize(parameters, new JsonSerializerOptions
            {
                WriteIndented = false,
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            });

            // Generate SHA256 hash for compact key
            using var sha256 = SHA256.Create();
            var hashBytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(json));
            var hash = Convert.ToBase64String(hashBytes)
                .Replace("+", "-")
                .Replace("/", "_")
                .Replace("=", "");

            var cacheKey = $"audit:{prefix}:{hash}";
            _logger.LogTrace("Generated cache key: {CacheKey} for parameters: {Json}", cacheKey, json);
            
            return cacheKey;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error generating cache key, using fallback");
            // Fallback to simple key if serialization fails
            return $"audit:{prefix}:{Guid.NewGuid():N}";
        }
    }

    /// <summary>
    /// Retrieves a value from the distributed cache.
    /// Returns null if the key doesn't exist or if Redis is unavailable.
    /// </summary>
    /// <typeparam name="T">Type of the cached value</typeparam>
    /// <param name="cacheKey">Cache key</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Cached value or null</returns>
    private async Task<T?> GetFromCacheAsync<T>(string cacheKey, CancellationToken cancellationToken) where T : class
    {
        if (!_cachingEnabled || _cache == null)
        {
            return null;
        }

        try
        {
            var cachedBytes = await _cache.GetAsync(cacheKey, cancellationToken);
            
            if (cachedBytes == null || cachedBytes.Length == 0)
            {
                return null;
            }

            var cachedJson = Encoding.UTF8.GetString(cachedBytes);
            var result = JsonSerializer.Deserialize<T>(cachedJson, new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            });

            _logger.LogTrace("Retrieved value from cache: {CacheKey}", cacheKey);
            return result;
        }
        catch (Exception ex)
        {
            // Log warning but don't throw - gracefully degrade to database query
            _logger.LogWarning(ex, "Error retrieving from cache (key: {CacheKey}). Falling back to database query.", cacheKey);
            return null;
        }
    }

    /// <summary>
    /// Stores a value in the distributed cache with the specified expiration.
    /// Fails gracefully if Redis is unavailable.
    /// </summary>
    /// <typeparam name="T">Type of the value to cache</typeparam>
    /// <param name="cacheKey">Cache key</param>
    /// <param name="value">Value to cache</param>
    /// <param name="expiration">Cache expiration duration</param>
    /// <param name="cancellationToken">Cancellation token</param>
    private async Task SetInCacheAsync<T>(string cacheKey, T value, TimeSpan expiration, CancellationToken cancellationToken) where T : class
    {
        if (!_cachingEnabled || _cache == null)
        {
            return;
        }

        try
        {
            var json = JsonSerializer.Serialize(value, new JsonSerializerOptions
            {
                WriteIndented = false,
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            });

            var bytes = Encoding.UTF8.GetBytes(json);

            var options = new DistributedCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = expiration
            };

            await _cache.SetAsync(cacheKey, bytes, options, cancellationToken);
            
            _logger.LogTrace("Stored value in cache: {CacheKey} (TTL: {Expiration})", cacheKey, expiration);
        }
        catch (Exception ex)
        {
            // Log warning but don't throw - caching is optional
            _logger.LogWarning(ex, "Error storing in cache (key: {CacheKey}). Continuing without caching.", cacheKey);
        }
    }

    #endregion

    #endregion
}

/// <summary>
/// Represents the user's access context for role-based audit data filtering.
/// </summary>
internal class UserAccessContext
{
    /// <summary>
    /// The user's ID.
    /// </summary>
    public long UserId { get; set; }

    /// <summary>
    /// Whether the user is a SuperAdmin (can access all audit data).
    /// </summary>
    public bool IsSuperAdmin { get; set; }

    /// <summary>
    /// The user's role (SUPER_ADMIN, COMPANY_ADMIN, USER, etc.).
    /// </summary>
    public string Role { get; set; } = null!;

    /// <summary>
    /// The user's company ID (null for SuperAdmins).
    /// </summary>
    public long? CompanyId { get; set; }

    /// <summary>
    /// The user's branch ID (null for SuperAdmins and CompanyAdmins).
    /// </summary>
    public long? BranchId { get; set; }
}
