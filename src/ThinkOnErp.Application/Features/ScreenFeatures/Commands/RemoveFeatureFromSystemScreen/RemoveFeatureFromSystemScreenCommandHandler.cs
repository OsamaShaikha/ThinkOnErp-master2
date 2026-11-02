using MediatR;
using ThinkOnErp.Domain.Interfaces;

namespace ThinkOnErp.Application.Features.ScreenFeatures.Commands.RemoveFeatureFromSystemScreen;

public class RemoveFeatureFromSystemScreenCommandHandler : IRequestHandler<RemoveFeatureFromSystemScreenCommand, bool>
{
    private readonly ISysScreenFeatureRepository _repo;

    public RemoveFeatureFromSystemScreenCommandHandler(ISysScreenFeatureRepository repo)
    {
        _repo = repo;
    }

    public async Task<bool> Handle(RemoveFeatureFromSystemScreenCommand request, CancellationToken cancellationToken)
    {
        await _repo.RemoveFeatureFromSystemScreenAsync(request.SystemId, request.ScreenId, request.FeatureId);
        return true;
    }
}
