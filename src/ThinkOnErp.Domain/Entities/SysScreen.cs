namespace ThinkOnErp.Domain.Entities;

/// <summary>
/// Represents a screen/page within a system
/// </summary>
public class SysScreen
{
    /// <summary>
    /// Unique identifier for the screen
    /// </summary>
    public Int64 RowId { get; set; }

    /// <summary>
    /// Foreign key to SYS_SYSTEM table
    /// </summary>
    public Int64 SystemId { get; set; }

    /// <summary>
    /// Parent screen ID for hierarchical screens (nullable)
    /// </summary>
    public Int64? ParentScreenId { get; set; }

    /// <summary>
    /// Unique code identifier (e.g., invoices_list)
    /// </summary>
    public string ScreenCode { get; set; } = string.Empty;

    /// <summary>
    /// Arabic name of the screen
    /// </summary>
    public string ScreenName { get; set; } = string.Empty;

    /// <summary>
    /// English name of the screen
    /// </summary>
    public string ScreenNameE { get; set; } = string.Empty;

    /// <summary>
    /// Frontend route path
    /// </summary>
    public string? Route { get; set; }

    /// <summary>
    /// Arabic description of the screen
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    /// English description of the screen
    /// </summary>
    public string? DescriptionE { get; set; }

    /// <summary>
    /// Icon identifier for UI display
    /// </summary>
    public string? Icon { get; set; }

    /// <summary>
    /// Display order for UI sorting
    /// </summary>
    public int DisplayOrder { get; set; }

    /// <summary>
    /// Indicates if the screen is active
    /// </summary>
    public bool IsActive { get; set; }

    /// <summary>
    /// Username of the user who created this record
    /// </summary>
    public string CreationUser { get; set; } = string.Empty;

    /// <summary>
    /// Timestamp when the record was created
    /// </summary>
    public DateTime? CreationDate { get; set; }

    /// <summary>
    /// Username of the user who last updated this record
    /// </summary>
    public string? UpdateUser { get; set; }

    /// <summary>
    /// Timestamp when the record was last updated
    /// </summary>
    public DateTime? UpdateDate { get; set; }

    // Navigation properties
    public SysSystem? System { get; set; }
    public SysScreen? ParentScreen { get; set; }
}
