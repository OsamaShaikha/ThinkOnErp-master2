using MediatR;
using ThinkOnErp.Domain.Interfaces;

namespace ThinkOnErp.Application.Features.ScreenFeatures.Commands.RemoveFeatureFromScreen;

public class RemoveFeatureFromScreenCommandHandler : IRequestHandler<RemoveFeatureFromScreenCommand, bool>
{
    private readonly ISysScreenFeatureRepository _repo;

    public RemoveFeatureFromScreenCommandHandler(ISysScreenFeatureRepository repo)
    {
        _repo = repo;
    }

    public async Task<bool> Handle(RemoveFeatureFromScreenCommand request, CancellationToken cancellationToken)
    {
        await _repo.RemoveFeatureFromScreenAsync(request.ScreenId, request.FeatureId);
        return true;
    }
}
