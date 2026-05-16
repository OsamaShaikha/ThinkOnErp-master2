using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using ThinkOnErp.Domain.Entities;
using ThinkOnErp.Domain.Interfaces;
using ThinkOnErp.Domain.Models;
using ThinkOnErp.Infrastructure.Data;

namespace ThinkOnErp.Infrastructure.Repositories;

public class SlowQueryRepository : ISlowQueryRepository
{
    private readonly OracleDbContext _dbContext;
    private readonly ILogger<SlowQueryRepository> _logger;

    public SlowQueryRepository(OracleDbContext dbContext, ILogger<SlowQueryRepository> logger)
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task LogSlowRequestAsync(SlowRequest slowRequest, CancellationToken cancellationToken = default)
    {
        if (slowRequest == null)
        {
            _logger.LogWarning("Attempted to log null slow request");
            return;
        }

        try
        {
            var requestMetadata = System.Text.Json.JsonSerializer.Serialize(new
            {
                Type = "SlowRequest",
                slowRequest.HttpMethod,
                slowRequest.Endpoint,
                slowRequest.StatusCode,
                slowRequest.DatabaseTimeMs,
                slowRequest.QueryCount,
                slowRequest.ExceptionMessage
            });

            var entity = new SysSlowQuery
            {
                CorrelationId = slowRequest.CorrelationId,
                SqlStatement = requestMetadata,
                ExecutionTimeMs = slowRequest.ExecutionTimeMs,
                RowsAffected = 0,
                EndpointPath = slowRequest.Endpoint,
                UserId = slowRequest.UserId,
                CompanyId = slowRequest.CompanyId,
                CreationDate = DateTime.UtcNow
            };

            await _dbContext.SysSlowQueries.AddAsync(entity, cancellationToken);
            await _dbContext.SaveChangesAsync(cancellationToken);

            _logger.LogInformation(
                "Logged slow request to database: CorrelationId={CorrelationId}, Endpoint={Endpoint}, ExecutionTime={ExecutionTimeMs}ms",
                slowRequest.CorrelationId, slowRequest.Endpoint, slowRequest.ExecutionTimeMs);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Failed to log slow request to database: CorrelationId={CorrelationId}, Endpoint={Endpoint}",
                slowRequest.CorrelationId, slowRequest.Endpoint);
        }
    }

    public async Task LogSlowQueryAsync(SlowQuery slowQuery, CancellationToken cancellationToken = default)
    {
        if (slowQuery == null)
        {
            _logger.LogWarning("Attempted to log null slow query");
            return;
        }

        try
        {
            var entity = new SysSlowQuery
            {
                CorrelationId = slowQuery.CorrelationId,
                SqlStatement = slowQuery.SqlStatement,
                ExecutionTimeMs = slowQuery.ExecutionTimeMs,
                RowsAffected = slowQuery.RowsAffected,
                EndpointPath = slowQuery.EndpointPath,
                UserId = slowQuery.UserId,
                CompanyId = slowQuery.CompanyId,
                CreationDate = DateTime.UtcNow
            };

            await _dbContext.SysSlowQueries.AddAsync(entity, cancellationToken);
            await _dbContext.SaveChangesAsync(cancellationToken);

            _logger.LogInformation(
                "Logged slow query to database: CorrelationId={CorrelationId}, ExecutionTime={ExecutionTimeMs}ms",
                slowQuery.CorrelationId, slowQuery.ExecutionTimeMs);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Failed to log slow query to database: CorrelationId={CorrelationId}",
                slowQuery.CorrelationId);
        }
    }

    public async Task<IEnumerable<SlowRequest>> GetSlowRequestsAsync(int thresholdMs, int limit = 100, CancellationToken cancellationToken = default)
    {
        try
        {
            return await _dbContext.SysSlowQueries
                .Where(q => q.ExecutionTimeMs >= thresholdMs
                         && q.SqlStatement.Contains("\"Type\":\"SlowRequest\""))
                .OrderByDescending(q => q.ExecutionTimeMs)
                .Take(limit)
                .Select(q => new SlowRequest
                {
                    CorrelationId = q.CorrelationId,
                    Endpoint = q.EndpointPath ?? "Unknown",
                    HttpMethod = "Unknown",
                    ExecutionTimeMs = q.ExecutionTimeMs,
                    DatabaseTimeMs = 0,
                    QueryCount = 0,
                    StatusCode = 0,
                    UserId = q.UserId,
                    CompanyId = q.CompanyId,
                    Timestamp = q.CreationDate
                })
                .ToListAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to retrieve slow requests from database");
            return Enumerable.Empty<SlowRequest>();
        }
    }

    public async Task<IEnumerable<SlowQuery>> GetSlowQueriesAsync(int thresholdMs, int limit = 100, CancellationToken cancellationToken = default)
    {
        try
        {
            return await _dbContext.SysSlowQueries
                .Where(q => q.ExecutionTimeMs >= thresholdMs
                         && !q.SqlStatement.Contains("\"Type\":\"SlowRequest\""))
                .OrderByDescending(q => q.ExecutionTimeMs)
                .Take(limit)
                .Select(q => new SlowQuery
                {
                    CorrelationId = q.CorrelationId,
                    SqlStatement = q.SqlStatement,
                    ExecutionTimeMs = q.ExecutionTimeMs,
                    RowsAffected = q.RowsAffected,
                    EndpointPath = q.EndpointPath,
                    UserId = q.UserId,
                    CompanyId = q.CompanyId,
                    Timestamp = q.CreationDate
                })
                .ToListAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to retrieve slow queries from database");
            return Enumerable.Empty<SlowQuery>();
        }
    }
}
