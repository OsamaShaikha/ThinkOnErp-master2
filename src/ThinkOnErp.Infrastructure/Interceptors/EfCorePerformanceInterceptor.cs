using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging;
using System.Data.Common;
using System.Diagnostics;
using ThinkOnErp.Domain.Interfaces;

namespace ThinkOnErp.Infrastructure.Interceptors;

/// <summary>
/// EF Core interceptor for performance monitoring and SQL query logging.
/// Logs all SQL queries with execution times and tracks slow queries.
/// Implements REQ-21: Monitoring and Observability
/// </summary>
public class EfCorePerformanceInterceptor : DbCommandInterceptor
{
    private readonly ILogger<EfCorePerformanceInterceptor> _logger;
    private readonly ISlowQueryRepository? _slowQueryRepository;
    private readonly int _slowQueryThresholdMs;
    private readonly bool _logSqlQueries;
    private readonly bool _logQueryExecutionTime;

    public EfCorePerformanceInterceptor(
        ILogger<EfCorePerformanceInterceptor> logger,
        ISlowQueryRepository? slowQueryRepository = null,
        int slowQueryThresholdMs = 1000,
        bool logSqlQueries = true,
        bool logQueryExecutionTime = true)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _slowQueryRepository = slowQueryRepository;
        _slowQueryThresholdMs = slowQueryThresholdMs;
        _logSqlQueries = logSqlQueries;
        _logQueryExecutionTime = logQueryExecutionTime;
    }

    public override InterceptionResult<DbDataReader> ReaderExecuting(
        DbCommand command,
        CommandEventData eventData,
        InterceptionResult<DbDataReader> result)
    {
        if (_logSqlQueries)
        {
            LogCommandExecution(command, eventData);
        }

        return base.ReaderExecuting(command, eventData, result);
    }

    public override async ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(
        DbCommand command,
        CommandEventData eventData,
        InterceptionResult<DbDataReader> result,
        CancellationToken cancellationToken = default)
    {
        if (_logSqlQueries)
        {
            LogCommandExecution(command, eventData);
        }

        return await base.ReaderExecutingAsync(command, eventData, result, cancellationToken);
    }

    public override DbDataReader ReaderExecuted(
        DbCommand command,
        CommandExecutedEventData eventData,
        DbDataReader result)
    {
        LogCommandExecuted(command, eventData);
        return base.ReaderExecuted(command, eventData, result);
    }

    public override async ValueTask<DbDataReader> ReaderExecutedAsync(
        DbCommand command,
        CommandExecutedEventData eventData,
        DbDataReader result,
        CancellationToken cancellationToken = default)
    {
        LogCommandExecuted(command, eventData);
        return await base.ReaderExecutedAsync(command, eventData, result, cancellationToken);
    }

    public override InterceptionResult<int> NonQueryExecuting(
        DbCommand command,
        CommandEventData eventData,
        InterceptionResult<int> result)
    {
        if (_logSqlQueries)
        {
            LogCommandExecution(command, eventData);
        }

        return base.NonQueryExecuting(command, eventData, result);
    }

    public override async ValueTask<InterceptionResult<int>> NonQueryExecutingAsync(
        DbCommand command,
        CommandEventData eventData,
        InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        if (_logSqlQueries)
        {
            LogCommandExecution(command, eventData);
        }

        return await base.NonQueryExecutingAsync(command, eventData, result, cancellationToken);
    }

    public override int NonQueryExecuted(
        DbCommand command,
        CommandExecutedEventData eventData,
        int result)
    {
        LogCommandExecuted(command, eventData);
        return base.NonQueryExecuted(command, eventData, result);
    }

    public override async ValueTask<int> NonQueryExecutedAsync(
        DbCommand command,
        CommandExecutedEventData eventData,
        int result,
        CancellationToken cancellationToken = default)
    {
        LogCommandExecuted(command, eventData);
        return await base.NonQueryExecutedAsync(command, eventData, result, cancellationToken);
    }

    public override InterceptionResult<object> ScalarExecuting(
        DbCommand command,
        CommandEventData eventData,
        InterceptionResult<object> result)
    {
        if (_logSqlQueries)
        {
            LogCommandExecution(command, eventData);
        }

        return base.ScalarExecuting(command, eventData, result);
    }

    public override async ValueTask<InterceptionResult<object>> ScalarExecutingAsync(
        DbCommand command,
        CommandEventData eventData,
        InterceptionResult<object> result,
        CancellationToken cancellationToken = default)
    {
        if (_logSqlQueries)
        {
            LogCommandExecution(command, eventData);
        }

        return await base.ScalarExecutingAsync(command, eventData, result, cancellationToken);
    }

    public override object ScalarExecuted(
        DbCommand command,
        CommandExecutedEventData eventData,
        object result)
    {
        LogCommandExecuted(command, eventData);
        return base.ScalarExecuted(command, eventData, result);
    }

    public override async ValueTask<object> ScalarExecutedAsync(
        DbCommand command,
        CommandExecutedEventData eventData,
        object result,
        CancellationToken cancellationToken = default)
    {
        LogCommandExecuted(command, eventData);
        return await base.ScalarExecutedAsync(command, eventData, result, cancellationToken);
    }

    public override void CommandFailed(
        DbCommand command,
        CommandErrorEventData eventData)
    {
        var duration = eventData.Duration.TotalMilliseconds;

        _logger.LogError(
            eventData.Exception,
            "EF Core query failed after {Duration}ms. CommandId: {CommandId}, SQL: {Sql}",
            duration,
            eventData.CommandId,
            SanitizeSql(command.CommandText));

        base.CommandFailed(command, eventData);
    }

    public override async Task CommandFailedAsync(
        DbCommand command,
        CommandErrorEventData eventData,
        CancellationToken cancellationToken = default)
    {
        var duration = eventData.Duration.TotalMilliseconds;

        _logger.LogError(
            eventData.Exception,
            "EF Core query failed after {Duration}ms. CommandId: {CommandId}, SQL: {Sql}",
            duration,
            eventData.CommandId,
            SanitizeSql(command.CommandText));

        await base.CommandFailedAsync(command, eventData, cancellationToken);
    }

    private void LogCommandExecution(DbCommand command, CommandEventData eventData)
    {
        _logger.LogDebug(
            "Executing EF Core query. CommandId: {CommandId}, SQL: {Sql}",
            eventData.CommandId,
            SanitizeSql(command.CommandText));
    }

    private void LogCommandExecuted(DbCommand command, CommandExecutedEventData eventData)
    {
        var duration = eventData.Duration.TotalMilliseconds;

        if (_logQueryExecutionTime)
        {
            if (duration >= _slowQueryThresholdMs)
            {
                _logger.LogWarning(
                    "Slow EF Core query detected ({Duration}ms). CommandId: {CommandId}, SQL: {Sql}",
                    duration,
                    eventData.CommandId,
                    SanitizeSql(command.CommandText));

                // Log to slow query repository asynchronously (fire and forget)
                if (_slowQueryRepository != null)
                {
                    _ = Task.Run(async () =>
                    {
                        try
                        {
                            var slowQuery = new Domain.Models.SlowQuery
                            {
                                CorrelationId = Guid.NewGuid().ToString(),
                                SqlStatement = command.CommandText,
                                ExecutionTimeMs = (long)duration,
                                RowsAffected = 0,
                                Timestamp = DateTime.UtcNow
                            };
                            await _slowQueryRepository.LogSlowQueryAsync(slowQuery);
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError(ex, "Failed to log slow query to repository");
                        }
                    });
                }
            }
            else
            {
                _logger.LogInformation(
                    "EF Core query executed in {Duration}ms. CommandId: {CommandId}",
                    duration,
                    eventData.CommandId);
            }
        }
    }

    private static string SanitizeSql(string sql)
    {
        // Truncate very long SQL statements for logging
        const int maxLength = 500;
        if (sql.Length > maxLength)
        {
            return sql.Substring(0, maxLength) + "... [truncated]";
        }
        return sql;
    }

    private static string GetParametersSummary(DbCommand command)
    {
        if (command.Parameters.Count == 0)
        {
            return "No parameters";
        }

        var parameters = new List<string>();
        foreach (DbParameter param in command.Parameters)
        {
            var value = param.Value == null || param.Value == DBNull.Value
                ? "NULL"
                : param.Value.ToString();

            // Truncate long parameter values
            if (value != null && value.Length > 50)
            {
                value = value.Substring(0, 50) + "...";
            }

            parameters.Add($"{param.ParameterName}={value}");
        }

        return string.Join(", ", parameters);
    }
}
