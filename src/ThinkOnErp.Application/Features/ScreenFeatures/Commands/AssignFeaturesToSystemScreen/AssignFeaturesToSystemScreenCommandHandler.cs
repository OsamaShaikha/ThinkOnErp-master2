using MediatR;
using ThinkOnErp.Domain.Interfaces;

namespace ThinkOnErp.Application.Features.ScreenFeatures.Commands.AssignFeaturesToSystemScreen;

public class AssignFeaturesToSystemScreenCommandHandler : IRequestHandler<AssignFeaturesToSystemScreenCommand, bool>
{
    private readonly ISysScreenFeatureRepository _repo;

    public AssignFeaturesToSystemScreenCommandHandler(ISysScreenFeatureRepository repo)
    {
        _repo = repo;
    }

    public async Task<bool> Handle(AssignFeaturesToSystemScreenCommand request, CancellationToken cancellationToken)
    {
        await _repo.AssignFeaturesToSystemScreenAsync(request.SystemId, request.ScreenId, request.FeatureIds, request.Username);
        return true;
    }
}
