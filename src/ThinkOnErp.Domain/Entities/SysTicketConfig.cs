namespace ThinkOnErp.Domain.Entities;

/// <summary>
/// Represents a ticket system configuration setting.
/// Stores configurable values for SLA targets, file attachment limits, notification templates, and workflow rules.
/// </summary>
public class SysTicketConfig
{
    /// <summary>
    /// Unique identifier for the configuration setting
    /// </summary>
    public Int64 RowId { get; set; }

    /// <summary>
    /// Unique key identifying the configuration setting (e.g., "SLA.Priority.High.Hours")
    /// </summary>
    public string ConfigKey { get; set; } = string.Empty;

    /// <summary>
    /// Configuration value stored as string (can be parsed to appropriate type)
    /// </summary>
    public string ConfigValue { get; set; } = string.Empty;

    /// <summary>
    /// Type of configuration: SLA, FileAttachment, Notification, Workflow, General
    /// </summary>
    public string ConfigType { get; set; } = string.Empty;

    /// <summary>
    /// Arabic description of the configuration setting
    /// </summary>
    public string? DescriptionAr { get; set; }

    /// <summary>
    /// English description of the configuration setting
    /// </summary>
    public string? DescriptionEn { get; set; }

    /// <summary>
    /// Indicates if the configuration is active
    /// </summary>
    public bool IsActive { get; set; }

    /// <summary>
    /// User who created the configuration
    /// </summary>
    public string CreationUser { get; set; } = string.Empty;

    /// <summary>
    /// Date when the configuration was created
    /// </summary>
    public DateTime? CreationDate { get; set; }

    /// <summary>
    /// User who last updated the configuration
    /// </summary>
    public string? UpdateUser { get; set; }

    /// <summary>
    /// Date when the configuration was last updated
    /// </summary>
    public DateTime? UpdateDate { get; set; }
}
