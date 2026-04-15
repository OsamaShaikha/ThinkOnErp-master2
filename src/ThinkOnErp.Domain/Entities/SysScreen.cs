namespace ThinkOnErp.Domain.Entities;

/// <summary>
/// Represents a screen/page within a system in the ERP platform.
/// Maps to the SYS_SCREEN table in Oracle database.
/// </summary>
public class SysScreen
{
    /// <summary>
    /// Primary key - generated from SEQ_SYS_SCREEN sequence
    /// </summary>
    public Int64 RowId { get; set; }

    /// <summary>
    /// Foreign key to SYS_SYSTEM table
    /// </summary>
    public Int64 SystemId { get; set; }

    /// <summary>
    /// Foreign key to parent screen for hierarchical screens (nullable)
    /// </summary>
    public Int64? ParentScreenId { get; set; }

    /// <summary>
    /// Unique code identifier for the screen (e.g., 'invoices_list', 'products')
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
    /// Frontend route path for the screen
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
    /// Display order for sorting in UI
    /// </summary>
    public int DisplayOrder { get; set; }

    /// <summary>
    /// Soft delete flag - true for active, false for deleted
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
}
