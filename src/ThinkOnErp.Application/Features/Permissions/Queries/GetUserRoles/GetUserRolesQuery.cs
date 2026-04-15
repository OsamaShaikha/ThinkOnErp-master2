using MediatR;
using ThinkOnErp.Application.DTOs.Permissions;

namespace ThinkOnErp.Application.Features.Permissions.Queries.GetUserRoles;

/// <summary>
/// Query to get all roles assigned to a user.
/// </summary>
public class GetUserRolesQuery : IRequest<List<UserRoleDto>>
{
    /// <summary>
    /// User ID
    /// </summary>
    public Int64 UserId { get; set; }
}
