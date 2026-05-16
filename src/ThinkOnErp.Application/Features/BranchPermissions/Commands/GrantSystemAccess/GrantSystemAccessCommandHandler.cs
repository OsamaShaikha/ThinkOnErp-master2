using MediatR;
using ThinkOnErp.Domain.Interfaces;

namespace ThinkOnErp.Application.Features.BranchPermissions.Commands.GrantSystemAccess;

public class GrantSystemAccessCommandHandler : IRequestHandler<GrantSystemAccessCommand, long>
{
    private readonly IBranchPermissionRepository _repository;

    public GrantSystemAccessCommandHandler(IBranchPermissionRepository repository)
    {
        _repository = repository;
    }

    public async Task<long> Handle(GrantSystemAccessCommand request, CancellationToken cancellationToken)
    {
        return await _repository.GrantSystemAccessAsync(
            request.BranchId,
            request.SystemId,
            request.GrantedBy,
            request.Notes,
            request.CreationUser
        );
    }
}
