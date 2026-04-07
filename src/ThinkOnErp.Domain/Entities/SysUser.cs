namespace ThinkOnErp.Domain.Entities;

/// <summary>
/// Represents a user account with authentication data in the ERP system.
/// Maps to the SYS_USERS table in Oracle database.
/// </summary>
public class SysUser
{
    /// <summary>
    /// Primary key - generated from SEQ_SYS_USERS sequence
    /// </summary>
    public Int64 RowId { get; set; }

    /// <summary>
    /// Arabic description of the user
    /// </summary>
    public string RowDesc { get; set; } = string.Empty;

    /// <summary>
    /// English description of the user
    /// </summary>
    public string RowDescE { get; set; } = string.Empty;

    /// <summary>
    /// Unique username for authentication
    /// </summary>
    public string UserName { get; set; } = string.Empty;

    /// <summary>
    /// SHA-256 hashed password stored as hexadecimal string
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
    public Int64? Role { get; set; }

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
    /// Soft delete flag - true for active, false for deleted
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

    /// <summary>
    /// Refresh token for JWT authentication
    /// </summary>
    public string? RefreshToken { get; set; }

    /// <summary>
    /// Expiration date for the refresh token
    /// </summary>
    public DateTime? RefreshTokenExpiry { get; set; }
}
