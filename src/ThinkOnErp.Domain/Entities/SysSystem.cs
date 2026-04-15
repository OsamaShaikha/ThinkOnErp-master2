namespace ThinkOnErp.Domain.Entities;

/// <summary>
/// Represents a system/module in the ERP platform (e.g., Accounting, Inventory, HR).
/// Maps to the SYS_SYSTEM table in Oracle database.
/// </summary>
public class SysSystem
{
    /// <summary>
    /// Primary key - generated from SEQ_SYS_SYSTEM sequence
    /// </summary>
    public Int64 RowId { get; set; }

    /// <summary>
    /// Unique code identifier for the system (e.g., 'accounting', 'inventory')
    /// </summary>
    public string SystemCode { get; set; } = string.Empty;

    /// <summary>
    /// Arabic name of the system
    /// </summary>
    public string SystemName { get; set; } = string.Empty;

    /// <summary>
    /// English name of the system
    /// </summary>
    public string SystemNameE { get; set; } = string.Empty;

    /// <summary>
    /// Arabic description of the system
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    /// English description of the system
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
