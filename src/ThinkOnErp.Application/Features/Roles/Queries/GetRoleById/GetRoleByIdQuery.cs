using MediatR;
using ThinkOnErp.Application.DTOs.Role;

namespace ThinkOnErp.Application.Features.Roles.Queries.GetRoleById;

/// <summary>
/// Query to retrieve a specific role by its ID.
/// Returns a RoleDto or null if not found.
/// </summary>
public class GetRoleByIdQuery : IRequest<RoleDto?>
{
    /// <summary>
    /// Unique identifier of the role to retrieve
    /// </summary>
    public Int64 RoleId { get; set; }
}
