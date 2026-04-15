using MediatR;
using ThinkOnErp.Domain.Interfaces;

namespace ThinkOnErp.Application.Features.Permissions.Commands.SetUserScreenPermission;

/// <summary>
/// Handler for SetUserScreenPermissionCommand.
/// Sets screen permission override for a user.
/// </summary>
public class SetUserScreenPermissionCommandHandler : IRequestHandler<SetUserScreenPermissionCommand, Unit>
{
    private readonly IPermissionRepository _permissionRepository;

    public SetUserScreenPermissionCommandHandler(IPermissionRepository permissionRepository)
    {
        _permissionRepository = permissionRepository;
    }

    public async Task<Unit> Handle(SetUserScreenPermissionCommand request, CancellationToken cancellationToken)
    {
        await _permissionRepository.SetUserScreenPermissionAsync(
            request.UserId,
            request.ScreenId,
            request.CanView,
            request.CanInsert,
            request.CanUpdate,
            request.CanDelete,
            request.AssignedBy,
            request.Notes,
            request.CreationUser
        );

        return Unit.Value;
    }
}
