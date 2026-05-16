using MediatR;
using ThinkOnErp.Domain.Interfaces;

namespace ThinkOnErp.Application.Features.BranchPermissions.Commands.GrantScreenAccess;

public class GrantScreenAccessCommandHandler : IRequestHandler<GrantScreenAccessCommand, long>
{
    private readonly IBranchPermissionRepository _repository;

    public GrantScreenAccessCommandHandler(IBranchPermissionRepository repository)
    {
        _repository = repository;
    }

    public async Task<long> Handle(GrantScreenAccessCommand request, CancellationToken cancellationToken)
    {
        return await _repository.GrantScreenAccessAsync(
            request.BranchId,
            request.ScreenId,
            request.GrantedBy,
            request.Notes,
            request.CreationUser
        );
    }
}
