using MediatR;
using ThinkOnErp.Domain.Interfaces;

namespace ThinkOnErp.Application.Features.BranchPermissions.Commands.GrantSystemWithScreens;

public class GrantSystemWithScreensCommandHandler : IRequestHandler<GrantSystemWithScreensCommand>
{
    private readonly IBranchPermissionRepository _repository;

    public GrantSystemWithScreensCommandHandler(IBranchPermissionRepository repository)
    {
        _repository = repository;
    }

    public async Task Handle(GrantSystemWithScreensCommand request, CancellationToken cancellationToken)
    {
        await _repository.GrantSystemWithAllScreensAsync(
            request.BranchId,
            request.SystemId,
            request.GrantedBy,
            request.CreationUser
        );
    }
}
