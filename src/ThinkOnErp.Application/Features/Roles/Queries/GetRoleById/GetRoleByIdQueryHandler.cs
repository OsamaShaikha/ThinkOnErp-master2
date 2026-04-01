using MediatR;
using ThinkOnErp.Application.DTOs.Role;
using ThinkOnErp.Domain.Interfaces;

namespace ThinkOnErp.Application.Features.Roles.Queries.GetRoleById;

/// <summary>
/// Handler for GetRoleByIdQuery.
/// Retrieves a specific role by ID and maps it to DTO.
/// </summary>
public class GetRoleByIdQueryHandler : IRequestHandler<GetRoleByIdQuery, RoleDto?>
{
    private readonly IRoleRepository _roleRepository;

    public GetRoleByIdQueryHandler(IRoleRepository roleRepository)
    {
        _roleRepository = roleRepository;
    }

    public async Task<RoleDto?> Handle(GetRoleByIdQuery request, CancellationToken cancellationToken)
    {
        var role = await _roleRepository.GetByIdAsync(request.RoleId);

        if (role == null)
            return null;

        return new RoleDto
        {
            RoleId = role.RowId,
            RoleNameAr = role.RowDesc,
            RoleNameEn = role.RowDescE,
            Note = role.Note,
            IsActive = role.IsActive,
            CreationUser = role.CreationUser,
            CreationDate = role.CreationDate,
            UpdateUser = role.UpdateUser,
            UpdateDate = role.UpdateDate
        };
    }
}
