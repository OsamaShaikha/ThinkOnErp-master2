namespace ThinkOnErp.Application.DTOs.User;

/// <summary>
/// Data transfer object for user information returned from API endpoints.
/// Used for read operations (GET requests).
/// Note: Password is excluded from this DTO for security reasons.
/// </summary>
public class UserDto
{
    /// <summary>
    /// Unique identifier for the user
    /// </summary>
    public Int64 UserId { get; set; }

    /// <summary>
    /// Arabic description of the user
    /// </summary>
    public string NameAr { get; set; } = string.Empty;

    /// <summary>
    /// English description of the user
    /// </summary>
    public string NameEn { get; set; } = string.Empty;

    /// <summary>
    /// Unique username for authentication
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
    /// Timestamp of the last successful login
    /// </summary>
    public DateTime? LastLoginDate { get; set; }

    /// <summary>
    /// Indicates if the user is active
    /// </summary>
    public bool IsActive { get; set; }

    /// <summary>
    /// Administrator flag - true for admin users
    /// </summary>
    public bool IsAdmin { get; set; }

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
