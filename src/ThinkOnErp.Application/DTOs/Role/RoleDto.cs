namespace ThinkOnErp.Application.DTOs.Role;

/// <summary>
/// Data transfer object for role information returned from API endpoints.
/// Used for read operations (GET requests).
/// </summary>
public class RoleDto
{
    /// <summary>
    /// Unique identifier for the role
    /// </summary>
    public Int64 RoleId { get; set; }

    /// <summary>
    /// Arabic description of the role
    /// </summary>
    public string RoleNameAr { get; set; } = string.Empty;

    /// <summary>
    /// English description of the role
    /// </summary>
    public string RoleNameEn { get; set; } = string.Empty;

    /// <summary>
    /// Optional notes about the role
    /// </summary>
    public string? Note { get; set; }

    /// <summary>
    /// Indicates if the role is active
    /// </summary>
    public bool IsActive { get; set; }

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
