using MediatR;
using ThinkOnErp.Domain.Interfaces;

namespace ThinkOnErp.Application.Features.Permissions.Commands.SetCompanySystem;

/// <summary>
/// Handler for SetCompanySystemCommand.
/// Sets system access for a company.
/// </summary>
public class SetCompanySystemCommandHandler : IRequestHandler<SetCompanySystemCommand, Unit>
{
    private readonly IPermissionRepository _permissionRepository;

    public SetCompanySystemCommandHandler(IPermissionRepository permissionRepository)
    {
        _permissionRepository = permissionRepository;
    }

    public async Task<Unit> Handle(SetCompanySystemCommand request, CancellationToken cancellationToken)
    {
        await _permissionRepository.SetCompanySystemAsync(
            request.CompanyId,
            request.SystemId,
            request.IsAllowed,
            request.GrantedBy,
            request.Notes,
            request.CreationUser
        );

        return Unit.Value;
    }
}
