using MediatR;
using ThinkOnErp.Application.DTOs.Permissions;

namespace ThinkOnErp.Application.Features.Permissions.Queries.GetRoleScreenPermissions;

/// <summary>
/// Query to get all screen permissions for a role.
/// </summary>
public class GetRoleScreenPermissionsQuery : IRequest<List<RoleScreenPermissionDto>>
{
    /// <summary>
    /// Role ID
    /// </summary>
    public Int64 RoleId { get; set; }
}
