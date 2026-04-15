using MediatR;
using ThinkOnErp.Application.DTOs.Permissions;
using ThinkOnErp.Domain.Interfaces;

namespace ThinkOnErp.Application.Features.Permissions.Queries.GetRoleScreenPermissions;

/// <summary>
/// Handler for GetRoleScreenPermissionsQuery.
/// Retrieves all screen permissions for a role.
/// </summary>
public class GetRoleScreenPermissionsQueryHandler : IRequestHandler<GetRoleScreenPermissionsQuery, List<RoleScreenPermissionDto>>
{
    private readonly IPermissionRepository _permissionRepository;

    public GetRoleScreenPermissionsQueryHandler(IPermissionRepository permissionRepository)
    {
        _permissionRepository = permissionRepository;
    }

    public async Task<List<RoleScreenPermissionDto>> Handle(GetRoleScreenPermissionsQuery request, CancellationToken cancellationToken)
    {
        var permissions = await _permissionRepository.GetRoleScreenPermissionsAsync(request.RoleId);

        // Note: The stored procedure returns screen details via join
        return permissions.Select(p => new RoleScreenPermissionDto
        {
            RoleId = p.RoleId,
            ScreenId = p.ScreenId,
            ScreenCode = string.Empty, // Populated by stored procedure
            ScreenNameAr = string.Empty, // Populated by stored procedure
            ScreenNameEn = string.Empty, // Populated by stored procedure
            SystemId = 0, // Populated by stored procedure
            CanView = p.CanView,
            CanInsert = p.CanInsert,
            CanUpdate = p.CanUpdate,
            CanDelete = p.CanDelete
        }).ToList();
    }
}
