namespace ThinkOnErp.Domain.Entities;

/// <summary>
/// Represents user to role assignments (many-to-many relationship).
/// Maps to the SYS_USER_ROLE table in Oracle database.
/// </summary>
public class SysUserRole
{
    /// <summary>
    /// Primary key - generated from SEQ_SYS_USER_ROLE sequence
    /// </summary>
    public Int64 RowId { get; set; }

    /// <summary>
    /// Foreign key to SYS_USERS table
    /// </summary>
    public Int64 UserId { get; set; }

    /// <summary>
    /// Foreign key to SYS_ROLE table
    /// </summary>
    public Int64 RoleId { get; set; }

    /// <summary>
    /// User ID who assigned this role (nullable)
    /// </summary>
    public Int64? AssignedBy { get; set; }

    /// <summary>
    /// Timestamp when the role was assigned
    /// </summary>
    public DateTime? AssignedDate { get; set; }

    /// <summary>
    /// Username of the user who created this record
    /// </summary>
    public string CreationUser { get; set; } = string.Empty;

    /// <summary>
    /// Timestamp when the record was created
    /// </summary>
    public DateTime? CreationDate { get; set; }
}
