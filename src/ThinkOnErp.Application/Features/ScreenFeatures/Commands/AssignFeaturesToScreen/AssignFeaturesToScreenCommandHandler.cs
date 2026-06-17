using MediatR;
using ThinkOnErp.Domain.Interfaces;

namespace ThinkOnErp.Application.Features.ScreenFeatures.Commands.AssignFeaturesToScreen;

public class AssignFeaturesToScreenCommandHandler : IRequestHandler<AssignFeaturesToScreenCommand, bool>
{
    private readonly ISysScreenFeatureRepository _repo;

    public AssignFeaturesToScreenCommandHandler(ISysScreenFeatureRepository repo)
    {
        _repo = repo;
    }

    public async Task<bool> Handle(AssignFeaturesToScreenCommand request, CancellationToken cancellationToken)
    {
        await _repo.AssignFeaturesToScreenAsync(request.ScreenId, request.FeatureIds);
        return true;
    }
}
