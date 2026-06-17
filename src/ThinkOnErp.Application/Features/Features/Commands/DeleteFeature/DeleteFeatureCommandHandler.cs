using MediatR;
using ThinkOnErp.Domain.Interfaces;

namespace ThinkOnErp.Application.Features.Features.Commands.DeleteFeature;

public class DeleteFeatureCommandHandler : IRequestHandler<DeleteFeatureCommand, long>
{
    private readonly ISysFeatureRepository _featureRepository;

    public DeleteFeatureCommandHandler(ISysFeatureRepository featureRepository)
    {
        _featureRepository = featureRepository;
    }

    public async Task<long> Handle(DeleteFeatureCommand request, CancellationToken cancellationToken)
    {
        var feature = await _featureRepository.GetFeatureByIdAsync(request.FeatureId);
        if (feature == null)
            return 0;

        await _featureRepository.DeleteFeatureAsync(request.FeatureId, request.UpdateUser);
        return 1;
    }
}
