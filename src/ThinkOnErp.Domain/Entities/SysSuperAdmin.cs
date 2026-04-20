namespace ThinkOnErp.Domain.Entities;

/// <summary>
/// Represents a Super Admin account with full platform access and 2FA support
/// </summary>
public class SysSuperAdmin
{
    /// <summary>
    /// Unique identifier for the super admin
    /// </summary>
    public Int64 RowId { get; set; }

    /// <summary>
    /// Arabic description/name of the super admin
    /// </summary>
    public string RowDesc { get; set; } = string.Empty;

    /// <summary>
    /// English description/name of the super admin
    /// </summary>
    public string RowDescE { get; set; } = string.Empty;

    /// <summary>
    /// Unique username for authentication
    /// </summary>
    public string UserName { get; set; } = string.Empty;

    /// <summary>
    /// Hashed password
    /// </summary>
    public string Password { get; set; } = string.Empty;

    /// <summary>
    /// Email address (unique)
    /// </summary>
    public string? Email { get; set; }

    /// <summary>
    /// Phone number
    /// </summary>
    public string? Phone { get; set; }

    /// <summary>
    /// TOTP secret for 2FA authentication
    /// </summary>
    public string? TwoFaSecret { get; set; }

    /// <summary>
    /// Indicates if 2FA is enabled (true/false)
    /// </summary>
    public bool TwoFaEnabled { get; set; }

    /// <summary>
    /// Indicates if the super admin account is active
    /// </summary>
    public bool IsActive { get; set; }

    /// <summary>
    /// Last login timestamp
    /// </summary>
    public DateTime? LastLoginDate { get; set; }

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
