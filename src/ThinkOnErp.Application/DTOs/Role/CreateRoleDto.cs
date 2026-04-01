namespace ThinkOnErp.Application.DTOs.Role;

/// <summary>
/// Data transfer object for creating a new role.
/// Used for POST requests to create role records.
/// </summary>
public class CreateRoleDto
{
    /// <summary>
    /// Arabic description of the role (required)
    /// </summary>
    public string RoleNameAr { get; set; } = string.Empty;

    /// <summary>
    /// English description of the role (required)
    /// </summary>
    public string RoleNameEn { get; set; } = string.Empty;

    /// <summary>
    /// Optional notes about the role
    /// </summary>
    public string? Note { get; set; }
}
