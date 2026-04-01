namespace ThinkOnErp.Application.DTOs.User;

/// <summary>
/// Data transfer object for updating an existing user.
/// Used for PUT requests to update user records.
/// Note: Password is not included - use ChangePasswordDto for password changes.
/// </summary>
public class UpdateUserDto
{
    /// <summary>
    /// Arabic description of the user (required)
    /// </summary>
    public string NameAr { get; set; } = string.Empty;

    /// <summary>
    /// English description of the user (required)
    /// </summary>
    public string NameEn { get; set; } = string.Empty;

    /// <summary>
    /// Unique username for authentication (required)
    /// </summary>
    public string UserName { get; set; } = string.Empty;

    /// <summary>
    /// Primary phone number
    /// </summary>
    public string? Phone { get; set; }

    /// <summary>
    /// Secondary phone number
    /// </summary>
    public string? Phone2 { get; set; }

    /// <summary>
    /// Foreign key to SYS_ROLE table
    /// </summary>
    public Int64? RoleId { get; set; }

    /// <summary>
    /// Foreign key to SYS_BRANCH table
    /// </summary>
    public Int64? BranchId { get; set; }

    /// <summary>
    /// Email address
    /// </summary>
    public string? Email { get; set; }

    /// <summary>
    /// Administrator flag - true for admin users
    /// </summary>
    public bool IsAdmin { get; set; }
}
