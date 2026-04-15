using MediatR;
using ThinkOnErp.Domain.Interfaces;

namespace ThinkOnErp.Application.Features.Permissions.Commands.RemoveRoleFromUser;

/// <summary>
/// Handler for RemoveRoleFromUserCommand.
/// Removes a role from a user.
/// </summary>
public class RemoveRoleFromUserCommandHandler : IRequestHandler<RemoveRoleFromUserCommand, Unit>
{
    private readonly IPermissionRepository _permissionRepository;

    public RemoveRoleFromUserCommandHandler(IPermissionRepository permissionRepository)
    {
        _permissionRepository = permissionRepository;
    }

    public async Task<Unit> Handle(RemoveRoleFromUserCommand request, CancellationToken cancellationToken)
    {
        await _permissionRepository.RemoveRoleFromUserAsync(request.UserId, request.RoleId);
        return Unit.Value;
    }
}
