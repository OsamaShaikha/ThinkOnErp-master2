using MediatR;

namespace ThinkOnErp.Application.Features.Roles.Commands.CreateRole;

/// <summary>
/// Command to create a new role in the system.
/// Returns the newly created role's ID.
/// </summary>
public class CreateRoleCommand : IRequest<Int64>
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

    /// <summary>
    /// Username of the user creating this role
    /// </summary>
    public string CreationUser { get; set; } = string.Empty;
}
