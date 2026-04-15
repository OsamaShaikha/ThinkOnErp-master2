using MediatR;
using ThinkOnErp.Domain.Interfaces;

namespace ThinkOnErp.Application.Features.Permissions.Commands.SetRoleScreenPermission;

/// <summary>
/// Handler for SetRoleScreenPermissionCommand.
/// Sets screen permission for a role.
/// </summary>
public class SetRoleScreenPermissionCommandHandler : IRequestHandler<SetRoleScreenPermissionCommand, Unit>
{
    private readonly IPermissionRepository _permissionRepository;

    public SetRoleScreenPermissionCommandHandler(IPermissionRepository permissionRepository)
    {
        _permissionRepository = permissionRepository;
    }

    public async Task<Unit> Handle(SetRoleScreenPermissionCommand request, CancellationToken cancellationToken)
    {
        await _permissionRepository.SetRoleScreenPermissionAsync(
            request.RoleId,
            request.ScreenId,
            request.CanView,
            request.CanInsert,
            request.CanUpdate,
            request.CanDelete,
            request.CreationUser
        );

        return Unit.Value;
    }
}
