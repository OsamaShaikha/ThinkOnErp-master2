namespace ThinkOnErp.Application.DTOs.Permissions;

/// <summary>
/// Data transfer object for user screen permission overrides.
/// </summary>
public class UserScreenPermissionDto
{
    /// <summary>
    /// User ID
    /// </summary>
    public Int64 UserId { get; set; }

    /// <summary>
    /// Screen ID
    /// </summary>
    public Int64 ScreenId { get; set; }

    /// <summary>
    /// Screen code
    /// </summary>
    public string ScreenCode { get; set; } = string.Empty;

    /// <summary>
    /// Arabic screen name
    /// </summary>
    public string ScreenNameAr { get; set; } = string.Empty;

    /// <summary>
    /// English screen name
    /// </summary>
    public string ScreenNameEn { get; set; } = string.Empty;

    /// <summary>
    /// System ID
    /// </summary>
    public Int64 SystemId { get; set; }

    /// <summary>
    /// Permission to view/read
    /// </summary>
    public bool CanView { get; set; }

    /// <summary>
    /// Permission to create
    /// </summary>
    public bool CanInsert { get; set; }

    /// <summary>
    /// Permission to edit
    /// </summary>
    public bool CanUpdate { get; set; }

    /// <summary>
    /// Permission to delete
    /// </summary>
    public bool CanDelete { get; set; }

    /// <summary>
    /// Who assigned this override
    /// </summary>
    public Int64? AssignedBy { get; set; }

    /// <summary>
    /// Timestamp when assigned
    /// </summary>
    public DateTime? AssignedDate { get; set; }

    /// <summary>
    /// Optional notes
    /// </summary>
    public string? Notes { get; set; }
}
