namespace ThinkOnErp.Domain.Entities;

/// <summary>
/// Represents direct user-level permission overrides (highest priority)
/// </summary>
public class SysUserScreenPermission
{
    /// <summary>
    /// Unique identifier for the permission record
    /// </summary>
    public Int64 RowId { get; set; }

    /// <summary>
    /// Foreign key to SYS_USERS table
    /// </summary>
    public Int64 UserId { get; set; }

    /// <summary>
    /// Foreign key to SYS_SCREEN table
    /// </summary>
    public Int64 ScreenId { get; set; }

    /// <summary>
    /// Can view/read the screen
    /// </summary>
    public bool CanView { get; set; }

    /// <summary>
    /// Can create new records
    /// </summary>
    public bool CanInsert { get; set; }

    /// <summary>
    /// Can edit existing records
    /// </summary>
    public bool CanUpdate { get; set; }

    /// <summary>
    /// Can delete records
    /// </summary>
    public bool CanDelete { get; set; }

    /// <summary>
    /// Super Admin or Company Admin who set this override
    /// </summary>
    public Int64? AssignedBy { get; set; }

    /// <summary>
    /// Date when the permission was assigned
    /// </summary>
    public DateTime? AssignedDate { get; set; }

    /// <summary>
    /// Notes about why this override was set
    /// </summary>
    public string? Notes { get; set; }

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
    public SysUser? User { get; set; }
    public SysScreen? Screen { get; set; }
}
