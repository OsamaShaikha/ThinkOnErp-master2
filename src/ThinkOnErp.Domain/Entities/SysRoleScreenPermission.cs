namespace ThinkOnErp.Domain.Entities;

/// <summary>
/// Represents granular screen-level permissions for a role (View/Insert/Update/Delete).
/// Maps to the SYS_ROLE_SCREEN_PERMISSION table in Oracle database.
/// </summary>
public class SysRoleScreenPermission
{
    /// <summary>
    /// Primary key - generated from SEQ_SYS_ROLE_SCREEN_PERM sequence
    /// </summary>
    public Int64 RowId { get; set; }

    /// <summary>
    /// Foreign key to SYS_ROLE table
    /// </summary>
    public Int64 RoleId { get; set; }

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
