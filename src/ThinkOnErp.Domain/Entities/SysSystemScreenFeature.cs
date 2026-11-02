namespace ThinkOnErp.Domain.Entities;

/// <summary>
/// Represents the Hybrid Many-to-Many association between Features, Systems, and optionally Screens.
/// If ScreenId is null, this represents a system-wide feature.
/// </summary>
public class SysSystemScreenFeature
{
    /// <summary>
    /// Unique identifier for the association record
    /// </summary>
    public long Id { get; set; }

    /// <summary>
    /// Foreign key to SYS_SYSTEM table
    /// </summary>
    public long SystemId { get; set; }

    /// <summary>
    /// Foreign key to SYS_SCREEN table (nullable for system-level features)
    /// </summary>
    public long? ScreenId { get; set; }

    /// <summary>
    /// Foreign key to SYS_FEATURE table
    /// </summary>
    public long FeatureId { get; set; }

    /// <summary>
    /// Indicates if the feature is active in this system/screen context
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
    public SysFeature? Feature { get; set; }
}
