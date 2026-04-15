namespace ThinkOnErp.Domain.Entities;

/// <summary>
/// Represents direct user-level permission overrides (takes precedence over role permissions).
/// Maps to the SYS_USER_SCREEN_PERMISSION table in Oracle database.
/// </summary>
public class SysUserScreenPermission
{
    /// <summary>
    /// Primary key - generated from SEQ_SYS_USER_SCREEN_PERM sequence
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
    /// Permission to view/read the screen
    /// </summary>
    public bool CanView { get; set; }

    /// <summary>
    /// Permission to create new records
    /// </summary>
    public bool CanInsert { get; set; }

    /// <summary>
    /// Permission to edit existing records
    /// </summary>
    public bool CanUpdate { get; set; }

    /// <summary>
    /// Permission to delete records
    /// </summary>
    public bool CanDelete { get; set; }

    /// <summary>
    /// Super Admin or Company Admin who set this override (nullable)
    /// </summary>
    public Int64? AssignedBy { get; set; }

    /// <summary>
    /// Timestamp when the override was assigned
    /// </summary>
    public DateTime? AssignedDate { get; set; }

    /// <summary>
    /// Optional notes about the permission override
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
}
