using MediatR;
using ThinkOnErp.Application.DTOs.Role;

namespace ThinkOnErp.Application.Features.Roles.Queries.GetAllRoles;

/// <summary>
/// Query to retrieve all active roles from the system.
/// Returns a list of RoleDto objects.
/// </summary>
public class GetAllRolesQuery : IRequest<List<RoleDto>>
{
}
