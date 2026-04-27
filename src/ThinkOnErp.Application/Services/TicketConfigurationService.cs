using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using ThinkOnErp.Domain.Interfaces;

namespace ThinkOnErp.Application.Services;

/// <summary>
/// Service for managing ticket system configuration with caching support.
/// Provides strongly-typed access to configuration values.
/// </summary>
public class TicketConfigurationService : ITicketConfigurationService
{
    private readonly ITicketConfigRepository _configRepository;
    private readonly IMemoryCache _cache;
    private readonly ILogger<TicketConfigurationService> _logger;
    private const string CacheKeyPrefix = "TicketConfig_";
    private static readonly TimeSpan CacheDuration = TimeSpan.FromMinutes(30);

    public TicketConfigurationService(
        ITicketConfigRepository configRepository,
        IMemoryCache cache,
        ILogger<TicketConfigurationService> logger)
    {
        _configRepository = configRepository ?? throw new ArgumentNullException(nameof(configRepository));
        _cache = cache ?? throw new ArgumentNullException(nameof(cache));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Gets SLA target hours for a specific priority level
    /// </summary>
    public async Task<decimal> GetSlaTargetHoursAsync(string priorityLevel)
    {
        var configKey = $"SLA.Priority.{priorityLevel}.Hours";
        var value = await GetConfigValueAsync(configKey);
        
        if (decimal.TryParse(value, out var hours))
        {
            return hours;
        }

        _logger.LogWarning("Invalid SLA target hours for priority {Priority}, using default 24 hours", priorityLevel);
        return 24; // Default fallback
    }

    /// <summary>
    /// Gets escalation threshold percentage
    /// </summary>
    public async Task<int> GetEscalationThresholdPercentageAsync()
    {
        var value = await GetConfigValueAsync("SLA.Escalation.Threshold.Percentage");
        
        if (int.TryParse(value, out var percentage))
        {
            return percentage;
        }

        return 80; // Default 80%
    }

    /// <summary>
    /// Gets maximum file attachment size in bytes
    /// </summary>
    public async Task<long> GetMaxFileAttachmentSizeAsync()
    {
        var value = await GetConfigValueAsync("FileAttachment.MaxSizeBytes");
        
        if (long.TryParse(value, out var size))
        {
            return size;
        }

        return 10485760; // Default 10MB
    }

    /// <summary>
    /// Gets maximum number of attachments per ticket
    /// </summary>
    public async Task<int> GetMaxAttachmentCountAsync()
    {
        var value = await GetConfigValueAsync("FileAttachment.MaxCount");
        
        if (int.TryParse(value, out var count))
        {
            return count;
        }

        return 5; // Default 5 attachments
    }

    /// <summary>
    /// Gets allowed file types as array
    /// </summary>
    public async Task<string[]> GetAllowedFileTypesAsync()
    {
        var value = await GetConfigValueAsync("FileAttachment.AllowedTypes");
        
        if (!string.IsNullOrEmpty(value))
        {
            return value.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        }

        return new[] { ".pdf", ".doc", ".docx", ".xls", ".xlsx", ".jpg", ".jpeg", ".png", ".txt" };
    }

    /// <summary>
    /// Checks if notifications are enabled
    /// </summary>
    public async Task<bool> AreNotificationsEnabledAsync()
    {
        var value = await GetConfigValueAsync("Notification.Enabled");
        
        if (bool.TryParse(value, out var enabled))
        {
            return enabled;
        }

        return true; // Default enabled
    }

    /// <summary>
    /// Gets notification template by key
    /// </summary>
    public async Task<string> GetNotificationTemplateAsync(string templateKey)
    {
        var configKey = $"Notification.Template.{templateKey}";
        var value = await GetConfigValueAsync(configKey);
        
        if (!string.IsNullOrEmpty(value))
        {
            return value;
        }

        _logger.LogWarning("Notification template not found: {TemplateKey}", templateKey);
        return string.Empty;
    }

    /// <summary>
    /// Gets allowed status transitions as dictionary
    /// </summary>
    public async Task<Dictionary<string, string[]>> GetAllowedStatusTransitionsAsync()
    {
        var value = await GetConfigValueAsync("Workflow.AllowedStatusTransitions");
        
        if (!string.IsNullOrEmpty(value))
        {
            try
            {
                var transitions = System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, string[]>>(value);
                if (transitions != null)
                {
                    return transitions;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to parse workflow transitions configuration");
            }
        }

        // Default workflow
        return new Dictionary<string, string[]>
        {
            { "Open", new[] { "InProgress", "Cancelled" } },
            { "InProgress", new[] { "PendingCustomer", "Resolved", "Cancelled" } },
            { "PendingCustomer", new[] { "InProgress", "Resolved", "Cancelled" } },
            { "Resolved", new[] { "Closed" } },
            { "Closed", Array.Empty<string>() },
            { "Cancelled", Array.Empty<string>() }
        };
    }

    /// <summary>
    /// Gets auto-close days for resolved tickets
    /// </summary>
    public async Task<int> GetAutoCloseResolvedAfterDaysAsync()
    {
        var value = await GetConfigValueAsync("Workflow.AutoCloseResolvedAfterDays");
        
        if (int.TryParse(value, out var days))
        {
            return days;
        }

        return 7; // Default 7 days
    }

    /// <summary>
    /// Updates a configuration value and clears cache
    /// </summary>
    public async Task<bool> UpdateConfigValueAsync(string configKey, string configValue, string updateUser)
    {
        var result = await _configRepository.UpdateByKeyAsync(configKey, configValue, updateUser);
        
        if (result)
        {
            // Clear cache for this key
            _cache.Remove($"{CacheKeyPrefix}{configKey}");
            _logger.LogInformation("Configuration updated: {ConfigKey} by {User}", configKey, updateUser);
        }

        return result;
    }

    /// <summary>
    /// Clears all configuration cache
    /// </summary>
    public void ClearCache()
    {
        _logger.LogInformation("Clearing all ticket configuration cache");
        // Note: MemoryCache doesn't have a clear all method, so we rely on expiration
        // In production, consider using a distributed cache with better cache management
    }

    /// <summary>
    /// Gets a configuration value with caching
    /// </summary>
    private async Task<string> GetConfigValueAsync(string configKey)
    {
        var cacheKey = $"{CacheKeyPrefix}{configKey}";

        if (_cache.TryGetValue(cacheKey, out string? cachedValue) && cachedValue != null)
        {
            return cachedValue;
        }

        var config = await _configRepository.GetByKeyAsync(configKey);
        
        if (config != null)
        {
            _cache.Set(cacheKey, config.ConfigValue, CacheDuration);
            return config.ConfigValue;
        }

        _logger.LogWarning("Configuration not found: {ConfigKey}", configKey);
        return string.Empty;
    }
}

/// <summary>
/// Interface for ticket configuration service
/// </summary>
public interface ITicketConfigurationService
{
    Task<decimal> GetSlaTargetHoursAsync(string priorityLevel);
    Task<int> GetEscalationThresholdPercentageAsync();
    Task<long> GetMaxFileAttachmentSizeAsync();
    Task<int> GetMaxAttachmentCountAsync();
    Task<string[]> GetAllowedFileTypesAsync();
    Task<bool> AreNotificationsEnabledAsync();
    Task<string> GetNotificationTemplateAsync(string templateKey);
    Task<Dictionary<string, string[]>> GetAllowedStatusTransitionsAsync();
    Task<int> GetAutoCloseResolvedAfterDaysAsync();
    Task<bool> UpdateConfigValueAsync(string configKey, string configValue, string updateUser);
    void ClearCache();
}
