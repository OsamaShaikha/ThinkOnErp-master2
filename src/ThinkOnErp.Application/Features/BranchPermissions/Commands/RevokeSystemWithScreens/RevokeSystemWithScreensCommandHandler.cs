using MediatR;
using ThinkOnErp.Domain.Interfaces;

namespace ThinkOnErp.Application.Features.BranchPermissions.Commands.RevokeSystemWithScreens;

public class RevokeSystemWithScreensCommandHandler : IRequestHandler<RevokeSystemWithScreensCommand>
{
    private readonly IBranchPermissionRepository _repository;

    public RevokeSystemWithScreensCommandHandler(IBranchPermissionRepository repository)
    {
        _repository = repository;
    }

    public async Task Handle(RevokeSystemWithScreensCommand request, CancellationToken cancellationToken)
    {
        await _repository.RevokeSystemWithAllScreensAsync(
            request.BranchId,
            request.SystemId,
            request.UpdateUser
        );
    }
}
