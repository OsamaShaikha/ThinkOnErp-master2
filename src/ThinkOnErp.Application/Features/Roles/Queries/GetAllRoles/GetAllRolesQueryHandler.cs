using MediatR;
using ThinkOnErp.Application.DTOs.Role;
using ThinkOnErp.Domain.Interfaces;

namespace ThinkOnErp.Application.Features.Roles.Queries.GetAllRoles;

/// <summary>
/// Handler for GetAllRolesQuery.
/// Retrieves all active roles and maps them to DTOs.
/// </summary>
public class GetAllRolesQueryHandler : IRequestHandler<GetAllRolesQuery, List<RoleDto>>
{
    private readonly IRoleRepository _roleRepository;

    public GetAllRolesQueryHandler(IRoleRepository roleRepository)
    {
        _roleRepository = roleRepository;
    }

    public async Task<List<RoleDto>> Handle(GetAllRolesQuery request, CancellationToken cancellationToken)
    {
        var roles = await _roleRepository.GetAllAsync();

        var roleDtos = roles.Select(role => new RoleDto
        {
            RowId = role.RowId,
            RowDesc = role.RowDesc,
            RowDescE = role.RowDescE,
            Note = role.Note,
            IsActive = role.IsActive,
            CreationUser = role.CreationUser,
            CreationDate = role.CreationDate,
            UpdateUser = role.UpdateUser,
            UpdateDate = role.UpdateDate
        }).ToList();

        return roleDtos;
    }
}
