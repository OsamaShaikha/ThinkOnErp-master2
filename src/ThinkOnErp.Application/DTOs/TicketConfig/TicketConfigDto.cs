namespace ThinkOnErp.Application.DTOs.TicketConfig;

/// <summary>
/// DTO for ticket configuration settings
/// </summary>
public class TicketConfigDto
{
    public Int64 RowId { get; set; }
    public string ConfigKey { get; set; } = string.Empty;
    public string ConfigValue { get; set; } = string.Empty;
    public string ConfigType { get; set; } = string.Empty;
    public string? DescriptionAr { get; set; }
    public string? DescriptionEn { get; set; }
    public bool IsActive { get; set; }
    public string CreationUser { get; set; } = string.Empty;
    public DateTime? CreationDate { get; set; }
    public string? UpdateUser { get; set; }
    public DateTime? UpdateDate { get; set; }
}

/// <summary>
/// DTO for creating a new ticket configuration
/// </summary>
public class CreateTicketConfigDto
{
    public string ConfigKey { get; set; } = string.Empty;
    public string ConfigValue { get; set; } = string.Empty;
    public string ConfigType { get; set; } = string.Empty;
    public string? DescriptionAr { get; set; }
    public string? DescriptionEn { get; set; }
}

/// <summary>
/// DTO for updating a ticket configuration
/// </summary>
public class UpdateTicketConfigDto
{
    public string ConfigValue { get; set; } = string.Empty;
    public string? DescriptionAr { get; set; }
    public string? DescriptionEn { get; set; }
}

/// <summary>
/// DTO for SLA configuration settings
/// </summary>
public class SlaConfigDto
{
    public decimal LowPriorityHours { get; set; }
    public decimal MediumPriorityHours { get; set; }
    public decimal HighPriorityHours { get; set; }
    public decimal CriticalPriorityHours { get; set; }
    public int EscalationThresholdPercentage { get; set; }
}

/// <summary>
/// DTO for file attachment configuration settings
/// </summary>
public class FileAttachmentConfigDto
{
    public long MaxSizeBytes { get; set; }
    public int MaxCount { get; set; }
    public string[] AllowedTypes { get; set; } = Array.Empty<string>();
}

/// <summary>
/// DTO for notification configuration settings
/// </summary>
public class NotificationConfigDto
{
    public bool Enabled { get; set; }
    public Dictionary<string, string> Templates { get; set; } = new();
}

/// <summary>
/// DTO for workflow configuration settings
/// </summary>
public class WorkflowConfigDto
{
    public Dictionary<string, string[]> AllowedStatusTransitions { get; set; } = new();
    public int AutoCloseResolvedAfterDays { get; set; }
}
