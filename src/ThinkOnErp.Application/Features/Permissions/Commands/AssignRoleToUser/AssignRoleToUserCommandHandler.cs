using MediatR;
using ThinkOnErp.Domain.Interfaces;

namespace ThinkOnErp.Application.Features.Permissions.Commands.AssignRoleToUser;

/// <summary>
/// Handler for AssignRoleToUserCommand.
/// Assigns a role to a user.
/// </summary>
public class AssignRoleToUserCommandHandler : IRequestHandler<AssignRoleToUserCommand, Unit>
{
    private readonly IPermissionRepository _permissionRepository;

    public AssignRoleToUserCommandHandler(IPermissionRepository permissionRepository)
    {
        _permissionRepository = permissionRepository;
    }

    public async Task<Unit> Handle(AssignRoleToUserCommand request, CancellationToken cancellationToken)
    {
        await _permissionRepository.AssignRoleToUserAsync(
            request.UserId,
            request.RoleId,
            request.AssignedBy,
            request.CreationUser
        );

        return Unit.Value;
    }
}
