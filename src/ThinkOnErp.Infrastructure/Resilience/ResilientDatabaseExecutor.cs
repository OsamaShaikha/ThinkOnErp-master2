using Microsoft.Extensions.Logging;
using Oracle.ManagedDataAccess.Client;
using System.Data;
using ThinkOnErp.Domain.Exceptions;

namespace ThinkOnErp.Infrastructure.Resilience;

/// <summary>
/// Provides resilient database operations with retry logic and circuit breaker pattern.
/// Implements Requirements 18.1, 18.3, 18.5, 18.8, 18.10
/// </summary>
public class ResilientDatabaseExecutor
{
    private readonly RetryPolicy _retryPolicy;
    private readonly CircuitBreaker _circuitBreaker;
    private readonly ILogger<ResilientDatabaseExecutor> _logger;

    public ResilientDatabaseExecutor(
        RetryPolicy retryPolicy,
        CircuitBreaker circuitBreaker,
        ILogger<ResilientDatabaseExecutor> logger)
    {
        _retryPolicy = retryPolicy;
        _circuitBreaker = circuitBreaker;
        _logger = logger;
    }

    /// <summary>
    /// Executes a database command with retry and circuit breaker protection.
    /// </summary>
    public async Task<T> ExecuteAsync<T>(
        Func<Task<T>> operation,
        string operationName,
        CancellationToken cancellationToken = default)
    {
        try
        {
            return await _circuitBreaker.ExecuteAsync(async () =>
            {
                return await _retryPolicy.ExecuteAsync(
                    operation,
                    operationName,
                    IsTransientDatabaseException);
            }, operationName);
        }
        catch (OracleException oracleEx)
        {
            _logger.LogError(oracleEx,
                "Oracle database error in {OperationName}. Error Code: {ErrorCode}",
                operationName, oracleEx.Number);

            throw new DatabaseConnectionException(operationName, oracleEx);
        }
        catch (TimeoutException timeoutEx)
        {
            _logger.LogError(timeoutEx,
                "Database operation {OperationName} timed out",
                operationName);

            throw new DatabaseConnectionException(operationName, timeoutEx);
        }
        catch (InvalidOperationException ex) when (ex.Message.Contains("Circuit breaker"))
        {
            _logger.LogError(ex,
                "Circuit breaker is open for {OperationName}",
                operationName);

            throw new DatabaseConnectionException(operationName, "Service temporarily unavailable due to repeated failures");
        }
    }

    /// <summary>
    /// Executes a database command with retry and circuit breaker protection (void return).
    /// </summary>
    public async Task ExecuteAsync(
        Func<Task> operation,
        string operationName,
        CancellationToken cancellationToken = default)
    {
        await ExecuteAsync(async () =>
        {
            await operation();
            return true;
        }, operationName, cancellationToken);
    }

    /// <summary>
    /// Executes a stored procedure with parameters and returns a result.
    /// </summary>
    public async Task<T> ExecuteStoredProcedureAsync<T>(
        OracleConnection connection,
        string procedureName,
        Func<OracleCommand, Task<T>> executeFunc,
        params OracleParameter[] parameters)
    {
        return await ExecuteAsync(async () =>
        {
            using var command = connection.CreateCommand();
            command.CommandText = procedureName;
            command.CommandType = CommandType.StoredProcedure;
            command.CommandTimeout = 30; // 30 seconds timeout

            if (parameters != null && parameters.Length > 0)
            {
                command.Parameters.AddRange(parameters);
            }

            if (connection.State != ConnectionState.Open)
            {
                await connection.OpenAsync();
            }

            return await executeFunc(command);
        }, $"SP_{procedureName}");
    }

    /// <summary>
    /// Determines if a database exception is transient and should be retried.
    /// </summary>
    private bool IsTransientDatabaseException(Exception ex)
    {
        if (ex is OracleException oracleEx)
        {
            // Oracle transient error codes
            return oracleEx.Number switch
            {
                // Connection errors
                1 => true,      // ORA-00001: unique constraint violated (can be transient in concurrent scenarios)
                51 => true,     // ORA-00051: timeout occurred while waiting for a resource
                54 => true,     // ORA-00054: resource busy and acquire with NOWAIT specified
                1012 => true,   // ORA-01012: not logged on
                1033 => true,   // ORA-01033: ORACLE initialization or shutdown in progress
                1034 => true,   // ORA-01034: ORACLE not available
                1089 => true,   // ORA-01089: immediate shutdown in progress
                3113 => true,   // ORA-03113: end-of-file on communication channel
                3114 => true,   // ORA-03114: not connected to ORACLE
                12150 => true,  // ORA-12150: TNS:unable to send data
                12154 => true,  // ORA-12154: TNS:could not resolve the connect identifier
                12157 => true,  // ORA-12157: TNS:internal network communication error
                12170 => true,  // ORA-12170: TNS:connect timeout occurred
                12541 => true,  // ORA-12541: TNS:no listener
                12543 => true,  // ORA-12543: TNS:destination host unreachable
                _ => false
            };
        }

        if (ex is TimeoutException)
        {
            return true;
        }

        if (ex is DatabaseConnectionException)
        {
            return true;
        }

        return false;
    }
}
