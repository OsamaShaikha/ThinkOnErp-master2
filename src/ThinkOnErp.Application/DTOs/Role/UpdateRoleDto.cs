namespace ThinkOnErp.Application.DTOs.Role;

/// <summary>
/// Data transfer object for updating an existing role.
/// Used for PUT requests to update role records.
/// </summary>
public class UpdateRoleDto
{
    /// <summary>
    /// Arabic description of the role (required)
    /// </summary>
    public string RowDesc { get; set; } = string.Empty;

    /// <summary>
    /// English description of the role (required)
    /// </summary>
    public string RowDescE { get; set; } = string.Empty;

    /// <summary>
    /// Optional notes about the role
    /// </summary>
    public string? Note { get; set; }
}
