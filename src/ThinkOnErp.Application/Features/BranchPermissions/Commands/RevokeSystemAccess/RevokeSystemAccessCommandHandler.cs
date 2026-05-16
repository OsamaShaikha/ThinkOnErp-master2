using MediatR;
using ThinkOnErp.Domain.Interfaces;

namespace ThinkOnErp.Application.Features.BranchPermissions.Commands.RevokeSystemAccess;

public class RevokeSystemAccessCommandHandler : IRequestHandler<RevokeSystemAccessCommand, long>
{
    private readonly IBranchPermissionRepository _repository;

    public RevokeSystemAccessCommandHandler(IBranchPermissionRepository repository)
    {
        _repository = repository;
    }

    public async Task<long> Handle(RevokeSystemAccessCommand request, CancellationToken cancellationToken)
    {
        return await _repository.RevokeSystemAccessAsync(
            request.BranchId,
            request.SystemId,
            request.UpdateUser
        );
    }
}
