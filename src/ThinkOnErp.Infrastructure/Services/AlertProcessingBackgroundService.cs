using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System.Threading.Channels;
using ThinkOnErp.Domain.Interfaces;
using ThinkOnErp.Domain.Models;

namespace ThinkOnErp.Infrastructure.Services;

/// <summary>
/// Background service that processes alert notifications asynchronously.
/// Reads alerts from a shared channel queue and sends notifications through configured channels.
/// Provides graceful shutdown, error handling, and monitoring of alert processing.
/// Integrates with the AlertManager to process queued alert notifications.
/// </summary>
public class AlertProcessingBackgroundService : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<AlertProcessingBackgroundService> _logger;
    private readonly IConfiguration _configuration;
    private readonly Channel<AlertNotificationTask> _notificationQueue;

    // Configuration keys
    private const string EnabledKey = "Alerting:BackgroundProcessing:Enabled";
    private const string MaxConcurrentAlertsKey = "Alerting:BackgroundProcessing:MaxConcurrentAlerts";
    private const int DefaultMaxConcurrentAlerts = 5;

    // Statistics tracking
    private long _processedCount = 0;
    private long _failedCount = 0;
    private DateTime _serviceStartTime;

    public AlertProcessingBackgroundService(
        IServiceProvider serviceProvider,
        ILogger<AlertProcessingBackgroundService> logger,
        IConfiguration configuration,
        Channel<AlertNotificationTask> notificationQueue)
    {
        _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
        _notificationQueue = notificationQueue ?? throw new ArgumentNullException(nameof(notificationQueue));
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!IsBackgroundServiceEnabled())
        {
            _logger.LogInformation("Alert processing background service is disabled");
            return;
        }

        _serviceStartTime = DateTime.UtcNow;
        _logger.LogInformation("Alert processing background service started");

        try
        {
            var maxConcurrentAlerts = GetMaxConcurrentAlerts();
            _logger.LogInformation("Processing alerts with max concurrency: {MaxConcurrency}", maxConcurrentAlerts);

            // Process alerts with controlled concurrency
            await ProcessAlertsWithConcurrencyAsync(maxConcurrentAlerts, stoppingToken);
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("Alert processing background service is shutting down gracefully");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Fatal error in alert processing background service");
            throw;
        }
        finally
        {
            LogServiceStatistics();
            _logger.LogInformation("Alert processing background service stopped");
        }
    }

    /// <summary>
    /// Process alerts from the queue with controlled concurrency.
    /// Uses SemaphoreSlim to limit the number of concurrent alert processing tasks.
    /// </summary>
    private async Task ProcessAlertsWithConcurrencyAsync(int maxConcurrency, CancellationToken stoppingToken)
    {
        using var semaphore = new SemaphoreSlim(maxConcurrency, maxConcurrency);
        var processingTasks = new List<Task>();

        try
        {
            await foreach (var task in _notificationQueue.Reader.ReadAllAsync(stoppingToken))
            {
                // Wait for an available slot
                await semaphore.WaitAsync(stoppingToken);

                // Start processing the alert
                var processingTask = ProcessAlertNotificationAsync(task, stoppingToken)
                    .ContinueWith(t =>
                    {
                        semaphore.Release();
                        
                        if (t.IsFaulted)
                        {
                            _logger.LogError(t.Exception,
                                "Error processing alert notification: Title={Title}",
                                task.Alert.Title);
                        }
                    }, stoppingToken);

                processingTasks.Add(processingTask);

                // Clean up completed tasks periodically
                if (processingTasks.Count >= maxConcurrency * 2)
                {
                    processingTasks.RemoveAll(t => t.IsCompleted);
                }
            }

            // Wait for all remaining tasks to complete
            if (processingTasks.Any())
            {
                _logger.LogInformation("Waiting for {Count} remaining alert processing tasks to complete", 
                    processingTasks.Count(t => !t.IsCompleted));
                await Task.WhenAll(processingTasks);
            }
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("Alert processing cancelled, waiting for in-flight tasks to complete");
            
            // Give in-flight tasks a chance to complete gracefully
            var completionTask = Task.WhenAll(processingTasks.Where(t => !t.IsCompleted));
            var timeoutTask = Task.Delay(TimeSpan.FromSeconds(30));
            
            await Task.WhenAny(completionTask, timeoutTask);
            
            if (!completionTask.IsCompleted)
            {
                _logger.LogWarning("Some alert processing tasks did not complete within shutdown timeout");
            }
        }
    }

    /// <summary>
    /// Process a single alert notification task.
    /// Sends notifications through all configured channels for the alert rule.
    /// </summary>
    private async Task ProcessAlertNotificationAsync(AlertNotificationTask task, CancellationToken cancellationToken)
    {
        var startTime = DateTime.UtcNow;
        
        try
        {
            _logger.LogDebug(
                "Processing alert notification: Title={Title}, RuleId={RuleId}, QueuedAt={QueuedAt}",
                task.Alert.Title, task.Rule.Id, task.QueuedAt);

            // Calculate queue wait time
            var queueWaitTime = startTime - task.QueuedAt;
            if (queueWaitTime.TotalSeconds > 10)
            {
                _logger.LogWarning(
                    "Alert notification spent {Seconds} seconds in queue: Title={Title}",
                    queueWaitTime.TotalSeconds, task.Alert.Title);
            }

            // Create a scope to resolve scoped services
            using var scope = _serviceProvider.CreateScope();
            var alertManager = scope.ServiceProvider.GetRequiredService<IAlertManager>();

            // Parse notification channels from the rule
            var channels = task.Rule.NotificationChannels
                .Split(',', StringSplitOptions.RemoveEmptyEntries)
                .Select(c => c.Trim().ToLowerInvariant())
                .ToList();

            _logger.LogDebug(
                "Sending alert through channels: {Channels} for alert: {Title}",
                string.Join(", ", channels), task.Alert.Title);

            var notificationTasks = new List<Task>();

            // Send through each configured channel
            foreach (var channel in channels)
            {
                try
                {
                    switch (channel)
                    {
                        case "email":
                            if (!string.IsNullOrWhiteSpace(task.Rule.EmailRecipients))
                            {
                                var recipients = task.Rule.EmailRecipients
                                    .Split(',', StringSplitOptions.RemoveEmptyEntries)
                                    .Select(r => r.Trim())
                                    .ToArray();
                                notificationTasks.Add(alertManager.SendEmailAlertAsync(task.Alert, recipients));
                            }
                            break;

                        case "webhook":
                            if (!string.IsNullOrWhiteSpace(task.Rule.WebhookUrl))
                            {
                                notificationTasks.Add(alertManager.SendWebhookAlertAsync(task.Alert, task.Rule.WebhookUrl));
                            }
                            break;

                        case "sms":
                            if (!string.IsNullOrWhiteSpace(task.Rule.SmsRecipients))
                            {
                                var phoneNumbers = task.Rule.SmsRecipients
                                    .Split(',', StringSplitOptions.RemoveEmptyEntries)
                                    .Select(p => p.Trim())
                                    .ToArray();
                                notificationTasks.Add(alertManager.SendSmsAlertAsync(task.Alert, phoneNumbers));
                            }
                            break;

                        default:
                            _logger.LogWarning("Unknown notification channel: {Channel}", channel);
                            break;
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex,
                        "Error preparing notification for channel {Channel}: Title={Title}",
                        channel, task.Alert.Title);
                    // Continue with other channels
                }
            }

            // Wait for all notifications to complete
            if (notificationTasks.Any())
            {
                await Task.WhenAll(notificationTasks);
            }

            var processingTime = DateTime.UtcNow - startTime;
            
            _logger.LogInformation(
                "Successfully processed alert notification: Title={Title}, Channels={Channels}, ProcessingTime={ProcessingTimeMs}ms",
                task.Alert.Title, string.Join(", ", channels), processingTime.TotalMilliseconds);

            Interlocked.Increment(ref _processedCount);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Failed to process alert notification: Title={Title}, RuleId={RuleId}",
                task.Alert.Title, task.Rule.Id);

            Interlocked.Increment(ref _failedCount);
            
            // Don't rethrow - we want to continue processing other alerts
        }
    }

    /// <summary>
    /// Check if the background service is enabled via configuration.
    /// </summary>
    private bool IsBackgroundServiceEnabled()
    {
        return _configuration.GetValue<bool>(EnabledKey, true);
    }

    /// <summary>
    /// Get the maximum number of concurrent alert processing tasks from configuration.
    /// </summary>
    private int GetMaxConcurrentAlerts()
    {
        var maxConcurrent = _configuration.GetValue<int>(MaxConcurrentAlertsKey, DefaultMaxConcurrentAlerts);
        
        // Ensure minimum of 1 and maximum of 20
        if (maxConcurrent < 1)
        {
            _logger.LogWarning("Configured max concurrent alerts {Value} is too low, using minimum of 1", maxConcurrent);
            return 1;
        }

        if (maxConcurrent > 20)
        {
            _logger.LogWarning("Configured max concurrent alerts {Value} is too high, using maximum of 20", maxConcurrent);
            return 20;
        }

        return maxConcurrent;
    }

    /// <summary>
    /// Log service statistics on shutdown.
    /// </summary>
    private void LogServiceStatistics()
    {
        var uptime = DateTime.UtcNow - _serviceStartTime;
        
        _logger.LogInformation(
            "Alert processing service statistics: Uptime={Uptime}, Processed={Processed}, Failed={Failed}, SuccessRate={SuccessRate:P2}",
            uptime,
            _processedCount,
            _failedCount,
            _processedCount > 0 ? (double)(_processedCount - _failedCount) / _processedCount : 0);
    }

    public override Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Alert processing background service is stopping");
        return base.StopAsync(cancellationToken);
    }
}

/// <summary>
/// Represents a queued alert notification task.
/// Contains the alert details and the rule that triggered it.
/// </summary>
public class AlertNotificationTask
{
    /// <summary>
    /// The alert to be sent.
    /// </summary>
    public Alert Alert { get; set; } = null!;

    /// <summary>
    /// The alert rule that triggered this notification.
    /// </summary>
    public AlertRule Rule { get; set; } = null!;

    /// <summary>
    /// The timestamp when this task was queued.
    /// Used to track queue wait times.
    /// </summary>
    public DateTime QueuedAt { get; set; }
}
