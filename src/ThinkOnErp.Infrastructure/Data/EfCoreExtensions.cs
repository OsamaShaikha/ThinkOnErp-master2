using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System.Diagnostics;

namespace ThinkOnErp.Infrastructure.Data;

/// <summary>
/// Extension methods for Entity Framework Core operations.
/// Provides safe async operations with error handling, logging, and pagination helpers.
/// </summary>
public static class EfCoreExtensions
{
    #region Safe Async Operations

    /// <summary>
    /// Safely executes ToListAsync with comprehensive error handling and logging.
    /// Catches and wraps database exceptions with meaningful error messages.
    /// </summary>
    /// <typeparam name="T">The entity type.</typeparam>
    /// <param name="query">The IQueryable to execute.</param>
    /// <param name="logger">Optional logger for error logging.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A list of entities, or an empty list if an error occurs.</returns>
    /// <exception cref="InvalidOperationException">Thrown when a database error occurs.</exception>
    public static async Task<List<T>> ToListAsyncSafe<T>(
        this IQueryable<T> query,
        ILogger? logger = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var stopwatch = Stopwatch.StartNew();
            var result = await query.ToListAsync(cancellationToken);
            stopwatch.Stop();

            logger?.LogDebug(
                "Query executed successfully. Returned {Count} items in {ElapsedMs}ms",
                result.Count,
                stopwatch.ElapsedMilliseconds);

            return result;
        }
        catch (OperationCanceledException)
        {
            logger?.LogWarning("Query execution was cancelled");
            throw;
        }
        catch (DbUpdateException ex)
        {
            logger?.LogError(ex, "Database update error occurred while executing query");
            throw new InvalidOperationException("A database update error occurred. See inner exception for details.", ex);
        }
        catch (InvalidOperationException ex)
        {
            logger?.LogError(ex, "Invalid operation error occurred while executing query");
            throw new InvalidOperationException("An invalid operation occurred while querying the database. See inner exception for details.", ex);
        }
        catch (Exception ex)
        {
            logger?.LogError(ex, "Unexpected error occurred while executing query");
            throw new InvalidOperationException("An unexpected error occurred while querying the database. See inner exception for details.", ex);
        }
    }

    /// <summary>
    /// Safely executes FirstOrDefaultAsync with comprehensive error handling and logging.
    /// Catches and wraps database exceptions with meaningful error messages.
    /// </summary>
    /// <typeparam name="T">The entity type.</typeparam>
    /// <param name="query">The IQueryable to execute.</param>
    /// <param name="logger">Optional logger for error logging.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The first entity or default value, or null if an error occurs.</returns>
    /// <exception cref="InvalidOperationException">Thrown when a database error occurs.</exception>
    public static async Task<T?> FirstOrDefaultAsyncSafe<T>(
        this IQueryable<T> query,
        ILogger? logger = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var stopwatch = Stopwatch.StartNew();
            var result = await query.FirstOrDefaultAsync(cancellationToken);
            stopwatch.Stop();

            logger?.LogDebug(
                "Query executed successfully. Result: {Found} in {ElapsedMs}ms",
                result != null ? "Found" : "Not Found",
                stopwatch.ElapsedMilliseconds);

            return result;
        }
        catch (OperationCanceledException)
        {
            logger?.LogWarning("Query execution was cancelled");
            throw;
        }
        catch (DbUpdateException ex)
        {
            logger?.LogError(ex, "Database update error occurred while executing query");
            throw new InvalidOperationException("A database update error occurred. See inner exception for details.", ex);
        }
        catch (InvalidOperationException ex) when (ex.Message.Contains("Sequence contains more than one element"))
        {
            logger?.LogError(ex, "Query returned multiple results when only one was expected");
            throw new InvalidOperationException("The query returned multiple results when only one was expected. Consider using SingleOrDefault instead.", ex);
        }
        catch (InvalidOperationException ex)
        {
            logger?.LogError(ex, "Invalid operation error occurred while executing query");
            throw new InvalidOperationException("An invalid operation occurred while querying the database. See inner exception for details.", ex);
        }
        catch (Exception ex)
        {
            logger?.LogError(ex, "Unexpected error occurred while executing query");
            throw new InvalidOperationException("An unexpected error occurred while querying the database. See inner exception for details.", ex);
        }
    }

    /// <summary>
    /// Safely executes AnyAsync with comprehensive error handling and logging.
    /// Catches and wraps database exceptions with meaningful error messages.
    /// </summary>
    /// <typeparam name="T">The entity type.</typeparam>
    /// <param name="query">The IQueryable to execute.</param>
    /// <param name="logger">Optional logger for error logging.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>True if any elements exist, false otherwise.</returns>
    /// <exception cref="InvalidOperationException">Thrown when a database error occurs.</exception>
    public static async Task<bool> AnyAsyncSafe<T>(
        this IQueryable<T> query,
        ILogger? logger = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var stopwatch = Stopwatch.StartNew();
            var result = await query.AnyAsync(cancellationToken);
            stopwatch.Stop();

            logger?.LogDebug(
                "Query executed successfully. Result: {Exists} in {ElapsedMs}ms",
                result ? "Exists" : "Not Exists",
                stopwatch.ElapsedMilliseconds);

            return result;
        }
        catch (OperationCanceledException)
        {
            logger?.LogWarning("Query execution was cancelled");
            throw;
        }
        catch (DbUpdateException ex)
        {
            logger?.LogError(ex, "Database update error occurred while executing query");
            throw new InvalidOperationException("A database update error occurred. See inner exception for details.", ex);
        }
        catch (InvalidOperationException ex)
        {
            logger?.LogError(ex, "Invalid operation error occurred while executing query");
            throw new InvalidOperationException("An invalid operation occurred while querying the database. See inner exception for details.", ex);
        }
        catch (Exception ex)
        {
            logger?.LogError(ex, "Unexpected error occurred while executing query");
            throw new InvalidOperationException("An unexpected error occurred while querying the database. See inner exception for details.", ex);
        }
    }

    #endregion

    #region Logging Extensions

    /// <summary>
    /// Logs query execution with timing information.
    /// Useful for performance monitoring and debugging.
    /// </summary>
    /// <typeparam name="T">The entity type.</typeparam>
    /// <param name="query">The IQueryable to log.</param>
    /// <param name="logger">Logger instance.</param>
    /// <param name="operationName">Name of the operation for logging context.</param>
    /// <returns>The same IQueryable for method chaining.</returns>
    public static IQueryable<T> LogQuery<T>(
        this IQueryable<T> query,
        ILogger logger,
        string operationName)
    {
        if (logger == null)
            throw new ArgumentNullException(nameof(logger));

        logger.LogDebug(
            "Executing query for operation: {OperationName}, Entity: {EntityType}",
            operationName,
            typeof(T).Name);

        return query;
    }

    /// <summary>
    /// Logs query execution with detailed SQL information (for debugging).
    /// WARNING: Only use in development as this may expose sensitive data.
    /// </summary>
    /// <typeparam name="T">The entity type.</typeparam>
    /// <param name="query">The IQueryable to log.</param>
    /// <param name="logger">Logger instance.</param>
    /// <param name="operationName">Name of the operation for logging context.</param>
    /// <returns>The same IQueryable for method chaining.</returns>
    public static IQueryable<T> LogQueryWithSql<T>(
        this IQueryable<T> query,
        ILogger logger,
        string operationName)
    {
        if (logger == null)
            throw new ArgumentNullException(nameof(logger));

        var sql = query.ToQueryString();
        logger.LogDebug(
            "Executing query for operation: {OperationName}, Entity: {EntityType}, SQL: {Sql}",
            operationName,
            typeof(T).Name,
            sql);

        return query;
    }

    /// <summary>
    /// Executes a query and logs the execution time.
    /// </summary>
    /// <typeparam name="T">The entity type.</typeparam>
    /// <param name="query">The IQueryable to execute.</param>
    /// <param name="logger">Logger instance.</param>
    /// <param name="operationName">Name of the operation for logging context.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A list of entities.</returns>
    public static async Task<List<T>> ToListWithLoggingAsync<T>(
        this IQueryable<T> query,
        ILogger logger,
        string operationName,
        CancellationToken cancellationToken = default)
    {
        if (logger == null)
            throw new ArgumentNullException(nameof(logger));

        var stopwatch = Stopwatch.StartNew();
        try
        {
            var result = await query.ToListAsync(cancellationToken);
            stopwatch.Stop();

            logger.LogInformation(
                "Query completed for operation: {OperationName}, Entity: {EntityType}, Count: {Count}, Duration: {ElapsedMs}ms",
                operationName,
                typeof(T).Name,
                result.Count,
                stopwatch.ElapsedMilliseconds);

            return result;
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            logger.LogError(
                ex,
                "Query failed for operation: {OperationName}, Entity: {EntityType}, Duration: {ElapsedMs}ms",
                operationName,
                typeof(T).Name,
                stopwatch.ElapsedMilliseconds);
            throw;
        }
    }

    #endregion

    #region Pagination Extensions

    /// <summary>
    /// Applies pagination to a query using page number and page size.
    /// </summary>
    /// <typeparam name="T">The entity type.</typeparam>
    /// <param name="query">The IQueryable to paginate.</param>
    /// <param name="pageNumber">The page number (1-based).</param>
    /// <param name="pageSize">The number of items per page.</param>
    /// <returns>A paginated IQueryable.</returns>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when pageNumber or pageSize is less than 1.</exception>
    public static IQueryable<T> Paginate<T>(
        this IQueryable<T> query,
        int pageNumber,
        int pageSize)
    {
        if (pageNumber < 1)
            throw new ArgumentOutOfRangeException(nameof(pageNumber), "Page number must be greater than or equal to 1.");

        if (pageSize < 1)
            throw new ArgumentOutOfRangeException(nameof(pageSize), "Page size must be greater than or equal to 1.");

        return query
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize);
    }

    /// <summary>
    /// Applies pagination to a query and returns a paginated result with metadata.
    /// </summary>
    /// <typeparam name="T">The entity type.</typeparam>
    /// <param name="query">The IQueryable to paginate.</param>
    /// <param name="pageNumber">The page number (1-based).</param>
    /// <param name="pageSize">The number of items per page.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A PaginatedResult containing items and pagination metadata.</returns>
    public static async Task<PaginatedResult<T>> ToPaginatedResultAsync<T>(
        this IQueryable<T> query,
        int pageNumber,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        if (pageNumber < 1)
            throw new ArgumentOutOfRangeException(nameof(pageNumber), "Page number must be greater than or equal to 1.");

        if (pageSize < 1)
            throw new ArgumentOutOfRangeException(nameof(pageSize), "Page size must be greater than or equal to 1.");

        var totalCount = await query.CountAsync(cancellationToken);
        var items = await query
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return new PaginatedResult<T>
        {
            Items = items,
            PageNumber = pageNumber,
            PageSize = pageSize,
            TotalCount = totalCount,
            TotalPages = (int)Math.Ceiling(totalCount / (double)pageSize)
        };
    }

    /// <summary>
    /// Applies skip and take operations with validation.
    /// </summary>
    /// <typeparam name="T">The entity type.</typeparam>
    /// <param name="query">The IQueryable to apply skip/take to.</param>
    /// <param name="skip">Number of items to skip.</param>
    /// <param name="take">Number of items to take.</param>
    /// <returns>An IQueryable with skip and take applied.</returns>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when skip or take is negative.</exception>
    public static IQueryable<T> SkipTake<T>(
        this IQueryable<T> query,
        int skip,
        int take)
    {
        if (skip < 0)
            throw new ArgumentOutOfRangeException(nameof(skip), "Skip value must be greater than or equal to 0.");

        if (take < 0)
            throw new ArgumentOutOfRangeException(nameof(take), "Take value must be greater than or equal to 0.");

        return query.Skip(skip).Take(take);
    }

    #endregion
}

/// <summary>
/// Represents a paginated result with metadata.
/// </summary>
/// <typeparam name="T">The entity type.</typeparam>
public class PaginatedResult<T>
{
    /// <summary>
    /// Gets or sets the items in the current page.
    /// </summary>
    public List<T> Items { get; set; } = new();

    /// <summary>
    /// Gets or sets the current page number (1-based).
    /// </summary>
    public int PageNumber { get; set; }

    /// <summary>
    /// Gets or sets the page size.
    /// </summary>
    public int PageSize { get; set; }

    /// <summary>
    /// Gets or sets the total count of items across all pages.
    /// </summary>
    public int TotalCount { get; set; }

    /// <summary>
    /// Gets or sets the total number of pages.
    /// </summary>
    public int TotalPages { get; set; }

    /// <summary>
    /// Gets a value indicating whether there is a previous page.
    /// </summary>
    public bool HasPreviousPage => PageNumber > 1;

    /// <summary>
    /// Gets a value indicating whether there is a next page.
    /// </summary>
    public bool HasNextPage => PageNumber < TotalPages;
}
