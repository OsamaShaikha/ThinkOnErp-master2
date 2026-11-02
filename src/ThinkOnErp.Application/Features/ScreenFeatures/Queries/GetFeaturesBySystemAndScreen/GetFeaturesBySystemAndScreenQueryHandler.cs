using MediatR;
using ThinkOnErp.Application.DTOs.Feature;
using ThinkOnErp.Domain.Interfaces;

namespace ThinkOnErp.Application.Features.ScreenFeatures.Queries.GetFeaturesBySystemAndScreen;

public class GetFeaturesBySystemAndScreenQueryHandler : IRequestHandler<GetFeaturesBySystemAndScreenQuery, List<FeatureDto>>
{
    private readonly ISysScreenFeatureRepository _repo;

    public GetFeaturesBySystemAndScreenQueryHandler(ISysScreenFeatureRepository repo)
    {
        _repo = repo;
    }

    public async Task<List<FeatureDto>> Handle(GetFeaturesBySystemAndScreenQuery request, CancellationToken cancellationToken)
    {
        var features = await _repo.GetFeaturesBySystemAndScreenAsync(request.SystemId, request.ScreenId);
        return features.Select(f => new FeatureDto
        {
            Id = f.Id,
            FeatureCode = f.FeatureCode,
            FeatureName = f.FeatureName,
            FeatureNameE = f.FeatureNameE,
            Description = f.Description,
            DescriptionE = f.DescriptionE,
            Icon = f.Icon,
            DisplayOrder = f.DisplayOrder,
            IsActive = f.IsActive,
            CreationUser = f.CreationUser,
            CreationDate = f.CreationDate,
            UpdateUser = f.UpdateUser,
            UpdateDate = f.UpdateDate
        }).ToList();
    }
}
