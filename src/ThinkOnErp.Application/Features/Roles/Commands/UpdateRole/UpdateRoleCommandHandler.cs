using MediatR;
using ThinkOnErp.Domain.Entities;
using ThinkOnErp.Domain.Interfaces;

namespace ThinkOnErp.Application.Features.Roles.Commands.UpdateRole;

/// <summary>
/// Handler for UpdateRoleCommand.
/// Updates an existing role in the database.
/// </summary>
public class UpdateRoleCommandHandler : IRequestHandler<UpdateRoleCommand, int>
{
    private readonly IRoleRepository _roleRepository;

    public UpdateRoleCommandHandler(IRoleRepository roleRepository)
    {
        _roleRepository = roleRepository;
    }

    public async Task<int> Handle(UpdateRoleCommand request, CancellationToken cancellationToken)
    {
        var role = new SysRole
        {
            RowId = request.RowId,
            RowDesc = request.RowDesc,
            RowDescE = request.RowDescE,
            Note = request.Note,
            UpdateUser = request.UpdateUser,
            UpdateDate = DateTime.UtcNow
        };

        var rowsAffected = await _roleRepository.UpdateAsync(role);
        return rowsAffected;
    }
}
