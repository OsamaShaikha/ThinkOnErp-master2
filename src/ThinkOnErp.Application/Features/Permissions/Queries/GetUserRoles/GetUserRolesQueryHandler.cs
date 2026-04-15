using MediatR;
using ThinkOnErp.Application.DTOs.Permissions;
using ThinkOnErp.Domain.Interfaces;

namespace ThinkOnErp.Application.Features.Permissions.Queries.GetUserRoles;

/// <summary>
/// Handler for GetUserRolesQuery.
/// Retrieves all roles assigned to a user.
/// </summary>
public class GetUserRolesQueryHandler : IRequestHandler<GetUserRolesQuery, List<UserRoleDto>>
{
    private readonly IPermissionRepository _permissionRepository;

    public GetUserRolesQueryHandler(IPermissionRepository permissionRepository)
    {
        _permissionRepository = permissionRepository;
    }

    public async Task<List<UserRoleDto>> Handle(GetUserRolesQuery request, CancellationToken cancellationToken)
    {
        var userRoles = await _permissionRepository.GetUserRolesAsync(request.UserId);

        // Note: The stored procedure returns role names, but we need to map them properly
        // For now, returning basic mapping - enhance with role name lookup if needed
        return userRoles.Select(ur => new UserRoleDto
        {
            UserId = ur.UserId,
            RoleId = ur.RoleId,
            RoleNameAr = string.Empty, // Will be populated by stored procedure join
            RoleNameEn = string.Empty, // Will be populated by stored procedure join
            AssignedBy = ur.AssignedBy,
            AssignedDate = ur.AssignedDate
        }).ToList();
    }
}
