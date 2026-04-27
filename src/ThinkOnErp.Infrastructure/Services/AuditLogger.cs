using System.Text.Json;
using System.Threading.Channels;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ThinkOnErp.Domain.Entities;
using ThinkOnErp.Domain.Entities.Audit;
using ThinkOnErp.Domain.Interfaces;
using ThinkOnErp.Domain.Models;
using ThinkOnErp.Infrastructure.Configuration;
using ThinkOnErp.Infrastructure.Resilience;

namespace ThinkOnErp.Infrastructure.Services;

/// <summary>
/// High-performance async audit logger using System.Threading.Channels.
/// Implements asynchronous audit event processing with batching and backpressure.
/// Runs as a background service to process queued audit events.
/// Includes circuit breaker pattern for database failure protection.
/// </summary>
public class AuditLogger : IAuditLogger, IHostedService
{
    private readonly Channel<AuditEvent> _channel;
    private readonly IServiceScopeFactory _serviceScopeFactory;
    private readonly ILogger<AuditLogger> _logger;
    private readonly AuditLoggingOptions _options;
    private readonly CircuitBreakerRegistry _circuitBreakerRegistry;
    private Task? _processingTask;
    private readonly CancellationTokenSource _shutdownCts = new();

    public AuditLogger(
        IServiceScopeFactory serviceScopeFactory,
        ILogger<AuditLogger> logger,
        IOptions<AuditLoggingOptions> options,
        CircuitBreakerRegistry circuitBreakerRegistry)
    {
        _serviceScopeFactory = serviceScopeFactory;
        _logger = logger;
        _options = options.Value;
        _circuitBreakerRegistry = circuitBreakerRegistry;

        // Create bounded channel with backpressure
        var channelOptions = new BoundedChannelOptions(_options.MaxQueueSize)
        {
            FullMode = BoundedChannelFullMode.Wait, // Apply backpressure when full
            SingleReader = true, // Only one background task reads from channel
            SingleWriter = false // Multiple threads can write to channel
        };

        _channel = Channel.CreateBounded<AuditEvent>(channelOptions);
    }

    /// <summary>
    /// Log a data change audit event asynchronously.
    /// </summary>
    public async Task LogDataChangeAsync(DataChangeAuditEvent auditEvent, CancellationToken cancellationToken = default)
    {
        if (!_options.Enabled)
            return;

        try
        {
            // Mask sensitive data before queuing (using scoped service)
            using var scope = _serviceScopeFactory.CreateScope();
            var dataMasker = scope.ServiceProvider.GetRequiredService<ISensitiveDataMasker>();
            MaskSensitiveData(auditEvent, dataMasker);

            // Enrich with correlation ID if not set
            if (string.IsNullOrEmpty(auditEvent.CorrelationId))
            {
                auditEvent.CorrelationId = CorrelationContext.GetOrCreate();
            }

            // Write to channel (will wait if channel is full due to backpressure)
            await _channel.Writer.WriteAsync(auditEvent, cancellationToken);
        }
        catch (InvalidOperationException)
        {
            // Channel is closed, log warning but don't throw
            _logger.LogWarning("Attempted to write to closed audit channel");
        }
        catch (OperationCanceledException)
        {
            // Operation was cancelled, this is expected during shutdown
            _logger.LogDebug("Audit logging cancelled during shutdown");
        }
        catch (Exception ex)
        {
            // Don't let audit logging failures break the application
            _logger.LogError(ex, "Failed to queue audit event for logging");
        }
    }

    /// <summary>
    /// Log an authentication audit event asynchronously.
    /// </summary>
    public async Task LogAuthenticationAsync(AuthenticationAuditEvent auditEvent, CancellationToken cancellationToken = default)
    {
        if (!_options.Enabled)
            return;

        try
        {
            // Enrich with correlation ID if not set
            if (string.IsNullOrEmpty(auditEvent.CorrelationId))
            {
                auditEvent.CorrelationId = CorrelationContext.GetOrCreate();
            }

            await _channel.Writer.WriteAsync(auditEvent, cancellationToken);
        }
        catch (InvalidOperationException)
        {
            _logger.LogWarning("Attempted to write to closed audit channel");
        }
        catch (OperationCanceledException)
        {
            _logger.LogDebug("Audit logging cancelled during shutdown");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to queue authentication audit event");
        }
    }

    /// <summary>
    /// Log a permission change audit event asynchronously.
    /// </summary>
    public async Task LogPermissionChangeAsync(PermissionChangeAuditEvent auditEvent, CancellationToken cancellationToken = default)
    {
        if (!_options.Enabled)
            return;

        try
        {
            // Mask sensitive data in permission JSON (using scoped service)
            using var scope = _serviceScopeFactory.CreateScope();
            var dataMasker = scope.ServiceProvider.GetRequiredService<ISensitiveDataMasker>();
            auditEvent.PermissionBefore = dataMasker.MaskSensitiveFields(auditEvent.PermissionBefore);
            auditEvent.PermissionAfter = dataMasker.MaskSensitiveFields(auditEvent.PermissionAfter);

            // Enrich with correlation ID if not set
            if (string.IsNullOrEmpty(auditEvent.CorrelationId))
            {
                auditEvent.CorrelationId = CorrelationContext.GetOrCreate();
            }

            await _channel.Writer.WriteAsync(auditEvent, cancellationToken);
        }
        catch (InvalidOperationException)
        {
            _logger.LogWarning("Attempted to write to closed audit channel");
        }
        catch (OperationCanceledException)
        {
            _logger.LogDebug("Audit logging cancelled during shutdown");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to queue permission change audit event");
        }
    }

    /// <summary>
    /// Log a configuration change audit event asynchronously.
    /// </summary>
    public async Task LogConfigurationChangeAsync(ConfigurationChangeAuditEvent auditEvent, CancellationToken cancellationToken = default)
    {
        if (!_options.Enabled)
            return;

        try
        {
            // Mask sensitive data in configuration values (using scoped service)
            using var scope = _serviceScopeFactory.CreateScope();
            var dataMasker = scope.ServiceProvider.GetRequiredService<ISensitiveDataMasker>();
            auditEvent.OldValue = dataMasker.MaskSensitiveFields(auditEvent.OldValue);
            auditEvent.NewValue = dataMasker.MaskSensitiveFields(auditEvent.NewValue);

            // Enrich with correlation ID if not set
            if (string.IsNullOrEmpty(auditEvent.CorrelationId))
            {
                auditEvent.CorrelationId = CorrelationContext.GetOrCreate();
            }

            await _channel.Writer.WriteAsync(auditEvent, cancellationToken);
        }
        catch (InvalidOperationException)
        {
            _logger.LogWarning("Attempted to write to closed audit channel");
        }
        catch (OperationCanceledException)
        {
            _logger.LogDebug("Audit logging cancelled during shutdown");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to queue configuration change audit event");
        }
    }

    /// <summary>
    /// Log an exception audit event asynchronously.
    /// Triggers critical alerts when severity is Critical.
    /// </summary>
    public async Task LogExceptionAsync(ExceptionAuditEvent auditEvent, CancellationToken cancellationToken = default)
    {
        if (!_options.Enabled)
            return;

        try
        {
            // Truncate stack trace if too long (using scoped service)
            using var scope = _serviceScopeFactory.CreateScope();
            var dataMasker = scope.ServiceProvider.GetRequiredService<ISensitiveDataMasker>();
            auditEvent.StackTrace = dataMasker.TruncateIfNeeded(auditEvent.StackTrace);
            auditEvent.InnerException = dataMasker.TruncateIfNeeded(auditEvent.InnerException);

            // Enrich with correlation ID if not set
            if (string.IsNullOrEmpty(auditEvent.CorrelationId))
            {
                auditEvent.CorrelationId = CorrelationContext.GetOrCreate();
            }

            await _channel.Writer.WriteAsync(auditEvent, cancellationToken);

            // Trigger alert for critical exceptions (fire-and-forget)
            if (auditEvent.Severity == "Critical")
            {
                _ = TriggerCriticalExceptionAlertAsync(auditEvent, scope);
            }
        }
        catch (InvalidOperationException)
        {
            _logger.LogWarning("Attempted to write to closed audit channel");
        }
        catch (OperationCanceledException)
        {
            _logger.LogDebug("Audit logging cancelled during shutdown");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to queue exception audit event");
        }
    }

    /// <summary>
    /// Triggers a critical alert for critical exceptions.
    /// </summary>
    private async Task TriggerCriticalExceptionAlertAsync(ExceptionAuditEvent auditEvent, IServiceScope scope)
    {
        try
        {
            var alertManager = scope.ServiceProvider.GetService<IAlertManager>();
            if (alertManager == null)
            {
                _logger.LogWarning("AlertManager not available, skipping critical exception alert");
                return;
            }

            var alert = new Alert
            {
                AlertType = "CriticalException",
                Severity = "Critical",
                Title = $"Critical Exception: {auditEvent.ExceptionType}",
                Description = $"A critical exception occurred in the system.\n\n" +
                         $"Exception Type: {auditEvent.ExceptionType}\n" +
                         $"Message: {auditEvent.ExceptionMessage}\n" +
                         $"Entity: {auditEvent.EntityType} (ID: {auditEvent.EntityId})\n" +
                         $"Actor: {auditEvent.ActorType} (ID: {auditEvent.ActorId})\n" +
                         $"Company ID: {auditEvent.CompanyId}\n" +
                         $"Correlation ID: {auditEvent.CorrelationId}\n" +
                         $"Timestamp: {auditEvent.Timestamp:yyyy-MM-dd HH:mm:ss UTC}",
                TriggeredAt = auditEvent.Timestamp,
                CorrelationId = auditEvent.CorrelationId,
                Metadata = JsonSerializer.Serialize(new Dictionary<string, object>
                {
                    ["ExceptionType"] = auditEvent.ExceptionType,
                    ["EntityType"] = auditEvent.EntityType,
                    ["EntityId"] = auditEvent.EntityId ?? 0,
                    ["ActorId"] = auditEvent.ActorId,
                    ["CompanyId"] = auditEvent.CompanyId ?? 0
                })
            };

            await alertManager.TriggerAlertAsync(alert);
            _logger.LogInformation("Critical exception alert triggered for {ExceptionType} with correlation ID {CorrelationId}",
                auditEvent.ExceptionType, auditEvent.CorrelationId);
        }
        catch (Exception ex)
        {
            // Don't let alert triggering failures break audit logging
            _logger.LogError(ex, "Failed to trigger critical exception alert for correlation ID {CorrelationId}",
                auditEvent.CorrelationId);
        }
    }

    /// <summary>
    /// Log multiple audit events in a single batch operation.
    /// </summary>
    public async Task LogBatchAsync(IEnumerable<AuditEvent> auditEvents, CancellationToken cancellationToken = default)
    {
        if (!_options.Enabled)
            return;

        try
        {
            foreach (var auditEvent in auditEvents)
            {
                // Process each event based on its type
                switch (auditEvent)
                {
                    case DataChangeAuditEvent dataChange:
                        await LogDataChangeAsync(dataChange, cancellationToken);
                        break;
                    case AuthenticationAuditEvent auth:
                        await LogAuthenticationAsync(auth, cancellationToken);
                        break;
                    case PermissionChangeAuditEvent permission:
                        await LogPermissionChangeAsync(permission, cancellationToken);
                        break;
                    case ConfigurationChangeAuditEvent config:
                        await LogConfigurationChangeAsync(config, cancellationToken);
                        break;
                    case ExceptionAuditEvent exception:
                        await LogExceptionAsync(exception, cancellationToken);
                        break;
                    default:
                        _logger.LogWarning("Unknown audit event type: {EventType}", auditEvent.GetType().Name);
                        break;
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to queue batch audit events");
        }
    }

    /// <summary>
    /// Check the health status of the audit logging system.
    /// Verifies circuit breaker state, queue depth, processing status, and database connectivity.
    /// </summary>
    public async Task<bool> IsHealthyAsync()
    {
        try
        {
            // Check if background processing task is running
            if (_processingTask == null || _processingTask.IsCompleted)
            {
                _logger.LogWarning("Audit logger health check failed: Background processing task is not running");
                return false;
            }

            // Check if background processing task has faulted
            if (_processingTask.IsFaulted)
            {
                _logger.LogWarning("Audit logger health check failed: Background processing task has faulted. Exception: {Exception}", 
                    _processingTask.Exception?.GetBaseException().Message);
                return false;
            }

            // Check circuit breaker state if enabled
            CircuitBreaker? circuitBreaker = null;
            if (_options.EnableCircuitBreaker)
            {
                circuitBreaker = _circuitBreakerRegistry.GetOrCreate("AuditLogger");
                var circuitState = circuitBreaker.State;
                if (circuitState == CircuitState.Open)
                {
                    _logger.LogWarning("Audit logger health check failed: Circuit breaker is open");
                    return false;
                }
            }

            // Check queue depth - warn if approaching capacity
            var queueCount = _channel.Reader.Count;
            var queueCapacityPercent = (queueCount * 100.0) / _options.MaxQueueSize;
            
            if (queueCapacityPercent > 90)
            {
                _logger.LogWarning(
                    "Audit logger queue is at {Percent:F1}% capacity ({Count}/{MaxSize}). System may be under heavy load or database is slow.",
                    queueCapacityPercent, queueCount, _options.MaxQueueSize);
            }

            // Fail health check if queue is completely full
            if (queueCount >= _options.MaxQueueSize)
            {
                _logger.LogError(
                    "Audit logger health check failed: Queue is full ({Count}/{MaxSize}). Backpressure is being applied.",
                    queueCount, _options.MaxQueueSize);
                return false;
            }

            // Check if channel is still accepting writes
            if (!_channel.Writer.TryWrite(new TestAuditEvent()))
            {
                _logger.LogWarning("Audit logger health check failed: Channel is not accepting writes");
                return false;
            }

            // Check repository health (database connectivity)
            using var scope = _serviceScopeFactory.CreateScope();
            var repository = scope.ServiceProvider.GetRequiredService<IAuditRepository>();
            var repositoryHealthy = await repository.IsHealthyAsync();
            if (!repositoryHealthy)
            {
                _logger.LogWarning("Audit logger health check failed: Repository health check failed");
                return false;
            }

            // All checks passed
            _logger.LogDebug(
                "Audit logger health check passed. Queue: {Count}/{MaxSize} ({Percent:F1}%), Circuit: {CircuitState}, Processing: Running",
                queueCount, _options.MaxQueueSize, queueCapacityPercent,
                circuitBreaker?.State.ToString() ?? "Disabled");

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Audit logger health check failed with exception");
            return false;
        }
    }

    /// <summary>
    /// Start the background processing task.
    /// </summary>
    public Task StartAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Starting audit logger background service");
        _processingTask = ProcessAuditEventsAsync(_shutdownCts.Token);
        return Task.CompletedTask;
    }

    /// <summary>
    /// Stop the background processing task and flush remaining events.
    /// </summary>
    public async Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Stopping audit logger background service");

        // Signal no more writes
        _channel.Writer.Complete();
        _shutdownCts.Cancel();

        // Wait for processing to complete
        if (_processingTask != null)
        {
            await _processingTask;
        }

        _logger.LogInformation("Audit logger background service stopped");
    }

    /// <summary>
    /// Background task that processes audit events from the channel.
    /// Implements batch processing with configurable batch size and time window.
    /// Batches are written when either:
    /// - Batch size is reached (default: 50 events)
    /// - Batch window expires (default: 100ms)
    /// </summary>
    private async Task ProcessAuditEventsAsync(CancellationToken cancellationToken)
    {
        var batch = new List<AuditEvent>(_options.BatchSize);
        using var batchWindowCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        Task? batchWindowTask = null;

        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                // Try to read from channel with timeout
                var readTask = _channel.Reader.WaitToReadAsync(cancellationToken).AsTask();
                var completedTask = batchWindowTask != null 
                    ? await Task.WhenAny(readTask, batchWindowTask)
                    : await Task.WhenAny(readTask);

                // Check if batch window expired
                if (completedTask == batchWindowTask && batch.Count > 0)
                {
                    _logger.LogDebug("Batch window expired, flushing {Count} events", batch.Count);
                    await WriteBatchAsync(batch, cancellationToken);
                    batch.Clear();
                    batchWindowTask = null;
                    continue;
                }

                // Check if channel has data available
                if (!await readTask)
                {
                    // Channel is complete and no more data
                    break;
                }

                // Read available events up to batch size
                while (_channel.Reader.TryRead(out var auditEvent))
                {
                    // Skip test events used for health checks
                    if (auditEvent is TestAuditEvent)
                        continue;

                    batch.Add(auditEvent);

                    // Start batch window timer on first event
                    if (batch.Count == 1)
                    {
                        batchWindowTask = Task.Delay(_options.BatchWindowMs, cancellationToken);
                        _logger.LogTrace("Started batch window timer for {WindowMs}ms", _options.BatchWindowMs);
                    }

                    // Flush batch if size limit reached
                    if (batch.Count >= _options.BatchSize)
                    {
                        _logger.LogDebug("Batch size limit reached, flushing {Count} events", batch.Count);
                        await WriteBatchAsync(batch, cancellationToken);
                        batch.Clear();
                        batchWindowTask = null;
                        break;
                    }
                }
            }
        }
        catch (OperationCanceledException)
        {
            // Expected during shutdown
            _logger.LogDebug("Audit event processing cancelled");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing audit events");
        }
        finally
        {
            // Flush remaining events
            if (batch.Count > 0)
            {
                try
                {
                    await WriteBatchAsync(batch, CancellationToken.None);
                    _logger.LogInformation("Flushed {Count} remaining audit events during shutdown", batch.Count);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to flush remaining audit events during shutdown");
                }
            }
        }
    }

    /// <summary>
    /// Write a batch of audit events to the database.
    /// Uses circuit breaker pattern to protect against database failures.
    /// </summary>
    private async Task WriteBatchAsync(List<AuditEvent> batch, CancellationToken cancellationToken)
    {
        try
        {
            // Create a scope to resolve scoped services
            using var scope = _serviceScopeFactory.CreateScope();
            var repository = scope.ServiceProvider.GetRequiredService<IAuditRepository>();
            var dataMasker = scope.ServiceProvider.GetRequiredService<ISensitiveDataMasker>();
            var legacyAuditService = scope.ServiceProvider.GetRequiredService<ILegacyAuditService>();
            
            // Convert AuditEvent objects to SysAuditLog entities
            var auditLogs = batch.Select(e => MapToSysAuditLog(e, dataMasker, legacyAuditService)).ToList();
            
            int insertedCount;

            // Use circuit breaker if enabled
            if (_options.EnableCircuitBreaker)
            {
                var circuitBreaker = _circuitBreakerRegistry.GetOrCreate("AuditLogger");
                insertedCount = await circuitBreaker.ExecuteAsync(
                    async () => await repository.InsertBatchAsync(auditLogs, cancellationToken),
                    "AuditLogger.WriteBatch");
            }
            else
            {
                insertedCount = await repository.InsertBatchAsync(auditLogs, cancellationToken);
            }

            _logger.LogDebug("Successfully wrote {Count} audit events to database", insertedCount);
        }
        catch (InvalidOperationException ex) when (ex.Message.Contains("Circuit breaker is open"))
        {
            // Circuit breaker is open - queue is protected from cascading failures
            _logger.LogWarning(
                "Circuit breaker is open for audit logging. Batch of {BatchSize} events will be retried later. Message: {Message}",
                batch.Count, ex.Message);

            // Re-queue events for retry when circuit closes
            await RequeueEventsAsync(batch, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to write audit batch to database. Batch size: {BatchSize}", batch.Count);

            // If circuit breaker is disabled, attempt to re-queue for retry
            if (!_options.EnableCircuitBreaker)
            {
                await RequeueEventsAsync(batch, cancellationToken);
            }
        }
    }

    /// <summary>
    /// Map an AuditEvent to a SysAuditLog entity for database persistence.
    /// </summary>
    private SysAuditLog MapToSysAuditLog(AuditEvent auditEvent, ISensitiveDataMasker dataMasker, ILegacyAuditService legacyAuditService)
    {
        var auditLog = new SysAuditLog
        {
            ActorType = auditEvent.ActorType,
            ActorId = auditEvent.ActorId,
            CompanyId = auditEvent.CompanyId,
            BranchId = auditEvent.BranchId,
            Action = auditEvent.Action,
            EntityType = auditEvent.EntityType,
            EntityId = auditEvent.EntityId,
            IpAddress = auditEvent.IpAddress,
            UserAgent = auditEvent.UserAgent,
            CorrelationId = auditEvent.CorrelationId,
            CreationDate = auditEvent.Timestamp
        };

        // Map specific event types to their additional properties
        switch (auditEvent)
        {
            case DataChangeAuditEvent dataChange:
                auditLog.OldValue = dataChange.OldValue;
                auditLog.NewValue = dataChange.NewValue;
                auditLog.EventCategory = "DataChange";
                break;

            case AuthenticationAuditEvent auth:
                auditLog.EventCategory = "Authentication";
                auditLog.Metadata = System.Text.Json.JsonSerializer.Serialize(new
                {
                    auth.Success,
                    auth.FailureReason,
                    auth.TokenId,
                    auth.SessionDuration
                });
                break;

            case PermissionChangeAuditEvent permission:
                auditLog.OldValue = permission.PermissionBefore;
                auditLog.NewValue = permission.PermissionAfter;
                auditLog.EventCategory = "Permission";
                auditLog.Metadata = System.Text.Json.JsonSerializer.Serialize(new
                {
                    permission.RoleId,
                    permission.PermissionId
                });
                break;

            case ConfigurationChangeAuditEvent config:
                auditLog.OldValue = config.OldValue;
                auditLog.NewValue = config.NewValue;
                auditLog.EventCategory = "Configuration";
                auditLog.Metadata = System.Text.Json.JsonSerializer.Serialize(new
                {
                    config.SettingName,
                    config.Source
                });
                break;

            case ExceptionAuditEvent exception:
                auditLog.EventCategory = "Exception";
                auditLog.ExceptionType = exception.ExceptionType;
                auditLog.ExceptionMessage = exception.ExceptionMessage;
                auditLog.StackTrace = exception.StackTrace;
                auditLog.Severity = exception.Severity;
                auditLog.Metadata = System.Text.Json.JsonSerializer.Serialize(new
                {
                    exception.InnerException
                });
                break;
        }

        // Automatically populate legacy fields for backward compatibility
        try
        {
            // BUSINESS_MODULE: Map endpoints to business modules (POS, HR, Accounting, etc.)
            auditLog.BusinessModule = legacyAuditService.DetermineBusinessModuleAsync(
                auditEvent.EntityType, 
                null).GetAwaiter().GetResult();

            // DEVICE_IDENTIFIER: Extract device information from User-Agent and IP address
            auditLog.DeviceIdentifier = legacyAuditService.ExtractDeviceIdentifierAsync(
                auditEvent.UserAgent ?? string.Empty, 
                auditEvent.IpAddress).GetAwaiter().GetResult();

            // ERROR_CODE: Generate standardized error codes for exceptions
            if (auditEvent is ExceptionAuditEvent exceptionEvent)
            {
                auditLog.ErrorCode = legacyAuditService.GenerateErrorCodeAsync(
                    exceptionEvent.ExceptionType, 
                    auditEvent.EntityType).GetAwaiter().GetResult();
            }

            // BUSINESS_DESCRIPTION: Create human-readable error descriptions
            // Note: This requires converting to AuditLogEntry first, so we'll create a minimal one
            var tempAuditEntry = new AuditLogEntry
            {
                RowId = 0, // Will be set by database
                Action = auditEvent.Action,
                EntityType = auditEvent.EntityType,
                ActorName = null, // Will be populated by database join
                ExceptionType = auditEvent is ExceptionAuditEvent ex ? ex.ExceptionType : null,
                ExceptionMessage = auditEvent is ExceptionAuditEvent ex2 ? ex2.ExceptionMessage : null,
                OldValue = auditLog.OldValue,
                NewValue = auditLog.NewValue,
                CompanyName = null, // Will be populated by database join
                BranchName = null, // Will be populated by database join
                EndpointPath = null,
                Severity = auditLog.Severity,
                EventCategory = auditLog.EventCategory,
                Metadata = auditLog.Metadata,
                CreationDate = auditEvent.Timestamp,
                BusinessDescription = null // Will be generated
            };

            auditLog.BusinessDescription = legacyAuditService.GenerateBusinessDescriptionAsync(
                tempAuditEntry).GetAwaiter().GetResult();
        }
        catch (Exception ex)
        {
            // Don't let legacy field population failures break audit logging
            _logger.LogWarning(ex, "Failed to populate legacy audit fields for correlation ID: {CorrelationId}", 
                auditEvent.CorrelationId);
        }

        return auditLog;
    }

    /// <summary>
    /// Re-queue events for retry when database is unavailable.
    /// Implements graceful degradation by keeping events in memory.
    /// </summary>
    private async Task RequeueEventsAsync(List<AuditEvent> batch, CancellationToken cancellationToken)
    {
        try
        {
            // Attempt to re-queue events back to the channel
            foreach (var auditEvent in batch)
            {
                // Use TryWrite to avoid blocking if channel is full
                if (!_channel.Writer.TryWrite(auditEvent))
                {
                    _logger.LogWarning(
                        "Failed to re-queue audit event. Channel is full. Event type: {EventType}, Entity: {EntityType}",
                        auditEvent.GetType().Name, auditEvent.EntityType);
                    break;
                }
            }

            _logger.LogInformation(
                "Re-queued {Count} audit events for retry after database failure",
                batch.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to re-queue audit events. Data may be lost.");
        }
    }

    /// <summary>
    /// Mask sensitive data in a data change audit event.
    /// </summary>
    private void MaskSensitiveData(DataChangeAuditEvent auditEvent, ISensitiveDataMasker dataMasker)
    {
        auditEvent.OldValue = dataMasker.MaskSensitiveFields(auditEvent.OldValue);
        auditEvent.NewValue = dataMasker.MaskSensitiveFields(auditEvent.NewValue);
    }

    /// <summary>
    /// Get the current queue depth (number of pending audit events).
    /// </summary>
    public int GetQueueDepth()
    {
        try
        {
            return _channel.Reader.Count;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to get audit queue depth");
            return 0;
        }
    }

    /// <summary>
    /// Dispose resources.
    /// </summary>
    public void Dispose()
    {
        _shutdownCts?.Dispose();
    }

    /// <summary>
    /// Test audit event used for health checks.
    /// </summary>
    private class TestAuditEvent : AuditEvent
    {
        public TestAuditEvent()
        {
            CorrelationId = "health-check";
            ActorType = "SYSTEM";
            ActorId = 0;
            Action = "HEALTH_CHECK";
            EntityType = "System";
        }
    }
}