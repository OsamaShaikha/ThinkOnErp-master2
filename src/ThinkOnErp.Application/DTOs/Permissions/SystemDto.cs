namespace ThinkOnErp.Application.DTOs.Permissions;

/// <summary>
/// Data transfer object for system/module information.
/// </summary>
public class SystemDto
{
    /// <summary>
    /// Unique identifier for the system
    /// </summary>
    public Int64 SystemId { get; set; }

    /// <summary>
    /// Unique code identifier (e.g., 'accounting', 'inventory')
    /// </summary>
    public string SystemCode { get; set; } = string.Empty;

    /// <summary>
    /// Arabic name of the system
    /// </summary>
    public string SystemNameAr { get; set; } = string.Empty;

    /// <summary>
    /// English name of the system
    /// </summary>
    public string SystemNameEn { get; set; } = string.Empty;

    /// <summary>
    /// Arabic description
    /// </summary>
    public string? DescriptionAr { get; set; }

    /// <summary>
    /// English description
    /// </summary>
    public string? DescriptionEn { get; set; }

    /// <summary>
    /// Icon identifier
    /// </summary>
    public string? Icon { get; set; }

    /// <summary>
    /// Display order
    /// </summary>
    public int DisplayOrder { get; set; }

    /// <summary>
    /// Indicates if the system is active
    /// </summary>
    public bool IsActive { get; set; }
}
