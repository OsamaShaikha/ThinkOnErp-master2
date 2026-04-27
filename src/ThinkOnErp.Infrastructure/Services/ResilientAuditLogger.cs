using Microsoft.Extensions.Logging;
using ThinkOnErp.Domain.Entities.Audit;
using ThinkOnErp.Domain.Interfaces;
using ThinkOnErp.Infrastructure.Resilience;

namespace ThinkOnErp.Infrastructure.Services;

/// <summary>
/// Resilient wrapper for AuditLogger that implements circuit breaker pattern
/// and retry logic for database failures.
/// 
/// This decorator adds an additional layer of resilience on top of the base AuditLogger:
/// - Circuit breaker pattern to prevent cascading failures
/// - Retry policy for transient database failures
/// - Fallback mechanisms when circuit is open
/// - Metrics tracking for circuit breaker state
/// 
/// Implements Task 16.1: ResilientAuditLogger with circuit breaker pattern
/// </summary>
public class ResilientAuditLogger : IAuditLogger
{
    private readonly IAuditLogger _innerLogger;
    private readonly CircuitBreaker _circuitBreaker;
    private readonly RetryPolicy _retryPolicy;
    private readonly ILogger<ResilientAuditLogger> _logger;
    private readonly ResilientAuditLoggerOptions _options;
    private readonly FileSystemAuditFallback? _fileSystemFallback;

    // Metrics
    private long _totalRequests = 0;
    private long _successfulRequests = 0;
    private long _failedRequests = 0;
    private long _circuitBreakerRejections = 0;
    private long _retriedRequests = 0;

    public ResilientAuditLogger(
        IAuditLogger innerLogger,
        CircuitBreaker circuitBreaker,
        RetryPolicy retryPolicy,
        ILogger<ResilientAuditLogger> logger,
        ResilientAuditLoggerOptions? options = null,
        FileSystemAuditFallback? fileSystemFallback = null)
    {
        _innerLogger = innerLogger ?? throw new ArgumentNullException(nameof(innerLogger));
        _circuitBreaker = circuitBreaker ?? throw new ArgumentNullException(nameof(circuitBreaker));
        _retryPolicy = retryPolicy ?? throw new ArgumentNullException(nameof(retryPolicy));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _options = options ?? new ResilientAuditLoggerOptions();
        _fileSystemFallback = fileSystemFallback;
    }

    /// <summary>
    /// Log a data change audit event with resilience patterns.
    /// </summary>
    public async Task LogDataChangeAsync(DataChangeAuditEvent auditEvent, CancellationToken cancellationToken = default)
    {
        await ExecuteWithResilienceAsync(
            async () => await _innerLogger.LogDataChangeAsync(auditEvent, cancellationToken),
            "LogDataChange",
            auditEvent,
            cancellationToken);
    }

    /// <summary>
    /// Log an authentication audit event with resilience patterns.
    /// </summary>
    public async Task LogAuthenticationAsync(AuthenticationAuditEvent auditEvent, CancellationToken cancellationToken = default)
    {
        await ExecuteWithResilienceAsync(
            async () => await _innerLogger.LogAuthenticationAsync(auditEvent, cancellationToken),
            "LogAuthentication",
            auditEvent,
            cancellationToken);
    }

    /// <summary>
    /// Log a permission change audit event with resilience patterns.
    /// </summary>
    public async Task LogPermissionChangeAsync(PermissionChangeAuditEvent auditEvent, CancellationToken cancellationToken = default)
    {
        await ExecuteWithResilienceAsync(
            async () => await _innerLogger.LogPermissionChangeAsync(auditEvent, cancellationToken),
            "LogPermissionChange",
            auditEvent,
            cancellationToken);
    }

    /// <summary>
    /// Log a configuration change audit event with resilience patterns.
    /// </summary>
    public async Task LogConfigurationChangeAsync(ConfigurationChangeAuditEvent auditEvent, CancellationToken cancellationToken = default)
    {
        await ExecuteWithResilienceAsync(
            async () => await _innerLogger.LogConfigurationChangeAsync(auditEvent, cancellationToken),
            "LogConfigurationChange",
            auditEvent,
            cancellationToken);
    }

    /// <summary>
    /// Log an exception audit event with resilience patterns.
    /// </summary>
    public async Task LogExceptionAsync(ExceptionAuditEvent auditEvent, CancellationToken cancellationToken = default)
    {
        await ExecuteWithResilienceAsync(
            async () => await _innerLogger.LogExceptionAsync(auditEvent, cancellationToken),
            "LogException",
            auditEvent,
            cancellationToken);
    }

    /// <summary>
    /// Log multiple audit events in a batch with resilience patterns.
    /// </summary>
    public async Task LogBatchAsync(IEnumerable<AuditEvent> auditEvents, CancellationToken cancellationToken = default)
    {
        var eventsList = auditEvents?.ToList();
        if (eventsList == null || !eventsList.Any())
        {
            return;
        }

        Interlocked.Increment(ref _totalRequests);

        try
        {
            // Check if circuit breaker is enabled
            if (_options.EnableCircuitBreaker)
            {
                // Execute with circuit breaker
                await _circuitBreaker.ExecuteAsync(async () =>
                {
                    // Execute with retry policy if enabled
                    if (_options.EnableRetryPolicy)
                    {
                        await _retryPolicy.ExecuteAsync(
                            async () => await _innerLogger.LogBatchAsync(eventsList, cancellationToken),
                            "LogBatch",
                            IsTransientFailure);
                        Interlocked.Increment(ref _retriedRequests);
                    }
                    else
                    {
                        await _innerLogger.LogBatchAsync(eventsList, cancellationToken);
                    }
                }, "ResilientAuditLogger.LogBatch");
            }
            else if (_options.EnableRetryPolicy)
            {
                // Execute with retry policy only
                await _retryPolicy.ExecuteAsync(
                    async () => await _innerLogger.LogBatchAsync(eventsList, cancellationToken),
                    "LogBatch",
                    IsTransientFailure);
                Interlocked.Increment(ref _retriedRequests);
            }
            else
            {
                // Execute without resilience patterns
                await _innerLogger.LogBatchAsync(eventsList, cancellationToken);
            }

            Interlocked.Increment(ref _successfulRequests);
        }
        catch (InvalidOperationException ex) when (ex.Message.Contains("Circuit breaker is open"))
        {
            // Circuit breaker is open - apply fallback mechanism for batch
            Interlocked.Increment(ref _circuitBreakerRejections);
            
            _logger.LogWarning(
                "Circuit breaker is open for LogBatch. Applying fallback mechanism. EventCount: {Count}",
                eventsList.Count);

            // Apply fallback mechanism for batch
            await ApplyBatchFallbackAsync(eventsList, cancellationToken);
        }
        catch (Exception ex)
        {
            Interlocked.Increment(ref _failedRequests);
            
            _logger.LogError(ex,
                "Failed to execute LogBatch after all resilience attempts. EventCount: {Count}",
                eventsList.Count);

            // Don't throw - audit logging failures should not break the application
        }
    }

    /// <summary>
    /// Check the health status of the audit logging system including resilience metrics.
    /// </summary>
    public async Task<bool> IsHealthyAsync()
    {
        try
        {
            // Check inner logger health
            var innerHealthy = await _innerLogger.IsHealthyAsync();
            
            // Check circuit breaker state
            var circuitState = _circuitBreaker.State;
            
            // Log health metrics
            _logger.LogDebug(
                "ResilientAuditLogger health check: InnerLogger={InnerHealthy}, Circuit={CircuitState}, " +
                "Total={Total}, Success={Success}, Failed={Failed}, Rejected={Rejected}, Retried={Retried}",
                innerHealthy, circuitState, _totalRequests, _successfulRequests, _failedRequests, 
                _circuitBreakerRejections, _retriedRequests);

            // System is healthy if inner logger is healthy and circuit is not open
            return innerHealthy && circuitState != CircuitState.Open;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "ResilientAuditLogger health check failed with exception");
            return false;
        }
    }

    /// <summary>
    /// Execute an audit logging operation with circuit breaker and retry patterns.
    /// </summary>
    private async Task ExecuteWithResilienceAsync(
        Func<Task> operation,
        string operationName,
        AuditEvent? auditEvent,
        CancellationToken cancellationToken)
    {
        Interlocked.Increment(ref _totalRequests);

        try
        {
            // Check if circuit breaker is enabled
            if (_options.EnableCircuitBreaker)
            {
                // Execute with circuit breaker
                await _circuitBreaker.ExecuteAsync(async () =>
                {
                    // Execute with retry policy if enabled
                    if (_options.EnableRetryPolicy)
                    {
                        await _retryPolicy.ExecuteAsync(
                            operation,
                            operationName,
                            IsTransientFailure);
                        Interlocked.Increment(ref _retriedRequests);
                    }
                    else
                    {
                        await operation();
                    }
                }, $"ResilientAuditLogger.{operationName}");
            }
            else if (_options.EnableRetryPolicy)
            {
                // Execute with retry policy only
                await _retryPolicy.ExecuteAsync(
                    operation,
                    operationName,
                    IsTransientFailure);
                Interlocked.Increment(ref _retriedRequests);
            }
            else
            {
                // Execute without resilience patterns
                await operation();
            }

            Interlocked.Increment(ref _successfulRequests);
        }
        catch (InvalidOperationException ex) when (ex.Message.Contains("Circuit breaker is open"))
        {
            // Circuit breaker is open - apply fallback mechanism
            Interlocked.Increment(ref _circuitBreakerRejections);
            
            _logger.LogWarning(
                "Circuit breaker is open for {OperationName}. Applying fallback mechanism. " +
                "Event: {EventType}, Entity: {EntityType}, CorrelationId: {CorrelationId}",
                operationName,
                auditEvent?.GetType().Name ?? "Batch",
                auditEvent?.EntityType ?? "Multiple",
                auditEvent?.CorrelationId ?? "N/A");

            // Apply fallback mechanism based on configuration
            await ApplyFallbackAsync(auditEvent, operationName, cancellationToken);
        }
        catch (Exception ex)
        {
            Interlocked.Increment(ref _failedRequests);
            
            _logger.LogError(ex,
                "Failed to execute {OperationName} after all resilience attempts. " +
                "Event: {EventType}, Entity: {EntityType}, CorrelationId: {CorrelationId}",
                operationName,
                auditEvent?.GetType().Name ?? "Batch",
                auditEvent?.EntityType ?? "Multiple",
                auditEvent?.CorrelationId ?? "N/A");

            // Don't throw - audit logging failures should not break the application
            // The event is already logged to standard logger above
        }
    }

    /// <summary>
    /// Determine if a failure is transient and should be retried.
    /// Handles database connection failures, timeouts, and Oracle-specific transient errors.
    /// </summary>
    private bool IsTransientFailure(Exception ex)
    {
        // Database connection failures
        if (ex is System.Data.Common.DbException)
            return true;

        // Timeout exceptions
        if (ex is TimeoutException)
            return true;

        // Task cancellation (may indicate timeout)
        if (ex is TaskCanceledException || ex is OperationCanceledException)
            return true;

        // Oracle-specific transient errors
        if (ex.GetType().Name == "OracleException")
        {
            // Use reflection to check Oracle error number
            var numberProperty = ex.GetType().GetProperty("Number");
            if (numberProperty != null)
            {
                var errorNumber = (int?)numberProperty.GetValue(ex);
                if (errorNumber.HasValue)
                {
                    return errorNumber.Value switch
                    {
                        // Deadlock and locking errors
                        60 => true,     // ORA-00060: deadlock detected while waiting for resource
                        54 => true,     // ORA-00054: resource busy and acquire with NOWAIT specified
                        
                        // Connection and network errors
                        1012 => true,   // ORA-01012: not logged on
                        1033 => true,   // ORA-01033: ORACLE initialization or shutdown in progress
                        1034 => true,   // ORA-01034: ORACLE not available
                        1089 => true,   // ORA-01089: immediate shutdown in progress
                        3113 => true,   // ORA-03113: end-of-file on communication channel
                        3114 => true,   // ORA-03114: not connected to ORACLE
                        12170 => true,  // ORA-12170: TNS:Connect timeout occurred
                        12541 => true,  // ORA-12541: TNS:no listener
                        12543 => true,  // ORA-12543: TNS:destination host unreachable
                        12545 => true,  // ORA-12545: Connect failed because target host or object does not exist
                        12560 => true,  // ORA-12560: TNS:protocol adapter error
                        12571 => true,  // ORA-12571: TNS:packet writer failure
                        
                        // Timeout errors
                        1013 => true,   // ORA-01013: user requested cancel of current operation
                        
                        // Temporary resource issues
                        30006 => true,  // ORA-30006: resource busy; acquire with WAIT timeout expired
                        
                        // Snapshot too old (can occur during long-running queries)
                        1555 => true,   // ORA-01555: snapshot too old
                        
                        // Non-transient errors (should not retry)
                        1 => false,      // ORA-00001: unique constraint violated (data issue, not transient)
                        1017 => false,   // ORA-01017: invalid username/password (authentication issue)
                        1400 => false,   // ORA-01400: cannot insert NULL (data validation issue)
                        2291 => false,   // ORA-02291: integrity constraint violated - parent key not found
                        2292 => false,   // ORA-02292: integrity constraint violated - child record found
                        
                        _ => false
                    };
                }
            }
        }

        // Check inner exception recursively
        if (ex.InnerException != null)
            return IsTransientFailure(ex.InnerException);

        return false;
    }

    /// <summary>
    /// Apply fallback mechanism when circuit breaker is open.
    /// </summary>
    private async Task ApplyFallbackAsync(
        AuditEvent? auditEvent,
        string operationName,
        CancellationToken cancellationToken)
    {
        try
        {
            switch (_options.FallbackStrategy)
            {
                case FallbackStrategy.LogToFile:
                    // Use structured FileSystemAuditFallback if available
                    if (_fileSystemFallback != null && auditEvent != null)
                    {
                        await _fileSystemFallback.WriteAsync(auditEvent, cancellationToken);
                    }
                    else
                    {
                        // Fallback to simple text logging
                        await LogToFileAsync(auditEvent, operationName);
                    }
                    break;

                case FallbackStrategy.LogToConsole:
                    LogToConsole(auditEvent, operationName);
                    break;

                case FallbackStrategy.Silent:
                    // Do nothing - already logged warning above
                    break;

                case FallbackStrategy.Throw:
                    throw new InvalidOperationException(
                        $"Circuit breaker is open for {operationName} and fallback strategy is Throw");

                default:
                    _logger.LogWarning(
                        "Unknown fallback strategy: {Strategy}. Using Silent fallback.",
                        _options.FallbackStrategy);
                    break;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to apply fallback mechanism for {OperationName}", operationName);
        }
    }

    /// <summary>
    /// Apply fallback mechanism for batch operations when circuit breaker is open.
    /// </summary>
    private async Task ApplyBatchFallbackAsync(
        IEnumerable<AuditEvent> auditEvents,
        CancellationToken cancellationToken)
    {
        try
        {
            switch (_options.FallbackStrategy)
            {
                case FallbackStrategy.LogToFile:
                    // Use structured FileSystemAuditFallback if available
                    if (_fileSystemFallback != null)
                    {
                        await _fileSystemFallback.WriteBatchAsync(auditEvents, cancellationToken);
                    }
                    else
                    {
                        // Fallback to simple text logging for each event
                        foreach (var auditEvent in auditEvents)
                        {
                            await LogToFileAsync(auditEvent, "LogBatch");
                        }
                    }
                    break;

                case FallbackStrategy.LogToConsole:
                    foreach (var auditEvent in auditEvents)
                    {
                        LogToConsole(auditEvent, "LogBatch");
                    }
                    break;

                case FallbackStrategy.Silent:
                    // Do nothing - already logged warning above
                    break;

                case FallbackStrategy.Throw:
                    throw new InvalidOperationException(
                        "Circuit breaker is open for LogBatch and fallback strategy is Throw");

                default:
                    _logger.LogWarning(
                        "Unknown fallback strategy: {Strategy}. Using Silent fallback.",
                        _options.FallbackStrategy);
                    break;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to apply batch fallback mechanism");
        }
    }

    /// <summary>
    /// Log audit event to file as fallback.
    /// </summary>
    private async Task LogToFileAsync(AuditEvent? auditEvent, string operationName)
    {
        if (string.IsNullOrEmpty(_options.FallbackFilePath))
        {
            _logger.LogWarning("Fallback file path is not configured. Cannot log to file.");
            return;
        }

        try
        {
            var logEntry = $"[{DateTime.UtcNow:O}] {operationName} - " +
                          $"Event: {auditEvent?.GetType().Name ?? "Batch"}, " +
                          $"Entity: {auditEvent?.EntityType ?? "Multiple"}, " +
                          $"Action: {auditEvent?.Action ?? "N/A"}, " +
                          $"Actor: {auditEvent?.ActorType ?? "N/A"}:{auditEvent?.ActorId ?? 0}, " +
                          $"CorrelationId: {auditEvent?.CorrelationId ?? "N/A"}" +
                          Environment.NewLine;

            await File.AppendAllTextAsync(_options.FallbackFilePath, logEntry);
            
            _logger.LogInformation(
                "Audit event logged to fallback file: {FilePath}",
                _options.FallbackFilePath);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to log audit event to fallback file: {FilePath}", _options.FallbackFilePath);
        }
    }

    /// <summary>
    /// Log audit event to console as fallback.
    /// </summary>
    private void LogToConsole(AuditEvent? auditEvent, string operationName)
    {
        var logEntry = $"[AUDIT FALLBACK] {operationName} - " +
                      $"Event: {auditEvent?.GetType().Name ?? "Batch"}, " +
                      $"Entity: {auditEvent?.EntityType ?? "Multiple"}, " +
                      $"Action: {auditEvent?.Action ?? "N/A"}, " +
                      $"Actor: {auditEvent?.ActorType ?? "N/A"}:{auditEvent?.ActorId ?? 0}, " +
                      $"CorrelationId: {auditEvent?.CorrelationId ?? "N/A"}";

        Console.WriteLine(logEntry);
        
        _logger.LogInformation("Audit event logged to console as fallback");
    }

    /// <summary>
    /// Get resilience metrics for monitoring.
    /// </summary>
    public ResilientAuditLoggerMetrics GetMetrics()
    {
        return new ResilientAuditLoggerMetrics
        {
            TotalRequests = _totalRequests,
            SuccessfulRequests = _successfulRequests,
            FailedRequests = _failedRequests,
            CircuitBreakerRejections = _circuitBreakerRejections,
            RetriedRequests = _retriedRequests,
            CircuitState = _circuitBreaker.State,
            SuccessRate = _totalRequests > 0 ? (_successfulRequests * 100.0) / _totalRequests : 100.0,
            FailureRate = _totalRequests > 0 ? (_failedRequests * 100.0) / _totalRequests : 0.0,
            RejectionRate = _totalRequests > 0 ? (_circuitBreakerRejections * 100.0) / _totalRequests : 0.0
        };
    }

    /// <summary>
    /// Reset resilience metrics.
    /// </summary>
    public void ResetMetrics()
    {
        Interlocked.Exchange(ref _totalRequests, 0);
        Interlocked.Exchange(ref _successfulRequests, 0);
        Interlocked.Exchange(ref _failedRequests, 0);
        Interlocked.Exchange(ref _circuitBreakerRejections, 0);
        Interlocked.Exchange(ref _retriedRequests, 0);
        
        _logger.LogInformation("ResilientAuditLogger metrics reset");
    }

    /// <summary>
    /// Get the current queue depth from the inner logger.
    /// </summary>
    public int GetQueueDepth()
    {
        // Delegate to inner logger if it supports queue depth
        if (_innerLogger is AuditLogger auditLogger)
        {
            return auditLogger.GetQueueDepth();
        }
        
        // Return 0 if inner logger doesn't support queue depth
        return 0;
    }

    /// <summary>
    /// Replay fallback events from file system to database.
    /// This should be called when the database becomes available again.
    /// Returns the number of successfully replayed events.
    /// </summary>
    public async Task<int> ReplayFallbackEventsAsync(CancellationToken cancellationToken = default)
    {
        if (_fileSystemFallback == null)
        {
            _logger.LogWarning("FileSystemAuditFallback is not configured, cannot replay events");
            return 0;
        }

        _logger.LogInformation("Starting replay of fallback events");

        try
        {
            // Define replay action that routes events to the appropriate logger method
            async Task ReplayAction(AuditEvent auditEvent)
            {
                switch (auditEvent)
                {
                    case DataChangeAuditEvent dataChange:
                        await _innerLogger.LogDataChangeAsync(dataChange, cancellationToken);
                        break;
                    case AuthenticationAuditEvent authentication:
                        await _innerLogger.LogAuthenticationAsync(authentication, cancellationToken);
                        break;
                    case PermissionChangeAuditEvent permissionChange:
                        await _innerLogger.LogPermissionChangeAsync(permissionChange, cancellationToken);
                        break;
                    case ConfigurationChangeAuditEvent configChange:
                        await _innerLogger.LogConfigurationChangeAsync(configChange, cancellationToken);
                        break;
                    case ExceptionAuditEvent exception:
                        await _innerLogger.LogExceptionAsync(exception, cancellationToken);
                        break;
                    default:
                        _logger.LogWarning("Unknown audit event type: {Type}", auditEvent.GetType().Name);
                        break;
                }
            }

            var replayedCount = await _fileSystemFallback.ReplayFallbackEventsAsync(ReplayAction, cancellationToken);
            
            _logger.LogInformation("Replay completed: {Count} events replayed successfully", replayedCount);
            
            return replayedCount;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to replay fallback events");
            return 0;
        }
    }

    /// <summary>
    /// Get the count of pending fallback files waiting to be replayed.
    /// </summary>
    public int GetPendingFallbackCount()
    {
        return _fileSystemFallback?.GetPendingFileCount() ?? 0;
    }
}

/// <summary>
/// Configuration options for ResilientAuditLogger.
/// </summary>
public class ResilientAuditLoggerOptions
{
    /// <summary>
    /// Enable circuit breaker pattern.
    /// Default: true
    /// </summary>
    public bool EnableCircuitBreaker { get; set; } = true;

    /// <summary>
    /// Enable retry policy for transient failures.
    /// Default: true
    /// </summary>
    public bool EnableRetryPolicy { get; set; } = true;

    /// <summary>
    /// Fallback strategy when circuit breaker is open.
    /// Default: LogToFile
    /// </summary>
    public FallbackStrategy FallbackStrategy { get; set; } = FallbackStrategy.LogToFile;

    /// <summary>
    /// File path for fallback logging when circuit breaker is open.
    /// Default: "logs/audit-fallback.log"
    /// </summary>
    public string FallbackFilePath { get; set; } = "logs/audit-fallback.log";
}

/// <summary>
/// Fallback strategies when circuit breaker is open.
/// </summary>
public enum FallbackStrategy
{
    /// <summary>
    /// Log audit events to a file.
    /// </summary>
    LogToFile,

    /// <summary>
    /// Log audit events to console.
    /// </summary>
    LogToConsole,

    /// <summary>
    /// Silently ignore (only log warning).
    /// </summary>
    Silent,

    /// <summary>
    /// Throw exception (not recommended for production).
    /// </summary>
    Throw
}

/// <summary>
/// Metrics for ResilientAuditLogger monitoring.
/// </summary>
public class ResilientAuditLoggerMetrics
{
    public long TotalRequests { get; set; }
    public long SuccessfulRequests { get; set; }
    public long FailedRequests { get; set; }
    public long CircuitBreakerRejections { get; set; }
    public long RetriedRequests { get; set; }
    public CircuitState CircuitState { get; set; }
    public double SuccessRate { get; set; }
    public double FailureRate { get; set; }
    public double RejectionRate { get; set; }
}
