namespace ThinkOnErp.Application.DTOs.User;

/// <summary>
/// Data transfer object for creating a new user.
/// Used for POST requests to create user records.
/// </summary>
public class CreateUserDto
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
    /// Password (will be hashed using SHA-256) (required)
    /// </summary>
    public string Password { get; set; } = string.Empty;

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
