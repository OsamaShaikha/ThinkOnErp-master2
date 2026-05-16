using MediatR;
using ThinkOnErp.Domain.Interfaces;

namespace ThinkOnErp.Application.Features.BranchPermissions.Commands.RevokeScreenAccess;

public class RevokeScreenAccessCommandHandler : IRequestHandler<RevokeScreenAccessCommand, long>
{
    private readonly IBranchPermissionRepository _repository;

    public RevokeScreenAccessCommandHandler(IBranchPermissionRepository repository)
    {
        _repository = repository;
    }

    public async Task<long> Handle(RevokeScreenAccessCommand request, CancellationToken cancellationToken)
    {
        return await _repository.RevokeScreenAccessAsync(
            request.BranchId,
            request.ScreenId,
            request.UpdateUser
        );
    }
}
