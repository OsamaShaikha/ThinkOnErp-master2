using MediatR;
using ThinkOnErp.Domain.Entities;
using ThinkOnErp.Domain.Interfaces;

namespace ThinkOnErp.Application.Features.Roles.Commands.UpdateRole;

/// <summary>
/// Handler for UpdateRoleCommand.
/// Updates an existing role in the database.
/// </summary>
public class UpdateRoleCommandHandler : IRequestHandler<UpdateRoleCommand, Int64>
{
    private readonly IRoleRepository _roleRepository;

    public UpdateRoleCommandHandler(IRoleRepository roleRepository)
    {
        _roleRepository = roleRepository;
    }

    public async Task<Int64> Handle(UpdateRoleCommand request, CancellationToken cancellationToken)
    {
        var role = new SysRole
        {
            RowId = request.RoleId,
            RowDesc = request.RoleNameAr,
            RowDescE = request.RoleNameEn,
            Note = request.Note,
            UpdateUser = request.UpdateUser,
            UpdateDate = DateTime.UtcNow
        };

        var rowsAffected = await _roleRepository.UpdateAsync(role);
        return rowsAffected;
    }
}
