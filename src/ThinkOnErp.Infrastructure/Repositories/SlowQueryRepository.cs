using System.Data;
using Microsoft.Extensions.Logging;
using Oracle.ManagedDataAccess.Client;
using ThinkOnErp.Domain.Interfaces;
using ThinkOnErp.Domain.Models;
using ThinkOnErp.Infrastructure.Data;

namespace ThinkOnErp.Infrastructure.Repositories;

/// <summary>
/// Repository for persisting slow query and slow request data to the database.
/// Implements logging to SYS_SLOW_QUERIES table for performance analysis.
/// </summary>
public class SlowQueryRepository : ISlowQueryRepository
{
    private readonly OracleDbContext _dbContext;
    private readonly ILogger<SlowQueryRepository> _logger;

    public SlowQueryRepository(OracleDbContext dbContext, ILogger<SlowQueryRepository> logger)
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Log a slow request to the SYS_SLOW_QUERIES table.
    /// Note: The table is named SYS_SLOW_QUERIES but stores both slow queries and slow requests.
    /// For requests, the SQL_STATEMENT column contains request metadata in JSON format.
    /// </summary>
    public async Task LogSlowRequestAsync(SlowRequest slowRequest, CancellationToken cancellationToken = default)
    {
        if (slowRequest == null)
        {
            _logger.LogWarning("Attempted to log null slow request");
            return;
        }

        try
        {
            using var connection = _dbContext.CreateConnection();
            await connection.OpenAsync(cancellationToken);

            // Create JSON representation of the request for the SQL_STATEMENT column
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

            using var command = connection.CreateCommand();
            command.CommandText = @"
                INSERT INTO SYS_SLOW_QUERIES (
                    ROW_ID,
                    CORRELATION_ID,
                    SQL_STATEMENT,
                    EXECUTION_TIME_MS,
                    ROWS_AFFECTED,
                    ENDPOINT_PATH,
                    USER_ID,
                    COMPANY_ID,
                    CREATION_DATE
                ) VALUES (
                    SEQ_SYS_SLOW_QUERIES.NEXTVAL,
                    :CorrelationId,
                    :SqlStatement,
                    :ExecutionTimeMs,
                    :RowsAffected,
                    :EndpointPath,
                    :UserId,
                    :CompanyId,
                    SYSDATE
                )";

            command.Parameters.Add(new OracleParameter("CorrelationId", OracleDbType.NVarchar2) { Value = slowRequest.CorrelationId });
            command.Parameters.Add(new OracleParameter("SqlStatement", OracleDbType.Clob) { Value = requestMetadata });
            command.Parameters.Add(new OracleParameter("ExecutionTimeMs", OracleDbType.Int64) { Value = slowRequest.ExecutionTimeMs });
            command.Parameters.Add(new OracleParameter("RowsAffected", OracleDbType.Int32) { Value = 0 }); // Not applicable for requests
            command.Parameters.Add(new OracleParameter("EndpointPath", OracleDbType.NVarchar2) { Value = slowRequest.Endpoint });
            command.Parameters.Add(new OracleParameter("UserId", OracleDbType.Int64) { Value = (object?)slowRequest.UserId ?? DBNull.Value });
            command.Parameters.Add(new OracleParameter("CompanyId", OracleDbType.Int64) { Value = (object?)slowRequest.CompanyId ?? DBNull.Value });

            await command.ExecuteNonQueryAsync(cancellationToken);

            _logger.LogInformation(
                "Logged slow request to database: CorrelationId={CorrelationId}, Endpoint={Endpoint}, ExecutionTime={ExecutionTimeMs}ms",
                slowRequest.CorrelationId, slowRequest.Endpoint, slowRequest.ExecutionTimeMs);
        }
        catch (Exception ex)
        {
            // Don't let database logging failures break the application
            _logger.LogError(ex, 
                "Failed to log slow request to database: CorrelationId={CorrelationId}, Endpoint={Endpoint}",
                slowRequest.CorrelationId, slowRequest.Endpoint);
        }
    }

    /// <summary>
    /// Log a slow database query to the SYS_SLOW_QUERIES table.
    /// </summary>
    public async Task LogSlowQueryAsync(SlowQuery slowQuery, CancellationToken cancellationToken = default)
    {
        if (slowQuery == null)
        {
            _logger.LogWarning("Attempted to log null slow query");
            return;
        }

        try
        {
            using var connection = _dbContext.CreateConnection();
            await connection.OpenAsync(cancellationToken);

            using var command = connection.CreateCommand();
            command.CommandText = @"
                INSERT INTO SYS_SLOW_QUERIES (
                    ROW_ID,
                    CORRELATION_ID,
                    SQL_STATEMENT,
                    EXECUTION_TIME_MS,
                    ROWS_AFFECTED,
                    ENDPOINT_PATH,
                    USER_ID,
                    COMPANY_ID,
                    CREATION_DATE
                ) VALUES (
                    SEQ_SYS_SLOW_QUERIES.NEXTVAL,
                    :CorrelationId,
                    :SqlStatement,
                    :ExecutionTimeMs,
                    :RowsAffected,
                    :EndpointPath,
                    :UserId,
                    :CompanyId,
                    SYSDATE
                )";

            command.Parameters.Add(new OracleParameter("CorrelationId", OracleDbType.NVarchar2) { Value = slowQuery.CorrelationId });
            command.Parameters.Add(new OracleParameter("SqlStatement", OracleDbType.Clob) { Value = slowQuery.SqlStatement });
            command.Parameters.Add(new OracleParameter("ExecutionTimeMs", OracleDbType.Int64) { Value = slowQuery.ExecutionTimeMs });
            command.Parameters.Add(new OracleParameter("RowsAffected", OracleDbType.Int32) { Value = slowQuery.RowsAffected });
            command.Parameters.Add(new OracleParameter("EndpointPath", OracleDbType.NVarchar2) { Value = (object?)slowQuery.EndpointPath ?? DBNull.Value });
            command.Parameters.Add(new OracleParameter("UserId", OracleDbType.Int64) { Value = (object?)slowQuery.UserId ?? DBNull.Value });
            command.Parameters.Add(new OracleParameter("CompanyId", OracleDbType.Int64) { Value = (object?)slowQuery.CompanyId ?? DBNull.Value });

            await command.ExecuteNonQueryAsync(cancellationToken);

            _logger.LogInformation(
                "Logged slow query to database: CorrelationId={CorrelationId}, ExecutionTime={ExecutionTimeMs}ms",
                slowQuery.CorrelationId, slowQuery.ExecutionTimeMs);
        }
        catch (Exception ex)
        {
            // Don't let database logging failures break the application
            _logger.LogError(ex, 
                "Failed to log slow query to database: CorrelationId={CorrelationId}",
                slowQuery.CorrelationId);
        }
    }

    /// <summary>
    /// Get slow requests from the database with optional filtering.
    /// </summary>
    public async Task<IEnumerable<SlowRequest>> GetSlowRequestsAsync(int thresholdMs, int limit = 100, CancellationToken cancellationToken = default)
    {
        try
        {
            using var connection = _dbContext.CreateConnection();
            await connection.OpenAsync(cancellationToken);

            using var command = connection.CreateCommand();
            command.CommandText = @"
                SELECT * FROM (
                    SELECT 
                        CORRELATION_ID,
                        SQL_STATEMENT,
                        EXECUTION_TIME_MS,
                        ENDPOINT_PATH,
                        USER_ID,
                        COMPANY_ID,
                        CREATION_DATE
                    FROM SYS_SLOW_QUERIES
                    WHERE EXECUTION_TIME_MS >= :ThresholdMs
                        AND SQL_STATEMENT LIKE '%""Type"":""SlowRequest""%'
                    ORDER BY EXECUTION_TIME_MS DESC
                ) WHERE ROWNUM <= :Limit";

            command.Parameters.Add(new OracleParameter("ThresholdMs", OracleDbType.Int32) { Value = thresholdMs });
            command.Parameters.Add(new OracleParameter("Limit", OracleDbType.Int32) { Value = limit });

            var results = new List<SlowRequest>();
            using var reader = await command.ExecuteReaderAsync(cancellationToken);
            
            while (await reader.ReadAsync(cancellationToken))
            {
                results.Add(new SlowRequest
                {
                    CorrelationId = reader.GetString(reader.GetOrdinal("CORRELATION_ID")),
                    Endpoint = reader.IsDBNull(reader.GetOrdinal("ENDPOINT_PATH")) ? "Unknown" : reader.GetString(reader.GetOrdinal("ENDPOINT_PATH")),
                    HttpMethod = "Unknown", // Would need to parse from JSON
                    ExecutionTimeMs = reader.GetInt64(reader.GetOrdinal("EXECUTION_TIME_MS")),
                    DatabaseTimeMs = 0, // Would need to parse from JSON
                    QueryCount = 0, // Would need to parse from JSON
                    StatusCode = 0, // Would need to parse from JSON
                    UserId = reader.IsDBNull(reader.GetOrdinal("USER_ID")) ? null : reader.GetInt64(reader.GetOrdinal("USER_ID")),
                    CompanyId = reader.IsDBNull(reader.GetOrdinal("COMPANY_ID")) ? null : reader.GetInt64(reader.GetOrdinal("COMPANY_ID")),
                    Timestamp = reader.GetDateTime(reader.GetOrdinal("CREATION_DATE"))
                });
            }

            return results;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to retrieve slow requests from database");
            return Enumerable.Empty<SlowRequest>();
        }
    }

    /// <summary>
    /// Get slow queries from the database with optional filtering.
    /// </summary>
    public async Task<IEnumerable<SlowQuery>> GetSlowQueriesAsync(int thresholdMs, int limit = 100, CancellationToken cancellationToken = default)
    {
        try
        {
            using var connection = _dbContext.CreateConnection();
            await connection.OpenAsync(cancellationToken);

            using var command = connection.CreateCommand();
            command.CommandText = @"
                SELECT * FROM (
                    SELECT 
                        CORRELATION_ID,
                        SQL_STATEMENT,
                        EXECUTION_TIME_MS,
                        ROWS_AFFECTED,
                        ENDPOINT_PATH,
                        USER_ID,
                        COMPANY_ID,
                        CREATION_DATE
                    FROM SYS_SLOW_QUERIES
                    WHERE EXECUTION_TIME_MS >= :ThresholdMs
                        AND SQL_STATEMENT NOT LIKE '%""Type"":""SlowRequest""%'
                    ORDER BY EXECUTION_TIME_MS DESC
                ) WHERE ROWNUM <= :Limit";

            command.Parameters.Add(new OracleParameter("ThresholdMs", OracleDbType.Int32) { Value = thresholdMs });
            command.Parameters.Add(new OracleParameter("Limit", OracleDbType.Int32) { Value = limit });

            var results = new List<SlowQuery>();
            using var reader = await command.ExecuteReaderAsync(cancellationToken);
            
            while (await reader.ReadAsync(cancellationToken))
            {
                results.Add(new SlowQuery
                {
                    CorrelationId = reader.GetString(reader.GetOrdinal("CORRELATION_ID")),
                    SqlStatement = reader.GetString(reader.GetOrdinal("SQL_STATEMENT")),
                    ExecutionTimeMs = reader.GetInt64(reader.GetOrdinal("EXECUTION_TIME_MS")),
                    RowsAffected = reader.GetInt32(reader.GetOrdinal("ROWS_AFFECTED")),
                    EndpointPath = reader.IsDBNull(reader.GetOrdinal("ENDPOINT_PATH")) ? null : reader.GetString(reader.GetOrdinal("ENDPOINT_PATH")),
                    UserId = reader.IsDBNull(reader.GetOrdinal("USER_ID")) ? null : reader.GetInt64(reader.GetOrdinal("USER_ID")),
                    CompanyId = reader.IsDBNull(reader.GetOrdinal("COMPANY_ID")) ? null : reader.GetInt64(reader.GetOrdinal("COMPANY_ID")),
                    Timestamp = reader.GetDateTime(reader.GetOrdinal("CREATION_DATE"))
                });
            }

            return results;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to retrieve slow queries from database");
            return Enumerable.Empty<SlowQuery>();
        }
    }
}
