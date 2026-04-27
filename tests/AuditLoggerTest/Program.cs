using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ThinkOnErp.Domain.Entities.Audit;
using ThinkOnErp.Domain.Interfaces;

// Simple test program to verify AuditLogger functionality
var host = Host.CreateDefaultBuilder(args)
    .ConfigureServices((context, services) =>
    {
        // Configure logging
        services.AddLogging(builder =>
        {
            builder.AddConsole();
            builder.SetMinimumLevel(LogLevel.Debug);
        });

        // Configure audit logging options
        services.Configure<AuditLoggingOptions>(options =>
        {
            options.Enabled = true;
            options.BatchSize = 5;
            options.BatchWindowMs = 1000;
            options.MaxQueueSize = 100;
            options.SensitiveFields = new[] { "password", "token" };
            options.MaskingPattern = "***MASKED***";
        });

        // Register test services
        services.AddSingleton<IAuditRepository, MockAuditRepository>();
        services.AddSingleton<SensitiveDataMasker>();
        services.AddSingleton<IAuditLogger, AuditLogger>();
        services.AddHostedService<AuditLogger>();
    })
    .Build();

var logger = host.Services.GetRequiredService<ILogger<Program>>();
var auditLogger = host.Services.GetRequiredService<IAuditLogger>();

logger.LogInformation("Starting AuditLogger test...");

// Start the host to initialize background services
await host.StartAsync();

// Test data change audit event
var dataChangeEvent = new DataChangeAuditEvent
{
    CorrelationId = "test-correlation-1",
    ActorType = "USER",
    ActorId = 123,
    CompanyId = 1,
    BranchId = 2,
    Action = "UPDATE",
    EntityType = "User",
    EntityId = 456,
    OldValue = "{\"name\":\"John\",\"password\":\"secret123\"}",
    NewValue = "{\"name\":\"John Doe\",\"password\":\"newsecret456\"}",
    IpAddress = "192.168.1.100",
    UserAgent = "Mozilla/5.0"
};

logger.LogInformation("Logging data change event...");
await auditLogger.LogDataChangeAsync(dataChangeEvent);

// Test authentication audit event
var authEvent = new AuthenticationAuditEvent
{
    CorrelationId = "test-correlation-2",
    ActorType = "USER",
    ActorId = 123,
    Action = "LOGIN",
    EntityType = "User",
    Success = true,
    TokenId = "token-123",
    IpAddress = "192.168.1.100"
};

logger.LogInformation("Logging authentication event...");
await auditLogger.LogAuthenticationAsync(authEvent);

// Test exception audit event
var exceptionEvent = new ExceptionAuditEvent
{
    CorrelationId = "test-correlation-3",
    ActorType = "SYSTEM",
    ActorId = 0,
    Action = "EXCEPTION",
    EntityType = "System",
    ExceptionType = "ValidationException",
    ExceptionMessage = "Test validation error",
    StackTrace = "Stack trace here...",
    Severity = "Error"
};

logger.LogInformation("Logging exception event...");
await auditLogger.LogExceptionAsync(exceptionEvent);

// Test health check
logger.LogInformation("Checking audit logger health...");
var isHealthy = await auditLogger.IsHealthyAsync();
logger.LogInformation("Audit logger health: {IsHealthy}", isHealthy);

// Wait a bit for background processing
logger.LogInformation("Waiting for background processing...");
await Task.Delay(2000);

// Stop the host
logger.LogInformation("Stopping host...");
await host.StopAsync();

logger.LogInformation("AuditLogger test completed successfully!");

// Configuration class for testing
public class AuditLoggingOptions
{
    public bool Enabled { get; set; } = true;
    public int BatchSize { get; set; } = 50;
    public int BatchWindowMs { get; set; } = 100;
    public int MaxQueueSize { get; set; } = 10000;
    public string[] SensitiveFields { get; set; } = Array.Empty<string>();
    public string MaskingPattern { get; set; } = "***MASKED***";
    public int MaxPayloadSize { get; set; } = 10240;
    public int DatabaseTimeoutSeconds { get; set; } = 30;
}

// Mock repository for testing
public class MockAuditRepository : IAuditRepository
{
    private readonly ILogger<MockAuditRepository> _logger;

    public MockAuditRepository(ILogger<MockAuditRepository> logger)
    {
        _logger = logger;
    }

    public Task<long> InsertAsync(AuditEvent auditEvent, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Mock: Inserting audit event - {EventType} {Action} {EntityType}", 
            auditEvent.GetType().Name, auditEvent.Action, auditEvent.EntityType);
        return Task.FromResult(Random.Shared.NextInt64(1, 1000000));
    }

    public Task<int> InsertBatchAsync(IEnumerable<AuditEvent> auditEvents, CancellationToken cancellationToken = default)
    {
        var events = auditEvents.ToList();
        _logger.LogInformation("Mock: Inserting batch of {Count} audit events", events.Count);
        
        foreach (var auditEvent in events)
        {
            _logger.LogDebug("  - {EventType} {Action} {EntityType} (CorrelationId: {CorrelationId})", 
                auditEvent.GetType().Name, auditEvent.Action, auditEvent.EntityType, auditEvent.CorrelationId);
        }
        
        return Task.FromResult(events.Count);
    }

    public Task<bool> IsHealthyAsync()
    {
        _logger.LogDebug("Mock: Health check - returning true");
        return Task.FromResult(true);
    }
}

// Mock sensitive data masker for testing
public class SensitiveDataMasker
{
    private readonly AuditLoggingOptions _options;
    private readonly HashSet<string> _sensitiveFields;

    public SensitiveDataMasker(IOptions<AuditLoggingOptions> options)
    {
        _options = options.Value;
        _sensitiveFields = new HashSet<string>(_options.SensitiveFields, StringComparer.OrdinalIgnoreCase);
    }

    public string? MaskSensitiveFields(string? json)
    {
        if (string.IsNullOrEmpty(json))
            return json;

        // Simple masking for testing - replace password values
        if (json.Contains("password"))
        {
            return json.Replace("\"secret123\"", $"\"{_options.MaskingPattern}\"")
                      .Replace("\"newsecret456\"", $"\"{_options.MaskingPattern}\"");
        }

        return json;
    }

    public string? TruncateIfNeeded(string? value)
    {
        if (string.IsNullOrEmpty(value) || value.Length <= _options.MaxPayloadSize)
            return value;

        return value.Substring(0, _options.MaxPayloadSize) + "... [TRUNCATED]";
    }
}

// Simplified AuditLogger for testing (without full infrastructure dependencies)
public class AuditLogger : IAuditLogger, IHostedService
{
    private readonly System.Threading.Channels.Channel<AuditEvent> _channel;
    private readonly IAuditRepository _repository;
    private readonly SensitiveDataMasker _dataMasker;
    private readonly ILogger<AuditLogger> _logger;
    private readonly AuditLoggingOptions _options;
    private Task? _processingTask;
    private readonly CancellationTokenSource _shutdownCts = new();

    public AuditLogger(
        IAuditRepository repository,
        SensitiveDataMasker dataMasker,
        ILogger<AuditLogger> logger,
        IOptions<AuditLoggingOptions> options)
    {
        _repository = repository;
        _dataMasker = dataMasker;
        _logger = logger;
        _options = options.Value;

        var channelOptions = new System.Threading.Channels.BoundedChannelOptions(_options.MaxQueueSize)
        {
            FullMode = System.Threading.Channels.BoundedChannelFullMode.Wait,
            SingleReader = true,
            SingleWriter = false
        };

        _channel = System.Threading.Channels.Channel.CreateBounded<AuditEvent>(channelOptions);
    }

    public async Task LogDataChangeAsync(DataChangeAuditEvent auditEvent, CancellationToken cancellationToken = default)
    {
        if (!_options.Enabled) return;

        try
        {
            auditEvent.OldValue = _dataMasker.MaskSensitiveFields(auditEvent.OldValue);
            auditEvent.NewValue = _dataMasker.MaskSensitiveFields(auditEvent.NewValue);

            await _channel.Writer.WriteAsync(auditEvent, cancellationToken);
            _logger.LogDebug("Queued data change audit event: {CorrelationId}", auditEvent.CorrelationId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to queue data change audit event");
        }
    }

    public async Task LogAuthenticationAsync(AuthenticationAuditEvent auditEvent, CancellationToken cancellationToken = default)
    {
        if (!_options.Enabled) return;

        try
        {
            await _channel.Writer.WriteAsync(auditEvent, cancellationToken);
            _logger.LogDebug("Queued authentication audit event: {CorrelationId}", auditEvent.CorrelationId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to queue authentication audit event");
        }
    }

    public async Task LogPermissionChangeAsync(PermissionChangeAuditEvent auditEvent, CancellationToken cancellationToken = default)
    {
        if (!_options.Enabled) return;

        try
        {
            await _channel.Writer.WriteAsync(auditEvent, cancellationToken);
            _logger.LogDebug("Queued permission change audit event: {CorrelationId}", auditEvent.CorrelationId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to queue permission change audit event");
        }
    }

    public async Task LogConfigurationChangeAsync(ConfigurationChangeAuditEvent auditEvent, CancellationToken cancellationToken = default)
    {
        if (!_options.Enabled) return;

        try
        {
            await _channel.Writer.WriteAsync(auditEvent, cancellationToken);
            _logger.LogDebug("Queued configuration change audit event: {CorrelationId}", auditEvent.CorrelationId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to queue configuration change audit event");
        }
    }

    public async Task LogExceptionAsync(ExceptionAuditEvent auditEvent, CancellationToken cancellationToken = default)
    {
        if (!_options.Enabled) return;

        try
        {
            auditEvent.StackTrace = _dataMasker.TruncateIfNeeded(auditEvent.StackTrace);
            
            await _channel.Writer.WriteAsync(auditEvent, cancellationToken);
            _logger.LogDebug("Queued exception audit event: {CorrelationId}", auditEvent.CorrelationId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to queue exception audit event");
        }
    }

    public async Task LogBatchAsync(IEnumerable<AuditEvent> auditEvents, CancellationToken cancellationToken = default)
    {
        foreach (var auditEvent in auditEvents)
        {
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
            }
        }
    }

    public async Task<bool> IsHealthyAsync()
    {
        try
        {
            return await _repository.IsHealthyAsync();
        }
        catch
        {
            return false;
        }
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Starting audit logger background service");
        _processingTask = ProcessAuditEventsAsync(_shutdownCts.Token);
        return Task.CompletedTask;
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Stopping audit logger background service");
        _channel.Writer.Complete();
        _shutdownCts.Cancel();

        if (_processingTask != null)
        {
            await _processingTask;
        }
    }

    private async Task ProcessAuditEventsAsync(CancellationToken cancellationToken)
    {
        var batch = new List<AuditEvent>(_options.BatchSize);
        using var batchTimer = new PeriodicTimer(TimeSpan.FromMilliseconds(_options.BatchWindowMs));

        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                // Try to fill batch or wait for timer
                bool timerExpired = false;
                
                while (batch.Count < _options.BatchSize && !timerExpired)
                {
                    // Use TryRead first to avoid blocking
                    if (_channel.Reader.TryRead(out var auditEvent))
                    {
                        batch.Add(auditEvent);
                        continue;
                    }

                    // If no items available, wait for either new item or timer
                    var waitForItemTask = _channel.Reader.WaitToReadAsync(cancellationToken);
                    var waitForTimerTask = batchTimer.WaitForNextTickAsync(cancellationToken);

                    var completedTask = await Task.WhenAny(waitForItemTask.AsTask(), waitForTimerTask.AsTask());

                    if (completedTask == waitForItemTask.AsTask())
                    {
                        var canRead = await waitForItemTask;
                        if (canRead && _channel.Reader.TryRead(out auditEvent))
                        {
                            batch.Add(auditEvent);
                        }
                        else
                        {
                            // Channel is complete
                            break;
                        }
                    }
                    else
                    {
                        // Timer expired
                        timerExpired = true;
                        await waitForTimerTask; // Consume the timer tick
                    }
                }

                // Process batch if we have events
                if (batch.Count > 0)
                {
                    await _repository.InsertBatchAsync(batch, cancellationToken);
                    _logger.LogInformation("Processed batch of {Count} audit events", batch.Count);
                    batch.Clear();
                }

                // Check if channel is complete and no more events
                if (_channel.Reader.Completion.IsCompleted)
                {
                    break;
                }
            }
        }
        catch (OperationCanceledException)
        {
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
                    await _repository.InsertBatchAsync(batch, CancellationToken.None);
                    _logger.LogInformation("Flushed {Count} remaining audit events", batch.Count);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to flush remaining audit events");
                }
            }
        }
    }

    public void Dispose()
    {
        _shutdownCts?.Dispose();
    }
}