using MediatR;
using ThinkOnErp.Domain.Interfaces;

namespace ThinkOnErp.Application.Features.Permissions.Commands.GrantSystemToBranch;

public class GrantSystemToBranchCommandHandler : IRequestHandler<GrantSystemToBranchCommand, Unit>
{
    private readonly IPermissionRepository _permissionRepository;

    public GrantSystemToBranchCommandHandler(IPermissionRepository permissionRepository)
    {
        _permissionRepository = permissionRepository;
    }

    public async Task<Unit> Handle(GrantSystemToBranchCommand request, CancellationToken cancellationToken)
    {
        await _permissionRepository.SetBranchSystemAsync(
            request.BranchId,
            request.SystemId,
            isAllowed: true,
            request.GrantedBy,
            request.Notes,
            request.CreationUser
        );

        await _permissionRepository.GrantSystemScreensToBranchAsync(
            request.BranchId,
            request.SystemId,
            request.GrantedBy,
            request.CreationUser
        );

        return Unit.Value;
    }
}
