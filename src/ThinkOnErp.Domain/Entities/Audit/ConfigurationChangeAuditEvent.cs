namespace ThinkOnErp.Domain.Entities.Audit;

/// <summary>
/// Audit event for configuration changes.
/// Tracks changes to application settings, feature flags, and system configuration.
/// </summary>
public class ConfigurationChangeAuditEvent : AuditEvent
{
    /// <summary>
    /// Name of the configuration setting that was changed
    /// </summary>
    public string SettingName { get; set; } = string.Empty;

    /// <summary>
    /// Value before the change
    /// </summary>
    public string? OldValue { get; set; }

    /// <summary>
    /// Value after the change
    /// </summary>
    public string? NewValue { get; set; }

    /// <summary>
    /// Source of the configuration: EnvironmentVariable, ConfigFile, Database
    /// </summary>
    public string Source { get; set; } = string.Empty;
}
