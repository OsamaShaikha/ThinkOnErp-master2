using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ThinkOnErp.Domain.Interfaces;
using ThinkOnErp.Domain.Models;
using ThinkOnErp.Infrastructure.Configuration;

namespace ThinkOnErp.Infrastructure.Services;

/// <summary>
/// Background service that monitors database connection pool utilization and triggers alerts
/// when thresholds are exceeded (80% warning, 95% critical).
/// Runs periodic checks based on configured intervals to prevent connection pool exhaustion.
/// </summary>
public class ConnectionPoolMonitoringService : BackgroundService
{
    private readonly ILogger<ConnectionPoolMonitoringService> _logger;
    private readonly IServiceScopeFactory _serviceScopeFactory;
    private readonly AlertingOptions _alertingOptions;
    private DateTime _lastWarningAlert = DateTime.MinValue;
    private DateTime _lastCriticalAlert = DateTime.MinValue;

    public ConnectionPoolMonitoringService(
        ILogger<ConnectionPoolMonitoringService> logger,
        IServiceScopeFactory serviceScopeFactory,
        IOptions<AlertingOptions> alertingOptions)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _serviceScopeFactory = serviceScopeFactory ?? throw new ArgumentNullException(nameof(serviceScopeFactory));
        _alertingOptions = alertingOptions?.Value ?? throw new ArgumentNullException(nameof(alertingOptions));
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Check if alerting is enabled
        if (!_alertingOptions.Enabled)
        {
            _logger.LogInformation("Connection pool monitoring service is disabled (Alerting.Enabled = false)");
            return;
        }

        // Get alert rule configurations
        var warningRule = GetAlertRuleConfig("ConnectionPoolWarning");
        var criticalRule = GetAlertRuleConfig("ConnectionPoolCritical");

        if (!warningRule.Enabled && !criticalRule.Enabled)
        {
            _logger.LogInformation(
                "Connection pool monitoring service is disabled (both ConnectionPoolWarning and ConnectionPoolCritical rules are disabled)");
            return;
        }

        _logger.LogInformation("Connection pool monitoring service started");

        // Determine check interval (use the minimum of the two configured intervals)
        var checkInterval = TimeSpan.FromMinutes(Math.Min(
            warningRule.CheckIntervalMinutes,
            criticalRule.CheckIntervalMinutes));

        _logger.LogInformation(
            "Connection pool monitoring will check every {Interval} minutes. Warning threshold: {WarningThreshold}%, Critical threshold: {CriticalThreshold}%",
            checkInterval.TotalMinutes,
            warningRule.ThresholdPercentage,
            criticalRule.ThresholdPercentage);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await CheckConnectionPoolUtilizationAsync(warningRule, criticalRule, stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking connection pool utilization");
            }

            // Wait for the next check interval
            await Task.Delay(checkInterval, stoppingToken);
        }

        _logger.LogInformation("Connection pool monitoring service stopped");
    }

    private async Task CheckConnectionPoolUtilizationAsync(
        ConnectionPoolAlertRuleConfig warningRule,
        ConnectionPoolAlertRuleConfig criticalRule,
        CancellationToken cancellationToken)
    {
        try
        {
            // Create a scope to access scoped services
            using var scope = _serviceScopeFactory.CreateScope();
            var performanceMonitor = scope.ServiceProvider.GetService<IPerformanceMonitor>();
            var alertManager = scope.ServiceProvider.GetService<IAlertManager>();

            if (performanceMonitor == null)
            {
                _logger.LogWarning("IPerformanceMonitor service not available");
                return;
            }

            if (alertManager == null)
            {
                _logger.LogWarning("IAlertManager service not available");
                return;
            }

            // Get current connection pool metrics
            var metrics = await performanceMonitor.GetConnectionPoolMetricsAsync();

            _logger.LogDebug(
                "Connection pool utilization: {Utilization}% ({Active} active, {Idle} idle, {Total} total, {Max} max)",
                metrics.UtilizationPercent,
                metrics.ActiveConnections,
                metrics.IdleConnections,
                metrics.TotalConnections,
                metrics.MaxPoolSize);

            // Check critical threshold first (95%)
            if (criticalRule.Enabled && metrics.UtilizationPercent >= criticalRule.ThresholdPercentage)
            {
                await TriggerCriticalAlertAsync(metrics, criticalRule, alertManager, cancellationToken);
            }
            // Check warning threshold (80%)
            else if (warningRule.Enabled && metrics.UtilizationPercent >= warningRule.ThresholdPercentage)
            {
                await TriggerWarningAlertAsync(metrics, warningRule, alertManager, cancellationToken);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in CheckConnectionPoolUtilizationAsync");
        }
    }

    private async Task TriggerWarningAlertAsync(
        ConnectionPoolMetrics metrics,
        ConnectionPoolAlertRuleConfig rule,
        IAlertManager alertManager,
        CancellationToken cancellationToken)
    {
        // Apply rate limiting
        var timeSinceLastAlert = DateTime.UtcNow - _lastWarningAlert;
        var minTimeBetweenAlerts = TimeSpan.FromHours(1.0 / rule.RateLimitPerHour);

        if (timeSinceLastAlert < minTimeBetweenAlerts)
        {
            _logger.LogDebug(
                "Skipping warning alert due to rate limiting. Time since last alert: {TimeSinceLastAlert}",
                timeSinceLastAlert);
            return;
        }

        _logger.LogWarning(
            "Connection pool utilization warning: {Utilization}% (threshold: {Threshold}%). Active: {Active}, Idle: {Idle}, Total: {Total}, Max: {Max}",
            metrics.UtilizationPercent,
            rule.ThresholdPercentage,
            metrics.ActiveConnections,
            metrics.IdleConnections,
            metrics.TotalConnections,
            metrics.MaxPoolSize);

        var alert = new Alert
        {
            AlertType = "ConnectionPoolWarning",
            Severity = rule.Severity,
            Title = "Database Connection Pool Warning",
            Description = $"Connection pool utilization has reached {metrics.UtilizationPercent:F1}% (threshold: {rule.ThresholdPercentage}%). " +
                      $"Active connections: {metrics.ActiveConnections}, Idle: {metrics.IdleConnections}, Total: {metrics.TotalConnections}, Max: {metrics.MaxPoolSize}. " +
                      $"Available connections: {metrics.AvailableConnections}. {rule.Description}",
            TriggeredAt = DateTime.UtcNow,
            Metadata = System.Text.Json.JsonSerializer.Serialize(new Dictionary<string, object>
            {
                { "UtilizationPercent", metrics.UtilizationPercent },
                { "ActiveConnections", metrics.ActiveConnections },
                { "IdleConnections", metrics.IdleConnections },
                { "TotalConnections", metrics.TotalConnections },
                { "MaxPoolSize", metrics.MaxPoolSize },
                { "AvailableConnections", metrics.AvailableConnections },
                { "Threshold", rule.ThresholdPercentage },
                { "Recommendations", metrics.Recommendations },
                { "Source", "ConnectionPoolMonitoringService" }
            })
        };

        try
        {
            await alertManager.TriggerAlertAsync(alert);
            _lastWarningAlert = DateTime.UtcNow;

            _logger.LogInformation(
                "Connection pool warning alert triggered successfully. Utilization: {Utilization}%",
                metrics.UtilizationPercent);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to trigger connection pool warning alert");
        }
    }

    private async Task TriggerCriticalAlertAsync(
        ConnectionPoolMetrics metrics,
        ConnectionPoolAlertRuleConfig rule,
        IAlertManager alertManager,
        CancellationToken cancellationToken)
    {
        // Apply rate limiting
        var timeSinceLastAlert = DateTime.UtcNow - _lastCriticalAlert;
        var minTimeBetweenAlerts = TimeSpan.FromHours(1.0 / rule.RateLimitPerHour);

        if (timeSinceLastAlert < minTimeBetweenAlerts)
        {
            _logger.LogDebug(
                "Skipping critical alert due to rate limiting. Time since last alert: {TimeSinceLastAlert}",
                timeSinceLastAlert);
            return;
        }

        _logger.LogError(
            "Connection pool utilization CRITICAL: {Utilization}% (threshold: {Threshold}%). Active: {Active}, Idle: {Idle}, Total: {Total}, Max: {Max}. IMMEDIATE ACTION REQUIRED!",
            metrics.UtilizationPercent,
            rule.ThresholdPercentage,
            metrics.ActiveConnections,
            metrics.IdleConnections,
            metrics.TotalConnections,
            metrics.MaxPoolSize);

        var alert = new Alert
        {
            AlertType = "ConnectionPoolCritical",
            Severity = rule.Severity,
            Title = "CRITICAL: Database Connection Pool Near Exhaustion",
            Description = $"CRITICAL: Connection pool utilization has reached {metrics.UtilizationPercent:F1}% (threshold: {rule.ThresholdPercentage}%). " +
                      $"Active connections: {metrics.ActiveConnections}, Idle: {metrics.IdleConnections}, Total: {metrics.TotalConnections}, Max: {metrics.MaxPoolSize}. " +
                      $"Only {metrics.AvailableConnections} connections available. IMMEDIATE ACTION REQUIRED to prevent application failures! {rule.Description}",
            TriggeredAt = DateTime.UtcNow,
            Metadata = System.Text.Json.JsonSerializer.Serialize(new Dictionary<string, object>
            {
                { "UtilizationPercent", metrics.UtilizationPercent },
                { "ActiveConnections", metrics.ActiveConnections },
                { "IdleConnections", metrics.IdleConnections },
                { "TotalConnections", metrics.TotalConnections },
                { "MaxPoolSize", metrics.MaxPoolSize },
                { "AvailableConnections", metrics.AvailableConnections },
                { "Threshold", rule.ThresholdPercentage },
                { "IsExhausted", metrics.IsExhausted },
                { "Source", "ConnectionPoolMonitoringService" },
                { "Recommendations", metrics.Recommendations }
            })
        };

        try
        {
            await alertManager.TriggerAlertAsync(alert);
            _lastCriticalAlert = DateTime.UtcNow;

            _logger.LogInformation(
                "Connection pool critical alert triggered successfully. Utilization: {Utilization}%",
                metrics.UtilizationPercent);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to trigger connection pool critical alert");
        }
    }

    private ConnectionPoolAlertRuleConfig GetAlertRuleConfig(string ruleName)
    {
        // Default configuration
        var config = new ConnectionPoolAlertRuleConfig
        {
            Enabled = false,
            Severity = "Medium",
            ThresholdPercentage = ruleName == "ConnectionPoolWarning" ? 80 : 95,
            Description = "",
            CheckIntervalMinutes = ruleName == "ConnectionPoolWarning" ? 5 : 2,
            RateLimitPerHour = ruleName == "ConnectionPoolWarning" ? 6 : 10
        };

        // Try to get configuration from AlertRules section
        try
        {
            var alertRulesSection = _alertingOptions.GetType().GetProperty("AlertRules")?.GetValue(_alertingOptions);
            if (alertRulesSection != null)
            {
                var ruleProperty = alertRulesSection.GetType().GetProperty(ruleName);
                if (ruleProperty != null)
                {
                    var ruleValue = ruleProperty.GetValue(alertRulesSection);
                    if (ruleValue != null)
                    {
                        // Extract properties using reflection
                        var enabledProp = ruleValue.GetType().GetProperty("Enabled");
                        var severityProp = ruleValue.GetType().GetProperty("Severity");
                        var thresholdProp = ruleValue.GetType().GetProperty("ThresholdPercentage");
                        var descriptionProp = ruleValue.GetType().GetProperty("Description");
                        var checkIntervalProp = ruleValue.GetType().GetProperty("CheckIntervalMinutes");
                        var rateLimitProp = ruleValue.GetType().GetProperty("RateLimitPerHour");

                        if (enabledProp != null)
                            config.Enabled = (bool)(enabledProp.GetValue(ruleValue) ?? false);
                        if (severityProp != null)
                            config.Severity = severityProp.GetValue(ruleValue)?.ToString() ?? config.Severity;
                        if (thresholdProp != null)
                            config.ThresholdPercentage = (int)(thresholdProp.GetValue(ruleValue) ?? config.ThresholdPercentage);
                        if (descriptionProp != null)
                            config.Description = descriptionProp.GetValue(ruleValue)?.ToString() ?? config.Description;
                        if (checkIntervalProp != null)
                            config.CheckIntervalMinutes = (int)(checkIntervalProp.GetValue(ruleValue) ?? config.CheckIntervalMinutes);
                        if (rateLimitProp != null)
                            config.RateLimitPerHour = (int)(rateLimitProp.GetValue(ruleValue) ?? config.RateLimitPerHour);
                    }
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to load alert rule configuration for {RuleName}. Using defaults.", ruleName);
        }

        return config;
    }

    private class ConnectionPoolAlertRuleConfig
    {
        public bool Enabled { get; set; }
        public string Severity { get; set; } = "Medium";
        public int ThresholdPercentage { get; set; }
        public string Description { get; set; } = "";
        public int CheckIntervalMinutes { get; set; }
        public int RateLimitPerHour { get; set; }
    }
}
