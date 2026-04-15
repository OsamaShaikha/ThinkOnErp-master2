namespace ThinkOnErp.Application.DTOs.Permissions;

/// <summary>
/// Data transfer object for user role assignments.
/// </summary>
public class UserRoleDto
{
    /// <summary>
    /// User ID
    /// </summary>
    public Int64 UserId { get; set; }

    /// <summary>
    /// Role ID
    /// </summary>
    public Int64 RoleId { get; set; }

    /// <summary>
    /// Arabic role name
    /// </summary>
    public string RoleNameAr { get; set; } = string.Empty;

    /// <summary>
    /// English role name
    /// </summary>
    public string RoleNameEn { get; set; } = string.Empty;

    /// <summary>
    /// User ID who assigned this role
    /// </summary>
    public Int64? AssignedBy { get; set; }

    /// <summary>
    /// Timestamp when assigned
    /// </summary>
    public DateTime? AssignedDate { get; set; }
}
