using MediatR;
using ThinkOnErp.Domain.Interfaces;

namespace ThinkOnErp.Application.Features.Permissions.Commands.GrantSystemToCompany;

public class GrantSystemToCompanyCommandHandler : IRequestHandler<GrantSystemToCompanyCommand, Unit>
{
    private readonly IPermissionRepository _permissionRepository;

    public GrantSystemToCompanyCommandHandler(IPermissionRepository permissionRepository)
    {
        _permissionRepository = permissionRepository;
    }

    public async Task<Unit> Handle(GrantSystemToCompanyCommand request, CancellationToken cancellationToken)
    {
        await _permissionRepository.SetCompanySystemAsync(
            request.CompanyId,
            request.SystemId,
            isAllowed: true,
            request.GrantedBy,
            request.Notes,
            request.CreationUser
        );

        await _permissionRepository.GrantSystemScreensToCompanyAsync(
            request.CompanyId,
            request.SystemId,
            request.GrantedBy,
            request.CreationUser
        );

        return Unit.Value;
    }
}
