namespace ThinkOnErp.Domain.Entities;

/// <summary>
/// Represents the many-to-many relationship between users and roles
/// </summary>
public class SysUserRole
{
    /// <summary>
    /// Unique identifier for the user-role assignment
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
    /// User ID who assigned this role
    /// </summary>
    public Int64? AssignedBy { get; set; }

    /// <summary>
    /// Date when the role was assigned
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

    // Navigation properties
    public SysUser? User { get; set; }
    public SysRole? Role { get; set; }
}
