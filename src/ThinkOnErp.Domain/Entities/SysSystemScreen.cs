namespace ThinkOnErp.Domain.Entities;

/// <summary>
/// Represents the many-to-many association between a system and a screen.
/// Includes contextual sorting and hierarchy for the screen inside that system.
/// </summary>
public class SysSystemScreen
{
    /// <summary>
    /// Unique identifier for the association
    /// </summary>
    public long Id { get; set; }

    /// <summary>
    /// Foreign key to SYS_SYSTEM table
    /// </summary>
    public long SystemId { get; set; }

    /// <summary>
    /// Foreign key to SYS_SCREEN table
    /// </summary>
    public long ScreenId { get; set; }

    /// <summary>
    /// Parent screen ID within this system hierarchy (nullable)
    /// </summary>
    public long? ParentScreenId { get; set; }

    /// <summary>
    /// Display order for UI sorting within this system
    /// </summary>
    public int DisplayOrder { get; set; }

    /// <summary>
    /// Indicates if the screen is active within this system
    /// </summary>
    public bool IsActive { get; set; } = true;

    /// <summary>
    /// Username of the user who created this association
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
    public SysScreen? Screen { get; set; }
    public SysScreen? ParentScreen { get; set; }
}
